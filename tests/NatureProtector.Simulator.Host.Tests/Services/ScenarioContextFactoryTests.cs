using Microsoft.Extensions.Options;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.Tests.TestData;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class ScenarioContextFactoryTests
{
    [Fact]
    public void Ctor_Throws_WhenOptionsWrapperIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ScenarioContextFactory(null!));

        Assert.Equal("simulatorOptions", ex.ParamName);
    }

    [Fact]
    public void Create_BuildsScenarioAndSensors_FromOptions()
    {
        var options = SimulatorOptionsMother.CreateValid();
        var factory = new ScenarioContextFactory(Options.Create(options));

        var context = factory.Create();

        Assert.Equal(options.AreaId, context.AreaId);
        Assert.Equal(options.ScenarioId, context.Scenario.Id);
        Assert.Equal(options.ScenarioName, context.Scenario.Name);
        Assert.Equal(options.ScenarioDescription, context.Scenario.Description);
        Assert.Equal(options.ScenarioCategory, context.Scenario.Category);
        Assert.Equal(options.BaseTemperature, context.Scenario.Parameters.BaseTemperature);
        Assert.Equal(options.BaseHumidity, context.Scenario.Parameters.BaseHumidity);
        Assert.Equal(options.BaseWindSpeed, context.Scenario.Parameters.BaseWindSpeed);
        Assert.Equal(TimeSpan.FromSeconds(options.IntervalSeconds), context.Interval);
        Assert.Equal(options.StartTimestamp, context.StartTimestamp);
        Assert.Equal(options.NumberOfCycles, context.NumberOfCycles);
        Assert.Null(context.RunOverrides);

        var sensors = context.Sensors.ToList();
        Assert.Equal(2, sensors.Count);
        Assert.Equal("Temperature-01", sensors[0].Name);
        Assert.Equal(SensorType.Temperature, sensors[0].Type);
        Assert.Equal("LoRa", sensors[0].Profile.CommunicationMode);
        Assert.Equal("Low latency", sensors[0].Profile.LatencyProfile);
        Assert.Equal("Rare failures", sensors[0].Profile.FailureProfile);
    }

    [Fact]
    public void Create_UsesScenarioDegradationProfile_WhenRunOverrideIsMissing()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.DegradationProfile = "missing-readings";
        var factory = new ScenarioContextFactory(Options.Create(options));

        var context = factory.Create();

        Assert.NotNull(context.RunOverrides);
        Assert.Null(context.RunOverrides!.Requested.DegradationProfile);
        Assert.Equal("missing-readings", context.RunOverrides.Resolved.DegradationProfile);
    }

    [Fact]
    public void Create_RunOverrideDegradationProfileOverridesScenarioProfile()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.DegradationProfile = "missing-readings";
        options.RunOverrides.DegradationProfile = "none";
        var factory = new ScenarioContextFactory(Options.Create(options));

        var context = factory.Create();

        Assert.NotNull(context.RunOverrides);
        Assert.Equal("none", context.RunOverrides!.Requested.DegradationProfile);
        Assert.Equal("none", context.RunOverrides.Resolved.DegradationProfile);
    }

    [Fact]
    public void Create_NormalizesFallbackStrings_WhenOptionalSensorFieldsAreBlank()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors =
        [
            SimulatorOptionsMother.CreateSensorDefinition(
                name: "Sensor-X",
                communicationMode: "   ",
                latencyProfile: "   ",
                failureProfile: null)
        ];
        var factory = new ScenarioContextFactory(Options.Create(options));

        var context = factory.Create();
        var sensor = Assert.Single(context.Sensors);

        Assert.Equal("Simulated", sensor.Profile.CommunicationMode);
        Assert.Equal("Normal latency", sensor.Profile.LatencyProfile);
        Assert.Equal("Nominal reliability", sensor.Profile.FailureProfile);
    }

    [Fact]
    public void Create_UsesCurrentUtcTime_WhenStartTimestampIsNotProvided()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.StartTimestamp = null;
        var factory = new ScenarioContextFactory(Options.Create(options));
        var before = DateTimeOffset.UtcNow;

        var context = factory.Create();

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(context.StartTimestamp, before, after);
    }

    [Fact]
    public void Create_Throws_WhenAreaIdIsEmpty()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.AreaId = Guid.Empty;
        var factory = new ScenarioContextFactory(Options.Create(options));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Create());

        Assert.Contains("AreaId must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Create_Throws_WhenSensorNameIsMissing()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(name: "   ")];
        var factory = new ScenarioContextFactory(Options.Create(options));

        var ex = Assert.Throws<ArgumentException>(() => factory.Create());

        Assert.Equal("definition", ex.ParamName);
        Assert.Contains("must define a non-empty name", ex.Message);
    }

    [Fact]
    public void Create_Throws_WhenSensorSamplingIntervalIsNotPositive()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(samplingIntervalSeconds: 0)];
        var factory = new ScenarioContextFactory(Options.Create(options));

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create());

        Assert.Equal("SamplingIntervalSeconds", ex.ParamName);
    }

    [Fact]
    public void Create_Throws_WhenSensorTypeIsInvalid()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(type: (SensorType)999)];
        var factory = new ScenarioContextFactory(Options.Create(options));

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create());

        Assert.Equal("Type", ex.ParamName);
    }

    [Theory]
    [InlineData("ScenarioIdEmpty", "ScenarioId must not be an empty GUID.")]
    [InlineData("ScenarioNameBlank", "ScenarioName must not be null or whitespace.")]
    [InlineData("ScenarioCategoryInvalid", "ScenarioCategory")]
    [InlineData("NumberOfCyclesInvalid", "NumberOfCycles must be greater than zero.")]
    [InlineData("IntervalSecondsInvalid", "IntervalSeconds must be greater than zero.")]
    [InlineData("BaseHumidityLow", "BaseHumidity must be in the range [0, 100].")]
    [InlineData("BaseHumidityHigh", "BaseHumidity must be in the range [0, 100].")]
    [InlineData("BaseWindSpeedNegative", "BaseWindSpeed must not be negative.")]
    [InlineData("FailureRateLow", "FailureRate must be in the range [0, 1].")]
    [InlineData("FailureRateHigh", "FailureRate must be in the range [0, 1].")]
    [InlineData("NoiseLevelNegative", "NoiseLevel must not be negative.")]
    [InlineData("TimeAccelerationInvalid", "TimeAcceleration must be greater than zero.")]
    [InlineData("SensorsNull", "must define at least one sensor.")]
    [InlineData("SensorsEmpty", "must define at least one sensor.")]
    public void Create_Throws_WhenTopLevelOptionsAreInvalid(string scenario, string expectedMessage)
    {
        var options = SimulatorOptionsMother.CreateValid();

        switch (scenario)
        {
            case "ScenarioIdEmpty":
                options.ScenarioId = Guid.Empty;
                break;
            case "ScenarioNameBlank":
                options.ScenarioName = "   ";
                break;
            case "ScenarioCategoryInvalid":
                options.ScenarioCategory = (ScenarioCategory)999;
                break;
            case "NumberOfCyclesInvalid":
                options.NumberOfCycles = 0;
                break;
            case "IntervalSecondsInvalid":
                options.IntervalSeconds = 0;
                break;
            case "BaseHumidityLow":
                options.BaseHumidity = -0.1;
                break;
            case "BaseHumidityHigh":
                options.BaseHumidity = 100.1;
                break;
            case "BaseWindSpeedNegative":
                options.BaseWindSpeed = -1.0;
                break;
            case "FailureRateLow":
                options.FailureRate = -0.1;
                break;
            case "FailureRateHigh":
                options.FailureRate = 1.1;
                break;
            case "NoiseLevelNegative":
                options.NoiseLevel = -0.1;
                break;
            case "TimeAccelerationInvalid":
                options.TimeAcceleration = 0.0;
                break;
            case "SensorsNull":
                options.Sensors = null!;
                break;
            case "SensorsEmpty":
                options.Sensors = [];
                break;
            default:
                throw new InvalidOperationException($"Unknown scenario '{scenario}'.");
        }

        var factory = new ScenarioContextFactory(Options.Create(options));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Create());

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void Create_PreservesConfiguredSensorIdentity_AndAltitude()
    {
        var sensorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors =
        [
            SimulatorOptionsMother.CreateSensorDefinition(
                id: sensorId,
                altitude: 245.5,
                communicationMode: "  Satellite  ",
                latencyProfile: "  Burst  ",
                failureProfile: "  Stable  ")
        ];
        var factory = new ScenarioContextFactory(Options.Create(options));

        var context = factory.Create();
        var sensor = Assert.Single(context.Sensors);

        Assert.Equal(sensorId, sensor.Id);
        Assert.Equal(245.5, sensor.Location.Altitude);
        Assert.Equal("Satellite", sensor.Profile.CommunicationMode);
        Assert.Equal("Burst", sensor.Profile.LatencyProfile);
        Assert.Equal("Stable", sensor.Profile.FailureProfile);
    }
}
