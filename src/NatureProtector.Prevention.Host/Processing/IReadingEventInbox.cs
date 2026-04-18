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
        RejectedEventMetadata? metadata,
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

public sealed record RejectedEventMetadata(
    Guid? EventId,
    string? CorrelationId,
    string? Producer,
    string? EventType,
    Guid? AreaId,
    string? SchemaVersion,
    Guid? SensorId,
    string? SensorName,
    string? MetricType,
    string? OperationalState,
    string? Stage,
    ulong? DeliveryTag);