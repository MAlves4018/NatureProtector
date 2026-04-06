using System.Text;
using System.Text.Json;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Tests.Messaging;

public sealed class JsonEventSerializerTests
{
    [Fact]
    public void SerializeToString_UsesCamelCase_OmitsNulls_AndSerializesEnumsAsStrings()
    {
        var envelope = CreateEnvelope();

        var json = JsonEventSerializer.SerializeToString(envelope);

        Assert.Contains("\"schemaVersion\":\"1.0\"", json);
        Assert.Contains("\"eventId\":", json);
        Assert.Contains("\"correlationId\":\"corr-001\"", json);
        Assert.Contains("\"eventType\":\"SensorReadingProduced\"", json);
        Assert.Contains("\"metricType\":\"Temperature\"", json);
        Assert.Contains("\"unit\":\"Celsius\"", json);
        Assert.Contains("\"operationalState\":\"Nominal\"", json);
        Assert.DoesNotContain("\"ingestTime\":", json);
    }

    [Fact]
    public void SerializeToUtf8Bytes_AndDeserialize_RoundTripEnvelope()
    {
        var envelope = CreateEnvelope();

        var bytes = JsonEventSerializer.SerializeToUtf8Bytes(envelope);
        var roundTrip = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(bytes);

        Assert.NotNull(roundTrip);
        Assert.Equal(envelope.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(envelope.EventId, roundTrip.EventId);
        Assert.Equal(envelope.CorrelationId, roundTrip.CorrelationId);
        Assert.Equal(envelope.Producer, roundTrip.Producer);
        Assert.Equal(envelope.EventType, roundTrip.EventType);
        Assert.Equal(envelope.AreaId, roundTrip.AreaId);
        Assert.Equal(envelope.EventTime, roundTrip.EventTime);
        Assert.Equal(envelope.Payload, roundTrip.Payload);
    }

    [Fact]
    public void Deserialize_Throws_WhenJsonIsInvalid()
    {
        var invalidJson = Encoding.UTF8.GetBytes("{ invalid");

        Assert.Throws<JsonException>(() =>
            JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(invalidJson));
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CorrelationId: "corr-001",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            EventTime: new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                SensorId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                SensorName: "Sensor T-01",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 28.4,
                Latitude: 39.75,
                Longitude: -7.92,
                OperationalState: SensorOperationalState.Nominal));
    }
}
