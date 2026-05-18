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
    public void Constructor_Throws_WhenDayIsDefault()
    {
        var ex = Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: default,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal("day", ex.ParamName);
        Assert.Contains("Day must be a valid, non-default value.", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenLastUpdatedAtIsDefault()
    {
        var ex = Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero),
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: default));

        Assert.Equal("lastUpdatedAt", ex.ParamName);
        Assert.Contains("LastUpdatedAt must be a valid, non-default value.", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenLastSourceEventIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero),
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero),
            lastSourceEventId: Guid.Empty));

        Assert.Equal("lastSourceEventId", ex.ParamName);
        Assert.Contains("LastSourceEventId must not be an empty GUID.", ex.Message);
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

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_Throws_WhenPrecipitationIsNotFinite(double precipitation)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero),
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero),
            dailyPrecipitationMillimeters: precipitation));

        Assert.Equal("dailyPrecipitationMillimeters", ex.ParamName);
        Assert.Contains("finite number", ex.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_Throws_WhenMaxTemperatureIsNotFinite(double temperature)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero),
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "pipeline",
            lastUpdatedAt: new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero),
            maxTemperatureCelsius: temperature));

        Assert.Equal("maxTemperatureCelsius", ex.ParamName);
        Assert.Contains("finite number", ex.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromRiskInput_Throws_WhenTemperatureValueIsNotFinite(double temperature)
    {
        var input = new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: temperature,
            Unit: MeasurementUnit.Celsius,
            EventTime: new DateTimeOffset(2026, 5, 12, 14, 15, 0, TimeSpan.Zero));

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => DailyCellState.FromRiskInput(
            input,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1"));

        Assert.Equal("value", ex.ParamName);
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
    public void ApplyRiskInput_AllowsDifferentSensor_WhenAreaGridCellRunAndDayMatch()
    {
        var areaId = Guid.NewGuid();
        var humiditySensorId = Guid.NewGuid();
        var temperatureSensorId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var configurationVersionId = Guid.NewGuid();

        var humidityInput = CreateRiskInput(
            areaId: areaId,
            sensorId: humiditySensorId,
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Humidity,
            value: 21.5,
            unit: MeasurementUnit.Percent,
            eventTime: new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var state = DailyCellState.FromRiskInput(
            humidityInput,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1");

        var temperatureInput = CreateRiskInput(
            areaId: areaId,
            sensorId: temperatureSensorId,
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            value: 32.0,
            unit: MeasurementUnit.Celsius,
            eventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var updated = state.ApplyRiskInput(temperatureInput);

        Assert.Equal(areaId, updated.AreaId);
        Assert.Equal(gridCellId, updated.GridCellId);
        Assert.Equal(simulationRunId, updated.SimulationRunId);
        Assert.Equal(configurationVersionId, updated.ConfigurationVersionId);
        Assert.Equal(temperatureSensorId, updated.SensorId);
        Assert.Equal(temperatureInput.SourceEventId, updated.LastSourceEventId);
        Assert.Equal(temperatureInput.EventTime, updated.LastUpdatedAt);
        Assert.Equal(21.5, updated.LatestHumidityPercent);
        Assert.Equal(32.0, updated.MaxTemperatureCelsius);
    }

    [Fact]
    public void ApplyRiskInput_AccumulatesHumidityTemperatureAndWindSpeed()
    {
        var areaId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var configurationVersionId = Guid.NewGuid();

        var humidityInput = CreateRiskInput(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Humidity,
            value: 22.5,
            unit: MeasurementUnit.Percent,
            eventTime: new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var state = DailyCellState.FromRiskInput(
            humidityInput,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1");

        var temperatureInput = CreateRiskInput(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            value: 31.8,
            unit: MeasurementUnit.Celsius,
            eventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var windInput = CreateRiskInput(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.WindSpeed,
            value: 5.4,
            unit: MeasurementUnit.MetersPerSecond,
            eventTime: new DateTimeOffset(2026, 5, 12, 10, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var afterTemperature = state.ApplyRiskInput(temperatureInput);
        var afterWind = afterTemperature.ApplyRiskInput(windInput);

        Assert.Equal(22.5, afterWind.LatestHumidityPercent);
        Assert.Equal(31.8, afterWind.MaxTemperatureCelsius);
        Assert.Equal(5.4, afterWind.LatestWindSpeedMetersPerSecond);
        Assert.Equal(windInput.SourceEventId, afterWind.LastSourceEventId);
        Assert.Equal(windInput.EventTime, afterWind.LastUpdatedAt);
        Assert.Equal(windInput.SensorId, afterWind.SensorId);
    }

    [Fact]
    public void ApplyRiskInput_Throws_WhenAreaGridCellRunOrDayDoNotMatch()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var configurationVersionId = Guid.NewGuid();

        var initialInput = CreateRiskInput(
            areaId: areaId,
            sensorId: sensorId,
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Humidity,
            value: 20.0,
            unit: MeasurementUnit.Percent,
            eventTime: new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var state = DailyCellState.FromRiskInput(
            initialInput,
            antecedentState: "baseline",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "risk_input_v1");

        var mismatchArea = CreateRiskInput(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            value: 21.0,
            unit: MeasurementUnit.Celsius,
            eventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var mismatchGridCell = CreateRiskInput(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            value: 21.0,
            unit: MeasurementUnit.Celsius,
            eventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero),
            gridCellId: Guid.NewGuid(),
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        var mismatchRun = CreateRiskInput(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            value: 21.0,
            unit: MeasurementUnit.Celsius,
            eventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: Guid.NewGuid(),
            configurationVersionId: configurationVersionId);

        var mismatchDay = CreateRiskInput(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            value: 21.0,
            unit: MeasurementUnit.Celsius,
            eventTime: new DateTimeOffset(2026, 5, 13, 0, 1, 0, TimeSpan.Zero),
            gridCellId: gridCellId,
            simulationRunId: simulationRunId,
            configurationVersionId: configurationVersionId);

        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchArea));
        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchGridCell));
        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchRun));
        Assert.Throws<InvalidOperationException>(() => state.ApplyRiskInput(mismatchDay));
    }

    private static RiskInput CreateRiskInput(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        SensorMetricType metricType,
        double value,
        MeasurementUnit unit,
        DateTimeOffset eventTime,
        Guid? gridCellId = null,
        Guid? simulationRunId = null,
        Guid? configurationVersionId = null)
    {
        return new RiskInput(
            AreaId: areaId,
            SensorId: sensorId,
            SourceEventId: sourceEventId,
            MetricType: metricType,
            Value: value,
            Unit: unit,
            EventTime: eventTime)
        {
            GridCellId = gridCellId,
            SimulationRunId = simulationRunId,
            ConfigurationVersionId = configurationVersionId
        };
    }
}