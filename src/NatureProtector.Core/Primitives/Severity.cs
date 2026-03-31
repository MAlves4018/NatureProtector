/*
 * This enumeration represents the communication severity of a preventive
 * condition, alert or recommendation within the Nature Protector domain.
 *
 * Rationale:
 * - Severity provides a domain-wide language for expressing urgency and impact.
 * - It is especially useful for alerts and operator-facing communication.
 *
 * Design considerations:
 * - The numeric ordering is intentional and supports direct comparison.
 * - The levels are aligned with the intended target model:
 *   Info, Low, Medium, High, Critical and Emergency.
 */

namespace NatureProtector.Core.Primitives;

public enum Severity
{
    /// <summary>
    /// Informational situation that does not require immediate action.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Low-severity situation with limited operational urgency.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium-severity situation requiring attention.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High-severity situation requiring prompt action.
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical-severity situation with serious operational relevance.
    /// </summary>
    Critical = 4,

    /// <summary>
    /// Emergency-level situation requiring immediate response.
    /// </summary>
    Emergency = 5
}

/// <summary>
/// Extension methods for working with severity values.
/// </summary>
public static class SeverityExtensions
{
    /// <summary>
    /// Indicates whether the severity is critical or above.
    /// </summary>
    /// <param name="severity">
    /// Severity value to evaluate.
    /// </param>
    /// <returns>
    /// True when the severity is Critical or Emergency; otherwise false.
    /// </returns>
    public static bool IsCriticalOrAbove(this Severity severity)
    {
        return severity >= Severity.Critical;
    }

    /// <summary>
    /// Returns true if the current severity is strictly worse than the other.
    /// </summary>
    /// <param name="severity">
    /// Current severity value.
    /// </param>
    /// <param name="other">
    /// Severity value used as comparison baseline.
    /// </param>
    /// <returns>
    /// True when the current severity is strictly worse; otherwise false.
    /// </returns>
    public static bool IsWorseThan(this Severity severity, Severity other)
    {
        return severity > other;
    }

    /// <summary>
    /// Maps a qualitative risk level to an approximate communication severity.
    /// </summary>
    /// <param name="riskLevel">
    /// Risk level to convert.
    /// </param>
    /// <returns>
    /// Approximate severity suitable for communication and alerting.
    /// </returns>
    public static Severity FromRiskLevel(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.Unknown => Severity.Info,
            RiskLevel.VeryLow => Severity.Info,
            RiskLevel.Low => Severity.Low,
            RiskLevel.Moderate => Severity.Medium,
            RiskLevel.High => Severity.High,
            RiskLevel.VeryHigh => Severity.Critical,
            RiskLevel.Extreme => Severity.Emergency,
            _ => Severity.Info
        };
    }
}