using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Scenarios;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.Tests.Fakes;
using NatureProtector.Simulator.Host.Tests.Helpers;
using NatureProtector.Simulator.Host.Tests.TestData;

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

    private static SimulationRunner CreateRunner(
        NatureProtector.Simulator.Host.Configuration.SimulatorOptions options,
        CollectingReadingPublisher publisher,
        ISimulationRunStore? simulationRunStore = null)
    {
        return new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            simulationContextSource: new ScenarioContextFactory(Options.Create(options)),
            readingGenerationService: new ReadingGenerationService(),
            simulationRunStore: simulationRunStore ?? new NoOpSimulationRunStore(),
            readingPublisher: publisher);
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
                context.StartTimestamp));

            return Task.CompletedTask;
        }
    }

    private sealed record RecordedRun(
        SimulationRunStatus Status,
        DateTimeOffset? StartedAt,
        DateTimeOffset? EndedAt,
        DateTimeOffset LogicalStartTimestamp);
}
