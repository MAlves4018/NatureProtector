using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class RiskInputTests
{
    [Fact]
    public void FromNormalizedReading_UsesRiskRelevantFields()
    {
        var reading = new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-01",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-02",
            MetricType: SensorMetricType.Temperature,
            Value: 33.4,
            Unit: MeasurementUnit.Celsius,
            Latitude: 39.75,
            Longitude: -7.92,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: new DateTimeOffset(2026, 4, 30, 10, 30, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 30, 10, 30, 5, TimeSpan.Zero));

        var input = RiskInput.FromNormalizedReading(reading);

        Assert.Equal(reading.AreaId, input.AreaId);
        Assert.Equal(reading.SensorId, input.SensorId);
        Assert.Equal(reading.EventId, input.SourceEventId);
        Assert.Equal(reading.MetricType, input.MetricType);
        Assert.Equal(reading.Value, input.Value);
        Assert.Equal(reading.Unit, input.Unit);
        Assert.Equal(reading.EventTime, input.EventTime);
    }
}
