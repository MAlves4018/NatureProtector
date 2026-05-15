using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;

/*
 * This class represents the in-memory simulation context built by the host
 * before the simulation loop starts.
 *
 * Rationale:
 * - The worker should not know how scenarios and sensors are created.
 * - A dedicated context object keeps the runner focused on execution, while
 *   the factory remains responsible for assembling the baseline simulation state.
 *
 * Design considerations:
 * - The context is immutable after construction to avoid accidental mutation
 *   during execution.
 * - It contains only the minimum data needed to generate and publish readings
 *   in a deterministic way during Day 4.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class SimulationContext
{
    /// <summary>
    /// Logical identifier of the area associated with the current simulation run.
    /// </summary>
    public Guid AreaId { get; }

    /// <summary>
    /// Scenario definition used as the baseline for simulated readings.
    /// </summary>
    public Scenario Scenario { get; }

    /// <summary>
    /// Optional scenario code when the context originates from the control plane.
    /// </summary>
    public string? ScenarioCode { get; }

    /// <summary>
    /// Sensors that participate in the current simulation.
    /// </summary>
    public IReadOnlyCollection<Sensor> Sensors { get; }

    /// <summary>
    /// Logical timestamp from which the simulation starts.
    /// </summary>
    public DateTimeOffset StartTimestamp { get; }

    /// <summary>
    /// Logical interval between simulation cycles.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Optional configuration version used when the context originates from the control plane.
    /// </summary>
    public Guid? ConfigurationVersionId { get; }

    /// <summary>
    /// Number of cycles the simulator should execute.
    /// </summary>
    public int NumberOfCycles { get; }

    /// <summary>
    /// Optional preferred seed resolved by the context source.
    /// </summary>
    public int? PreferredSeed { get; }

    /// <summary>
    /// Snapshot of run overrides requested/resolved by the orchestrator path.
    /// </summary>
    public SimulationRunOverridesSnapshot? RunOverrides { get; }

    /// <summary>
    /// Creates a new SimulationContext instance.
    /// </summary>
    /// <param name="areaId">
    /// Logical area identifier used during publication.
    /// </param>
    /// <param name="scenario">
    /// Scenario definition for the current run.
    /// </param>
    /// <param name="sensors">
    /// Sensors participating in the current run.
    /// </param>
    /// <param name="startTimestamp">
    /// Logical start timestamp of the simulation.
    /// </param>
    /// <param name="interval">
    /// Logical interval between cycles.
    /// </param>
    /// <param name="numberOfCycles">
    /// Number of cycles to execute.
    /// </param>
    /// <param name="configurationVersionId">
    /// Optional control-plane configuration version that produced this context.
    /// </param>
    /// <param name="scenarioCode">
    /// Optional control-plane scenario code that produced this context.
    /// </param>
    /// <param name="preferredSeed">
    /// Optional preferred seed resolved by the context source.
    /// </param>
    /// <param name="runOverrides">
    /// Optional override snapshot used for metadata persistence.
    /// </param>
    public SimulationContext(
        Guid areaId,
        Scenario scenario,
        IReadOnlyCollection<Sensor> sensors,
        DateTimeOffset startTimestamp,
        TimeSpan interval,
        int numberOfCycles,
        Guid? configurationVersionId = null,
        string? scenarioCode = null,
        int? preferredSeed = null,
        SimulationRunOverridesSnapshot? runOverrides = null)
    {
        if (areaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Area identifier must not be an empty GUID.",
                nameof(areaId));
        }

        if (numberOfCycles <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numberOfCycles),
                numberOfCycles,
                "Number of cycles must be greater than zero.");
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "Interval must be greater than zero.");
        }

        Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        ScenarioCode = string.IsNullOrWhiteSpace(scenarioCode) ? null : scenarioCode.Trim();
        Sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));

        if (Sensors.Count == 0)
        {
            throw new ArgumentException(
                "Simulation context must contain at least one sensor.",
                nameof(sensors));
        }

        AreaId = areaId;
        StartTimestamp = startTimestamp;
        Interval = interval;
        NumberOfCycles = numberOfCycles;
        ConfigurationVersionId = configurationVersionId;
        PreferredSeed = preferredSeed;
        RunOverrides = runOverrides;
    }
}

public sealed record SimulationRunOverridesRequested(
    int? SensorCount,
    int? NumberOfCycles,
    int? IntervalSeconds,
    int? Seed,
    string? DegradationProfile,
    string? OrchestratorCorrelationId);

public sealed record SimulationRunOverridesResolved(
    int SensorCount,
    int NumberOfCycles,
    int IntervalSeconds,
    int? PreferredSeed,
    string? DegradationProfile,
    string? OrchestratorCorrelationId,
    IReadOnlyList<string> SelectedSensorNames);

public sealed record SimulationRunOverridesSnapshot(
    SimulationRunOverridesRequested Requested,
    SimulationRunOverridesResolved Resolved);
