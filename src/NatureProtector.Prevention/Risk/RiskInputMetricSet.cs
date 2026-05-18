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
