using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class ReadingRangeClassifierTests
{
    [Fact]
    public void Classify_TemperatureOutsideCandidateRange_FailsWithTypedFlags()
    {
        var result = Assert.Single(ReadingRangeClassifier.Classify(CreateReading(
            SensorMetricType.Temperature,
            MeasurementUnit.Celsius,
            80.0)));

        Assert.Equal(ClassifierStatus.Failed, result.Status);
        Assert.Equal(ClassifierAction.Block, result.Action);
        Assert.Contains(QualityFlag.Outlier, result.TypedQualityFlags);
        Assert.Contains(QualityFlag.RangeClipping, result.TypedQualityFlags);
    }

    [Fact]
    public void Classify_HumidityInsideCandidateRange_ReturnsNoClassifier()
    {
        Assert.Empty(ReadingRangeClassifier.Classify(CreateReading(
            SensorMetricType.Humidity,
            MeasurementUnit.Percent,
            45.0)));
    }

    private static NormalizedReading CreateReading(
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value)
    {
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "range-test",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "range-sensor",
            MetricType: metricType,
            Value: value,
            Unit: unit,
            Latitude: 39.7,
            Longitude: -7.9,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: DateTimeOffset.UtcNow,
            IngestTime: DateTimeOffset.UtcNow);
    }
}
