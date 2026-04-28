using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Prevention.Host.Projection;

/*
 * Este store materializa em PostgreSQL o estado operacional corrente por célula,
 * por área e os alertas derivados.
 *
 * Rationale:
 * - A pipeline precisa de uma projeção pronta a consultar pela API/backoffice
 *   sem recalcular tudo a partir dos logs.
 * - A manutenção desta projeção deve ficar separada da persistência histórica.
 *
 * Design considerations:
 * - O estado por célula segue a última avaliação conhecida do sensor associado.
 * - O estado agregado da área é atualizado a partir do snapshot mais recente.
 * - Alertas operacionais simples são abertos e resolvidos na mesma transação da
 *   projeção agregada.
 */

public sealed class PostgresAreaOperationalProjectionStore(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<PostgresAreaOperationalProjectionStore> logger) : IAreaOperationalProjectionStore
{
    /// <summary>
    /// Atualiza a projeção operacional da célula associada ao sensor recebido.
    /// </summary>
    public async Task SaveCellAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.cell_projection");
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var sensorNode = await dbContext.SensorNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == sensorId, cancellationToken);

        if (sensorNode is null)
        {
            logger.LogWarning(
                "Projection update skipped for cell state because sensor was not found in control plane | AreaId={AreaId} | SensorId={SensorId}",
                areaId,
                sensorId);
            return;
        }

        if (sensorNode.AreaId != areaId)
        {
            logger.LogWarning(
                "Projection update detected area mismatch between reading and sensor deployment | ReadingAreaId={ReadingAreaId} | SensorAreaId={SensorAreaId} | SensorId={SensorId}",
                areaId,
                sensorNode.AreaId,
                sensorId);
        }

        var existingState = await dbContext.CellOperationalStates
            .SingleOrDefaultAsync(entity => entity.GridCellId == sensorNode.GridCellId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var severity = SeverityExtensions.FromRiskLevel(assessment.RiskLevel);

        if (existingState is null)
        {
            existingState = new CellOperationalStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = sensorNode.AreaId,
                GridCellId = sensorNode.GridCellId
            };

            dbContext.CellOperationalStates.Add(existingState);
        }

        existingState.SensorId = sensorId;
        existingState.LatestAssessmentId = assessment.Id;
        existingState.SnapshotTimestamp = assessment.Timestamp;
        existingState.RiskScore = assessment.RiskScore;
        existingState.RiskLevel = assessment.RiskLevel.ToString();
        existingState.Severity = severity.ToString();
        existingState.Summary = Truncate(assessment.ExplanationSummary, 2000);
        existingState.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        stopwatch.Stop();
        PreventionHostTelemetry.PostgresWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Operation, "cell_projection" },
            { TelemetryTags.Outcome, "stored" }
        });
    }

    /// <summary>
    /// Atualiza a projeção agregada da área e o estado dos alertas simples
    /// derivados do snapshot.
    /// </summary>
    public async Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken)
    {
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.area_projection");
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var area = await dbContext.Areas
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == areaId, cancellationToken);

        var existingState = await dbContext.AreaOperationalStates
            .SingleOrDefaultAsync(entity => entity.AreaId == areaId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);

        if (existingState is null)
        {
            existingState = new AreaOperationalStateRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                ConfigurationVersionId = area.ConfigurationVersionId
            };

            dbContext.AreaOperationalStates.Add(existingState);
        }

        existingState.SnapshotTimestamp = snapshot.Timestamp;
        existingState.AggregateRiskScore = snapshot.AggregateRiskScore;
        existingState.AggregateRiskLevel = snapshot.AggregateRiskLevel.ToString();
        existingState.Severity = severity.ToString();
        existingState.Summary = Truncate(snapshot.Summary, 2000);
        existingState.AssessmentCount = assessmentCount;
        existingState.UpdatedAt = now;

        var existingAlert = await dbContext.AlertStates
            .SingleOrDefaultAsync(
                entity => entity.AreaId == areaId &&
                    entity.AlertCode == "area-risk-high" &&
                    entity.Status == OperationalAlertStatus.Open.ToString(),
                cancellationToken);

        if (snapshot.AggregateRiskLevel.IsHighOrAbove())
        {
            if (existingAlert is null)
            {
                dbContext.AlertStates.Add(new AlertStateRecord
                {
                    Id = Guid.NewGuid(),
                    AreaId = areaId,
                    ConfigurationVersionId = area.ConfigurationVersionId,
                    AreaOperationalStateId = existingState.Id,
                    AlertCode = "area-risk-high",
                    Severity = severity.ToString(),
                    Status = OperationalAlertStatus.Open.ToString(),
                    Message = Truncate(BuildAlertMessage(snapshot), 2000) ?? string.Empty,
                    TriggeredAt = snapshot.Timestamp,
                    UpdatedAt = now
                });
            }
            else
            {
                existingAlert.AreaOperationalStateId = existingState.Id;
                existingAlert.Severity = severity.ToString();
                existingAlert.Message = Truncate(BuildAlertMessage(snapshot), 2000) ?? string.Empty;
                existingAlert.UpdatedAt = now;
                existingAlert.ResolvedAt = null;
            }
        }
        else if (existingAlert is not null)
        {
            existingAlert.Status = OperationalAlertStatus.Resolved.ToString();
            existingAlert.UpdatedAt = now;
            existingAlert.ResolvedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        stopwatch.Stop();
        PreventionHostTelemetry.PostgresWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Operation, "area_projection" },
            { TelemetryTags.Outcome, "stored" }
        });

        logger.LogInformation(
            "Projection updated | AreaId={AreaId} | RiskLevel={RiskLevel} | Severity={Severity} | AssessmentCount={AssessmentCount}",
            areaId,
            snapshot.AggregateRiskLevel,
            severity,
            assessmentCount);
    }

    /// <summary>
    /// Constrói a mensagem curta do alerta operacional agregado.
    /// </summary>
    private static string BuildAlertMessage(AreaRiskSnapshot snapshot)
        => $"Area risk is {snapshot.AggregateRiskLevel} with score {snapshot.AggregateRiskScore:F2}.";

    /// <summary>
    /// Limita texto livre aos comprimentos suportados pelo esquema relacional.
    /// </summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
