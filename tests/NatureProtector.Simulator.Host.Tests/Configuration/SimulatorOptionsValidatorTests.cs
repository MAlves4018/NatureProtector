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
}
