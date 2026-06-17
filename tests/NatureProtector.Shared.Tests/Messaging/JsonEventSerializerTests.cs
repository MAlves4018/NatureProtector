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
    public void Deserialize_CurrentSchemaFixture_MapsAllContractFields()
    {
        var envelope = DeserializeFixture(CurrentSchemaFixtureJson);

        Assert.Equal("1.0", envelope.SchemaVersion);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), envelope.EventId);
        Assert.Equal("corr-current", envelope.CorrelationId);
        Assert.Equal("NatureProtector.Simulator.Host", envelope.Producer);
        Assert.Equal(EventTypes.SensorReadingProduced, envelope.EventType);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), envelope.AreaId);
        Assert.Equal(new DateTimeOffset(2026, 4, 6, 10, 30, 0, TimeSpan.Zero), envelope.EventTime);
        Assert.Equal(new DateTimeOffset(2026, 4, 6, 10, 31, 2, TimeSpan.Zero), envelope.IngestTime);
        Assert.Equal(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), envelope.Payload.SimulationRunId);
        Assert.Equal(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), envelope.Payload.SensorId);
        Assert.Equal("Sensor T-01", envelope.Payload.SensorName);
        Assert.Equal(SensorMetricType.Temperature, envelope.Payload.MetricType);
        Assert.Equal(MeasurementUnit.Celsius, envelope.Payload.Unit);
        Assert.Equal(28.4, envelope.Payload.Value);
        Assert.Equal(39.75, envelope.Payload.Latitude);
        Assert.Equal(-7.92, envelope.Payload.Longitude);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
    }

    [Fact]
    public void Deserialize_PreviousSupportedV1FixtureWithoutOptionalIngestTime_MapsForBackwardCompatibility()
    {
        var envelope = DeserializeFixture(PreviousSupportedV1FixtureJson);

        Assert.Equal("1.0", envelope.SchemaVersion);
        Assert.Equal("corr-previous-v1", envelope.CorrelationId);
        Assert.Null(envelope.IngestTime);
        Assert.Equal(SensorMetricType.Humidity, envelope.Payload.MetricType);
        Assert.Equal(MeasurementUnit.Percent, envelope.Payload.Unit);
        Assert.Equal(SensorOperationalState.Delayed, envelope.Payload.OperationalState);
    }

    [Fact]
    public void Deserialize_Throws_WhenJsonIsInvalid()
    {
        var invalidJson = Encoding.UTF8.GetBytes("{ invalid");

        Assert.Throws<JsonException>(() =>
            JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(invalidJson));
    }

    [Fact]
    public void Deserialize_IgnoresUnknownFields_ForForwardCompatibleAdditions()
    {
        var json = """
                   {
                     "schemaVersion": "1.0",
                     "eventId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                     "correlationId": "corr-forward",
                     "producer": "NatureProtector.Simulator.Host",
                     "eventType": "SensorReadingProduced",
                     "areaId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                     "eventTime": "2026-04-06T10:30:00+00:00",
                     "producerBuild": "future-field",
                     "payload": {
                       "simulationRunId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
                       "sensorId": "dddddddd-dddd-dddd-dddd-dddddddddddd",
                       "sensorName": "Sensor T-01",
                       "metricType": "Temperature",
                       "unit": "Celsius",
                       "value": 28.4,
                       "latitude": 39.75,
                       "longitude": -7.92,
                       "operationalState": "Nominal",
                       "futurePayloadField": "ignored"
                     }
                   }
                   """;

        var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            Encoding.UTF8.GetBytes(json));

        Assert.NotNull(envelope);
        Assert.Equal("corr-forward", envelope.CorrelationId);
        Assert.Equal(SensorMetricType.Temperature, envelope.Payload.MetricType);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
    }

    [Fact]
    public void Deserialize_Throws_WhenStringEnumIsUnknown()
    {
        var json = JsonEventSerializer.SerializeToString(CreateEnvelope())
            .Replace("\"Temperature\"", "\"UnknownFutureMetric\"", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() =>
            JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
                Encoding.UTF8.GetBytes(json)));
    }

    private static EventEnvelope<SensorReadingProducedPayload> DeserializeFixture(string json)
    {
        var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            Encoding.UTF8.GetBytes(json));

        return Assert.IsType<EventEnvelope<SensorReadingProducedPayload>>(envelope);
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

    private const string CurrentSchemaFixtureJson = """
                                                    {
                                                      "schemaVersion": "1.0",
                                                      "eventId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                                                      "correlationId": "corr-current",
                                                      "producer": "NatureProtector.Simulator.Host",
                                                      "eventType": "SensorReadingProduced",
                                                      "areaId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                                                      "eventTime": "2026-04-06T10:30:00+00:00",
                                                      "ingestTime": "2026-04-06T10:31:02+00:00",
                                                      "payload": {
                                                        "simulationRunId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
                                                        "sensorId": "dddddddd-dddd-dddd-dddd-dddddddddddd",
                                                        "sensorName": "Sensor T-01",
                                                        "metricType": "Temperature",
                                                        "unit": "Celsius",
                                                        "value": 28.4,
                                                        "latitude": 39.75,
                                                        "longitude": -7.92,
                                                        "operationalState": "Nominal"
                                                      }
                                                    }
                                                    """;

    // V1 has no supported pre-1.0 transport schema; this fixture protects the older v1 shape without optional ingestTime.
    private const string PreviousSupportedV1FixtureJson = """
                                                         {
                                                           "schemaVersion": "1.0",
                                                           "eventId": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
                                                           "correlationId": "corr-previous-v1",
                                                           "producer": "NatureProtector.Simulator.Host",
                                                           "eventType": "SensorReadingProduced",
                                                           "areaId": "ffffffff-ffff-ffff-ffff-ffffffffffff",
                                                           "eventTime": "2026-04-06T11:00:00+00:00",
                                                           "payload": {
                                                             "simulationRunId": "11111111-1111-1111-1111-111111111111",
                                                             "sensorId": "22222222-2222-2222-2222-222222222222",
                                                             "sensorName": "Sensor H-02",
                                                             "metricType": "Humidity",
                                                             "unit": "Percent",
                                                             "value": 63.2,
                                                             "latitude": 39.76,
                                                             "longitude": -7.93,
                                                             "operationalState": "Delayed"
                                                           }
                                                         }
                                                         """;
}
