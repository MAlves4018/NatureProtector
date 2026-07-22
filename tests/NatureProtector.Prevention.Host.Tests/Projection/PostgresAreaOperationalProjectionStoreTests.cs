using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;

namespace NatureProtector.Prevention.Host.Tests.Projection;

public sealed class PostgresAreaOperationalProjectionStoreTests
{
    [Fact]
    public async Task SaveCellAsync_KnownSensor_CreatesCellProjection()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);
        var assessment = CreateAssessment(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.72,
            "High risk due to temperature");

        await store.SaveCellAsync(seed.AreaId, seed.SensorId, assessment, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.CellOperationalStates);
        Assert.Equal(seed.AreaId, row.AreaId);
        Assert.Equal(seed.GridCellId, row.GridCellId);
        Assert.Equal(seed.SensorId, row.SensorId);
        Assert.Equal(assessment.Id, row.LatestAssessmentId);
        Assert.Equal("VeryHigh", row.RiskLevel);
        Assert.Equal("Critical", row.Severity);
        Assert.Equal("Complete", row.CoverageStatus);
        Assert.Equal("Expired", row.FreshnessStatus);
        Assert.Equal("ExpiredCarryForward", row.CarryForwardStatus);
    }

    [Fact]
    public async Task SaveCellAsync_SameCellUpdated_ReplacesPreviousOperationalState()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);

        await store.SaveCellAsync(
            seed.AreaId,
            seed.SensorId,
            CreateAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.30, "First"),
            CancellationToken.None);

        var updatedAssessment = CreateAssessment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero),
            0.90,
            "Updated");

        await store.SaveCellAsync(seed.AreaId, seed.SensorId, updatedAssessment, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.CellOperationalStates);
        Assert.Equal(updatedAssessment.Id, row.LatestAssessmentId);
        Assert.Equal(0.90, row.RiskScore);
        Assert.Equal("Extreme", row.RiskLevel);
        Assert.Equal("Emergency", row.Severity);
    }

    [Fact]
    public async Task SaveCellAsync_ConcurrentUniqueViolation_RetriesAsUpdate()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"natureprotector-cell-projection-tests-{Guid.NewGuid():N}.sqlite");
        await using var bootstrapScope = new SqliteControlDbContextScope(
            useFileDatabase: true,
            databasePath: databasePath);
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(bootstrapScope);
        var assessment = CreateAssessment(
            Guid.Parse("11000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.72,
            "High risk due to temperature");
        var interceptor = new DuplicateInsertOnSaveInterceptor(
            bootstrapScope.PlainOptions,
            context => context.ChangeTracker.Entries<CellOperationalStateRecord>().Any(entry => entry.State == EntityState.Added),
            (sidecarContext, currentContext, _) =>
            {
                var pending = currentContext.ChangeTracker.Entries<CellOperationalStateRecord>()
                    .Single(entry => entry.State == EntityState.Added)
                    .Entity;

                sidecarContext.CellOperationalStates.Add(new CellOperationalStateRecord
                {
                    Id = Guid.NewGuid(),
                    AreaId = pending.AreaId,
                    GridCellId = pending.GridCellId,
                    SensorId = pending.SensorId,
                    LatestAssessmentId = pending.LatestAssessmentId,
                    SnapshotTimestamp = pending.SnapshotTimestamp,
                    RiskScore = pending.RiskScore,
                    RiskLevel = pending.RiskLevel,
                    Severity = pending.Severity,
                    Summary = pending.Summary,
                    UpdatedAt = pending.UpdatedAt
                });

                return Task.CompletedTask;
            });
        await using var scope = new SqliteControlDbContextScope(
            configureOptions: builder => builder.AddInterceptors(interceptor),
            useFileDatabase: true,
            databasePath: databasePath);
        var store = CreateStore(scope);

        await store.SaveCellAsync(seed.AreaId, seed.SensorId, assessment, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.CellOperationalStates);
        Assert.Equal(assessment.Id, row.LatestAssessmentId);
    }

    [Fact]
    public async Task SaveCellAsync_SensorMissing_DoesNotCreateProjection()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);

        await store.SaveCellAsync(
            seed.AreaId,
            Guid.NewGuid(),
            CreateAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.70, "Skipped"),
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        Assert.Empty(dbContext.CellOperationalStates);
    }

    [Fact]
    public async Task SaveAsync_HighRisk_CreatesAreaProjectionAndOpenAlert()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);
        var snapshot = new AreaRiskSnapshot(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.85,
            "Area remains under elevated risk.");

        await store.SaveAsync(seed.AreaId, snapshot, 6, CancellationToken.None);
        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(
                Guid.Parse("20000000-0000-0000-0000-000000000002"),
                new DateTimeOffset(2026, 4, 10, 8, 1, 0, TimeSpan.Zero),
                0.85,
                "Area remains under elevated risk."),
            6,
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var areaState = Assert.Single(dbContext.AreaOperationalStates);
        Assert.Equal(seed.AreaId, areaState.AreaId);
        Assert.Equal(seed.ConfigurationVersionId, areaState.ConfigurationVersionId);
        Assert.Equal("VeryHigh", areaState.AggregateRiskLevel);
        Assert.Equal(6, areaState.AssessmentCount);
        Assert.Equal("Complete", areaState.CoverageStatus);
        Assert.Equal("Expired", areaState.FreshnessStatus);
        Assert.Equal("ExpiredCarryForward", areaState.CarryForwardStatus);

        var alert = Assert.Single(dbContext.AlertStates);
        Assert.Equal("area-risk-high", alert.AlertCode);
        Assert.Equal(OperationalAlertStatus.Open.ToString(), alert.Status);
        Assert.Equal("Critical", alert.Severity);
    }

    [Fact]
    public async Task SaveAsync_SingleAssessment_MarksLowCoverage()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);
        var snapshot = new AreaRiskSnapshot(
            Guid.Parse("20500000-0000-0000-0000-000000000001"),
            DateTimeOffset.UtcNow,
            0.45,
            "Single source assessment.");

        await store.SaveAsync(seed.AreaId, snapshot, 1, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var areaState = Assert.Single(dbContext.AreaOperationalStates);
        Assert.Equal("LowCoverage", areaState.CoverageStatus);
        Assert.Equal("Fresh", areaState.FreshnessStatus);
    }


    [Fact]
    public async Task SaveAsync_WarningThreshold_CreatesOpenWarningAlert()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);
        var snapshot = new AreaRiskSnapshot(
            Guid.Parse("21000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 10, 0, TimeSpan.Zero),
            0.62,
            "Warning candidate.");

        await store.SaveAsync(seed.AreaId, snapshot, 4, CancellationToken.None);
        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(
                Guid.Parse("21000000-0000-0000-0000-000000000002"),
                new DateTimeOffset(2026, 4, 10, 8, 11, 0, TimeSpan.Zero),
                0.62,
                "Warning candidate persists."),
            4,
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var alert = Assert.Single(dbContext.AlertStates);
        Assert.Equal("area-risk-high", alert.AlertCode);
        Assert.Equal(OperationalAlertStatus.Open.ToString(), alert.Status);
        Assert.Contains("AlertState=Warning", alert.Message);
    }

    [Fact]
    public async Task SaveAsync_AlarmDropWithinHysteresis_DowngradesToWarningWithoutResolving()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);

        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(
                Guid.Parse("23000000-0000-0000-0000-000000000001"),
                new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
                0.85,
                "Alarm open"),
            5,
            CancellationToken.None);
        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(
                Guid.Parse("23000000-0000-0000-0000-000000000003"),
                new DateTimeOffset(2026, 4, 10, 8, 1, 0, TimeSpan.Zero),
                0.85,
                "Alarm open persists"),
            5,
            CancellationToken.None);

        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(
                Guid.Parse("23000000-0000-0000-0000-000000000002"),
                new DateTimeOffset(2026, 4, 10, 8, 30, 0, TimeSpan.Zero),
                0.66,
                "Alarm drops but remains warning"),
            5,
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var alert = Assert.Single(dbContext.AlertStates);
        Assert.Equal(OperationalAlertStatus.Open.ToString(), alert.Status);
        Assert.Null(alert.ResolvedAt);
        Assert.Contains("AlertState=Warning", alert.Message);
    }

    [Fact]
    public async Task SaveAsync_RiskDrops_ResolvesExistingAlertAndUpdatesProjection()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);

        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.85, "High"),
            6,
            CancellationToken.None);
        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 1, 0, TimeSpan.Zero), 0.85, "High persists"),
            6,
            CancellationToken.None);

        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), 0.20, "Improved"),
            3,
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var areaState = Assert.Single(dbContext.AreaOperationalStates);
        Assert.Equal("Low", areaState.AggregateRiskLevel);
        Assert.Equal("Low", areaState.Severity);
        Assert.Equal(3, areaState.AssessmentCount);

        var alert = Assert.Single(dbContext.AlertStates);
        Assert.Equal(OperationalAlertStatus.Resolved.ToString(), alert.Status);
        Assert.NotNull(alert.ResolvedAt);
    }

    [Fact]
    public async Task MarkUnavailableAsync_PreservesOpenAlertWithoutFabricatingZeroScore()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);
        var openedAt = new DateTimeOffset(2026, 7, 19, 21, 58, 0, TimeSpan.Zero);
        var unavailableAt = openedAt.AddMinutes(2);

        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(Guid.NewGuid(), openedAt, 0.82, "Alarm open"),
            1,
            CancellationToken.None);
        await store.SaveAsync(
            seed.AreaId,
            new AreaRiskSnapshot(Guid.NewGuid(), openedAt.AddMinutes(1), 0.82, "Alarm persists"),
            1,
            CancellationToken.None);
        await store.MarkUnavailableAsync(
            seed.AreaId,
            unavailableAt,
            "NoEligibleAssessments",
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var state = Assert.Single(dbContext.AreaOperationalStates);
        Assert.Equal(0, state.AssessmentCount);
        Assert.Equal(OperationalProjectionStatus.Blocked, state.CoverageStatus);
        Assert.Equal(OperationalProjectionStatus.Unavailable, state.FreshnessStatus);

        var alert = Assert.Single(dbContext.AlertStates);
        Assert.Equal(OperationalAlertStatus.Open.ToString(), alert.Status);
        Assert.Null(alert.ResolvedAt);
        Assert.Contains("AlertState=Alarm", alert.Message);
    }

    [Fact]
    public async Task SaveAsync_AfterResolvedAlert_EnforcesCooldownBeforeReopening()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var store = CreateStore(scope);
        var openedAt = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

        await store.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), openedAt, 0.85, "Alarm candidate"), 5, CancellationToken.None);
        await store.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), openedAt.AddMinutes(1), 0.85, "Alarm opens"), 5, CancellationToken.None);
        await store.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), openedAt.AddMinutes(2), 0.20, "Risk recovered"), 5, CancellationToken.None);
        await store.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), openedAt.AddMinutes(3), 0.85, "Spike during cooldown"), 5, CancellationToken.None);

        await using (var duringCooldownContext = scope.CreateDbContext())
        {
            var state = Assert.Single(duringCooldownContext.AreaOperationalStates);
            Assert.Equal("Alarm", state.PendingAlertState);
            Assert.Equal(0, state.PendingAlertCycles);
            Assert.True(state.AlertCooldownUntil > openedAt.AddMinutes(3));

            var alert = Assert.Single(duringCooldownContext.AlertStates);
            Assert.Equal(OperationalAlertStatus.Resolved.ToString(), alert.Status);
            Assert.NotNull(alert.ResolvedAt);
        }

        await store.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), openedAt.AddMinutes(6), 0.85, "Post-cooldown candidate"), 5, CancellationToken.None);
        await store.SaveAsync(seed.AreaId, new AreaRiskSnapshot(Guid.NewGuid(), openedAt.AddMinutes(7), 0.85, "Post-cooldown persists"), 5, CancellationToken.None);

        await using var afterCooldownContext = scope.CreateDbContext();
        var alerts = (await afterCooldownContext.AlertStates.ToArrayAsync())
            .OrderBy(alert => alert.TriggeredAt)
            .ToArray();
        Assert.Equal(2, alerts.Length);
        Assert.Equal(OperationalAlertStatus.Resolved.ToString(), alerts[0].Status);
        Assert.Equal(OperationalAlertStatus.Open.ToString(), alerts[1].Status);
        Assert.Contains("AlertState=Alarm", alerts[1].Message);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentUniqueViolation_RetriesAsUpdate()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"natureprotector-area-projection-tests-{Guid.NewGuid():N}.sqlite");
        await using var bootstrapScope = new SqliteControlDbContextScope(
            useFileDatabase: true,
            databasePath: databasePath);
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(bootstrapScope);
        var snapshot = new AreaRiskSnapshot(
            Guid.Parse("22000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.85,
            "Area remains under elevated risk.");
        var interceptor = new DuplicateInsertOnSaveInterceptor(
            bootstrapScope.PlainOptions,
            context => context.ChangeTracker.Entries<AreaOperationalStateRecord>().Any(entry => entry.State == EntityState.Added),
            (sidecarContext, currentContext, _) =>
            {
                var pending = currentContext.ChangeTracker.Entries<AreaOperationalStateRecord>()
                    .Single(entry => entry.State == EntityState.Added)
                    .Entity;

                sidecarContext.AreaOperationalStates.Add(new AreaOperationalStateRecord
                {
                    Id = Guid.NewGuid(),
                    AreaId = pending.AreaId,
                    ConfigurationVersionId = pending.ConfigurationVersionId,
                    SnapshotTimestamp = pending.SnapshotTimestamp,
                    AggregateRiskScore = pending.AggregateRiskScore,
                    AggregateRiskLevel = pending.AggregateRiskLevel,
                    Severity = pending.Severity,
                    Summary = pending.Summary,
                    AssessmentCount = pending.AssessmentCount,
                    UpdatedAt = pending.UpdatedAt
                });

                return Task.CompletedTask;
            });
        await using var scope = new SqliteControlDbContextScope(
            configureOptions: builder => builder.AddInterceptors(interceptor),
            useFileDatabase: true,
            databasePath: databasePath);
        var store = CreateStore(scope);

        await store.SaveAsync(seed.AreaId, snapshot, 6, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.AreaOperationalStates);
        Assert.Equal(snapshot.AggregateRiskScore, row.AggregateRiskScore);
    }

    private static PostgresAreaOperationalProjectionStore CreateStore(SqliteControlDbContextScope scope)
    {
        return new PostgresAreaOperationalProjectionStore(
            scope.Factory,
            NullLogger<PostgresAreaOperationalProjectionStore>.Instance);
    }

    private static RiskAssessment CreateAssessment(
        Guid id,
        DateTimeOffset timestamp,
        double score,
        string summary)
    {
        return new RiskAssessment(id, timestamp, score, summary);
    }
}
