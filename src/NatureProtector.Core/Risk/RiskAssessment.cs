using NatureProtector.Core.Areas;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Readings;

/*
 * This class represents the result of a preventive wildfire risk evaluation
 * for a specific cell and point in time.
 *
 * Rationale:
 * - RiskAssessment is the domain object that captures the analytical result
 *   of applying a RuleSet to a Reading and the contextual characteristics
 *   of the area.
 * - It separates analytical state from the structural spatial model of RiskCell.
 *
 * Design considerations:
 * - The class stores the baseline score, the adjusted score and a legacy
 *   compatibility score (`RiskScore`) that mirrors the adjusted score.
 * - The score is constrained to the range [0, 1] for consistency with the
 *   current preliminary rule model.
 * - ExplanationSummary is optional because some pipeline stages may choose
 *   to compute the score first and enrich the explanation later.
 */

namespace NatureProtector.Core.Risk;

public sealed class RiskAssessment
{
    /// <summary>
    /// Globally unique identifier of the assessment.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Instant at which the assessment result is considered valid.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Baseline risk component before contextual adjustment.
    /// </summary>
    public double BaseRisk { get; }

    /// <summary>
    /// Adjusted risk component after applying contextual candidate factors.
    /// </summary>
    public double AdjustedScore { get; }

    /// <summary>
    /// Legacy compatibility score used by the current persistence and projection
    /// pipeline. Mirrors <see cref="AdjustedScore"/>.
    /// </summary>
    public double RiskScore { get; }

    /// <summary>
    /// Compatibility projection of <see cref="AdjustedScore"/> on a 0..100
    /// integer scale for reporting and evidence packs.
    /// </summary>
    public int Score100 => (int)Math.Round(AdjustedScore * 100.0, MidpointRounding.AwayFromZero);

    public double MeteorologyComponent { get; }

    public double DroughtComponent { get; }

    public double TerritoryComponent { get; }

    public double HazardComponent { get; }

    public double FuelComponent { get; }

    public double GeomorphologyComponent { get; }

    public double ConfidenceFactor { get; }

    public double IntegrityFactor { get; }

    public string DominantDriver { get; }

    public string ParameterSetVersion { get; }

    public string CalculationStatus { get; }

    public string? Limitations { get; }

    /// <summary>
    /// Qualitative risk level derived from the numeric score.
    /// </summary>
    public RiskLevel RiskLevel { get; }

    /// <summary>
    /// Optional short human-readable explanation of the assessment result.
    /// </summary>
    public string? ExplanationSummary { get; }

