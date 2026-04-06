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

        await repository.AddAsync(areaId, latest, CancellationToken.None);
        await repository.AddAsync(areaId, earliest, CancellationToken.None);
        await repository.AddAsync(areaId, middle, CancellationToken.None);

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

        await repository.AddAsync(targetAreaId, targetAssessment, CancellationToken.None);
        await repository.AddAsync(otherAreaId, otherAssessment, CancellationToken.None);

        var result = await repository.GetByAreaAsync(targetAreaId, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(targetAssessment.Id, item.Id);
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
