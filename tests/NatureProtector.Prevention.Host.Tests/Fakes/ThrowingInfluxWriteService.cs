using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.Fakes;

internal sealed class ThrowingInfluxWriteService : IInfluxWriteService
{
    private readonly Exception _exception;

    public ThrowingInfluxWriteService(Exception? exception = null)
    {
        _exception = exception ?? new InvalidOperationException("Simulated InfluxDB failure.");
    }

    public int AcceptedReadingCalls { get; private set; }
    public int RiskAssessmentCalls { get; private set; }
    public int AreaRiskSnapshotCalls { get; private set; }
    public int BatchCalls { get; private set; }

    public Task WriteBatchAsync(
        InfluxTelemetryBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        BatchCalls++;
        throw _exception;
    }

    public Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        AcceptedReadingCalls++;
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
        RiskAssessmentCalls++;
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
        AreaRiskSnapshotCalls++;
        return WriteBatchAsync(
            new InfluxTelemetryBatch().AddAreaRiskSnapshot(areaId, assessmentCount, snapshot),
            cancellationToken);
    }
}
