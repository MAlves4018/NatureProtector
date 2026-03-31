using Microsoft.Extensions.Options;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Publishing;

/*
 * This hosted service orchestrates the full simulation execution lifecycle.
 *
 * Rationale:
 * - The runner owns the simulation loop, timing, seed resolution, context
 *   creation, reading generation and publication.
 * - This replaces the previous all-in-one worker with a cleaner orchestration
 *   layer that delegates specialized work to dedicated services.
 *
 * Design considerations:
 * - A single seed is resolved once per execution to guarantee deterministic
 *   pseudo-random behaviour.
 * - A Scenario-derived SimulationRun is created and its lifecycle is updated
 *   in the correct temporal order.
 * - The loop publishes one batch per cycle and waits for the configured interval
 *   between cycles.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class SimulationRunner(
    ILogger<SimulationRunner> logger,
    IOptions<SimulatorOptions> simulatorOptions,
    SeedProvider seedProvider,
    ScenarioContextFactory scenarioContextFactory,
    ReadingGenerationService readingGenerationService,
    IReadingPublisher readingPublisher) : BackgroundService
{
    private readonly SimulatorOptions _options = simulatorOptions.Value;

    /// <summary>
    /// Executes the simulation loop until the configured number of cycles is
    /// completed or the host is cancelled.
    /// </summary>
    /// <param name="stoppingToken">
    /// Cancellation token triggered during host shutdown.
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Simulation runner starting at {Time}.",
            DateTimeOffset.UtcNow);

        var context = scenarioContextFactory.Create();
        var seed = seedProvider.ResolveSeed(_options.Seed);
        var random = seedProvider.CreateRandom(seed);

        logger.LogInformation(
            "Simulation context created | AreaId={AreaId} | ScenarioId={ScenarioId} | ScenarioName={ScenarioName} | Seed={Seed} | Cycles={Cycles} | Interval={IntervalSeconds}s",
            context.AreaId,
            context.Scenario.Id,
            context.Scenario.Name,
            seed,
            context.NumberOfCycles,
            context.Interval.TotalSeconds);

        var run = context.Scenario.CreateRun(seed);
        run.Start(context.StartTimestamp);

        try
        {
            for (var cycleIndex = 0;
                 cycleIndex < context.NumberOfCycles && !stoppingToken.IsCancellationRequested;
                 cycleIndex++)
            {
                var eventTime = context.StartTimestamp + TimeSpan.FromTicks(
                    context.Interval.Ticks * cycleIndex);

                logger.LogInformation(
                    "Starting simulation cycle {CycleNumber}/{TotalCycles} at logical time {EventTime}.",
                    cycleIndex + 1,
                    context.NumberOfCycles,
                    eventTime);

                var envelopes = readingGenerationService.GenerateBatch(
                    context,
                    run.Id,
                    cycleIndex,
                    eventTime,
                    random);

                foreach (var envelope in envelopes)
                {
                    await readingPublisher.PublishAsync(envelope, stoppingToken);
                }

                if (cycleIndex < context.NumberOfCycles - 1)
                {
                    await Task.Delay(context.Interval, stoppingToken);
                }
            }

            var completedAt = context.StartTimestamp + TimeSpan.FromTicks(
                context.Interval.Ticks * context.NumberOfCycles);

            run.Complete(completedAt);

            logger.LogInformation(
                "Simulation completed successfully | SimulationRunId={SimulationRunId} | CompletedAt={CompletedAt}.",
                run.Id,
                completedAt);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Simulation runner cancellation requested. Execution is stopping gracefully.");
        }
    }
}