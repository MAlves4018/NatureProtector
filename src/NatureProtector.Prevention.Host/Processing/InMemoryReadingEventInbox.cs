using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Text;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public sealed class InMemoryReadingEventInbox : IReadingEventInbox
{
    private const string InvalidRetryPayloadCode = "invalid_retry_payload";
    private const string InvalidRetryPayloadReason = "Retry inbox event envelope could not be deserialized.";
    private const string ProcessingLeaseExpiredCode = "processing_lease_expired";
    private const string ProcessingLeaseExpiredReason = "Processing lease expired before the attempt completed.";

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<Guid, InMemoryInboxEvent> _eventsByEventId = new();
    private readonly ConcurrentDictionary<Guid, InMemoryInboxEvent> _eventsByInboxId = new();
    private readonly ConcurrentDictionary<Guid, InMemoryProcessingAttempt> _attemptsById = new();
    private readonly List<InMemoryRejectedEvent> _rejections = [];
    private readonly List<InMemoryQuarantinedEvent> _quarantines = [];

    public IReadOnlyCollection<InMemoryInboxEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _eventsByInboxId.Values
                    .OrderBy(entity => entity.ReceivedAt)
                    .ToArray();
            }
        }
    }

    public IReadOnlyCollection<InMemoryProcessingAttempt> Attempts
    {
        get
        {
            lock (_gate)
            {
                return _attemptsById.Values
                    .OrderBy(entity => entity.StartedAt)
                    .ToArray();
            }
        }
    }

    public IReadOnlyCollection<InMemoryRejectedEvent> Rejections
    {
        get
        {
            lock (_gate)
            {
                return _rejections.ToArray();
            }
        }
    }

    public IReadOnlyCollection<InMemoryQuarantinedEvent> Quarantines
    {
        get
        {
            lock (_gate)
            {
                return _quarantines.ToArray();
            }
        }
    }

    public Task<InboxStoreResult> StoreIncomingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        ReadOnlyMemory<byte> rawBody,
        string stage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_eventsByEventId.TryGetValue(envelope.EventId, out var existing))
            {
                var incomingEnvelopeJson = JsonEventSerializer.SerializeToString(envelope);

                if (!string.Equals(existing.EnvelopeJson, incomingEnvelopeJson, StringComparison.Ordinal))
                {
                    _rejections.Add(new InMemoryRejectedEvent(
                        Guid.NewGuid(),
                        existing.InboxEventId,
                        envelope.EventId,
                        "duplicate_payload_mismatch",
                        "Received a duplicate event id with a different payload.",
                        DateTimeOffset.UtcNow,
                        Encoding.UTF8.GetString(rawBody.Span),
                        new RejectedEventMetadata(
                            EventId: envelope.EventId,
                            CorrelationId: envelope.CorrelationId,
                            Producer: envelope.Producer,
                            EventType: envelope.EventType,
                            AreaId: envelope.AreaId,
                            SchemaVersion: envelope.SchemaVersion,
                            SensorId: envelope.Payload.SensorId,
                            SensorName: envelope.Payload.SensorName,
                            MetricType: envelope.Payload.MetricType.ToString(),
                            OperationalState: envelope.Payload.OperationalState.ToString(),
                            Stage: stage,
                            DeliveryTag: null)));
                }

                return Task.FromResult(new InboxStoreResult(
                    existing.InboxEventId,
                    existing.Status,
                    true,
                    false,
                    null));
            }

            var receivedAt = DateTimeOffset.UtcNow;
            var inboxEventId = Guid.NewGuid();
            var attemptId = Guid.NewGuid();
            const int attemptNumber = 1;
            var envelopeJson = JsonEventSerializer.SerializeToString(envelope);

            var inboxEvent = new InMemoryInboxEvent(
                inboxEventId,
                envelope.EventId,
                InboxEventStatus.Processing,
                receivedAt,
                envelopeJson,
                JsonEventSerializer.SerializeToString(envelope.Payload),
                attemptNumber,
                receivedAt,
                null,
                null,
                null,
                null,
                null);

            var attempt = new InMemoryProcessingAttempt(
                attemptId,
                inboxEventId,
                attemptNumber,
                stage,
                receivedAt,
                null,
                ProcessingAttemptOutcome.Started,
                null,
                null);

            _eventsByEventId[envelope.EventId] = inboxEvent;
            _eventsByInboxId[inboxEventId] = inboxEvent;
            _attemptsById[attemptId] = attempt;

            return Task.FromResult(new InboxStoreResult(
                inboxEventId,
                InboxEventStatus.Processing,
                false,
                true,
                new InboxProcessingLease(inboxEventId, attemptId, attemptNumber, stage)));
        }
    }

    public Task StoreRejectedAsync(ReadOnlyMemory<byte> rawBody,
        string rejectionCode,
        string rejectionReason,
        RejectedEventMetadata? metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _rejections.Add(new InMemoryRejectedEvent(
                Guid.NewGuid(),
                null,
                metadata?.EventId,
                rejectionCode,
                rejectionReason,
                DateTimeOffset.UtcNow,
                Encoding.UTF8.GetString(rawBody.Span),
                metadata));
        }

        return Task.CompletedTask;
    }

    public Task CompleteProcessingAsync(InboxProcessingLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var inboxEvent = _eventsByInboxId[lease.InboxEventId];
            _eventsByInboxId[lease.InboxEventId] = inboxEvent with
            {
                Status = InboxEventStatus.Processed,
                LastAttemptAt = now,
                LastProcessedAt = now,
                NextAttemptNotBefore = null,
                QuarantinedAt = null,
                LastErrorCode = null,
                LastErrorMessage = null
            };

            _eventsByEventId[inboxEvent.EventId] = _eventsByInboxId[lease.InboxEventId];

            var attempt = _attemptsById[lease.AttemptId];
            _attemptsById[lease.AttemptId] = attempt with
            {
                FinishedAt = now,
                Outcome = ProcessingAttemptOutcome.Succeeded,
                ErrorCode = null,
                ErrorMessage = null
            };
        }

        return Task.CompletedTask;
    }

    public Task ScheduleRetryAsync(
        InboxProcessingLease lease,
        string errorCode,
        string errorMessage,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var inboxEvent = _eventsByInboxId[lease.InboxEventId];
            _eventsByInboxId[lease.InboxEventId] = inboxEvent with
            {
                Status = InboxEventStatus.RetryPending,
                LastAttemptAt = now,
                LastProcessedAt = null,
                NextAttemptNotBefore = now.Add(retryDelay),
                QuarantinedAt = null,
                LastErrorCode = errorCode,
                LastErrorMessage = errorMessage
            };

            _eventsByEventId[inboxEvent.EventId] = _eventsByInboxId[lease.InboxEventId];

            var attempt = _attemptsById[lease.AttemptId];
            _attemptsById[lease.AttemptId] = attempt with
            {
                FinishedAt = now,
                Outcome = ProcessingAttemptOutcome.RetryScheduled,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }

        return Task.CompletedTask;
    }

    public Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
        string stage,
        CancellationToken cancellationToken,
        TimeSpan? processingLeaseTimeout = null,
        int? maxProcessingAttempts = null)
    {
        lock (_gate)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var now = DateTimeOffset.UtcNow;
                var recoverStaleProcessing = processingLeaseTimeout is { } timeout && timeout > TimeSpan.Zero;
                var staleProcessingCutoff = recoverStaleProcessing
                    ? now.Subtract(processingLeaseTimeout!.Value)
                    : DateTimeOffset.MinValue;
                var dueEvent = _eventsByInboxId.Values
                    .Where(entity =>
                        (entity.Status == InboxEventStatus.RetryPending &&
                         entity.NextAttemptNotBefore is not null &&
                         entity.NextAttemptNotBefore <= now) ||
                        (recoverStaleProcessing &&
                         entity.Status == InboxEventStatus.Processing &&
                         entity.LastAttemptAt is not null &&
                         entity.LastAttemptAt <= staleProcessingCutoff))
                    .OrderBy(entity => entity.Status == InboxEventStatus.RetryPending ? 0 : 1)
                    .ThenBy(entity => entity.NextAttemptNotBefore ?? entity.LastAttemptAt ?? entity.ReceivedAt)
                    .FirstOrDefault();

                if (dueEvent is null)
                {
                    return Task.FromResult<InboxRetryWorkItem?>(null);
                }

                var isRecoveringStaleProcessing = dueEvent.Status == InboxEventStatus.Processing;
                var attemptNumber = dueEvent.AttemptCount + 1;
                var attemptId = Guid.NewGuid();

                if (isRecoveringStaleProcessing &&
                    maxProcessingAttempts.HasValue &&
                    dueEvent.AttemptCount >= maxProcessingAttempts.Value)
                {
                    QuarantineExpiredProcessingLease(dueEvent, now);
                    continue;
                }

                if (!TryDeserializeEnvelope(dueEvent.EnvelopeJson, out var envelope, out var errorMessage))
                {
                    var quarantinedEvent = dueEvent with
                    {
                        Status = InboxEventStatus.Quarantined,
                        AttemptCount = attemptNumber,
                        LastAttemptAt = now,
                        LastProcessedAt = null,
                        NextAttemptNotBefore = null,
                        QuarantinedAt = now,
                        LastErrorCode = InvalidRetryPayloadCode,
                        LastErrorMessage = errorMessage
                    };

                    _eventsByInboxId[dueEvent.InboxEventId] = quarantinedEvent;
                    _eventsByEventId[dueEvent.EventId] = quarantinedEvent;
                    _attemptsById[attemptId] = new InMemoryProcessingAttempt(
                        attemptId,
                        dueEvent.InboxEventId,
                        attemptNumber,
                        stage,
                        now,
                        now,
                        ProcessingAttemptOutcome.Quarantined,
                        InvalidRetryPayloadCode,
                        errorMessage);
                    _quarantines.Add(new InMemoryQuarantinedEvent(
                        Guid.NewGuid(),
                        dueEvent.InboxEventId,
                        dueEvent.EventId,
                        attemptNumber,
                        InvalidRetryPayloadCode,
                        InvalidRetryPayloadReason,
                        now));

                    continue;
                }

                if (isRecoveringStaleProcessing)
                {
                    var expiredAttempt = _attemptsById.Values.SingleOrDefault(
                        attempt =>
                            attempt.InboxEventId == dueEvent.InboxEventId &&
                            attempt.AttemptNumber == dueEvent.AttemptCount);

                    if (expiredAttempt is not null && expiredAttempt.Outcome == ProcessingAttemptOutcome.Started)
                    {
                        _attemptsById[expiredAttempt.AttemptId] = expiredAttempt with
                        {
                            FinishedAt = now,
                            Outcome = ProcessingAttemptOutcome.RetryScheduled,
                            ErrorCode = ProcessingLeaseExpiredCode,
                            ErrorMessage = ProcessingLeaseExpiredReason
                        };
                    }
                }

                var updatedEvent = dueEvent with
                {
                    Status = InboxEventStatus.Processing,
                    AttemptCount = attemptNumber,
                    LastAttemptAt = now,
                    NextAttemptNotBefore = null
                };

                _eventsByInboxId[dueEvent.InboxEventId] = updatedEvent;
                _eventsByEventId[dueEvent.EventId] = updatedEvent;
                _attemptsById[attemptId] = new InMemoryProcessingAttempt(
                    attemptId,
                    dueEvent.InboxEventId,
                    attemptNumber,
                    stage,
                    now,
                    null,
                    ProcessingAttemptOutcome.Started,
                    null,
                    null);

                return Task.FromResult<InboxRetryWorkItem?>(
                    new InboxRetryWorkItem(
                        envelope,
                        new InboxProcessingLease(dueEvent.InboxEventId, attemptId, attemptNumber, stage)));
            }
        }
    }

    public Task QuarantineProcessingAsync(
        InboxProcessingLease lease,
        string errorCode,
        string errorMessage,
        string quarantineCode,
        string quarantineReason,
        string? errorMetadataJson,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var inboxEvent = _eventsByInboxId[lease.InboxEventId];
            _eventsByInboxId[lease.InboxEventId] = inboxEvent with
            {
                Status = InboxEventStatus.Quarantined,
                LastAttemptAt = now,
                LastProcessedAt = null,
                NextAttemptNotBefore = null,
                QuarantinedAt = now,
                LastErrorCode = errorCode,
                LastErrorMessage = errorMessage
            };

            _eventsByEventId[inboxEvent.EventId] = _eventsByInboxId[lease.InboxEventId];

            var attempt = _attemptsById[lease.AttemptId];
            _attemptsById[lease.AttemptId] = attempt with
            {
                FinishedAt = now,
                Outcome = ProcessingAttemptOutcome.Quarantined,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };

            _quarantines.Add(new InMemoryQuarantinedEvent(
                Guid.NewGuid(),
                lease.InboxEventId,
                inboxEvent.EventId,
                lease.AttemptNumber,
                quarantineCode,
                quarantineReason,
                now));
        }

        return Task.CompletedTask;
    }

    private void QuarantineExpiredProcessingLease(
        InMemoryInboxEvent inboxEvent,
        DateTimeOffset now)
    {
        var quarantinedEvent = inboxEvent with
        {
            Status = InboxEventStatus.Quarantined,
            LastProcessedAt = null,
            NextAttemptNotBefore = null,
            QuarantinedAt = now,
            LastErrorCode = ProcessingLeaseExpiredCode,
            LastErrorMessage = ProcessingLeaseExpiredReason
        };

        _eventsByInboxId[inboxEvent.InboxEventId] = quarantinedEvent;
        _eventsByEventId[inboxEvent.EventId] = quarantinedEvent;

        var attempt = _attemptsById.Values.SingleOrDefault(
            item =>
                item.InboxEventId == inboxEvent.InboxEventId &&
                item.AttemptNumber == inboxEvent.AttemptCount);

        if (attempt is not null && attempt.Outcome == ProcessingAttemptOutcome.Started)
        {
            _attemptsById[attempt.AttemptId] = attempt with
            {
                FinishedAt = now,
                Outcome = ProcessingAttemptOutcome.Quarantined,
                ErrorCode = ProcessingLeaseExpiredCode,
                ErrorMessage = ProcessingLeaseExpiredReason
            };
        }

        _quarantines.Add(new InMemoryQuarantinedEvent(
            Guid.NewGuid(),
            inboxEvent.InboxEventId,
            inboxEvent.EventId,
            inboxEvent.AttemptCount,
            ProcessingLeaseExpiredCode,
            ProcessingLeaseExpiredReason,
            now));
    }

    public sealed record InMemoryInboxEvent(
        Guid InboxEventId,
        Guid EventId,
        InboxEventStatus Status,
        DateTimeOffset ReceivedAt,
        string EnvelopeJson,
        string PayloadJson,
        int AttemptCount,
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? LastProcessedAt,
        DateTimeOffset? NextAttemptNotBefore,
        DateTimeOffset? QuarantinedAt,
        string? LastErrorCode,
        string? LastErrorMessage);

    public sealed record InMemoryProcessingAttempt(
        Guid AttemptId,
        Guid InboxEventId,
        int AttemptNumber,
        string Stage,
        DateTimeOffset StartedAt,
        DateTimeOffset? FinishedAt,
        ProcessingAttemptOutcome Outcome,
        string? ErrorCode,
        string? ErrorMessage);

    public sealed record InMemoryRejectedEvent(
        Guid RejectionId,
        Guid? InboxEventId,
        Guid? EventId,
        string RejectionCode,
        string RejectionReason,
        DateTimeOffset RejectedAt,
        string RawBodyUtf8,
        RejectedEventMetadata? Metadata);

    public sealed record InMemoryQuarantinedEvent(
        Guid QuarantineId,
        Guid InboxEventId,
        Guid EventId,
        int FinalAttemptNumber,
        string QuarantineCode,
        string QuarantineReason,
        DateTimeOffset QuarantinedAt);

    private static bool TryDeserializeEnvelope(
        string envelopeJson,
        [NotNullWhen(true)] out EventEnvelope<SensorReadingProducedPayload>? envelope,
        out string errorMessage)
    {
        try
        {
            envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
                Encoding.UTF8.GetBytes(envelopeJson));

            if (envelope is null)
            {
                errorMessage = "Retry inbox event contains a null envelope.";
                return false;
            }

            if (envelope.Payload is null)
            {
                errorMessage = "Retry inbox event contains a null payload.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            envelope = null;
            errorMessage = $"{InvalidRetryPayloadReason} {ex.Message}".Trim();
            return false;
        }
    }
}
