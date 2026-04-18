using System.Diagnostics;
using NatureProtector.Core.Primitives;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

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
    ISimpleRiskScoringService riskScoringService,
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

        var acceptedReadingInfluxStopwatch = Stopwatch.StartNew();
        await influxWriteService.WriteAcceptedReadingAsync(envelope, cancellationToken);
        acceptedReadingInfluxStopwatch.Stop();
        logger.LogDebug(
            "accepted_reading_influx_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            acceptedReadingInfluxStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId);

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

        var riskAssessmentInfluxStopwatch = Stopwatch.StartNew();
        await influxWriteService.WriteRiskAssessmentAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment,
            cancellationToken);
        riskAssessmentInfluxStopwatch.Stop();
        logger.LogDebug(
            "risk_assessment_influx_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            riskAssessmentInfluxStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            envelope.Payload.SensorId);

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

        var snapshotInfluxStopwatch = Stopwatch.StartNew();
        await influxWriteService.WriteAreaRiskSnapshotAsync(
            envelope.AreaId,
            areaAssessments.Count,
            snapshot,
            cancellationToken);
        snapshotInfluxStopwatch.Stop();
        logger.LogDebug(
            "snapshot_influx_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | AssessmentCount={AssessmentCount}",
            snapshotInfluxStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.AreaId,
            areaAssessments.Count);

        var saveAreaProjectionStopwatch = Stopwatch.StartNew();
        await areaOperationalProjectionStore.SaveAsync(
            envelope.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken);
        saveAreaProjectionStopwatch.Stop();

        var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);
        pipelineStopwatch.Stop();

        logger.LogInformation(
            "pipeline_total_ms={PipelineTotalMs} | accepted_reading_persist_ms={AcceptedReadingPersistMs} | accepted_reading_influx_ms={AcceptedReadingInfluxMs} | risk_assessment_persist_ms={RiskAssessmentPersistMs} | save_cell_projection_ms={SaveCellProjectionMs} | risk_assessment_influx_ms={RiskAssessmentInfluxMs} | get_latest_by_area_ms={GetLatestByAreaMs} | build_snapshot_ms={BuildSnapshotMs} | snapshot_persist_ms={SnapshotPersistMs} | snapshot_influx_ms={SnapshotInfluxMs} | save_area_projection_ms={SaveAreaProjectionMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId} | RiskScore={RiskScore:F2} | RiskLevel={RiskLevel} | SnapshotScore={SnapshotScore:F2} | SnapshotLevel={SnapshotLevel} | Severity={Severity} | AssessmentCount={AssessmentCount}",
            pipelineStopwatch.ElapsedMilliseconds,
            acceptedReadingPersistStopwatch.ElapsedMilliseconds,
            acceptedReadingInfluxStopwatch.ElapsedMilliseconds,
            riskAssessmentPersistStopwatch.ElapsedMilliseconds,
            saveCellProjectionStopwatch.ElapsedMilliseconds,
            riskAssessmentInfluxStopwatch.ElapsedMilliseconds,
            getLatestByAreaStopwatch.ElapsedMilliseconds,
            buildSnapshotStopwatch.ElapsedMilliseconds,
            snapshotPersistStopwatch.ElapsedMilliseconds,
            snapshotInfluxStopwatch.ElapsedMilliseconds,
            saveAreaProjectionStopwatch.ElapsedMilliseconds,
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
