/*
 * This class groups together the environmental quantities measured for a given reading.
 *
 * Rationale:
 * - Separating the measured values from the Reading identity allows the domain
 *   to reuse the same value structure across simulated, observed or aggregated data.
 * - Not all sensors expose the same signals, so the value container must support
 *   partial observations.
 *
 * Design considerations:
 * - All properties are nullable because different sensor configurations may provide
 *   only a subset of the available values.
 * - Validation is performed at construction time so invalid values do not enter
 *   the domain model.
 * - The class provides immutable "with" helpers to support safe transformation
 *   without mutating existing instances.
 */

namespace NatureProtector.Core.Readings;

public sealed class ReadingValues(
    double? temperatureCelsius = null,
    double? relativeHumidityPercent = null,
    double? windSpeedMetersPerSecond = null,
    double? windDirectionDegrees = null,
    double? precipitationMillimetresPerHour = null)
{
    /// <summary>
    /// Air temperature in degrees Celsius.
    /// </summary>
    public double? TemperatureCelsius { get; } =
        ValidateFinite(temperatureCelsius, nameof(temperatureCelsius));

    /// <summary>
    /// Relative humidity in percent, constrained to the range [0, 100].
    /// </summary>
    public double? RelativeHumidityPercent { get; } =
        ValidatePercentage(relativeHumidityPercent, nameof(relativeHumidityPercent));

    /// <summary>
    /// Wind speed in meters per second.
    /// </summary>
    public double? WindSpeedMetersPerSecond { get; } =
        ValidateNonNegativeFinite(windSpeedMetersPerSecond, nameof(windSpeedMetersPerSecond));

    /// <summary>
    /// Wind direction in degrees, constrained to the range [0, 360].
    /// </summary>
    public double? WindDirectionDegrees { get; } =
        ValidateDirection(windDirectionDegrees, nameof(windDirectionDegrees));

    /// <summary>
    /// Precipitation intensity in millimetres per hour.
    /// </summary>
    public double? PrecipitationMillimetresPerHour { get; } =
        ValidateNonNegativeFinite(
            precipitationMillimetresPerHour,
            nameof(precipitationMillimetresPerHour));

    /// <summary>
    /// Returns a new ReadingValues instance with an updated temperature.
    /// </summary>
    /// <param name="temperatureCelsius">
    /// New air temperature in degrees Celsius.
    /// </param>
    public ReadingValues WithTemperature(double? temperatureCelsius) =>
        new(
            temperatureCelsius,
            RelativeHumidityPercent,
            WindSpeedMetersPerSecond,
            WindDirectionDegrees,
            PrecipitationMillimetresPerHour);

    /// <summary>
    /// Returns a new ReadingValues instance with an updated relative humidity.
    /// </summary>
    /// <param name="relativeHumidityPercent">
    /// New relative humidity in percent.
    /// </param>
    public ReadingValues WithRelativeHumidity(double? relativeHumidityPercent) =>
        new(
            TemperatureCelsius,
            relativeHumidityPercent,
            WindSpeedMetersPerSecond,
            WindDirectionDegrees,
            PrecipitationMillimetresPerHour);

    /// <summary>
    /// Returns a new ReadingValues instance with updated wind data.
    /// </summary>
    /// <param name="windSpeedMetersPerSecond">
    /// New wind speed in meters per second.
    /// </param>
    /// <param name="windDirectionDegrees">
    /// New wind direction in degrees.
    /// </param>
    public ReadingValues WithWind(
        double? windSpeedMetersPerSecond,
        double? windDirectionDegrees) =>
        new(
            TemperatureCelsius,
            RelativeHumidityPercent,
            windSpeedMetersPerSecond,
            windDirectionDegrees,
            PrecipitationMillimetresPerHour);

    /// <summary>
    /// Returns a new ReadingValues instance with updated precipitation intensity.
    /// </summary>
    /// <param name="precipitationMillimetresPerHour">
    /// New precipitation value in millimetres per hour.
    /// </param>
    public ReadingValues WithPrecipitation(double? precipitationMillimetresPerHour) =>
        new(
            TemperatureCelsius,
            RelativeHumidityPercent,
            WindSpeedMetersPerSecond,
            WindDirectionDegrees,
            precipitationMillimetresPerHour);

    /// <summary>
    /// Combines this instance with another ReadingValues instance by averaging
    /// overlapping numeric values and preserving whichever value is available
    /// when only one side provides it.
    /// </summary>
    /// <param name="other">
    /// Other ReadingValues instance to combine with the current one.
    /// </param>
    /// <returns>
    /// A new ReadingValues instance representing the merged view.
    /// </returns>
    public ReadingValues CombineWith(ReadingValues other)
    {
        ArgumentNullException.ThrowIfNull(other);

        static double? Average(double? left, double? right)
        {
            if (left.HasValue && right.HasValue)
            {
                return (left.Value + right.Value) / 2.0;
            }

            return left ?? right;
        }

        return new ReadingValues(
            temperatureCelsius: Average(TemperatureCelsius, other.TemperatureCelsius),
            relativeHumidityPercent: Average(RelativeHumidityPercent, other.RelativeHumidityPercent),
            windSpeedMetersPerSecond: Average(WindSpeedMetersPerSecond, other.WindSpeedMetersPerSecond),
            windDirectionDegrees: Average(WindDirectionDegrees, other.WindDirectionDegrees),
            precipitationMillimetresPerHour: Average(
                PrecipitationMillimetresPerHour,
                other.PrecipitationMillimetresPerHour));
    }

    /// <summary>
    /// Validates that a nullable numeric value is finite when present.
    /// </summary>
    /// <param name="value">
    /// Value to validate.
    /// </param>
    /// <param name="paramName">
    /// Parameter name used in exception messages.
    /// </param>
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
    /// Validates that a nullable numeric value is finite and non-negative when present.
    /// </summary>
    /// <param name="value">
    /// Value to validate.
    /// </param>
    /// <param name="paramName">
    /// Parameter name used in exception messages.
    /// </param>
    private static double? ValidateNonNegativeFinite(double? value, string paramName)
    {
        var validated = ValidateFinite(value, paramName);

        if (validated.HasValue && validated.Value < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be greater than or equal to zero.");
        }

        return validated;
    }

    /// <summary>
    /// Validates that a nullable percentage is finite and inside the range [0, 100].
    /// </summary>
    /// <param name="value">
    /// Percentage value to validate.
    /// </param>
    /// <param name="paramName">
    /// Parameter name used in exception messages.
    /// </param>
    private static double? ValidatePercentage(double? value, string paramName)
    {
        var validated = ValidateFinite(value, paramName);

        if (validated is < 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Percentage must be in the range [0, 100].");
        }

        return validated;
    }

    /// <summary>
    /// Validates that a nullable direction is finite and inside the range [0, 360].
    /// </summary>
    /// <param name="value">
    /// Direction value to validate.
    /// </param>
    /// <param name="paramName">
    /// Parameter name used in exception messages.
    /// </param>
    private static double? ValidateDirection(double? value, string paramName)
    {
        var validated = ValidateFinite(value, paramName);

        if (validated is < 0.0 or > 360.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Direction must be in the range [0, 360].");
        }

        return validated;
    }
}