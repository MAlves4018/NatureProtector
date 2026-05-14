namespace NatureProtector.Prevention.Risk;

public sealed record ClassifierResult(
    string ClassifierName,
    ClassifierStatus Status,
    ClassifierSeverity Severity,
    IReadOnlyList<string> QualityFlags,
    IReadOnlyList<string> Reasons,
    DateTimeOffset EvaluatedAt,
    string RuleSetVersion)
{
    public static ClassifierResult Create(
        string classifierName,
        ClassifierStatus status,
        ClassifierSeverity severity,
        IReadOnlyList<string>? qualityFlags,
        IReadOnlyList<string>? reasons,
        DateTimeOffset evaluatedAt,
        string ruleSetVersion)
    {
        if (string.IsNullOrWhiteSpace(classifierName))
        {
            throw new ArgumentException("Classifier name is required.", nameof(classifierName));
        }

        if (string.IsNullOrWhiteSpace(ruleSetVersion))
        {
            throw new ArgumentException("Rule set version is required.", nameof(ruleSetVersion));
        }

        return new ClassifierResult(
            classifierName.Trim(),
            status,
            severity,
            qualityFlags ?? Array.Empty<string>(),
            reasons ?? Array.Empty<string>(),
            evaluatedAt,
            ruleSetVersion.Trim());
    }

    public static ClassifierAggregationSummary AggregateForEligibility(
        IReadOnlyCollection<ClassifierResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return new ClassifierAggregationSummary(
                HasFailure: false,
                HasWarning: false,
                HighestSeverity: ClassifierSeverity.Info,
                DistinctQualityFlags: Array.Empty<string>(),
                DistinctReasons: Array.Empty<string>());
        }

        var hasFailure = results.Any(r => r.Status == ClassifierStatus.Failed);
        var hasWarning = results.Any(r => r.Status == ClassifierStatus.Warning);
        var highestSeverity = results.Max(r => r.Severity);
        var flags = results.SelectMany(r => r.QualityFlags)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var reasons = results.SelectMany(r => r.Reasons)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ClassifierAggregationSummary(
            hasFailure,
            hasWarning,
            highestSeverity,
            flags,
            reasons);
    }
}

public sealed record ClassifierAggregationSummary(
    bool HasFailure,
    bool HasWarning,
    ClassifierSeverity HighestSeverity,
    IReadOnlyList<string> DistinctQualityFlags,
    IReadOnlyList<string> DistinctReasons);
