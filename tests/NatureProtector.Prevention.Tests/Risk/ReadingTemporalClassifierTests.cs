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

        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("Stale", result.QualityFlags);
        Assert.Contains("stale_threshold_exceeded", result.Reasons);
    }

    [Fact]
    public void Classify_FlagsOutOfOrderReadings()
    {
        var eventTime = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(eventTime, eventTime.AddSeconds(5));

        var result = Assert.Single(ReadingTemporalClassifier.Classify(
            reading,
            TimeSpan.FromSeconds(60),
            latestObservedEventTime: eventTime.AddMinutes(1)));

        Assert.Contains("OutOfOrder", result.QualityFlags);
        Assert.Contains("event_time_before_latest_observed", result.Reasons);
    }

    private static NormalizedReading CreateReading(
        DateTimeOffset eventTime,
        DateTimeOffset ingestTime)
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
            OperationalState: SensorOperationalState.Nominal,
            EventTime: eventTime,
            IngestTime: ingestTime);
    }
}
