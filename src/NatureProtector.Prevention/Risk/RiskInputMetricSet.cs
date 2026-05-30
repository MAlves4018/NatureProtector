using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public sealed record RiskInputMetricSet(
    double? TemperatureCelsius,
    double? RelativeHumidityPercent,
    double? WindSpeedMetersPerSecond)
{
    public bool HasTemperature => TemperatureCelsius.HasValue;
    public bool HasRelativeHumidity => RelativeHumidityPercent.HasValue;
    public bool HasWindSpeed => WindSpeedMetersPerSecond.HasValue;
    public int V1MetricCount =>
        (HasTemperature ? 1 : 0) +
        (HasRelativeHumidity ? 1 : 0) +
        (HasWindSpeed ? 1 : 0);
    public bool IsCompleteV1 => HasTemperature && HasRelativeHumidity && HasWindSpeed;

    public static RiskInputMetricSet FromReading(
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value)
    {
        return metricType switch
        {
            SensorMetricType.Temperature when unit == MeasurementUnit.Celsius => new(value, null, null),
            SensorMetricType.Humidity when unit == MeasurementUnit.Percent => new(null, value, null),
            SensorMetricType.WindSpeed when unit == MeasurementUnit.MetersPerSecond => new(null, null, value),
            _ => new(null, null, null)
        };
    }

    public static RiskInputMetricSet FromReadings(IEnumerable<NormalizedReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        double? temperature = null;
        double? humidity = null;
        double? windSpeed = null;

        foreach (var reading in readings.OrderBy(item => item.EventTime))
        {
            var metric = FromReading(reading.MetricType, reading.Unit, reading.Value);
            temperature = metric.TemperatureCelsius ?? temperature;
            humidity = metric.RelativeHumidityPercent ?? humidity;
            windSpeed = metric.WindSpeedMetersPerSecond ?? windSpeed;
        }

        return new RiskInputMetricSet(temperature, humidity, windSpeed);
    }

    public RiskInputMetricSet Merge(DailyCellState? dailyCellState)
    {
        if (dailyCellState is null)
        {
            return this;
        }

        return this with
        {
            TemperatureCelsius = TemperatureCelsius ?? dailyCellState.MaxTemperatureCelsius,
            RelativeHumidityPercent = RelativeHumidityPercent ?? dailyCellState.LatestHumidityPercent,
            WindSpeedMetersPerSecond = WindSpeedMetersPerSecond ?? dailyCellState.LatestWindSpeedMetersPerSecond
        };
    }
}