    /// <summary>
    /// Creates a new RiskAssessment instance from a known legacy score.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the assessment.
    /// </param>
    /// <param name="timestamp">
    /// Instant at which the assessment applies.
    /// </param>
    /// <param name="riskScore">
    /// Legacy risk score in the range [0, 1]. Used as both baseline and
    /// adjusted score to preserve backward compatibility.
    /// </param>
    /// <param name="explanationSummary">
    /// Optional short explanation of the assessment result.
    /// </param>
    public RiskAssessment(
        Guid id,
        DateTimeOffset timestamp,
        double riskScore,
        string? explanationSummary = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Risk assessment identifier must not be an empty GUID.",
                nameof(id));
        }

        if (timestamp == default)
        {
            throw new ArgumentException(
                "Assessment timestamp must be a valid, non-default value.",
                nameof(timestamp));
        }

        ValidateNormalizedScore(riskScore, nameof(riskScore));

        Id = id;
        Timestamp = timestamp;
        BaseRisk = riskScore;
        AdjustedScore = riskScore;
        RiskScore = riskScore;
        MeteorologyComponent = 0.0;
        DroughtComponent = 0.0;
        TerritoryComponent = 0.0;
        HazardComponent = 0.0;
        FuelComponent = 0.0;
        GeomorphologyComponent = 0.0;
        ConfidenceFactor = 1.0;
        IntegrityFactor = 1.0;
        DominantDriver = "LegacyCompatibility";
        ParameterSetVersion = "LegacyCompatibility";
        CalculationStatus = "LegacyCompatibility";
        Limitations = null;
        RiskLevel = CalculateLevel();
        ExplanationSummary = string.IsNullOrWhiteSpace(explanationSummary)
            ? null
            : explanationSummary.Trim();
    }

    /// <summary>
    /// Creates a new RiskAssessment instance from explicit baseline and adjusted
    /// scores.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the assessment.
    /// </param>
    /// <param name="timestamp">
    /// Instant at which the assessment applies.
    /// </param>
    /// <param name="baseRisk">
    /// Baseline risk component in the range [0, 1].
    /// </param>
    /// <param name="adjustedScore">
    /// Adjusted risk component in the range [0, 1].
    /// </param>
    /// <param name="explanationSummary">
    /// Optional short explanation of the assessment result.
    /// </param>
    public RiskAssessment(
        Guid id,
        DateTimeOffset timestamp,
        double baseRisk,
        double adjustedScore,
        string? explanationSummary = null,
        double meteorologyComponent = 0.0,
        double droughtComponent = 0.0,
        double territoryComponent = 0.0,
        double hazardComponent = 0.0,
        double fuelComponent = 0.0,
        double geomorphologyComponent = 0.0,
        double confidenceFactor = 1.0,
        double integrityFactor = 1.0,
        string? dominantDriver = null,
        string? parameterSetVersion = null,
        string? calculationStatus = null,
        string? limitations = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Risk assessment identifier must not be an empty GUID.",
                nameof(id));
        }

        if (timestamp == default)
        {
            throw new ArgumentException(
                "Assessment timestamp must be a valid, non-default value.",
                nameof(timestamp));
        }

        ValidateNormalizedScore(baseRisk, nameof(baseRisk));
        ValidateNormalizedScore(adjustedScore, nameof(adjustedScore));
        ValidateNormalizedScore(meteorologyComponent, nameof(meteorologyComponent));
        ValidateNormalizedScore(droughtComponent, nameof(droughtComponent));
        ValidateNormalizedScore(territoryComponent, nameof(territoryComponent));
        ValidateNormalizedScore(hazardComponent, nameof(hazardComponent));
        ValidateNormalizedScore(fuelComponent, nameof(fuelComponent));
        ValidateNormalizedScore(geomorphologyComponent, nameof(geomorphologyComponent));
        ValidateNormalizedScore(confidenceFactor, nameof(confidenceFactor));
        ValidateNormalizedScore(integrityFactor, nameof(integrityFactor));

        Id = id;
        Timestamp = timestamp;
        BaseRisk = baseRisk;
        AdjustedScore = adjustedScore;
        RiskScore = adjustedScore;
        MeteorologyComponent = meteorologyComponent;
        DroughtComponent = droughtComponent;
        TerritoryComponent = territoryComponent;
        HazardComponent = hazardComponent;
        FuelComponent = fuelComponent;
        GeomorphologyComponent = geomorphologyComponent;
        ConfidenceFactor = confidenceFactor;
        IntegrityFactor = integrityFactor;
        DominantDriver = string.IsNullOrWhiteSpace(dominantDriver) ? "Mixed" : dominantDriver.Trim();
        ParameterSetVersion = string.IsNullOrWhiteSpace(parameterSetVersion) ? "Unknown" : parameterSetVersion.Trim();
        CalculationStatus = string.IsNullOrWhiteSpace(calculationStatus) ? "CandidateFallback" : calculationStatus.Trim();
        Limitations = string.IsNullOrWhiteSpace(limitations) ? null : limitations.Trim();
        RiskLevel = CalculateLevel();
        ExplanationSummary = string.IsNullOrWhiteSpace(explanationSummary)
            ? null
            : explanationSummary.Trim();
    }

    /// <summary>
    /// Derives the qualitative risk level from the current numeric score.
    /// </summary>
    /// <returns>
    /// Qualitative risk level associated with the current score.
    /// </returns>
    public RiskLevel CalculateLevel()
    {
        return RiskLevelExtensions.FromScore(AdjustedScore);
    }

    /// <summary>
    /// Creates a new RiskAssessment by applying a RuleSet to a Reading and AreaContext.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the new assessment.
    /// </param>
    /// <param name="timestamp">
    /// Instant at which the assessment applies.
    /// </param>
    /// <param name="ruleSet">
    /// Rule set used to compute the score.
    /// </param>
    /// <param name="reading">
    /// Reading used as observational input.
    /// </param>
    /// <param name="areaContext">
    /// Contextual characteristics of the area.
    /// </param>
    /// <returns>
    /// A new RiskAssessment instance.
    /// </returns>
    public static RiskAssessment Create(
        Guid id,
        DateTimeOffset timestamp,
        RuleSet ruleSet,
        Reading reading,
        AreaContext areaContext)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(areaContext);

        var score = ruleSet.CalculateScore(reading, areaContext);
        var explanation = ruleSet.BuildExplanationSummary(reading, areaContext);

        return new RiskAssessment(
            id: id,
            timestamp: timestamp,
            riskScore: score,
            explanationSummary: explanation);
    }

    private static void ValidateNormalizedScore(double score, string paramName)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                score,
                "Score must be a finite value.");
        }

        if (score is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                score,
                "Score must be in the range [0, 1].");
        }
    }
}
