namespace NatureProtector.Backoffice.Api.ControlPlane.Contracts;

public static class RuntimeOperationalHealthStatus
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unhealthy = "Unhealthy";
    public const string Unknown = "Unknown";
    public const string NotInstrumented = "NotInstrumented";
    public const string NotApplicable = "NotApplicable";
}

public static class RuntimeMetricCollectionStatus
{
    public const string Measured = "Measured";
    public const string Unavailable = "Unavailable";
    public const string Error = "Error";
    public const string NotApplicable = "NotApplicable";
}

public sealed record RuntimeDataScopeResponse(
    Guid RequestedRunId,
    Guid? ResolvedRunId,
    Guid? DataRunId,
    DateTimeOffset ObservedAt,
    string Source,
    string Scope,
    IReadOnlyList<RuntimeLimitationResponse> Limitations);

public sealed record RuntimeTimelinePointResponse(
    string Stage,
    DateTimeOffset Timestamp,
    string Source,
    string Scope,
    Guid? EventId = null,
    string? Status = null);

public sealed record RuntimeOperationalHealthResponse(
    DateTimeOffset ObservedAt,
    IReadOnlyList<RuntimeOperationalHealthComponentResponse> Components,
    RabbitMqMetricsResponse RabbitMq,
    IReadOnlyList<RuntimeLimitationResponse> Limitations);

public sealed record RuntimeOperationalHealthComponentResponse(
    string Component,
    string Status,
    DateTimeOffset ObservedAt,
    string Source,
    string Reason,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    double? AgeSeconds,
    string Scope,
    string? Limitation);

public sealed record RabbitMqMetricsResponse(
    DateTimeOffset ObservedAt,
    string Source,
    string CollectionStatus,
    IReadOnlyList<RabbitMqQueueMetricResponse> Queues,
    IReadOnlyList<RuntimeLimitationResponse> Limitations);

public sealed record RabbitMqQueueMetricResponse(
    string QueueName,
    int? MessagesReady,
    int? MessagesUnacknowledged,
    int? MessagesTotal,
    int? Consumers,
    DateTimeOffset ObservedAt,
    string Source,
    string CollectionStatus,
    string? Limitation);

public sealed record RuntimeEvidenceCatalogResponse(
    DateTimeOffset ObservedAt,
    IReadOnlyList<RuntimeEvidenceItemResponse> Items,
    IReadOnlyList<RuntimeLimitationResponse> Limitations);

public sealed record RuntimeEvidenceItemResponse(
    string EvidenceId,
    string Title,
    string Type,
    DateTimeOffset? GeneratedAt,
    string Environment,
    string Scope,
    string? Version,
    bool ContentAvailable,
    bool DownloadAvailable,
    long Size,
    string Status,
    string? Limitation);

public sealed record RuntimeEvidenceContentResponse(
    RuntimeEvidenceItemResponse Metadata,
    string ContentType,
    byte[] Content,
    string CacheControl);
