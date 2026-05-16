using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class RiskEligibilityResultTests
{
    [Fact]
    public void CompleteEligible_NoOverrides_ReturnsHighConfidenceEligibleResult()
    {
        var result = RiskEligibilityResult.CompleteEligible();

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.CompleteEligible, result.Status);
        Assert.Equal(RiskEligibilityReason.Eligible, result.ReasonCode);
        Assert.Equal([RiskEligibilityReason.Eligible], result.Reasons);
        Assert.Equal(ObservationalConfidenceLevel.High, result.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Intact, result.OperationalIntegrity);
        Assert.Empty(result.QualityFlags);
        Assert.Empty(result.ClassifierResults);
        Assert.Null(result.Message);
    }

    [Fact]
    public void CompleteEligible_MessageIsWhitespace_NormalizesMessageToNull()
    {
        var result = RiskEligibilityResult.CompleteEligible("   ");

        Assert.True(result.IsEligible);
        Assert.Null(result.Message);
    }

    [Fact]
    public void PartialButUsable_NonEligibleReason_ReturnsDegradedEligibleResult()
    {
        var result = RiskEligibilityResult.PartialButUsable(
            RiskEligibilityReason.DelayedReading,
            "  delayed but usable  ",
            qualityFlags: ["Delayed"]);

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.PartialButUsable, result.Status);
        Assert.Equal(RiskEligibilityReason.DelayedReading, result.ReasonCode);
        Assert.Equal(ObservationalConfidenceLevel.Medium, result.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Degraded, result.OperationalIntegrity);
        Assert.Equal("delayed but usable", result.Message);
        Assert.Equal(["Delayed"], result.QualityFlags);
    }

    [Fact]
    public void PartialButUsable_EligibleReason_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            RiskEligibilityResult.PartialButUsable(RiskEligibilityReason.Eligible));

        Assert.Equal("reasonCode", exception.ParamName);
        Assert.Contains("Partial results must include a non-eligible reason code.", exception.Message);
    }

    [Fact]
    public void Blocked_NonEligibleReason_ReturnsCompromisedIneligibleResult()
    {
        var result = RiskEligibilityResult.Blocked(
            RiskEligibilityReason.MissingRequiredValue,
            "missing area id",
            qualityFlags: ["MissingValue"]);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.MissingRequiredValue, result.ReasonCode);
        Assert.Equal(ObservationalConfidenceLevel.Low, result.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Compromised, result.OperationalIntegrity);
        Assert.Equal(["MissingValue"], result.QualityFlags);
    }

    [Fact]
    public void Blocked_EligibleReason_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            RiskEligibilityResult.Blocked(RiskEligibilityReason.Eligible));

        Assert.Equal("reasonCode", exception.ParamName);
        Assert.Contains("Blocked results must include a non-eligible reason code.", exception.Message);
    }

    [Fact]
    public void NotEligible_NonEligibleReason_ReturnsBlockedResult()
    {
        var result = RiskEligibilityResult.NotEligible(
            RiskEligibilityReason.UnsupportedMetric,
            "unsupported metric");

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.UnsupportedMetric, result.ReasonCode);
        Assert.Equal("unsupported metric", result.Message);
    }

    [Fact]
    public void NotEligible_EligibleReason_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            RiskEligibilityResult.NotEligible(RiskEligibilityReason.Eligible));

        Assert.Equal("reasonCode", exception.ParamName);
        Assert.Contains("Use the Eligible singleton for eligible results.", exception.Message);
    }

    [Fact]
    public void Ineligible_NonEligibleReason_ReturnsSameSemanticsAsNotEligible()
    {
        var result = RiskEligibilityResult.Ineligible(
            RiskEligibilityReason.InvalidOperationalState,
            "invalid state");

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.InvalidOperationalState, result.ReasonCode);
        Assert.Equal("invalid state", result.Message);
    }
}
