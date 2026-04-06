using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class SimulationContextTests
{
    [Fact]
    public void Ctor_Throws_WhenAreaIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SimulationContext(
            areaId: Guid.Empty,
            scenario: CreateScenario(),
            sensors: [CreateSensor()],
            startTimestamp: DateTimeOffset.UtcNow,
            interval: TimeSpan.FromSeconds(1),
            numberOfCycles: 1));

        Assert.Equal("areaId", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenNumberOfCyclesIsNotPositive()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationContext(
            areaId: Guid.NewGuid(),
            scenario: CreateScenario(),
            sensors: [CreateSensor()],
            startTimestamp: DateTimeOffset.UtcNow,
            interval: TimeSpan.FromSeconds(1),
            numberOfCycles: 0));

        Assert.Equal("numberOfCycles", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenIntervalIsNotPositive()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationContext(
            areaId: Guid.NewGuid(),
            scenario: CreateScenario(),
            sensors: [CreateSensor()],
            startTimestamp: DateTimeOffset.UtcNow,
            interval: TimeSpan.Zero,
            numberOfCycles: 1));

        Assert.Equal("interval", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenSensorsCollectionIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SimulationContext(
            areaId: Guid.NewGuid(),
            scenario: CreateScenario(),
            sensors: Array.Empty<Sensor>(),
            startTimestamp: DateTimeOffset.UtcNow,
            interval: TimeSpan.FromSeconds(1),
            numberOfCycles: 1));

        Assert.Equal("sensors", ex.ParamName);
    }

    [Fact]
    public void Ctor_PreservesValues_WhenValid()
    {
        var scenario = CreateScenario();
        var sensors = new[] { CreateSensor() };
        var startTimestamp = new DateTimeOffset(2026, 4, 6, 14, 0, 0, TimeSpan.Zero);
        var interval = TimeSpan.FromSeconds(5);

        var context = new SimulationContext(
            areaId: Guid.NewGuid(),
            scenario: scenario,
            sensors: sensors,
            startTimestamp: startTimestamp,
            interval: interval,
            numberOfCycles: 4);

        Assert.Same(scenario, context.Scenario);
        Assert.Same(sensors, context.Sensors);
        Assert.Equal(startTimestamp, context.StartTimestamp);
        Assert.Equal(interval, context.Interval);
        Assert.Equal(4, context.NumberOfCycles);
    }

    private static Scenario CreateScenario()
    {
        return new Scenario(
            id: Guid.NewGuid(),
            name: "Scenario",
            category: ScenarioCategory.Base,
            parameters: new ScenarioParameters(
                baseTemperature: 30,
                baseHumidity: 40,
                baseWindSpeed: 5));
    }

    private static Sensor CreateSensor()
    {
        return new Sensor(
            id: Guid.NewGuid(),
            name: "Sensor-01",
            type: SensorType.Temperature,
            location: new Location(39.8, -7.9),
            profile: new SensorProfile(
                id: Guid.NewGuid(),
                samplingInterval: TimeSpan.FromSeconds(5),
                communicationMode: "LoRa",
                noiseLevel: 0.1,
                latencyProfile: "Low latency",
                failureProfile: "Rare failures"));
    }
}
