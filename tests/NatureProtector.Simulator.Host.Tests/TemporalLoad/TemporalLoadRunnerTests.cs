using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.TemporalLoad;
using NatureProtector.Simulator.Host.Tests.Fakes;

namespace NatureProtector.Simulator.Host.Tests.TemporalLoad;

public sealed class TemporalLoadRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesUniqueRealContractEventsAndWritesAccounting()
    {
        using var scope = TemporaryDirectory.Create();
        var workloadPath = WriteWorkload(scope.Path, requestedRate: 20, durationSeconds: 0.2);
        var publisher = new CollectingReadingPublisher();
        var runStore = new RecordingSimulationRunStore();
        var runner = CreateRunner(scope.Path, workloadPath, publisher, runStore: runStore);

        await InvokeExecuteAsync(runner, CancellationToken.None);

        Assert.Equal(4, publisher.Published.Count);
        Assert.Equal(publisher.Published.Count, publisher.Published.Select(item => item.EventId).Distinct().Count());
        Assert.All(publisher.Published, envelope =>
        {
            Assert.Equal(EventTypes.SensorReadingProduced, envelope.EventType);
            Assert.NotEqual(Guid.Empty, envelope.Payload.SimulationRunId);
            Assert.NotNull(envelope.Payload.CycleIndex);
            Assert.Equal(SensorOperationalState.Nominal, envelope.Payload.OperationalState);
        });
        Assert.Equal(
            [SimulationRunStatus.Ready, SimulationRunStatus.Running, SimulationRunStatus.Completed],
            runStore.Upserts.ToArray());
        Assert.Single(Directory.GetFiles(scope.Path, "events.csv", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(scope.Path, "summary.json", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(scope.Path, "receipt.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ExecuteAsync_UsesDeterministicSeedForGeneratedValues()
    {
        using var first = TemporaryDirectory.Create();
        using var second = TemporaryDirectory.Create();
        var workloadPath = WriteWorkload(first.Path, requestedRate: 10, durationSeconds: 0.2);
        var firstPublisher = new CollectingReadingPublisher();
        var secondPublisher = new CollectingReadingPublisher();

        await InvokeExecuteAsync(CreateRunner(first.Path, workloadPath, firstPublisher), CancellationToken.None);
        await InvokeExecuteAsync(CreateRunner(second.Path, workloadPath, secondPublisher), CancellationToken.None);

        Assert.Equal(
            firstPublisher.Published.Select(item => item.Payload.Value).ToArray(),
            secondPublisher.Published.Select(item => item.Payload.Value).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_PublisherFailureMarksRunFailed()
    {
        using var scope = TemporaryDirectory.Create();
        var workloadPath = WriteWorkload(scope.Path, requestedRate: 1, durationSeconds: 1);
        var runStore = new RecordingSimulationRunStore();
        var processExitCode = new RecordingSimulatorProcessExitCode();
        var runner = CreateRunner(
            scope.Path,
            workloadPath,
            new ThrowingReadingPublisher(),
            runStore,
            processExitCode);

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeExecuteAsync(runner, CancellationToken.None));

        Assert.True(processExitCode.FailureMarked);
        Assert.Equal(SimulationRunStatus.Failed, runStore.Upserts.Last());
    }

    [Fact]
    public async Task ExecuteAsync_RequireNominalEventsFailsWhenNominalReadingCannotBeGenerated()
    {
        using var scope = TemporaryDirectory.Create();
        var workloadPath = WriteWorkload(scope.Path, requestedRate: 1, durationSeconds: 1);
        var publisher = new CollectingReadingPublisher();
        var runner = CreateRunner(
            scope.Path,
            workloadPath,
            publisher,
            contextFailureRate: 1.0,
            maxNominalGenerationAttempts: 3);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeExecuteAsync(runner, CancellationToken.None));

        Assert.Contains("could not generate a nominal reading", exception.Message, StringComparison.Ordinal);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationWritesCancelledReceipt()
    {
        using var scope = TemporaryDirectory.Create();
        var workloadPath = WriteWorkload(scope.Path, requestedRate: 1, durationSeconds: 5);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var runStore = new RecordingSimulationRunStore();
        var runner = CreateRunner(scope.Path, workloadPath, new CollectingReadingPublisher(), runStore: runStore);

        await Assert.ThrowsAsync<TaskCanceledException>(() => InvokeExecuteAsync(runner, cancellation.Token));

        Assert.Equal(SimulationRunStatus.Cancelled, runStore.Upserts.Last());
        var receipt = File.ReadAllText(Directory.GetFiles(scope.Path, "receipt.json", SearchOption.AllDirectories).Single());
        Assert.Contains("\"status\": \"FAIL\"", receipt, StringComparison.Ordinal);
    }

    private static TemporalLoadRunner CreateRunner(
        string outputRoot,
        string workloadPath,
        IReadingPublisher publisher,
        ISimulationRunStore? runStore = null,
        ISimulatorProcessExitCode? processExitCode = null,
        double contextFailureRate = 0.0,
        int maxNominalGenerationAttempts = 100)
    {
        return new TemporalLoadRunner(
            NullLogger<TemporalLoadRunner>.Instance,
            Options.Create(new TemporalLoadOptions
            {
                Enabled = true,
                WorkloadPath = workloadPath,
                WorkloadId = "test",
                OutputRoot = outputRoot,
                RunLabel = "unit",
                Topology = "fixed-one",
                Repetition = 1,
                Seed = 123,
                MaxNominalGenerationAttempts = maxNominalGenerationAttempts,
                PublisherTimeoutSeconds = 10
            }),
            new SeedProvider(),
            new StaticSimulationContextSource(contextFailureRate),
            new ReadingGenerationService(),
            runStore ?? new RecordingSimulationRunStore(),
            publisher,
            processExitCode ?? new RecordingSimulatorProcessExitCode(),
            new NoOpApplicationLifetime());
    }

    private static string WriteWorkload(string root, double requestedRate, double durationSeconds)
    {
        var path = Path.Combine(root, "workloads.json");
        var catalog = new TemporalWorkloadCatalog
        {
            Workloads =
            [
                new TemporalWorkloadDefinition
                {
                    Id = "test",
                    Description = "unit test workload",
                    DrainTimeoutSeconds = 5,
                    Seed = 123,
                    Segments =
                    [
                        new TemporalWorkloadSegment
                        {
                            Id = "steady",
                            Kind = "constant",
                            DurationSeconds = durationSeconds,
                            RequestedRate = requestedRate
                        }
                    ]
                }
            ]
        };
        TemporalWorkloadLoader.WriteJson(path, catalog);
        return path;
    }

    private static Task InvokeExecuteAsync(TemporalLoadRunner runner, CancellationToken cancellationToken)
    {
        var method = typeof(TemporalLoadRunner).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (Task)method!.Invoke(runner, [cancellationToken])!;
    }

    private sealed class StaticSimulationContextSource : ISimulationContextSource
    {
        private readonly double _failureRate;

        public StaticSimulationContextSource(double failureRate = 0.0)
        {
            _failureRate = failureRate;
        }

        public Task<SimulationContext> CreateAsync(CancellationToken cancellationToken)
        {
            var scenario = new Scenario(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Temporal Test",
                ScenarioCategory.HighRisk,
                new ScenarioParameters(
                    baseTemperature: 31,
                    baseHumidity: 35,
                    baseWindSpeed: 7,
                    failureRate: _failureRate,
                    noiseLevel: 0.1,
                    timeAcceleration: 1));
            var sensors = new[]
            {
                new Sensor(
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    "Temperature-01",
                    SensorType.Temperature,
                    new Location(39.8, -7.9, 100, "cell-1"),
                    new SensorProfile(Guid.NewGuid(), TimeSpan.FromSeconds(5), "RabbitMq", 0.1, "low", "rare")),
                new Sensor(
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    "Humidity-01",
                    SensorType.Humidity,
                    new Location(39.81, -7.91, 100, "cell-2"),
                    new SensorProfile(Guid.NewGuid(), TimeSpan.FromSeconds(5), "RabbitMq", 0.1, "low", "rare"))
            };
            return Task.FromResult(new SimulationContext(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                scenario,
                sensors,
                new DateTimeOffset(2026, 7, 27, 8, 0, 0, TimeSpan.Zero),
                TimeSpan.FromSeconds(1),
                1,
                scenarioCode: "scenario_b",
                preferredSeed: 123));
        }
    }

    private sealed class RecordingSimulationRunStore : ISimulationRunStore
    {
        public List<SimulationRunStatus> Upserts { get; } = [];

        public Task UpsertAsync(
            SimulationContext context,
            SimulationRun run,
            CancellationToken cancellationToken)
        {
            Upserts.Add(run.Status);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingReadingPublisher : IReadingPublisher
    {
        public Task PublishAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("publisher failed");
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(path);
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "np-temporal-" + Guid.NewGuid().ToString("N")));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
