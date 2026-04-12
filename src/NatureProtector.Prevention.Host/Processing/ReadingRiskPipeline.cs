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

        await acceptedReadingRepository.AddAsync(envelope, cancellationToken);
        await influxWriteService.WriteAcceptedReadingAsync(envelope, cancellationToken);

        var assessment = riskScoringService.CreateAssessment(
            areaId: envelope.AreaId,
            sensorId: envelope.Payload.SensorId,
            sourceEventId: envelope.EventId,
            metricType: envelope.Payload.MetricType,
            value: envelope.Payload.Value,
            assessedAt: envelope.EventTime);

        await riskAssessmentRepository.AddAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            envelope.EventId,
            assessment,
            cancellationToken);

        // A projeção por célula é atualizada antes do snapshot agregado para que
        // o estado local já reflita a nova leitura quando a área for recalculada.
        await areaOperationalProjectionStore.SaveCellAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment,
            cancellationToken);

        await influxWriteService.WriteRiskAssessmentAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment,
            cancellationToken);

        var areaAssessments = await riskAssessmentRepository.GetLatestByAreaAsync(
            envelope.AreaId,
            cancellationToken);

        var snapshot = areaRiskSnapshotService.BuildSnapshot(
            assessments: areaAssessments,
            snapshotTime: envelope.EventTime);

        await areaRiskSnapshotRepository.SaveAsync(
            envelope.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken);

        await influxWriteService.WriteAreaRiskSnapshotAsync(
            envelope.AreaId,
            areaAssessments.Count,
            snapshot,
            cancellationToken);

        await areaOperationalProjectionStore.SaveAsync(
            envelope.AreaId,
            snapshot,
            areaAssessments.Count,
            cancellationToken);

        var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);

        logger.LogInformation(
            "Risk pipeline completed | AreaId={AreaId} | SensorId={SensorId} | RiskScore={RiskScore:F2} | RiskLevel={RiskLevel} | SnapshotScore={SnapshotScore:F2} | SnapshotLevel={SnapshotLevel} | Severity={Severity} | AssessmentCount={AssessmentCount}",
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
