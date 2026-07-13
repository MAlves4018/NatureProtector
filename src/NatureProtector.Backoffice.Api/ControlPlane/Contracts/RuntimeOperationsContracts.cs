namespace NatureProtector.Backoffice.Api.ControlPlane.Contracts;

public sealed record RuntimeDiagnosticCatalogResponse(
    IReadOnlyList<RuntimeDiagnosticDefinitionResponse> Diagnostics);

public sealed record RuntimeDiagnosticDefinitionResponse(
    string Id,
    string Title,
    string Description);

public sealed record RuntimeDiagnosticRequest(
    string? AreaCode = null,
    int RecentMinutes = 30,
    string? ScenarioCode = null);

public sealed record RuntimeDiagnosticResultResponse(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    IReadOnlyList<string> Limitations);

public sealed record RuntimeTableCountResponse(
    string Schema,
    string Table,
    int Count);

public sealed record RuntimeRunTimingSummaryResponse(
    Guid SimulationRunId,
    double? RunDurationMs,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset? FirstInboxReceivedAt,
    DateTimeOffset? FirstProcessingAttemptStartedAt,
    DateTimeOffset? LastProcessingAttemptFinishedAt,
    DateTimeOffset? FirstRiskAssessmentCreatedAt,
    DateTimeOffset? FirstAlertTriggeredAt,
    double? TimeToFirstInboxMs,
    double? TimeToFirstProcessingAttemptMs,
    double? TimeToFirstRiskAssessmentMs,
    double? TimeToFirstAlertMs,
    RuntimeAttemptTimingSummaryResponse Attempts,
    IReadOnlyList<RuntimeStageTimingSummaryResponse> Stages,
    IReadOnlyList<string> Limitations,
    RuntimeDataScopeResponse? DataScope = null,
    IReadOnlyList<RuntimeTimelinePointResponse>? Timeline = null);

public sealed record RuntimeAttemptTimingSummaryResponse(
    int AttemptCount,
    int SuccessfulAttempts,
    int FailedAttempts,
    int QuarantinedAttempts,
    double? MinDurationMs,
    double? AvgDurationMs,
    double? MaxDurationMs);

public sealed record RuntimeStageTimingSummaryResponse(
    string Stage,
    string Outcome,
    string? ErrorCode,
    int Count,
    DateTimeOffset? FirstStartedAt,
    DateTimeOffset? LastFinishedAt,
    double? MinDurationMs,
    double? AvgDurationMs,
    double? MaxDurationMs);

public sealed record RuntimeFreshnessSummaryResponse(
    int FreshCount,
    int StaleCount,
    int ExpiredCount,
    DateTimeOffset? OldestIncludedAssessment,
    DateTimeOffset? LatestIncludedAssessment,
    int FreshSeconds,
    int StaleSeconds,
    string Note);

public sealed record RuntimeRunStartRequest(
    string AreaCode,
    string ScenarioCode,
    int? SensorCount,
    int? NumberOfCycles,
    int? IntervalSeconds,
    int? Seed,
    string? DegradationProfile,
    bool CollectEvidence = false,
    bool WaitForCompletion = false,
    int TimeoutSeconds = 180,
    bool AllowParallelRun = false,
    string? RunLabel = null,
    IReadOnlyList<string>? DegradationProfiles = null);

public sealed record RuntimeRunStartResponse(
    Guid RequestId,
    string OrchestratorCorrelationId,
    string Status,
    string Message,
    DateTimeOffset RequestedAtUtc,
    RuntimeRunOverrideValuesResponse Requested,
    RuntimeRunSummaryResponse? Run,
    IReadOnlyList<string> Warnings,
    string? LogDirectory,
    string? EvidenceDirectory,
    Guid? OperationId = null);

public sealed record RuntimeOperationResponse(
    Guid OperationId,
    Guid RequestId,
    string CorrelationId,
    Guid? SimulationRunId,
    string RequestedState,
    string ProviderState,
    string RunState,
    string ProcessingState,
    string State,
    string? TerminalOutcome,
    DateTimeOffset AcceptedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ProducerCompletedAt,
    DateTimeOffset? SystemCompletedAt,
    DateTimeOffset? FinishedAt,
    string? FailureCode,
    string? FailureDetail,
    string? EvidenceId,
    string? EvidenceLocation,
    RuntimeOperationAccountingResponse Accounting);

public sealed record RuntimeOperationAccountingResponse(
    int ExpectedObservations,
    int AcceptedObservations,
    int PendingInbox,
    int ProcessingInbox,
    int RetryPendingInbox,
    int ProcessedInbox,
    int QuarantinedInbox,
    bool Settled);

public sealed record RuntimeResetRequest(
    string Scope,
    string Confirm,
    bool DryRun);

public sealed record RuntimeResetResponse(
    DateTimeOffset GeneratedAtUtc,
    bool DryRun,
    string Status,
    string Message,
    IReadOnlyList<RuntimeTableCountResponse> Before,
    IReadOnlyList<RuntimeTableCountResponse> After);

public sealed record ControlledValidationP3AvailabilityResponse(
    string Phase,
    string Environment,
    bool Available,
    string Message,
    int MessageCount,
    int ExecutableCases,
    int BlockedCases);

public sealed record ControlledValidationP3RunRequest(
    string? RunLabel = null,
    bool WaitForCompletion = true,
    bool CollectEvidence = true,
    bool RunAuditAfterCompletion = false,
    int TimeoutSeconds = 300);

public sealed record ControlledValidationP3RunResponse(
    Guid RequestId,
    string RunLabel,
    string Phase,
    string Status,
    string Environment,
    string Message,
    DateTimeOffset RequestedAtUtc,
    int MessageCount,
    int ExecutableCases,
    int BlockedCases,
    string? EvidencePath,
    string? QueryPackPath,
    bool AuditRequired,
    RuntimeRunSummaryResponse? Run,
    IReadOnlyList<string> Notes);
