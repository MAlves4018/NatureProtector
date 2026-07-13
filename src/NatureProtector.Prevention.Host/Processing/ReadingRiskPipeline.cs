using System.Diagnostics;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Readings;
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
    IRiskEligibilityService riskEligibilityService,
    IDailyCellStateRepository dailyCellStateRepository,
    IRiskScoringService riskScoringService,
    IRiskAssessmentRepository riskAssessmentRepository,
    IAreaRiskSnapshotService areaRiskSnapshotService,
    IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
    IAreaOperationalProjectionStore areaOperationalProjectionStore,
    IInfluxWriteService influxWriteService,
    ILogger<ReadingRiskPipeline> logger,
    ICycleProjectionCoordinator? cycleProjectionCoordinator = null)
{
    /// <summary>
    /// Processa uma leitura aceite ao longo de todo o fluxo de risco.
    /// </summary>
    public async Task ProcessAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var operationalEvent = OperationalEvent.FromEnvelope(envelope);
        var normalizedReading = NormalizedReading.FromOperationalEvent(operationalEvent);
        var temporalCycle = operationalEvent.CycleIndex.HasValue && cycleProjectionCoordinator is not null;
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.pipeline.process");
        activity?.SetTag(TelemetryTags.EventId, normalizedReading.EventId);
        activity?.SetTag(TelemetryTags.CorrelationId, normalizedReading.CorrelationId);
        activity?.SetTag(TelemetryTags.AreaId, normalizedReading.AreaId);
        activity?.SetTag(TelemetryTags.SensorId, normalizedReading.SensorId);
        activity?.SetTag(TelemetryTags.SensorName, normalizedReading.SensorName);
        activity?.SetTag(TelemetryTags.MetricType, normalizedReading.MetricType);

        var pipelineStopwatch = Stopwatch.StartNew();

        var acceptedReadingPersistStopwatch = Stopwatch.StartNew();
        await acceptedReadingRepository.AddAsync(envelope, cancellationToken);
        acceptedReadingPersistStopwatch.Stop();
        logger.LogDebug(
            "accepted_reading_persist_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            acceptedReadingPersistStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            normalizedReading.SensorId);
        var influxBatch = new InfluxTelemetryBatch()
            .AddAcceptedReading(envelope);

        // The pipeline now crosses an explicit internal boundary before risk
        // evaluation: transport envelope -> normalized reading -> eligibility
        // decision -> risk input. The current implementation stays permissive
        // for compatibility with the baseline demo.
        var eligibility = await riskEligibilityService.EvaluateAsync(
            normalizedReading,
            cancellationToken);

        if (eligibility.Status == RiskInputStatus.Blocked || !eligibility.IsEligible)
        {
            if (temporalCycle)
            {
                var finalized = await cycleProjectionCoordinator!.RecordAsync(
                    operationalEvent.SimulationRunId, operationalEvent.CycleIndex!.Value,
                    normalizedReading.AreaId, normalizedReading.SensorId, normalizedReading.EventId,
                    normalizedReading.EventTime, normalizedReading.Origin, CycleObservationOutcome.Blocked,
                    null, cancellationToken);
                await ApplyFinalizedCyclesAsync(finalized, cancellationToken);
            }
            var acceptedOnlyInfluxWriteStopwatch = Stopwatch.StartNew();
            await influxWriteService.WriteBatchAsync(influxBatch, cancellationToken);
            acceptedOnlyInfluxWriteStopwatch.Stop();
            pipelineStopwatch.Stop();

            logger.LogInformation(
                "pipeline_total_ms={PipelineTotalMs} | accepted_reading_persist_ms={AcceptedReadingPersistMs} | influx_batch_write_ms={InfluxBatchWriteMs} | influx_batch_points={InfluxBatchPoints} | influx_batch_accepted_readings={InfluxBatchAcceptedReadings} | influx_batch_risk_assessments={InfluxBatchRiskAssessments} | influx_batch_area_risk_snapshots={InfluxBatchAreaRiskSnapshots} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId} | Outcome=completed_without_risk | EligibilityReason={EligibilityReason} | EligibilityMessage={EligibilityMessage}",
                pipelineStopwatch.ElapsedMilliseconds,
                acceptedReadingPersistStopwatch.ElapsedMilliseconds,
                acceptedOnlyInfluxWriteStopwatch.ElapsedMilliseconds,
                influxBatch.PointCount,
                influxBatch.AcceptedReadingCount,
                influxBatch.RiskAssessmentCount,
                influxBatch.AreaRiskSnapshotCount,
                normalizedReading.EventId,
                normalizedReading.CorrelationId,
                normalizedReading.AreaId,
                normalizedReading.SensorId,
                eligibility.ReasonCode,
                eligibility.Message ?? "n/a");
            return;
        }

        var dailyStateLookup = await dailyCellStateRepository.GetForReadingAsync(
            normalizedReading,
            operationalEvent.SimulationRunId,
            cancellationToken);
        var riskInput = RiskInput.FromNormalizedReading(
            normalizedReading,
            eligibility,
            dailyStateLookup.State,
            operationalEvent.SimulationRunId,
            dailyStateLookup.GridCellId,
            dailyStateLookup.ConfigurationVersionId,
            dailyStateLookup.TerritorialContext);
        await dailyCellStateRepository.UpsertAsync(riskInput, cancellationToken);
        dailyStateLookup = await dailyCellStateRepository.GetForReadingAsync(
            normalizedReading,
            operationalEvent.SimulationRunId,
            cancellationToken);
        riskInput = RiskInput.FromNormalizedReading(
            normalizedReading,
            eligibility,
            dailyStateLookup.State,
            operationalEvent.SimulationRunId,
            dailyStateLookup.GridCellId,
            dailyStateLookup.ConfigurationVersionId,
            dailyStateLookup.TerritorialContext);
        if (riskInput.InputStatus == RiskInputStatus.Blocked)
        {
            if (temporalCycle)
            {
                var finalized = await cycleProjectionCoordinator!.RecordAsync(
                    operationalEvent.SimulationRunId, operationalEvent.CycleIndex!.Value,
                    normalizedReading.AreaId, normalizedReading.SensorId, normalizedReading.EventId,
                    normalizedReading.EventTime, normalizedReading.Origin, CycleObservationOutcome.Blocked,
                    null, cancellationToken);
                await ApplyFinalizedCyclesAsync(finalized, cancellationToken);
            }
            var blockedInfluxWriteStopwatch = Stopwatch.StartNew();
            await influxWriteService.WriteBatchAsync(influxBatch, cancellationToken);
            blockedInfluxWriteStopwatch.Stop();
            pipelineStopwatch.Stop();

            logger.LogInformation(
                "pipeline_total_ms={PipelineTotalMs} | accepted_reading_persist_ms={AcceptedReadingPersistMs} | influx_batch_write_ms={InfluxBatchWriteMs} | influx_batch_points={InfluxBatchPoints} | influx_batch_accepted_readings={InfluxBatchAcceptedReadings} | influx_batch_risk_assessments={InfluxBatchRiskAssessments} | influx_batch_area_risk_snapshots={InfluxBatchAreaRiskSnapshots} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId} | Outcome=blocked_without_risk | RiskInputStatus={RiskInputStatus} | CoverageFlag={CoverageFlag}",
                pipelineStopwatch.ElapsedMilliseconds,
                acceptedReadingPersistStopwatch.ElapsedMilliseconds,
                blockedInfluxWriteStopwatch.ElapsedMilliseconds,
                influxBatch.PointCount,
                influxBatch.AcceptedReadingCount,
                influxBatch.RiskAssessmentCount,
                influxBatch.AreaRiskSnapshotCount,
                normalizedReading.EventId,
                normalizedReading.CorrelationId,
                normalizedReading.AreaId,
                normalizedReading.SensorId,
                riskInput.InputStatus,
                riskInput.QualityFlags.Contains("low_coverage", StringComparer.OrdinalIgnoreCase) ? "low_coverage" : "n/a");
            return;
        }

        var assessment = riskScoringService.CreateAssessment(riskInput);

        var riskAssessmentPersistStopwatch = Stopwatch.StartNew();
        assessment = await riskAssessmentRepository.AddAsync(
            normalizedReading.AreaId,
            normalizedReading.SensorId,
            normalizedReading.EventId,
            assessment,
            cancellationToken,
            simulationRunId: operationalEvent.SimulationRunId);
        riskAssessmentPersistStopwatch.Stop();
        logger.LogDebug(
            "risk_assessment_persist_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            riskAssessmentPersistStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            normalizedReading.SensorId);

        if (temporalCycle)
        {
            influxBatch.AddRiskAssessment(
                normalizedReading.AreaId,
                normalizedReading.SensorId,
                assessment,
                normalizedReading.EventId,
                operationalEvent.SimulationRunId);
            var finalized = await cycleProjectionCoordinator!.RecordAsync(
                operationalEvent.SimulationRunId, operationalEvent.CycleIndex!.Value,
                normalizedReading.AreaId, normalizedReading.SensorId, normalizedReading.EventId,
                normalizedReading.EventTime, normalizedReading.Origin, CycleObservationOutcome.Eligible,
                assessment, cancellationToken);
            await ApplyFinalizedCyclesAsync(finalized, cancellationToken);
            await influxWriteService.WriteBatchAsync(influxBatch, cancellationToken);
            return;
        }

        var saveCellProjectionStopwatch = Stopwatch.StartNew();
        await areaOperationalProjectionStore.SaveCellAsync(
            normalizedReading.AreaId,
            normalizedReading.SensorId,
            assessment,
            cancellationToken);
        saveCellProjectionStopwatch.Stop();
        logger.LogDebug(
            "save_cell_projection_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId}",
            saveCellProjectionStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            normalizedReading.SensorId);
        influxBatch.AddRiskAssessment(
            normalizedReading.AreaId,
            normalizedReading.SensorId,
            assessment,
            normalizedReading.EventId,
            operationalEvent.SimulationRunId);

        // The current area aggregate is intentionally based on the latest
        // assessment known per sensor. Future models with temporal windows or
        // accumulated state should introduce a richer evaluation context rather
        // than expanding orchestration logic here.
        var getLatestByAreaStopwatch = Stopwatch.StartNew();
        var areaAssessments = await riskAssessmentRepository.GetLatestByAreaAsync(
            normalizedReading.AreaId,
            cancellationToken,
            simulationRunId: operationalEvent.SimulationRunId);
        getLatestByAreaStopwatch.Stop();
        logger.LogDebug(
            "get_latest_by_area_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | Count={Count}",
            getLatestByAreaStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            areaAssessments.Count);

        var buildSnapshotStopwatch = Stopwatch.StartNew();
        var computedSnapshot = areaRiskSnapshotService.BuildSnapshot(
            assessments: areaAssessments,
            snapshotTime: normalizedReading.EventTime);
        var snapshot = new AreaRiskSnapshot(
            id: normalizedReading.EventId,
            timestamp: computedSnapshot.Timestamp,
            aggregateRiskScore: computedSnapshot.AggregateRiskScore,
            summary: computedSnapshot.Summary);
        buildSnapshotStopwatch.Stop();
        logger.LogDebug(
            "build_snapshot_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | AssessmentCount={AssessmentCount}",
            buildSnapshotStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            areaAssessments.Count);

        var snapshotPersistStopwatch = Stopwatch.StartNew();
        await areaRiskSnapshotRepository.SaveAsync(
            normalizedReading.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken,
            simulationRunId: operationalEvent.SimulationRunId);
        snapshotPersistStopwatch.Stop();
        logger.LogDebug(
            "snapshot_persist_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | AssessmentCount={AssessmentCount}",
            snapshotPersistStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            areaAssessments.Count);
        influxBatch.AddAreaRiskSnapshot(
            normalizedReading.AreaId,
            areaAssessments.Count,
            snapshot,
            normalizedReading.EventId,
            operationalEvent.SimulationRunId);

        var saveAreaProjectionStopwatch = Stopwatch.StartNew();
        await areaOperationalProjectionStore.SaveAsync(
            normalizedReading.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken,
            simulationRunId: operationalEvent.SimulationRunId);
        saveAreaProjectionStopwatch.Stop();

        var influxBatchWriteStopwatch = Stopwatch.StartNew();
        await influxWriteService.WriteBatchAsync(influxBatch, cancellationToken);
        influxBatchWriteStopwatch.Stop();
        logger.LogDebug(
            "influx_batch_write_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | AreaId={AreaId} | SensorId={SensorId} | points={PointCount} | accepted_readings={AcceptedReadings} | risk_assessments={RiskAssessments} | area_risk_snapshots={AreaRiskSnapshots}",
            influxBatchWriteStopwatch.ElapsedMilliseconds,
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            normalizedReading.SensorId,
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
            normalizedReading.EventId,
            normalizedReading.CorrelationId,
            normalizedReading.AreaId,
            normalizedReading.SensorId,
            assessment.RiskScore,
            assessment.RiskLevel,
            snapshot.AggregateRiskScore,
            snapshot.AggregateRiskLevel,
            severity,
            areaAssessments.Count);
    }

    private async Task ApplyFinalizedCyclesAsync(
        IReadOnlyList<FinalizedCycleProjection> finalizations,
        CancellationToken cancellationToken)
    {
        foreach (var finalized in finalizations.Where(item => item.IsOperational))
        {
            await areaOperationalProjectionStore.SaveAsync(
                finalized.AreaId,
                finalized.Snapshot,
                finalized.EligibleCount,
                cancellationToken,
                finalized.SimulationRunId,
                finalized.CycleIndex);
        }
    }
}
