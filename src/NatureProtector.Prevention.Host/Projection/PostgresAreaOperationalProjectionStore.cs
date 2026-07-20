using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Risk;
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
        for (var attempt = 0; attempt < 2; attempt++)
        {
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
            var freshness = OperationalProjectionStatus.ResolveFreshness(assessment.Timestamp, now);

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
            existingState.CoverageStatus = OperationalProjectionStatus.ResolveCoverage(assessment);
            existingState.FreshnessStatus = freshness;
            existingState.CarryForwardStatus = OperationalProjectionStatus.ResolveCarryForward(freshness);
            existingState.Summary = Truncate(assessment.ExplanationSummary, 2000);
            existingState.UpdatedAt = now;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                stopwatch.Stop();
                PreventionHostTelemetry.PostgresWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
                {
                    { TelemetryTags.Operation, "cell_projection" },
                    { TelemetryTags.Outcome, "stored" }
                });
                return;
            }
            catch (DbUpdateException ex) when (attempt == 0 && ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.CellOperationalStateGridCellId))
            {
                logger.LogDebug(
                    "Cell projection insert raced on GridCellId and will be retried as update | AreaId={AreaId} | SensorId={SensorId}",
                    areaId,
                    sensorId);
            }
        }

        throw new InvalidOperationException("Cell operational projection retry loop exited unexpectedly.");
    }

    /// <summary>
    /// Atualiza a projeção agregada da área e o estado dos alertas simples
    /// derivados do snapshot.
    /// </summary>
    public async Task<AreaProjectionWriteResult> SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null,
        int? cycleIndex = null)
    {
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.area_projection");
        var stopwatch = Stopwatch.StartNew();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var area = await dbContext.Areas
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == areaId, cancellationToken);

            var existingState = await dbContext.AreaOperationalStates
                .SingleOrDefaultAsync(entity => entity.AreaId == areaId, cancellationToken);
            if (cycleIndex.HasValue && existingState?.CycleIndex > cycleIndex.Value)
                return new AreaProjectionWriteResult(DateTimeOffset.UtcNow, null);
            var previousAdjustedScore = existingState?.AggregateRiskScore ?? snapshot.AggregateRiskScore;

            var now = DateTimeOffset.UtcNow;
            var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);
            var freshness = OperationalProjectionStatus.ResolveFreshness(snapshot.Timestamp, now);
            var coverage = OperationalProjectionStatus.ResolveCoverage(assessmentCount);

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
            existingState.SimulationRunId = simulationRunId;
            existingState.CycleIndex = cycleIndex;
            existingState.AggregateRiskScore = snapshot.AggregateRiskScore;
            existingState.AggregateRiskLevel = snapshot.AggregateRiskLevel.ToString();
            existingState.Severity = severity.ToString();
            existingState.CoverageStatus = coverage;
            existingState.FreshnessStatus = freshness;
            existingState.CarryForwardStatus = OperationalProjectionStatus.ResolveCarryForward(freshness);
            existingState.Summary = Truncate(snapshot.Summary, 2000);
            existingState.AssessmentCount = assessmentCount;
            existingState.UpdatedAt = now;

            var existingAlert = await dbContext.AlertStates
                .SingleOrDefaultAsync(
                    entity => entity.AreaId == areaId &&
                        entity.AlertCode == "area-risk-high" &&
                        entity.Status == OperationalAlertStatus.Open.ToString(),
                    cancellationToken);
            var currentState = V1AlertPolicy.InferCurrentState(
                hasOpenAlert: existingAlert is not null,
                previousAdjustedScore: previousAdjustedScore);
            var pendingState = Enum.TryParse<V1AlertState>(existingState.PendingAlertState, out var parsedPendingState)
                ? parsedPendingState
                : V1AlertState.None;
            var decision = V1AlertPolicy.EvaluateTransition(
                currentState,
                snapshot.AggregateRiskScore,
                pendingState,
                existingState.PendingAlertCycles,
                snapshot.Timestamp,
                existingState.AlertCooldownUntil,
                TimeSpan.FromSeconds(60));
            var nextState = decision.State;

            existingState.PendingAlertState = decision.PendingState.ToString();
            existingState.PendingAlertCycles = decision.PendingCycles;
            existingState.AlertCooldownUntil = decision.CooldownUntil;

            if (nextState is V1AlertState.Warning or V1AlertState.Alarm)
            {
                var alertedAt = now;
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
                        Message = Truncate(BuildAlertMessage(snapshot, nextState), 2000) ?? string.Empty,
                        TriggeredAt = snapshot.Timestamp,
                        UpdatedAt = alertedAt
                    });
                }
                else
                {
                    existingAlert.AreaOperationalStateId = existingState.Id;
                    existingAlert.Severity = severity.ToString();
                    existingAlert.Message = Truncate(BuildAlertMessage(snapshot, nextState), 2000) ?? string.Empty;
                    existingAlert.UpdatedAt = alertedAt;
                    existingAlert.ResolvedAt = null;
                }
            }
            else if (existingAlert is not null)
            {
                existingAlert.Status = OperationalAlertStatus.Resolved.ToString();
                existingAlert.UpdatedAt = now;
                existingAlert.ResolvedAt = now;
            }

            try
            {
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
                var alertedAt = nextState is V1AlertState.Warning or V1AlertState.Alarm
                    ? now
                    : (DateTimeOffset?)null;
                return new AreaProjectionWriteResult(now, alertedAt);
            }
            catch (DbUpdateException ex) when (attempt == 0 && ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.AreaOperationalStateAreaId))
            {
                logger.LogDebug(
                    "Area projection insert raced on AreaId and will be retried as update | AreaId={AreaId}",
                    areaId);
            }
        }

        throw new InvalidOperationException("Area operational projection retry loop exited unexpectedly.");
    }


    /// <summary>
    /// Marks the current operational projection as unavailable without evaluating
    /// alert transitions from a fabricated numeric score.
    /// </summary>
    public async Task MarkUnavailableAsync(
        Guid areaId,
        DateTimeOffset snapshotTimestamp,
        string reason,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null,
        int? cycleIndex = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingState = await dbContext.AreaOperationalStates
            .SingleOrDefaultAsync(entity => entity.AreaId == areaId, cancellationToken);

        if (existingState is null)
        {
            logger.LogInformation(
                "Operational projection unavailable and no previous state exists | AreaId={AreaId} | Reason={Reason}",
                areaId,
                reason);
            return;
        }

        if (cycleIndex.HasValue && existingState.CycleIndex > cycleIndex.Value)
        {
            return;
        }

        existingState.SnapshotTimestamp = snapshotTimestamp;
        existingState.SimulationRunId = simulationRunId;
        existingState.CycleIndex = cycleIndex;
        existingState.AggregateRiskLevel = RiskLevel.Unknown.ToString();
        existingState.Severity = Severity.Info.ToString();
        existingState.CoverageStatus = OperationalProjectionStatus.Blocked;
        existingState.FreshnessStatus = OperationalProjectionStatus.Unavailable;
        existingState.CarryForwardStatus = OperationalProjectionStatus.NotAvailable;
        existingState.Summary = Truncate(reason, 2000);
        existingState.AssessmentCount = 0;
        existingState.PendingAlertState = V1AlertState.None.ToString();
        existingState.PendingAlertCycles = 0;
        existingState.UpdatedAt = DateTimeOffset.UtcNow;

        // Deliberately preserve the previous numeric score internally for alert
        // hysteresis memory. The API hides it while AssessmentCount is zero.
        // Do not open, update or resolve AlertStateRecord in this path.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Constrói a mensagem curta do alerta operacional agregado.
    /// </summary>
    private static string BuildAlertMessage(AreaRiskSnapshot snapshot, V1AlertState state)
        => $"AlertState={state}; Area risk is {snapshot.AggregateRiskLevel} with adjusted score {snapshot.AggregateRiskScore:F2}. {CandidateParameterSetV1.Version} (non-official).";

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
