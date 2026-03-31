using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;

/*
 * This class represents a logical geographic area within the Nature Protector domain.
 *
 * Rationale:
 * - Area is the main aggregate root for the spatial scope of preventive wildfire analysis.
 * - It encapsulates a human-readable name, geographic boundaries and the collection
 *   of risk cells that partition the area for later assessment.
 *
 * Design considerations:
 * - The constructor enforces a minimal valid state, namely a non-empty identifier,
 *   a non-empty name and non-null boundaries.
 * - The internal list of RiskCells is kept private and exposed as a read-only view
 *   in order to preserve aggregate consistency.
 * - Risk cells are explicitly associated with the area through AreaId.
 * - The location lookup behaviour is intentionally simple for this phase:
 *   the area first checks whether the location is inside its boundaries and then
 *   attempts to resolve the corresponding risk cell by CellId or by proximity.
 */

namespace NatureProtector.Core.Areas;

public sealed class Area
{
    /// <summary>
    /// Globally unique identifier of the area.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Human-readable name of the area
    /// (e.g. "Serra da Estrela - North Sector").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Geographic boundaries that define the spatial extent of this area.
    /// </summary>
    public Boundaries Boundaries { get; }

    private readonly List<RiskCell> _riskCells = [];

    /// <summary>
    /// Read-only view over the risk cells associated with this area.
    /// </summary>
    public IReadOnlyCollection<RiskCell> RiskCells => _riskCells.AsReadOnly();

