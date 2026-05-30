using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class CandidateParameterSetV1Tests
{
    [Fact]
    public void Version_IsExplicitCandidateParameterSet()
    {
        Assert.Equal("Candidate Parameter Set V1.0", CandidateParameterSetV1.Version);
    }

    [Fact]
    public void BaselineWeights_SumToOne()
    {
        var total =
            CandidateParameterSetV1.MeteorologyWeight +
            CandidateParameterSetV1.DroughtWeight +
            CandidateParameterSetV1.TerritoryWeight;

        Assert.Equal(1.0, total, precision: 6);
    }

    [Fact]
    public void TerritoryWeights_SumToOne()
    {
        var total =
            CandidateParameterSetV1.TerritoryHazardWeight +
            CandidateParameterSetV1.TerritoryFuelWeight +
            CandidateParameterSetV1.TerritoryGeomorphologyWeight;

        Assert.Equal(1.0, total, precision: 6);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0.004, 0)]
    [InlineData(0.005, 1)]
    [InlineData(0.604, 60)]
    [InlineData(0.605, 61)]
    [InlineData(1.2, 100)]
    public void ToScore100_UsesRoundedCompatibilityScale(double normalizedScore, int expectedScore100)
    {
        Assert.Equal(expectedScore100, CandidateParameterSetV1.ToScore100(normalizedScore));
    }

    [Fact]
    public void TemporalWindows_FollowV1CandidateRules()
    {
        var interval = TimeSpan.FromSeconds(60);

        Assert.Equal(TimeSpan.FromSeconds(120), CandidateParameterSetV1.ResolveLatenessThreshold(interval));
        Assert.Equal(TimeSpan.FromSeconds(180), CandidateParameterSetV1.ResolveReorderWindow(interval));
        Assert.Equal(TimeSpan.FromSeconds(300), CandidateParameterSetV1.ResolveStaleThreshold(interval));
    }

    [Fact]
    public void AlertCooldown_FollowsV1CandidateRule()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(180),
            CandidateParameterSetV1.ResolveAlertCooldown(TimeSpan.FromSeconds(30)));

        Assert.Equal(
            TimeSpan.FromSeconds(300),
            CandidateParameterSetV1.ResolveAlertCooldown(TimeSpan.FromSeconds(100)));
    }
}
