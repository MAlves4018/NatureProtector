using NatureProtector.Core.Areas;
using NatureProtector.Core.Readings;

/*
 * This class represents a versioned collection of empirical weighting rules
 * used to compute a preliminary preventive wildfire risk score.
 *
 * Rationale:
 * - The preventive assessment needs a stable place where scoring assumptions
 *   are defined and versioned.
 * - Keeping the preliminary scoring logic inside RuleSet makes later
 *   replacement or refinement much easier.
 *
 * Design considerations:
 * - The class stores only a small set of weights aligned with the current
 *   target model: temperature, humidity, wind and vegetation.
 * - The class validates that weights are non-negative and that at least one
 *   weight is strictly positive.
 * - The current scoring method is intentionally simple and explainable.
 */

namespace NatureProtector.Core.Risk;

public sealed class RuleSet
{
    /// <summary>
    /// Globally unique identifier of the rule set.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Human-readable version label of the rule set.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Weight assigned to the temperature-derived risk contribution.
    /// </summary>
    public double TemperatureWeight { get; }

    /// <summary>
    /// Weight assigned to the humidity-derived risk contribution.
    /// </summary>
    public double HumidityWeight { get; }

    /// <summary>
    /// Weight assigned to the wind-derived risk contribution.
    /// </summary>
    public double WindWeight { get; }

    /// <summary>
    /// Weight assigned to the vegetation-derived risk contribution.
    /// </summary>
    public double VegetationWeight { get; }

