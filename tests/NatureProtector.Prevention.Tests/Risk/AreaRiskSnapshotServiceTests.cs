using NatureProtector.Core.Risk;
using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class AreaRiskSnapshotServiceTests
{
    private readonly AreaRiskSnapshotService _service = new();

    [Fact]
    public void BuildSnapshot_Throws_WhenAssessmentsIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _service.BuildSnapshot(
            assessments: null!,
            snapshotTime: DateTimeOffset.UtcNow));

        Assert.Equal("assessments", ex.ParamName);
    }

    [Fact]
    public void BuildSnapshot_Throws_WhenAssessmentsIsEmpty()
    {
        var snapshotTime = DateTimeOffset.UtcNow;

        var ex = Assert.Throws<ArgumentException>(() => _service.BuildSnapshot(
            assessments: Array.Empty<RiskAssessment>(),
            snapshotTime: snapshotTime));

        Assert.Equal("assessments", ex.ParamName);
        Assert.Contains("area risk is unavailable", ex.Message);
    }

    [Fact]
    public void BuildSnapshot_AggregatesAssessments_WhenItemsExist()
    {
        var snapshotTime = DateTimeOffset.UtcNow;
        var assessments = new[]
        {
            CreateAssessment(snapshotTime.AddMinutes(-2), 0.65),
            CreateAssessment(snapshotTime.AddMinutes(-1), 0.85)
        };

        var snapshot = _service.BuildSnapshot(
            assessments: assessments,
            snapshotTime: snapshotTime);

        Assert.Equal(0.85, snapshot.AggregateRiskScore, precision: 3);
        Assert.Contains("Aggregated from 2 assessments", snapshot.Summary);
        Assert.Contains("2 at High or above", snapshot.Summary);
        Assert.Contains("0.70*p80", snapshot.Summary);
    }

    [Fact]
    public void BuildSnapshot_UsesP80AndMaxAreaAggregation()
    {
        var snapshotTime = DateTimeOffset.UtcNow;
        var assessments = new[]
        {
            CreateAssessment(snapshotTime.AddMinutes(-5), 0.10),
            CreateAssessment(snapshotTime.AddMinutes(-4), 0.20),
            CreateAssessment(snapshotTime.AddMinutes(-3), 0.30),
            CreateAssessment(snapshotTime.AddMinutes(-2), 0.80),
            CreateAssessment(snapshotTime.AddMinutes(-1), 1.00)
        };

        var snapshot = _service.BuildSnapshot(
            assessments: assessments,
            snapshotTime: snapshotTime);

        Assert.Equal(0.86, snapshot.AggregateRiskScore, precision: 3);
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
