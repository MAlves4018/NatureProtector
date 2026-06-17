using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public static class ReadingTemporalClassifier
{
    public const string ClassifierName = "temporal_v1";
    public const string RuleSetVersion = "candidate-temporal-v1";

    public static IReadOnlyList<ClassifierResult> Classify(
        NormalizedReading reading,
        TimeSpan interval,
        DateTimeOffset? latestObservedEventTime = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var flags = new List<QualityFlag>();
        var reasons = new List<string>();
        var evaluatedAt = reading.IngestTime ?? (timeProvider ?? TimeProvider.System).GetUtcNow();

        if (interval <= TimeSpan.Zero)
        {
            flags.Add(QualityFlag.SemanticMismatch);
            reasons.Add("invalid_temporal_policy_interval_non_positive");
            return CreateResult(flags, reasons, evaluatedAt);
        }

        var latenessThreshold = ResolveLatenessThreshold(interval);
        var reorderWindow = ResolveReorderWindow(interval);
        var staleThreshold = ResolveStaleThreshold(interval);

        if (reading.OperationalState == SensorOperationalState.Retransmitted)
        {
            flags.Add(QualityFlag.Duplicate);
            reasons.Add("operational_state_retransmitted");
        }

        if (reading.OperationalState == SensorOperationalState.Delayed)
        {
            flags.Add(QualityFlag.Delayed);
            reasons.Add("operational_state_delayed");
        }

        if (latestObservedEventTime.HasValue && reading.EventTime < latestObservedEventTime.Value)
        {
            var reorderLag = latestObservedEventTime.Value - reading.EventTime;
            if (reorderLag > reorderWindow)
            {
                flags.Add(QualityFlag.OutOfOrder);
                reasons.Add("event_time_before_latest_observed_outside_reorder_window");
            }
        }

        var lag = evaluatedAt - reading.EventTime;
        if (lag < TimeSpan.Zero)
        {
            flags.Add(QualityFlag.SemanticMismatch);
            reasons.Add(reading.IngestTime.HasValue
                ? "ingest_time_before_event_time_clock_skew"
                : "evaluation_time_before_event_time_clock_skew");
        }
        else
        {
            if (lag > latenessThreshold)
            {
                flags.Add(QualityFlag.Delayed);
                reasons.Add("lateness_threshold_exceeded");
            }

            if (lag > staleThreshold)
            {
                flags.Add(QualityFlag.Stale);
                reasons.Add("stale_threshold_exceeded");
            }
        }

        if (flags.Count == 0)
        {
            return Array.Empty<ClassifierResult>();
        }

        return CreateResult(flags, reasons, evaluatedAt);
    }

    private static IReadOnlyList<ClassifierResult> CreateResult(
        IReadOnlyList<QualityFlag> flags,
        IReadOnlyList<string> reasons,
        DateTimeOffset evaluatedAt)
    {
        return
        [
            ClassifierResult.Create(
                ClassifierName,
                ClassifierStatus.Warning,
                flags.Contains(QualityFlag.Stale) || flags.Contains(QualityFlag.SemanticMismatch)
                    ? ClassifierSeverity.High
                    : ClassifierSeverity.Medium,
                flags.Select(flag => flag.ToWireName()).Distinct().ToArray(),
                reasons.Distinct(StringComparer.Ordinal).ToArray(),
                evaluatedAt,
                RuleSetVersion)
        ];
    }

    public static TimeSpan ResolveLatenessThreshold(TimeSpan interval)
    {
        return CandidateParameterSetV1.ResolveLatenessThreshold(interval);
    }

    public static TimeSpan ResolveReorderWindow(TimeSpan interval)
    {
        return CandidateParameterSetV1.ResolveReorderWindow(interval);
    }

    public static TimeSpan ResolveStaleThreshold(TimeSpan interval)
    {
        return CandidateParameterSetV1.ResolveStaleThreshold(interval);
    }
}
