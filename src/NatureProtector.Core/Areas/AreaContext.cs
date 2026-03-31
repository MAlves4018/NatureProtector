/*
 * This class represents contextual and environmental characteristics of an Area
 * that influence preventive wildfire risk evaluation.
 *
 * Rationale:
 * - The geographic boundaries of an Area are not sufficient on their own to support
 *   preventive risk assessment.
 * - Additional contextual signals such as vegetation, population exposure and
 *   infrastructure exposure are needed to enrich the domain model and support
 *   later risk calculations.
 *
 * Design considerations:
 * - The class is intentionally immutable after construction in order to preserve
 *   consistency once an AreaContext is created.
 * - The numeric factors are treated as normalised coefficients in the range [0, 1]
 *   so that later risk models can consume them consistently.
 * - Textual descriptors such as VegetationType and Seasonality are required because
 *   they capture domain-relevant classifications that are useful even before a full
 *   taxonomy is introduced.
 */

namespace NatureProtector.Core.Areas;

public sealed class AreaContext
{
    /// <summary>
    /// Describes the dominant vegetation type of the area
    /// (e.g. "Pine Forest", "Shrubland", "Mixed Vegetation").
    /// </summary>
    public string VegetationType { get; }

    /// <summary>
    /// Normalised vegetation density coefficient in the range [0, 1].
    /// Higher values indicate denser vegetation coverage.
    /// </summary>
    public double VegetationDensity { get; }

    /// <summary>
    /// Normalised population exposure coefficient in the range [0, 1].
    /// Higher values indicate greater potential impact on nearby population.
    /// </summary>
    public double PopulationExposure { get; }

    /// <summary>
    /// Normalised critical infrastructure exposure coefficient in the range [0, 1].
    /// Higher values indicate greater exposure of infrastructure assets.
    /// </summary>
    public double CriticalInfrastructureExposure { get; }

    /// <summary>
    /// Describes the seasonal context relevant to the area
    /// (e.g. "Summer", "Dry Season", "High Fire Season").
    /// </summary>
    public string Seasonality { get; }

    /// <summary>
    /// Creates a new contextual description for an area.
    /// </summary>
    /// <param name="vegetationType">
    /// Human-readable classification of the dominant vegetation type.
    /// </param>
    /// <param name="vegetationDensity">
    /// Normalised density factor in the range [0, 1].
    /// </param>
    /// <param name="populationExposure">
    /// Normalised population exposure factor in the range [0, 1].
    /// </param>
    /// <param name="criticalInfrastructureExposure">
    /// Normalised infrastructure exposure factor in the range [0, 1].
    /// </param>
    /// <param name="seasonality">
    /// Human-readable seasonal context relevant to preventive assessment.
    /// </param>
    public AreaContext(
        string vegetationType,
        double vegetationDensity,
        double populationExposure,
        double criticalInfrastructureExposure,
        string seasonality)
    {
        if (string.IsNullOrWhiteSpace(vegetationType))
        {
            throw new ArgumentException(
                "Vegetation type must not be null or whitespace.",
                nameof(vegetationType));
        }

        if (vegetationDensity is < 0.0 or > 1.0 || double.IsNaN(vegetationDensity) || double.IsInfinity(vegetationDensity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(vegetationDensity),
                vegetationDensity,
                "Vegetation density must be a finite value in the range [0, 1].");
        }

        if (populationExposure is < 0.0 or > 1.0 || double.IsNaN(populationExposure) || double.IsInfinity(populationExposure))
        {
            throw new ArgumentOutOfRangeException(
                nameof(populationExposure),
                populationExposure,
                "Population exposure must be a finite value in the range [0, 1].");
        }

        if (criticalInfrastructureExposure is < 0.0 or > 1.0 ||
            double.IsNaN(criticalInfrastructureExposure) ||
            double.IsInfinity(criticalInfrastructureExposure))
        {
            throw new ArgumentOutOfRangeException(
                nameof(criticalInfrastructureExposure),
                criticalInfrastructureExposure,
                "Critical infrastructure exposure must be a finite value in the range [0, 1].");
        }

        if (string.IsNullOrWhiteSpace(seasonality))
        {
            throw new ArgumentException(
                "Seasonality must not be null or whitespace.",
                nameof(seasonality));
        }

        VegetationType = vegetationType.Trim();
        VegetationDensity = vegetationDensity;
        PopulationExposure = populationExposure;
        CriticalInfrastructureExposure = criticalInfrastructureExposure;
        Seasonality = seasonality.Trim();
    }
}