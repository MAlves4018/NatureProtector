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

        await Task.Delay(20);

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
