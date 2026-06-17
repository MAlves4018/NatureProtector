using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerPostgresPipelineInboxTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PostgresReadingEventInbox_PersistsDuplicateRetryAndQuarantine_OnRealPostgres()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var inbox = new PostgresReadingEventInbox(
            database.CreateFactory(),
            NullLogger<PostgresReadingEventInbox>.Instance);
        var envelope = CreateEnvelope();
        var rawBody = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        var stored = await inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "docker-integration-receive",
            CancellationToken.None);
        var duplicate = await inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "docker-integration-duplicate",
            CancellationToken.None);

        Assert.True(stored.ShouldProcessNow);
        Assert.False(stored.IsDuplicate);
        Assert.NotNull(stored.Lease);
        Assert.True(duplicate.IsDuplicate);
        Assert.False(duplicate.ShouldProcessNow);
        Assert.Null(duplicate.Lease);

        await inbox.ScheduleRetryAsync(
            stored.Lease!,
            "transient_integration_failure",
            "Retryable integration failure.",
            TimeSpan.Zero,
            CancellationToken.None);

        var retry = await inbox.TryStartDueRetryAsync(
            "docker-integration-retry",
            CancellationToken.None,
            processingLeaseTimeout: TimeSpan.FromSeconds(30),
            maxProcessingAttempts: 3);

        Assert.NotNull(retry);
        Assert.Equal(envelope.EventId, retry!.Envelope.EventId);
        Assert.Equal(2, retry.Lease.AttemptNumber);

        await inbox.QuarantineProcessingAsync(
            retry.Lease,
            "permanent_integration_failure",
            "Permanent integration failure.",
            "integration_quarantine",
            "Integration test quarantine.",
            "{\"source\":\"docker\"}",
            CancellationToken.None);

        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == envelope.EventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inboxEvent.Id)
            .OrderBy(entity => entity.AttemptNumber)
            .ToListAsync();
        var quarantine = await dbContext.QuarantinedEvents.SingleAsync(entity => entity.EventId == envelope.EventId);

        Assert.Equal(InboxEventStatus.Quarantined, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);
        Assert.Equal("permanent_integration_failure", inboxEvent.LastErrorCode);
        Assert.Equal([ProcessingAttemptOutcome.RetryScheduled, ProcessingAttemptOutcome.Quarantined], attempts.Select(attempt => attempt.Outcome));
        Assert.Equal("integration_quarantine", quarantine.QuarantineCode);
        Assert.Contains("\"stage\":\"docker-integration-retry\"", quarantine.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PostgresReadingEventInbox_RecoversExpiredProcessingLease_OnRealPostgres()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var inbox = new PostgresReadingEventInbox(
            database.CreateFactory(),
            NullLogger<PostgresReadingEventInbox>.Instance);
        var envelope = CreateEnvelope();
        var rawBody = JsonEventSerializer.SerializeToUtf8Bytes(envelope);
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "docker-integration-receive",
            CancellationToken.None);
        await MarkProcessingLeaseAsStaleAsync(database, stored.InboxEventId);

        var retry = await inbox.TryStartDueRetryAsync(
            "docker-integration-lease-recovery",
            CancellationToken.None,
            processingLeaseTimeout: TimeSpan.FromSeconds(1),
            maxProcessingAttempts: 3);

        Assert.NotNull(retry);
        Assert.Equal(envelope.EventId, retry!.Envelope.EventId);
        Assert.Equal(2, retry.Lease.AttemptNumber);

        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == envelope.EventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inboxEvent.Id)
            .OrderBy(entity => entity.AttemptNumber)
            .ToListAsync();

        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal(2, inboxEvent.AttemptCount);
        Assert.Equal([ProcessingAttemptOutcome.RetryScheduled, ProcessingAttemptOutcome.Started], attempts.Select(attempt => attempt.Outcome));
        Assert.Equal("processing_lease_expired", attempts[0].ErrorCode);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PostgresReadingEventInbox_QuarantinesExpiredProcessingLease_WhenAttemptsAreExhausted_OnRealPostgres()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var inbox = new PostgresReadingEventInbox(
            database.CreateFactory(),
            NullLogger<PostgresReadingEventInbox>.Instance);
        var envelope = CreateEnvelope();
        var rawBody = JsonEventSerializer.SerializeToUtf8Bytes(envelope);
        var stored = await inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "docker-integration-receive",
            CancellationToken.None);
        await MarkProcessingLeaseAsStaleAsync(database, stored.InboxEventId);

        var retry = await inbox.TryStartDueRetryAsync(
            "docker-integration-lease-quarantine",
            CancellationToken.None,
            processingLeaseTimeout: TimeSpan.FromSeconds(1),
            maxProcessingAttempts: 1);

        Assert.Null(retry);

        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == envelope.EventId);
        var attempt = await dbContext.ProcessingAttempts.SingleAsync(entity => entity.InboxEventId == inboxEvent.Id);
        var quarantine = await dbContext.QuarantinedEvents.SingleAsync(entity => entity.EventId == envelope.EventId);

        Assert.Equal(InboxEventStatus.Quarantined, inboxEvent.Status);
        Assert.Equal("processing_lease_expired", inboxEvent.LastErrorCode);
        Assert.Equal(ProcessingAttemptOutcome.Quarantined, attempt.Outcome);
        Assert.Equal("processing_lease_expired", quarantine.QuarantineCode);
        Assert.Contains("\"stage\":\"docker-integration-lease-quarantine\"", quarantine.MetadataJson, StringComparison.Ordinal);
    }

    private static async Task MarkProcessingLeaseAsStaleAsync(
        TemporaryPostgresDatabase database,
        Guid inboxEventId)
    {
        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.Id == inboxEventId);
        inboxEvent.LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await dbContext.SaveChangesAsync();
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        var eventId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero);

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "v1",
            EventId: eventId,
            CorrelationId: $"docker-it-{eventId:N}",
            Producer: "NatureProtector.IntegrationTests",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: timestamp,
            IngestTime: timestamp.AddSeconds(1),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Docker-IT-Sensor",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 33.5,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }
}
