using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.Fakes;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class InboxRetryWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ContinuesPolling_AfterUnexpectedInboxFailure()
    {
        var retryInbox = new ThrowOnceReadingEventInbox();
        var worker = new InboxRetryWorker(
            NullLogger<InboxRetryWorker>.Instance,
            Options.Create(new PreventionHostOptions
            {
                PipelinePersistenceEnabled = false,
                MaxProcessingAttempts = 3,
                RetryDelaySeconds = [0, 0],
                RetryPollingIntervalSeconds = 1
            }),
            retryInbox,
            CreateProcessingService());

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var executeTask = InvokeExecuteAsync(worker, cancellationSource.Token);

        await retryInbox.SecondPollReached.Task.WaitAsync(TimeSpan.FromSeconds(3));

        cancellationSource.Cancel();
        await executeTask;

        Assert.True(retryInbox.CallCount >= 2);
    }

    private static ReadingEventProcessingService CreateProcessingService()
    {
        var pipeline = new ReadingRiskPipeline(
            new InMemoryAcceptedReadingRepository(),
            new RiskEligibilityService(),
            new SimpleRiskScoringService(),
            new InMemoryRiskAssessmentRepository(),
            new AreaRiskSnapshotService(),
            new InMemoryAreaRiskSnapshotRepository(),
            new InMemoryAreaOperationalProjectionStore(),
            new FakeInfluxWriteService(),
            NullLogger<ReadingRiskPipeline>.Instance);

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
            new InMemoryReadingEventInbox(),
            new PassThroughReadingSemanticValidator(),
            new DefaultProcessingFailureClassifier());
    }

    private static Task InvokeExecuteAsync(
        InboxRetryWorker worker,
        CancellationToken cancellationToken)
    {
        var method = typeof(InboxRetryWorker).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteAsync method was not found.");

        return method.Invoke(worker, [cancellationToken]) as Task
            ?? throw new InvalidOperationException("ExecuteAsync did not return a Task.");
    }

    private sealed class ThrowOnceReadingEventInbox : IReadingEventInbox
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public TaskCompletionSource<bool> SecondPollReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<InboxStoreResult> StoreIncomingAsync(EventEnvelope<SensorReadingProducedPayload> envelope,
            ReadOnlyMemory<byte> rawBody,
            string stage,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task StoreRejectedAsync(ReadOnlyMemory<byte> rawBody,
            string rejectionCode,
            string rejectionReason,
            RejectedEventMetadata? metadata,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task CompleteProcessingAsync(
            InboxProcessingLease lease,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ScheduleRetryAsync(
            InboxProcessingLease lease,
            string errorCode,
            string errorMessage,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
            string stage,
            CancellationToken cancellationToken,
            TimeSpan? processingLeaseTimeout = null,
            int? maxProcessingAttempts = null)
        {
            var callCount = Interlocked.Increment(ref _callCount);

            if (callCount == 1)
            {
                throw new InvalidOperationException("boom");
            }

            SecondPollReached.TrySetResult(true);
            return Task.FromResult<InboxRetryWorkItem?>(null);
        }

        public Task QuarantineProcessingAsync(
            InboxProcessingLease lease,
            string errorCode,
            string errorMessage,
            string quarantineCode,
            string quarantineReason,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
