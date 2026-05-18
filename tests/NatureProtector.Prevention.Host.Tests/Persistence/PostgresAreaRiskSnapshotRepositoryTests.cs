using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Core.Risk;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresAreaRiskSnapshotRepositoryTests
{
    [Fact]
    public async Task SaveAsync_MultipleSnapshots_GetLatestReturnsMostRecent()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var older = new AreaRiskSnapshot(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.40,
            "Older snapshot");
        var newer = new AreaRiskSnapshot(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero),
            0.82,
            "Newer snapshot");

        await repository.SaveAsync(seed.AreaId, older, 2, CancellationToken.None);
        await repository.SaveAsync(seed.AreaId, newer, 3, CancellationToken.None);

        var latest = await repository.GetLatestAsync(seed.AreaId, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(newer.Id, latest!.Id);
        Assert.Equal(0.82, latest.AggregateRiskScore);

        await using var dbContext = scope.CreateDbContext();
        Assert.Equal(2, dbContext.AreaRiskSnapshotLogs.Count());
    }

    [Fact]
    public async Task SaveAsync_WithSimulationRunId_PersistsAndFiltersByRun()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var runA = await SeedSimulationRunAsync(scope, seed);
        var runB = await SeedSimulationRunAsync(scope, seed);
        var repository = CreateRepository(scope);
        var snapshotA = new AreaRiskSnapshot(
            Guid.Parse("21000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.40,
            "Run A snapshot");
        var snapshotB = new AreaRiskSnapshot(
            Guid.Parse("22000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero),
            0.82,
            "Run B snapshot");

        await repository.SaveAsync(seed.AreaId, snapshotA, 1, CancellationToken.None, runA);
        await repository.SaveAsync(seed.AreaId, snapshotB, 1, CancellationToken.None, runB);

        var latestRunA = await repository.GetLatestAsync(seed.AreaId, CancellationToken.None, runA);

        Assert.NotNull(latestRunA);
        Assert.Equal(snapshotA.Id, latestRunA!.Id);

        await using var dbContext = scope.CreateDbContext();
        var rowA = Assert.Single(dbContext.AreaRiskSnapshotLogs.Where(row => row.Id == snapshotA.Id));
        Assert.Equal(runA, rowA.SimulationRunId);
    }

    [Fact]
    public async Task SaveAsync_DuplicateSnapshotId_IgnoresSecondWrite()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var snapshotId = Guid.Parse("30000000-0000-0000-0000-000000000001");

        await repository.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(snapshotId, new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.55, "First"),
            4,
            CancellationToken.None);

        await repository.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(snapshotId, new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), 0.90, "Second"),
            5,
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.AreaRiskSnapshotLogs);
        Assert.Equal(0.55, row.AggregateRiskScore);
        Assert.Equal(4, row.AssessmentCount);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentUniqueViolation_TreatedAsIdempotent()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"natureprotector-area-snapshot-tests-{Guid.NewGuid():N}.sqlite");
        await using var bootstrapScope = new SqliteControlDbContextScope(
            useFileDatabase: true,
            databasePath: databasePath);
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(bootstrapScope);
        var snapshot = new AreaRiskSnapshot(
            Guid.Parse("50000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.55,
            "First");
        var interceptor = new DuplicateInsertOnSaveInterceptor(
            bootstrapScope.PlainOptions,
            context => context.ChangeTracker.Entries<AreaRiskSnapshotLogRecord>().Any(entry => entry.State == EntityState.Added),
            (sidecarContext, currentContext, _) =>
            {
                var pending = currentContext.ChangeTracker.Entries<AreaRiskSnapshotLogRecord>()
                    .Single(entry => entry.State == EntityState.Added)
                    .Entity;

                sidecarContext.AreaRiskSnapshotLogs.Add(new AreaRiskSnapshotLogRecord
                {
                    Id = pending.Id,
                    AreaId = pending.AreaId,
                    SimulationRunId = pending.SimulationRunId,
                    SnapshotTimestamp = pending.SnapshotTimestamp,
                    AggregateRiskScore = pending.AggregateRiskScore,
                    AggregateRiskLevel = pending.AggregateRiskLevel,
                    Summary = pending.Summary,
                    AssessmentCount = pending.AssessmentCount,
                    CreatedAt = pending.CreatedAt
                });

                return Task.CompletedTask;
            });
        await using var scope = new SqliteControlDbContextScope(
            configureOptions: builder => builder.AddInterceptors(interceptor),
            useFileDatabase: true,
            databasePath: databasePath);
        var repository = CreateRepository(scope);

        await repository.SaveAsync(seed.AreaId, snapshot, 4, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.AreaRiskSnapshotLogs);
        Assert.Equal(snapshot.Id, row.Id);
    }

    [Fact]
    public async Task GetLatestAsync_AreaHasNoSnapshots_ReturnsNull()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);

        var latest = await repository.GetLatestAsync(seed.AreaId, CancellationToken.None);

        Assert.Null(latest);
    }

    private static PostgresAreaRiskSnapshotRepository CreateRepository(SqliteControlDbContextScope scope)
    {
        return new PostgresAreaRiskSnapshotRepository(
            scope.Factory,
            NullLogger<PostgresAreaRiskSnapshotRepository>.Instance);
    }

    private static async Task<Guid> SeedSimulationRunAsync(
        SqliteControlDbContextScope scope,
        SeededControlPlane seed)
    {
        var scenarioId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        await scope.SeedAsync(dbContext =>
        {
            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = seed.AreaId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                Code = $"scenario-{scenarioId:N}",
                Name = "Test scenario",
                ScenarioKind = ScenarioCategory.Base
            });

            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = seed.AreaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = seed.ConfigurationVersionId,
                ScenarioCode = "scenario-test",
                ScenarioName = "Test scenario",
                CreatedAt = new DateTimeOffset(2026, 4, 10, 7, 59, 0, TimeSpan.Zero),
                StartedAt = new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
                LogicalStartTimestamp = new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
                IntervalSeconds = 60,
                NumberOfCycles = 1,
                Status = SimulationRunStatus.Running
            });

            return Task.CompletedTask;
        });

        return runId;
    }
}
