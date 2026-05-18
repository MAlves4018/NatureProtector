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
        DateTimeOffset? latestObservedEventTime = null)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var flags = new List<QualityFlag>();
        var reasons = new List<string>();

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
            flags.Add(QualityFlag.OutOfOrder);
            reasons.Add("event_time_before_latest_observed");
        }

        if (reading.IngestTime.HasValue)
        {
            var lag = reading.IngestTime.Value - reading.EventTime;
            if (lag > ResolveLatenessThreshold(interval))
            {
                flags.Add(QualityFlag.Delayed);
                reasons.Add("lateness_threshold_exceeded");
            }

            if (lag > ResolveStaleThreshold(interval))
            {
                flags.Add(QualityFlag.Stale);
                reasons.Add("stale_threshold_exceeded");
            }
        }

        if (flags.Count == 0)
        {
            return Array.Empty<ClassifierResult>();
        }

        return
        [
            ClassifierResult.Create(
                ClassifierName,
                flags.Contains(QualityFlag.Stale) ? ClassifierStatus.Warning : ClassifierStatus.Warning,
                flags.Contains(QualityFlag.Stale) ? ClassifierSeverity.High : ClassifierSeverity.Medium,
                flags.Select(flag => flag.ToWireName()).Distinct().ToArray(),
                reasons.Distinct(StringComparer.Ordinal).ToArray(),
                reading.IngestTime ?? DateTimeOffset.UtcNow,
                RuleSetVersion)
        ];
    }

    public static TimeSpan ResolveLatenessThreshold(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(Math.Max(2 * interval.TotalSeconds, 120));
    }

    public static TimeSpan ResolveReorderWindow(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(Math.Max(3 * interval.TotalSeconds, 180));
    }

    public static TimeSpan ResolveStaleThreshold(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(Math.Max(5 * interval.TotalSeconds, 300));
    }
}
