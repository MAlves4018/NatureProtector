using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

internal sealed class CloudRunJobRuntimeRunOrchestrator(
    ICloudRunExecutionStore store,
    ICloudRunJobsGateway gateway,
    IOptions<RuntimeOrchestrationOptions> options,
    ILogger<CloudRunJobRuntimeRunOrchestrator> logger) : IRuntimeRunOrchestrator
{
    private readonly RuntimeOrchestrationOptions _options = options.Value;

    public bool IsAvailable => true;
    public string AvailabilityMessage =>
        $"Cloud Run Job runtime orchestration is enabled for '{_options.CloudRunSimulatorJobName}' in '{_options.CloudRunRegion}'.";

    public async Task<RuntimeLaunchReceipt> StartAsync(RuntimeLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
        var reservation = await store.ReserveAsync(
            request,
            TimeSpan.FromSeconds(_options.CloudRunLaunchLeaseSeconds),
            cancellationToken);
        var record = reservation.Record;

        if (!reservation.OwnsLaunch)
        {
            return ToReceipt(record, reused: true);
        }

        try
        {
            var operation = await gateway.StartAsync(request, record.ExecutionId, cancellationToken);
            var attached = await store.AttachOperationAsync(
                record.ExecutionId,
                reservation.LeaseToken,
                operation,
                cancellationToken);
            if (!attached)
            {
                try
                {
                    await gateway.CancelAsync(operation, executionName: null, cancellationToken);
                }
                catch (Exception cancellationException)
                {
                    logger.LogCritical(
                        cancellationException,
                        "Cloud Run operation could not be attached after the launch lease was lost and cancellation also failed. Operation={OperationName} ExecutionId={ExecutionId}",
                        operation,
                        record.ExecutionId.Value);
                }

                var current = await store.GetAsync(record.ExecutionId, cancellationToken) ?? record;
                return ToReceipt(current, reused: true);
            }

            record = record with
            {
                ProviderOperationName = operation,
                State = RuntimeExecutionState.Running,
                StartedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                LaunchLeaseToken = null,
                LaunchLeaseUntilUtc = null
            };

            if (request.WaitForCompletion)
            {
                return ToReceipt(await WaitForCompletionAsync(record, request.Timeout, cancellationToken), reservation.ReusedExistingExecution);
            }

            return ToReceipt(record, reservation.ReusedExistingExecution);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var failed = record with
            {
                State = RuntimeExecutionState.Failed,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                FailureCode = "cloud_run_job_start_failed",
                FailureMessage = exception.Message,
                LaunchLeaseToken = null,
                LaunchLeaseUntilUtc = null
            };
            await store.UpdateAsync(failed, CancellationToken.None);
            logger.LogError(exception, "Cloud Run Simulator launch failed. ExecutionId={ExecutionId}", record.ExecutionId.Value);
            return ToReceipt(failed, reservation.ReusedExistingExecution);
        }
    }

    public async Task<RuntimeExecutionSnapshot?> GetAsync(RuntimeExecutionId executionId, CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(executionId, cancellationToken);
        if (record is null) return null;
        if (IsTerminal(record.State) || string.IsNullOrWhiteSpace(record.ProviderOperationName)) return ToSnapshot(record);

        try
        {
            var operation = await gateway.GetAsync(record.ProviderOperationName, cancellationToken);
            var state = operation.Done
                ? operation.Failed ? RuntimeExecutionState.Failed : RuntimeExecutionState.Succeeded
                : RuntimeExecutionState.Running;
            var updated = record with
            {
                ProviderExecutionName = operation.ExecutionName ?? record.ProviderExecutionName,
                State = state,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                StartedAtUtc = operation.StartedAtUtc ?? record.StartedAtUtc,
                FinishedAtUtc = operation.Done ? operation.FinishedAtUtc ?? DateTimeOffset.UtcNow : null,
                FailureCode = operation.FailureCode,
                FailureMessage = operation.FailureMessage
            };
            await store.UpdateAsync(updated, cancellationToken);
            return ToSnapshot(updated);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cloud Run operation refresh failed. ExecutionId={ExecutionId}", executionId.Value);
            return ToSnapshot(record with
            {
                State = RuntimeExecutionState.Unknown,
                FailureCode = "cloud_run_operation_refresh_failed",
                FailureMessage = exception.Message,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<RuntimeStopReceipt> StopAsync(
        RuntimeExecutionId executionId,
        RuntimeStopReason reason,
        CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(executionId, cancellationToken);
        if (record is null)
        {
            return new RuntimeStopReceipt(executionId, RuntimeExecutionState.Unknown, false, "Runtime execution was not found.");
        }
        if (IsTerminal(record.State))
        {
            return new RuntimeStopReceipt(executionId, record.State, false, "Runtime execution is already terminal.");
        }
        if (string.IsNullOrWhiteSpace(record.ProviderOperationName))
        {
            return new RuntimeStopReceipt(executionId, record.State, false, "Runtime launch has not yet received a provider operation.");
        }

        try
        {
            await gateway.CancelAsync(record.ProviderOperationName, record.ProviderExecutionName, cancellationToken);
            var cancelled = record with
            {
                State = RuntimeExecutionState.Cancelled,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                FailureCode = "cancelled",
                FailureMessage = $"Cancellation accepted. Reason={reason}."
            };
            await store.UpdateAsync(cancelled, cancellationToken);
            return new RuntimeStopReceipt(executionId, cancelled.State, true, cancelled.FailureMessage);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Cloud Run execution cancellation failed. ExecutionId={ExecutionId}", executionId.Value);
            return new RuntimeStopReceipt(executionId, record.State, false, exception.Message);
        }
    }

    private async Task<CloudRunExecutionRecord> WaitForCompletionAsync(
        CloudRunExecutionRecord record,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await GetAsync(record.ExecutionId, cancellationToken);
            var current = await store.GetAsync(record.ExecutionId, cancellationToken) ?? record;
            if (snapshot is null || IsTerminal(snapshot.State))
            {
                return current;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var delay = TimeSpan.FromSeconds(_options.CloudRunPollIntervalSeconds);
            await Task.Delay(remaining < delay ? remaining : delay, cancellationToken);
        }

        var latest = await store.GetAsync(record.ExecutionId, cancellationToken) ?? record;
        if (IsTerminal(latest.State))
        {
            return latest;
        }

        string timeoutMessage;
        try
        {
            if (!string.IsNullOrWhiteSpace(latest.ProviderOperationName))
            {
                await gateway.CancelAsync(latest.ProviderOperationName, latest.ProviderExecutionName, cancellationToken);
            }
            timeoutMessage = $"Cloud Run execution exceeded the requested timeout of {timeout}. Cancellation was requested.";
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Cloud Run execution timed out and cancellation failed. ExecutionId={ExecutionId}",
                latest.ExecutionId.Value);
            timeoutMessage = $"Cloud Run execution exceeded the requested timeout of {timeout}; cancellation failed: {exception.Message}";
        }

        var timedOut = latest with
        {
            State = RuntimeExecutionState.TimedOut,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            FailureCode = "cloud_run_execution_timed_out",
            FailureMessage = timeoutMessage
        };
        await store.UpdateAsync(timedOut, CancellationToken.None);
        return timedOut;
    }

    private static RuntimeLaunchReceipt ToReceipt(CloudRunExecutionRecord record, bool reused) => new(
        record.ExecutionId,
        record.State,
        record.AcceptedAtUtc,
        record.ProviderExecutionName ?? record.ProviderOperationName,
        record.LogCorrelation,
        reused,
        record.State == RuntimeExecutionState.Rejected ? record.FailureCode : null,
        record.FailureMessage,
        record.Evidence);

    private static RuntimeExecutionSnapshot ToSnapshot(CloudRunExecutionRecord record) => new(
        record.ExecutionId,
        record.State,
        record.UpdatedAtUtc,
        record.StartedAtUtc,
        record.FinishedAtUtc,
        record.State == RuntimeExecutionState.Succeeded ? 0 : null,
        record.FailureCode,
        record.FailureMessage,
        record.LogCorrelation,
        record.Evidence);

    private static bool IsTerminal(RuntimeExecutionState state) => state is RuntimeExecutionState.Succeeded
        or RuntimeExecutionState.Failed or RuntimeExecutionState.TimedOut
        or RuntimeExecutionState.Cancelled or RuntimeExecutionState.Rejected;
}
