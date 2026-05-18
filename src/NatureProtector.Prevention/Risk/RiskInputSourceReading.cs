using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public sealed record RiskInputSourceReading(
    Guid EventId,
    Guid SensorId,
    SensorMetricType MetricType,
    double Value,
    MeasurementUnit Unit,
    DateTimeOffset EventTime)
{
    public static RiskInputSourceReading FromNormalizedReading(NormalizedReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return new RiskInputSourceReading(
            reading.EventId,
            reading.SensorId,
            reading.MetricType,
            reading.Value,
            reading.Unit,
            reading.EventTime);
    }
}
