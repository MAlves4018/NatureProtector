using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Readings;

public sealed record NormalizedReading(
    Guid EventId,
    string CorrelationId,
    Guid AreaId,
    Guid SensorId,
    string SensorName,
    SensorMetricType MetricType,
    double Value,
    MeasurementUnit Unit,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState,
    DateTimeOffset EventTime,
    DateTimeOffset? IngestTime)
{
    public static NormalizedReading FromEnvelope(EventEnvelope<SensorReadingProducedPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new NormalizedReading(
            EventId: envelope.EventId,
            CorrelationId: envelope.CorrelationId,
            AreaId: envelope.AreaId,
            SensorId: envelope.Payload.SensorId,
            SensorName: envelope.Payload.SensorName,
            MetricType: envelope.Payload.MetricType,
            Value: envelope.Payload.Value,
            Unit: envelope.Payload.Unit,
            Latitude: envelope.Payload.Latitude,
            Longitude: envelope.Payload.Longitude,
            OperationalState: envelope.Payload.OperationalState,
            EventTime: envelope.EventTime,
            IngestTime: envelope.IngestTime);
    }
}
