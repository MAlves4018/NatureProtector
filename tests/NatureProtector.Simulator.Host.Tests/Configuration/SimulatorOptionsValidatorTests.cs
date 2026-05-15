using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Tests.TestData;

namespace NatureProtector.Simulator.Host.Tests.Configuration;

public sealed class SimulatorOptionsValidatorTests
{
    private readonly SimulatorOptionsValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_ForSupportedStandaloneProfile()
    {
        var options = SimulatorOptionsMother.CreateValid();

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenControlPlaneProfileMissesCodes()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ControlPlaneEnabled = true;
        options.ControlPlaneAreaCode = "   ";
        options.ControlPlaneScenarioCode = null;

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Simulator:ControlPlaneAreaCode is required when ControlPlaneEnabled=true.",
            result.Failures);
        Assert.Contains(
            "Simulator:ControlPlaneScenarioCode is required when ControlPlaneEnabled=true.",
            result.Failures);
    }

    [Fact]
    public void Validate_Fails_WhenControlPlaneProfileAlsoConfiguresManifest()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ControlPlaneEnabled = true;
        options.ControlPlaneAreaCode = "proenca-a-nova";
        options.ControlPlaneScenarioCode = "scenario_b";
        options.ScenarioManifestPath = "data/manifests/scenarios/proenca-a-nova.json";

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Simulator:ScenarioManifestPath is not supported when ControlPlaneEnabled=true.",
            result.Failures);
    }

    [Fact]
    public void Validate_Fails_WhenStandaloneProfileUsesUnsupportedSensorType()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ControlPlaneEnabled = false;
        options.Sensors =
        [
            SimulatorOptionsMother.CreateSensorDefinition(type: SensorType.Composite)
        ];

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Simulator:Sensors contains unsupported standalone sensor type 'Composite'. Use Temperature, Humidity or Wind.",
            result.Failures);
    }

    [Fact]
    public void Validate_StandaloneProfileMissingRequiredFields_ReturnsSpecificFailures()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ControlPlaneEnabled = false;
        options.AreaId = Guid.Empty;
        options.ScenarioId = Guid.Empty;
        options.ScenarioName = " ";
        options.Sensors = [];

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains("Simulator:AreaId is required when ControlPlaneEnabled=false.", result.Failures);
        Assert.Contains("Simulator:ScenarioId is required when ControlPlaneEnabled=false.", result.Failures);
        Assert.Contains("Simulator:ScenarioName is required when ControlPlaneEnabled=false.", result.Failures);
        Assert.Contains("Simulator:Sensors must define at least one sensor when ControlPlaneEnabled=false.", result.Failures);
    }

    [Fact]
    public void Validate_StandaloneProfileWithNullSensors_ReturnsSensorFailure()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.ControlPlaneEnabled = false;
        options.Sensors = null!;

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Simulator:Sensors must define at least one sensor when ControlPlaneEnabled=false.",
            result.Failures);
    }

    [Theory]
    [InlineData("SensorCount", "Simulator:RunOverrides:SensorCount must be greater than zero when provided.")]
    [InlineData("NumberOfCycles", "Simulator:RunOverrides:NumberOfCycles must be greater than zero when provided.")]
    [InlineData("IntervalSeconds", "Simulator:RunOverrides:IntervalSeconds must be greater than zero when provided.")]
    public void Validate_RunOverrideIsZero_ReturnsSpecificFailure(
        string overrideName,
        string expectedFailure)
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.RunOverrides = new SimulatorRunOverridesOptions();

        switch (overrideName)
        {
            case "SensorCount":
                options.RunOverrides.SensorCount = 0;
                break;
            case "NumberOfCycles":
                options.RunOverrides.NumberOfCycles = 0;
                break;
            case "IntervalSeconds":
                options.RunOverrides.IntervalSeconds = 0;
                break;
        }

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.Failures);
    }

    [Fact]
    public void Validate_ValidStandaloneProfileWithRunOverrides_Succeeds()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.RunOverrides = new SimulatorRunOverridesOptions
        {
            SensorCount = 1,
            NumberOfCycles = 2,
            IntervalSeconds = 3
        };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }
}