    /// <summary>
    /// Creates a new Area aggregate.
    /// </summary>
    /// <param name="id">
    /// Globally unique identifier of the area.
    /// </param>
    /// <param name="name">
    /// Human-readable name of the area.
    /// </param>
    /// <param name="boundaries">
    /// Geographic boundary box of the area.
    /// </param>
    /// <param name="riskCells">
    /// Optional initial collection of risk cells associated with the area.
    /// </param>
    public Area(
        Guid id,
        string name,
        Boundaries boundaries,
        IEnumerable<RiskCell>? riskCells = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Area identifier must not be an empty GUID.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Area name must not be null or whitespace.",
                nameof(name));
        }

        Boundaries = boundaries ?? throw new ArgumentNullException(nameof(boundaries));

        Id = id;
        Name = name.Trim();

        if (riskCells is null)
        {
            return;
        }

        var cells = riskCells.ToList();

        if (cells.Any(cell => cell.AreaId != id))
        {
            throw new ArgumentException(
                "All risk cells must belong to the same AreaId as the Area.",
                nameof(riskCells));
        }

        if (cells.GroupBy(cell => cell.Id).Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Risk cells must not contain duplicate identifiers.",
                nameof(riskCells));
        }

        _riskCells.AddRange(cells);
    }

    /// <summary>
    /// Adds a single risk cell to the area if it is not already present.
    /// </summary>
    /// <param name="riskCell">
    /// Risk cell to add to the aggregate.
    /// </param>
    public void AddRiskCell(RiskCell riskCell)
    {
        ArgumentNullException.ThrowIfNull(riskCell);

        if (riskCell.AreaId != Id)
        {
            throw new InvalidOperationException(
                $"Risk cell {riskCell.Id} does not belong to area {Id}.");
        }

        if (_riskCells.Any(existing => existing.Id == riskCell.Id))
        {
            return;
        }

        _riskCells.Add(riskCell);
    }

    /// <summary>
    /// Adds multiple risk cells to the area.
    /// </summary>
    /// <param name="cells">
    /// Collection of risk cells to add.
    /// </param>
    public void AddRiskCells(IEnumerable<RiskCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        foreach (var cell in cells)
        {
            AddRiskCell(cell);
        }
    }

    /// <summary>
    /// Removes a risk cell from the area by its identifier.
    /// </summary>
    /// <param name="riskCellId">
    /// Identifier of the risk cell to remove.
    /// </param>
    /// <returns>
    /// True if a matching risk cell was found and removed; otherwise false.
    /// </returns>
    public bool RemoveRiskCell(Guid riskCellId)
    {
        var riskCell = _riskCells.FirstOrDefault(cell => cell.Id == riskCellId);
        return riskCell is not null && _riskCells.Remove(riskCell);
    }

    /// <summary>
    /// Attempts to locate a risk cell within the area by its identifier.
    /// </summary>
    /// <param name="riskCellId">
    /// Identifier of the risk cell to search for.
    /// </param>
    /// <returns>
    /// The matching risk cell when found; otherwise null.
    /// </returns>
    private RiskCell? FindRiskCellById(Guid riskCellId)
    {
        return _riskCells.FirstOrDefault(cell => cell.Id == riskCellId);
    }

    /// <summary>
    /// Returns the risk cell with the given identifier or throws if it is not found.
    /// </summary>
    /// <param name="riskCellId">
    /// Identifier of the risk cell to retrieve.
    /// </param>
    /// <returns>
    /// The matching risk cell.
    /// </returns>
    public RiskCell GetRiskCellById(Guid riskCellId)
    {
        var cell = FindRiskCellById(riskCellId);

        if (cell is null)
        {
            throw new KeyNotFoundException(
                $"Risk cell {riskCellId} was not found in area {Id}.");
        }

        return cell;
    }

    /// <summary>
    /// Attempts to resolve the risk cell corresponding to the provided location.
    /// </summary>
    /// <param name="location">
    /// Location to resolve against the risk grid.
    /// </param>
    /// <param name="riskCell">
    /// Matching risk cell when found; otherwise null.
    /// </param>
    /// <returns>
    /// True when a matching risk cell is found; otherwise false.
    /// </returns>
    public bool TryGetRiskCellForLocation(Location location, out RiskCell? riskCell)
    {
        ArgumentNullException.ThrowIfNull(location);

        riskCell = null;

        if (!Boundaries.Contains(location))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(location.CellId))
        {
            riskCell = _riskCells.FirstOrDefault(cell =>
                !string.IsNullOrWhiteSpace(cell.CellId) &&
                string.Equals(cell.CellId, location.CellId, StringComparison.OrdinalIgnoreCase));

            if (riskCell is not null)
            {
                return true;
            }
        }

        const double toleranceMeters = 10.0;

        riskCell = _riskCells.FirstOrDefault(cell =>
            cell.Location.DistanceTo(location) <= toleranceMeters);

        return riskCell is not null;
    }

    /// <summary>
    /// Returns the risk cell corresponding to the given location, if one can be resolved.
    /// </summary>
    /// <param name="location">
    /// Location to resolve against the area's risk grid.
    /// </param>
    /// <returns>
    /// The matching risk cell when one is found; otherwise null.
    /// </returns>
    public RiskCell? GetRiskCellForLocation(Location location)
    {
        return TryGetRiskCellForLocation(location, out var riskCell)
            ? riskCell
            : null;
    }

    /// <summary>
    /// Returns a new Area instance with the same identity and boundaries,
    /// but with a replaced risk grid.
    /// </summary>
    /// <param name="newRiskCells">
    /// New collection of risk cells to associate with the area.
    /// </param>
    /// <returns>
    /// A new Area instance with the updated set of risk cells.
    /// </returns>
    public Area WithUpdatedRiskGrid(IEnumerable<RiskCell> newRiskCells)
    {
        ArgumentNullException.ThrowIfNull(newRiskCells);

        var cells = newRiskCells.ToList();

        if (cells.Any(cell => cell.AreaId != Id))
        {
            throw new ArgumentException(
                "All risk cells must belong to this area.",
                nameof(newRiskCells));
        }

        return new Area(
            id: Id,
            name: Name,
            boundaries: Boundaries,
            riskCells: cells);
    }

    /// <summary>
    /// Indicates whether a given location is within the boundaries of this area.
    /// </summary>
    /// <param name="location">
    /// Location to test against the area's boundaries.
    /// </param>
    /// <returns>
    /// True when the location is inside the area; otherwise false.
    /// </returns>
    public bool ContainsLocation(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Boundaries.Contains(location);
    }
}