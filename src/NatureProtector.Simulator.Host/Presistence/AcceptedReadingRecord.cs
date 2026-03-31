using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * This record represents one accepted reading as persisted by the Prevention.Host.
 *
 * Rationale:
 * - The raw event envelope contains transport-oriented metadata as well as payload.
 * - Persistence should keep the important operational fields in a flat and easily
 *   inspectable structure.
 *
 * Design considerations:
 * - The record keeps both event metadata and reading payload data.
 * - AcceptedAt is stored explicitly to distinguish event time from persistence time.
 * - The structure is intentionally append-only and suitable for NDJSON persistence.
 */

namespace NatureProtector.Prevention.Host.Persistence;

public sealed record AcceptedReadingRecord(
    Guid EventId,
    string CorrelationId,
    string Producer,
    string EventType,
    Guid AreaId,
    DateTimeOffset EventTime,
    DateTimeOffset AcceptedAt,
    Guid SimulationRunId,
    Guid SensorId,
    string SensorName,
    SensorMetricType MetricType,
    MeasurementUnit Unit,
    double Value,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState)
{
    /// <summary>
    /// Creates a persisted accepted-reading record from a validated event envelope.
    /// </summary>
    public static AcceptedReadingRecord FromEnvelope(
        EventEnvelope<SensorReadingProducedPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Payload);

        return new AcceptedReadingRecord(
            EventId: envelope.EventId,
            CorrelationId: envelope.CorrelationId,
            Producer: envelope.Producer,
            EventType: envelope.EventType,
            AreaId: envelope.AreaId,
            EventTime: envelope.EventTime,
            AcceptedAt: DateTimeOffset.UtcNow,
            SimulationRunId: envelope.Payload.SimulationRunId,
            SensorId: envelope.Payload.SensorId,
            SensorName: envelope.Payload.SensorName,
            MetricType: envelope.Payload.MetricType,
            Unit: envelope.Payload.Unit,
            Value: envelope.Payload.Value,
            Latitude: envelope.Payload.Latitude,
            Longitude: envelope.Payload.Longitude,
            OperationalState: envelope.Payload.OperationalState);
    }
}