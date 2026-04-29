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

    public Task WriteAcceptedReadingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AcceptedReadingCalls++;
        throw _exception;
    }

    public Task WriteRiskAssessmentAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RiskAssessmentCalls++;
        throw _exception;
    }

    public Task WriteAreaRiskSnapshotAsync(
        Guid areaId,
        int assessmentCount,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AreaRiskSnapshotCalls++;
        throw _exception;
    }
}
