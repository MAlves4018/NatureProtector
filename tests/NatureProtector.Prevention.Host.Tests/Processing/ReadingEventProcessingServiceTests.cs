using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.Fakes;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class ReadingEventProcessingServiceTests
{
    [Fact]
    public async Task ProcessAsync_SchedulesRetry_ForTransientFailures()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        var processingService = CreateService(
            CreatePipeline(new TimeoutThrowingAcceptedReadingRepository()),
            inbox);

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.RetryPending, inboxEvent.Status);
        Assert.Single(inbox.Attempts);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.RetryScheduled,
            inbox.Attempts.Single().Outcome);
        Assert.Empty(inbox.Quarantines);
    }

    [Fact]
    public async Task ProcessAsync_QuarantinesPermanentFailures()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        var processingService = CreateService(
            CreatePipeline(new PermanentThrowingAcceptedReadingRepository()),
            inbox);

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Quarantined, inboxEvent.Status);
        Assert.Single(inbox.Attempts);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.Quarantined,
            inbox.Attempts.Single().Outcome);
        var quarantine = Assert.Single(inbox.Quarantines);
        Assert.Equal("permanent_failure", quarantine.QuarantineCode);
    }

    [Fact]
    public async Task ProcessAsync_Completes_WhenRetryWorkItemSucceedsLater()
    {
        var acceptedReadingRepository = new FlakyAcceptedReadingRepository();
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);

        var processingService = CreateService(
            CreatePipeline(acceptedReadingRepository),
            inbox);

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        var retryWorkItem = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.NotNull(retryWorkItem);

        await processingService.ProcessAsync(
            retryWorkItem!.Envelope,
            retryWorkItem.Lease,
            CancellationToken.None);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Processed, inboxEvent.Status);
        Assert.Equal(2, inbox.Attempts.Count);
        Assert.Equal(1, acceptedReadingRepository.StoredCount);
        Assert.Empty(inbox.Quarantines);
    }

    private static ReadingEventProcessingService CreateService(
        ReadingRiskPipeline pipeline,
        IReadingEventInbox inbox)
    {
        return new ReadingEventProcessingService(
            NullLogger<ReadingEventProcessingService>.Instance,
            Options.Create(new PreventionHostOptions
            {
                PipelinePersistenceEnabled = false,
                MaxProcessingAttempts = 3,
                RetryDelaySeconds = [0, 0],
                RetryPollingIntervalSeconds = 1
            }),
            pipeline,
            inbox,
            new DefaultProcessingFailureClassifier());
    }

    private static ReadingRiskPipeline CreatePipeline(IAcceptedReadingRepository acceptedReadingRepository)
    {
        return new ReadingRiskPipeline(
            acceptedReadingRepository,
            new SimpleRiskScoringService(),
            new InMemoryRiskAssessmentRepository(),
            new AreaRiskSnapshotService(),
            new InMemoryAreaRiskSnapshotRepository(),
            new InMemoryAreaOperationalProjectionStore(),
            new FakeInfluxWriteService(),
            NullLogger<ReadingRiskPipeline>.Instance);
    }

    private sealed class TimeoutThrowingAcceptedReadingRepository : IAcceptedReadingRepository
    {
        public Task AddAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            throw new TimeoutException("boom");
        }

        public Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>>([]);
        }
    }

    private sealed class PermanentThrowingAcceptedReadingRepository : IAcceptedReadingRepository
    {
        public Task AddAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            throw new ArgumentException("broken input");
        }

        public Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>>([]);
        }
    }

    private sealed class FlakyAcceptedReadingRepository : IAcceptedReadingRepository
    {
        private int _attempts;
        public int StoredCount { get; private set; }

        public Task AddAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            _attempts++;

            if (_attempts == 1)
            {
                throw new TimeoutException("temporary outage");
            }

            StoredCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>>([]);
        }
    }
}
