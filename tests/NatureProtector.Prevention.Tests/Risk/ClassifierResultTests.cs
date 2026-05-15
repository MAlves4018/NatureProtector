using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class ClassifierResultTests
{
    [Fact]
    public void ClassifierResult_CarriesFlagsAndReasons()
    {
        var result = ClassifierResult.Create(
            classifierName: "quality_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Medium,
            qualityFlags: ["Stale", "OutOfOrder"],
            reasons: ["timestamp_gap", "late_arrival"],
            evaluatedAt: new DateTimeOffset(2026, 5, 11, 10, 0, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");

        Assert.Equal("quality_classifier", result.ClassifierName);
        Assert.Equal(ClassifierStatus.Warning, result.Status);
        Assert.Equal(ClassifierSeverity.Medium, result.Severity);
        Assert.Equal(["Stale", "OutOfOrder"], result.QualityFlags);
        Assert.Equal(["timestamp_gap", "late_arrival"], result.Reasons);
        Assert.Equal("v1.0", result.RuleSetVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingClassifierName_ThrowsArgumentException(string? classifierName)
    {
        var exception = Assert.Throws<ArgumentException>(() => ClassifierResult.Create(
            classifierName: classifierName!,
            status: ClassifierStatus.Passed,
            severity: ClassifierSeverity.Info,
            qualityFlags: null,
            reasons: null,
            evaluatedAt: DateTimeOffset.UnixEpoch,
            ruleSetVersion: "v1"));

        Assert.Equal("classifierName", exception.ParamName);
        Assert.Contains("Classifier name is required.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingRuleSetVersion_ThrowsArgumentException(string? ruleSetVersion)
    {
        var exception = Assert.Throws<ArgumentException>(() => ClassifierResult.Create(
            classifierName: "classifier",
            status: ClassifierStatus.Passed,
            severity: ClassifierSeverity.Info,
            qualityFlags: null,
            reasons: null,
            evaluatedAt: DateTimeOffset.UnixEpoch,
            ruleSetVersion: ruleSetVersion!));

        Assert.Equal("ruleSetVersion", exception.ParamName);
        Assert.Contains("Rule set version is required.", exception.Message);
    }

    [Fact]
    public void Create_NullCollectionsAndWhitespaceNames_NormalizesResult()
    {
        var evaluatedAt = new DateTimeOffset(2026, 5, 11, 10, 5, 0, TimeSpan.Zero);

        var result = ClassifierResult.Create(
            classifierName: "  semantic_classifier  ",
            status: ClassifierStatus.Passed,
            severity: ClassifierSeverity.Low,
            qualityFlags: null,
            reasons: null,
            evaluatedAt: evaluatedAt,
            ruleSetVersion: "  v1.0  ");

        Assert.Equal("semantic_classifier", result.ClassifierName);
        Assert.Equal("v1.0", result.RuleSetVersion);
        Assert.Empty(result.QualityFlags);
        Assert.Empty(result.Reasons);
        Assert.Equal(evaluatedAt, result.EvaluatedAt);
    }

    [Fact]
    public void ClassifierResult_CanRepresentTechnicalSemanticAndTemporalClassification()
    {
        var evaluatedAt = new DateTimeOffset(2026, 5, 11, 10, 10, 0, TimeSpan.Zero);

        var technical = ClassifierResult.Create(
            classifierName: "technical_classifier",
            status: ClassifierStatus.Passed,
            severity: ClassifierSeverity.Info,
            qualityFlags: [],
            reasons: ["payload_schema_valid"],
            evaluatedAt: evaluatedAt,
            ruleSetVersion: "v1.0");

        var semantic = ClassifierResult.Create(
            classifierName: "semantic_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Low,
            qualityFlags: ["SemanticMismatch"],
            reasons: ["sensor_area_mismatch_soft"],
            evaluatedAt: evaluatedAt,
            ruleSetVersion: "v1.0");

        var temporal = ClassifierResult.Create(
            classifierName: "temporal_classifier",
            status: ClassifierStatus.Failed,
            severity: ClassifierSeverity.High,
            qualityFlags: ["OutOfOrder"],
            reasons: ["event_time_regression"],
            evaluatedAt: evaluatedAt,
            ruleSetVersion: "v1.0");

        Assert.Equal(ClassifierStatus.Passed, technical.Status);
        Assert.Equal(ClassifierStatus.Warning, semantic.Status);
        Assert.Equal(ClassifierStatus.Failed, temporal.Status);
        Assert.Equal(ClassifierSeverity.High, temporal.Severity);
    }

    [Fact]
    public void MultipleClassifierResults_CanBeAggregatedForEligibility()
    {
        var evaluatedAt = new DateTimeOffset(2026, 5, 11, 10, 20, 0, TimeSpan.Zero);
        var results = new[]
        {
            ClassifierResult.Create(
                classifierName: "technical_classifier",
                status: ClassifierStatus.Passed,
                severity: ClassifierSeverity.Info,
                qualityFlags: ["Delayed"],
                reasons: ["transport_ok"],
                evaluatedAt: evaluatedAt,
                ruleSetVersion: "v1.0"),
            ClassifierResult.Create(
                classifierName: "semantic_classifier",
                status: ClassifierStatus.Warning,
                severity: ClassifierSeverity.Medium,
                qualityFlags: ["SemanticMismatch", "Delayed"],
                reasons: ["domain_warning"],
                evaluatedAt: evaluatedAt,
                ruleSetVersion: "v1.0"),
            ClassifierResult.Create(
                classifierName: "temporal_classifier",
                status: ClassifierStatus.Failed,
                severity: ClassifierSeverity.Critical,
                qualityFlags: ["OutOfOrder"],
                reasons: ["temporal_failure"],
                evaluatedAt: evaluatedAt,
                ruleSetVersion: "v1.0")
        };

        var aggregate = ClassifierResult.AggregateForEligibility(results);

        Assert.True(aggregate.HasFailure);
        Assert.True(aggregate.HasWarning);
        Assert.Equal(ClassifierSeverity.Critical, aggregate.HighestSeverity);
        Assert.Contains("Delayed", aggregate.DistinctQualityFlags);
        Assert.Contains("OutOfOrder", aggregate.DistinctQualityFlags);
        Assert.Contains("temporal_failure", aggregate.DistinctReasons);
    }

    [Fact]
    public void AggregateForEligibility_EmptyResults_ReturnsNeutralSummary()
    {
        var aggregate = ClassifierResult.AggregateForEligibility([]);

        Assert.False(aggregate.HasFailure);
        Assert.False(aggregate.HasWarning);
        Assert.Equal(ClassifierSeverity.Info, aggregate.HighestSeverity);
        Assert.Empty(aggregate.DistinctQualityFlags);
        Assert.Empty(aggregate.DistinctReasons);
    }

    [Fact]
    public void AggregateForEligibility_NullResults_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ClassifierResult.AggregateForEligibility(null!));

        Assert.Equal("results", exception.ParamName);
    }
}
