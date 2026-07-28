namespace NatureProtector.Simulator.Host.TemporalLoad;

public sealed record TemporalScheduleEntry(
    int EventIndex,
    string SegmentId,
    string SegmentKind,
    TimeSpan DueOffset,
    double RequestedRate);

public sealed class TemporalLoadSchedule
{
    public TemporalLoadSchedule(IReadOnlyList<TemporalScheduleEntry> entries, TimeSpan activeDuration)
    {
        Entries = entries;
        ActiveDuration = activeDuration;
    }

    public IReadOnlyList<TemporalScheduleEntry> Entries { get; }

    public TimeSpan ActiveDuration { get; }
}

public static class TemporalLoadScheduler
{
    public static TemporalLoadSchedule Build(TemporalWorkloadDefinition workload)
    {
        ArgumentNullException.ThrowIfNull(workload);

        if (workload.Segments.Count == 0)
        {
            throw new InvalidOperationException("Temporal workload must contain at least one segment.");
        }

        var entries = new List<TemporalScheduleEntry>();
        var activeDuration = TimeSpan.Zero;
        var segmentOffset = TimeSpan.FromSeconds(Math.Max(0, workload.WarmUpSeconds));
        foreach (var segment in workload.Segments)
        {
            ValidateSegment(segment);
            AppendSegment(entries, segment, segmentOffset);
            segmentOffset += TimeSpan.FromSeconds(segment.DurationSeconds);
            activeDuration += TimeSpan.FromSeconds(segment.DurationSeconds);
        }

        return new TemporalLoadSchedule(entries, activeDuration);
    }

    public static TemporalRatePrecision CalculatePrecision(
        double requestedRate,
        int scheduledCount,
        int confirmedCount,
        TimeSpan publishWindow,
        IReadOnlyList<double> actualIntervalsMs,
        IReadOnlyList<double> delaysMs)
    {
        if (requestedRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedRate), requestedRate, "Requested rate must be positive.");
        }

        var seconds = publishWindow.TotalSeconds;
        var actualRate = seconds > 0 ? confirmedCount / seconds : confirmedCount;
        var absoluteError = Math.Abs(actualRate - requestedRate);
        var percentError = absoluteError / requestedRate * 100.0;
        var jitter = actualIntervalsMs.Count == 0
            ? 0
            : actualIntervalsMs.Select(value => Math.Abs(value - (1000.0 / requestedRate))).Average();
        var accumulatedDelay = delaysMs.Count == 0 ? 0 : delaysMs.Sum();

        return new TemporalRatePrecision(
            RequestedRate: requestedRate,
            ScheduledCount: scheduledCount,
            ConfirmedCount: confirmedCount,
            ActualPublishRate: actualRate,
            AbsoluteError: absoluteError,
            PercentError: percentError,
            JitterMs: jitter,
            AccumulatedDelayMs: accumulatedDelay,
            WithinFivePercent: percentError <= 5.0);
    }

    private static void AppendSegment(
        List<TemporalScheduleEntry> entries,
        TemporalWorkloadSegment segment,
        TimeSpan segmentOffset)
    {
        var kind = NormalizeKind(segment.Kind);
        if (kind == "burst")
        {
            var burstCount = segment.BurstCount ?? ScheduledCount(segment.RequestedRate ?? 1.0, segment.DurationSeconds);
            for (var index = 0; index < burstCount; index++)
            {
                entries.Add(new TemporalScheduleEntry(
                    entries.Count,
                    RequiredId(segment),
                    kind,
                    segmentOffset,
                    segment.RequestedRate ?? burstCount / Math.Max(0.001, segment.DurationSeconds)));
            }

            return;
        }

        if (kind == "ramp")
        {
            AppendRamp(entries, segment, segmentOffset);
            return;
        }

        var rate = RequiredRate(segment);
        var count = ScheduledCount(rate, segment.DurationSeconds);
        for (var index = 0; index < count; index++)
        {
            entries.Add(new TemporalScheduleEntry(
                entries.Count,
                RequiredId(segment),
                kind,
                segmentOffset + TimeSpan.FromSeconds(index / rate),
                rate));
        }
    }

    private static void AppendRamp(
        List<TemporalScheduleEntry> entries,
        TemporalWorkloadSegment segment,
        TimeSpan segmentOffset)
    {
        var startRate = segment.StartRate ?? throw new InvalidOperationException(
            $"Ramp segment '{RequiredId(segment)}' must define StartRate.");
        var endRate = segment.EndRate ?? throw new InvalidOperationException(
            $"Ramp segment '{RequiredId(segment)}' must define EndRate.");
        if (startRate <= 0 || endRate <= 0)
        {
            throw new InvalidOperationException($"Ramp segment '{RequiredId(segment)}' rates must be positive.");
        }

        var elapsed = 0.0;
        while (elapsed < segment.DurationSeconds)
        {
            var progress = segment.DurationSeconds <= 0 ? 1.0 : elapsed / segment.DurationSeconds;
            var rate = startRate + ((endRate - startRate) * progress);
            entries.Add(new TemporalScheduleEntry(
                entries.Count,
                RequiredId(segment),
                "ramp",
                segmentOffset + TimeSpan.FromSeconds(elapsed),
                rate));
            elapsed += 1.0 / rate;
        }
    }

    private static void ValidateSegment(TemporalWorkloadSegment segment)
    {
        if (segment.DurationSeconds <= 0)
        {
            throw new InvalidOperationException($"Temporal segment '{RequiredId(segment)}' must have positive duration.");
        }

        _ = RequiredId(segment);
    }

    private static int ScheduledCount(double rate, double durationSeconds)
    {
        if (rate <= 0)
        {
            throw new InvalidOperationException("Temporal segment requested rate must be positive.");
        }

        return Math.Max(1, (int)Math.Floor((rate * durationSeconds) + 0.000001));
    }

    private static double RequiredRate(TemporalWorkloadSegment segment)
    {
        return segment.RequestedRate ?? throw new InvalidOperationException(
            $"Temporal segment '{RequiredId(segment)}' must define RequestedRate.");
    }

    private static string RequiredId(TemporalWorkloadSegment segment)
    {
        return string.IsNullOrWhiteSpace(segment.Id)
            ? throw new InvalidOperationException("Temporal segment id is required.")
            : segment.Id.Trim();
    }

    private static string NormalizeKind(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "constant" : value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "constant" or "spike" or "step" or "hold" or "recovery" or "drain" => "constant",
            "burst" => "burst",
            "ramp" => "ramp",
            _ => throw new InvalidOperationException($"Unsupported temporal segment kind '{value}'.")
        };
    }
}

public sealed record TemporalRatePrecision(
    double RequestedRate,
    int ScheduledCount,
    int ConfirmedCount,
    double ActualPublishRate,
    double AbsoluteError,
    double PercentError,
    double JitterMs,
    double AccumulatedDelayMs,
    bool WithinFivePercent);
