using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public interface IReadingEventInbox
{
    Task<InboxStoreResult> StoreIncomingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        ReadOnlyMemory<byte> rawBody,
        string stage,
        CancellationToken cancellationToken);

    Task StoreRejectedAsync(
        ReadOnlyMemory<byte> rawBody,
        string rejectionCode,
        string rejectionReason,
        CancellationToken cancellationToken);

    Task CompleteProcessingAsync(
        InboxProcessingLease lease,
        CancellationToken cancellationToken);

    Task ScheduleRetryAsync(
        InboxProcessingLease lease,
        string errorCode,
        string errorMessage,
        TimeSpan retryDelay,
        CancellationToken cancellationToken);

    Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
        string stage,
        CancellationToken cancellationToken);

    Task QuarantineProcessingAsync(
        InboxProcessingLease lease,
        string errorCode,
        string errorMessage,
        string quarantineCode,
        string quarantineReason,
        CancellationToken cancellationToken);
}
