using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Prevention.Host;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Runtime;
using NatureProtector.Prevention.Risk;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed partial class DockerRabbitMqConsumerPipelineTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task RabbitMqPublisherToPreventionWorker_PersistsProcessedAssessmentAndAcks_OnRealServices()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var exchangeName = $"np.it.consumer.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(baseOptions, CancellationToken.None);
        var rabbitMqOptions = virtualHost.CreateOptions(exchangeName);
        var connectionFactory = virtualHost.CreateConnectionFactory();
        var dbContextFactory = database.CreateFactory();
        var areaId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        await SeedControlPlaneAsync(
            database,
            Guid.NewGuid(),
            areaId,
            gridCellId,
            sensorId,
            simulationRunId,
            timestamp);

        var preventionOptions = Options.Create(new PreventionHostOptions
        {
            ConsumerPrefetchCount = 1,
            MaxProcessingAttempts = 3,
            RetryDelaySeconds = [0, 0],
            RetryPollingIntervalSeconds = 1
        });
        var inbox = new PostgresReadingEventInbox(
            dbContextFactory,
            NullLogger<PostgresReadingEventInbox>.Instance);
        var pipeline = CreatePostgresPipeline(dbContextFactory);
        var processingService = new ReadingEventProcessingService(
            NullLogger<ReadingEventProcessingService>.Instance,
            preventionOptions,
            pipeline,
            inbox,
            new PassThroughReadingSemanticValidator(),
            new NoOpProcessingFaultInjector(),
            new DefaultProcessingFailureClassifier());
        var worker = new PreventionWorker(
            NullLogger<PreventionWorker>.Instance,
            Options.Create(rabbitMqOptions),
            preventionOptions,
            inbox,
            processingService,
            new PreventionRuntimeState());
        using var publisher = new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(rabbitMqOptions));
        var envelope = CreateEnvelope(areaId, sensorId, timestamp, simulationRunId);
        var workerStarted = false;

        try
        {
            await worker.StartAsync(CancellationToken.None);
            workerStarted = true;
            await WaitForConsumerAsync(connectionFactory, rabbitMqOptions.IngestionReadingsQueueName);

            await publisher.PublishAsync(envelope, CancellationToken.None);

            await WaitForProcessedEventAsync(database, envelope.EventId, areaId, gridCellId);

            await worker.StopAsync(CancellationToken.None);
            workerStarted = false;

            AssertNoResidualIngestionMessage(connectionFactory, rabbitMqOptions.IngestionReadingsQueueName);
        }
        finally
        {
            if (workerStarted)
            {
                await worker.StopAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RejectsMalformedPayloadAndAcks_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();

        PublishRaw(
            harness.ConnectionFactory,
            harness.RabbitMqOptions,
            Encoding.UTF8.GetBytes("{ invalid-json"));

        await WaitForRejectedEventAsync(harness.Database, "invalid_json");
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_ProcessesValidMessageAfterMalformedPayload_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);

        PublishRaw(
            harness.ConnectionFactory,
            harness.RabbitMqOptions,
            Encoding.UTF8.GetBytes("{ invalid-json"));
        await WaitForRejectedEventAsync(harness.Database, "invalid_json");

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
        await WaitForProcessedEventAsync(harness.Database, envelope.EventId, harness.AreaId, harness.GridCellId);

        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RejectsInvalidEnumAndAcks_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId,
            metricType: (SensorMetricType)999);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);

        await WaitForRejectedEventAsync(harness.Database, "invalid_metric_type");
        await AssertNoAcceptedOrInboxEventsAsync(harness.Database, envelope.EventId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_AcksDuplicateEventIdWithoutDuplicateEffects_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
        await WaitForProcessedEventAsync(harness.Database, envelope.EventId, harness.AreaId, harness.GridCellId);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
        await WaitForQueueReadyCountAsync(
            harness.ConnectionFactory,
            harness.RabbitMqOptions.IngestionReadingsQueueName,
            expectedReadyCount: 0);

        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
        await AssertSingleProcessedEffectAsync(harness.Database, envelope.EventId, harness.AreaId, harness.GridCellId);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_PostgresOutageBeforeInboxCommit_RequeuesWithoutAcking_OnIsolatedDatabase()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync(startWorker: false);
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
        await WaitForQueueReadyCountAsync(
            harness.ConnectionFactory,
            harness.RabbitMqOptions.IngestionReadingsQueueName,
            expectedReadyCount: 1);

        await harness.Database.DropAsync();
        var outageInbox = new StoreFailureSignalInbox(harness.Inbox, envelope.EventId);
        using var outageWorker = harness.CreateWorker(outageInbox);

        try
        {
            await outageWorker.StartAsync(CancellationToken.None);
            await WaitForConsumerAsync(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
            await outageInbox.WaitForStoreFailureAsync();
        }
        finally
        {
            await outageWorker.StopAsync(CancellationToken.None);
        }

        await WaitForQueueReadyCountAsync(
            harness.ConnectionFactory,
            harness.RabbitMqOptions.IngestionReadingsQueueName,
            expectedReadyCount: 1);
        Assert.False(await TemporaryPostgresDatabase.DatabaseExistsAsync(harness.Database.DatabaseName));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_IsolatedRabbitMqVhostDeletion_StopsWithoutTouchingPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();

        await harness.DeleteRabbitMqVirtualHostAsync();
        Assert.False(await harness.RabbitMqVirtualHostExistsAsync());

        await harness.StopWorkerAsync();

        Assert.True(await TemporaryPostgresDatabase.DatabaseExistsAsync(harness.Database.DatabaseName));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RediveredStoredInboxEvent_AcksAndRecoversViaRetryWorker_WithoutDuplicateEffects()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync(startWorker: false);
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);
        var rawBody = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        var stored = await harness.Inbox.StoreIncomingAsync(
            envelope,
            rawBody,
            "docker-integration-pre-ack-crash",
            CancellationToken.None);

        Assert.False(stored.IsDuplicate);
        Assert.True(stored.ShouldProcessNow);
        Assert.NotNull(stored.Lease);

        var signalingInbox = new DuplicateStoreSignalInbox(harness.Inbox, envelope.EventId);
        using var redeliveryWorker = harness.CreateWorker(signalingInbox);

        try
        {
            await redeliveryWorker.StartAsync(CancellationToken.None);
            await WaitForConsumerAsync(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);

            await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
            await signalingInbox.WaitForDuplicateAsync();

            await AssertProcessingLeaseWithoutProjectionEffectsAsync(
                harness.Database,
                envelope.EventId,
                harness.AreaId,
                harness.GridCellId);

            await MarkProcessingLeaseAsStaleAsync(harness.Database, stored.InboxEventId);

            using var retryWorker = harness.CreateRetryWorker();

            try
            {
                await retryWorker.StartAsync(CancellationToken.None);
                await WaitForRecoveredProcessedEventAsync(
                    harness.Database,
                    envelope.EventId,
                    harness.AreaId,
                    harness.GridCellId);
            }
            finally
            {
                await retryWorker.StopAsync(CancellationToken.None);
            }

            AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
            await AssertRecoveredSingleProcessedEffectAsync(
                harness.Database,
                envelope.EventId,
                harness.AreaId,
                harness.GridCellId);
        }
        finally
        {
            await redeliveryWorker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_ProcessesSamePayloadWithDifferentEventIds_AsSeparateEvents_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var first = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);
        var second = first with
        {
            EventId = Guid.NewGuid(),
            CorrelationId = $"docker-rabbitmq-consumer-{Guid.NewGuid():N}"
        };
        var eventIds = new[] { first.EventId, second.EventId };

        await harness.Publisher.PublishAsync(first, CancellationToken.None);
        await harness.Publisher.PublishAsync(second, CancellationToken.None);

        await WaitForProcessedEventsAsync(
            harness.Database,
            eventIds,
            harness.AreaId,
            harness.GridCellId);

        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
        await AssertProcessedEffectsAsync(
            harness.Database,
            eventIds,
            harness.AreaId,
            harness.GridCellId);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RejectsDuplicateEventIdWithPayloadMismatchWithoutDuplicateEffects_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var original = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);
        var conflicting = original with
        {
            Payload = original.Payload with { Value = original.Payload.Value + 9.0 }
        };

        await harness.Publisher.PublishAsync(original, CancellationToken.None);
        await WaitForProcessedEventAsync(harness.Database, original.EventId, harness.AreaId, harness.GridCellId);

        await harness.Publisher.PublishAsync(conflicting, CancellationToken.None);

        await WaitForRejectedEventAsync(
            harness.Database,
            "duplicate_payload_mismatch",
            original.EventId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
        await AssertDuplicatePayloadMismatchDidNotDuplicateEffectsAsync(
            harness.Database,
            original.EventId,
            harness.AreaId,
            harness.GridCellId);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RejectsUnsupportedEventTypeBeforeInbox_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId) with
        {
            EventType = "UnexpectedEvent"
        };

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);

        await WaitForRejectedEventAsync(harness.Database, "unsupported_event_type");
        await AssertNoAcceptedOrInboxEventsAsync(harness.Database, envelope.EventId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RejectsMissingPayloadBeforeInbox_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId) with
        {
            Payload = null!
        };

        PublishRaw(
            harness.ConnectionFactory,
            harness.RabbitMqOptions,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope));

        await WaitForRejectedEventAsync(harness.Database, "missing_payload");
        await AssertNoAcceptedOrInboxEventsAsync(harness.Database, envelope.EventId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_SchedulesRetryAndAcks_WhenTransientFailureHappensAfterInboxCommit()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync(
            new ThrowingProcessingFaultInjector(new TimeoutException("forced transient failure")));
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);

        await WaitForInboxOutcomeAsync(
            harness.Database,
            envelope.EventId,
            InboxEventStatus.RetryPending,
            [ProcessingAttemptOutcome.RetryScheduled],
            expectedErrorCode: "timeout");
        await AssertNoAcceptedOrProjectionEffectsAsync(harness.Database, envelope.EventId, harness.AreaId, harness.GridCellId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_RetryWorkerQuarantinesAfterRetryExhaustion_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync(
            new ThrowingProcessingFaultInjector(new TimeoutException("forced retry exhaustion")));
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);
        using var retryWorker = harness.CreateRetryWorker();
        var retryWorkerStarted = false;

        try
        {
            await harness.Publisher.PublishAsync(envelope, CancellationToken.None);

            await WaitForInboxOutcomeAsync(
                harness.Database,
                envelope.EventId,
                InboxEventStatus.RetryPending,
                [ProcessingAttemptOutcome.RetryScheduled],
                expectedErrorCode: "timeout");

            await retryWorker.StartAsync(CancellationToken.None);
            retryWorkerStarted = true;

            await WaitForInboxOutcomeAsync(
                harness.Database,
                envelope.EventId,
                InboxEventStatus.Quarantined,
                [
                    ProcessingAttemptOutcome.RetryScheduled,
                    ProcessingAttemptOutcome.RetryScheduled,
                    ProcessingAttemptOutcome.Quarantined
                ],
                expectedErrorCode: "timeout",
                expectedQuarantineCode: "retries_exhausted");
        }
        finally
        {
            if (retryWorkerStarted)
            {
                await retryWorker.StopAsync(CancellationToken.None);
            }
        }

        await AssertNoAcceptedOrProjectionEffectsAsync(
            harness.Database,
            envelope.EventId,
            harness.AreaId,
            harness.GridCellId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_QuarantinesAndAcks_WhenPermanentFailureHappensAfterInboxCommit()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync(
            new ThrowingProcessingFaultInjector(new ArgumentException("forced permanent failure")));
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);

        await WaitForInboxOutcomeAsync(
            harness.Database,
            envelope.EventId,
            InboxEventStatus.Quarantined,
            [ProcessingAttemptOutcome.Quarantined],
            expectedErrorCode: "invalid_argument",
            expectedQuarantineCode: "permanent_failure");
        await AssertNoAcceptedOrProjectionEffectsAsync(harness.Database, envelope.EventId, harness.AreaId, harness.GridCellId);
        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_ProcessesQueuedMessageAfterDelayedConsumerStartup_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync(startWorker: false);
        var envelope = CreateEnvelope(
            harness.AreaId,
            harness.SensorId,
            harness.Timestamp,
            harness.SimulationRunId);

        await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
        await WaitForQueueReadyCountAsync(
            harness.ConnectionFactory,
            harness.RabbitMqOptions.IngestionReadingsQueueName,
            expectedReadyCount: 1);

        await harness.StartWorkerAsync();
        await WaitForProcessedEventAsync(harness.Database, envelope.EventId, harness.AreaId, harness.GridCellId);

        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PreventionWorker_CompetingConsumersProcessDistinctEventsWithoutDuplicateEffects_OnRealRabbitMqAndPostgres()
    {
        await using var harness = await ConsumerPipelineHarness.CreateAsync();
        using var secondWorker = harness.CreateWorker();
        var secondWorkerStarted = false;
        var envelopes = Enumerable.Range(0, 4)
            .Select(index => CreateEnvelope(
                harness.AreaId,
                harness.SensorId,
                harness.Timestamp.AddSeconds(index),
                harness.SimulationRunId))
            .ToArray();
        var eventIds = envelopes.Select(envelope => envelope.EventId).ToArray();

        try
        {
            await secondWorker.StartAsync(CancellationToken.None);
            secondWorkerStarted = true;
            await WaitForConsumerCountAsync(
                harness.ConnectionFactory,
                harness.RabbitMqOptions.IngestionReadingsQueueName,
                minimumConsumerCount: 2);

            foreach (var envelope in envelopes)
            {
                await harness.Publisher.PublishAsync(envelope, CancellationToken.None);
            }

            await WaitForProcessedEventsAsync(
                harness.Database,
                eventIds,
                harness.AreaId,
                harness.GridCellId);
        }
        finally
        {
            if (secondWorkerStarted)
            {
                await secondWorker.StopAsync(CancellationToken.None);
            }
        }

        await harness.StopWorkerAsync();
        AssertNoResidualIngestionMessage(harness.ConnectionFactory, harness.RabbitMqOptions.IngestionReadingsQueueName);
        await AssertProcessedEffectsAsync(
            harness.Database,
            eventIds,
            harness.AreaId,
            harness.GridCellId);
    }

    private static ReadingRiskPipeline CreatePostgresPipeline(
        IDbContextFactory<NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext> dbContextFactory)
    {
        var noOpInflux = new NoOpInfluxWriteService(
            Options.Create(new InfluxDbOptions { Enabled = false }),
            NullLogger<NoOpInfluxWriteService>.Instance);

        return new ReadingRiskPipeline(
            new PostgresAcceptedReadingRepository(
                dbContextFactory,
                NullLogger<PostgresAcceptedReadingRepository>.Instance),
            new RiskEligibilityService(),
            new PostgresDailyCellStateRepository(dbContextFactory),
            new SimpleRiskScoringService(),
            new PostgresRiskAssessmentRepository(
                dbContextFactory,
                NullLogger<PostgresRiskAssessmentRepository>.Instance),
            new AreaRiskSnapshotService(),
            new PostgresAreaRiskSnapshotRepository(
                dbContextFactory,
                NullLogger<PostgresAreaRiskSnapshotRepository>.Instance),
            new PostgresAreaOperationalProjectionStore(
                dbContextFactory,
                NullLogger<PostgresAreaOperationalProjectionStore>.Instance),
            noOpInflux,
            NullLogger<ReadingRiskPipeline>.Instance);
    }

    private static async Task WaitForConsumerAsync(ConnectionFactory factory, string queueName)
    {
        await WaitForConsumerCountAsync(factory, queueName, minimumConsumerCount: 1);
    }

    private static async Task WaitForConsumerCountAsync(
        ConnectionFactory factory,
        string queueName,
        uint minimumConsumerCount)
    {
        Exception? lastFailure = null;

        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                using var connection = factory.CreateConnection("natureprotector-it-consumer-readiness");
                using var channel = connection.CreateModel();
                var declaration = channel.QueueDeclarePassive(queueName);
                if (declaration.ConsumerCount >= minimumConsumerCount)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException(
            $"RabbitMQ queue '{queueName}' did not observe {minimumConsumerCount} prevention consumer(s).",
            lastFailure);
    }

    private static async Task WaitForQueueReadyCountAsync(
        ConnectionFactory factory,
        string queueName,
        uint expectedReadyCount)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var connection = factory.CreateConnection("natureprotector-it-queue-count");
            using var channel = connection.CreateModel();
            var declaration = channel.QueueDeclarePassive(queueName);
            if (declaration.MessageCount == expectedReadyCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"RabbitMQ queue '{queueName}' did not reach ready count {expectedReadyCount}.");
    }

    private static async Task WaitForProcessedEventAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        string? lastState = null;

        for (var attempt = 0; attempt < 80; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            var inboxEvent = await dbContext.InboxEvents.SingleOrDefaultAsync(entity => entity.EventId == eventId);
            var acceptedReadings = await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == eventId);
            var assessments = await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == eventId);
            var areaSnapshots = await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId);
            var cellStates = await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId);
            var areaStates = await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId);
            var attemptOutcomes = inboxEvent is null
                ? []
                : await dbContext.ProcessingAttempts
                    .Where(entity => entity.InboxEventId == inboxEvent.Id)
                    .OrderBy(entity => entity.AttemptNumber)
                    .Select(entity => entity.Outcome)
                    .ToListAsync();

            lastState =
                $"status={inboxEvent?.Status.ToString() ?? "<missing>"} " +
                $"attempts=[{string.Join(",", attemptOutcomes)}] " +
                $"accepted={acceptedReadings} assessments={assessments} snapshots={areaSnapshots} " +
                $"cellStates={cellStates} areaStates={areaStates}";

            if (inboxEvent?.Status == InboxEventStatus.Processed &&
                attemptOutcomes.SequenceEqual([ProcessingAttemptOutcome.Succeeded]) &&
                acceptedReadings == 1 &&
                assessments == 1 &&
                areaSnapshots == 1 &&
                cellStates == 1 &&
                areaStates == 1)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"RabbitMQ consumer pipeline did not reach processed durable state. Last state: {lastState}");
    }

    private static async Task WaitForRecoveredProcessedEventAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        string? lastState = null;

        for (var attempt = 0; attempt < 80; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            var inboxEvent = await dbContext.InboxEvents.SingleOrDefaultAsync(entity => entity.EventId == eventId);
            var acceptedReadings = await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == eventId);
            var assessments = await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == eventId);
            var areaSnapshots = await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId);
            var cellStates = await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId);
            var areaStates = await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId);
            var attemptOutcomes = inboxEvent is null
                ? []
                : await dbContext.ProcessingAttempts
                    .Where(entity => entity.InboxEventId == inboxEvent.Id)
                    .OrderBy(entity => entity.AttemptNumber)
                    .Select(entity => entity.Outcome)
                    .ToListAsync();

            lastState =
                $"status={inboxEvent?.Status.ToString() ?? "<missing>"} " +
                $"attempts=[{string.Join(",", attemptOutcomes)}] " +
                $"accepted={acceptedReadings} assessments={assessments} snapshots={areaSnapshots} " +
                $"cellStates={cellStates} areaStates={areaStates}";

            if (inboxEvent?.Status == InboxEventStatus.Processed &&
                attemptOutcomes.SequenceEqual([ProcessingAttemptOutcome.RetryScheduled, ProcessingAttemptOutcome.Succeeded]) &&
                acceptedReadings == 1 &&
                assessments == 1 &&
                areaSnapshots == 1 &&
                cellStates == 1 &&
                areaStates == 1)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"RabbitMQ consumer pipeline did not recover a stale durable lease. Last state: {lastState}");
    }

    private static async Task WaitForProcessedEventsAsync(
        TemporaryPostgresDatabase database,
        IReadOnlyCollection<Guid> eventIds,
        Guid areaId,
        Guid gridCellId)
    {
        string? lastState = null;
        var expectedCount = eventIds.Count;
        var ids = eventIds.ToArray();

        for (var attempt = 0; attempt < 80; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            var inboxEvents = await dbContext.InboxEvents
                .Where(entity => ids.Contains(entity.EventId))
                .ToListAsync();
            var processingAttemptOutcomes = await dbContext.ProcessingAttempts
                .Where(entity => inboxEvents.Select(inbox => inbox.Id).Contains(entity.InboxEventId))
                .OrderBy(entity => entity.StartedAt)
                .Select(entity => entity.Outcome)
                .ToListAsync();
            var acceptedReadings = await dbContext.AcceptedReadingLogs.CountAsync(entity => ids.Contains(entity.EventId));
            var assessments = await dbContext.RiskAssessmentLogs.CountAsync(entity => ids.Contains(entity.SourceEventId));
            var areaSnapshots = await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId);
            var cellStates = await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId);
            var areaStates = await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId);

            lastState =
                $"inbox={inboxEvents.Count}/{expectedCount} " +
                $"statuses=[{string.Join(",", inboxEvents.Select(entity => entity.Status))}] " +
                $"attempts=[{string.Join(",", processingAttemptOutcomes)}] " +
                $"accepted={acceptedReadings} assessments={assessments} snapshots={areaSnapshots} " +
                $"cellStates={cellStates} areaStates={areaStates}";

            if (inboxEvents.Count == expectedCount &&
                inboxEvents.All(entity => entity.Status == InboxEventStatus.Processed) &&
                processingAttemptOutcomes.Count == expectedCount &&
                processingAttemptOutcomes.All(outcome => outcome == ProcessingAttemptOutcome.Succeeded) &&
                acceptedReadings == expectedCount &&
                assessments == expectedCount &&
                areaSnapshots == expectedCount &&
                cellStates == 1 &&
                areaStates == 1)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"RabbitMQ consumer pipeline did not process all expected events. Last state: {lastState}");
    }

    private static async Task WaitForInboxOutcomeAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        InboxEventStatus expectedStatus,
        ProcessingAttemptOutcome[] expectedAttemptOutcomes,
        string expectedErrorCode,
        string? expectedQuarantineCode = null)
    {
        string? lastState = null;

        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            var inboxEvent = await dbContext.InboxEvents.SingleOrDefaultAsync(entity => entity.EventId == eventId);
            var attempts = inboxEvent is null
                ? []
                : await dbContext.ProcessingAttempts
                    .Where(entity => entity.InboxEventId == inboxEvent.Id)
                    .OrderBy(entity => entity.AttemptNumber)
                    .ToListAsync();
            var quarantine = expectedQuarantineCode is null
                ? null
                : await dbContext.QuarantinedEvents.SingleOrDefaultAsync(entity => entity.EventId == eventId);

            lastState =
                $"status={inboxEvent?.Status.ToString() ?? "<missing>"} " +
                $"lastError={inboxEvent?.LastErrorCode ?? "<none>"} " +
                $"attempts=[{string.Join(",", attempts.Select(entity => $"{entity.Outcome}:{entity.ErrorCode}"))}] " +
                $"quarantine={quarantine?.QuarantineCode ?? "<none>"}";

            var attemptsMatch = attempts
                .Select(entity => entity.Outcome)
                .SequenceEqual(expectedAttemptOutcomes);
            var errorMatches =
                string.Equals(inboxEvent?.LastErrorCode, expectedErrorCode, StringComparison.Ordinal) &&
                attempts.All(entity => string.Equals(entity.ErrorCode, expectedErrorCode, StringComparison.Ordinal));
            var quarantineMatches =
                expectedQuarantineCode is null ||
                string.Equals(quarantine?.QuarantineCode, expectedQuarantineCode, StringComparison.Ordinal);

            if (inboxEvent?.Status == expectedStatus &&
                attemptsMatch &&
                errorMatches &&
                quarantineMatches)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"RabbitMQ consumer pipeline did not reach expected inbox outcome. Last state: {lastState}");
    }

    private static void AssertNoResidualIngestionMessage(ConnectionFactory factory, string queueName)
    {
        using var connection = factory.CreateConnection("natureprotector-it-ingestion-empty-check");
        using var channel = connection.CreateModel();
        var residual = channel.BasicGet(queueName, autoAck: false);
        if (residual is not null)
        {
            channel.BasicNack(residual.DeliveryTag, multiple: false, requeue: true);
        }

        Assert.Null(residual);
    }

    private static void PublishRaw(
        ConnectionFactory factory,
        RabbitMqOptions options,
        byte[] body)
    {
        using var connection = factory.CreateConnection("natureprotector-it-raw-publisher");
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.ContentEncoding = "utf-8";
        properties.MessageId = Guid.NewGuid().ToString();
        properties.CorrelationId = $"docker-rabbitmq-consumer-{Guid.NewGuid():N}";
        properties.Type = EventTypes.SensorReadingProduced;

        channel.BasicPublish(
            exchange: options.ExchangeName,
            routingKey: RoutingKeys.SensorReadingProduced,
            basicProperties: properties,
            body: body);
    }

    private static async Task WaitForRejectedEventAsync(
        TemporaryPostgresDatabase database,
        string rejectionCode,
        Guid? eventId = null)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            if (await dbContext.RejectedEvents.AnyAsync(entity =>
                    entity.RejectionCode == rejectionCode &&
                    (!eventId.HasValue || entity.EventId == eventId.Value)))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"Rejected event with code '{rejectionCode}' was not persisted.");
    }

    private static async Task AssertNoAcceptedOrInboxEventsAsync(
        TemporaryPostgresDatabase database,
        Guid eventId)
    {
        await using var dbContext = database.CreateDbContext();

        Assert.False(await dbContext.InboxEvents.AnyAsync(entity => entity.EventId == eventId));
        Assert.False(await dbContext.AcceptedReadingLogs.AnyAsync(entity => entity.EventId == eventId));
        Assert.False(await dbContext.RiskAssessmentLogs.AnyAsync(entity => entity.SourceEventId == eventId));
    }

    private static async Task AssertNoAcceptedOrProjectionEffectsAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        await using var dbContext = database.CreateDbContext();

        Assert.False(await dbContext.AcceptedReadingLogs.AnyAsync(entity => entity.EventId == eventId));
        Assert.False(await dbContext.RiskAssessmentLogs.AnyAsync(entity => entity.SourceEventId == eventId));
        Assert.False(await dbContext.AreaRiskSnapshotLogs.AnyAsync(entity => entity.AreaId == areaId));
        Assert.False(await dbContext.CellOperationalStates.AnyAsync(entity => entity.GridCellId == gridCellId));
        Assert.False(await dbContext.AreaOperationalStates.AnyAsync(entity => entity.AreaId == areaId));
    }

    private static async Task AssertProcessingLeaseWithoutProjectionEffectsAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == eventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inboxEvent.Id)
            .OrderBy(entity => entity.AttemptNumber)
            .Select(entity => entity.Outcome)
            .ToListAsync();

        Assert.Equal(InboxEventStatus.Processing, inboxEvent.Status);
        Assert.Equal([ProcessingAttemptOutcome.Started], attempts);
        Assert.Equal(1, inboxEvent.AttemptCount);
        Assert.Equal(0, await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == eventId));
        Assert.Equal(0, await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == eventId));
        Assert.Equal(0, await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId));
        Assert.Equal(0, await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId));
        Assert.Equal(0, await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId));
    }

    private static async Task AssertSingleProcessedEffectAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == eventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inboxEvent.Id)
            .OrderBy(entity => entity.AttemptNumber)
            .Select(entity => entity.Outcome)
            .ToListAsync();

        Assert.Equal(InboxEventStatus.Processed, inboxEvent.Status);
        Assert.Equal([ProcessingAttemptOutcome.Succeeded], attempts);
        Assert.Equal(1, await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == eventId));
        Assert.Equal(1, await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == eventId));
        Assert.Equal(1, await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId));
        Assert.Equal(1, await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId));
    }

    private static async Task AssertRecoveredSingleProcessedEffectAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == eventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inboxEvent.Id)
            .OrderBy(entity => entity.AttemptNumber)
            .Select(entity => entity.Outcome)
            .ToListAsync();

        Assert.Equal(InboxEventStatus.Processed, inboxEvent.Status);
        Assert.Equal([ProcessingAttemptOutcome.RetryScheduled, ProcessingAttemptOutcome.Succeeded], attempts);
        Assert.Equal(2, inboxEvent.AttemptCount);
        Assert.Equal(1, await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == eventId));
        Assert.Equal(1, await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == eventId));
        Assert.Equal(1, await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId));
        Assert.Equal(1, await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId));
        Assert.Equal(1, await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId));
    }

    private static async Task AssertProcessedEffectsAsync(
        TemporaryPostgresDatabase database,
        IReadOnlyCollection<Guid> eventIds,
        Guid areaId,
        Guid gridCellId)
    {
        await using var dbContext = database.CreateDbContext();
        var ids = eventIds.ToArray();
        var inboxEvents = await dbContext.InboxEvents
            .Where(entity => ids.Contains(entity.EventId))
            .ToListAsync();
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => inboxEvents.Select(inbox => inbox.Id).Contains(entity.InboxEventId))
            .ToListAsync();

        Assert.Equal(ids.Length, inboxEvents.Count);
        Assert.All(inboxEvents, inboxEvent => Assert.Equal(InboxEventStatus.Processed, inboxEvent.Status));
        Assert.Equal(ids.Length, attempts.Count);
        Assert.All(attempts, attempt => Assert.Equal(ProcessingAttemptOutcome.Succeeded, attempt.Outcome));
        Assert.Equal(ids.Length, await dbContext.AcceptedReadingLogs.CountAsync(entity => ids.Contains(entity.EventId)));
        Assert.Equal(ids.Length, await dbContext.RiskAssessmentLogs.CountAsync(entity => ids.Contains(entity.SourceEventId)));
        Assert.Equal(ids.Length, await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId));
        Assert.Equal(1, await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId));
        Assert.Equal(1, await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId));
    }

    private static async Task AssertDuplicatePayloadMismatchDidNotDuplicateEffectsAsync(
        TemporaryPostgresDatabase database,
        Guid eventId,
        Guid areaId,
        Guid gridCellId)
    {
        await using var dbContext = database.CreateDbContext();
        var inboxEvent = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == eventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inboxEvent.Id)
            .ToListAsync();
        var rejection = await dbContext.RejectedEvents.SingleAsync(entity =>
            entity.EventId == eventId &&
            entity.RejectionCode == "duplicate_payload_mismatch");

        Assert.Equal(InboxEventStatus.Processed, inboxEvent.Status);
        Assert.Single(attempts);
        Assert.Equal(ProcessingAttemptOutcome.Succeeded, attempts.Single().Outcome);
        Assert.Contains("\"stage\":\"reading_risk_pipeline\"", rejection.MetadataJson, StringComparison.Ordinal);
        Assert.Equal(1, await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == eventId));
        Assert.Equal(1, await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == eventId));
        Assert.Equal(1, await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId));
        Assert.Equal(1, await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId));
        Assert.Equal(1, await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId));
    }

    private static async Task SeedControlPlaneAsync(
        TemporaryPostgresDatabase database,
        Guid configurationVersionId,
        Guid areaId,
        Guid gridCellId,
        Guid sensorId,
        Guid simulationRunId,
        DateTimeOffset timestamp)
    {
        var profileId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        await using var dbContext = database.CreateDbContext();
        dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
        {
            Id = configurationVersionId,
            VersionNumber = 20_001,
            Description = "RabbitMQ consumer integration configuration",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "integration-test"
        });
        dbContext.Areas.Add(new AreaRecord
        {
            Id = areaId,
            ConfigurationVersionId = configurationVersionId,
            Code = $"IT-{areaId:N}"[..20],
            Name = "RabbitMQ Consumer Integration Area",
            CountryCode = "PT"
        });
        dbContext.GridCells.Add(new GridCellRecord
        {
            Id = gridCellId,
            AreaId = areaId,
            ConfigurationVersionId = configurationVersionId,
            CellCode = $"CELL-{gridCellId:N}"[..20],
            CentroidLatitude = 39.8,
            CentroidLongitude = -7.9,
            LandCoverClass = "Matos",
            DominantForestType = "Florestas de pinheiro bravo",
            DominantFuelModel = "Matos",
            TreeCoverDensity = 0.55,
            StructuralHazard = "muito_alta",
            SlopeDegrees = 18.0,
            AspectDegrees = 180.0,
            AltitudeMeters = 420.0
        });
        dbContext.SensorProfiles.Add(new SensorProfileRecord
        {
            Id = profileId,
            ConfigurationVersionId = configurationVersionId,
            Name = "RabbitMQ consumer temperature profile",
            SensorFamily = "meteorological"
        });
        dbContext.SensorNodes.Add(new SensorNodeRecord
        {
            Id = sensorId,
            AreaId = areaId,
            GridCellId = gridCellId,
            ProfileId = profileId,
            ConfigurationVersionId = configurationVersionId,
            Name = "Docker-RabbitMQ-Consumer-Sensor",
            Type = SensorType.Temperature,
            Latitude = 39.8,
            Longitude = -7.9,
            AltitudeMeters = 420.0,
            IsActive = true,
            InstallationProfile = "integration"
        });
        dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
        {
            Id = scenarioId,
            AreaId = areaId,
            ConfigurationVersionId = configurationVersionId,
            Code = $"SCN-{scenarioId:N}"[..20],
            Name = "RabbitMQ Consumer Integration Scenario",
            ScenarioKind = ScenarioCategory.HighRisk,
            Description = "RabbitMQ consumer integration scenario",
            ParametersJson = "{}"
        });
        dbContext.SimulationRuns.Add(new SimulationRunRecord
        {
            Id = simulationRunId,
            AreaId = areaId,
            ScenarioId = scenarioId,
            ConfigurationVersionId = configurationVersionId,
            ScenarioCode = $"SCN-{scenarioId:N}"[..20],
            ScenarioName = "RabbitMQ Consumer Integration Scenario",
            CreatedAt = timestamp.AddMinutes(-1),
            StartedAt = timestamp.AddSeconds(-30),
            LogicalStartTimestamp = timestamp,
            IntervalSeconds = 60,
            NumberOfCycles = 1,
            ExecutionSeed = 42,
            Status = SimulationRunStatus.Running,
            MetadataJson = "{\"source\":\"docker-rabbitmq-consumer-integration\"}"
        });

        await dbContext.SaveChangesAsync();
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

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(
        Guid areaId,
        Guid sensorId,
        DateTimeOffset timestamp,
        Guid simulationRunId,
        SensorMetricType metricType = SensorMetricType.Temperature)
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: $"docker-rabbitmq-consumer-{Guid.NewGuid():N}",
            Producer: "NatureProtector.IntegrationTests",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: areaId,
            EventTime: timestamp,
            IngestTime: timestamp.AddSeconds(1),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: simulationRunId,
                SensorId: sensorId,
                SensorName: "Docker-RabbitMQ-Consumer-Sensor",
                MetricType: metricType,
                Unit: MeasurementUnit.Celsius,
                Value: 34.2,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private sealed class ConsumerPipelineHarness : IAsyncDisposable
    {
        private readonly TemporaryRabbitMqVirtualHost _virtualHost;
        private bool _workerStarted;

        private ConsumerPipelineHarness(
            TemporaryPostgresDatabase database,
            TemporaryRabbitMqVirtualHost virtualHost,
            RabbitMqOptions rabbitMqOptions,
            ConnectionFactory connectionFactory,
            RabbitMqReadingPublisher publisher,
            PreventionWorker worker,
            PreventionHostOptions preventionOptions,
            PostgresReadingEventInbox inbox,
            ReadingEventProcessingService processingService,
            Guid areaId,
            Guid gridCellId,
            Guid sensorId,
            Guid simulationRunId,
            DateTimeOffset timestamp)
        {
            Database = database;
            _virtualHost = virtualHost;
            RabbitMqOptions = rabbitMqOptions;
            ConnectionFactory = connectionFactory;
            Publisher = publisher;
            Worker = worker;
            PreventionOptions = preventionOptions;
            Inbox = inbox;
            ProcessingService = processingService;
            AreaId = areaId;
            GridCellId = gridCellId;
            SensorId = sensorId;
            SimulationRunId = simulationRunId;
            Timestamp = timestamp;
        }

        public TemporaryPostgresDatabase Database { get; }

        public RabbitMqOptions RabbitMqOptions { get; }

        public TemporaryRabbitMqVirtualHost VirtualHost => _virtualHost;

        public ConnectionFactory ConnectionFactory { get; }

        public RabbitMqReadingPublisher Publisher { get; }

        public PreventionWorker Worker { get; }

        public PreventionHostOptions PreventionOptions { get; }

        public PostgresReadingEventInbox Inbox { get; }

        public ReadingEventProcessingService ProcessingService { get; }

        public Guid AreaId { get; }

        public Guid GridCellId { get; }

        public Guid SensorId { get; }

        public Guid SimulationRunId { get; }

        public DateTimeOffset Timestamp { get; }

        public static async Task<ConsumerPipelineHarness> CreateAsync(
            IProcessingFaultInjector? processingFaultInjector = null,
            bool startWorker = true,
            bool observabilityRawEnabled = false)
        {
            var database = await TemporaryPostgresDatabase.CreateAsync();
            var exchangeName = $"np.it.consumer.{Guid.NewGuid():N}";
            var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(
                exchangeName,
                observabilityRawEnabled: observabilityRawEnabled);
            var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(baseOptions, CancellationToken.None);
            var rabbitMqOptions = virtualHost.CreateOptions(exchangeName);
            var connectionFactory = virtualHost.CreateConnectionFactory();
            var dbContextFactory = database.CreateFactory();
            var areaId = Guid.NewGuid();
            var gridCellId = Guid.NewGuid();
            var sensorId = Guid.NewGuid();
            var simulationRunId = Guid.NewGuid();
            var timestamp = DateTimeOffset.UtcNow.AddMinutes(1);

            await SeedControlPlaneAsync(
                database,
                Guid.NewGuid(),
                areaId,
                gridCellId,
                sensorId,
                simulationRunId,
                timestamp);

            var preventionOptions = new PreventionHostOptions
            {
                ConsumerPrefetchCount = 1,
                MaxProcessingAttempts = 3,
                RetryDelaySeconds = [0, 0],
                RetryPollingIntervalSeconds = 1
            };
            var preventionOptionsAccessor = Options.Create(preventionOptions);
            var inbox = new PostgresReadingEventInbox(
                dbContextFactory,
                NullLogger<PostgresReadingEventInbox>.Instance);
            var processingService = new ReadingEventProcessingService(
                NullLogger<ReadingEventProcessingService>.Instance,
                preventionOptionsAccessor,
                CreatePostgresPipeline(dbContextFactory),
                inbox,
                new PassThroughReadingSemanticValidator(),
                processingFaultInjector ?? new NoOpProcessingFaultInjector(),
                new DefaultProcessingFailureClassifier());
            var worker = new PreventionWorker(
                NullLogger<PreventionWorker>.Instance,
                Options.Create(rabbitMqOptions),
                preventionOptionsAccessor,
                inbox,
                processingService,
                new PreventionRuntimeState());
            var publisher = new RabbitMqReadingPublisher(
                NullLogger<RabbitMqReadingPublisher>.Instance,
                Options.Create(rabbitMqOptions));
            var harness = new ConsumerPipelineHarness(
                database,
                virtualHost,
                rabbitMqOptions,
                connectionFactory,
                publisher,
                worker,
                preventionOptions,
                inbox,
                processingService,
                areaId,
                gridCellId,
                sensorId,
                simulationRunId,
                timestamp);

            if (startWorker)
            {
                await harness.StartWorkerAsync();
            }

            return harness;
        }

        public async Task StartWorkerAsync()
        {
            if (_workerStarted)
            {
                return;
            }

            await Worker.StartAsync(CancellationToken.None);
            _workerStarted = true;
            await WaitForConsumerAsync(ConnectionFactory, RabbitMqOptions.IngestionReadingsQueueName);
        }

        public async Task StopWorkerAsync()
        {
            if (!_workerStarted)
            {
                return;
            }

            await Worker.StopAsync(CancellationToken.None);
            _workerStarted = false;
        }

        public PreventionWorker CreateWorker(IReadingEventInbox? inbox = null)
        {
            return new PreventionWorker(
                NullLogger<PreventionWorker>.Instance,
                Options.Create(RabbitMqOptions),
                Options.Create(PreventionOptions),
                inbox ?? Inbox,
                ProcessingService,
                new PreventionRuntimeState());
        }

        public InboxRetryWorker CreateRetryWorker()
        {
            return new InboxRetryWorker(
                NullLogger<InboxRetryWorker>.Instance,
                Options.Create(PreventionOptions),
                Inbox,
                ProcessingService);
        }

        public Task DeleteRabbitMqVirtualHostAsync()
        {
            return _virtualHost.DeleteAsync(CancellationToken.None);
        }

        public Task<bool> RabbitMqVirtualHostExistsAsync()
        {
            return _virtualHost.ExistsAsync(CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await StopWorkerAsync();
            Publisher.Dispose();
            await _virtualHost.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private sealed class ThrowingProcessingFaultInjector(Exception exception) : IProcessingFaultInjector
    {
        public ValueTask InjectAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            InboxProcessingLease lease,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }

    private sealed class StoreFailureSignalInbox(
        IReadingEventInbox inner,
        Guid expectedEventId) : IReadingEventInbox
    {
        private readonly TaskCompletionSource<Exception> _storeFailureObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<InboxStoreResult> StoreIncomingAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            ReadOnlyMemory<byte> rawBody,
            string stage,
            CancellationToken cancellationToken)
        {
            try
            {
                return await inner.StoreIncomingAsync(envelope, rawBody, stage, cancellationToken);
            }
            catch (Exception ex) when (envelope.EventId == expectedEventId)
            {
                _storeFailureObserved.TrySetResult(ex);
                throw;
            }
        }

        public async Task WaitForStoreFailureAsync()
        {
            var completed = await Task.WhenAny(
                _storeFailureObserved.Task,
                Task.Delay(TimeSpan.FromSeconds(10)));

            if (completed != _storeFailureObserved.Task)
            {
                throw new TimeoutException("The PostgreSQL outage did not reach inbox storage.");
            }

            await _storeFailureObserved.Task;
        }

        public Task StoreRejectedAsync(
            ReadOnlyMemory<byte> rawBody,
            string rejectionCode,
            string rejectionReason,
            RejectedEventMetadata? metadata,
            CancellationToken cancellationToken) =>
            inner.StoreRejectedAsync(rawBody, rejectionCode, rejectionReason, metadata, cancellationToken);

        public Task CompleteProcessingAsync(
            InboxProcessingLease lease,
            CancellationToken cancellationToken) =>
            inner.CompleteProcessingAsync(lease, cancellationToken);

        public Task ScheduleRetryAsync(
            InboxProcessingLease lease,
            string errorCode,
            string errorMessage,
            TimeSpan retryDelay,
            CancellationToken cancellationToken) =>
            inner.ScheduleRetryAsync(lease, errorCode, errorMessage, retryDelay, cancellationToken);

        public Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
            string stage,
            CancellationToken cancellationToken,
            TimeSpan? processingLeaseTimeout = null,
            int? maxProcessingAttempts = null) =>
            inner.TryStartDueRetryAsync(stage, cancellationToken, processingLeaseTimeout, maxProcessingAttempts);

        public Task QuarantineProcessingAsync(
            InboxProcessingLease lease,
            string errorCode,
            string errorMessage,
            string quarantineCode,
            string quarantineReason,
            string? errorMetadataJson,
            CancellationToken cancellationToken) =>
            inner.QuarantineProcessingAsync(
                lease,
                errorCode,
                errorMessage,
                quarantineCode,
                quarantineReason,
                errorMetadataJson,
                cancellationToken);
    }

    private sealed class DuplicateStoreSignalInbox(
        IReadingEventInbox inner,
        Guid expectedEventId) : IReadingEventInbox
    {
        private readonly TaskCompletionSource _duplicateObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<InboxStoreResult> StoreIncomingAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            ReadOnlyMemory<byte> rawBody,
            string stage,
            CancellationToken cancellationToken)
        {
            var result = await inner.StoreIncomingAsync(envelope, rawBody, stage, cancellationToken);
            if (result.IsDuplicate && envelope.EventId == expectedEventId)
            {
                _duplicateObserved.TrySetResult();
            }

            return result;
        }

        public async Task WaitForDuplicateAsync()
        {
            var completed = await Task.WhenAny(
                _duplicateObserved.Task,
                Task.Delay(TimeSpan.FromSeconds(10)));

            if (completed != _duplicateObserved.Task)
            {
                throw new TimeoutException("The redelivered inbox event was not observed as a duplicate.");
            }

            await _duplicateObserved.Task;
        }

        public Task StoreRejectedAsync(
            ReadOnlyMemory<byte> rawBody,
            string rejectionCode,
            string rejectionReason,
            RejectedEventMetadata? metadata,
            CancellationToken cancellationToken) =>
            inner.StoreRejectedAsync(rawBody, rejectionCode, rejectionReason, metadata, cancellationToken);

        public Task CompleteProcessingAsync(
            InboxProcessingLease lease,
            CancellationToken cancellationToken) =>
            inner.CompleteProcessingAsync(lease, cancellationToken);

        public Task ScheduleRetryAsync(
            InboxProcessingLease lease,
            string errorCode,
            string errorMessage,
            TimeSpan retryDelay,
            CancellationToken cancellationToken) =>
            inner.ScheduleRetryAsync(lease, errorCode, errorMessage, retryDelay, cancellationToken);

        public Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
            string stage,
            CancellationToken cancellationToken,
            TimeSpan? processingLeaseTimeout = null,
            int? maxProcessingAttempts = null) =>
            inner.TryStartDueRetryAsync(stage, cancellationToken, processingLeaseTimeout, maxProcessingAttempts);

        public Task QuarantineProcessingAsync(
            InboxProcessingLease lease,
            string errorCode,
            string errorMessage,
            string quarantineCode,
            string quarantineReason,
            string? errorMetadataJson,
            CancellationToken cancellationToken) =>
            inner.QuarantineProcessingAsync(
                lease,
                errorCode,
                errorMessage,
                quarantineCode,
                quarantineReason,
                errorMetadataJson,
                cancellationToken);
    }
}
