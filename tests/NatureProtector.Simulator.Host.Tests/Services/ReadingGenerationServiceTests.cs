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

    [Theory]
    [InlineData("none")]
    [InlineData("missing-readings")]
    public void GenerateReading_ExplicitDegradationProfileDoesNotApplyScenarioFailureRate(string degradationProfile)
    {
        var sensor = CreateSensor(SensorType.Temperature, "Sensor-01");
        var context = CreateContext(
            sensors: [sensor],
            failureRate: 1.0,
            degradationProfile: degradationProfile);

        var envelope = _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10));

        Assert.NotEqual(0.0, envelope.Payload.Value);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
    }

    [Fact]
    public void GenerateReading_MultipleDegradationProfilesPreserveLegacyContract()
    {
        var sensor = CreateSensor(SensorType.Temperature, "Sensor-01");
        var context = CreateContext(
            sensors: [sensor],
            failureRate: 1.0,
            degradationProfiles: [SimulationDegradationProfiles.MissingReadings, SimulationDegradationProfiles.Noise]);

        var envelope = _service.GenerateReading(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(10));

        Assert.Equal(EventTypes.SensorReadingProduced, envelope.EventType);
        Assert.NotEqual(0.0, envelope.Payload.Value);
        Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
        Assert.Equal(SimulationDegradationProfiles.MissingReadings + "+" + SimulationDegradationProfiles.Noise, context.RunOverrides!.Resolved.DegradationProfile);
        Assert.Equal(
            new[] { SimulationDegradationProfiles.MissingReadings, SimulationDegradationProfiles.Noise },
            context.RunOverrides.Resolved.DegradationProfiles);
    }

    [Theory]
    [InlineData(SimulationDegradationProfiles.Noise)]
    [InlineData(SimulationDegradationProfiles.Bias)]
    [InlineData(SimulationDegradationProfiles.Drift)]
    public void GenerateObservation_ObservationProfilesAlterObservedValueButNotTruth(string degradationProfile)
    {
        var sensor = CreateSensor(SensorType.Temperature, "Sensor-01");
        var baseline = CreateContext(sensors: [sensor], failureRate: 0.0, degradationProfile: "none");
        var degraded = CreateContext(sensors: [sensor], failureRate: 0.0, degradationProfile: degradationProfile);

        var baselineObservation = _service.GenerateObservation(
            baseline,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 3,
            eventTime: baseline.StartTimestamp,
            random: new Random(111));
        var degradedObservation = _service.GenerateObservation(
            degraded,
            simulationRunId: baselineObservation.TruthSnapshot.SimulationRunId,
            sensor: sensor,
            cycleIndex: 3,
            eventTime: degraded.StartTimestamp,
            random: new Random(111));

        Assert.Equal(baselineObservation.TruthSnapshot.PhysicalValue, degradedObservation.TruthSnapshot.PhysicalValue);
        Assert.NotEqual(baselineObservation.ObservedValue, degradedObservation.ObservedValue);
        Assert.False(degradedObservation.IsMissing);
    }

    [Fact]
    public void GenerateObservation_TransportProfilesDoNotChangePhysicalReading()
    {
        var sensor = CreateSensor(SensorType.Wind, "Sensor-W");
        var baseline = CreateContext(sensors: [sensor], failureRate: 0.0, degradationProfile: "none");
        var transport = CreateContext(
            sensors: [sensor],
            failureRate: 0.0,
            degradationProfiles: [SimulationDegradationProfiles.Duplicate, SimulationDegradationProfiles.OutOfOrder]);

        var baselineObservation = _service.GenerateObservation(
            baseline,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 2,
            eventTime: baseline.StartTimestamp,
            random: new Random(222));
        var transportObservation = _service.GenerateObservation(
            transport,
            simulationRunId: baselineObservation.TruthSnapshot.SimulationRunId,
            sensor: sensor,
            cycleIndex: 2,
            eventTime: transport.StartTimestamp,
            random: new Random(222));

        Assert.Equal(baselineObservation.TruthSnapshot.PhysicalValue, transportObservation.TruthSnapshot.PhysicalValue);
        Assert.Equal(baselineObservation.ObservedValue, transportObservation.ObservedValue);
        Assert.False(transportObservation.IsMissing);
    }

    [Fact]
    public void GenerateObservation_CreatesTruthSnapshotBeforeLocalObservation()
    {
        var sensor = CreateSensor(SensorType.Temperature, "Sensor-01");
        var context = CreateContext(sensors: [sensor], failureRate: 0.0);
        var runId = Guid.NewGuid();

        var observation = _service.GenerateObservation(
            context,
            simulationRunId: runId,
            sensor: sensor,
            cycleIndex: 1,
            eventTime: context.StartTimestamp,
            random: new Random(77));

        Assert.NotEqual(Guid.Empty, observation.TruthSnapshot.Id);
        Assert.Equal(observation.TruthSnapshot.Id, observation.TruthSnapshotId);
        Assert.Equal(runId, observation.TruthSnapshot.SimulationRunId);
        Assert.Equal(context.Scenario.Id, observation.TruthSnapshot.ScenarioId);
        Assert.Equal(context.AreaId, observation.TruthSnapshot.AreaId);
        Assert.Equal(sensor.Id, observation.TruthSnapshot.SensorId);
        Assert.Equal(context.StartTimestamp, observation.TruthSnapshot.LogicalTimestamp);
        Assert.False(observation.IsMissing);
    }

    [Fact]
    public void LocalObservation_PreservesTruthSnapshotProvenanceInPublishedPayload()
    {
        var sensor = CreateSensor(SensorType.Humidity, "Sensor-H");
        var context = CreateContext(sensors: [sensor], failureRate: 0.0);
        var runId = Guid.NewGuid();

        var observation = _service.GenerateObservation(
            context,
            simulationRunId: runId,
            sensor: sensor,
            cycleIndex: 2,
            eventTime: context.StartTimestamp.AddSeconds(10),
            random: new Random(99));

        var envelope = _service.CreateEnvelope(observation);

        Assert.Equal(observation.TruthSnapshot.SimulationRunId, envelope.Payload.SimulationRunId);
        Assert.Equal(observation.TruthSnapshot.SensorId, envelope.Payload.SensorId);
        Assert.Equal(observation.TruthSnapshot.SensorName, envelope.Payload.SensorName);
        Assert.Equal(observation.TruthSnapshot.MetricType, envelope.Payload.MetricType);
        Assert.Equal(observation.TruthSnapshot.Unit, envelope.Payload.Unit);
        Assert.Equal(observation.ObservedValue, envelope.Payload.Value);
        Assert.Equal(observation.Latitude, envelope.Payload.Latitude);
        Assert.Equal(observation.Longitude, envelope.Payload.Longitude);
        Assert.Equal(EventTypes.SensorReadingProduced, envelope.EventType);
    }

    [Fact]
    public void LocalObservation_AsMissing_CannotBeConvertedToPayload()
    {
        var sensor = CreateSensor(SensorType.Wind, "Sensor-W");
        var context = CreateContext(sensors: [sensor], failureRate: 0.0);
        var observation = _service.GenerateObservation(
            context,
            simulationRunId: Guid.NewGuid(),
            sensor: sensor,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(12));

        var missing = observation.AsMissing("missing-readings");

        Assert.True(missing.IsMissing);
        Assert.Equal("missing-readings", missing.DegradationProfile);
        Assert.Equal(observation.TruthSnapshot.Id, missing.TruthSnapshot.Id);
        Assert.Throws<InvalidOperationException>(() => _service.CreateEnvelope(missing));
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

    [Fact]
    public void GenerateObservations_ProducesDeterministicPhysicalSequence_ForSameSeed()
    {
        var context = CreateContext(
            sensors:
            [
                CreateSensor(SensorType.Temperature, "Sensor-T"),
                CreateSensor(SensorType.Humidity, "Sensor-H"),
                CreateSensor(SensorType.Wind, "Sensor-W")
            ],
            failureRate: 0.0);
        var runId = Guid.NewGuid();

        var first = _service.GenerateObservations(
            context,
            simulationRunId: runId,
            cycleIndex: 2,
            eventTime: context.StartTimestamp,
            random: new Random(20260518));

        var second = _service.GenerateObservations(
            context,
            simulationRunId: runId,
            cycleIndex: 2,
            eventTime: context.StartTimestamp,
            random: new Random(20260518));

        Assert.Equal(
            first.Select(x => x.TruthSnapshot.PhysicalValue).ToArray(),
            second.Select(x => x.TruthSnapshot.PhysicalValue).ToArray());
        Assert.Equal(
            first.Select(x => x.TruthSnapshot.MetricType).ToArray(),
            second.Select(x => x.TruthSnapshot.MetricType).ToArray());
    }

    [Fact]
    public void GenerateObservations_DoesNotApplyMissingReadingsToTruthSnapshots()
    {
        var sensors = new[]
        {
            CreateSensor(SensorType.Temperature, "Sensor-T"),
            CreateSensor(SensorType.Humidity, "Sensor-H")
        };
        var baseline = CreateContext(sensors: sensors, failureRate: 0.0, degradationProfile: "none");
        var degraded = CreateContext(sensors: sensors, failureRate: 0.0, degradationProfile: "missing-readings");
        var runId = Guid.NewGuid();

        var baselineObservations = _service.GenerateObservations(
            baseline,
            simulationRunId: runId,
            cycleIndex: 3,
            eventTime: baseline.StartTimestamp,
            random: new Random(4242));

        var degradedObservations = _service.GenerateObservations(
            degraded,
            simulationRunId: runId,
            cycleIndex: 3,
            eventTime: degraded.StartTimestamp,
            random: new Random(4242));

        Assert.Equal(baselineObservations.Count, degradedObservations.Count);
        Assert.Equal(
            baselineObservations.Select(x => x.TruthSnapshot.PhysicalValue).ToArray(),
            degradedObservations.Select(x => x.TruthSnapshot.PhysicalValue).ToArray());
        Assert.All(degradedObservations, observation => Assert.False(observation.IsMissing));
    }

    private static SimulationContext CreateContext(
        IReadOnlyCollection<Sensor> sensors,
        double? baseTemperature = 31.0,
        double? baseHumidity = 33.0,
        double? baseWindSpeed = 7.5,
        double failureRate = 0.05,
        double noiseLevel = 0.10,
        string? degradationProfile = null,
        IReadOnlyList<string>? degradationProfiles = null)
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
            numberOfCycles: 3,
            runOverrides: degradationProfile is null && degradationProfiles is null
                ? null
                : new SimulationRunOverridesSnapshot(
                    Requested: new SimulationRunOverridesRequested(
                        SensorCount: sensors.Count,
                        NumberOfCycles: 3,
                        IntervalSeconds: 1,
                        Seed: null,
                        DegradationProfile: degradationProfile,
                        OrchestratorCorrelationId: "tests")
                    {
                        DegradationProfiles = SimulationDegradationProfiles.Normalize(degradationProfiles, degradationProfile)
                    },
                    Resolved: new SimulationRunOverridesResolved(
                        SensorCount: sensors.Count,
                        NumberOfCycles: 3,
                        IntervalSeconds: 1,
                        PreferredSeed: null,
                        DegradationProfile: SimulationDegradationProfiles.ToLegacyProfile(
                            SimulationDegradationProfiles.Normalize(degradationProfiles, degradationProfile)),
                        OrchestratorCorrelationId: "tests",
                        SelectedSensorNames: sensors.Select(sensor => sensor.Name).ToArray())
                    {
                        DegradationProfiles = SimulationDegradationProfiles.Normalize(degradationProfiles, degradationProfile)
                    }));
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
