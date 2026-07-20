using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Tests.TestData;

internal static class EnvelopeFactory
{
    public static EventEnvelope<SensorReadingProducedPayload> Create(
        Guid? areaId = null,
        Guid? eventId = null,
        Guid? simulationRunId = null,
        Guid? sensorId = null,
        string sensorName = "Sensor-01",
        SensorMetricType metricType = SensorMetricType.Temperature,
        MeasurementUnit unit = MeasurementUnit.Celsius,
        double value = 32.5,
        double latitude = 39.8,
        double longitude = -7.9,
        SensorOperationalState operationalState = SensorOperationalState.Nominal,
        DateTimeOffset? eventTime = null,
        DateTimeOffset? publishedAt = null)
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: eventId ?? Guid.NewGuid(),
            CorrelationId: $"{(simulationRunId ?? Guid.NewGuid()):N}-{(sensorId ?? Guid.NewGuid()):N}",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: areaId ?? Guid.NewGuid(),
            EventTime: eventTime ?? new DateTimeOffset(2026, 4, 6, 18, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: simulationRunId ?? Guid.NewGuid(),
                SensorId: sensorId ?? Guid.NewGuid(),
                SensorName: sensorName,
                MetricType: metricType,
                Unit: unit,
                Value: value,
                Latitude: latitude,
                Longitude: longitude,
                OperationalState: operationalState),
            PublishedAt: publishedAt);
    }
}
