using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public interface IProcessingFaultInjector
{
    ValueTask InjectAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        InboxProcessingLease lease,
        CancellationToken cancellationToken);
}
