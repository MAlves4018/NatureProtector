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
    public static RiskInput FromNormalizedReading(NormalizedReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return new RiskInput(
            AreaId: reading.AreaId,
            SensorId: reading.SensorId,
            SourceEventId: reading.EventId,
            MetricType: reading.MetricType,
            Value: reading.Value,
            Unit: reading.Unit,
            EventTime: reading.EventTime);
    }
}
