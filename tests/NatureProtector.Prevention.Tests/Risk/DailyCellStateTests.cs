using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class DailyCellStateTests
{
    [Fact]
    public void Constructor_Throws_WhenRequiredFieldsAreInvalid()
    {
        var eventTime = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.Empty,
            sensorId: Guid.NewGuid(),
            day: eventTime,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: eventTime));

        Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.Empty,
            day: eventTime,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: eventTime));

        Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: eventTime,
            antecedentState: " ",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: eventTime));

        Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: eventTime,
            antecedentState: "baseline",
            candidateParameterSetVersion: " ",
            provenance: "pipeline",
            lastUpdatedAt: eventTime));

        Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: eventTime,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: " ",
            lastUpdatedAt: eventTime));
    }

    [Fact]
    public void Constructor_Throws_WhenPrecipitationIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero),
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero),
            dailyPrecipitationMillimeters: -0.1));

        Assert.Equal("dailyPrecipitationMillimeters", ex.ParamName);
    }

    [Fact]
    public void FromRiskInput_MapsCoreFields_AndTemperatureWhenAvailable()
    {
        var input = new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 32.5,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 12, 14, 15, 0, TimeSpan.Zero));

        var state = DailyCellState.FromRiskInput(
            input,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1",
            dailyPrecipitationMillimeters: 1.4);

        Assert.Equal(input.AreaId, state.AreaId);
        Assert.Equal(input.SensorId, state.SensorId);
        Assert.Equal(new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero), state.Day);
        Assert.Equal(1.4, state.DailyPrecipitationMillimeters);
        Assert.Equal(32.5, state.MaxTemperatureCelsius);
        Assert.Equal(input.SourceEventId, state.LastSourceEventId);
        Assert.Equal(input.EventTime, state.LastUpdatedAt);
    }

    [Fact]
    public void FromNormalizedReading_MapsCoreFields_AndTemperatureWhenAvailable()
    {
        var reading = new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-daily-01",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-10",
            MetricType: SensorMetricType.Temperature,
            Value: 27.3,
            Unit: MeasurementUnit.Celsius,
            Latitude: 39.70,
            Longitude: -7.80,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: new DateTimeOffset(2026, 5, 12, 10, 20, 0, TimeSpan.Zero),
            IngestTime: null);

        var state = DailyCellState.FromNormalizedReading(
            reading,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "normalized_reading");

        Assert.Equal(reading.AreaId, state.AreaId);
        Assert.Equal(reading.SensorId, state.SensorId);
        Assert.Equal(new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero), state.Day);
        Assert.Equal(27.3, state.MaxTemperatureCelsius);
        Assert.Equal(reading.EventId, state.LastSourceEventId);
        Assert.Equal(reading.EventTime, state.LastUpdatedAt);
    }

    [Fact]
    public void ApplyRiskInput_UpdatesDailyMaxTemperature_WhenInputIsHotter()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var firstInput = new RiskInput(
            AreaId: areaId,
            SensorId: sensorId,
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 25.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero));
        var secondInput = new RiskInput(
            AreaId: areaId,
            SensorId: sensorId,
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 33.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 12, 16, 0, 0, TimeSpan.Zero));
        var state = DailyCellState.FromRiskInput(
            firstInput,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1");

        var updated = state.ApplyRiskInput(secondInput);

        Assert.Equal(33.0, updated.MaxTemperatureCelsius);
        Assert.Equal(secondInput.SourceEventId, updated.LastSourceEventId);
        Assert.Equal(secondInput.EventTime, updated.LastUpdatedAt);
        Assert.Equal(state.DailyPrecipitationMillimeters, updated.DailyPrecipitationMillimeters);
    }

    [Fact]
    public void ApplyRiskInput_Throws_WhenAreaOrSensorOrDayDoNotMatch()
    {
        var state = DailyCellState.FromRiskInput(
            new RiskInput(
                AreaId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SourceEventId: Guid.NewGuid(),
                MetricType: SensorMetricType.Temperature,
                Value: 20.0,
                Unit: MeasurementUnit.Celsius,
                EventTime: new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero)),
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1");

        var mismatchArea = new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: state.SensorId,
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 21.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero));
        var mismatchSensor = new RiskInput(
            AreaId: state.AreaId,
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 21.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero));
        var mismatchDay = new RiskInput(
            AreaId: state.AreaId,
            SensorId: state.SensorId,
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 21.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 13, 0, 1, 0, TimeSpan.Zero));

        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchArea));
        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchSensor));
        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchDay));
    }
}
