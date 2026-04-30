using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Prevention.Host.Persistence;

/*
 * Este repositório guarda e consulta snapshots agregados de risco por área.
 *
 * Rationale:
 * - O snapshot agregado é o resumo operativo que sustenta dashboards,
 *   projeções e alertas.
 * - A pipeline precisa de persistir estes snapshots sem conhecer o detalhe do
 *   esquema de projeção.
 *
 * Design considerations:
 * - Cada snapshot é guardado com metadados suficientes para relacionar o número
 *   de avaliações que contribuíram para o agregado.
 * - A consulta principal devolve o snapshot mais recente da área.
 */

public sealed class PostgresAreaRiskSnapshotRepository(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<PostgresAreaRiskSnapshotRepository> logger) : IAreaRiskSnapshotRepository
{
    /// <summary>
    /// Persiste um snapshot agregado de risco para a área indicada.
    /// </summary>
    public async Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.area_risk_snapshot");
        var stopwatch = Stopwatch.StartNew();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.AreaRiskSnapshotLogs
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == snapshot.Id, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.AreaRiskSnapshotLogs.Add(new AreaRiskSnapshotLogRecord
        {
            Id = snapshot.Id,
            AreaId = areaId,
            SnapshotTimestamp = snapshot.Timestamp,
            AggregateRiskScore = snapshot.AggregateRiskScore,
            AggregateRiskLevel = snapshot.AggregateRiskLevel.ToString(),
            Summary = snapshot.Summary,
            AssessmentCount = assessmentCount,
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.AreaRiskSnapshotId))
        {
            logger.LogDebug(
                "Area risk snapshot duplicate treated as idempotent after concurrent insert | AreaId={AreaId} | SnapshotId={SnapshotId}",
                areaId,
                snapshot.Id);
            return;
        }

        stopwatch.Stop();
        PreventionHostTelemetry.PostgresWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Operation, "area_risk_snapshot" },
            { TelemetryTags.Outcome, "stored" }
        });

        logger.LogDebug(
            "Area risk snapshot persisted in PostgreSQL | AreaId={AreaId} | SnapshotId={SnapshotId} | AssessmentCount={AssessmentCount}",
            areaId,
            snapshot.Id,
            assessmentCount);
    }

    /// <summary>
    /// Obtém o snapshot agregado mais recente conhecido para uma área.
    /// </summary>
    public async Task<AreaRiskSnapshot?> GetLatestAsync(
        Guid areaId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var row = await dbContext.AreaRiskSnapshotLogs
            .AsNoTracking()
            .Where(entity => entity.AreaId == areaId)
            .ToListAsync(cancellationToken);

        var latest = row
            .OrderByDescending(entity => entity.SnapshotTimestamp)
            .ThenByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();

        if (latest is null)
        {
            return null;
        }

        return new AreaRiskSnapshot(
            latest.Id,
            latest.SnapshotTimestamp,
            latest.AggregateRiskScore,
            latest.Summary);
    }
}
