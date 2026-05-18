using NatureProtector.Core.Primitives;

/*
 * This class represents an aggregated preventive risk view for an area
 * at a specific point in time.
 *
 * Rationale:
 * - Individual RiskAssessment instances describe localised results, typically
 *   at the cell level.
 * - AreaRiskSnapshot provides a compact summary that can be used by dashboards,
 *   alerts and operator-facing communication.
 *
 * Design considerations:
 * - The V1 candidate aggregation strategy is 0.70 * p80 + 0.30 * max, so
 *   localised high-risk cells remain visible in the area view without using the
 *   maximum alone.
 * - The aggregate level is derived from the aggregate score, ensuring consistency
 *   with the same score-to-level mapping used elsewhere in the domain.
 * - Summary is optional and can be generated either upstream or through the
 *   provided factory helper.
 */

namespace NatureProtector.Core.Risk;

public sealed class AreaRiskSnapshot
{
    /// <summary>
    /// Globally unique identifier of the snapshot.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Instant represented by the snapshot.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Qualitative aggregate risk level of the area.
    /// </summary>
    public RiskLevel AggregateRiskLevel { get; }

    /// <summary>
    /// Normalised aggregate risk score of the area in the range [0, 1].
    /// </summary>
    public double AggregateRiskScore { get; }

    /// <summary>
    /// Optional human-readable summary of the snapshot.
    /// </summary>
    public string? Summary { get; }

    /// <summary>
    /// Creates a new AreaRiskSnapshot instance from a known aggregate score.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the snapshot.
    /// </param>
    /// <param name="timestamp">
    /// Instant represented by the snapshot.
    /// </param>
    /// <param name="aggregateRiskScore">
    /// Normalised aggregate score in the range [0, 1].
    /// </param>
    /// <param name="summary">
    /// Optional human-readable summary.
    /// </param>
    public AreaRiskSnapshot(
        Guid id,
        DateTimeOffset timestamp,
        double aggregateRiskScore,
        string? summary = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Area risk snapshot identifier must not be an empty GUID.",
                nameof(id));
        }

        if (timestamp == default)
        {
            throw new ArgumentException(
                "Snapshot timestamp must be a valid, non-default value.",
                nameof(timestamp));
        }

        if (double.IsNaN(aggregateRiskScore) || double.IsInfinity(aggregateRiskScore))
        {
            throw new ArgumentOutOfRangeException(
                nameof(aggregateRiskScore),
                aggregateRiskScore,
                "Aggregate risk score must be a finite value.");
        }

        if (aggregateRiskScore is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aggregateRiskScore),
                aggregateRiskScore,
                "Aggregate risk score must be in the range [0, 1].");
        }

        Id = id;
        Timestamp = timestamp;
        AggregateRiskScore = aggregateRiskScore;
        AggregateRiskLevel = RiskLevelExtensions.FromScore(aggregateRiskScore);
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }

    /// <summary>
    /// Creates a snapshot by aggregating a collection of RiskAssessment instances.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the snapshot.
    /// </param>
    /// <param name="timestamp">
    /// Instant represented by the snapshot.
    /// </param>
    /// <param name="assessments">
    /// Assessments to aggregate.
    /// </param>
    /// <returns>
    /// A new AreaRiskSnapshot instance.
    /// </returns>
    public static AreaRiskSnapshot CreateFromAssessments(
        Guid id,
        DateTimeOffset timestamp,
        IEnumerable<RiskAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var items = assessments.ToList();

        if (items.Count == 0)
        {
            throw new ArgumentException(
                "At least one risk assessment is required to create an area snapshot.",
                nameof(assessments));
        }

        var scores = items
            .Select(item => item.RiskScore)
            .Order()
            .ToList();
        var p80 = CalculateNearestRankPercentile(scores, 0.80);
        var max = scores[^1];
        var aggregateScore = (0.70 * p80) + (0.30 * max);
        var highOrAboveCount = items.Count(item => item.RiskLevel.IsHighOrAbove());

        var summary =
            $"Aggregated from {items.Count} assessments; " +
            $"{highOrAboveCount} at High or above; " +
            $"AreaRisk=0.70*p80({p80:F2})+0.30*max({max:F2}).";

        return new AreaRiskSnapshot(
            id: id,
            timestamp: timestamp,
            aggregateRiskScore: aggregateScore,
            summary: summary);
    }

    private static double CalculateNearestRankPercentile(IReadOnlyList<double> sortedScores, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sortedScores.Count);
        var index = Math.Clamp(rank - 1, 0, sortedScores.Count - 1);
        return sortedScores[index];
    }
}
