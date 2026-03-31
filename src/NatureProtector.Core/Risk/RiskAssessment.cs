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
 * - The class stores both the numeric score and the derived qualitative level.
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
    /// Normalised preventive risk score in the range [0, 1].
    /// </summary>
    public double RiskScore { get; }

    /// <summary>
    /// Qualitative risk level derived from the numeric score.
    /// </summary>
    public RiskLevel RiskLevel { get; }

    /// <summary>
    /// Optional short human-readable explanation of the assessment result.
    /// </summary>
    public string? ExplanationSummary { get; }

    /// <summary>
    /// Creates a new RiskAssessment instance from a known score.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the assessment.
    /// </param>
    /// <param name="timestamp">
    /// Instant at which the assessment applies.
    /// </param>
    /// <param name="riskScore">
    /// Normalised preventive risk score in the range [0, 1].
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

        if (double.IsNaN(riskScore) || double.IsInfinity(riskScore))
        {
            throw new ArgumentOutOfRangeException(
                nameof(riskScore),
                riskScore,
                "Risk score must be a finite value.");
        }

        if (riskScore is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(riskScore),
                riskScore,
                "Risk score must be in the range [0, 1].");
        }

        Id = id;
        Timestamp = timestamp;
        RiskScore = riskScore;
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
        return RiskLevelExtensions.FromScore(RiskScore);
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
}