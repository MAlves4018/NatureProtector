using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.Fakes;

internal sealed class FakeInfluxWriteService : IInfluxWriteService
{
    public List<EventEnvelope<SensorReadingProducedPayload>> AcceptedReadings { get; } = [];
    public List<(Guid AreaId, Guid SensorId, RiskAssessment Assessment)> RiskAssessments { get; } = [];
    public List<(Guid AreaId, int AssessmentCount, AreaRiskSnapshot Snapshot)> AreaSnapshots { get; } = [];
    public List<InfluxTelemetryBatch> Batches { get; } = [];

    public Task WriteBatchAsync(
        InfluxTelemetryBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        Batches.Add(batch);

        foreach (var envelope in batch.AcceptedReadings)
        {
            AcceptedReadings.Add(envelope);
        }

        foreach (var write in batch.RiskAssessments)
        {
            RiskAssessments.Add((write.AreaId, write.SensorId, write.Assessment));
        }

        foreach (var write in batch.AreaRiskSnapshots)
        {
            AreaSnapshots.Add((write.AreaId, write.AssessmentCount, write.Snapshot));
        }

        return Task.CompletedTask;
    }

    public Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(envelope),
            cancellationToken);
    }

    public Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddRiskAssessment(areaId, sensorId, assessment),
            cancellationToken);
    }

    public Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddAreaRiskSnapshot(areaId, assessmentCount, snapshot),
            cancellationToken);
    }
}
