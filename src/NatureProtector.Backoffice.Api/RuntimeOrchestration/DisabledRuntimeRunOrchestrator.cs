namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public sealed class DisabledRuntimeRunOrchestrator : IRuntimeRunOrchestrator
{
    public static DisabledRuntimeRunOrchestrator Instance { get; } = new();

    private DisabledRuntimeRunOrchestrator()
    {
    }

    public string Provider => "disabled";

    public bool IsAvailable => false;

    public string AvailabilityMessage =>
        "Runtime process launch is disabled. Configure RuntimeOrchestration:Mode=LocalProcess only for an explicit local development profile.";

    public Task<RuntimeLaunchReceipt> StartAsync(
        RuntimeLaunchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RuntimeLaunchReceipt(
            request.ExecutionId,
            RuntimeExecutionState.Rejected,
            DateTimeOffset.UtcNow,
            null,
            request.Simulation.OrchestratorCorrelationId,
            false,
            "orchestration_disabled",
            AvailabilityMessage,
            request.Evidence));
    }

    public Task<RuntimeExecutionSnapshot?> GetAsync(
        RuntimeExecutionId executionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<RuntimeExecutionSnapshot?>(null);
    }

    public Task<RuntimeStopReceipt> StopAsync(
        RuntimeExecutionId executionId,
        RuntimeStopReason reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RuntimeStopReceipt(
            executionId,
            RuntimeExecutionState.Rejected,
            false,
            AvailabilityMessage));
    }
}
