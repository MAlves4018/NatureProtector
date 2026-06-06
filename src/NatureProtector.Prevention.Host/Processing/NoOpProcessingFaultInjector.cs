using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public sealed class NoOpProcessingFaultInjector : IProcessingFaultInjector
{
    public ValueTask InjectAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        InboxProcessingLease lease,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
