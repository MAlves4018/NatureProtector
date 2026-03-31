/*
 * This enumeration represents a qualitative wildfire risk level for a
 * given area, cell or aggregated preventive assessment.
 *
 * Rationale:
 * - Risk levels provide a human-readable interpretation of underlying
 *   preventive risk scores.
 * - The chosen levels align with the intended preventive assessment model
 *   and are suitable for dashboards, summaries and alerts.
 *
 * Design considerations:
 * - The numeric ordering is intentional and supports comparison operators.
 * - Unknown is preserved as the default state for situations in which risk
 *   has not yet been computed or cannot be derived.
 */

namespace NatureProtector.Core.Primitives;

public enum RiskLevel
{
    /// <summary>
    /// Risk is not known or has not yet been computed.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Very low level of preventive wildfire risk.
    /// </summary>
    VeryLow = 1,

    /// <summary>
    /// Low level of preventive wildfire risk.
    /// </summary>
    Low = 2,

    /// <summary>
    /// Moderate level of preventive wildfire risk.
    /// </summary>
    Moderate = 3,

    /// <summary>
    /// High level of preventive wildfire risk.
    /// </summary>
    High = 4,

    /// <summary>
    /// Very high level of preventive wildfire risk.
    /// </summary>
    VeryHigh = 5,

    /// <summary>
    /// Extreme level of preventive wildfire risk.
    /// </summary>
    Extreme = 6
}

/// <summary>
/// Extension methods for working with qualitative risk levels.
/// </summary>
public static class RiskLevelExtensions
{
    /// <summary>
    /// Indicates whether the risk level is operationally considered high or above.
    /// </summary>
    /// <param name="level">
    /// Risk level to evaluate.
    /// </param>
    /// <returns>
    /// True when the level is High, VeryHigh or Extreme; otherwise false.
    /// </returns>
    public static bool IsHighOrAbove(this RiskLevel level)
    {
        return level >= RiskLevel.High;
    }

    /// <summary>
    /// Returns true if the current level represents strictly higher risk than the other.
    /// </summary>
    /// <param name="level">
    /// Current risk level.
    /// </param>
    /// <param name="other">
    /// Risk level used as comparison baseline.
    /// </param>
    /// <returns>
    /// True when the current level is strictly higher; otherwise false.
    /// </returns>
    public static bool IsHigherThan(this RiskLevel level, RiskLevel other)
    {
        return level > other;
    }

    /// <summary>
    /// Maps a normalised score in the range [0, 1] to a qualitative risk level.
    /// </summary>
    /// <param name="score">
    /// Normalised preventive risk score.
    /// </param>
    /// <returns>
    /// Corresponding qualitative risk level.
    /// </returns>
    public static RiskLevel FromScore(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                score,
                "Score must be a finite value.");
        }

        if (score is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                score,
                "Score must be in the range [0, 1].");
        }

        return score switch
        {
            < 0.10 => RiskLevel.VeryLow,
            < 0.25 => RiskLevel.Low,
            < 0.50 => RiskLevel.Moderate,
            < 0.70 => RiskLevel.High,
            < 0.90 => RiskLevel.VeryHigh,
            _ => RiskLevel.Extreme
        };
    }
}