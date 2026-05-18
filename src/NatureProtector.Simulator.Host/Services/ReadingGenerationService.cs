using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Readings;

namespace NatureProtector.Simulator.Host.Services;

public sealed class ReadingGenerationService
{
    private const string ProducerName = "NatureProtector.Simulator.Host";
    private const string SchemaVersion = "1.0";

    public IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>> GenerateBatch(
        SimulationContext context,
        Guid simulationRunId,
        int cycleIndex,
        DateTimeOffset eventTime,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);

        return GenerateObservations(
                context,
                simulationRunId,
                cycleIndex,
                eventTime,
                random)
            .Select(CreateEnvelope)
            .ToArray();
    }

    public IReadOnlyCollection<LocalObservation> GenerateObservations(
        SimulationContext context,
        Guid simulationRunId,
        int cycleIndex,
        DateTimeOffset eventTime,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);

        var observations = new List<LocalObservation>();
        foreach (var sensor in context.Sensors)
        {
            observations.Add(GenerateObservation(
                context,
                simulationRunId,
                sensor,
                cycleIndex,
                eventTime,
                random));
        }

        return observations;
    }

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

        var observation = GenerateObservation(
            context,
            simulationRunId,
            sensor,
            cycleIndex,
            eventTime,
            random);

        return CreateEnvelope(observation);
    }

    public LocalObservation GenerateObservation(
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

        var truthSnapshot = CreateTruthSnapshot(
            context,
            simulationRunId,
            sensor,
            cycleIndex,
            eventTime,
            random);

        return CreateLocalObservation(
            context,
            sensor,
            truthSnapshot,
            random);
    }

    public TruthSnapshot CreateTruthSnapshot(
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

        var metricType = ResolveMetricType(sensor.Type);
        var unit = ResolveMeasurementUnit(sensor.Type);
        var physicalValue = GeneratePhysicalValue(
            context,
            sensor,
            cycleIndex,
            random);

        return new TruthSnapshot(
            Id: Guid.NewGuid(),
            SimulationRunId: simulationRunId,
            ScenarioId: context.Scenario.Id,
            ScenarioCode: context.ScenarioCode,
            AreaId: context.AreaId,
            SensorId: sensor.Id,
            SensorName: sensor.Name,
            GridCellId: sensor.Location.CellId,
            CycleIndex: cycleIndex,
            LogicalTimestamp: eventTime,
            MetricType: metricType,
            Unit: unit,
            PhysicalValue: physicalValue);
    }

    public LocalObservation CreateLocalObservation(
        SimulationContext context,
        Sensor sensor,
        TruthSnapshot truthSnapshot,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(truthSnapshot);
        ArgumentNullException.ThrowIfNull(random);

        var failureRate = context.Scenario.Parameters.FailureRate;
        var isAvailable = sensor.IsActive && random.NextDouble() >= failureRate;
        var observedValue = isAvailable
            ? ApplyObservationNoise(truthSnapshot, sensor, random)
            : 0.0;

        return new LocalObservation(
            Id: Guid.NewGuid(),
            TruthSnapshotId: truthSnapshot.Id,
            TruthSnapshot: truthSnapshot,
            ObservedValue: observedValue,
            Latitude: sensor.Location.Latitude,
            Longitude: sensor.Location.Longitude,
            OperationalState: isAvailable
                ? SensorOperationalState.Nominal
                : SensorOperationalState.Invalid,
            DegradationProfile: null,
            IsMissing: false);
    }

    public EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(LocalObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var truthSnapshot = observation.TruthSnapshot;
        var correlationId = $"{truthSnapshot.SimulationRunId:N}-{truthSnapshot.CycleIndex:D4}-{truthSnapshot.SensorId:N}";

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: SchemaVersion,
            EventId: Guid.NewGuid(),
            CorrelationId: correlationId,
            Producer: ProducerName,
            EventType: EventTypes.SensorReadingProduced,
            AreaId: truthSnapshot.AreaId,
            EventTime: truthSnapshot.LogicalTimestamp,
            IngestTime: null,
            Payload: observation.ToPayload());
    }

    private static double GeneratePhysicalValue(
        SimulationContext context,
        Sensor sensor,
        int cycleIndex,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(random);

        var parameters = context.Scenario.Parameters;
        var baseTemperature = RequireValue(parameters.BaseTemperature, nameof(parameters.BaseTemperature));
        var baseHumidity = RequireValue(parameters.BaseHumidity, nameof(parameters.BaseHumidity));
        var baseWindSpeed = RequireValue(parameters.BaseWindSpeed, nameof(parameters.BaseWindSpeed));
        var temporalWave = Math.Sin(cycleIndex / 3.0);
        var scenarioNoise = parameters.NoiseLevel;

        return sensor.Type switch
        {
            SensorType.Temperature => Clamp(
                baseTemperature
                + (temporalWave * 1.5)
                + NextCenteredNoise(random, scenarioNoise, amplitude: 2.0),
                min: -20.0,
                max: 60.0),

            SensorType.Humidity => Clamp(
                baseHumidity
                - (temporalWave * 4.0)
                + NextCenteredNoise(random, scenarioNoise, amplitude: 5.0),
                min: 0.0,
                max: 100.0),

            SensorType.Wind => Clamp(
                baseWindSpeed
                + Math.Abs(temporalWave * 1.8)
                + NextCenteredNoise(random, scenarioNoise, amplitude: 1.5),
                min: 0.0,
                max: 35.0),

            SensorType.Composite => throw new InvalidOperationException(
                "Composite sensors are not yet supported by the current shared reading contracts. " +
                "Use Temperature, Humidity or Wind sensors in the simulator configuration for Day 4."),

            _ => throw new InvalidOperationException(
                $"Sensor type '{sensor.Type}' is not supported by the simulator.")
        };
    }

    private static double ApplyObservationNoise(
        TruthSnapshot truthSnapshot,
        Sensor sensor,
        Random random)
    {
        var amplitude = sensor.Type switch
        {
            SensorType.Temperature => 2.0,
            SensorType.Humidity => 5.0,
            SensorType.Wind => 1.5,
            _ => throw new InvalidOperationException(
                $"Sensor type '{sensor.Type}' is not supported by the simulator.")
        };

        var value = truthSnapshot.PhysicalValue
            + NextCenteredNoise(random, sensor.Profile.NoiseLevel, amplitude);

        return truthSnapshot.MetricType switch
        {
            SensorMetricType.Temperature => Clamp(value, min: -20.0, max: 60.0),
            SensorMetricType.Humidity => Clamp(value, min: 0.0, max: 100.0),
            SensorMetricType.WindSpeed => Clamp(value, min: 0.0, max: 35.0),
            _ => value
        };
    }

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

    private static double RequireValue(double? value, string name)
    {
        return value ?? throw new InvalidOperationException(
            $"Scenario parameter '{name}' must have a value for simulation reading generation.");
    }

    private static double NextCenteredNoise(Random random, double noiseLevel, double amplitude)
    {
        var raw = (random.NextDouble() * 2.0) - 1.0;
        return raw * amplitude * noiseLevel;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
