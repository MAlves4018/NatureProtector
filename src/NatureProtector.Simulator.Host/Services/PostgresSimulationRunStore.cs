using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Persistence;

/*
 * Este store persiste no control plane o ciclo de vida de cada execução do
 * simulador.
 *
 * Rationale:
 * - As execuções precisam de ficar observáveis e consultáveis pela API de
 *   backoffice.
 * - O simulador não deve conhecer detalhes de mapeamento relacional nem do
 *   esquema PostgreSQL.
 *
 * Design considerations:
 * - O registo só é persistido quando o contexto está associado a uma versão de
 *   configuração real.
 * - O método é idempotente por SimulationRunId para simplificar atualizações de
 *   estado ao longo da execução.
 */

namespace NatureProtector.Simulator.Host.Services;

public sealed class PostgresSimulationRunStore(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory) : ISimulationRunStore
{
    /// <summary>
    /// Cria ou atualiza o registo persistido de uma execução de simulação.
    /// </summary>
    public async Task UpsertAsync(SimulationContext context, SimulationRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(run);

        if (!context.ConfigurationVersionId.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot persist simulation run {run.Id} without a configuration version. " +
                "Ensure the context resolves a valid ConfigurationVersionId.");
        }
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await dbContext.SimulationRuns
            .SingleOrDefaultAsync(entity => entity.Id == run.Id, cancellationToken);

        if (record is null)
        {
            record = new SimulationRunRecord
            {
                Id = run.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.SimulationRuns.Add(record);
        }

        record.AreaId = context.AreaId;
        record.ScenarioId = context.Scenario.Id;
        record.ConfigurationVersionId = context.ConfigurationVersionId.Value;
        record.ScenarioCode = context.ScenarioCode ?? context.Scenario.Id.ToString("D");
        record.ScenarioName = context.Scenario.Name;
        record.StartedAt = NormalizeUtc(run.StartedAt);
        record.EndedAt = NormalizeUtc(run.EndedAt);
        record.LogicalStartTimestamp = NormalizeUtc(context.StartTimestamp);
        record.IntervalSeconds = (int)context.Interval.TotalSeconds;
        record.NumberOfCycles = context.NumberOfCycles;
        record.ExecutionSeed = run.ExecutionSeed;
        record.Status = run.Status;
        record.MetadataJson = JsonSerializer.Serialize(new
        {
            sensor_count = context.Sensors.Count,
            scenario_category = context.Scenario.Category.ToString(),
            orchestrator_correlation_id = context.RunOverrides?.Resolved.OrchestratorCorrelationId,
            run_overrides = context.RunOverrides is null
                ? null
                : new
                {
                    requested = new
                    {
                        sensor_count = context.RunOverrides.Requested.SensorCount,
                        number_of_cycles = context.RunOverrides.Requested.NumberOfCycles,
                        interval_seconds = context.RunOverrides.Requested.IntervalSeconds,
                        seed = context.RunOverrides.Requested.Seed,
                        degradation_profile = context.RunOverrides.Requested.DegradationProfile,
                        degradation_profiles = context.RunOverrides.Requested.DegradationProfiles,
                        orchestrator_correlation_id = context.RunOverrides.Requested.OrchestratorCorrelationId
                    },
                    resolved = new
                    {
                        sensor_count = context.RunOverrides.Resolved.SensorCount,
                        number_of_cycles = context.RunOverrides.Resolved.NumberOfCycles,
                        interval_seconds = context.RunOverrides.Resolved.IntervalSeconds,
                        seed = run.ExecutionSeed,
                        degradation_profile = context.RunOverrides.Resolved.DegradationProfile,
                        degradation_profiles = context.RunOverrides.Resolved.DegradationProfiles,
                        orchestrator_correlation_id = context.RunOverrides.Resolved.OrchestratorCorrelationId,
                        selected_sensor_names = context.RunOverrides.Resolved.SelectedSensorNames
                    }
                }
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Normaliza timestamps para UTC antes da persistência.
    /// </summary>
    internal static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime();
    }

    /// <summary>
    /// Normaliza timestamps opcionais para UTC antes da persistência.
    /// </summary>
    internal static DateTimeOffset? NormalizeUtc(DateTimeOffset? value)
    {
        return value?.ToUniversalTime();
    }
}
