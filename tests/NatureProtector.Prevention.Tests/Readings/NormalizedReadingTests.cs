using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Tests.Readings;

public sealed class NormalizedReadingTests
{
    [Fact]
    public void FromEnvelope_PreservesAcceptedReadingFields()
    {
        var simulationRunId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var eventTime = new DateTimeOffset(2026, 4, 30, 10, 15, 0, TimeSpan.Zero);
        var ingestTime = new DateTimeOffset(2026, 4, 30, 10, 15, 5, TimeSpan.Zero);
        var envelope = new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: eventId,
            CorrelationId: $"{simulationRunId:N}-{sensorId:N}",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: areaId,
            EventTime: eventTime,
            IngestTime: ingestTime,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: simulationRunId,
                SensorId: sensorId,
                SensorName: "Sensor-PT-01",
                MetricType: SensorMetricType.Humidity,
                Unit: MeasurementUnit.Percent,
                Value: 37.5,
                Latitude: 39.73,
                Longitude: -7.91,
                OperationalState: SensorOperationalState.Nominal));

        var normalized = NormalizedReading.FromEnvelope(envelope);

        Assert.Equal(envelope.EventId, normalized.EventId);
        Assert.Equal(envelope.CorrelationId, normalized.CorrelationId);
        Assert.Equal(envelope.AreaId, normalized.AreaId);
        Assert.Equal(envelope.Payload.SensorId, normalized.SensorId);
        Assert.Equal(envelope.Payload.SensorName, normalized.SensorName);
        Assert.Equal(envelope.Payload.MetricType, normalized.MetricType);
        Assert.Equal(envelope.Payload.Value, normalized.Value);
        Assert.Equal(envelope.Payload.Unit, normalized.Unit);
        Assert.Equal(envelope.Payload.Latitude, normalized.Latitude);
        Assert.Equal(envelope.Payload.Longitude, normalized.Longitude);
        Assert.Equal(envelope.Payload.OperationalState, normalized.OperationalState);
        Assert.Equal(envelope.EventTime, normalized.EventTime);
        Assert.Equal(envelope.IngestTime, normalized.IngestTime);
        Assert.Empty(normalized.QualityFlags);
        Assert.Empty(normalized.ClassifierResults);
    }

    [Fact]
    public void FromOperationalEvent_PreservesFlagsAndClassifierResults()
    {
        var classifierResult = ClassifierResult.Create(
            classifierName: "temporal_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Medium,
            qualityFlags: ["Delayed"],
            reasons: ["late_arrival"],
            evaluatedAt: new DateTimeOffset(2026, 5, 12, 11, 0, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");
        var envelope = CreateEnvelope();
        var operationalEvent = OperationalEvent.FromEnvelope(
            envelope,
            qualityFlags: ["Delayed", "OutOfOrder"],
            classifierResults: [classifierResult]);

        var normalized = NormalizedReading.FromOperationalEvent(operationalEvent);

        Assert.Equal(operationalEvent.EventId, normalized.EventId);
        Assert.Equal(operationalEvent.CorrelationId, normalized.CorrelationId);
        Assert.Equal(["Delayed", "OutOfOrder"], normalized.QualityFlags);
        var carried = Assert.Single(normalized.ClassifierResults);
        Assert.Equal(classifierResult.ClassifierName, carried.ClassifierName);
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        var simulationRunId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: $"{simulationRunId:N}-{sensorId:N}",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 30, 10, 15, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 30, 10, 15, 5, TimeSpan.Zero),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: simulationRunId,
                SensorId: sensorId,
                SensorName: "Sensor-PT-01",
                MetricType: SensorMetricType.Humidity,
                Unit: MeasurementUnit.Percent,
                Value: 37.5,
                Latitude: 39.73,
                Longitude: -7.91,
                OperationalState: SensorOperationalState.Nominal));
    }
}
