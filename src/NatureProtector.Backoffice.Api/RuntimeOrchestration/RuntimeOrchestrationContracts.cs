namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public readonly record struct RuntimeExecutionId(Guid Value);

public enum RuntimeExecutionState
{
    Accepted,
    Starting,
    Running,
    Succeeded,
    Failed,
    TimedOut,
    Cancelled,
    Rejected,
    Unknown
}

public enum RuntimeLaunchProfile
{
    Simulation,
    ControlledValidationP3
}

public sealed record RuntimeSimulationParameters(
    string AreaCode,
    string ScenarioCode,
    int? SensorCount,
    int? NumberOfCycles,
    int? IntervalSeconds,
    int? Seed,
    string? LegacyDegradationProfile,
    IReadOnlyList<string> DegradationProfiles,
    string OrchestratorCorrelationId);

public sealed record RuntimeControlledValidationParameters(
    string Phase,
    Guid ControlledValidationRunId,
    string RunLabel,
    string ScenarioCode,
    Guid AreaId,
    Guid SimulationRunId,
    Guid NominalSensorId,
    string NominalSensorName,
    Guid SensorNotFoundId,
    DateTimeOffset EventTime,
    string EvidenceOutputReference);

public sealed record RuntimeLaunchRequest(
    Guid RequestId,
    string IdempotencyKey,
    string Environment,
    RuntimeLaunchProfile Profile,
    RuntimeSimulationParameters Simulation,
    RuntimeControlledValidationParameters? ControlledValidation,
    bool CollectEvidence,
    bool WaitForCompletion,
    TimeSpan Timeout,
    RuntimeEvidenceReference? Evidence);

public sealed record RuntimeEvidenceReference(
    string EvidenceId,
    string Location);

public sealed record RuntimeLaunchReceipt(
    RuntimeExecutionId ExecutionId,
    RuntimeExecutionState State,
    DateTimeOffset AcceptedAtUtc,
    string? ProviderReference,
    string LogCorrelation,
    bool ReusedExistingExecution,
    string? RejectionCode,
    string? Message,
    RuntimeEvidenceReference? Evidence);

public sealed record RuntimeExecutionSnapshot(
    RuntimeExecutionId ExecutionId,
    RuntimeExecutionState State,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int? ExitCode,
    string? FailureCode,
    string? FailureMessage,
    string LogCorrelation,
    RuntimeEvidenceReference? Evidence);

public enum RuntimeStopReason
{
    UserRequest,
    Timeout,
    Superseded,
    OperationalSafety
}

public sealed record RuntimeStopReceipt(
    RuntimeExecutionId ExecutionId,
    RuntimeExecutionState State,
    bool StopAccepted,
    string? Message);

public interface IRuntimeRunOrchestrator
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    Task<RuntimeLaunchReceipt> StartAsync(
        RuntimeLaunchRequest request,
        CancellationToken cancellationToken);

    Task<RuntimeExecutionSnapshot?> GetAsync(
        RuntimeExecutionId executionId,
        CancellationToken cancellationToken);

    Task<RuntimeStopReceipt> StopAsync(
        RuntimeExecutionId executionId,
        RuntimeStopReason reason,
        CancellationToken cancellationToken);
}

public interface IRuntimeEvidenceSink
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    Task<RuntimeEvidenceReference> CreateAsync(
        string category,
        DateTimeOffset requestedAtUtc,
        string label,
        CancellationToken cancellationToken);

    Task WriteJsonAsync(
        RuntimeEvidenceReference evidence,
        string fileName,
        object value,
        CancellationToken cancellationToken);

    Task WriteTextAsync(
        RuntimeEvidenceReference evidence,
        string fileName,
        string value,
        CancellationToken cancellationToken);
}
