namespace NatureProtector.Shared.Messaging;

public sealed record EventEnvelope<TPayload>(
    string SchemaVersion,
    Guid EventId,
    string CorrelationId,
    string Producer,
    string EventType,
    Guid AreaId,
    DateTimeOffset EventTime,
    DateTimeOffset? IngestTime,
    TPayload Payload);