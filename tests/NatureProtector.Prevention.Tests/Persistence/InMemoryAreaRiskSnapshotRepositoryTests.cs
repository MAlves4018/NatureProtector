using NatureProtector.Core.Risk;
using NatureProtector.Prevention.Persistence;

namespace NatureProtector.Prevention.Tests.Persistence;

public sealed class InMemoryAreaRiskSnapshotRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Throws_WhenSnapshotIsNull()
    {
        var repository = new InMemoryAreaRiskSnapshotRepository();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveAsync(
            areaId: Guid.NewGuid(),
            snapshot: null!,
            assessmentCount: 0,
            cancellationToken: CancellationToken.None));

        Assert.Equal("snapshot", ex.ParamName);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsNull_WhenAreaHasNoSnapshot()
    {
        var repository = new InMemoryAreaRiskSnapshotRepository();

        var snapshot = await repository.GetLatestAsync(
            areaId: Guid.NewGuid(),
            cancellationToken: CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task SaveAsync_StoresSnapshot_ForArea()
    {
        var repository = new InMemoryAreaRiskSnapshotRepository();
        var areaId = Guid.NewGuid();
        var snapshot = CreateSnapshot(0.65);

        await repository.SaveAsync(areaId, snapshot, 1, CancellationToken.None);

        var stored = await repository.GetLatestAsync(areaId, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(snapshot.Id, stored.Id);
        Assert.Equal(snapshot.AggregateRiskScore, stored.AggregateRiskScore);
    }

    [Fact]
    public async Task SaveAsync_ReplacesPreviousSnapshot_ForSameArea()
    {
        var repository = new InMemoryAreaRiskSnapshotRepository();
        var areaId = Guid.NewGuid();
        var first = CreateSnapshot(0.25);
        var second = CreateSnapshot(0.85);

        await repository.SaveAsync(areaId, first, 1, CancellationToken.None);
        await repository.SaveAsync(areaId, second, 2, CancellationToken.None);

        var stored = await repository.GetLatestAsync(areaId, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(second.Id, stored.Id);
    }

    [Fact]
    public async Task GetLatestAsync_IsolatesAreas()
    {
        var repository = new InMemoryAreaRiskSnapshotRepository();
        var targetAreaId = Guid.NewGuid();
        var otherAreaId = Guid.NewGuid();
        var targetSnapshot = CreateSnapshot(0.45);
        var otherSnapshot = CreateSnapshot(0.90);

        await repository.SaveAsync(targetAreaId, targetSnapshot, 1, CancellationToken.None);
        await repository.SaveAsync(otherAreaId, otherSnapshot, 1, CancellationToken.None);

        var stored = await repository.GetLatestAsync(targetAreaId, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(targetSnapshot.Id, stored.Id);
    }

    private static AreaRiskSnapshot CreateSnapshot(double score)
    {
        return new AreaRiskSnapshot(
            id: Guid.NewGuid(),
            timestamp: DateTimeOffset.UtcNow,
            aggregateRiskScore: score,
            summary: "Snapshot");
    }
}
