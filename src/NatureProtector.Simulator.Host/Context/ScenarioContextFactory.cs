using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Observability;
using NatureProtector.Simulator.Host.Configuration;
using System.Diagnostics;

/*
 * This factory builds the in-memory simulation context consumed by the host.
 *
 * Rationale:
 * - The simulator host needs a single place where configuration is transformed
 *   into domain objects such as Scenario, ScenarioParameters, SensorProfile and Sensor.
 * - This keeps object creation rules out of the worker and makes the execution
 *   pipeline easier to test and evolve.
 *
 * Design considerations:
 * - The factory performs lightweight validation on configuration before creating
 *   domain objects.
 * - It maps strongly typed options into the current Core model with the minimum
 *   amount of knowledge required by Day 4.
 * - String fields required by SensorProfile are normalized with safe defaults,
 *   avoiding nullability warnings and runtime failures.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class ScenarioContextFactory : ISimulationContextSource
{
    private readonly SimulatorOptions _options;

    /// <summary>
    /// Creates a new factory bound to the configured simulator options.
    /// </summary>
    /// <param name="simulatorOptions">
    /// Bound simulator options.
    /// </param>
    public ScenarioContextFactory(IOptions<SimulatorOptions> simulatorOptions)
    {
        ArgumentNullException.ThrowIfNull(simulatorOptions);
        _options = simulatorOptions.Value ?? throw new ArgumentNullException(nameof(simulatorOptions));
    }

    /// <summary>
    /// Builds the simulation context used by the simulator host.
    /// </summary>
    /// <returns>
    /// Fully initialized simulation context.
    /// </returns>
    public SimulationContext Create()
    {
        using var activity = SimulatorHostTelemetry.ActivitySource.StartActivity("natureprotector.simulator.context.create");
        var stopwatch = Stopwatch.StartNew();
        ValidateOptions(_options);

        var scenarioParameters = new ScenarioParameters(
            baseTemperature: _options.BaseTemperature,
            baseHumidity: _options.BaseHumidity,
            baseWindSpeed: _options.BaseWindSpeed,
            failureRate: _options.FailureRate,
            noiseLevel: _options.NoiseLevel,
            timeAcceleration: _options.TimeAcceleration);

        var scenario = new Scenario(
            id: _options.ScenarioId,
            name: _options.ScenarioName,
            category: _options.ScenarioCategory,
            parameters: scenarioParameters,
            description: _options.ScenarioDescription);

        var sensors = _options.Sensors
            .Select(CreateSensor)
            .ToList()
            .AsReadOnly();

        var startTimestamp = _options.StartTimestamp ?? DateTimeOffset.UtcNow;
        var interval = TimeSpan.FromSeconds(_options.IntervalSeconds);
        var requestedOverrides = _options.RunOverrides ?? new SimulatorRunOverridesOptions();
        var requestedProfiles = SimulationDegradationProfiles.Normalize(
            requestedOverrides.DegradationProfiles,
            requestedOverrides.DegradationProfile);
        var effectiveDegradationProfiles = SimulationDegradationProfiles.Resolve(
            requestedOverrides.DegradationProfiles,
            requestedOverrides.DegradationProfile,
            _options.DegradationProfiles,
            _options.DegradationProfile);
        var effectiveDegradationProfile = SimulationDegradationProfiles.ToLegacyProfile(effectiveDegradationProfiles);
        var runOverrides = effectiveDegradationProfile is null && requestedOverrides.DegradationProfile is null
            ? null
            : new SimulationRunOverridesSnapshot(
                Requested: new SimulationRunOverridesRequested(
                    SensorCount: requestedOverrides.SensorCount,
                    NumberOfCycles: requestedOverrides.NumberOfCycles,
                    IntervalSeconds: requestedOverrides.IntervalSeconds,
                    Seed: requestedOverrides.Seed,
                    DegradationProfile: requestedOverrides.DegradationProfile,
                    OrchestratorCorrelationId: requestedOverrides.OrchestratorCorrelationId)
                {
                    DegradationProfiles = requestedProfiles
                },
                Resolved: new SimulationRunOverridesResolved(
                    SensorCount: sensors.Count,
                    NumberOfCycles: _options.NumberOfCycles,
                    IntervalSeconds: _options.IntervalSeconds,
                    PreferredSeed: _options.Seed,
                    DegradationProfile: effectiveDegradationProfile,
                    OrchestratorCorrelationId: requestedOverrides.OrchestratorCorrelationId,
                    SelectedSensorNames: sensors.Select(sensor => sensor.Name).ToArray())
                {
                    DegradationProfiles = effectiveDegradationProfiles
                });

        var context = new SimulationContext(
            areaId: _options.AreaId,
            scenario: scenario,
            scenarioCode: null,
            sensors: sensors,
            startTimestamp: startTimestamp,
            interval: interval,
            numberOfCycles: _options.NumberOfCycles,
            runOverrides: runOverrides,
            lagDelay: TimeSpan.FromSeconds(_options.LagDelaySeconds));

        activity?.SetTag(TelemetryTags.AreaId, context.AreaId);
        activity?.SetTag(TelemetryTags.ScenarioId, context.Scenario.Id);
        activity?.SetTag(TelemetryTags.Outcome, "completed");
        stopwatch.Stop();
        SimulatorHostTelemetry.ContextCreations.Add(1);
        SimulatorHostTelemetry.ContextCreationDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);

        return context;
    }

    public Task<SimulationContext> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Create());
    }

    /// <summary>
    /// Creates one Sensor aggregate from one configured sensor definition.
    /// </summary>
    /// <param name="definition">
    /// Sensor definition loaded from configuration.
    /// </param>
    /// <returns>
    /// Constructed Sensor instance.
    /// </returns>
    private static Sensor CreateSensor(SensorDefinitionOptions definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException(
                "Each configured sensor must define a non-empty name.",
                nameof(definition));
        }

        if (definition.SamplingIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition.SamplingIntervalSeconds),
                definition.SamplingIntervalSeconds,
                "Sampling interval must be greater than zero.");
        }

        if (!Enum.IsDefined(definition.Type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition.Type),
                definition.Type,
                "Configured sensor type is invalid.");
        }

        var location = new Location(
            latitude: definition.Latitude,
            longitude: definition.Longitude,
            altitude: definition.Altitude);

        var communicationMode = NormalizeRequiredString(
            definition.CommunicationMode,
            "Simulated");

        var latencyProfile = NormalizeRequiredString(
            definition.LatencyProfile,
            "Normal latency");

        var failureProfile = NormalizeRequiredString(
            definition.FailureProfile,
            "Nominal reliability");

        var profile = new SensorProfile(
            id: Guid.NewGuid(),
            samplingInterval: TimeSpan.FromSeconds(definition.SamplingIntervalSeconds),
            communicationMode: communicationMode,
            noiseLevel: definition.ProfileNoiseLevel,
            latencyProfile: latencyProfile,
            failureProfile: failureProfile);

        return new Sensor(
            id: definition.Id ?? Guid.NewGuid(),
            name: definition.Name,
            type: definition.Type,
            location: location,
            profile: profile,
            isActive: definition.IsActive);
    }

    /// <summary>
    /// Normalizes a required string option, applying a safe default when needed.
    /// </summary>
    /// <param name="value">
    /// Raw configured value.
    /// </param>
    /// <param name="fallback">
    /// Fallback text used when the value is null or whitespace.
    /// </param>
    /// <returns>
    /// A non-empty normalized string.
    /// </returns>
    private static string NormalizeRequiredString(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>
    /// Performs lightweight validation over the simulator options before any
    /// domain object is created.
    /// </summary>
    /// <param name="options">
    /// Simulator options to validate.
    /// </param>
    private static void ValidateOptions(SimulatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.AreaId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Simulator AreaId must not be an empty GUID.");
        }

        if (options.ScenarioId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Simulator ScenarioId must not be an empty GUID.");
        }

        if (string.IsNullOrWhiteSpace(options.ScenarioName))
        {
            throw new InvalidOperationException(
                "Simulator ScenarioName must not be null or whitespace.");
        }

        if (!Enum.IsDefined(options.ScenarioCategory))
        {
            throw new InvalidOperationException(
                $"Simulator ScenarioCategory '{options.ScenarioCategory}' is invalid.");
        }

        if (options.NumberOfCycles <= 0)
        {
            throw new InvalidOperationException(
                "Simulator NumberOfCycles must be greater than zero.");
        }

        if (options.IntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Simulator IntervalSeconds must be greater than zero.");
        }

        if (options.BaseHumidity is < 0.0 or > 100.0)
        {
            throw new InvalidOperationException(
                "Simulator BaseHumidity must be in the range [0, 100].");
        }

        if (options.BaseWindSpeed < 0.0)
        {
            throw new InvalidOperationException(
                "Simulator BaseWindSpeed must not be negative.");
        }

        if (options.FailureRate is < 0.0 or > 1.0)
        {
            throw new InvalidOperationException(
                "Simulator FailureRate must be in the range [0, 1].");
        }

        if (options.NoiseLevel < 0.0)
        {
            throw new InvalidOperationException(
                "Simulator NoiseLevel must not be negative.");
        }

        if (options.TimeAcceleration <= 0.0)
        {
            throw new InvalidOperationException(
                "Simulator TimeAcceleration must be greater than zero.");
        }

        if (options.Sensors is null || options.Sensors.Count == 0)
        {
            throw new InvalidOperationException(
                "Simulator must define at least one sensor.");
        }
    }
}
