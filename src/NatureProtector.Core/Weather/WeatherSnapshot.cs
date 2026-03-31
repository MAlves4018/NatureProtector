using NatureProtector.Core.Primitives;

/*
 * This class captures a snapshot of weather conditions at a specific point in time.
 *
 * Rationale:
 * - Weather conditions are a relevant input for preventive risk assessment and
 *   simulation scenarios.
 * - The current target model treats WeatherSnapshot as a lightweight domain object
 *   that may represent either a localised observation or a broader area-level
 *   weather estimate.
 *
 * Design considerations:
 * - All meteorological fields are optional because real or simulated observations
 *   may be partial.
 * - The target model no longer includes AreaId, precipitation or fire danger index,
 *   so those fields were removed to keep the class aligned with the intended design.
 * - The object remains immutable after construction.
 */

namespace NatureProtector.Core.Weather;

public sealed class WeatherSnapshot
{
    /// <summary>
    /// Globally unique identifier of the weather snapshot.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Instant at which the weather snapshot was observed or computed.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Optional location for which this snapshot is most relevant.
    /// When null, the snapshot may represent a broader area-level estimate.
    /// </summary>
    public Location? Location { get; }

    /// <summary>
    /// Air temperature in degrees Celsius.
    /// </summary>
    public double? TemperatureCelsius { get; }

    /// <summary>
    /// Relative humidity in percent, in the range [0, 100].
    /// </summary>
    public double? RelativeHumidityPercent { get; }

    /// <summary>
    /// Optional wind vector associated with this snapshot.
    /// </summary>
    public WindVector? Wind { get; }

    /// <summary>
    /// Creates a new WeatherSnapshot instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the snapshot.
    /// </param>
    /// <param name="timestamp">
    /// Observation or computation timestamp.
    /// </param>
    /// <param name="location">
    /// Optional physical or logical location associated with the snapshot.
    /// </param>
    /// <param name="temperatureCelsius">
    /// Optional air temperature in degrees Celsius.
    /// </param>
    /// <param name="relativeHumidityPercent">
    /// Optional relative humidity in percent.
    /// </param>
    /// <param name="wind">
    /// Optional wind vector.
    /// </param>
    public WeatherSnapshot(
        Guid id,
        DateTimeOffset timestamp,
        Location? location = null,
        double? temperatureCelsius = null,
        double? relativeHumidityPercent = null,
        WindVector? wind = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Weather snapshot identifier must not be an empty GUID.",
                nameof(id));
        }

        if (timestamp == default)
        {
            throw new ArgumentException(
                "Timestamp must be a valid, non-default value.",
                nameof(timestamp));
        }

        TemperatureCelsius = ValidateFinite(
            temperatureCelsius,
            nameof(temperatureCelsius));

        RelativeHumidityPercent = ValidatePercentage(
            relativeHumidityPercent,
            nameof(relativeHumidityPercent));

        Id = id;
        Timestamp = timestamp;
        Location = location;
        Wind = wind;
    }

    /// <summary>
    /// Indicates whether the available conditions are both hot and dry according
    /// to a simple coarse heuristic useful for early preventive logic.
    /// </summary>
    /// <returns>
    /// True when both temperature and humidity are available and satisfy the
    /// heuristic thresholds, otherwise false.
    /// </returns>
    public bool IsHotAndDry()
    {
        if (TemperatureCelsius is null || RelativeHumidityPercent is null)
        {
            return false;
        }

        return TemperatureCelsius.Value >= 30.0 &&
               RelativeHumidityPercent.Value <= 30.0;
    }

    /// <summary>
    /// Returns a new WeatherSnapshot with the same fields except for an updated wind vector.
    /// </summary>
    /// <param name="newWind">
    /// New wind vector to associate with the snapshot.
    /// </param>
    public WeatherSnapshot WithUpdatedWind(WindVector? newWind)
    {
        return new WeatherSnapshot(
            id: Id,
            timestamp: Timestamp,
            location: Location,
            temperatureCelsius: TemperatureCelsius,
            relativeHumidityPercent: RelativeHumidityPercent,
            wind: newWind);
    }

    /// <summary>
    /// Produces a simple merged snapshot between this snapshot and another one.
    /// Numeric values are averaged when both are present; otherwise the non-null
    /// value is preserved. The newer timestamp is kept.
    /// </summary>
    /// <param name="nextSnapshot">
    /// Another snapshot to merge with this one.
    /// </param>
    /// <returns>
    /// A new merged WeatherSnapshot instance.
    /// </returns>
    public WeatherSnapshot MergeWith(WeatherSnapshot nextSnapshot)
    {
        ArgumentNullException.ThrowIfNull(nextSnapshot);

        var newer = Timestamp >= nextSnapshot.Timestamp ? this : nextSnapshot;

        var mergedTemperature = AverageNullable(
            TemperatureCelsius,
            nextSnapshot.TemperatureCelsius);

        var mergedHumidity = AverageNullable(
            RelativeHumidityPercent,
            nextSnapshot.RelativeHumidityPercent);

        var mergedLocation = nextSnapshot.Location ?? Location;
        var mergedWind = nextSnapshot.Wind ?? Wind;

        return new WeatherSnapshot(
            id: Guid.NewGuid(),
            timestamp: newer.Timestamp,
            location: mergedLocation,
            temperatureCelsius: mergedTemperature,
            relativeHumidityPercent: mergedHumidity,
            wind: mergedWind);

        static double? AverageNullable(double? a, double? b)
        {
            if (a.HasValue && b.HasValue)
            {
                return (a.Value + b.Value) / 2.0;
            }

            return a ?? b;
        }
    }

    /// <summary>
    /// Validates an optional numeric value, ensuring it is finite when present.
    /// </summary>
    private static double? ValidateFinite(double? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be a finite number or null.");
        }

        return value;
    }

    /// <summary>
    /// Validates an optional percentage value in the range [0, 100].
    /// </summary>
    private static double? ValidatePercentage(double? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Percentage must be a finite number or null.");
        }

        if (value is < 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Percentage must be in the range [0, 100].");
        }

        return value;
    }
}