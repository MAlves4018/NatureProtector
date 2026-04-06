using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.IntegrationTests.Fakes;

internal sealed class FakeInfluxWriteService : IInfluxWriteService
{
    public List<EventEnvelope<SensorReadingProducedPayload>> AcceptedReadings { get; } = [];
    public List<(Guid AreaId, Guid SensorId, RiskAssessment Assessment)> RiskAssessments { get; } = [];
    public List<(Guid AreaId, int AssessmentCount, AreaRiskSnapshot Snapshot)> AreaSnapshots { get; } = [];

    public Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AcceptedReadings.Add(envelope);
        return Task.CompletedTask;
    }

    public Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RiskAssessments.Add((areaId, sensorId, assessment));
        return Task.CompletedTask;
    }

    public Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AreaSnapshots.Add((areaId, assessmentCount, snapshot));
        return Task.CompletedTask;
    }
}
