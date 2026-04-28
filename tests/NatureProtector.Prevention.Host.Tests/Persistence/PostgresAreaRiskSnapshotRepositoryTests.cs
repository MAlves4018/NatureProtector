using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Core.Risk;
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
}
