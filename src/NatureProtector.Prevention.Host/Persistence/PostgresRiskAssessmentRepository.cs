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
    public async Task AddAsync(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.risk_assessment");
        var stopwatch = Stopwatch.StartNew();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .AnyAsync(entity => entity.SourceEventId == sourceEventId, cancellationToken);

        if (exists)
        {
            return;
        }

        var sensorNode = await dbContext.SensorNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == sensorId, cancellationToken);

        dbContext.RiskAssessmentLogs.Add(new RiskAssessmentLogRecord
        {
            Id = assessment.Id,
            AreaId = areaId,
            SensorId = sensorId,
            GridCellId = sensorNode?.GridCellId,
            SourceEventId = sourceEventId,
            Timestamp = assessment.Timestamp,
            RiskScore = assessment.RiskScore,
            RiskLevel = assessment.RiskLevel.ToString(),
            ExplanationSummary = assessment.ExplanationSummary,
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.RiskAssessmentSourceEventId))
        {
            logger.LogDebug(
                "Risk assessment duplicate treated as idempotent after concurrent insert | SourceEventId={SourceEventId} | SensorId={SensorId}",
                sourceEventId,
                sensorId);
            return;
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
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.AreaId == areaId)
            .ToListAsync(cancellationToken);

        return SelectLatestAssessments(rows);
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
        return new RiskAssessment(
            entity.Id,
            entity.Timestamp,
            entity.RiskScore,
            entity.ExplanationSummary);
    }
}
