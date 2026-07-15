using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Projection;

public sealed class PostgresCycleProjectionCoordinator(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<PostgresCycleProjectionCoordinator> logger) : ICycleProjectionCoordinator
{
    private const long CycleProjectionAdvisoryLockKey = 5638591602766115926L;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<FinalizedCycleProjection>> FinalizeCompletedRunsAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireCycleProjectionLockAsync(dbContext, cancellationToken);
            var open = await dbContext.CycleSettlements
                .Where(entity => entity.FinalizedAt == null)
                .OrderBy(entity => entity.SimulationRunId)
                .ThenBy(entity => entity.CycleIndex)
                .ToListAsync(cancellationToken);
            if (open.Count == 0) return Array.Empty<FinalizedCycleProjection>();

            var runIds = open.Select(entity => entity.SimulationRunId).Distinct().ToArray();
            var completedRuns = await dbContext.SimulationRuns.AsNoTracking()
                .Where(entity => runIds.Contains(entity.Id) && entity.Status == Core.Scenarios.SimulationRunStatus.Completed)
                .ToDictionaryAsync(entity => entity.Id, cancellationToken);
            var finalizations = new List<FinalizedCycleProjection>();
            foreach (var group in open.Where(entity => completedRuns.ContainsKey(entity.SimulationRunId)).GroupBy(entity => entity.SimulationRunId))
            {
                foreach (var settlement in group)
                {
                    var sensors = await ResolveSettlementSensorsAsync(dbContext, settlement, cancellationToken);
                    var observations = await dbContext.CycleObservations.AsNoTracking()
                        .Where(entity => entity.SimulationRunId == group.Key && entity.CycleIndex == settlement.CycleIndex)
                        .ToListAsync(cancellationToken);
                    finalizations.Add(FinalizeCycle(dbContext, settlement, sensors, observations, true, DateTimeOffset.UtcNow));
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return finalizations;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<FinalizedCycleProjection>> RecordAsync(
        Guid simulationRunId,
        int cycleIndex,
        Guid areaId,
        Guid sensorId,
        Guid eventId,
        DateTimeOffset eventTime,
        MetricOrigin origin,
        CycleObservationOutcome outcome,
        RiskAssessment? assessment,
        CancellationToken cancellationToken)
    {
        if (cycleIndex < 0) throw new ArgumentOutOfRangeException(nameof(cycleIndex));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireCycleProjectionLockAsync(dbContext, cancellationToken);
            var run = await dbContext.SimulationRuns.AsNoTracking()
                .SingleAsync(entity => entity.Id == simulationRunId && entity.AreaId == areaId, cancellationToken);
            var existingSettlement = await dbContext.CycleSettlements.AsNoTracking().SingleOrDefaultAsync(
                entity => entity.SimulationRunId == simulationRunId && entity.CycleIndex == cycleIndex,
                cancellationToken);
            var sensors = existingSettlement is null
                ? await ResolveExpectedSensorsAsync(dbContext, run, cancellationToken)
                : await ResolveSettlementSensorsAsync(dbContext, existingSettlement, cancellationToken);
            if (sensors.All(sensor => sensor.Id != sensorId))
                return Array.Empty<FinalizedCycleProjection>();

            var now = DateTimeOffset.UtcNow;
            var expectedJson = SerializeIds(sensors.Select(sensor => sensor.Id));
            var operational = await dbContext.RuntimeOperations.AsNoTracking()
                .AnyAsync(entity => entity.SimulationRunId == simulationRunId && entity.IsOperational, cancellationToken);

            for (var index = 0; index <= cycleIndex; index++)
            {
                if (!await dbContext.CycleSettlements.AnyAsync(
                        entity => entity.SimulationRunId == simulationRunId && entity.CycleIndex == index,
                        cancellationToken))
                {
                    dbContext.CycleSettlements.Add(new CycleSettlementRecord
                    {
                        Id = Guid.NewGuid(),
                        SimulationRunId = simulationRunId,
                        CycleIndex = index,
                        AreaId = areaId,
                        ExpectedSensorIdsJson = expectedJson,
                        IsOperational = operational,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            var settlement = await dbContext.CycleSettlements.SingleAsync(
                entity => entity.SimulationRunId == simulationRunId && entity.CycleIndex == cycleIndex,
                cancellationToken);
            sensors = await ResolveSettlementSensorsAsync(dbContext, settlement, cancellationToken);
            if (sensors.All(sensor => sensor.Id != sensorId))
                return Array.Empty<FinalizedCycleProjection>();

            var replay = await dbContext.CycleObservations.AnyAsync(
                entity => entity.SimulationRunId == simulationRunId && entity.CycleIndex == cycleIndex && entity.SensorId == sensorId,
                cancellationToken);
            if (!replay)
            {
                var sensor = sensors.Single(entity => entity.Id == sensorId);
                dbContext.CycleObservations.Add(new CycleObservationRecord
                {
                    Id = Guid.NewGuid(),
                    SimulationRunId = simulationRunId,
                    CycleIndex = cycleIndex,
                    AreaId = areaId,
                    SensorId = sensorId,
                    GridCellId = sensor.GridCellId,
                    EventId = eventId,
                    MetricOrigin = origin.ToString(),
                    Outcome = outcome.ToString(),
                    RiskScore = assessment?.RiskScore,
                    RiskLevel = assessment?.RiskLevel.ToString(),
                    EventTime = eventTime,
                    CreatedAt = now
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (settlement.FinalizedAt is not null)
                return Array.Empty<FinalizedCycleProjection>();

            var finalizations = new List<FinalizedCycleProjection>();
            var open = await dbContext.CycleSettlements
                .Where(entity => entity.SimulationRunId == simulationRunId && entity.FinalizedAt == null)
                .OrderBy(entity => entity.CycleIndex)
                .ToListAsync(cancellationToken);
            foreach (var candidate in open)
            {
                var candidateSensors = await ResolveSettlementSensorsAsync(dbContext, candidate, cancellationToken);
                var observations = await dbContext.CycleObservations.AsNoTracking()
                    .Where(entity => entity.SimulationRunId == simulationRunId && entity.CycleIndex == candidate.CycleIndex)
                    .ToListAsync(cancellationToken);
                var timedOut = candidate.CycleIndex < cycleIndex;
                if (!timedOut && observations.Select(item => item.SensorId).Distinct().Count() < candidateSensors.Count)
                    continue;

                finalizations.Add(FinalizeCycle(dbContext, candidate, candidateSensors, observations, timedOut, now));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return finalizations;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<List<SensorNodeRecord>> ResolveExpectedSensorsAsync(
        NatureProtectorControlDbContext dbContext,
        SimulationRunRecord run,
        CancellationToken cancellationToken)
    {
        var selectedNames = ReadSelectedSensorNames(run.MetadataJson);
        var query = dbContext.SensorNodes.AsNoTracking().Where(entity => entity.AreaId == run.AreaId && entity.IsActive);
        if (selectedNames.Count > 0) query = query.Where(entity => selectedNames.Contains(entity.Name));
        return await query.OrderBy(entity => entity.Id).ToListAsync(cancellationToken);
    }

    private static async Task<List<SensorNodeRecord>> ResolveSettlementSensorsAsync(
        NatureProtectorControlDbContext dbContext,
        CycleSettlementRecord settlement,
        CancellationToken cancellationToken)
    {
        var ids = JsonSerializer.Deserialize<Guid[]>(settlement.ExpectedSensorIdsJson) ?? [];
        return await dbContext.SensorNodes.AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .OrderBy(entity => entity.Id)
            .ToListAsync(cancellationToken);
    }

    private static FinalizedCycleProjection FinalizeCycle(
        NatureProtectorControlDbContext dbContext,
        CycleSettlementRecord settlement,
        IReadOnlyList<SensorNodeRecord> sensors,
        IReadOnlyList<CycleObservationRecord> observations,
        bool timedOut,
        DateTimeOffset now)
    {
        var observed = observations.Where(item => item.MetricOrigin == MetricOrigin.Observed.ToString())
            .Select(item => item.SensorId).ToHashSet();
        var blocked = observations.Where(item => item.Outcome == CycleObservationOutcome.Blocked.ToString())
            .Select(item => item.SensorId).ToHashSet();
        var eligible = observations.Where(item => item.Outcome == CycleObservationOutcome.Eligible.ToString() && item.RiskScore.HasValue)
            .Select(item => item.SensorId).ToHashSet();
        var missing = sensors.Select(item => item.Id).Where(id => !observed.Contains(id)).ToHashSet();

        settlement.ObservedSensorIdsJson = SerializeIds(observed);
        settlement.MissingSensorIdsJson = SerializeIds(missing);
        settlement.BlockedSensorIdsJson = SerializeIds(blocked);
        settlement.EligibleSensorIdsJson = SerializeIds(eligible);
        settlement.Status = "Finalized";
        settlement.FinalizedAt = now;
        settlement.UpdatedAt = now;
        settlement.FinalizationReason = timedOut ? "LogicalTimeout" : "AllExpectedTerminal";

        var cellSnapshots = new List<CellCycleSnapshotRecord>();
        foreach (var cell in sensors.GroupBy(sensor => sensor.GridCellId).OrderBy(group => group.Key))
        {
            var ids = cell.Select(sensor => sensor.Id).ToHashSet();
            var cellObservations = observations.Where(item => ids.Contains(item.SensorId)).ToArray();
            var scores = cellObservations.Where(item => item.Outcome == "Eligible" && item.RiskScore.HasValue)
                .Select(item => item.RiskScore!.Value).Order().ToArray();
            var score = Aggregate(scores);
            var snapshot = new CellCycleSnapshotRecord
            {
                Id = Guid.NewGuid(),
                SimulationRunId = settlement.SimulationRunId,
                CycleIndex = settlement.CycleIndex,
                AreaId = settlement.AreaId,
                GridCellId = cell.Key,
                ExpectedCount = ids.Count,
                ObservedCount = ids.Count(observed.Contains),
                MissingCount = ids.Count(missing.Contains),
                BlockedCount = ids.Count(blocked.Contains),
                EligibleCount = ids.Count(eligible.Contains),
                AggregateRiskScore = score,
                AggregateRiskLevel = new AreaRiskSnapshot(Guid.NewGuid(), now, score).AggregateRiskLevel.ToString(),
                SnapshotTimestamp = now
            };
            dbContext.CellCycleSnapshots.Add(snapshot);
            cellSnapshots.Add(snapshot);
        }

        var areaScore = Aggregate(cellSnapshots.Where(item => item.EligibleCount > 0).Select(item => item.AggregateRiskScore).Order().ToArray());
        var areaSnapshot = new AreaRiskSnapshot(
            Guid.NewGuid(), now, areaScore,
            $"Cycle {settlement.CycleIndex}: expected={sensors.Count}; observed={observed.Count}; missing={missing.Count}; blocked={blocked.Count}; eligible={eligible.Count}.");
        dbContext.AreaCycleSnapshots.Add(new AreaCycleSnapshotRecord
        {
            Id = areaSnapshot.Id,
            SimulationRunId = settlement.SimulationRunId,
            CycleIndex = settlement.CycleIndex,
            AreaId = settlement.AreaId,
            CellCount = cellSnapshots.Count,
            ExpectedCount = sensors.Count,
            ObservedCount = observed.Count,
            MissingCount = missing.Count,
            BlockedCount = blocked.Count,
            EligibleCount = eligible.Count,
            AggregateRiskScore = areaScore,
            AggregateRiskLevel = areaSnapshot.AggregateRiskLevel.ToString(),
            SnapshotTimestamp = now,
            AlertEvaluatedAt = now,
            AlertOutcome = areaScore >= 0.75 ? "Alarm" : areaScore >= 0.60 ? "Warning" : "None",
            IsOperational = settlement.IsOperational
        });
        return new FinalizedCycleProjection(
            settlement.SimulationRunId, settlement.CycleIndex, settlement.AreaId,
            areaSnapshot, eligible.Count, settlement.IsOperational);
    }

    private static double Aggregate(IReadOnlyList<double> scores)
    {
        if (scores.Count == 0) return 0;
        var rank = Math.Clamp((int)Math.Ceiling(scores.Count * 0.8) - 1, 0, scores.Count - 1);
        return (0.7 * scores[rank]) + (0.3 * scores[^1]);
    }

    private async Task AcquireCycleProjectionLockAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CycleProjectionAdvisoryLockKey});",
            cancellationToken);
        logger.LogInformation(
            "Cycle projection advisory lock acquired | LockKey={LockKey} | ProcessId={ProcessId}",
            CycleProjectionAdvisoryLockKey,
            Environment.ProcessId);
    }

    private static HashSet<string> ReadSelectedSensorNames(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return [];
        using var document = JsonDocument.Parse(metadataJson);
        if (!document.RootElement.TryGetProperty("run_overrides", out var overrides) ||
            !overrides.TryGetProperty("resolved", out var resolved) ||
            !resolved.TryGetProperty("selected_sensor_names", out var names) || names.ValueKind != JsonValueKind.Array)
            return [];
        return names.EnumerateArray().Select(item => item.GetString()).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string SerializeIds(IEnumerable<Guid> ids)
        => JsonSerializer.Serialize(ids.Distinct().Order().Select(id => id.ToString("D")));
}