    /// <summary>
    /// Creates a new RuleSet instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the rule set.
    /// </param>
    /// <param name="version">
    /// Human-readable version label.
    /// </param>
    /// <param name="temperatureWeight">
    /// Weight of the temperature component.
    /// </param>
    /// <param name="humidityWeight">
    /// Weight of the humidity component.
    /// </param>
    /// <param name="windWeight">
    /// Weight of the wind component.
    /// </param>
    /// <param name="vegetationWeight">
    /// Weight of the vegetation component.
    /// </param>
    public RuleSet(
        Guid id,
        string version,
        double temperatureWeight,
        double humidityWeight,
        double windWeight,
        double vegetationWeight)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Rule set identifier must not be an empty GUID.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(
                "Rule set version must not be null or whitespace.",
                nameof(version));
        }

        ValidateWeight(temperatureWeight, nameof(temperatureWeight));
        ValidateWeight(humidityWeight, nameof(humidityWeight));
        ValidateWeight(windWeight, nameof(windWeight));
        ValidateWeight(vegetationWeight, nameof(vegetationWeight));

        if (temperatureWeight + humidityWeight + windWeight + vegetationWeight <= 0.0)
        {
            throw new ArgumentException(
                "At least one rule weight must be greater than zero.");
        }

        Id = id;
        Version = version.Trim();
        TemperatureWeight = temperatureWeight;
        HumidityWeight = humidityWeight;
        WindWeight = windWeight;
        VegetationWeight = vegetationWeight;
    }

    /// <summary>
    /// Calculates a normalised preventive risk score in the range [0, 1]
    /// from the provided reading and area context.
    /// </summary>
    /// <param name="reading">
    /// Reading used as the observational input.
    /// </param>
    /// <param name="areaContext">
    /// Contextual characteristics of the area.
    /// </param>
    /// <returns>
    /// Normalised preventive risk score in the range [0, 1].
    /// </returns>
    public double CalculateScore(Reading reading, AreaContext areaContext)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(areaContext);

        var weightedSum = 0.0;
        var totalWeight = 0.0;

        /*
         * Temperature contribution:
         * - 10°C or below maps to minimal thermal contribution.
         * - 40°C or above maps to maximum thermal contribution.
         */
        if (reading.Values.TemperatureCelsius.HasValue && TemperatureWeight > 0.0)
        {
            var temperatureRisk = NormalizeTemperature(reading.Values.TemperatureCelsius.Value);
            weightedSum += temperatureRisk * TemperatureWeight;
            totalWeight += TemperatureWeight;
        }

        /*
         * Humidity contribution:
         * - Higher humidity reduces fire risk.
         * - Lower humidity increases fire risk.
         */
        if (reading.Values.RelativeHumidityPercent.HasValue && HumidityWeight > 0.0)
        {
            var humidityRisk = NormalizeHumidityAsRisk(reading.Values.RelativeHumidityPercent.Value);
            weightedSum += humidityRisk * HumidityWeight;
            totalWeight += HumidityWeight;
        }

        /*
         * Wind contribution:
         * - Stronger wind increases spread potential and therefore risk.
         * - 20 m/s is treated as a pragmatic upper normalisation bound for now.
         */
        if (reading.Values.WindSpeedMetersPerSecond.HasValue && WindWeight > 0.0)
        {
            var windRisk = NormalizeWindSpeed(reading.Values.WindSpeedMetersPerSecond.Value);
            weightedSum += windRisk * WindWeight;
            totalWeight += WindWeight;
        }

        /*
         * Vegetation contribution:
         * - AreaContext already stores vegetation density as a normalised value in [0, 1].
         */
        if (VegetationWeight > 0.0)
        {
            weightedSum += areaContext.VegetationDensity * VegetationWeight;
            totalWeight += VegetationWeight;
        }

        if (totalWeight <= 0.0)
        {
            throw new InvalidOperationException(
                "A preventive risk score cannot be calculated because no usable weighted signals are available.");
        }

        return weightedSum / totalWeight;
    }

    /// <summary>
    /// Builds a short explanation string describing the dominant factors
    /// that contributed to the preventive risk score.
    /// </summary>
    /// <param name="reading">
    /// Reading used as observational input.
    /// </param>
    /// <param name="areaContext">
    /// Contextual characteristics of the area.
    /// </param>
    /// <returns>
    /// Human-readable explanation summary.
    /// </returns>
    public string BuildExplanationSummary(Reading reading, AreaContext areaContext)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(areaContext);

        var drivers = new List<string>();

        if (reading.Values.TemperatureCelsius.HasValue)
        {
            var temperatureRisk = NormalizeTemperature(reading.Values.TemperatureCelsius.Value);
            if (temperatureRisk >= 0.60)
            {
                drivers.Add("elevated temperature");
            }
        }

        if (reading.Values.RelativeHumidityPercent.HasValue)
        {
            var humidityRisk = NormalizeHumidityAsRisk(reading.Values.RelativeHumidityPercent.Value);
            if (humidityRisk >= 0.60)
            {
                drivers.Add("low relative humidity");
            }
        }

        if (reading.Values.WindSpeedMetersPerSecond.HasValue)
        {
            var windRisk = NormalizeWindSpeed(reading.Values.WindSpeedMetersPerSecond.Value);
            if (windRisk >= 0.60)
            {
                drivers.Add("strong wind");
            }
        }

        if (areaContext.VegetationDensity >= 0.60)
        {
            drivers.Add("dense vegetation");
        }

        if (drivers.Count == 0)
        {
            return "Risk is driven by combined moderate factors rather than a single dominant signal.";
        }

        return $"Main contributors: {string.Join(", ", drivers)}.";
    }

    /// <summary>
    /// Validates that a weight is finite and non-negative.
    /// </summary>
    private static void ValidateWeight(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Weight must be a finite number.");
        }

        if (value < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Weight must be greater than or equal to zero.");
        }
    }

    /// <summary>
    /// Normalises temperature to a preliminary risk contribution in [0, 1].
    /// </summary>
    private static double NormalizeTemperature(double temperatureCelsius)
    {
        return Math.Clamp((temperatureCelsius - 10.0) / 30.0, 0.0, 1.0);
    }

    /// <summary>
    /// Converts relative humidity to a preliminary risk contribution in [0, 1],
    /// where lower humidity implies higher risk.
    /// </summary>
    private static double NormalizeHumidityAsRisk(double relativeHumidityPercent)
    {
        return Math.Clamp((100.0 - relativeHumidityPercent) / 100.0, 0.0, 1.0);
    }

    /// <summary>
    /// Normalises wind speed to a preliminary risk contribution in [0, 1].
    /// </summary>
    private static double NormalizeWindSpeed(double windSpeedMetersPerSecond)
    {
        return Math.Clamp(windSpeedMetersPerSecond / 20.0, 0.0, 1.0);
    }
}