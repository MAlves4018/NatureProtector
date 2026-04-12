using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public sealed record InboxProcessingLease(
    Guid InboxEventId,
    Guid AttemptId,
    int AttemptNumber,
    string Stage);

public sealed record InboxStoreResult(
    Guid InboxEventId,
    InboxEventStatus Status,
    bool IsDuplicate,
    bool ShouldProcessNow,
    InboxProcessingLease? Lease);

public sealed record InboxRetryWorkItem(
    EventEnvelope<SensorReadingProducedPayload> Envelope,
    InboxProcessingLease Lease);

public enum ProcessingFailureKind
{
    Unknown = 0,
    Transient = 1,
    Permanent = 2
}

public sealed record ProcessingFailureClassification(
    ProcessingFailureKind Kind,
    string ErrorCode)
{
    public bool IsRetryable => Kind != ProcessingFailureKind.Permanent;
}
