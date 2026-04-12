using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;

/*
 * This class represents the configuration required by the simulator host.
 *
 * Rationale:
 * - The simulator host needs a single configuration root that defines how many
 *   cycles it should run, which seed it should use, which scenario baseline
 *   should be created and which sensors should participate in the simulation.
 *
 * Design considerations:
 * - The options are intentionally simple and strongly typed so they can be bound
 *   directly from appsettings.json.
 * - Scenario-related values are kept here because, at this phase, the host is
 *   responsible for building a synthetic simulation context in memory.
 * - Sensor definitions are also included here to make the simulation fully
 *   configurable without hardcoding deployment data inside the worker logic.
 */

namespace NatureProtector.Simulator.Host.Configuration;

public sealed class SimulatorOptions
{
    /// <summary>
    /// Configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "Simulator";

    /// <summary>
    /// Optional fixed seed for deterministic pseudo-random generation.
    /// When omitted, the simulator will generate one at startup.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Optional path to a generated scenario manifest JSON file.
    /// When configured, the manifest can override the simulator baseline options.
    /// </summary>
    public string? ScenarioManifestPath { get; set; }

    /// <summary>
    /// Optional scenario key used when ScenarioManifestPath points to a generated
    /// catalog containing multiple scenarios.
    /// </summary>
    public string? ScenarioManifestScenarioKey { get; set; }

    /// <summary>
    /// Enables loading the scenario baseline and sensor topology from the
    /// PostgreSQL control plane instead of using only appsettings/manifest data.
    /// </summary>
    public bool ControlPlaneEnabled { get; set; }

    /// <summary>
    /// Optional area code used when resolving the pilot area from the control plane.
    /// </summary>
    public string? ControlPlaneAreaCode { get; set; }

    /// <summary>
    /// Optional scenario code used when resolving the scenario from the control plane.
    /// </summary>
    public string? ControlPlaneScenarioCode { get; set; }

    /// <summary>
    /// Number of simulation cycles to execute before stopping.
    /// </summary>
    public int NumberOfCycles { get; set; } = 12;

    /// <summary>
    /// Time interval, in seconds, between generated reading batches.
    /// </summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Optional fixed logical start timestamp for deterministic runs.
    /// When omitted, the current UTC time is used.
    /// </summary>
    public DateTimeOffset? StartTimestamp { get; set; }

    /// <summary>
    /// Logical area identifier used in published envelopes.
    /// </summary>
    public Guid AreaId { get; set; } =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Scenario identifier to use when constructing the simulation context.
    /// </summary>
    public Guid ScenarioId { get; set; } =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Human-readable scenario name.
    /// </summary>
    public string ScenarioName { get; set; } = "Base Preventive Scenario";

    /// <summary>
    /// Optional free-form scenario description.
    /// </summary>
    public string? ScenarioDescription { get; set; } =
        "Default deterministic scenario used by the simulator host.";

    /// <summary>
    /// Scenario category used to shape the simulation baseline.
    /// </summary>
    public ScenarioCategory ScenarioCategory { get; set; } = ScenarioCategory.Base;

    /// <summary>
    /// Baseline temperature, in degrees Celsius, used by the scenario.
    /// </summary>
    public double BaseTemperature { get; set; } = 28.0;

    /// <summary>
    /// Baseline relative humidity, in percent, used by the scenario.
    /// </summary>
    public double BaseHumidity { get; set; } = 35.0;

    /// <summary>
    /// Baseline wind speed, in meters per second, used by the scenario.
    /// </summary>
    public double BaseWindSpeed { get; set; } = 6.0;

    /// <summary>
    /// Approximate failure rate used by the scenario.
    /// </summary>
    public double FailureRate { get; set; } = 0.05;

    /// <summary>
    /// Noise level used both by the scenario and by generated sensor profiles.
    /// </summary>
    public double NoiseLevel { get; set; } = 0.10;

    /// <summary>
    /// Time acceleration factor for the scenario.
    /// </summary>
    public double TimeAcceleration { get; set; } = 1.0;

    /// <summary>
    /// List of sensors that should exist in the in-memory simulation context.
    /// </summary>
    public List<SensorDefinitionOptions> Sensors { get; set; } = [];
}

/// <summary>
/// Configuration object describing one simulated sensor instance.
/// </summary>
public sealed class SensorDefinitionOptions
{
    /// <summary>
    /// Optional explicit identifier of the sensor.
    /// When omitted, a deterministic identifier can be generated elsewhere if needed.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Human-readable sensor name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Functional type of the sensor.
    /// </summary>
    public SensorType Type { get; set; } = SensorType.Composite;

    /// <summary>
    /// Latitude of the sensor location.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude of the sensor location.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Optional altitude of the sensor location.
    /// </summary>
    public double? Altitude { get; set; }

    /// <summary>
    /// Indicates whether the sensor starts active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Sampling interval, in seconds, associated with the sensor profile.
    /// </summary>
    public int SamplingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Communication mode label used in the synthetic sensor profile.
    /// </summary>
    public string CommunicationMode { get; set; } = "Simulated";

    /// <summary>
    /// Noise level associated with the synthetic sensor profile.
    /// </summary>
    public double ProfileNoiseLevel { get; set; } = 0.10;

    /// <summary>
    /// Optional latency profile description.
    /// </summary>
    public string? LatencyProfile { get; set; } = "Low latency";

    /// <summary>
    /// Optional failure profile description.
    /// </summary>
    public string? FailureProfile { get; set; } = "Rare failures";
}
