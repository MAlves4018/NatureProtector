using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
    public async Task ProcessAsync_RetriesThenSucceeds_WhenControlledTransientFaultIsInjected()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create() with
        {
            CorrelationId = "cv:p1-smoke:N5_TRANSIENT_FAILURE:001"
        };
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var processingService = CreateService(
            CreatePipeline(
                acceptedReadingRepository,
                new FakeInfluxWriteService(),
                riskAssessmentRepository),
            inbox,
            new AlwaysValidReadingSemanticValidator(),
            CreateControlledFaultInjector(
                runLabel: "p1-smoke",
                faultCaseId: "N5_TRANSIENT_FAILURE",
                faultKind: "transient_failure",
                failAttempts: 1));

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.RetryPending,
            inbox.Events.Single().Status);
        Assert.Equal("transient_failure", inbox.Attempts.Single().ErrorCode);
        Assert.Empty(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));

        var retryWorkItem = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None);

        Assert.NotNull(retryWorkItem);

        await processingService.ProcessAsync(
            retryWorkItem!.Envelope,
            retryWorkItem.Lease,
            CancellationToken.None);

        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Processed,
            inbox.Events.Single().Status);
        Assert.Equal(2, inbox.Attempts.Count);
        Assert.Contains(inbox.Attempts, attempt =>
            attempt.Outcome == NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.RetryScheduled &&
            attempt.ErrorCode == "transient_failure");
        Assert.Contains(inbox.Attempts, attempt =>
            attempt.Outcome == NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.Succeeded);
        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Single(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Empty(inbox.Quarantines);
    }

    [Fact]
    public async Task ProcessAsync_QuarantinesWithRetriesExhausted_WhenControlledTransientFaultExceedsMaxAttempts()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create() with
        {
            CorrelationId = "cv:p3-smoke:P3_RETRY_EXHAUSTED_TO_QUARANTINE:010"
        };
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var processingService = CreateService(
            CreatePipeline(
                acceptedReadingRepository,
                new FakeInfluxWriteService(),
                riskAssessmentRepository),
            inbox,
            new AlwaysValidReadingSemanticValidator(),
            CreateControlledFaultInjector(
                runLabel: "p3-smoke",
                faultCaseId: "P3_RETRY_EXHAUSTED_TO_QUARANTINE",
                faultKind: "transient_failure",
                failAttempts: 3));

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.RetryPending,
            inbox.Events.Single().Status);

        var secondAttempt = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None);
        Assert.NotNull(secondAttempt);

        await processingService.ProcessAsync(
            secondAttempt!.Envelope,
            secondAttempt.Lease,
            CancellationToken.None);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.RetryPending,
            inbox.Events.Single().Status);

        var thirdAttempt = await inbox.TryStartDueRetryAsync(
            "reading_risk_pipeline",
            CancellationToken.None);
        Assert.NotNull(thirdAttempt);

        await processingService.ProcessAsync(
            thirdAttempt!.Envelope,
            thirdAttempt.Lease,
            CancellationToken.None);

        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Quarantined,
            inbox.Events.Single().Status);
        Assert.Equal(3, inbox.Attempts.Count);
        Assert.Equal(
            2,
            inbox.Attempts.Count(attempt =>
                attempt.Outcome == NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.RetryScheduled &&
                attempt.ErrorCode == "transient_failure"));
        Assert.Contains(inbox.Attempts, attempt =>
            attempt.AttemptNumber == 3 &&
            attempt.Outcome == NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.Quarantined &&
            attempt.ErrorCode == "transient_failure");
        var quarantine = Assert.Single(inbox.Quarantines);
        Assert.Equal("retries_exhausted", quarantine.QuarantineCode);
        Assert.Equal(3, quarantine.FinalAttemptNumber);
        Assert.Empty(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_QuarantinesWithoutProjections_WhenControlledPermanentFaultIsInjected()
    {
        var inbox = new InMemoryReadingEventInbox();
        var envelope = EnvelopeFactory.Create() with
        {
            CorrelationId = "cv:p1-smoke:N6_PERMANENT_FAILURE:002"
        };
        var storeResult = await inbox.StoreIncomingAsync(
            envelope,
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            "reading_risk_pipeline",
            CancellationToken.None);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var processingService = CreateService(
            CreatePipeline(
                acceptedReadingRepository,
                new FakeInfluxWriteService(),
                riskAssessmentRepository),
            inbox,
            new AlwaysValidReadingSemanticValidator(),
            CreateControlledFaultInjector(
                runLabel: "p1-smoke",
                faultCaseId: "N6_PERMANENT_FAILURE",
                faultKind: "permanent_failure"));

        await processingService.ProcessAsync(
            envelope,
            storeResult.Lease!,
            CancellationToken.None);

        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Quarantined,
            inbox.Events.Single().Status);
        Assert.Equal("permanent_failure", inbox.Attempts.Single().ErrorCode);
        var quarantine = Assert.Single(inbox.Quarantines);
        Assert.Equal("permanent_failure", quarantine.QuarantineCode);
        Assert.Empty(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
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
        IReadingSemanticValidator validator,
        IProcessingFaultInjector? processingFaultInjector = null)
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
            processingFaultInjector ?? new NoOpProcessingFaultInjector(),
            new DefaultProcessingFailureClassifier());
    }

    private static IProcessingFaultInjector CreateControlledFaultInjector(
        string runLabel,
        string faultCaseId,
        string faultKind,
        int failAttempts = 1)
    {
        return new ControlledValidationProcessingFaultInjector(
            Options.Create(new ControlledValidationProcessingFaultOptions
            {
                Enabled = true,
                Cases =
                [
                    new ProcessingFaultCaseOptions
                    {
                        RunLabel = runLabel,
                        FaultCaseId = faultCaseId,
                        FaultKind = faultKind,
                        FailAttempts = failAttempts
                    }
                ]
            }),
            new TestHostEnvironment("Evidence"),
            NullLogger<ControlledValidationProcessingFaultInjector>.Instance);
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
            new InMemoryDailyCellStateRepository(),
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

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "NatureProtector.Prevention.Host.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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
