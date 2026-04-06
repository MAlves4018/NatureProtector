using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class ReadingGenerationServiceTests
{
    private readonly ReadingGenerationService _service = new();

    [Fact]
    public void GenerateBatch_Throws_WhenContextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _service.GenerateBatch(
            context: null!,
            simulationRunId: Guid.NewGuid(),
            cycleIndex: 0,
            eventTime: DateTimeOffset.UtcNow,
            random: new Random(1)));

        Assert.Equal("context", ex.ParamName);
    }

    [Fact]
    public void GenerateBatch_Throws_WhenRandomIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _service.GenerateBatch(
            context: CreateContext(sensors: [CreateSensor(SensorType.Temperature, "Sensor-01")]),
            simulationRunId: Guid.NewGuid(),
            cycleIndex: 0,
            eventTime: DateTimeOffset.UtcNow,
            random: null!));

        Assert.Equal("random", ex.ParamName);
    }

    [Fact]
    public void GenerateBatch_ReturnsOneEnvelopePerSensor()
    {
        var context = CreateContext(
            sensors:
            [
                CreateSensor(SensorType.Temperature, "Sensor-T"),
                CreateSensor(SensorType.Humidity, "Sensor-H"),
                CreateSensor(SensorType.Wind, "Sensor-W")
            ]);
        var runId = Guid.NewGuid();

        var envelopes = _service.GenerateBatch(
            context,
            simulationRunId: runId,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(2026));

        Assert.Equal(3, envelopes.Count);
        Assert.All(envelopes, envelope =>
        {
            Assert.Equal(EventTypes.SensorReadingProduced, envelope.EventType);
            Assert.Equal(runId, envelope.Payload.SimulationRunId);
            Assert.Equal(context.AreaId, envelope.AreaId);
            Assert.Equal(context.StartTimestamp, envelope.EventTime);
        });
    }

    [Theory]
    [InlineData(SensorType.Temperature, SensorMetricType.Temperature, MeasurementUnit.Celsius)]
    [InlineData(SensorType.Humidity, SensorMetricType.Humidity, MeasurementUnit.Percent)]
    [InlineData(SensorType.Wind, SensorMetricType.WindSpeed, MeasurementUnit.MetersPerSecond)]
    public void GenerateReading_MapsSensorTypeToMetricAndUnit(
        SensorType sensorType,
        SensorMetricType expectedMetricType,
        MeasurementUnit expectedUnit)
    {
        var sensor = CreateSensor(sensorType, "Sensor-01");
        var context = CreateContext(sensors: [sensor]);
        var runId = Guid.NewGuid();

        var envelope = _service.GenerateReading(
            context,
            simulationRunId: runId,
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10));

        Assert.Equal(expectedMetricType, envelope.Payload.MetricType);
        Assert.Equal(expectedUnit, envelope.Payload.Unit);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
        Assert.Equal(runId, envelope.Payload.SimulationRunId);
        Assert.StartsWith(runId.ToString("N"), envelope.CorrelationId);
    }

    [Fact]
    public void GenerateReading_ReturnsInvalidReading_WhenFailureRateForcesUnavailable()
    {
        var sensor = CreateSensor(SensorType.Temperature, "Sensor-01");
        var context = CreateContext(sensors: [sensor], failureRate: 1.0);

        var envelope = _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10));

        Assert.Equal(0.0, envelope.Payload.Value);
        Assert.Equal(SensorOperationalState.Invalid, envelope.Payload.OperationalState);
    }

    [Fact]
    public void GenerateReading_Throws_WhenSensorIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _service.GenerateReading(
            context: CreateContext(sensors: [CreateSensor(SensorType.Temperature, "Sensor-01")]),
            simulationRunId: Guid.NewGuid(),
            sensor: null!,
            cycleIndex: 0,
            eventTime: DateTimeOffset.UtcNow,
            random: new Random(1)));

        Assert.Equal("sensor", ex.ParamName);
    }

    [Fact]
    public void GenerateReading_ReturnsInvalidReading_WhenSensorIsInactive()
    {
        var sensor = CreateSensor(SensorType.Temperature, "Inactive-01", isActive: false);
        var context = CreateContext(sensors: [sensor], failureRate: 0.0);

        var envelope = _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10));

        Assert.Equal(0.0, envelope.Payload.Value);
        Assert.Equal(SensorOperationalState.Invalid, envelope.Payload.OperationalState);
    }

    [Fact]
    public void GenerateReading_Throws_WhenScenarioBaselineValueIsMissing()
    {
        var sensor = CreateSensor(SensorType.Temperature, "Sensor-01");
        var context = CreateContext(
            sensors: [sensor],
            baseTemperature: null,
            baseHumidity: 30.0,
            baseWindSpeed: 7.0);

        var ex = Assert.Throws<InvalidOperationException>(() => _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10)));

        Assert.Contains("Scenario parameter 'BaseTemperature' must have a value", ex.Message);
    }

    [Fact]
    public void GenerateReading_Throws_WhenHumidityBaselineIsMissing()
    {
        var sensor = CreateSensor(SensorType.Humidity, "Sensor-H");
        var context = CreateContext(
            sensors: [sensor],
            baseTemperature: 30.0,
            baseHumidity: null,
            baseWindSpeed: 7.0);

        var ex = Assert.Throws<InvalidOperationException>(() => _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10)));

        Assert.Contains("Scenario parameter 'BaseHumidity' must have a value", ex.Message);
    }

    [Fact]
    public void GenerateReading_Throws_WhenWindBaselineIsMissing()
    {
        var sensor = CreateSensor(SensorType.Wind, "Sensor-W");
        var context = CreateContext(
            sensors: [sensor],
            baseTemperature: 30.0,
            baseHumidity: 30.0,
            baseWindSpeed: null);

        var ex = Assert.Throws<InvalidOperationException>(() => _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10)));

        Assert.Contains("Scenario parameter 'BaseWindSpeed' must have a value", ex.Message);
    }

    [Fact]
    public void GenerateReading_Throws_WhenCompositeSensorIsUsed()
    {
        var sensor = CreateSensor(SensorType.Composite, "Sensor-C");
        var context = CreateContext(sensors: [sensor]);

        var ex = Assert.Throws<InvalidOperationException>(() => _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10)));

        Assert.Contains("Composite sensors", ex.Message);
    }

    [Fact]
    public void ResolveMetricType_Throws_WhenSensorTypeIsUnknown()
    {
        var method = typeof(ReadingGenerationService).GetMethod(
            "ResolveMetricType",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveMetricType method was not found.");

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            method.Invoke(null, [(SensorType)999]));

        Assert.Contains("cannot be mapped to a metric type", ex.InnerException!.Message);
    }

    [Fact]
    public void ResolveMeasurementUnit_Throws_WhenSensorTypeIsUnknown()
    {
        var method = typeof(ReadingGenerationService).GetMethod(
            "ResolveMeasurementUnit",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveMeasurementUnit method was not found.");

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            method.Invoke(null, [(SensorType)999]));

        Assert.Contains("cannot be mapped to a measurement unit", ex.InnerException!.Message);
    }

    [Fact]
    public void GenerateBatch_ProducesDeterministicMetricValues_ForSameSeed()
    {
        var sensor = CreateSensor(SensorType.Wind, "Sensor-W");
        var context = CreateContext(sensors: [sensor]);
        var runId = Guid.NewGuid();
        var eventTime = context.StartTimestamp.AddMinutes(2);

        var first = _service.GenerateBatch(
            context,
            simulationRunId: runId,
            cycleIndex: 2,
            eventTime: eventTime,
            random: new Random(1234));

        var second = _service.GenerateBatch(
            context,
            simulationRunId: runId,
            cycleIndex: 2,
            eventTime: eventTime,
            random: new Random(1234));

        Assert.Equal(
            first.Select(x => x.Payload.Value).ToArray(),
            second.Select(x => x.Payload.Value).ToArray());
        Assert.Equal(
            first.Select(x => x.Payload.OperationalState).ToArray(),
            second.Select(x => x.Payload.OperationalState).ToArray());
        Assert.Equal(
            first.Select(x => x.CorrelationId).ToArray(),
            second.Select(x => x.CorrelationId).ToArray());
    }

    private static SimulationContext CreateContext(
        IReadOnlyCollection<Sensor> sensors,
        double? baseTemperature = 31.0,
        double? baseHumidity = 33.0,
        double? baseWindSpeed = 7.5,
        double failureRate = 0.05,
        double noiseLevel = 0.10)
    {
        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario",
            category: ScenarioCategory.HighRisk,
            parameters: new ScenarioParameters(
                baseTemperature: baseTemperature,
                baseHumidity: baseHumidity,
                baseWindSpeed: baseWindSpeed,
                failureRate: failureRate,
                noiseLevel: noiseLevel,
                timeAcceleration: 1.0));

        return new SimulationContext(
            areaId: Guid.NewGuid(),
            scenario: scenario,
            sensors: sensors,
            startTimestamp: new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero),
            interval: TimeSpan.FromSeconds(1),
            numberOfCycles: 3);
    }

    private static Sensor CreateSensor(SensorType sensorType, string name, bool isActive = true)
    {
        return new Sensor(
            id: Guid.NewGuid(),
            name: name,
            type: sensorType,
            location: new Location(39.8, -7.9),
            profile: new SensorProfile(
                id: Guid.NewGuid(),
                samplingInterval: TimeSpan.FromSeconds(5),
                communicationMode: "LoRa",
                noiseLevel: 0.10,
                latencyProfile: "Low latency",
                failureProfile: "Rare failures"),
            isActive: isActive);
    }
}
