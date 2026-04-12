using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;

/*
 * This class represents one territorial grid cell inside a configured Area.
 *
 * Rationale:
 * - The project now distinguishes between the territorial control-plane grid
 *   and the runtime-oriented risk cells used by prevention.
 * - GridCell is the stable spatial unit that can be hydrated from curated
 *   datasets and later persisted in PostgreSQL.
 * - RiskCell can then be derived from GridCell when a runtime risk view is needed.
 *
 * Design considerations:
 * - Identity, ownership and the cell code are immutable after construction.
 * - The class keeps the current phase intentionally simple: centroid-based
 *   location plus stable or semi-stable territorial attributes.
 * - Nullable attributes are used because the curated dataset is still growing
 *   and some fields, such as altitude or conjunctural hazard, may not yet exist.
 */

namespace NatureProtector.Core.Areas;

public sealed class GridCell
{
    /// <summary>
    /// Globally unique identifier of the grid cell.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Identifier of the area to which the cell belongs.
    /// </summary>
    public Guid AreaId { get; }

    /// <summary>
    /// Stable logical code of the grid cell.
    /// </summary>
    public string CellCode { get; }

    /// <summary>
    /// Representative centroid of the cell.
    /// </summary>
    public Location Centroid { get; }

    /// <summary>
    /// Optional altitude in meters.
    /// </summary>
    public double? AltitudeMeters { get; }

    /// <summary>
    /// Optional slope in degrees.
    /// </summary>
    public double? SlopeDegrees { get; }

    /// <summary>
    /// Optional aspect in degrees.
    /// </summary>
    public double? AspectDegrees { get; }

    /// <summary>
    /// Optional dominant land cover class.
    /// </summary>
    public string? LandCoverClass { get; }

    /// <summary>
    /// Optional dominant forest type.
    /// </summary>
    public string? DominantForestType { get; }

    /// <summary>
    /// Optional dominant fuel model.
    /// </summary>
    public string? DominantFuelModel { get; }

    /// <summary>
    /// Optional tree cover density in the range [0, 100].
    /// </summary>
    public double? TreeCoverDensity { get; }

    /// <summary>
    /// Optional structural hazard label.
    /// </summary>
    public string? StructuralHazard { get; }

    /// <summary>
    /// Optional conjunctural hazard label.
    /// </summary>
    public string? ConjuncturalHazard { get; }

    public GridCell(
        Guid id,
        Guid areaId,
        string cellCode,
        Location centroid,
        double? altitudeMeters = null,
        double? slopeDegrees = null,
        double? aspectDegrees = null,
        string? landCoverClass = null,
        string? dominantForestType = null,
        string? dominantFuelModel = null,
        double? treeCoverDensity = null,
        string? structuralHazard = null,
        string? conjuncturalHazard = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Grid cell identifier must not be an empty GUID.",
                nameof(id));
        }

        if (areaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Area identifier must not be an empty GUID.",
                nameof(areaId));
        }

        if (string.IsNullOrWhiteSpace(cellCode))
        {
            throw new ArgumentException(
                "Grid cell code must not be null or whitespace.",
                nameof(cellCode));
        }

        Centroid = centroid ?? throw new ArgumentNullException(nameof(centroid));

        if (slopeDegrees is < 0.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slopeDegrees),
                slopeDegrees,
                "Slope must be in the range [0, 90] degrees when provided.");
        }

        if (aspectDegrees is < 0.0 or > 360.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aspectDegrees),
                aspectDegrees,
                "Aspect must be in the range [0, 360] degrees when provided.");
        }

        if (treeCoverDensity is < 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(treeCoverDensity),
                treeCoverDensity,
                "Tree cover density must be in the range [0, 100] when provided.");
        }

        Id = id;
        AreaId = areaId;
        CellCode = cellCode.Trim();
        AltitudeMeters = altitudeMeters;
        SlopeDegrees = slopeDegrees;
        AspectDegrees = aspectDegrees;
        LandCoverClass = NormalizeOptional(landCoverClass);
        DominantForestType = NormalizeOptional(dominantForestType);
        DominantFuelModel = NormalizeOptional(dominantFuelModel);
        TreeCoverDensity = treeCoverDensity;
        StructuralHazard = NormalizeOptional(structuralHazard);
        ConjuncturalHazard = NormalizeOptional(conjuncturalHazard);
    }

    /// <summary>
    /// Converts the territorial cell into a risk-oriented cell anchored at the same centroid.
    /// </summary>
    public RiskCell ToRiskCell(
        RiskLevel initialRiskLevel = RiskLevel.Low,
        DateTimeOffset? initialTimestamp = null)
    {
        return new RiskCell(
            id: Id,
            areaId: AreaId,
            location: Centroid.WithCellId(CellCode),
            initialRiskLevel: initialRiskLevel,
            cellId: CellCode,
            initialTimestamp: initialTimestamp);
    }

    /// <summary>
    /// Returns a copy of the current grid cell with updated terrain-related fields.
    /// </summary>
    public GridCell WithTerrain(
        double? altitudeMeters,
        double? slopeDegrees,
        double? aspectDegrees)
    {
        return new GridCell(
            id: Id,
            areaId: AreaId,
            cellCode: CellCode,
            centroid: Centroid,
            altitudeMeters: altitudeMeters,
            slopeDegrees: slopeDegrees,
            aspectDegrees: aspectDegrees,
            landCoverClass: LandCoverClass,
            dominantForestType: DominantForestType,
            dominantFuelModel: DominantFuelModel,
            treeCoverDensity: TreeCoverDensity,
            structuralHazard: StructuralHazard,
            conjuncturalHazard: ConjuncturalHazard);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
