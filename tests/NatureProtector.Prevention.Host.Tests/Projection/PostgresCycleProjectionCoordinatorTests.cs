using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Core.Risk;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Tests.Projection;

public sealed class PostgresCycleProjectionCoordinatorTests
{
    [Fact]
    public async Task ObservationPermutations_ProduceSameSingleSnapshotAndAlertEvaluation()
    {
        var forward = await ExecuteCycleAsync(reverse: false);
        var reverse = await ExecuteCycleAsync(reverse: true);

        Assert.Equal(forward.Score, reverse.Score, 10);
        Assert.Equal(forward.Observed, reverse.Observed);
        Assert.Equal(1, forward.SnapshotCount);
        Assert.Equal(1, reverse.SnapshotCount);
        Assert.Equal(1, forward.AlertEvaluations);
        Assert.Equal(1, reverse.AlertEvaluations);
    }

    [Fact]
    public async Task ReferenceObservation_IsTerminalButDoesNotRestoreObservedCoverage()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);

        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.7);
        await RecordAsync(coordinator, seed, seed.SensorIds[1], 0, MetricOrigin.Reference, 0.8);

        await using var dbContext = scope.CreateDbContext();
        var settlement = Assert.Single(dbContext.CycleSettlements);
        var snapshot = Assert.Single(dbContext.AreaCycleSnapshots);
        Assert.Contains(seed.SensorIds[1].ToString("D"), settlement.MissingSensorIdsJson);
        Assert.Equal(1, snapshot.ObservedCount);
        Assert.Equal(1, snapshot.MissingCount);
    }

    [Fact]
    public async Task LogicalTimeout_MarksAbsentSensorMissing_AndReplayDoesNotDuplicateSnapshots()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);

        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.7);
        await RecordAsync(coordinator, seed, seed.SensorIds[0], 1, MetricOrigin.Observed, 0.6);
        await RecordAsync(coordinator, seed, seed.SensorIds[0], 1, MetricOrigin.Observed, 0.6);

        await using var dbContext = scope.CreateDbContext();
        var cycleZero = await dbContext.CycleSettlements.SingleAsync(entity => entity.CycleIndex == 0);
        Assert.Equal("LogicalTimeout", cycleZero.FinalizationReason);
        Assert.Contains(seed.SensorIds[1].ToString("D"), cycleZero.MissingSensorIdsJson);
        Assert.Single(dbContext.AreaCycleSnapshots);
        Assert.Equal(2, dbContext.CycleObservations.Count());
    }

    [Fact]
    public async Task LateObservationForFinalizedCycle_IsRecordedWithoutDuplicatingSnapshot()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);

        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.7);
        await RecordAsync(coordinator, seed, seed.SensorIds[0], 1, MetricOrigin.Observed, 0.6);
        await RecordAsync(coordinator, seed, seed.SensorIds[1], 0, MetricOrigin.Observed, 0.8);

        await using var dbContext = scope.CreateDbContext();
        var cycleZero = await dbContext.CycleSettlements.SingleAsync(entity => entity.CycleIndex == 0);
        Assert.NotNull(cycleZero.FinalizedAt);
        Assert.Equal("LogicalTimeout", cycleZero.FinalizationReason);
        Assert.Equal(2, dbContext.CycleObservations.Count(entity => entity.CycleIndex == 0));
        Assert.Single(dbContext.AreaCycleSnapshots);
    }

    [Fact]
    public async Task OperationalTemporalCycle_MaterializesCurrentCellOperationalStates()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);

        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.7);
        await RecordAsync(coordinator, seed, seed.SensorIds[1], 0, MetricOrigin.Observed, 0.8);

        await using var dbContext = scope.CreateDbContext();
        var states = await dbContext.CellOperationalStates.OrderBy(entity => entity.SensorId).ToListAsync();
        Assert.Equal(2, states.Count);
        Assert.All(states, state =>
        {
            Assert.Contains(state.SensorId!.Value, seed.SensorIds);
            Assert.Null(state.LatestAssessmentId);
            Assert.Equal(OperationalProjectionStatus.LowCoverage, state.CoverageStatus);
            Assert.Equal(OperationalProjectionStatus.Fresh, state.FreshnessStatus);
            Assert.Equal(OperationalProjectionStatus.Current, state.CarryForwardStatus);
            Assert.Contains("eligible=1", state.Summary);
        });
    }

    [Fact]
    public async Task ProducerCompletion_FinalizesLastIncompleteCycle_WithMissingAndBlockedMembership()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);
        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.7);
        await coordinator.RecordAsync(seed.RunId, 0, seed.AreaId, seed.SensorIds[1], Guid.NewGuid(),
            DateTimeOffset.UtcNow, MetricOrigin.Blocked, CycleObservationOutcome.Blocked,
            null, CancellationToken.None);
        await scope.SeedAsync(async dbContext =>
        {
            var run = await dbContext.SimulationRuns.SingleAsync(entity => entity.Id == seed.RunId);
            run.Status = SimulationRunStatus.Completed;
            run.EndedAt = DateTimeOffset.UtcNow;
        });

        await coordinator.FinalizeCompletedRunsAsync(CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var settlement = Assert.Single(dbContext.CycleSettlements);
        Assert.Contains(seed.SensorIds[1].ToString("D"), settlement.MissingSensorIdsJson);
        Assert.Contains(seed.SensorIds[1].ToString("D"), settlement.BlockedSensorIdsJson);
        Assert.Single(dbContext.AreaCycleSnapshots);
    }

    [Fact]
    public async Task ExpectedMembership_RemainsStableWhenSensorActivationChangesMidCycle()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);
        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.7);
        await scope.SeedAsync(async dbContext =>
        {
            var sensor = await dbContext.SensorNodes.SingleAsync(entity => entity.Id == seed.SensorIds[1]);
            sensor.IsActive = false;
        });

        await RecordAsync(coordinator, seed, seed.SensorIds[1], 0, MetricOrigin.Observed, 0.8);

        await using var dbContext = scope.CreateDbContext();
        var snapshot = Assert.Single(dbContext.AreaCycleSnapshots);
        Assert.Equal(2, snapshot.ExpectedCount);
        Assert.Equal(2, snapshot.ObservedCount);
    }

    [Fact]
    public async Task RunsKeepIndependentCycleState_AndLateCycleCannotRegressOperationalCurrent()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope, includeSecondRun: true);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);
        await RecordAsync(coordinator, seed, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.5);
        await RecordAsync(coordinator, seed with { RunId = seed.SecondRunId!.Value }, seed.SensorIds[0], 0, MetricOrigin.Observed, 0.9);

        var projectionStore = new PostgresAreaOperationalProjectionStore(scope.Factory, NullLogger<PostgresAreaOperationalProjectionStore>.Instance);
        await projectionStore.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, 0.9), 2,
            CancellationToken.None, seed.RunId, 2);
        await projectionStore.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), 0.2), 2,
            CancellationToken.None, seed.RunId, 1);

        await using var dbContext = scope.CreateDbContext();
        Assert.Equal(2, dbContext.CycleSettlements.Select(entity => entity.SimulationRunId).Distinct().Count());
        var current = Assert.Single(dbContext.AreaOperationalStates);
        Assert.Equal(2, current.CycleIndex);
        Assert.Equal(0.9, current.AggregateRiskScore, 10);
    }

    private static async Task<(double Score, int Observed, int SnapshotCount, int AlertEvaluations)> ExecuteCycleAsync(bool reverse)
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(scope.Factory, NullLogger<PostgresCycleProjectionCoordinator>.Instance);
        var readings = new[] { (seed.SensorIds[0], 0.4), (seed.SensorIds[1], 0.8) };
        foreach (var reading in reverse ? readings.Reverse() : readings)
            await RecordAsync(coordinator, seed, reading.Item1, 0, MetricOrigin.Observed, reading.Item2);

        await using var dbContext = scope.CreateDbContext();
        var snapshot = Assert.Single(dbContext.AreaCycleSnapshots);
        return (snapshot.AggregateRiskScore!.Value, snapshot.ObservedCount, dbContext.AreaCycleSnapshots.Count(),
            dbContext.AreaCycleSnapshots.Count(entity => entity.AlertEvaluatedAt != default));
    }

    private static Task<IReadOnlyList<FinalizedCycleProjection>> RecordAsync(
        PostgresCycleProjectionCoordinator coordinator,
        TemporalSeed seed,
        Guid sensorId,
        int cycleIndex,
        MetricOrigin origin,
        double score)
        => coordinator.RecordAsync(seed.RunId, cycleIndex, seed.AreaId, sensorId, Guid.NewGuid(),
            DateTimeOffset.UtcNow, origin, CycleObservationOutcome.Eligible,
            new RiskAssessment(Guid.NewGuid(), DateTimeOffset.UtcNow, score, "test"), CancellationToken.None);

    private static async Task<TemporalSeed> SeedAsync(SqliteControlDbContextScope scope, bool includeSecondRun = false)
    {
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var secondSensorId = Guid.NewGuid();
        var secondGridId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var secondRunId = includeSecondRun ? Guid.NewGuid() : (Guid?)null;
        var now = DateTimeOffset.UtcNow;
        await scope.SeedAsync(dbContext =>
        {
            dbContext.GridCells.Add(new GridCellRecord
            {
                Id = secondGridId,
                AreaId = seed.AreaId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                CellCode = "CELL-002",
                CentroidLatitude = 39.76,
                CentroidLongitude = -7.91
            });
            dbContext.SensorNodes.Add(new SensorNodeRecord
            {
                Id = secondSensorId,
                AreaId = seed.AreaId,
                GridCellId = secondGridId,
                ProfileId = seed.SensorProfileId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                Name = "Sensor-02",
                Type = SensorType.WeatherStation,
                Latitude = 39.76,
                Longitude = -7.91,
                IsActive = true
            });
            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = seed.AreaId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                Code = "temporal-test",
                Name = "Temporal test",
                ScenarioKind = ScenarioCategory.Base,
                ParametersJson = "{}"
            });
            AddRun(runId);
            if (secondRunId.HasValue) AddRun(secondRunId.Value);
            AddOperation(runId, isOperational: true);
            if (secondRunId.HasValue) AddOperation(secondRunId.Value, isOperational: false);
            return Task.CompletedTask;

            void AddRun(Guid id) => dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = id,
                AreaId = seed.AreaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                ScenarioCode = "temporal-test",
                ScenarioName = "Temporal test",
                CreatedAt = now,
                StartedAt = now,
                LogicalStartTimestamp = now,
                IntervalSeconds = 1,
                NumberOfCycles = 3,
                Status = SimulationRunStatus.Running,
                MetadataJson = "{}"
            });
            void AddOperation(Guid id, bool isOperational) => dbContext.RuntimeOperations.Add(new RuntimeOperationRecord
            {
                OperationId = Guid.NewGuid(),
                RequestId = Guid.NewGuid(),
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                SimulationRunId = id,
                CorrelationId = Guid.NewGuid().ToString("N"),
                State = "Running",
                IsOperational = isOperational,
                AcceptedAt = now,
                UpdatedAt = now,
                DeadlineAt = now.AddMinutes(5)
            });

        });
        return new TemporalSeed(seed.AreaId, runId, secondRunId, [seed.SensorId, secondSensorId]);
    }

    private sealed record TemporalSeed(Guid AreaId, Guid RunId, Guid? SecondRunId, Guid[] SensorIds);
}
