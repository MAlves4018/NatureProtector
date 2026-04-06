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
    public void BuildSnapshot_ReturnsZeroSnapshot_WhenAssessmentsIsEmpty()
    {
        var snapshotTime = DateTimeOffset.UtcNow;

        var snapshot = _service.BuildSnapshot(
            assessments: Array.Empty<RiskAssessment>(),
            snapshotTime: snapshotTime);

        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(snapshotTime, snapshot.Timestamp);
        Assert.Equal(0.0, snapshot.AggregateRiskScore);
        Assert.Equal("No accepted assessments are available for this area.", snapshot.Summary);
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

        Assert.Equal(0.75, snapshot.AggregateRiskScore, precision: 3);
        Assert.Contains("Aggregated from 2 assessments", snapshot.Summary);
        Assert.Contains("2 at High or above.", snapshot.Summary);
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
