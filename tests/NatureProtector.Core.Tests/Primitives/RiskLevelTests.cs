using NatureProtector.Core.Primitives;
using Xunit;

namespace NatureProtector.Core.Tests.Primitives;

/// <summary>
/// Unit tests for RiskLevel and its extension helpers.
/// </summary>
public class RiskLevelTests
{
    [Theory]
    [InlineData(RiskLevel.Unknown, false)]
    [InlineData(RiskLevel.Moderate, false)]
    [InlineData(RiskLevel.High, true)]
    [InlineData(RiskLevel.Extreme, true)]
    public void IsHighOrAbove_ReturnsExpectedValue(RiskLevel level, bool expected)
    {
        Assert.Equal(expected, level.IsHighOrAbove());
    }

    [Fact]
    public void IsHigherThan_ReturnsTrue_OnlyForStrictlyHigherLevels()
    {
        Assert.True(RiskLevel.Extreme.IsHigherThan(RiskLevel.High));
        Assert.False(RiskLevel.High.IsHigherThan(RiskLevel.High));
        Assert.False(RiskLevel.Low.IsHigherThan(RiskLevel.VeryHigh));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void FromScore_Throws_WhenScoreIsInvalid(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RiskLevelExtensions.FromScore(score));
    }

    [Theory]
    [InlineData(0.00, RiskLevel.VeryLow)]
    [InlineData(0.09, RiskLevel.VeryLow)]
    [InlineData(0.10, RiskLevel.Low)]
    [InlineData(0.24, RiskLevel.Low)]
    [InlineData(0.25, RiskLevel.Moderate)]
    [InlineData(0.49, RiskLevel.Moderate)]
    [InlineData(0.50, RiskLevel.High)]
    [InlineData(0.69, RiskLevel.High)]
    [InlineData(0.70, RiskLevel.VeryHigh)]
    [InlineData(0.89, RiskLevel.VeryHigh)]
    [InlineData(0.90, RiskLevel.Extreme)]
    [InlineData(1.00, RiskLevel.Extreme)]
    public void FromScore_MapsExpectedThresholds(double score, RiskLevel expected)
    {
        Assert.Equal(expected, RiskLevelExtensions.FromScore(score));
    }
}
