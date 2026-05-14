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
}
