using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Prevention.Host.Persistence;

/*
 * Este repositório persiste e consulta avaliações de risco produzidas pela
 * pipeline de prevenção.
 *
 * Rationale:
 * - As avaliações de risco são a matéria-prima do snapshot agregado e das
 *   projeções operacionais.
 * - O pipeline precisa de conseguir recuperar o estado mais recente por sensor
 *   sem conhecer detalhes do esquema relacional.
 *
 * Design considerations:
 * - A persistência é idempotente por evento de origem.
 * - O repositório consegue devolver o histórico total ou apenas a última
 *   avaliação conhecida por sensor numa área.
 */

public sealed class PostgresRiskAssessmentRepository(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<PostgresRiskAssessmentRepository> logger) : NatureProtector.Prevention.Persistence.IRiskAssessmentRepository
{
    /// <summary>
    /// Persiste uma avaliação de risco associada a uma leitura de origem.
    /// </summary>
    public async Task<RiskAssessment> AddAsync(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        RiskAssessment assessment,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.risk_assessment");
        var stopwatch = Stopwatch.StartNew();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.RiskAssessmentLogs
            .SingleOrDefaultAsync(entity => entity.SourceEventId == sourceEventId, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayIdentityMatches(existing, areaId, sensorId, simulationRunId, sourceEventId);
            return ToDomainAssessment(existing);
        }

        var sensorNode = await dbContext.SensorNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == sensorId, cancellationToken);

        var assessedAt = DateTimeOffset.UtcNow;
        dbContext.RiskAssessmentLogs.Add(new RiskAssessmentLogRecord
        {
            Id = assessment.Id,
            AreaId = areaId,
            SimulationRunId = simulationRunId,
            SensorId = sensorId,
            GridCellId = sensorNode?.GridCellId,
            SourceEventId = sourceEventId,
            Timestamp = assessment.Timestamp,
            RiskScore = assessment.RiskScore,
            BaseRisk = assessment.BaseRisk,
            AdjustedScore = assessment.AdjustedScore,
            Score100 = assessment.Score100,
            MeteorologyComponent = assessment.MeteorologyComponent,
            DroughtComponent = assessment.DroughtComponent,
            TerritoryComponent = assessment.TerritoryComponent,
            HazardComponent = assessment.HazardComponent,
            FuelComponent = assessment.FuelComponent,
            GeomorphologyComponent = assessment.GeomorphologyComponent,
            ConfidenceFactor = assessment.ConfidenceFactor,
            IntegrityFactor = assessment.IntegrityFactor,
            DominantDriver = assessment.DominantDriver,
            ParameterSetVersion = assessment.ParameterSetVersion,
            CalculationStatus = assessment.CalculationStatus,
            Limitations = assessment.Limitations,
            RiskLevel = assessment.RiskLevel.ToString(),
            ExplanationSummary = assessment.ExplanationSummary,
            CreatedAt = assessedAt,
            AssessedAt = assessedAt
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.RiskAssessmentSourceEventId))
        {
            await using var lookupContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var winner = await lookupContext.RiskAssessmentLogs.AsNoTracking()
                .SingleAsync(entity => entity.SourceEventId == sourceEventId, cancellationToken);
            EnsureReplayIdentityMatches(winner, areaId, sensorId, simulationRunId, sourceEventId);
            return ToDomainAssessment(winner);
        }

        stopwatch.Stop();
        PreventionHostTelemetry.PostgresWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Operation, "risk_assessment" },
            { TelemetryTags.Outcome, "stored" }
        });

        logger.LogDebug(
            "Risk assessment persisted in PostgreSQL | SourceEventId={SourceEventId} | SensorId={SensorId} | RiskLevel={RiskLevel}",
            sourceEventId,
            sensorId,
            assessment.RiskLevel);
        return assessment;
    }

    private static void EnsureReplayIdentityMatches(
        RiskAssessmentLogRecord persisted,
        Guid areaId,
        Guid sensorId,
        Guid? simulationRunId,
        Guid sourceEventId)
    {
        if (persisted.AreaId == areaId && persisted.SensorId == sensorId && persisted.SimulationRunId == simulationRunId)
            return;
        throw new InvalidOperationException(
            $"SourceEventId '{sourceEventId}' is already associated with a different area, sensor or run.");
    }

    /// <summary>
    /// Devolve o histórico completo de avaliações de risco de uma área.
    /// </summary>
    public async Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.AreaId == areaId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(entity => entity.Timestamp)
            .Select(ToDomainAssessment)
            .ToArray();
    }

    /// <summary>
    /// Devolve apenas a avaliação mais recente conhecida por sensor na área.
    /// </summary>
    public async Task<IReadOnlyCollection<RiskAssessment>> GetLatestByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.AreaId == areaId);

        if (simulationRunId.HasValue)
        {
            query = query.Where(entity => entity.SimulationRunId == simulationRunId.Value);
        }

        var rows = await query
            .ToListAsync(cancellationToken);

        return SelectLatestAssessments(rows);
    }

    public async Task MarkProjectedAsync(
        Guid sourceEventId,
        DateTimeOffset projectedAt,
        DateTimeOffset? alertedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.RiskAssessmentLogs
            .Where(entity => entity.SourceEventId == sourceEventId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.ProjectedAt, projectedAt)
                .SetProperty(entity => entity.AlertedAt, alertedAt),
                cancellationToken);
    }

    /// <summary>
    /// Seleciona a avaliação mais recente por sensor a partir de um conjunto de
    /// linhas já carregadas.
    /// </summary>
    internal static IReadOnlyCollection<RiskAssessment> SelectLatestAssessments(
        IEnumerable<RiskAssessmentLogRecord> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .OrderByDescending(entity => entity.Timestamp)
            .ThenByDescending(entity => entity.CreatedAt)
            .GroupBy(entity => entity.SensorId)
            .Select(group => group.First())
            .OrderBy(entity => entity.Timestamp)
            .ThenBy(entity => entity.SensorId)
            .Select(ToDomainAssessment)
            .ToArray();
    }

    /// <summary>
    /// Converte um registo persistido numa avaliação de domínio.
    /// </summary>
    private static RiskAssessment ToDomainAssessment(RiskAssessmentLogRecord entity)
    {
        var baseRisk = entity.BaseRisk == 0.0 && entity.AdjustedScore == 0.0 && entity.RiskScore != 0.0
            ? entity.RiskScore
            : entity.BaseRisk;
        var adjustedScore = entity.AdjustedScore == 0.0 && entity.RiskScore != 0.0
            ? entity.RiskScore
            : entity.AdjustedScore;

        return new RiskAssessment(
            entity.Id,
            entity.Timestamp,
            baseRisk,
            adjustedScore,
            entity.ExplanationSummary,
            entity.MeteorologyComponent,
            entity.DroughtComponent,
            entity.TerritoryComponent,
            entity.HazardComponent,
            entity.FuelComponent,
            entity.GeomorphologyComponent,
            entity.ConfidenceFactor == 0.0 ? 1.0 : entity.ConfidenceFactor,
            entity.IntegrityFactor == 0.0 ? 1.0 : entity.IntegrityFactor,
            entity.DominantDriver,
            entity.ParameterSetVersion,
            entity.CalculationStatus,
            entity.Limitations);
    }
}
