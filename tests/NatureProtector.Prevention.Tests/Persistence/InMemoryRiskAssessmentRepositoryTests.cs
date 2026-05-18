using NatureProtector.Core.Risk;
using NatureProtector.Prevention.Persistence;

namespace NatureProtector.Prevention.Tests.Persistence;

public sealed class InMemoryRiskAssessmentRepositoryTests
{
    [Fact]
    public async Task AddAsync_Throws_WhenAssessmentIsNull()
    {
        var repository = new InMemoryRiskAssessmentRepository();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            assessment: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("assessment", ex.ParamName);
    }

    [Fact]
    public async Task GetByAreaAsync_ReturnsEmptyCollection_WhenAreaHasNoItems()
    {
        var repository = new InMemoryRiskAssessmentRepository();

        var result = await repository.GetByAreaAsync(
            areaId: Guid.NewGuid(),
            cancellationToken: CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByAreaAsync_ReturnsAssessmentsOrderedByTimestamp()
    {
        var repository = new InMemoryRiskAssessmentRepository();
        var areaId = Guid.NewGuid();
        var latest = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(2), 0.80);
        var earliest = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(-2), 0.20);
        var middle = CreateAssessment(DateTimeOffset.UtcNow, 0.50);

        await repository.AddAsync(areaId, Guid.NewGuid(), Guid.NewGuid(), latest, CancellationToken.None);
        await repository.AddAsync(areaId, Guid.NewGuid(), Guid.NewGuid(), earliest, CancellationToken.None);
        await repository.AddAsync(areaId, Guid.NewGuid(), Guid.NewGuid(), middle, CancellationToken.None);

        var result = await repository.GetByAreaAsync(areaId, CancellationToken.None);

        Assert.Equal(new[] { earliest.Id, middle.Id, latest.Id }, result.Select(x => x.Id));
    }

    [Fact]
    public async Task GetByAreaAsync_IsolatesAreas()
    {
        var repository = new InMemoryRiskAssessmentRepository();
        var targetAreaId = Guid.NewGuid();
        var otherAreaId = Guid.NewGuid();
        var targetAssessment = CreateAssessment(DateTimeOffset.UtcNow, 0.60);
        var otherAssessment = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(1), 0.30);

        await repository.AddAsync(targetAreaId, Guid.NewGuid(), Guid.NewGuid(), targetAssessment, CancellationToken.None);
        await repository.AddAsync(otherAreaId, Guid.NewGuid(), Guid.NewGuid(), otherAssessment, CancellationToken.None);

        var result = await repository.GetByAreaAsync(targetAreaId, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(targetAssessment.Id, item.Id);
    }

    [Fact]
    public async Task GetLatestByAreaAsync_ReturnsLatestAssessmentPerSensor()
    {
        var repository = new InMemoryRiskAssessmentRepository();
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var first = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(-2), 0.20);
        var second = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(-1), 0.80);
        var otherSensor = CreateAssessment(DateTimeOffset.UtcNow, 0.55);

        await repository.AddAsync(areaId, sensorId, Guid.NewGuid(), first, CancellationToken.None);
        await repository.AddAsync(areaId, sensorId, Guid.NewGuid(), second, CancellationToken.None);
        await repository.AddAsync(areaId, Guid.NewGuid(), Guid.NewGuid(), otherSensor, CancellationToken.None);

        var result = await repository.GetLatestByAreaAsync(areaId, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Id == first.Id);
        Assert.Contains(result, x => x.Id == second.Id);
        Assert.Contains(result, x => x.Id == otherSensor.Id);
    }

    [Fact]
    public async Task GetLatestByAreaAsync_IsolatesAreas()
    {
        var repository = new InMemoryRiskAssessmentRepository();
        var targetAreaId = Guid.NewGuid();
        var otherAreaId = Guid.NewGuid();
        var targetAssessment = CreateAssessment(DateTimeOffset.UtcNow, 0.60);
        var otherAssessment = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(1), 0.30);

        await repository.AddAsync(targetAreaId, Guid.NewGuid(), Guid.NewGuid(), targetAssessment, CancellationToken.None);
        await repository.AddAsync(otherAreaId, Guid.NewGuid(), Guid.NewGuid(), otherAssessment, CancellationToken.None);

        var result = await repository.GetLatestByAreaAsync(targetAreaId, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(targetAssessment.Id, item.Id);
    }

    [Fact]
    public async Task GetLatestByAreaAsync_WithSimulationRunId_IsolatesRuns()
    {
        var repository = new InMemoryRiskAssessmentRepository();
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var runAAssessment = CreateAssessment(DateTimeOffset.UtcNow, 0.20);
        var runBAssessment = CreateAssessment(DateTimeOffset.UtcNow.AddMinutes(1), 0.90);

        await repository.AddAsync(areaId, sensorId, Guid.NewGuid(), runAAssessment, CancellationToken.None, runA);
        await repository.AddAsync(areaId, sensorId, Guid.NewGuid(), runBAssessment, CancellationToken.None, runB);

        var result = await repository.GetLatestByAreaAsync(areaId, CancellationToken.None, runA);

        var item = Assert.Single(result);
        Assert.Equal(runAAssessment.Id, item.Id);
    }

    [Fact]
    public async Task GetLatestByAreaAsync_WithoutSimulationRunId_IncludesLegacyRows()
    {
        var repository = new InMemoryRiskAssessmentRepository();
        var areaId = Guid.NewGuid();
        var legacyAssessment = CreateAssessment(DateTimeOffset.UtcNow, 0.60);

        await repository.AddAsync(areaId, Guid.NewGuid(), Guid.NewGuid(), legacyAssessment, CancellationToken.None);

        var result = await repository.GetLatestByAreaAsync(areaId, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(legacyAssessment.Id, item.Id);
    }

    private static RiskAssessment CreateAssessment(DateTimeOffset timestamp, double score)
    {
        return new RiskAssessment(
            id: Guid.NewGuid(),
            timestamp: timestamp,
            riskScore: score,
            explanationSummary: "Assessment");
    }
}
