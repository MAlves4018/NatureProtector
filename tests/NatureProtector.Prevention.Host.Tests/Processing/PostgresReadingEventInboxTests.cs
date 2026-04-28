using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class PostgresReadingEventInboxTests
{
    [Fact]
    public async Task StoreIncomingAsync_NewEvent_StoresInboxEventAndFirstAttempt()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();

        var result = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.True(result.ShouldProcessNow);
        Assert.Equal(InboxEventStatus.Processing, result.Status);
        Assert.NotNull(result.Lease);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(envelope.EventId, inboxEvent.EventId);
        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(1, inboxEvent.AttemptCount);

        var attempt = Assert.Single(dbContext.ProcessingAttempts);
        Assert.Equal(result.Lease!.AttemptId, attempt.Id);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(ProcessingAttemptOutcome.Started, attempt.Outcome);
    }

    [Fact]
    public async Task StoreIncomingAsync_DuplicateDifferentPayload_PersistsRejection()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var eventId = Guid.NewGuid();
        var originalEnvelope = EnvelopeFactory.Create(eventId: eventId, value: 31.2);
        var conflictingEnvelope = EnvelopeFactory.Create(eventId: eventId, value: 45.0);

        await inbox.StoreIncomingAsync(
            originalEnvelope,
            JsonEventSerializer.SerializeToUtf8Bytes(originalEnvelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        var result = await inbox.StoreIncomingAsync(
            conflictingEnvelope,
            JsonEventSerializer.SerializeToUtf8Bytes(conflictingEnvelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.False(result.ShouldProcessNow);

        await using var dbContext = scope.CreateDbContext();
        Assert.Single(dbContext.InboxEvents);
        Assert.Single(dbContext.ProcessingAttempts);

        var rejection = Assert.Single(dbContext.RejectedEvents);
        Assert.Equal("duplicate_payload_mismatch", rejection.RejectionCode);
        Assert.Equal(eventId, rejection.EventId);
        Assert.Contains("\"stage\":\"reading_risk_pipeline\"", rejection.MetadataJson);
    }

    [Fact]
    public async Task StoreRejectedAsync_PersistsRejectedPayloadOutsideInbox()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var rawBody = Encoding.UTF8.GetBytes("{\"broken\":true}");

        await inbox.StoreRejectedAsync(
            rawBody,
            "invalid_operational_state",
            "Unsupported sensor state.",
            new RejectedEventMetadata(
                Guid.NewGuid(),
                "corr-1",
                "producer",
                "SensorReadingProduced",
                Guid.NewGuid(),
                "1.0",
                Guid.NewGuid(),
                "Sensor-01",
                "Temperature",
                "Unknown",
                "broker_receive",
                42),
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var rejection = Assert.Single(dbContext.RejectedEvents);
        Assert.Null(rejection.InboxEventId);
        Assert.Equal("invalid_operational_state", rejection.RejectionCode);
        Assert.Equal("{\"broken\":true}", rejection.RawBodyUtf8);
        Assert.Contains("\"Stage\":\"broker_receive\"", rejection.MetadataJson);
    }

    [Fact]
    public async Task CompleteProcessingAsync_LeaseMarkedProcessed_UpdatesEventAndAttempt()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.CompleteProcessingAsync(stored.Lease!, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(InboxEventStatus.Processed, inboxEvent.Status);
        Assert.NotNull(inboxEvent.LastProcessedAt);
        Assert.Null(inboxEvent.NextAttemptNotBefore);

        var attempt = Assert.Single(dbContext.ProcessingAttempts);
        Assert.Equal(ProcessingAttemptOutcome.Succeeded, attempt.Outcome);
        Assert.NotNull(attempt.FinishedAt);
    }

    [Fact]
    public async Task ScheduleRetryAsync_LeaseMarkedRetryPending_PersistsRetryMetadata()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.ScheduleRetryAsync(
            stored.Lease!,
            "timeout",
            "temporary downstream outage",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(InboxEventStatus.RetryPending, inboxEvent.Status);
        Assert.Equal("timeout", inboxEvent.LastErrorCode);
        Assert.NotNull(inboxEvent.NextAttemptNotBefore);

        var attempt = Assert.Single(dbContext.ProcessingAttempts);
        Assert.Equal(ProcessingAttemptOutcome.RetryScheduled, attempt.Outcome);
        Assert.Equal("timeout", attempt.ErrorCode);
    }

    [Fact]
    public async Task TryStartDueRetryAsync_DueRetryExists_StartsNewAttempt()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.ScheduleRetryAsync(
            stored.Lease!,
            "timeout",
            "temporary downstream outage",
            TimeSpan.Zero,
            CancellationToken.None);

        var retry = await inbox.TryStartDueRetryAsync("reading_risk_pipeline", CancellationToken.None);

        Assert.NotNull(retry);
        Assert.Equal(envelope.EventId, retry!.Envelope.EventId);
        Assert.Equal(2, retry.Lease.AttemptNumber);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);
        Assert.Equal(2, dbContext.ProcessingAttempts.Count());
    }

    [Fact]
    public async Task TryStartDueRetryAsync_RetryNotDueYet_ReturnsNull()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.ScheduleRetryAsync(
            stored.Lease!,
            "timeout",
            "temporary downstream outage",
            TimeSpan.FromHours(1),
            CancellationToken.None);

        var retry = await inbox.TryStartDueRetryAsync("reading_risk_pipeline", CancellationToken.None);

        Assert.Null(retry);
    }

    [Fact]
    public async Task QuarantineProcessingAsync_LeaseMarkedQuarantined_PersistsQuarantineRecord()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.QuarantineProcessingAsync(
            stored.Lease!,
            "invalid_data",
            "Payload did not satisfy domain requirements.",
            "permanent_failure",
            "Further processing would not succeed.",
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(InboxEventStatus.Quarantined, inboxEvent.Status);
        Assert.Equal("invalid_data", inboxEvent.LastErrorCode);
        Assert.NotNull(inboxEvent.QuarantinedAt);

        var attempt = Assert.Single(dbContext.ProcessingAttempts);
        Assert.Equal(ProcessingAttemptOutcome.Quarantined, attempt.Outcome);

        var quarantine = Assert.Single(dbContext.QuarantinedEvents);
        Assert.Equal("permanent_failure", quarantine.QuarantineCode);
        Assert.Equal(1, quarantine.FinalAttemptNumber);
    }

    [Fact]
    public async Task TryStartDueRetryAsync_MalformedPersistedEnvelope_QuarantinesItem()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await inbox.ScheduleRetryAsync(
            stored.Lease!,
            "timeout",
            "temporary downstream outage",
            TimeSpan.Zero,
            CancellationToken.None);

        await scope.SeedAsync(async dbContext =>
        {
            var row = await dbContext.InboxEvents.SingleAsync();
            row.EnvelopeJson = "{ invalid";
        });

        var retry = await inbox.TryStartDueRetryAsync("reading_risk_pipeline", CancellationToken.None);

        Assert.Null(retry);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(InboxEventStatus.Quarantined, inboxEvent.Status);
        Assert.Equal("invalid_retry_payload", inboxEvent.LastErrorCode);

        var quarantine = Assert.Single(dbContext.QuarantinedEvents);
        Assert.Equal("invalid_retry_payload", quarantine.QuarantineCode);
        Assert.Equal(2, quarantine.FinalAttemptNumber);
    }

    private static PostgresReadingEventInbox CreateInbox(SqliteControlDbContextScope scope)
    {
        return new PostgresReadingEventInbox(
            scope.Factory,
            NullLogger<PostgresReadingEventInbox>.Instance);
    }
}
