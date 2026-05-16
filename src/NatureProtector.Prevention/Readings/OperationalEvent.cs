using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Readings;

/// <summary>
/// Adapter interno que traduz o envelope de transporte para o evento
/// operacional consumido pela pipeline de prevenção.
/// </summary>
public sealed record OperationalEvent(
    string SchemaVersion,
    Guid EventId,
    string CorrelationId,
    string Producer,
    string EventType,
    Guid AreaId,
    Guid SimulationRunId,
    Guid SensorId,
    string SensorName,
    SensorMetricType MetricType,
    double Value,
    MeasurementUnit Unit,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState,
    DateTimeOffset EventTime,
    DateTimeOffset? IngestTime,
    IReadOnlyList<string> QualityFlags,
    IReadOnlyList<ClassifierResult> ClassifierResults)
{
    private static readonly IReadOnlyList<string> EmptyQualityFlags = Array.Empty<string>();
    private static readonly IReadOnlyList<ClassifierResult> EmptyClassifierResults = Array.Empty<ClassifierResult>();

    public static OperationalEvent FromEnvelope(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        IReadOnlyList<string>? qualityFlags = null,
        IReadOnlyList<ClassifierResult>? classifierResults = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new OperationalEvent(
            SchemaVersion: envelope.SchemaVersion,
            EventId: envelope.EventId,
            CorrelationId: envelope.CorrelationId,
            Producer: envelope.Producer,
            EventType: envelope.EventType,
            AreaId: envelope.AreaId,
            SimulationRunId: envelope.Payload.SimulationRunId,
            SensorId: envelope.Payload.SensorId,
            SensorName: envelope.Payload.SensorName,
            MetricType: envelope.Payload.MetricType,
            Value: envelope.Payload.Value,
            Unit: envelope.Payload.Unit,
            Latitude: envelope.Payload.Latitude,
            Longitude: envelope.Payload.Longitude,
            OperationalState: envelope.Payload.OperationalState,
            EventTime: envelope.EventTime,
            IngestTime: envelope.IngestTime,
            QualityFlags: qualityFlags ?? EmptyQualityFlags,
            ClassifierResults: classifierResults ?? EmptyClassifierResults);
    }
}
