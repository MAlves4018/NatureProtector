using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Tests.Readings;

public sealed class OperationalEventTests
{
    [Fact]
    public void FromEnvelope_MapsTransportAndPayloadFields()
    {
        var envelope = CreateEnvelope();

        var operationalEvent = OperationalEvent.FromEnvelope(envelope);

        Assert.Equal(envelope.SchemaVersion, operationalEvent.SchemaVersion);
        Assert.Equal(envelope.EventId, operationalEvent.EventId);
        Assert.Equal(envelope.CorrelationId, operationalEvent.CorrelationId);
        Assert.Equal(envelope.Producer, operationalEvent.Producer);
        Assert.Equal(envelope.EventType, operationalEvent.EventType);
        Assert.Equal(envelope.AreaId, operationalEvent.AreaId);
        Assert.Equal(envelope.Payload.SimulationRunId, operationalEvent.SimulationRunId);
        Assert.Equal(envelope.Payload.SensorId, operationalEvent.SensorId);
        Assert.Equal(envelope.Payload.SensorName, operationalEvent.SensorName);
        Assert.Equal(envelope.Payload.MetricType, operationalEvent.MetricType);
        Assert.Equal(envelope.Payload.Value, operationalEvent.Value);
        Assert.Equal(envelope.Payload.Unit, operationalEvent.Unit);
        Assert.Equal(envelope.Payload.Latitude, operationalEvent.Latitude);
        Assert.Equal(envelope.Payload.Longitude, operationalEvent.Longitude);
        Assert.Equal(envelope.Payload.OperationalState, operationalEvent.OperationalState);
        Assert.Equal(envelope.EventTime, operationalEvent.EventTime);
        Assert.Equal(envelope.IngestTime, operationalEvent.IngestTime);
        Assert.Empty(operationalEvent.QualityFlags);
        Assert.Empty(operationalEvent.ClassifierResults);
    }

    [Fact]
    public void FromEnvelope_AllowsAdditiveQualityFlagsAndClassifierResults()
    {
        var envelope = CreateEnvelope();
        var classifierResult = ClassifierResult.Create(
            classifierName: "semantic_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Medium,
            qualityFlags: ["SemanticMismatch"],
            reasons: ["sensor_name_mismatch"],
            evaluatedAt: new DateTimeOffset(2026, 5, 12, 11, 15, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");

        var operationalEvent = OperationalEvent.FromEnvelope(
            envelope,
            qualityFlags: ["SemanticMismatch"],
            classifierResults: [classifierResult]);

        Assert.Equal(["SemanticMismatch"], operationalEvent.QualityFlags);
        var carried = Assert.Single(operationalEvent.ClassifierResults);
        Assert.Equal(classifierResult.ClassifierName, carried.ClassifierName);
        Assert.Equal(classifierResult.Status, carried.Status);
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
            EventTime: new DateTimeOffset(2026, 4, 30, 11, 0, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 30, 11, 0, 4, TimeSpan.Zero),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: simulationRunId,
                SensorId: sensorId,
                SensorName: "Sensor-PT-05",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 29.8,
                Latitude: 39.79,
                Longitude: -7.87,
                OperationalState: SensorOperationalState.Nominal));
    }
}
