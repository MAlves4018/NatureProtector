using FsCheck.Xunit;
using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class ReadingTemporalClassifierTests
{
    [Fact]
    public void Classify_UsesCandidateWindowsFromInterval()
    {
        var interval = TimeSpan.FromSeconds(60);

        Assert.Equal(TimeSpan.FromSeconds(120), ReadingTemporalClassifier.ResolveLatenessThreshold(interval));
        Assert.Equal(TimeSpan.FromSeconds(180), ReadingTemporalClassifier.ResolveReorderWindow(interval));
        Assert.Equal(TimeSpan.FromSeconds(300), ReadingTemporalClassifier.ResolveStaleThreshold(interval));
    }

    [Fact]
    public void Classify_FlagsDelayedAndStaleReadings()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(360));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, TimeSpan.FromSeconds(60)));

        Assert.Equal(ReadingTemporalClassifier.ClassifierName, result.ClassifierName);
        Assert.Equal(ReadingTemporalClassifier.RuleSetVersion, result.RuleSetVersion);
        Assert.Equal(ClassifierStatus.Warning, result.Status);
        Assert.Equal(ClassifierAction.MarkPartial, result.Action);
        Assert.Equal(ClassifierSeverity.High, result.Severity);
        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("Stale", result.QualityFlags);
        Assert.Contains("stale_threshold_exceeded", result.Reasons);
    }

    [Fact]
    public void Classify_AssignsMediumWarningSeverity_ForDuplicateOnlyReadings()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(
            eventTime,
            eventTime.AddSeconds(5),
            SensorOperationalState.Retransmitted);

        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, TimeSpan.FromSeconds(60)));

        Assert.Equal(ReadingTemporalClassifier.ClassifierName, result.ClassifierName);
        Assert.Equal(ReadingTemporalClassifier.RuleSetVersion, result.RuleSetVersion);
        Assert.Equal(ClassifierStatus.Warning, result.Status);
        Assert.Equal(ClassifierAction.MarkPartial, result.Action);
        Assert.Equal(ClassifierSeverity.Medium, result.Severity);
        Assert.Equal(["Duplicate"], result.QualityFlags);
        Assert.Equal(["operational_state_retransmitted"], result.Reasons);
    }

    [Fact]
    public void Classify_FlagsOutOfOrderReadings()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(5));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60),
            latestObservedEventTime: eventTime.AddMinutes(5)));

        Assert.Contains("OutOfOrder", result.QualityFlags);
        Assert.Contains("event_time_before_latest_observed_outside_reorder_window", result.Reasons);
    }

    [Fact]
    public void Classify_AllowsEarlierEventExactlyAtReorderWindow()
    {
        var interval = TimeSpan.FromSeconds(60);
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(5));

        var result = ReadingTemporalClassifier.Classify(
            reading,
            interval,
            latestObservedEventTime: eventTime.Add(ReadingTemporalClassifier.ResolveReorderWindow(interval)));

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_FlagsOutOfOrderJustBeyondReorderWindow()
    {
        var interval = TimeSpan.FromSeconds(60);
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(5));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            interval,
            latestObservedEventTime: eventTime
                .Add(ReadingTemporalClassifier.ResolveReorderWindow(interval))
                .AddTicks(1)));

        Assert.Contains("OutOfOrder", result.QualityFlags);
        Assert.Contains("event_time_before_latest_observed_outside_reorder_window", result.Reasons);
    }

    [Fact]
    public void Classify_AllowsEarlierEventInsideReorderWindow()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(5));

        var result = ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60),
            latestObservedEventTime: eventTime.AddMinutes(2));

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_DoesNotFlagDelayedAtExactLatenessThreshold()
    {
        var interval = TimeSpan.FromSeconds(60);
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(
            eventTime,
            eventTime.Add(ReadingTemporalClassifier.ResolveLatenessThreshold(interval)));

        var result = ReadingTemporalClassifier.Classify(reading, interval);

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_FlagsDelayedJustBeyondLatenessThreshold()
    {
        var interval = TimeSpan.FromSeconds(60);
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(
            eventTime,
            eventTime.Add(ReadingTemporalClassifier.ResolveLatenessThreshold(interval)).AddTicks(1));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, interval));

        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("lateness_threshold_exceeded", result.Reasons);
        Assert.DoesNotContain("Stale", result.QualityFlags);
    }

    [Fact]
    public void Classify_AtExactStaleThresholdIsDelayedButNotStale()
    {
        var interval = TimeSpan.FromSeconds(60);
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(
            eventTime,
            eventTime.Add(ReadingTemporalClassifier.ResolveStaleThreshold(interval)));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, interval));

        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("lateness_threshold_exceeded", result.Reasons);
        Assert.DoesNotContain("Stale", result.QualityFlags);
        Assert.DoesNotContain("stale_threshold_exceeded", result.Reasons);
    }

    [Fact]
    public void Classify_FlagsStaleJustBeyondStaleThreshold()
    {
        var interval = TimeSpan.FromSeconds(60);
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(
            eventTime,
            eventTime.Add(ReadingTemporalClassifier.ResolveStaleThreshold(interval)).AddTicks(1));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, interval));

        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("Stale", result.QualityFlags);
        Assert.Contains("stale_threshold_exceeded", result.Reasons);
    }

    [Fact]
    public void Classify_FlagsClockSkew_WhenIngestTimePrecedesEventTime()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(-1));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60)));

        Assert.Contains("SemanticMismatch", result.QualityFlags);
        Assert.Contains("ingest_time_before_event_time_clock_skew", result.Reasons);
    }

    [Fact]
    public void Classify_FlagsClockSkew_WhenEvaluationClockPrecedesEventTime()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var fixedNow = eventTime.AddSeconds(-1);
        var reading = CreateReading(eventTime, ingestTime: null);

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60),
            timeProvider: new FixedTimeProvider(fixedNow)));

        Assert.Equal(fixedNow, result.EvaluatedAt);
        Assert.Contains("SemanticMismatch", result.QualityFlags);
        Assert.Contains("evaluation_time_before_event_time_clock_skew", result.Reasons);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Classify_FlagsInvalidTemporalPolicy_WhenIntervalIsNotPositive(int intervalSeconds)
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(5));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(intervalSeconds)));

        Assert.Contains("SemanticMismatch", result.QualityFlags);
        Assert.Contains("invalid_temporal_policy_interval_non_positive", result.Reasons);
    }

    [Fact]
    public void Classify_UsesControllableClock_WhenIngestTimeIsMissing()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 18, 12, 30, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(fixedNow);
        var reading = CreateReading(
            new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
            ingestTime: null,
            operationalState: SensorOperationalState.Retransmitted);

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60),
            timeProvider: clock));

        Assert.Equal(fixedNow, result.EvaluatedAt);
        Assert.Contains("Duplicate", result.QualityFlags);
    }

    [Fact]
    public void Classify_CalculatesSpringForwardLagByInstant_NotLocalWallClock()
    {
        var lisbon = FindEuropeLisbonTimeZone();
        Assert.True(lisbon.IsInvalidTime(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Unspecified)));

        var eventTimeBeforeJump = new DateTimeOffset(2026, 3, 29, 0, 58, 0, TimeSpan.Zero);
        var ingestTimeAfterJump = new DateTimeOffset(2026, 3, 29, 2, 4, 0, TimeSpan.FromHours(1));
        var reading = CreateReading(eventTimeBeforeJump, ingestTimeAfterJump);

        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, TimeSpan.FromSeconds(60)));

        Assert.Equal(TimeSpan.FromMinutes(6), ingestTimeAfterJump - eventTimeBeforeJump);
        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("Stale", result.QualityFlags);
    }

    [Fact]
    public void Classify_CalculatesFallBackOrderingByInstant_NotAmbiguousLocalWallClock()
    {
        var lisbon = FindEuropeLisbonTimeZone();
        Assert.True(lisbon.IsAmbiguousTime(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Unspecified)));

        var eventTimeFirstOccurrence = new DateTimeOffset(2026, 10, 25, 1, 50, 0, TimeSpan.FromHours(1));
        var latestObservedSecondOccurrence = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTimeFirstOccurrence, eventTimeFirstOccurrence.AddSeconds(5));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60),
            latestObservedEventTime: latestObservedSecondOccurrence));

        Assert.True(eventTimeFirstOccurrence < latestObservedSecondOccurrence);
        Assert.Contains("OutOfOrder", result.QualityFlags);
    }

    [Property(MaxTest = 100)]
    public bool ResolveTemporalThresholds_ArePositiveAndOrdered(int rawIntervalSeconds)
    {
        var interval = TimeSpan.FromSeconds(Math.Abs(rawIntervalSeconds % 3600) + 1);
        var lateness = ReadingTemporalClassifier.ResolveLatenessThreshold(interval);
        var reorder = ReadingTemporalClassifier.ResolveReorderWindow(interval);
        var stale = ReadingTemporalClassifier.ResolveStaleThreshold(interval);

        return lateness > TimeSpan.Zero &&
            reorder >= lateness &&
            stale >= reorder;
    }

    [Property(MaxTest = 100)]
    public bool Classify_NominalReadingsInsideLatenessThreshold_HaveNoTemporalFlags(
        int rawIntervalSeconds,
        int rawLagSeconds)
    {
        var interval = TimeSpan.FromSeconds(Math.Abs(rawIntervalSeconds % 3600) + 1);
        var thresholdSeconds = (int)ReadingTemporalClassifier
            .ResolveLatenessThreshold(interval)
            .TotalSeconds;
        var lagSeconds = Math.Abs(rawLagSeconds % (thresholdSeconds + 1));
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(lagSeconds));

        return ReadingTemporalClassifier.Classify(reading, interval).Count == 0;
    }

    [Property(MaxTest = 100)]
    public bool Classify_NominalReadingsBeyondStaleThreshold_AreStale(int rawIntervalSeconds)
    {
        var interval = TimeSpan.FromSeconds(Math.Abs(rawIntervalSeconds % 3600) + 1);
        var lag = ReadingTemporalClassifier.ResolveStaleThreshold(interval).Add(TimeSpan.FromSeconds(1));
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.Add(lag));
        var result = Assert.Single(ReadingTemporalClassifier.Classify(reading, interval));

        return result.QualityFlags.Contains("Stale");
    }

    private static NormalizedReading CreateReading(
        DateTimeOffset eventTime,
        DateTimeOffset? ingestTime,
        SensorOperationalState operationalState = SensorOperationalState.Nominal)
    {
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-temporal",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-Temporal",
            MetricType: SensorMetricType.Temperature,
            Value: 30.0,
            Unit: MeasurementUnit.Celsius,
            Latitude: 39.7,
            Longitude: -7.9,
            OperationalState: operationalState,
            EventTime: eventTime,
            IngestTime: ingestTime);
    }

    private static TimeZoneInfo FindEuropeLisbonTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
