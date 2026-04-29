using System.Diagnostics;
using NatureProtector.Core.Primitives;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Prevention.Host.Processing;

/*
 * Este componente transforma uma leitura aceite em persistência operacional,
 * avaliação de risco, snapshot agregado e projeções.
 *
 * Rationale:
 * - O fluxo principal da prevenção precisa de ficar explícito e linear para ser
 *   fácil de seguir e testar.
 * - O componente encadeia blocos especializados sem lhes delegar a
 *   responsabilidade de conhecer o fluxo completo.
 *
 * Design considerations:
 * - A mesma leitura alimenta PostgreSQL e InfluxDB para fins distintos:
 *   rastreabilidade operacional e observabilidade temporal.
 * - O snapshot de área é recalculado a partir das avaliações mais recentes por
 *   sensor.
 * - As projeções operacionais são atualizadas no fim para refletirem o estado
 *   mais recente da área.
 */

public sealed class ReadingRiskPipeline(
    IAcceptedReadingRepository acceptedReadingRepository,
    IRiskScoringService riskScoringService,
    IRiskAssessmentRepository riskAssessmentRepository,
    IAreaRiskSnapshotService areaRiskSnapshotService,
    IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
    IAreaOperationalProjectionStore areaOperationalProjectionStore,
    IInfluxWriteService influxWriteService,
    ILogger<ReadingRiskPipeline> logger)
{
    /// <summary>
    /// Processa uma leitura aceite ao longo de todo o fluxo de risco.
    /// </summary>
    public async Task ProcessAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.pipeline.process");
        activity?.SetTag(TelemetryTags.EventId, envelope.EventId);
        activity?.SetTag(TelemetryTags.CorrelationId, envelope.CorrelationId);
        activity?.SetTag(TelemetryTags.AreaId, envelope.AreaId);
        activity?.SetTag(TelemetryTags.SensorId, envelope.Payload.SensorId);
        activity?.SetTag(TelemetryTags.SensorName, envelope.Payload.SensorName);
        activity?.SetTag(TelemetryTags.MetricType, envelope.Payload.MetricType);

        var pipelineStopwatch = Stopwatch.StartNew();

        var acceptedReadingPersistStopwatch = Stopwatch.StartNew();
        await acceptedReadingRepository.AddAsync(envelope, cancellationToken);
        acceptedReadingPersistStopwatch.Stop();
        logger.LogDebug(
            "accepted_reading_persist_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            acceptedReadingPersistStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId);
        var influxBatch = new InfluxTelemetryBatch()
            .AddAcceptedReading(envelope);

        // The pipeline depends on a model-agnostic scoring contract so the current
        // threshold baseline can later evolve into stateful wildfire indices
        // without coupling scoring rules to broker, persistence or telemetry code.
        var assessment = riskScoringService.CreateAssessment(
            areaId: envelope.AreaId,
            sensorId: envelope.Payload.SensorId,
            sourceEventId: envelope.EventId,
            metricType: envelope.Payload.MetricType,
            value: envelope.Payload.Value,
            assessedAt: envelope.EventTime);

        var riskAssessmentPersistStopwatch = Stopwatch.StartNew();
        await riskAssessmentRepository.AddAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            envelope.EventId,
            assessment,
            cancellationToken);
        riskAssessmentPersistStopwatch.Stop();
        logger.LogDebug(
            "risk_assessment_persist_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            riskAssessmentPersistStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId);

        var saveCellProjectionStopwatch = Stopwatch.StartNew();
        await areaOperationalProjectionStore.SaveCellAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment,
            cancellationToken);
        saveCellProjectionStopwatch.Stop();
        logger.LogDebug(
            "save_cell_projection_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            saveCellProjectionStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId);
        influxBatch.AddRiskAssessment(
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment);

        // The current area aggregate is intentionally based on the latest
        // assessment known per sensor. Future models with temporal windows or
        // accumulated state should introduce a richer evaluation context rather
        // than expanding orchestration logic here.
        var getLatestByAreaStopwatch = Stopwatch.StartNew();
        var areaAssessments = await riskAssessmentRepository.GetLatestByAreaAsync(
            envelope.AreaId,
            cancellationToken);
        getLatestByAreaStopwatch.Stop();
        logger.LogDebug(
            "get_latest_by_area_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | Count={Count}",
            getLatestByAreaStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            areaAssessments.Count);

        var buildSnapshotStopwatch = Stopwatch.StartNew();
        var snapshot = areaRiskSnapshotService.BuildSnapshot(
            assessments: areaAssessments,
            snapshotTime: envelope.EventTime);
        buildSnapshotStopwatch.Stop();
        logger.LogDebug(
            "build_snapshot_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | AssessmentCount={AssessmentCount}",
            buildSnapshotStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            areaAssessments.Count);

        var snapshotPersistStopwatch = Stopwatch.StartNew();
        await areaRiskSnapshotRepository.SaveAsync(
            envelope.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken);
        snapshotPersistStopwatch.Stop();
        logger.LogDebug(
            "snapshot_persist_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | AssessmentCount={AssessmentCount}",
            snapshotPersistStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            areaAssessments.Count);
        influxBatch.AddAreaRiskSnapshot(
            envelope.AreaId,
            areaAssessments.Count,
            snapshot);

        var saveAreaProjectionStopwatch = Stopwatch.StartNew();
        await areaOperationalProjectionStore.SaveAsync(
            envelope.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken);
        saveAreaProjectionStopwatch.Stop();

        var influxBatchWriteStopwatch = Stopwatch.StartNew();
        await influxWriteService.WriteBatchAsync(influxBatch, cancellationToken);
        influxBatchWriteStopwatch.Stop();
        logger.LogDebug(
            "influx_batch_write_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId} | points={PointCount} | accepted_readings={AcceptedReadings} | risk_assessments={RiskAssessments} | area_risk_snapshots={AreaRiskSnapshots}",
            influxBatchWriteStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId,
            influxBatch.PointCount,
            influxBatch.AcceptedReadingCount,
            influxBatch.RiskAssessmentCount,
            influxBatch.AreaRiskSnapshotCount);

        var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);
        pipelineStopwatch.Stop();

        logger.LogInformation(
            "pipeline_total_ms={PipelineTotalMs} | accepted_reading_persist_ms={AcceptedReadingPersistMs} | risk_assessment_persist_ms={RiskAssessmentPersistMs} | save_cell_projection_ms={SaveCellProjectionMs} | get_latest_by_area_ms={GetLatestByAreaMs} | build_snapshot_ms={BuildSnapshotMs} | snapshot_persist_ms={SnapshotPersistMs} | save_area_projection_ms={SaveAreaProjectionMs} | influx_batch_write_ms={InfluxBatchWriteMs} | influx_batch_points={InfluxBatchPoints} | influx_batch_accepted_readings={InfluxBatchAcceptedReadings} | influx_batch_risk_assessments={InfluxBatchRiskAssessments} | influx_batch_area_risk_snapshots={InfluxBatchAreaRiskSnapshots} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId} | RiskScore={RiskScore:F2} | RiskLevel={RiskLevel} | SnapshotScore={SnapshotScore:F2} | SnapshotLevel={SnapshotLevel} | Severity={Severity} | AssessmentCount={AssessmentCount}",
            pipelineStopwatch.ElapsedMilliseconds,
            acceptedReadingPersistStopwatch.ElapsedMilliseconds,
            riskAssessmentPersistStopwatch.ElapsedMilliseconds,
            saveCellProjectionStopwatch.ElapsedMilliseconds,
            getLatestByAreaStopwatch.ElapsedMilliseconds,
            buildSnapshotStopwatch.ElapsedMilliseconds,
            snapshotPersistStopwatch.ElapsedMilliseconds,
            saveAreaProjectionStopwatch.ElapsedMilliseconds,
            influxBatchWriteStopwatch.ElapsedMilliseconds,
            influxBatch.PointCount,
            influxBatch.AcceptedReadingCount,
            influxBatch.RiskAssessmentCount,
            influxBatch.AreaRiskSnapshotCount,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment.RiskScore,
            assessment.RiskLevel,
            snapshot.AggregateRiskScore,
            snapshot.AggregateRiskLevel,
            severity,
            areaAssessments.Count);
    }
}
