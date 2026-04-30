using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
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
            inbox,
            new AlwaysValidReadingSemanticValidator());

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
            inbox,
            new AlwaysValidReadingSemanticValidator());

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
            inbox,
            new AlwaysValidReadingSemanticValidator());

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

    [Fact]
    public async Task ProcessAsync_CompletesWithoutRetryOrQuarantine_WhenOnlyInfluxFailsAndFailureIsTolerated()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var tolerantInfluxWriteService = new SafeInfluxWriteService(
            () => new ThrowingInfluxWriteService(),
            Options.Create(new InfluxDbOptions
            {
                Enabled = true,
                FailPipelineOnWriteError = false
            }),
            NullLogger<SafeInfluxWriteService>.Instance);
        var processingService = CreateService(
            CreatePipeline(
                new InMemoryAcceptedReadingRepository(),
                tolerantInfluxWriteService),
            inbox,
            new AlwaysValidReadingSemanticValidator());

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Processed, inboxEvent.Status);
        Assert.Single(inbox.Attempts);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.Succeeded,
            inbox.Attempts.Single().Outcome);
        Assert.Empty(inbox.Quarantines);
    }

    [Fact]
    public async Task ProcessAsync_Quarantines_WhenSemanticValidationFails()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var processingService = CreateService(
            CreatePipeline(
                acceptedReadingRepository,
                new FakeInfluxWriteService(),
                riskAssessmentRepository,
                areaSnapshotRepository),
            inbox,
            new InvalidReadingSemanticValidator(
                ReadingSemanticValidationReason.SensorAreaMismatch,
                "Sensor belongs to another area."));

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Quarantined, inboxEvent.Status);
        Assert.Empty(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Null(await areaSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        var quarantine = Assert.Single(inbox.Quarantines);
        Assert.Equal("sensor_area_mismatch", quarantine.QuarantineCode);
    }

    [Fact]
    public async Task ProcessAsync_Completes_WhenSemanticValidationPasses()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var processingService = CreateService(
            CreatePipeline(
                acceptedReadingRepository,
                new FakeInfluxWriteService(),
                riskAssessmentRepository,
                areaSnapshotRepository),
            inbox,
            new AlwaysValidReadingSemanticValidator());

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Processed, inbox.Events.Single().Status);
        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Single(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.NotNull(await areaSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_SchedulesRetry_WhenSemanticValidationThrowsTransientFailure()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create();
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var processingService = CreateService(
            CreatePipeline(new InMemoryAcceptedReadingRepository()),
            inbox,
            new ThrowingReadingSemanticValidator(new TimeoutException("control plane unavailable")));

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        var inboxEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.RetryPending, inboxEvent.Status);
        Assert.Empty(inbox.Quarantines);
    }

    [Fact]
    public async Task ProcessAsync_Completes_WhenReadingIsNotEligibleForRisk()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.WindDirection,
            unit: MeasurementUnit.Degrees,
            value: 180.0);
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var processingService = CreateService(
            CreatePipeline(
                acceptedReadingRepository,
                new FakeInfluxWriteService(),
                riskAssessmentRepository,
                areaSnapshotRepository,
                new NotEligibleRiskEligibilityService()),
            inbox,
            new AlwaysValidReadingSemanticValidator());

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Processed, inbox.Events.Single().Status);
        Assert.Single(inbox.Attempts);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.Succeeded,
            inbox.Attempts.Single().Outcome);
        Assert.Empty(inbox.Quarantines);
        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Null(await areaSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
    }

    private static ReadingEventProcessingService CreateService(
        ReadingRiskPipeline pipeline,
        IReadingEventInbox inbox,
        IReadingSemanticValidator validator)
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
            validator,
            new DefaultProcessingFailureClassifier());
    }

    private static ReadingRiskPipeline CreatePipeline(
        IAcceptedReadingRepository acceptedReadingRepository,
        IInfluxWriteService? influxWriteService = null,
        IRiskAssessmentRepository? riskAssessmentRepository = null,
        IAreaRiskSnapshotRepository? areaRiskSnapshotRepository = null,
        IRiskEligibilityService? riskEligibilityService = null)
    {
        return new ReadingRiskPipeline(
            acceptedReadingRepository,
            riskEligibilityService ?? new RiskEligibilityService(),
            new SimpleRiskScoringService(),
            riskAssessmentRepository ?? new InMemoryRiskAssessmentRepository(),
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository ?? new InMemoryAreaRiskSnapshotRepository(),
            new InMemoryAreaOperationalProjectionStore(),
            influxWriteService ?? new FakeInfluxWriteService(),
            NullLogger<ReadingRiskPipeline>.Instance);
    }

    private sealed class AlwaysValidReadingSemanticValidator : IReadingSemanticValidator
    {
        public Task<ReadingSemanticValidationResult> ValidateAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ReadingSemanticValidationResult.Valid);
        }
    }

    private sealed class InvalidReadingSemanticValidator(
        ReadingSemanticValidationReason reason,
        string message) : IReadingSemanticValidator
    {
        public Task<ReadingSemanticValidationResult> ValidateAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ReadingSemanticValidationResult.Invalid(reason, message));
        }
    }

    private sealed class ThrowingReadingSemanticValidator(Exception exception) : IReadingSemanticValidator
    {
        public Task<ReadingSemanticValidationResult> ValidateAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class NotEligibleRiskEligibilityService : IRiskEligibilityService
    {
        public Task<RiskEligibilityResult> EvaluateAsync(
            NatureProtector.Prevention.Readings.NormalizedReading reading,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(RiskEligibilityResult.NotEligible(
                RiskEligibilityReason.UnsupportedMetric,
                "Metric is not currently eligible for risk evaluation."));
        }
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
