using NatureProtector.Prevention.Host.Validation;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class SimpleReadingValidatorTests
{
    private readonly SimpleReadingValidator _validator = new();

    [Fact]
    public void Validate_AcceptsNominalCompatibleEnvelope()
    {
        var result = _validator.Validate(CreateEnvelope());

        Assert.True(result.IsAccepted);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsNullEnvelope()
    {
        var result = _validator.Validate(null);

        Assert.False(result.IsAccepted);
        Assert.Equal("Envelope is null.", result.RejectionReason);
    }

    [Theory]
    [InlineData("", "CorrelationId must not be empty.")]
    [InlineData("   ", "CorrelationId must not be empty.")]
    public void Validate_RejectsBlankCorrelationId(string correlationId, string expectedReason)
    {
        var result = _validator.Validate(CreateEnvelope(correlationId: correlationId));

        Assert.False(result.IsAccepted);
        Assert.Equal(expectedReason, result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsEmptyEventId()
    {
        var result = _validator.Validate(CreateEnvelope(eventId: Guid.Empty));

        Assert.False(result.IsAccepted);
        Assert.Equal("EventId must not be empty.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsEmptyAreaId()
    {
        var result = _validator.Validate(CreateEnvelope(areaId: Guid.Empty));

        Assert.False(result.IsAccepted);
        Assert.Equal("AreaId must not be empty.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsUnsupportedEventType()
    {
        var result = _validator.Validate(CreateEnvelope(eventType: "OtherEvent"));

        Assert.False(result.IsAccepted);
        Assert.Equal("Unsupported event type 'OtherEvent'.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsNullPayload()
    {
        var envelope = CreateEnvelope() with { Payload = null! };

        var result = _validator.Validate(envelope);

        Assert.False(result.IsAccepted);
        Assert.Equal("Payload must not be null.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsEmptySimulationRunId()
    {
        var result = _validator.Validate(CreateEnvelope(simulationRunId: Guid.Empty));

        Assert.False(result.IsAccepted);
        Assert.Equal("SimulationRunId must not be empty.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsEmptySensorId()
    {
        var result = _validator.Validate(CreateEnvelope(sensorId: Guid.Empty));

        Assert.False(result.IsAccepted);
        Assert.Equal("SensorId must not be empty.", result.RejectionReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsBlankSensorName(string sensorName)
    {
        var result = _validator.Validate(CreateEnvelope(sensorName: sensorName));

        Assert.False(result.IsAccepted);
        Assert.Equal("SensorName must not be empty.", result.RejectionReason);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_RejectsNonFiniteValue(double value)
    {
        var result = _validator.Validate(CreateEnvelope(value: value));

        Assert.False(result.IsAccepted);
        Assert.Equal("Value must be a finite number.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsNonNominalOperationalState()
    {
        var result = _validator.Validate(CreateEnvelope(operationalState: SensorOperationalState.Invalid));

        Assert.False(result.IsAccepted);
        Assert.Equal("Operational state 'Invalid' is not accepted.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsOutOfRangeLongitude()
    {
        var result = _validator.Validate(CreateEnvelope(longitude: 190.0));

        Assert.False(result.IsAccepted);
        Assert.Equal("Longitude '190' is outside the valid range.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsMismatchedMetricAndUnit()
    {
        var result = _validator.Validate(CreateEnvelope(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Percent));

        Assert.False(result.IsAccepted);
        Assert.Equal("Metric 'Temperature' is incompatible with unit 'Percent'.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsOutOfRangeValue()
    {
        var result = _validator.Validate(CreateEnvelope(
            metricType: SensorMetricType.Humidity,
            unit: MeasurementUnit.Percent,
            value: 120.0));

        Assert.False(result.IsAccepted);
        Assert.Equal("Value '120' is outside the accepted range for metric 'Humidity'.", result.RejectionReason);
    }

    [Fact]
    public void Validate_RejectsOutOfRangeLatitude()
    {
        var result = _validator.Validate(CreateEnvelope(latitude: 95.0));

        Assert.False(result.IsAccepted);
        Assert.Equal("Latitude '95' is outside the valid range.", result.RejectionReason);
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(
        Guid? eventId = null,
        string correlationId = "corr-001",
        string eventType = EventTypes.SensorReadingProduced,
        Guid? areaId = null,
        Guid? simulationRunId = null,
        Guid? sensorId = null,
        string sensorName = "Sensor-01",
        SensorMetricType metricType = SensorMetricType.Temperature,
        MeasurementUnit unit = MeasurementUnit.Celsius,
        double value = 28.0,
        double latitude = 39.8,
        double longitude = -7.9,
        SensorOperationalState operationalState = SensorOperationalState.Nominal)
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: eventId ?? Guid.NewGuid(),
            CorrelationId: correlationId,
            Producer: "NatureProtector.Simulator.Host",
            EventType: eventType,
            AreaId: areaId ?? Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 6, 20, 0, 0, TimeSpan.Zero),
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
                OperationalState: operationalState));
    }
}
