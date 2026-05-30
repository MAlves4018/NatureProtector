using NatureProtector.Core.Risk;
using NatureProtector.Prevention.Host.Projection;

namespace NatureProtector.Prevention.Host.Tests.Projection;

public sealed class OperationalProjectionStatusTests
{
    [Theory]
    [InlineData(0, "NoRecentData")]
    [InlineData(1, "LowCoverage")]
    [InlineData(2, "Partial")]
    [InlineData(3, "Complete")]
    public void ResolveCoverage_AssessmentCount_ReturnsExplicitCoverage(int assessmentCount, string expected)
    {
        var actual = OperationalProjectionStatus.ResolveCoverage(assessmentCount);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveCoverage_PartialAssessment_ReturnsPartial()
    {
        var assessment = new RiskAssessment(
            id: Guid.NewGuid(),
            timestamp: DateTimeOffset.UtcNow,
            baseRisk: 0.45,
            adjustedScore: 0.45,
            explanationSummary: "partial",
            meteorologyComponent: 0.4,
            droughtComponent: 0.5,
            territoryComponent: 0.45,
            hazardComponent: 0.5,
            fuelComponent: 0.5,
            geomorphologyComponent: 0.5,
            confidenceFactor: 1,
            integrityFactor: 1,
            dominantDriver: "Mixed",
            parameterSetVersion: "Candidate Parameter Set V1.0",
            calculationStatus: "PartialButUsable",
            limitations: "missing_humidity");

        var actual = OperationalProjectionStatus.ResolveCoverage(assessment);

        Assert.Equal("Partial", actual);
    }

    [Fact]
    public void ResolveFreshness_UsesCandidateStaleAndExpiredWindows()
    {
        var observedAt = new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("Fresh", OperationalProjectionStatus.ResolveFreshness(observedAt.AddSeconds(-300), observedAt, 60));
        Assert.Equal("Stale", OperationalProjectionStatus.ResolveFreshness(observedAt.AddSeconds(-450), observedAt, 60));
        Assert.Equal("Expired", OperationalProjectionStatus.ResolveFreshness(observedAt.AddSeconds(-700), observedAt, 60));
    }

    [Theory]
    [InlineData("Fresh", "Current")]
    [InlineData("Stale", "CarriedForward")]
    [InlineData("Expired", "ExpiredCarryForward")]
    [InlineData("unknown", "NotAvailable")]
    public void ResolveCarryForward_SeparatesFreshnessFromCarryForward(string freshness, string expected)
    {
        var actual = OperationalProjectionStatus.ResolveCarryForward(freshness);

        Assert.Equal(expected, actual);
    }
}
