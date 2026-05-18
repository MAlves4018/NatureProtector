namespace NatureProtector.Prevention.Risk;

public sealed record TerritorialRiskContext(
    Guid? GridCellId,
    string Source,
    double StructuralHazardScore)
{
    public static TerritorialRiskContext Unknown(Guid? gridCellId)
        => new(gridCellId, "unknown", 0.5);
}
