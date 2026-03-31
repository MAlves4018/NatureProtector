using NatureProtector.Core.Primitives;

/*
 * This class represents a spatial risk cell used as the basic unit of
 * preventive wildfire risk evaluation inside an Area.
 *
 * Rationale:
 * - A RiskCell belongs to a specific Area and anchors risk-related analysis to
 *   a representative location.
 * - The cell stores the current qualitative risk state and a lightweight
 *   history of updates, which is sufficient for the current phase.
 *
 * Design considerations:
 * - Identity and ownership are immutable after construction.
 * - CurrentRiskLevel and update history are mutable because assessment evolves
 *   over time.
 * - Spatial containment is currently approximated through proximity because a
 *   full polygon or grid geometry is not yet modelled.
 */

namespace NatureProtector.Core.Risk;

public sealed class RiskCell
{
    private const double DefaultContainmentRadiusMeters = 25.0;

    /// <summary>
    /// Globally unique identifier of the risk cell.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Identifier of the area to which this cell belongs.
    /// </summary>
    public Guid AreaId { get; }

    /// <summary>
    /// Optional logical identifier of the cell
    /// (e.g. grid index, row-column label or tiling key).
    /// </summary>
    public string? CellId { get; }

    /// <summary>
    /// Representative location of the cell.
    /// For the current phase this acts as the centre or anchor point.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Current qualitative risk level associated with the cell.
    /// </summary>
    public RiskLevel CurrentRiskLevel { get; private set; }

    /// <summary>
    /// Timestamp of the most recent risk update, when available.
    /// </summary>
    public DateTimeOffset? LastUpdatedAt { get; private set; }

    private readonly List<(DateTimeOffset Timestamp, RiskLevel Level)> _history = [];

    /// <summary>
    /// Read-only view over the historical risk updates registered for this cell.
    /// </summary>
    public IReadOnlyList<(DateTimeOffset Timestamp, RiskLevel Level)> History => _history.AsReadOnly();

    /// <summary>
    /// Creates a new RiskCell instance.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the cell.
    /// </param>
    /// <param name="areaId">
    /// Identifier of the area to which the cell belongs.
    /// </param>
    /// <param name="location">
    /// Representative location of the cell.
    /// </param>
    /// <param name="initialRiskLevel">
    /// Initial qualitative risk level assigned to the cell.
    /// </param>
    /// <param name="cellId">
    /// Optional logical identifier of the cell.
    /// </param>
    /// <param name="initialTimestamp">
    /// Optional timestamp associated with the initial risk level.
    /// </param>
    public RiskCell(
        Guid id,
        Guid areaId,
        Location location,
        RiskLevel initialRiskLevel,
        string? cellId = null,
        DateTimeOffset? initialTimestamp = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Risk cell identifier must not be an empty GUID.",
                nameof(id));
        }

        if (areaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Area identifier must not be an empty GUID.",
                nameof(areaId));
        }

        Location = location ?? throw new ArgumentNullException(nameof(location));

        Id = id;
        AreaId = areaId;
        CellId = string.IsNullOrWhiteSpace(cellId) ? null : cellId.Trim();
        CurrentRiskLevel = initialRiskLevel;

        if (initialTimestamp.HasValue)
        {
            LastUpdatedAt = initialTimestamp.Value;
            _history.Add((initialTimestamp.Value, initialRiskLevel));
        }
    }

    /// <summary>
    /// Indicates whether the provided location is considered to belong to this cell.
    /// </summary>
    /// <param name="location">
    /// Location to test against the current cell.
    /// </param>
    /// <returns>
    /// True when the location falls inside the approximated spatial extent
    /// of this cell; otherwise false.
    /// </returns>
    public bool Contains(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!string.IsNullOrWhiteSpace(CellId) &&
            !string.IsNullOrWhiteSpace(location.CellId) &&
            string.Equals(CellId, location.CellId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Location.DistanceTo(location) <= DefaultContainmentRadiusMeters;
    }

    /// <summary>
    /// Updates the qualitative risk level of the cell and records the change
    /// in the historical timeline.
    /// </summary>
    /// <param name="newLevel">
    /// New risk level to assign.
    /// </param>
    /// <param name="updatedAt">
    /// Timestamp of the update.
    /// </param>
    public void UpdateRiskLevel(RiskLevel newLevel, DateTimeOffset updatedAt)
    {
        if (updatedAt == default)
        {
            throw new ArgumentException(
                "Update time must be a valid, non-default timestamp.",
                nameof(updatedAt));
        }

        if (LastUpdatedAt.HasValue && updatedAt < LastUpdatedAt.Value)
        {
            throw new InvalidOperationException(
                $"Update time {updatedAt:O} cannot be earlier than last update {LastUpdatedAt:O}.");
        }

        CurrentRiskLevel = newLevel;
        LastUpdatedAt = updatedAt;
        _history.Add((updatedAt, newLevel));
    }

    /// <summary>
    /// Returns a very coarse qualitative trend derived from the recorded history.
    /// </summary>
    /// <returns>
    /// A human-readable trend description.
    /// </returns>
    public string GetRiskTrendDescription()
    {
        if (_history.Count < 2)
        {
            return "Unknown or insufficient data";
        }

        var first = _history[0].Level;
        var last = _history[^1].Level;

        if (last > first)
        {
            return "Increasing";
        }

        if (last < first)
        {
            return "Decreasing";
        }

        var min = first;
        var max = first;

        foreach (var (_, level) in _history)
        {
            if (level < min)
            {
                min = level;
            }

            if (level > max)
            {
                max = level;
            }
        }

        return max == min ? "Stable" : "Variable but overall stable";
    }

    /// <summary>
    /// Indicates whether the current risk level is at least the specified level.
    /// </summary>
    public bool IsAtLeast(RiskLevel level) => CurrentRiskLevel >= level;

    /// <summary>
    /// Indicates whether the current risk level is strictly above the specified level.
    /// </summary>
    public bool IsAbove(RiskLevel level) => CurrentRiskLevel > level;

    /// <summary>
    /// Indicates whether this cell has become safer compared to another state
    /// of the same logical cell.
    /// </summary>
    /// <param name="previous">
    /// Previous state of the same risk cell.
    /// </param>
    /// <returns>
    /// True when the current risk level is lower than the previous one.
    /// </returns>
    public bool HasBecomeSaferComparedTo(RiskCell previous)
    {
        ArgumentNullException.ThrowIfNull(previous);

        if (previous.Id != Id || previous.AreaId != AreaId)
        {
            throw new InvalidOperationException(
                "HasBecomeSaferComparedTo should be used to compare different states of the same risk cell.");
        }

        return CurrentRiskLevel < previous.CurrentRiskLevel;
    }
}