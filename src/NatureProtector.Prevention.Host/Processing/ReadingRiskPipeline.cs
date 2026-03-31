using NatureProtector.Core.Primitives;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public sealed class ReadingRiskPipeline(
    IAcceptedReadingRepository acceptedReadingRepository,
    ISimpleRiskScoringService riskScoringService,
    IRiskAssessmentRepository riskAssessmentRepository,
    IAreaRiskSnapshotService areaRiskSnapshotService,
    IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
    IInfluxWriteService influxWriteService,
    ILogger<ReadingRiskPipeline> logger)
{
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
            assessment,
            cancellationToken);

        await influxWriteService.WriteRiskAssessmentAsync(
            envelope.AreaId,
            envelope.Payload.SensorId,
            assessment,
            cancellationToken);

        var areaAssessments = await riskAssessmentRepository.GetByAreaAsync(
            envelope.AreaId,
            cancellationToken);

        var snapshot = areaRiskSnapshotService.BuildSnapshot(
            assessments: areaAssessments,
            snapshotTime: envelope.EventTime);

        await areaRiskSnapshotRepository.SaveAsync(
            envelope.AreaId,
            snapshot,
            cancellationToken);

        await influxWriteService.WriteAreaRiskSnapshotAsync(
            envelope.AreaId,
            areaAssessments.Count,
            snapshot,
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
