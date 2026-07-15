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
        Assert.Equal(envelope.Payload.SimulationRunId, inboxEvent.SimulationRunId);
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
    public async Task StoreIncomingAsync_ConcurrentUniqueViolation_TreatedAsDuplicate()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"natureprotector-inbox-tests-{Guid.NewGuid():N}.sqlite");
        await using var bootstrapScope = new SqliteControlDbContextScope(
            useFileDatabase: true,
            databasePath: databasePath);
        var envelope = EnvelopeFactory.Create();
        var interceptor = new DuplicateInsertOnSaveInterceptor(
            bootstrapScope.PlainOptions,
            context => context.ChangeTracker.Entries<InboxEventRecord>().Any(entry => entry.State == EntityState.Added),
            (sidecarContext, currentContext, _) =>
            {
                var pendingInbox = currentContext.ChangeTracker.Entries<InboxEventRecord>()
                    .Single(entry => entry.State == EntityState.Added)
                    .Entity;
                var pendingAttempt = currentContext.ChangeTracker.Entries<ProcessingAttemptRecord>()
                    .Single(entry => entry.State == EntityState.Added)
                    .Entity;

                sidecarContext.InboxEvents.Add(new InboxEventRecord
                {
                    Id = pendingInbox.Id,
                    EventId = pendingInbox.EventId,
                    SchemaVersion = pendingInbox.SchemaVersion,
                    CorrelationId = pendingInbox.CorrelationId,
                    Producer = pendingInbox.Producer,
                    EventType = pendingInbox.EventType,
                    AreaId = pendingInbox.AreaId,
                    EventTime = pendingInbox.EventTime,
                    ReceivedAt = pendingInbox.ReceivedAt,
                    IngestTime = pendingInbox.IngestTime,
                    PayloadJson = pendingInbox.PayloadJson,
                    EnvelopeJson = pendingInbox.EnvelopeJson,
                    Status = pendingInbox.Status,
                    AttemptCount = pendingInbox.AttemptCount,
                    LastAttemptAt = pendingInbox.LastAttemptAt
                });

                sidecarContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
                {
                    Id = pendingAttempt.Id,
                    InboxEventId = pendingAttempt.InboxEventId,
                    AttemptNumber = pendingAttempt.AttemptNumber,
                    Stage = pendingAttempt.Stage,
                    StartedAt = pendingAttempt.StartedAt,
                    Outcome = pendingAttempt.Outcome
                });

                return Task.CompletedTask;
            });
        await using var scope = new SqliteControlDbContextScope(
            configureOptions: builder => builder.AddInterceptors(interceptor),
            useFileDatabase: true,
            databasePath: databasePath);
        var inbox = CreateInbox(scope);

        var result = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.False(result.ShouldProcessNow);

        await using var dbContext = scope.CreateDbContext();
        Assert.Single(dbContext.InboxEvents);
        Assert.Single(dbContext.ProcessingAttempts);
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
    public async Task TryStartDueRetryAsync_ExpiredProcessingLease_StartsRecoveredAttempt()
    {
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await scope.SeedAsync(async dbContext =>
        {
            var row = await dbContext.InboxEvents.SingleAsync();
            row.LastAttemptAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(30));
        });

        var retry = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None,
            TimeSpan.FromMinutes(5),
            maxProcessingAttempts: 3);

        Assert.NotNull(retry);
        Assert.Equal(envelope.EventId, retry!.Envelope.EventId);
        Assert.Equal(2, retry.Lease.AttemptNumber);

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(stored.InboxEventId, inboxEvent.Id);
        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);

        var attempts = dbContext.ProcessingAttempts
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToArray();
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
        await using var scope = new SqliteControlDbContextScope();
        var inbox = CreateInbox(scope);
        var envelope = EnvelopeFactory.Create();
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await scope.SeedAsync(async dbContext =>
        {
            var row = await dbContext.InboxEvents.SingleAsync();
            row.LastAttemptAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(30));
        });

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

        await using var dbContext = scope.CreateDbContext();
        var inboxEvent = Assert.Single(dbContext.InboxEvents);
        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);
        Assert.Null(inboxEvent.LastProcessedAt);
        Assert.Null(inboxEvent.QuarantinedAt);
        Assert.Null(inboxEvent.LastErrorCode);
        Assert.Null(inboxEvent.LastErrorMessage);

        var attempts = dbContext.ProcessingAttempts
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.Equal(ProcessingAttemptOutcome.RetryScheduled, attempts[0].Outcome);
        Assert.Equal("processing_lease_expired", attempts[0].ErrorCode);
        Assert.Equal(ProcessingAttemptOutcome.Started, attempts[1].Outcome);
        Assert.Empty(dbContext.QuarantinedEvents);
    }

    [Fact]
    public async Task TryStartDueRetryAsync_ConcurrentAttemptNumberRace_ReturnsNull()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"natureprotector-retry-race-tests-{Guid.NewGuid():N}.sqlite");
        await using var bootstrapScope = new SqliteControlDbContextScope(
            useFileDatabase: true,
            databasePath: databasePath);
        var bootstrapInbox = CreateInbox(bootstrapScope);
        var envelope = EnvelopeFactory.Create();
        var stored = await bootstrapInbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        await bootstrapInbox.ScheduleRetryAsync(
            stored.Lease!,
            "timeout",
            "temporary downstream outage",
            TimeSpan.Zero,
            CancellationToken.None);

        var interceptor = new DuplicateInsertOnSaveInterceptor(
            bootstrapScope.PlainOptions,
            context => context.ChangeTracker.Entries<ProcessingAttemptRecord>().Any(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.AttemptNumber == 2),
            async (sidecarContext, currentContext, cancellationToken) =>
            {
                var pending = currentContext.ChangeTracker.Entries<ProcessingAttemptRecord>()
                    .Single(entry => entry.State == EntityState.Added && entry.Entity.AttemptNumber == 2)
                    .Entity;
                var inboxRow = await sidecarContext.InboxEvents.SingleAsync(
                    entity => entity.Id == pending.InboxEventId,
                    cancellationToken);

                inboxRow.Status = InboxEventStatus.Processing;
                inboxRow.AttemptCount = pending.AttemptNumber;
                inboxRow.LastAttemptAt = pending.StartedAt;
                inboxRow.NextAttemptNotBefore = null;

                sidecarContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
                {
                    Id = Guid.NewGuid(),
                    InboxEventId = pending.InboxEventId,
                    AttemptNumber = pending.AttemptNumber,
                    Stage = pending.Stage,
                    StartedAt = pending.StartedAt,
                    Outcome = pending.Outcome
                });
            });
        await using var scope = new SqliteControlDbContextScope(
            configureOptions: builder => builder.AddInterceptors(interceptor),
            useFileDatabase: true,
            databasePath: databasePath);
        var inbox = CreateInbox(scope);

        var retry = await inbox.TryStartDueRetryAsync("reading_risk_pipeline", CancellationToken.None);

        Assert.Null(retry);
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
            null,
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
    public async Task QuarantineProcessingAsync_PersistsProviderSpecificMetadata()
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
            "db_data_exception",
            "SqlState=22001 | MessageText=value too long for type character varying(100)",
            "permanent_failure",
            "Further processing would not succeed.",
            "{\"sqlState\":\"22001\",\"messageText\":\"value too long for type character varying(100)\",\"tableName\":\"daily_cell_state\",\"columnName\":\"DroughtContext\"}",
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var quarantine = Assert.Single(dbContext.QuarantinedEvents);
        Assert.Contains("\"stage\":\"reading_risk_pipeline\"", quarantine.MetadataJson);
        Assert.Contains("\"sqlState\":\"22001\"", quarantine.MetadataJson);
        Assert.Contains("\"tableName\":\"daily_cell_state\"", quarantine.MetadataJson);
        Assert.Contains("\"columnName\":\"DroughtContext\"", quarantine.MetadataJson);
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
