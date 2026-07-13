using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.Tests.Fakes;
using NatureProtector.Simulator.Host.Tests.Helpers;
using NatureProtector.Simulator.Host.Tests.TestData;
using Microsoft.Extensions.Hosting;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class SimulationRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesExpectedNumberOfEnvelopesAcrossCycles()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 2;
        options.IntervalSeconds = 1;
        var publisher = new CollectingReadingPublisher();
        var runner = CreateRunner(options, publisher);

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        Assert.Equal(options.NumberOfCycles * options.Sensors.Count, publisher.Published.Count);
    }

    [Fact]
    public async Task ScenarioB_WithDegradationProfileNone_PublishesExpectedEventCount()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 5;
        options.IntervalSeconds = 1;
        options.FailureRate = 1.0;
        options.DegradationProfile = "none";
        options.Sensors =
        [
            SimulatorOptionsMother.CreateSensorDefinition(name: "Temperature-01", type: SensorType.Temperature),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Temperature-02", type: SensorType.Temperature),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Humidity-01", type: SensorType.Humidity),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Humidity-02", type: SensorType.Humidity),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Wind-01", type: SensorType.Wind),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Wind-02", type: SensorType.Wind)
        ];
        var publisher = new CollectingReadingPublisher();
        var runner = CreateRunner(options, publisher);

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        Assert.Equal(30, publisher.Published.Count);
        Assert.All(publisher.Published, envelope =>
            Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState));
    }

    [Fact]
    public async Task ExecuteAsync_MissingReadingsDegradation_OmitsDeterministicSubset()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 5;
        options.IntervalSeconds = 1;
        var context = CreateContextWithDegradation(options, "missing-readings");
        var publisher = new CollectingReadingPublisher();
        var runner = new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new StaticSimulationContextSource(context),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: new NoOpSimulationRunStore(),
            readingPublisher: publisher,
            processExitCode: new NoOpSimulatorProcessExitCode(),
            applicationLifetime: new NoOpApplicationLifetime());

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        var expectedWithoutDegradation = context.NumberOfCycles * context.Sensors.Count;
        Assert.InRange(publisher.Published.Count, 1, expectedWithoutDegradation - 1);
    }

    [Fact]
    public async Task ScenarioC_WithMissingReadings_PublishesFewerThanExpected()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 5;
        options.IntervalSeconds = 1;
        options.FailureRate = 1.0;
        options.DegradationProfile = "missing-readings";
        options.Sensors =
        [
            SimulatorOptionsMother.CreateSensorDefinition(name: "Temperature-01", type: SensorType.Temperature),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Temperature-02", type: SensorType.Temperature),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Humidity-01", type: SensorType.Humidity),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Humidity-02", type: SensorType.Humidity),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Wind-01", type: SensorType.Wind),
            SimulatorOptionsMother.CreateSensorDefinition(name: "Wind-02", type: SensorType.Wind)
        ];
        var publisher = new CollectingReadingPublisher();
        var runner = CreateRunner(options, publisher);

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        Assert.InRange(publisher.Published.Count, 1, 29);
        Assert.All(publisher.Published, envelope =>
            Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState));
    }

    [Fact]
    public async Task ScenarioC_WithMultipleProfiles_StillUsesMissingReadingsForAcceptedCount()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 5;
        options.IntervalSeconds = 1;
        var context = CreateContextWithDegradation(
            options,
            [SimulationDegradationProfiles.MissingReadings, SimulationDegradationProfiles.Noise]);
        var publisher = new CollectingReadingPublisher();
        var runner = new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new StaticSimulationContextSource(context),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: new NoOpSimulationRunStore(),
            readingPublisher: publisher,
            processExitCode: new NoOpSimulatorProcessExitCode(),
            applicationLifetime: new NoOpApplicationLifetime());

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        var expectedWithoutDegradation = context.NumberOfCycles * context.Sensors.Count;
        Assert.InRange(publisher.Published.Count, 1, expectedWithoutDegradation - 1);
        Assert.Equal(
            new[] { SimulationDegradationProfiles.MissingReadings, SimulationDegradationProfiles.Noise },
            context.RunOverrides!.Resolved.DegradationProfiles);
    }


    [Fact]
    public async Task ExecuteAsync_RunStartLogIncludesEffectiveScenarioAndDegradationContext()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 1;
        options.IntervalSeconds = 1;
        var context = CreateContextWithDegradation(options, "missing-readings");
        var logger = new CapturingLogger<SimulationRunner>();
        var runner = new SimulationRunner(
            logger: logger,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new StaticSimulationContextSource(context),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: new NoOpSimulationRunStore(),
            readingPublisher: new CollectingReadingPublisher(),
            processExitCode: new NoOpSimulatorProcessExitCode(),
            applicationLifetime: new NoOpApplicationLifetime());

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        var runStartLog = Assert.Single(
            logger.Messages,
            message => message.Contains("Simulation run started", StringComparison.Ordinal));
        Assert.Contains("SimulationRunId=", runStartLog);
        Assert.Contains("ScenarioId=", runStartLog);
        Assert.Contains("ScenarioName=Preferred seed scenario", runStartLog);
        Assert.Contains("DegradationProfile=missing-readings", runStartLog);
        Assert.Contains("FailureRate=0", runStartLog);
        Assert.Contains("NoiseLevel=0", runStartLog);
        Assert.Contains("SensorLimit=1", runStartLog);
    }

    [Fact]
    public async Task ExecuteAsync_UsesLogicalEventTimesAcrossCycles()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(name: "OnlySensor")];
        options.NumberOfCycles = 3;
        options.IntervalSeconds = 2;
        options.StartTimestamp = new DateTimeOffset(2026, 4, 6, 16, 0, 0, TimeSpan.Zero);
        var publisher = new CollectingReadingPublisher();
        var runner = CreateRunner(options, publisher);

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        Assert.Equal(
            new[]
            {
                options.StartTimestamp.Value,
                options.StartTimestamp.Value.AddSeconds(2),
                options.StartTimestamp.Value.AddSeconds(4)
            },
            publisher.Published.Select(x => x.EventTime));
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefully_WhenCancelledDuringDelay()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(name: "OnlySensor")];
        options.NumberOfCycles = 5;
        options.IntervalSeconds = 1;
        using var cts = new CancellationTokenSource();
        var publishCount = 0;
        var publisher = new CollectingReadingPublisher(_ =>
        {
            publishCount++;
            if (publishCount == 1)
            {
                cts.Cancel();
            }
        });
        var runner = CreateRunner(options, publisher);

        await SimulationRunnerInvoker.ExecuteAsync(runner, cts.Token);

        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task ExecuteAsync_StoresActualRunLifecycle_TimestampsSeparatelyFromLogicalEventTimes()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(name: "OnlySensor")];
        options.NumberOfCycles = 1;
        options.IntervalSeconds = 1;
        options.StartTimestamp = new DateTimeOffset(2030, 1, 15, 8, 30, 0, TimeSpan.Zero);
        var publisher = new CollectingReadingPublisher();
        var runStore = new RecordingSimulationRunStore();
        var runner = CreateRunner(options, publisher, runStore);
        var logicalCompletedAt = options.StartTimestamp.Value.AddSeconds(options.IntervalSeconds * options.NumberOfCycles);
        var before = DateTimeOffset.UtcNow;

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        var after = DateTimeOffset.UtcNow;

        Assert.Equal(
            new[]
            {
                SimulationRunStatus.Ready,
                SimulationRunStatus.Running,
                SimulationRunStatus.Completed
            },
            runStore.Upserts.Select(x => x.Status));

        var running = runStore.Upserts[1];
        var completed = runStore.Upserts[2];

        Assert.NotNull(running.StartedAt);
        Assert.InRange(running.StartedAt!.Value, before, after);
        Assert.Equal(options.StartTimestamp, running.LogicalStartTimestamp);
        Assert.NotEqual(options.StartTimestamp, running.StartedAt);

        Assert.NotNull(completed.EndedAt);
        Assert.InRange(completed.EndedAt!.Value, before, after);
        Assert.Equal(options.StartTimestamp, completed.LogicalStartTimestamp);
        Assert.NotEqual(logicalCompletedAt, completed.EndedAt);
        Assert.True(completed.EndedAt >= completed.StartedAt);
    }

    [Fact]
    public async Task ExecuteAsync_PublisherThrows_MarksRunFailedAndPropagatesException()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Sensors = [SimulatorOptionsMother.CreateSensorDefinition(name: "OnlySensor")];
        options.NumberOfCycles = 1;
        var runStore = new RecordingSimulationRunStore();
        var publisher = new ThrowingReadingPublisher();
        var processExitCode = new RecordingSimulatorProcessExitCode();
        var runner = CreateRunner(options, publisher, runStore, processExitCode);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None));

        Assert.Equal("Simulated publisher failure.", exception.Message);
        Assert.Equal(
            new[]
            {
                SimulationRunStatus.Ready,
                SimulationRunStatus.Running,
                SimulationRunStatus.Failed
            },
            runStore.Upserts.Select(x => x.Status));
        Assert.NotNull(runStore.Upserts[2].EndedAt);
        Assert.True(processExitCode.FailureMarked);
    }

    [Fact]
    public async Task ExecuteAsync_PreferredSeedPresent_UsesPreferredSeedForCreatedRun()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.Seed = 111;
        var runStore = new RecordingSimulationRunStore();
        var context = CreateContextWithPreferredSeed(222);
        var runner = new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new StaticSimulationContextSource(context),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: runStore,
            readingPublisher: new CollectingReadingPublisher(),
            processExitCode: new NoOpSimulatorProcessExitCode(),
            applicationLifetime: new NoOpApplicationLifetime());

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        Assert.All(runStore.Upserts, upsert => Assert.Equal(222, upsert.ExecutionSeed));
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenRunCompletes_StopsApplication()
    {
        var options = SimulatorOptionsMother.CreateValid();
        options.NumberOfCycles = 1;
        options.IntervalSeconds = 1;

        var applicationLifetime = new RecordingApplicationLifetime();
        var processExitCode = new RecordingSimulatorProcessExitCode();

        var runner = new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new ScenarioContextFactory(Options.Create(options)),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: new NoOpSimulationRunStore(),
            readingPublisher: new CollectingReadingPublisher(),
            processExitCode: processExitCode,
            applicationLifetime: applicationLifetime);

        await SimulationRunnerInvoker.ExecuteAsync(runner, CancellationToken.None);

        Assert.True(applicationLifetime.StopApplicationCalled);
        Assert.False(processExitCode.FailureMarked);
    }

    private static SimulationRunner CreateRunner(
        NatureProtector.Simulator.Host.Configuration.SimulatorOptions options,
        IReadingPublisher publisher,
        ISimulationRunStore? simulationRunStore = null,
        ISimulatorProcessExitCode? processExitCode = null)
    {
        return new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new ScenarioContextFactory(Options.Create(options)),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: simulationRunStore ?? new NoOpSimulationRunStore(),
            readingPublisher: publisher,
            processExitCode: processExitCode ?? new NoOpSimulatorProcessExitCode(),
            applicationLifetime: new NoOpApplicationLifetime());
    }

    private static SimulationContext CreateContextWithPreferredSeed(int preferredSeed)
    {
        var sensor = new Sensor(
            id: Guid.NewGuid(),
            name: "PreferredSeedSensor",
            type: SensorType.Temperature,
            location: new Location(39.8, -7.9),
            profile: new SensorProfile(
                id: Guid.NewGuid(),
                samplingInterval: TimeSpan.FromSeconds(5),
                communicationMode: "Test",
                noiseLevel: 0.0,
                latencyProfile: "None",
                failureProfile: "None"));

        var scenario = new Scenario(
            id: Guid.NewGuid(),
            name: "Preferred seed scenario",
            category: ScenarioCategory.HighRisk,
            parameters: new ScenarioParameters(
                baseTemperature: 30.0,
                baseHumidity: 40.0,
                baseWindSpeed: 5.0,
                failureRate: 0.0,
                noiseLevel: 0.0,
                timeAcceleration: 1.0));

        return new SimulationContext(
            areaId: Guid.NewGuid(),
            scenario: scenario,
            sensors: [sensor],
            startTimestamp: new DateTimeOffset(2026, 4, 6, 18, 0, 0, TimeSpan.Zero),
            interval: TimeSpan.FromSeconds(1),
            numberOfCycles: 1,
            preferredSeed: preferredSeed);
    }

    private static SimulationContext CreateContextWithDegradation(
        NatureProtector.Simulator.Host.Configuration.SimulatorOptions options,
        string degradationProfile)
        => CreateContextWithDegradation(
            options,
            SimulationDegradationProfiles.Normalize(null, degradationProfile));

    private static SimulationContext CreateContextWithDegradation(
        NatureProtector.Simulator.Host.Configuration.SimulatorOptions options,
        IReadOnlyList<string> degradationProfiles)
    {
        var context = CreateContextWithPreferredSeed(options.Seed ?? 12345);
        var degradationProfile = SimulationDegradationProfiles.ToLegacyProfile(degradationProfiles);
        return new SimulationContext(
            areaId: context.AreaId,
            scenario: context.Scenario,
            sensors: context.Sensors,
            startTimestamp: context.StartTimestamp,
            interval: TimeSpan.FromSeconds(options.IntervalSeconds),
            numberOfCycles: options.NumberOfCycles,
            preferredSeed: options.Seed,
            runOverrides: new SimulationRunOverridesSnapshot(
                new SimulationRunOverridesRequested(null, options.NumberOfCycles, options.IntervalSeconds, options.Seed, degradationProfile, "tests")
                {
                    DegradationProfiles = degradationProfiles
                },
                new SimulationRunOverridesResolved(context.Sensors.Count, options.NumberOfCycles, options.IntervalSeconds, options.Seed, degradationProfile, "tests", context.Sensors.Select(sensor => sensor.Name).ToArray())
                {
                    DegradationProfiles = degradationProfiles
                }));
    }

    private sealed class RecordingSimulationRunStore : ISimulationRunStore
    {
        public List<RecordedRun> Upserts { get; } = [];

        public Task UpsertAsync(
            SimulationContext context,
            SimulationRun run,
            CancellationToken cancellationToken)
        {
            Upserts.Add(new RecordedRun(
                run.Status,
                run.StartedAt,
                run.EndedAt,
                context.StartTimestamp,
                run.ExecutionSeed));

            return Task.CompletedTask;
        }
    }

    private sealed record RecordedRun(
        SimulationRunStatus Status,
        DateTimeOffset? StartedAt,
        DateTimeOffset? EndedAt,
        DateTimeOffset LogicalStartTimestamp,
        int? ExecutionSeed);

    private sealed class ThrowingReadingPublisher : IReadingPublisher
    {
        public Task PublishAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated publisher failure.");
        }
    }

    private sealed class StaticSimulationContextSource(SimulationContext context) : ISimulationContextSource
    {
        public Task<SimulationContext> CreateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(context);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoOpScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NoOpScope : IDisposable
        {
            public static NoOpScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
    
    private sealed class NoOpSimulatorProcessExitCode : ISimulatorProcessExitCode
    {
        public void MarkFailure()
        {
        }
    }

    private sealed class RecordingSimulatorProcessExitCode : ISimulatorProcessExitCode
    {
        public bool FailureMarked { get; private set; }

        public void MarkFailure()
        {
            FailureMarked = true;
        }
    }

    private sealed class NoOpApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
    
    private sealed class RecordingApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopApplicationCalled { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            StopApplicationCalled = true;
        }
    }
}
