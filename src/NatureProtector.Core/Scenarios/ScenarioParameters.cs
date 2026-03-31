/*
 * This class represents the configurable environmental and execution parameters
 * associated with a Scenario.
 *
 * Rationale:
 * - A Scenario describes the semantic situation being modelled, while
 *   ScenarioParameters stores the numeric inputs that influence simulation
 *   behaviour and generated observations.
 * - Separating these values from Scenario keeps the scenario identity clean
 *   and makes parameter tuning explicit and testable.
 *
 * Design considerations:
 * - All environmental base values are nullable because some scenarios may
 *   override only a subset of the available signals.
 * - FailureRate is treated as a normalised value in the range [0, 1].
 * - NoiseLevel is treated as a non-negative scalar.
 * - TimeAcceleration must be strictly greater than zero because a simulation
 *   cannot advance with a null or negative time factor.
 */

namespace NatureProtector.Core.Scenarios;

public sealed class ScenarioParameters
{
    /// <summary>
    /// Optional base temperature in degrees Celsius.
    /// </summary>
    public double? BaseTemperature { get; }

    /// <summary>
    /// Optional base relative humidity in percent.
    /// </summary>
    public double? BaseHumidity { get; }

    /// <summary>
    /// Optional base wind speed in meters per second.
    /// </summary>
    public double? BaseWindSpeed { get; }

    /// <summary>
    /// Failure rate used to inject degraded behaviour during execution.
    /// Expected range is [0, 1].
    /// </summary>
    public double FailureRate { get; }

    /// <summary>
    /// Non-negative scalar controlling the magnitude of injected noise.
    /// </summary>
    public double NoiseLevel { get; }

    /// <summary>
    /// Positive multiplier controlling how fast simulated time advances.
    /// </summary>
    public double TimeAcceleration { get; }

    /// <summary>
    /// Creates a new ScenarioParameters instance.
    /// </summary>
    /// <param name="baseTemperature">
    /// Optional baseline temperature in degrees Celsius.
    /// </param>
    /// <param name="baseHumidity">
    /// Optional baseline relative humidity in percent.
    /// </param>
    /// <param name="baseWindSpeed">
    /// Optional baseline wind speed in meters per second.
    /// </param>
    /// <param name="failureRate">
    /// Normalised failure rate in the range [0, 1].
    /// </param>
    /// <param name="noiseLevel">
    /// Non-negative scalar representing noise magnitude.
    /// </param>
    /// <param name="timeAcceleration">
    /// Positive multiplier used to accelerate simulation time.
    /// </param>
    public ScenarioParameters(
        double? baseTemperature = null,
        double? baseHumidity = null,
        double? baseWindSpeed = null,
        double failureRate = 0.0,
        double noiseLevel = 0.0,
        double timeAcceleration = 1.0)
    {
        BaseTemperature = ValidateFiniteNullable(baseTemperature, nameof(baseTemperature));
        BaseHumidity = ValidatePercentageNullable(baseHumidity, nameof(baseHumidity));
        BaseWindSpeed = ValidateNonNegativeNullable(baseWindSpeed, nameof(baseWindSpeed));
        FailureRate = ValidateRange(failureRate, 0.0, 1.0, nameof(failureRate));
        NoiseLevel = ValidateNonNegative(noiseLevel, nameof(noiseLevel));
        TimeAcceleration = ValidateStrictlyPositive(timeAcceleration, nameof(timeAcceleration));
    }

    /// <summary>
    /// Returns a new ScenarioParameters instance with an updated base temperature.
    /// </summary>
    /// <param name="baseTemperature">
    /// New baseline temperature in degrees Celsius.
    /// </param>
    public ScenarioParameters WithBaseTemperature(double? baseTemperature)
    {
        return new ScenarioParameters(
            baseTemperature: baseTemperature,
            baseHumidity: BaseHumidity,
            baseWindSpeed: BaseWindSpeed,
            failureRate: FailureRate,
            noiseLevel: NoiseLevel,
            timeAcceleration: TimeAcceleration);
    }

    /// <summary>
    /// Returns a new ScenarioParameters instance with an updated base humidity.
    /// </summary>
    /// <param name="baseHumidity">
    /// New baseline relative humidity in percent.
    /// </param>
    public ScenarioParameters WithBaseHumidity(double? baseHumidity)
    {
        return new ScenarioParameters(
            baseTemperature: BaseTemperature,
            baseHumidity: baseHumidity,
            baseWindSpeed: BaseWindSpeed,
            failureRate: FailureRate,
            noiseLevel: NoiseLevel,
            timeAcceleration: TimeAcceleration);
    }

    /// <summary>
    /// Returns a new ScenarioParameters instance with an updated base wind speed.
    /// </summary>
    /// <param name="baseWindSpeed">
    /// New baseline wind speed in meters per second.
    /// </param>
    public ScenarioParameters WithBaseWindSpeed(double? baseWindSpeed)
    {
        return new ScenarioParameters(
            baseTemperature: BaseTemperature,
            baseHumidity: BaseHumidity,
            baseWindSpeed: baseWindSpeed,
            failureRate: FailureRate,
            noiseLevel: NoiseLevel,
            timeAcceleration: TimeAcceleration);
    }

    /// <summary>
    /// Returns a new ScenarioParameters instance with updated execution controls.
    /// </summary>
    /// <param name="failureRate">
    /// New failure rate in the range [0, 1].
    /// </param>
    /// <param name="noiseLevel">
    /// New non-negative noise level.
    /// </param>
    /// <param name="timeAcceleration">
    /// New positive time acceleration factor.
    /// </param>
    public ScenarioParameters WithExecutionControls(
        double failureRate,
        double noiseLevel,
        double timeAcceleration)
    {
        return new ScenarioParameters(
            baseTemperature: BaseTemperature,
            baseHumidity: BaseHumidity,
            baseWindSpeed: BaseWindSpeed,
            failureRate: failureRate,
            noiseLevel: noiseLevel,
            timeAcceleration: timeAcceleration);
    }

    /// <summary>
    /// Validates a nullable finite numeric value.
    /// </summary>
    private static double? ValidateFiniteNullable(double? value, string paramName)
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
    /// Validates a nullable percentage in the range [0, 100].
    /// </summary>
    private static double? ValidatePercentageNullable(double? value, string paramName)
    {
        var validated = ValidateFiniteNullable(value, paramName);

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
    /// Validates a nullable non-negative numeric value.
    /// </summary>
    private static double? ValidateNonNegativeNullable(double? value, string paramName)
    {
        var validated = ValidateFiniteNullable(value, paramName);

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
    /// Validates that a finite numeric value is non-negative.
    /// </summary>
    private static double ValidateNonNegative(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be a finite number.");
        }

        if (value < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be greater than or equal to zero.");
        }

        return value;
    }

    /// <summary>
    /// Validates that a finite numeric value is strictly positive.
    /// </summary>
    private static double ValidateStrictlyPositive(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be a finite number.");
        }

        if (value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be strictly greater than zero.");
        }

        return value;
    }

    /// <summary>
    /// Validates that a finite numeric value lies within the specified inclusive range.
    /// </summary>
    private static double ValidateRange(double value, double min, double max, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be a finite number.");
        }

        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                $"Value must be in the range [{min}, {max}].");
        }

        return value;
    }
}