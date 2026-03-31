using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * This service is responsible for generating plausible simulated sensor readings
 * for the current simulation cycle.
 *
 * Rationale:
 * - Reading generation should be isolated from orchestration and publication.
 * - This makes the simulation pipeline easier to reason about, easier to test
 *   and easier to extend later with richer behaviour.
 *
 * Design considerations:
 * - The service uses scenario baseline values plus bounded pseudo-random noise
 *   to generate plausible values.
 * - Sensor type determines both the generated metric and the measurement unit.
 * - The implementation is aligned with the current shared contracts actually
 *   available in the solution, avoiding assumptions about non-existent enum members.
 * - For the current phase, only Temperature, Humidity and Wind are published as
 *   event metrics. If Composite sensors exist in configuration, they should not
 *   be used until the shared contracts expose a dedicated metric and unit for them.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class ReadingGenerationService
{
    private const string ProducerName = "NatureProtector.Simulator.Host";
    private const string SchemaVersion = "1.0";

    /// <summary>
    /// Generates one batch of readings, one per configured sensor.
    /// </summary>
    /// <param name="context">
    /// In-memory simulation context for the current execution.
    /// </param>
    /// <param name="simulationRunId">
    /// Identifier of the current simulation run.
    /// </param>
    /// <param name="cycleIndex">
    /// Zero-based cycle index used to evolve generated values over time.
    /// </param>
    /// <param name="eventTime">
    /// Logical timestamp associated with this cycle.
    /// </param>
    /// <param name="random">
    /// Pseudo-random generator created from the resolved simulation seed.
    /// </param>
    /// <returns>
    /// Collection of envelopes ready to be published.
    /// </returns>
    public IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>> GenerateBatch(
        SimulationContext context,
        Guid simulationRunId,
        int cycleIndex,
        DateTimeOffset eventTime,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);

        var envelopes = new List<EventEnvelope<SensorReadingProducedPayload>>();

        foreach (var sensor in context.Sensors)
        {
            var envelope = GenerateReading(
                context,
                simulationRunId,
                sensor,
                cycleIndex,
                eventTime,
                random);

            envelopes.Add(envelope);
        }

        return envelopes;
    }

    /// <summary>
    /// Generates one simulated reading envelope for one sensor.
    /// </summary>
    /// <param name="context">
    /// In-memory simulation context.
    /// </param>
    /// <param name="simulationRunId">
    /// Identifier of the simulation run.
    /// </param>
    /// <param name="sensor">
    /// Sensor for which the reading is generated.
    /// </param>
    /// <param name="cycleIndex">
    /// Current cycle index.
    /// </param>
    /// <param name="eventTime">
    /// Logical timestamp for the reading.
    /// </param>
    /// <param name="random">
    /// Deterministic pseudo-random generator.
    /// </param>
    /// <returns>
    /// Fully constructed event envelope ready for publication.
    /// </returns>
    public EventEnvelope<SensorReadingProducedPayload> GenerateReading(
        SimulationContext context,
        Guid simulationRunId,
        Sensor sensor,
        int cycleIndex,
        DateTimeOffset eventTime,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(random);

        var failureRate = context.Scenario.Parameters.FailureRate;
        var isAvailable = sensor.IsActive && random.NextDouble() >= failureRate;

        var metricType = ResolveMetricType(sensor.Type);
        var unit = ResolveMeasurementUnit(sensor.Type);

        double value;

        if (!isAvailable)
        {
            value = 0.0;
        }
        else
        {
            value = GenerateMetricValue(
                context,
                sensor,
                cycleIndex,
                random);
        }

        var payload = new SensorReadingProducedPayload(
            SimulationRunId: simulationRunId,
            SensorId: sensor.Id,
            SensorName: sensor.Name,
            MetricType: metricType,
            Unit: unit,
            Value: value,
            Latitude: sensor.Location.Latitude,
            Longitude: sensor.Location.Longitude,
            OperationalState: isAvailable
                ? SensorOperationalState.Nominal
                : SensorOperationalState.Invalid);

        var correlationId = $"{simulationRunId:N}-{cycleIndex:D4}-{sensor.Id:N}";

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: SchemaVersion,
            EventId: Guid.NewGuid(),
            CorrelationId: correlationId,
            Producer: ProducerName,
            EventType: EventTypes.SensorReadingProduced,
            AreaId: context.AreaId,
            EventTime: eventTime,
            IngestTime: null,
            Payload: payload);
    }

    /// <summary>
    /// Generates a plausible numeric value based on scenario baseline and sensor type.
    /// </summary>
    /// <param name="context">
    /// Simulation context containing the scenario baseline.
    /// </param>
    /// <param name="sensor">
    /// Sensor whose type determines the metric generation logic.
    /// </param>
    /// <param name="cycleIndex">
    /// Current cycle index used to introduce a smooth temporal variation.
    /// </param>
    /// <param name="random">
    /// Deterministic pseudo-random generator.
    /// </param>
    /// <returns>
    /// Generated numeric value for the selected sensor metric.
    /// </returns>
    private static double GenerateMetricValue(
        SimulationContext context,
        Sensor sensor,
        int cycleIndex,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(random);

        var parameters = context.Scenario.Parameters;

        var baseTemperature = RequireValue(
            parameters.BaseTemperature,
            nameof(parameters.BaseTemperature));

        var baseHumidity = RequireValue(
            parameters.BaseHumidity,
            nameof(parameters.BaseHumidity));

        var baseWindSpeed = RequireValue(
            parameters.BaseWindSpeed,
            nameof(parameters.BaseWindSpeed));

        var temporalWave = Math.Sin(cycleIndex / 3.0);
        var profileNoise = sensor.Profile.NoiseLevel;
        var scenarioNoise = parameters.NoiseLevel;
        var totalNoise = profileNoise + scenarioNoise;

        return sensor.Type switch
        {
            SensorType.Temperature => Clamp(
                baseTemperature
                + (temporalWave * 1.5)
                + NextCenteredNoise(random, totalNoise, amplitude: 2.0),
                min: -20.0,
                max: 60.0),

            SensorType.Humidity => Clamp(
                baseHumidity
                - (temporalWave * 4.0)
                + NextCenteredNoise(random, totalNoise, amplitude: 5.0),
                min: 0.0,
                max: 100.0),

            SensorType.Wind => Clamp(
                baseWindSpeed
                + Math.Abs(temporalWave * 1.8)
                + NextCenteredNoise(random, totalNoise, amplitude: 1.5),
                min: 0.0,
                max: 35.0),

            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors are not yet supported by the current shared reading contracts. " +
                "Use Temperature, Humidity or Wind sensors in the simulator configuration for Day 4."),

            _ => throw new InvalidOperationException(
                $"Sensor type '{sensor.Type}' is not supported by the simulator.")
        };
    }

    /// <summary>
    /// Maps a SensorType to the event contract metric type.
    /// </summary>
    /// <param name="sensorType">
    /// Sensor type from the Core domain.
    /// </param>
    /// <returns>
    /// Metric type used in the published payload.
    /// </returns>
    private static SensorMetricType ResolveMetricType(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Temperature => SensorMetricType.Temperature,
            SensorType.Humidity => SensorMetricType.Humidity,
            SensorType.Wind => SensorMetricType.WindSpeed,
            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors do not yet have a matching metric type in the shared contracts."),
            _ => throw new InvalidOperationException(
                $"Sensor type '{sensorType}' cannot be mapped to a metric type.")
        };
    }

    /// <summary>
    /// Maps a SensorType to the corresponding measurement unit.
    /// </summary>
    /// <param name="sensorType">
    /// Sensor type from the Core domain.
    /// </param>
    /// <returns>
    /// Unit used in the published payload.
    /// </returns>
    private static MeasurementUnit ResolveMeasurementUnit(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Temperature => MeasurementUnit.Celsius,
            SensorType.Humidity => MeasurementUnit.Percent,
            SensorType.Wind => MeasurementUnit.MetersPerSecond,
            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors do not yet have a matching measurement unit in the shared contracts."),
            _ => throw new InvalidOperationException(
                $"Sensor type '{sensorType}' cannot be mapped to a measurement unit.")
        };
    }

    /// <summary>
    /// Validates that a nullable numeric scenario parameter is present.
    /// </summary>
    /// <param name="value">
    /// Nullable value to validate.
    /// </param>
    /// <param name="name">
    /// Parameter name used for error reporting.
    /// </param>
    /// <returns>
    /// The non-null numeric value.
    /// </returns>
    private static double RequireValue(double? value, string name)
    {
        return value ?? throw new InvalidOperationException(
            $"Scenario parameter '{name}' must have a value for simulation reading generation.");
    }

    /// <summary>
    /// Generates zero-centered bounded noise.
    /// </summary>
    /// <param name="random">
    /// Pseudo-random generator.
    /// </param>
    /// <param name="noiseLevel">
    /// Noise level factor.
    /// </param>
    /// <param name="amplitude">
    /// Maximum absolute impact of the noise.
    /// </param>
    /// <returns>
    /// Signed bounded random value.
    /// </returns>
    private static double NextCenteredNoise(Random random, double noiseLevel, double amplitude)
    {
        var raw = (random.NextDouble() * 2.0) - 1.0;
        return raw * amplitude * noiseLevel;
    }

    /// <summary>
    /// Clamps a numeric value to a closed interval.
    /// </summary>
    /// <param name="value">
    /// Value to clamp.
    /// </param>
    /// <param name="min">
    /// Minimum allowed value.
    /// </param>
    /// <param name="max">
    /// Maximum allowed value.
    /// </param>
    /// <returns>
    /// Clamped value.
    /// </returns>
    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}