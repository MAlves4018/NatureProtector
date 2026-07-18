namespace NatureProtector.Backoffice.Api.Operations.Contracts;

public sealed record CapabilityProfileResponse(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Capabilities,
    string Authority,
    DateTimeOffset EvaluatedAt);

public sealed record OperationInputDefinitionResponse(
    string Name,
    string Description,
    bool Required,
    string? DefaultValue);

public sealed record OperationDefinitionResponse(
    string OperationId,
    string Category,
    string DisplayName,
    string Description,
    string RequiredCapability,
    string RiskLevel,
    bool RequiresConfirmation,
    bool RequiresApproval,
    IReadOnlyList<string> Environments,
    IReadOnlyList<OperationInputDefinitionResponse> Inputs,
    string Workflow,
    string ConfirmationTemplate,
    bool Authorized,
    string Availability,
    string EvidenceLevel,
    string? Limitation);

public sealed record StartOperationRequest(
    string OperationId,
    string Environment,
    string? Ref,
    IReadOnlyDictionary<string, string>? Inputs,
    bool CollectEvidence,
    string? Confirmation);

public sealed record OperationDecisionRequest(
    string Decision,
    string? Comment);

public sealed record OperationStepResponse(
    int Sequence,
    string Name,
    string Status,
    DateTimeOffset At,
    string? Detail);

public sealed record OperationArtifactResponse(
    string ArtifactId,
    string Name,
    string Kind,
    string Reference,
    string? Sha256,
    long? SizeBytes,
    string EvidenceLevel);

public sealed record OperationApprovalResponse(
    string Decision,
    string Reviewer,
    DateTimeOffset At,
    string? Comment);

public sealed record EngineeringOperationResponse(
    Guid Id,
    string OperationId,
    string Category,
    string DisplayName,
    string Status,
    string Environment,
    string Ref,
    string RequestedBy,
    IReadOnlyList<string> RequestedByRoles,
    IReadOnlyList<string> RequestedByCapabilities,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt,
    bool CollectEvidence,
    string RiskLevel,
    bool RequiresApproval,
    string? Provider,
    string? ProviderReference,
    string? Workflow,
    string? PlanHash,
    string EvidenceLevel,
    IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyList<OperationStepResponse> Steps,
    IReadOnlyList<OperationArtifactResponse> Artifacts,
    IReadOnlyList<OperationApprovalResponse> Approvals,
    IReadOnlyList<string> Limitations,
    string? Detail);

public sealed record OperationComparisonResponse(
    Guid LeftOperationId,
    Guid RightOperationId,
    string LeftStatus,
    string RightStatus,
    IReadOnlyList<string> OnlyOnLeft,
    IReadOnlyList<string> OnlyOnRight,
    IReadOnlyList<string> SharedArtifacts,
    string EvidenceLevel);

public sealed record CloudEnvironmentResponse(
    string Environment,
    string ProjectId,
    string Region,
    bool Deployable,
    string ConfigurationSource,
    string ObservedState,
    string EvidenceLevel,
    IReadOnlyList<CloudResourceDeclarationResponse> Resources,
    IReadOnlyList<string> Limitations);

public sealed record CloudResourceDeclarationResponse(
    string ResourceType,
    string Name,
    string Scope,
    string State,
    string Source);

public sealed record OperationCallbackRequest(
    Guid OperationId,
    string Status,
    string? ProviderReference,
    IReadOnlyList<OperationArtifactResponse>? Artifacts,
    string? Detail);
