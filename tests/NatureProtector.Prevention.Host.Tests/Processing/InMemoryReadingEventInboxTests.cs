using System.Collections.Concurrent;
using System.Reflection;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class InMemoryReadingEventInboxTests
{
    [Fact]
    public async Task TryStartDueRetryAsync_QuarantinesMalformedEnvelope_AndContinuesToNextRetry()
    {
        var inbox = new InMemoryReadingEventInbox();
        var malformedEnvelope = EnvelopeFactory.Create(eventId: Guid.NewGuid());
        var validEnvelope = EnvelopeFactory.Create(eventId: Guid.NewGuid());

        var malformedStoreResult = await inbox.StoreIncomingAsync(
            malformedEnvelope,
            JsonEventSerializer.SerializeToUtf8Bytes(malformedEnvelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        var validStoreResult = await inbox.StoreIncomingAsync(
            validEnvelope,
            JsonEventSerializer.SerializeToUtf8Bytes(validEnvelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.ScheduleRetryAsync(
            malformedStoreResult.Lease!,
            "timeout",
            "first failure",
            TimeSpan.Zero,
            CancellationToken.None);

        await inbox.ScheduleRetryAsync(
            validStoreResult.Lease!,
            "timeout",
            "second failure",
            TimeSpan.Zero,
            CancellationToken.None);

        SetNextAttemptNotBefore(
            inbox,
            malformedStoreResult.InboxEventId,
            new DateTimeOffset(2026, 5, 12, 10, 0, 0, TimeSpan.Zero));
        SetNextAttemptNotBefore(
            inbox,
            validStoreResult.InboxEventId,
            new DateTimeOffset(2026, 5, 12, 10, 0, 1, TimeSpan.Zero));
        CorruptEnvelope(inbox, malformedStoreResult.InboxEventId, "{ invalid");

        var workItem = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.NotNull(workItem);
        Assert.Equal(validEnvelope.EventId, workItem!.Envelope.EventId);

        var malformedEvent = Assert.Single(
            inbox.Events,
            x => x.InboxEventId == malformedStoreResult.InboxEventId);
        Assert.Equal(InboxEventStatus.Quarantined, malformedEvent.Status);
        Assert.Equal("invalid_retry_payload", malformedEvent.LastErrorCode);
        Assert.NotNull(malformedEvent.QuarantinedAt);

        var malformedAttempt = Assert.Single(
            inbox.Attempts,
            x => x.InboxEventId == malformedStoreResult.InboxEventId &&
                x.AttemptNumber == 2);
        Assert.Equal(ProcessingAttemptOutcome.Quarantined, malformedAttempt.Outcome);
        Assert.Equal("invalid_retry_payload", malformedAttempt.ErrorCode);

        var quarantine = Assert.Single(
            inbox.Quarantines,
            x => x.InboxEventId == malformedStoreResult.InboxEventId);
        Assert.Equal("invalid_retry_payload", quarantine.QuarantineCode);
    }

    [Fact]
    public async Task StoreIncomingAsync_DuplicateEventWithSamePayload_ReturnsExistingLeaseWithoutRejection()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var rawBody = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        var first = await inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "reading_risk_pipeline",
            CancellationToken.None);
        var duplicate = await inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(first.ShouldProcessNow);
        Assert.True(duplicate.IsDuplicate);
        Assert.False(duplicate.ShouldProcessNow);
        Assert.Equal(first.InboxEventId, duplicate.InboxEventId);
        Assert.Empty(inbox.Rejections);
        Assert.Single(inbox.Events);
    }

    [Fact]
    public async Task StoreIncomingAsync_DuplicateEventWithDifferentPayload_RecordsRejection()
    {
        var inbox = new InMemoryReadingEventInbox();
        var eventId = Guid.NewGuid();
        var original = EnvelopeFactory.Create(eventId: eventId, value: 31.0);
        var conflicting = EnvelopeFactory.Create(eventId: eventId, value: 35.0);

        var first = await inbox.StoreIncomingAsync(
            original,
            JsonEventSerializer.SerializeToUtf8Bytes(original),
            "reading_risk_pipeline",
            CancellationToken.None);
        var duplicate = await inbox.StoreIncomingAsync(
            conflicting,
            JsonEventSerializer.SerializeToUtf8Bytes(conflicting),
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.True(duplicate.IsDuplicate);
        Assert.False(duplicate.ShouldProcessNow);
        Assert.Equal(first.InboxEventId, duplicate.InboxEventId);
        var rejection = Assert.Single(inbox.Rejections);
        Assert.Equal(first.InboxEventId, rejection.InboxEventId);
        Assert.Equal(eventId, rejection.EventId);
        Assert.Equal("duplicate_payload_mismatch", rejection.RejectionCode);
        Assert.Contains("different payload", rejection.RejectionReason);
        Assert.Equal("reading_risk_pipeline", rejection.Metadata?.Stage);
    }

    [Fact]
    public async Task TryStartDueRetryAsync_NoDueRetry_ReturnsNull()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        await inbox.ScheduleRetryAsync(
            storeResult.Lease!,
            "timeout",
            "temporary failure",
            TimeSpan.FromHours(1),
            CancellationToken.None);

        var workItem = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.Null(workItem);
        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(InboxEventStatus.RetryPending, inboxEvent.Status);
        Assert.NotNull(inboxEvent.NextAttemptNotBefore);
    }

    [Fact]
    public async Task TryStartDueRetryAsync_ExpiredProcessingLease_StartsRecoveredAttempt()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        SetLastAttemptAt(
            inbox,
            stored.InboxEventId,
            DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(30)));

        var workItem = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None,
            TimeSpan.FromMinutes(5),
            maxProcessingAttempts: 3);

        Assert.NotNull(workItem);
        Assert.Equal(envelope.EventId, workItem!.Envelope.EventId);
        Assert.Equal(2, workItem.Lease.AttemptNumber);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);

        var attempts = inbox.Attempts.OrderBy(attempt => attempt.AttemptNumber).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.Equal(ProcessingAttemptOutcome.RetryScheduled, attempts[0].Outcome);
        Assert.Equal("processing_lease_expired", attempts[0].ErrorCode);
        Assert.Equal(ProcessingAttemptOutcome.Started, attempts[1].Outcome);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("schedule_retry")]
    [InlineData("quarantine")]
    public async Task StaleProcessingLeaseFinalization_AfterRecovery_DoesNotClobberCurrentAttempt(
        string operation)
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        SetLastAttemptAt(
            inbox,
            stored.InboxEventId,
            DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(30)));

        var recovered = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None,
            TimeSpan.FromMinutes(5),
            maxProcessingAttempts: 3);

        Assert.NotNull(recovered);

        switch (operation)
        {
            case "complete":
                await inbox.CompleteProcessingAsync(stored.Lease!, CancellationToken.None);
                break;
            case "schedule_retry":
                await inbox.ScheduleRetryAsync(
                    stored.Lease!,
                    "late_timeout",
                    "late failure from expired attempt",
                    TimeSpan.Zero,
                    CancellationToken.None);
                break;
            case "quarantine":
                await inbox.QuarantineProcessingAsync(
                    stored.Lease!,
                    "late_permanent_failure",
                    "late permanent failure from expired attempt",
                    "permanent_failure",
                    "Late expired attempt should not quarantine current processing.",
                    null,
                    CancellationToken.None);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{operation}'.");
        }

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);
        Assert.Null(inboxEvent.LastProcessedAt);
        Assert.Null(inboxEvent.QuarantinedAt);
        Assert.Null(inboxEvent.LastErrorCode);
        Assert.Null(inboxEvent.LastErrorMessage);

        var attempts = inbox.Attempts.OrderBy(attempt => attempt.AttemptNumber).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.Equal(ProcessingAttemptOutcome.RetryScheduled, attempts[0].Outcome);
        Assert.Equal("processing_lease_expired", attempts[0].ErrorCode);
        Assert.Equal(ProcessingAttemptOutcome.Started, attempts[1].Outcome);
        Assert.Empty(inbox.Quarantines);
    }

    private static void CorruptEnvelope(
        InMemoryReadingEventInbox inbox,
        Guid inboxEventId,
        string corruptedEnvelopeJson)
    {
        var eventsByInboxId = GetPrivateDictionary(
            inbox,
            "_eventsByInboxId");
        var eventsByEventId = GetPrivateDictionary(
            inbox,
            "_eventsByEventId");
        var current = eventsByInboxId[inboxEventId];
        var corrupted = current with { EnvelopeJson = corruptedEnvelopeJson };

        eventsByInboxId[inboxEventId] = corrupted;
        eventsByEventId[current.EventId] = corrupted;
    }

    private static void SetNextAttemptNotBefore(
        InMemoryReadingEventInbox inbox,
        Guid inboxEventId,
        DateTimeOffset nextAttemptNotBefore)
    {
        var eventsByInboxId = GetPrivateDictionary(
            inbox,
            "_eventsByInboxId");
        var eventsByEventId = GetPrivateDictionary(
            inbox,
            "_eventsByEventId");
        var current = eventsByInboxId[inboxEventId];
        var updated = current with { NextAttemptNotBefore = nextAttemptNotBefore };

        eventsByInboxId[inboxEventId] = updated;
        eventsByEventId[current.EventId] = updated;
    }

    private static void SetLastAttemptAt(
        InMemoryReadingEventInbox inbox,
        Guid inboxEventId,
        DateTimeOffset lastAttemptAt)
    {
        var eventsByInboxId = GetPrivateDictionary(
            inbox,
            "_eventsByInboxId");
        var eventsByEventId = GetPrivateDictionary(
            inbox,
            "_eventsByEventId");
        var current = eventsByInboxId[inboxEventId];
        var updated = current with { LastAttemptAt = lastAttemptAt };

        eventsByInboxId[inboxEventId] = updated;
        eventsByEventId[current.EventId] = updated;
    }

    private static ConcurrentDictionary<Guid, InMemoryReadingEventInbox.InMemoryInboxEvent> GetPrivateDictionary(
        InMemoryReadingEventInbox inbox,
        string fieldName)
    {
        var field = typeof(InMemoryReadingEventInbox).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        return field.GetValue(inbox) as ConcurrentDictionary<Guid, InMemoryReadingEventInbox.InMemoryInboxEvent>
            ?? throw new InvalidOperationException($"Field '{fieldName}' did not expose the expected dictionary.");
    }
}
