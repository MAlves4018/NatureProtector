using Microsoft.Extensions.Options;
using NatureProtector.Core.Scenarios;
using NatureProtector.Shared.Observability;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Publishing;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/*
 * Este hosted service orquestra o ciclo de vida completo de uma execução de
 * simulação.
 *
 * Rationale:
 * - O runner controla o loop principal, a temporização, a seed, a resolução do
 *   contexto, a geração das leituras e a publicação.
 * - Esta camada substitui a antiga abordagem monolítica por uma orquestração
 *   mais clara, apoiada em serviços especializados.
 *
 * Design considerations:
 * - A seed é resolvida uma única vez por execução para garantir comportamento
 *   determinístico.
 * - O objeto SimulationRun é atualizado pela ordem temporal correta para
 *   manter o estado observável no control plane.
 * - O loop publica um lote por ciclo e espera o intervalo lógico configurado
 *   entre ciclos.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class SimulationRunner(
    ILogger<SimulationRunner> logger,
    IOptions<SimulatorOptions> simulatorOptions,
    SeedProvider seedProvider,
    ISimulationContextSource simulationContextSource,
    ReadingGenerationService readingGenerationService,
    ISimulationRunStore simulationRunStore,
    IReadingPublisher readingPublisher) : BackgroundService
{
    private readonly SimulatorOptions _options = simulatorOptions.Value;

    /// <summary>
    /// Executa o loop de simulação até terminar o número configurado de ciclos
    /// ou até o host ser cancelado.
    /// </summary>
    /// <param name="stoppingToken">
    /// Token de cancelamento disparado durante o encerramento do host.
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var runActivity = SimulatorHostTelemetry.ActivitySource.StartActivity("natureprotector.simulator.run");
        var runStopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Simulation runner starting at {Time}.",
            DateTimeOffset.UtcNow);

        var context = await simulationContextSource.CreateAsync(stoppingToken);
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
        runActivity?.SetTag(TelemetryTags.AreaId, context.AreaId);
        runActivity?.SetTag(TelemetryTags.ScenarioId, context.Scenario.Id);
        runActivity?.SetTag(TelemetryTags.ScenarioCode, context.ScenarioCode);

        // O registo é persistido logo em Ready e depois em Running para tornar
        // explícita a transição de estado observável da execução.
        var run = context.Scenario.CreateRun(seed);
        run.MarkReady();
        await simulationRunStore.UpsertAsync(context, run, stoppingToken);
        var actualStartedAt = DateTimeOffset.UtcNow;
        run.Start(actualStartedAt);
        await simulationRunStore.UpsertAsync(context, run, stoppingToken);
        SimulatorHostTelemetry.SimulationRuns.Add(1, new TagList { { TelemetryTags.Outcome, "started" } });
        runActivity?.SetTag(TelemetryTags.SimulationRunId, run.Id);

        logger.LogInformation(
            "Simulation run started | SimulationRunId={SimulationRunId} | StartedAt={StartedAt} | LogicalStartTimestamp={LogicalStartTimestamp}",
            run.Id,
            actualStartedAt,
            context.StartTimestamp);

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

                using var cycleActivity = SimulatorHostTelemetry.ActivitySource.StartActivity("natureprotector.simulator.cycle");
                cycleActivity?.SetTag(TelemetryTags.SimulationRunId, run.Id);
                cycleActivity?.SetTag(TelemetryTags.AreaId, context.AreaId);
                cycleActivity?.SetTag(TelemetryTags.ScenarioId, context.Scenario.Id);
                var envelopes = readingGenerationService.GenerateBatch(
                    context,
                    run.Id,
                    cycleIndex,
                    eventTime,
                    random);
                var publishStopwatch = Stopwatch.StartNew();
                SimulatorHostTelemetry.PublishBatchSize.Record(envelopes.Count);

                foreach (var envelope in envelopes)
                {
                    await readingPublisher.PublishAsync(envelope, stoppingToken);
                }

                publishStopwatch.Stop();
                SimulatorHostTelemetry.PublishDurationMs.Record(publishStopwatch.Elapsed.TotalMilliseconds);

                if (cycleIndex < context.NumberOfCycles - 1)
                {
                    await Task.Delay(context.Interval, stoppingToken);
                }
            }

            var logicalCompletedAt = context.StartTimestamp + TimeSpan.FromTicks(
                context.Interval.Ticks * context.NumberOfCycles);
            var actualCompletedAt = DateTimeOffset.UtcNow;

            run.Complete(actualCompletedAt);
            await simulationRunStore.UpsertAsync(context, run, stoppingToken);
            runStopwatch.Stop();
            runActivity?.SetTag(TelemetryTags.Outcome, "completed");
            SimulatorHostTelemetry.SimulationRuns.Add(1, new TagList { { TelemetryTags.Outcome, "completed" } });
            SimulatorHostTelemetry.SimulationRunDurationMs.Record(runStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "completed" } });

            logger.LogInformation(
                "Simulation completed successfully | SimulationRunId={SimulationRunId} | CompletedAt={CompletedAt} | LogicalCompletedAt={LogicalCompletedAt}.",
                run.Id,
                actualCompletedAt,
                logicalCompletedAt);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (run.Status is SimulationRunStatus.Ready or SimulationRunStatus.Running)
            {
                run.Cancel(DateTimeOffset.UtcNow);
                await simulationRunStore.UpsertAsync(context, run, CancellationToken.None);
            }

            runStopwatch.Stop();
            runActivity?.SetTag(TelemetryTags.Outcome, "cancelled");
            SimulatorHostTelemetry.SimulationRuns.Add(1, new TagList { { TelemetryTags.Outcome, "cancelled" } });
            SimulatorHostTelemetry.SimulationRunDurationMs.Record(runStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "cancelled" } });

            logger.LogInformation(
                "Simulation runner cancellation requested. Execution is stopping gracefully.");
        }
        catch
        {
            if (run.Status is SimulationRunStatus.Running)
            {
                run.Fail(DateTimeOffset.UtcNow);
                await simulationRunStore.UpsertAsync(context, run, CancellationToken.None);
            }

            runStopwatch.Stop();
            runActivity?.SetTag(TelemetryTags.Outcome, "failed");
            SimulatorHostTelemetry.SimulationRuns.Add(1, new TagList { { TelemetryTags.Outcome, "failed" } });
            SimulatorHostTelemetry.SimulationRunDurationMs.Record(runStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "failed" } });

            throw;
        }
    }
}
