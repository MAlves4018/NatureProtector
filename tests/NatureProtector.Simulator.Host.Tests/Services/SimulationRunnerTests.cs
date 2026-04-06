using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

    private static SimulationRunner CreateRunner(
        NatureProtector.Simulator.Host.Configuration.SimulatorOptions options,
        CollectingReadingPublisher publisher)
    {
        return new SimulationRunner(
            logger: NullLogger<SimulationRunner>.Instance,
            simulatorOptions: Options.Create(options),
            seedProvider: new SeedProvider(),
            scenarioContextFactory: new ScenarioContextFactory(Options.Create(options)),
            readingGenerationService: new ReadingGenerationService(),
            readingPublisher: publisher);
    }
}
