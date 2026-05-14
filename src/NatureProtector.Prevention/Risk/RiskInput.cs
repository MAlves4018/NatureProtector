using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public sealed record RiskInput(
    Guid AreaId,
    Guid SensorId,
    Guid SourceEventId,
    SensorMetricType MetricType,
    double Value,
    MeasurementUnit Unit,
    DateTimeOffset EventTime)
{
    private static readonly IReadOnlyList<string> EmptyQualityFlags = Array.Empty<string>();
    private static readonly IReadOnlyList<ClassifierResult> EmptyClassifierResults = Array.Empty<ClassifierResult>();

    public RiskInputStatus InputStatus { get; init; } = RiskInputStatus.CompleteEligible;

    public RiskEligibilityReason EligibilityReason { get; init; } = RiskEligibilityReason.Eligible;

    public ObservationalConfidenceLevel ObservationalConfidence { get; init; } = ObservationalConfidenceLevel.High;

    public OperationalIntegrityLevel OperationalIntegrity { get; init; } = OperationalIntegrityLevel.Intact;

    public IReadOnlyList<string> QualityFlags { get; init; } = EmptyQualityFlags;

    public IReadOnlyList<ClassifierResult> ClassifierResults { get; init; } = EmptyClassifierResults;

    public static RiskInput FromNormalizedReading(NormalizedReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return CreateBaseInput(reading) with
        {
            QualityFlags = reading.QualityFlags ?? EmptyQualityFlags,
            ClassifierResults = reading.ClassifierResults ?? EmptyClassifierResults
        };
    }

    public static RiskInput FromNormalizedReading(
        NormalizedReading reading,
        RiskEligibilityResult eligibility)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(eligibility);

        return CreateBaseInput(reading) with
        {
            InputStatus = eligibility.Status,
            EligibilityReason = eligibility.ReasonCode,
            ObservationalConfidence = eligibility.ObservationalConfidence,
            OperationalIntegrity = eligibility.OperationalIntegrity,
            QualityFlags = MergeQualityFlags(reading.QualityFlags, eligibility.QualityFlags),
            ClassifierResults = ResolveClassifierResults(reading.ClassifierResults, eligibility.ClassifierResults)
        };
    }

    private static RiskInput CreateBaseInput(NormalizedReading reading)
    {
        return new RiskInput(
            AreaId: reading.AreaId,
            SensorId: reading.SensorId,
            SourceEventId: reading.EventId,
            MetricType: reading.MetricType,
            Value: reading.Value,
            Unit: reading.Unit,
            EventTime: reading.EventTime);
    }

    private static IReadOnlyList<string> MergeQualityFlags(
        IReadOnlyList<string>? readingFlags,
        IReadOnlyList<string>? eligibilityFlags)
    {
        if ((readingFlags is null || readingFlags.Count == 0) &&
            (eligibilityFlags is null || eligibilityFlags.Count == 0))
        {
            return EmptyQualityFlags;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<string>();

        AppendFlags(merged, seen, readingFlags);
        AppendFlags(merged, seen, eligibilityFlags);

        return merged.Count == 0
            ? EmptyQualityFlags
            : merged;
    }

    private static IReadOnlyList<ClassifierResult> ResolveClassifierResults(
        IReadOnlyList<ClassifierResult>? readingResults,
        IReadOnlyList<ClassifierResult>? eligibilityResults)
    {
        if (eligibilityResults is { Count: > 0 })
        {
            return eligibilityResults;
        }

        if (readingResults is { Count: > 0 })
        {
            return readingResults;
        }

        return EmptyClassifierResults;
    }

    private static void AppendFlags(
        List<string> target,
        HashSet<string> seen,
        IReadOnlyList<string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return;
        }

        foreach (var flag in source)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                continue;
            }

            var normalizedFlag = flag.Trim();
            if (seen.Add(normalizedFlag))
            {
                target.Add(normalizedFlag);
            }
        }
    }
}
