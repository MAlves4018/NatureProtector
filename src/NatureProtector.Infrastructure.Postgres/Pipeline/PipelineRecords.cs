namespace NatureProtector.Infrastructure.Postgres.Pipeline;

public enum InboxEventStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    Rejected = 4,
    RetryPending = 5,
    Quarantined = 6
}

public enum ProcessingAttemptOutcome
{
    Started = 0,
    Succeeded = 1,
    Failed = 2,
    RetryScheduled = 3,
    Quarantined = 4
}

public sealed class InboxEventRecord
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public Guid AreaId { get; set; }
    public Guid? SimulationRunId { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? IngestTime { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string EnvelopeJson { get; set; } = "{}";
    public InboxEventStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? LastProcessedAt { get; set; }
    public DateTimeOffset? NextAttemptNotBefore { get; set; }
    public DateTimeOffset? QuarantinedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }

    public List<ProcessingAttemptRecord> Attempts { get; set; } = [];
    public List<RejectedEventRecord> Rejections { get; set; } = [];
    public List<QuarantinedEventRecord> Quarantines { get; set; } = [];
}

public sealed class ProcessingAttemptRecord
{
    public Guid Id { get; set; }
    public Guid InboxEventId { get; set; }
    public int AttemptNumber { get; set; }
    public string Stage { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public ProcessingAttemptOutcome Outcome { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public InboxEventRecord? InboxEvent { get; set; }
}

public sealed class QuarantinedEventRecord
{
    public Guid Id { get; set; }
    public Guid InboxEventId { get; set; }
    public Guid EventId { get; set; }
    public int FinalAttemptNumber { get; set; }
    public string QuarantineCode { get; set; } = string.Empty;
    public string QuarantineReason { get; set; } = string.Empty;
    public DateTimeOffset QuarantinedAt { get; set; }
    public string? MetadataJson { get; set; }

    public InboxEventRecord? InboxEvent { get; set; }
}

public sealed class RejectedEventRecord
{
    public Guid Id { get; set; }
    public Guid? InboxEventId { get; set; }
    public Guid? EventId { get; set; }
    public string RejectionCode { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public DateTimeOffset RejectedAt { get; set; }
    public string RawBodyUtf8 { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }

    public InboxEventRecord? InboxEvent { get; set; }
}
