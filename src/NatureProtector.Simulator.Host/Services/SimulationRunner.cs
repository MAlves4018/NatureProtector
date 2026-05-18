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
        var seed = seedProvider.ResolveSeed(context.PreferredSeed ?? _options.Seed);
        var random = seedProvider.CreateRandom(seed);

        logger.LogInformation(
            "Simulation context created | AreaId={AreaId} | ScenarioId={ScenarioId} | ScenarioCode={ScenarioCode} | ScenarioName={ScenarioName} | Seed={Seed} | Cycles={Cycles} | Interval={IntervalSeconds}s | DegradationProfile={DegradationProfile}",
            context.AreaId,
            context.Scenario.Id,
            context.ScenarioCode,
            context.Scenario.Name,
            seed,
            context.NumberOfCycles,
            context.Interval.TotalSeconds,
            context.RunOverrides?.Resolved.DegradationProfile);
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
            "Simulation run started | SimulationRunId={SimulationRunId} | ScenarioId={ScenarioId} | ScenarioCode={ScenarioCode} | ScenarioName={ScenarioName} | DegradationProfile={DegradationProfile} | FailureRate={FailureRate} | NoiseLevel={NoiseLevel} | SensorLimit={SensorLimit} | StartedAt={StartedAt} | LogicalStartTimestamp={LogicalStartTimestamp}",
            run.Id,
            context.Scenario.Id,
            context.ScenarioCode,
            context.Scenario.Name,
            context.RunOverrides?.Resolved.DegradationProfile,
            context.Scenario.Parameters.FailureRate,
            context.Scenario.Parameters.NoiseLevel,
            context.RunOverrides?.Resolved.SensorCount ?? context.Sensors.Count,
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
                var publishableEnvelopes = ApplyOperationalDegradation(
                    context,
                    envelopes,
                    cycleIndex);
                var publishStopwatch = Stopwatch.StartNew();
                SimulatorHostTelemetry.PublishBatchSize.Record(publishableEnvelopes.Count);

                if (publishableEnvelopes.Count != envelopes.Count)
                {
                    logger.LogInformation(
                        "Operational degradation omitted {MissingCount} reading(s) in cycle {CycleNumber}. Profile={DegradationProfile}",
                        envelopes.Count - publishableEnvelopes.Count,
                        cycleIndex + 1,
                        context.RunOverrides?.Resolved.DegradationProfile);
                }

                foreach (var envelope in publishableEnvelopes)
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

    private static IReadOnlyCollection<Shared.Messaging.EventEnvelope<Shared.Contracts.Readings.SensorReadingProducedPayload>> ApplyOperationalDegradation(
        SimulationContext context,
        IReadOnlyCollection<Shared.Messaging.EventEnvelope<Shared.Contracts.Readings.SensorReadingProducedPayload>> envelopes,
        int cycleIndex)
    {
        var profile = context.RunOverrides?.Resolved.DegradationProfile;
        if (string.IsNullOrWhiteSpace(profile) ||
            string.Equals(profile, "none", StringComparison.OrdinalIgnoreCase))
        {
            return envelopes;
        }

        if (!string.Equals(profile, "missing-readings", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(profile, "deterministic-missing-readings", StringComparison.OrdinalIgnoreCase))
        {
            return envelopes;
        }

        return envelopes
            .Where(envelope => !ShouldOmitReading(envelope.Payload.SensorId, cycleIndex))
            .ToArray();
    }

    private static bool ShouldOmitReading(Guid sensorId, int cycleIndex)
    {
        var bytes = sensorId.ToByteArray();
        var accumulator = cycleIndex * 17;
        for (var index = 0; index < bytes.Length; index++)
        {
            accumulator += bytes[index] * (index + 3);
        }

        return accumulator % 5 == 0;
    }
}
