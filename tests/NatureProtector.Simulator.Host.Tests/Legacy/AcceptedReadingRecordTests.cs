using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class AcceptedReadingRecordTests
{
    [Fact]
    public void FromEnvelope_Throws_WhenEnvelopeIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => AcceptedReadingRecord.FromEnvelope(null!));

        Assert.Equal("envelope", ex.ParamName);
    }

    [Fact]
    public void FromEnvelope_FlattensEnvelopeFields_IntoRecord()
    {
        var acceptedBefore = DateTimeOffset.UtcNow;
        var envelope = new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-001",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 6, 20, 15, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor-01",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 30.2,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));

        var record = AcceptedReadingRecord.FromEnvelope(envelope);
        var acceptedAfter = DateTimeOffset.UtcNow;

        Assert.Equal(envelope.EventId, record.EventId);
        Assert.Equal(envelope.CorrelationId, record.CorrelationId);
        Assert.Equal(envelope.Producer, record.Producer);
        Assert.Equal(envelope.EventType, record.EventType);
        Assert.Equal(envelope.AreaId, record.AreaId);
        Assert.Equal(envelope.EventTime, record.EventTime);
        Assert.Equal(envelope.Payload.SensorId, record.SensorId);
        Assert.Equal(envelope.Payload.SensorName, record.SensorName);
        Assert.Equal(envelope.Payload.MetricType, record.MetricType);
        Assert.Equal(envelope.Payload.Unit, record.Unit);
        Assert.Equal(envelope.Payload.Value, record.Value);
        Assert.Equal(envelope.Payload.Latitude, record.Latitude);
        Assert.Equal(envelope.Payload.Longitude, record.Longitude);
        Assert.Equal(envelope.Payload.OperationalState, record.OperationalState);
        Assert.InRange(record.AcceptedAt, acceptedBefore, acceptedAfter);
    }
}
