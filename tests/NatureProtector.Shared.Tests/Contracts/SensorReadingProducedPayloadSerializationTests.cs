using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Tests.Contracts;

public sealed class SensorReadingProducedPayloadSerializationTests
{
    [Fact]
    public void Payload_RoundTripsInsideEnvelope_WithExpectedEnums()
    {
        var envelope = new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-002",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 6, 12, 0, 1, TimeSpan.Zero),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor W-01",
                MetricType: SensorMetricType.WindSpeed,
                Unit: MeasurementUnit.MetersPerSecond,
                Value: 12.8,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Retransmitted));

        var json = JsonEventSerializer.SerializeToString(envelope);
        var roundTrip = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            JsonEventSerializer.SerializeToUtf8Bytes(envelope));

        Assert.Contains("\"metricType\":\"WindSpeed\"", json);
        Assert.Contains("\"unit\":\"MetersPerSecond\"", json);
        Assert.Contains("\"operationalState\":\"Retransmitted\"", json);
        Assert.NotNull(roundTrip);
        Assert.Equal(envelope.Payload, roundTrip.Payload);
        Assert.Equal(envelope.IngestTime, roundTrip.IngestTime);
    }
}
