using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Core.Risk;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Tests.Projection;

public sealed class CycleSettlementWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_FinalizesOperationalCompletedRun_AndMarksEligibleEventsProjected()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedOperationalRunAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(
            scope.Factory,
            NullLogger<PostgresCycleProjectionCoordinator>.Instance);
        var projectedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var alertedAt = projectedAt.AddMilliseconds(25);
        var projectionStore = new RecordingProjectionStore(new AreaProjectionWriteResult(projectedAt, alertedAt));
        var repository = new RecordingRiskAssessmentRepository();
        var eventId = Guid.NewGuid();

        await coordinator.RecordAsync(
            seed.RunId,
            cycleIndex: 0,
            seed.AreaId,
            seed.SensorIds[0],
            eventId,
            DateTimeOffset.UtcNow,
            MetricOrigin.Observed,
            CycleObservationOutcome.Eligible,
            new RiskAssessment(Guid.NewGuid(), DateTimeOffset.UtcNow, 0.8, "eligible"),
            CancellationToken.None);
        await MarkRunCompletedAsync(scope, seed.RunId);

        using var worker = CreateWorker(coordinator, projectionStore, repository);
        await worker.StartAsync(CancellationToken.None);
        var save = await projectionStore.WaitForSaveAsync();
        await worker.StopAsync(CancellationToken.None);

        var projected = Assert.Single(await repository.WaitForProjectedAsync());
        Assert.Equal(seed.AreaId, save.AreaId);
        Assert.Equal(seed.RunId, save.SimulationRunId);
        Assert.Equal(0, save.CycleIndex);
        Assert.Equal(1, save.AssessmentCount);
        Assert.NotNull(save.Snapshot);
        Assert.Equal(eventId, projected.SourceEventId);
        Assert.Equal(projectedAt, projected.ProjectedAt);
        Assert.Equal(alertedAt, projected.AlertedAt);
    }

    [Fact]
    public async Task ExecuteAsync_MarksOperationalCompletedRunUnavailable_WhenCycleHasNoEligibleAssessments()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedOperationalRunAsync(scope);
        var coordinator = new PostgresCycleProjectionCoordinator(
            scope.Factory,
            NullLogger<PostgresCycleProjectionCoordinator>.Instance);
        var projectionStore = new RecordingProjectionStore(new AreaProjectionWriteResult(DateTimeOffset.UtcNow, null));
        var repository = new RecordingRiskAssessmentRepository();

        await coordinator.RecordAsync(
            seed.RunId,
            cycleIndex: 0,
            seed.AreaId,
            seed.SensorIds[0],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            MetricOrigin.Observed,
            CycleObservationOutcome.Blocked,
            assessment: null,
            CancellationToken.None);
        await MarkRunCompletedAsync(scope, seed.RunId);

        using var worker = CreateWorker(coordinator, projectionStore, repository);
        await worker.StartAsync(CancellationToken.None);
        var unavailable = await projectionStore.WaitForUnavailableAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(seed.AreaId, unavailable.AreaId);
        Assert.Equal(seed.RunId, unavailable.SimulationRunId);
        Assert.Equal(0, unavailable.CycleIndex);
        Assert.Equal("NoEligibleAssessments", unavailable.Reason);
        Assert.Empty(repository.ProjectedCalls);
    }

    private static CycleSettlementWorker CreateWorker(
        PostgresCycleProjectionCoordinator coordinator,
        IAreaOperationalProjectionStore projectionStore,
        IRiskAssessmentRepository repository)
        => new(coordinator, projectionStore, repository, NullLogger<CycleSettlementWorker>.Instance);

    private static async Task<WorkerSeed> SeedOperationalRunAsync(SqliteControlDbContextScope scope)
    {
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var secondSensorId = Guid.NewGuid();
        var secondGridId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        var runId = Guid.NewGuid();
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
                Code = "worker-test",
                Name = "Worker test",
                ScenarioKind = ScenarioCategory.Base,
                ParametersJson = "{}"
            });
            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = seed.AreaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                ScenarioCode = "worker-test",
                ScenarioName = "Worker test",
                CreatedAt = now,
                StartedAt = now,
                LogicalStartTimestamp = now,
                IntervalSeconds = 1,
                NumberOfCycles = 1,
                Status = SimulationRunStatus.Running,
                MetadataJson = "{}"
            });
            dbContext.RuntimeOperations.Add(new RuntimeOperationRecord
            {
                OperationId = Guid.NewGuid(),
                RequestId = Guid.NewGuid(),
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                SimulationRunId = runId,
                CorrelationId = Guid.NewGuid().ToString("N"),
                State = "Running",
                IsOperational = true,
                AcceptedAt = now,
                UpdatedAt = now,
                DeadlineAt = now.AddMinutes(5)
            });
            return Task.CompletedTask;
        });

        return new WorkerSeed(seed.AreaId, runId, [seed.SensorId, secondSensorId]);
    }

    private static Task MarkRunCompletedAsync(SqliteControlDbContextScope scope, Guid runId)
        => scope.SeedAsync(async dbContext =>
        {
            var run = await dbContext.SimulationRuns.SingleAsync(entity => entity.Id == runId);
            run.Status = SimulationRunStatus.Completed;
            run.EndedAt = DateTimeOffset.UtcNow;
        });

    private sealed record WorkerSeed(Guid AreaId, Guid RunId, Guid[] SensorIds);

    private sealed class RecordingProjectionStore(AreaProjectionWriteResult result) : IAreaOperationalProjectionStore
    {
        private readonly TaskCompletionSource<SaveCall> _save =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<UnavailableCall> _unavailable =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SaveCellAsync(
            Guid areaId,
            Guid sensorId,
            RiskAssessment assessment,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<AreaProjectionWriteResult> SaveAsync(
            Guid areaId,
            AreaRiskSnapshot snapshot,
            int assessmentCount,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null,
            int? cycleIndex = null)
        {
            _save.TrySetResult(new SaveCall(areaId, snapshot, assessmentCount, simulationRunId, cycleIndex));
            return Task.FromResult(result);
        }

        public Task MarkUnavailableAsync(
            Guid areaId,
            DateTimeOffset snapshotTimestamp,
            string reason,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null,
            int? cycleIndex = null)
        {
            _unavailable.TrySetResult(new UnavailableCall(areaId, reason, simulationRunId, cycleIndex));
            return Task.CompletedTask;
        }

        public async Task<SaveCall> WaitForSaveAsync()
            => await _save.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public async Task<UnavailableCall> WaitForUnavailableAsync()
            => await _unavailable.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class RecordingRiskAssessmentRepository : IRiskAssessmentRepository
    {
        private readonly TaskCompletionSource<IReadOnlyList<ProjectedCall>> _projected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ProjectedCall> ProjectedCalls { get; } = [];

        public Task<RiskAssessment> AddAsync(
            Guid areaId,
            Guid sensorId,
            Guid sourceEventId,
            RiskAssessment assessment,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null)
            => Task.FromResult(assessment);

        public Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
            Guid areaId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<RiskAssessment>>([]);

        public Task<IReadOnlyCollection<RiskAssessment>> GetLatestByAreaAsync(
            Guid areaId,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null)
            => Task.FromResult<IReadOnlyCollection<RiskAssessment>>([]);

        public Task MarkProjectedAsync(
            Guid sourceEventId,
            DateTimeOffset projectedAt,
            DateTimeOffset? alertedAt,
            CancellationToken cancellationToken)
        {
            ProjectedCalls.Add(new ProjectedCall(sourceEventId, projectedAt, alertedAt));
            _projected.TrySetResult(ProjectedCalls);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<ProjectedCall>> WaitForProjectedAsync()
            => await _projected.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed record SaveCall(
        Guid AreaId,
        AreaRiskSnapshot Snapshot,
        int AssessmentCount,
        Guid? SimulationRunId,
        int? CycleIndex);

    private sealed record UnavailableCall(
        Guid AreaId,
        string Reason,
        Guid? SimulationRunId,
        int? CycleIndex);

    private sealed record ProjectedCall(
        Guid SourceEventId,
        DateTimeOffset ProjectedAt,
        DateTimeOffset? AlertedAt);
}
