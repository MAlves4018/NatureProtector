using NatureProtector.Core.Primitives;
using Xunit;

namespace NatureProtector.Core.Tests.Primitives;

/// <summary>
/// Unit tests for Severity and its extension helpers.
/// </summary>
public class SeverityTests
{
    [Theory]
    [InlineData(Severity.Info, false)]
    [InlineData(Severity.Medium, false)]
    [InlineData(Severity.High, false)]
    [InlineData(Severity.Critical, true)]
    [InlineData(Severity.Emergency, true)]
    public void IsCriticalOrAbove_ReturnsExpectedValue(Severity severity, bool expected)
    {
        Assert.Equal(expected, severity.IsCriticalOrAbove());
    }

    [Fact]
    public void IsWorseThan_ReturnsTrue_OnlyForStrictlyHigherSeverity()
    {
        Assert.True(Severity.Critical.IsWorseThan(Severity.High));
        Assert.True(Severity.Emergency.IsWorseThan(Severity.Critical));
        Assert.False(Severity.High.IsWorseThan(Severity.High));
        Assert.False(Severity.Low.IsWorseThan(Severity.Critical));
    }

    [Theory]
    [InlineData(RiskLevel.Unknown, Severity.Info)]
    [InlineData(RiskLevel.VeryLow, Severity.Info)]
    [InlineData(RiskLevel.Low, Severity.Low)]
    [InlineData(RiskLevel.Moderate, Severity.Medium)]
    [InlineData(RiskLevel.High, Severity.High)]
    [InlineData(RiskLevel.VeryHigh, Severity.Critical)]
    [InlineData(RiskLevel.Extreme, Severity.Emergency)]
    public void FromRiskLevel_MapsExpectedSeverity(RiskLevel riskLevel, Severity expected)
    {
        Assert.Equal(expected, SeverityExtensions.FromRiskLevel(riskLevel));
    }
}