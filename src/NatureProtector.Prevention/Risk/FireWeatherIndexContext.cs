namespace NatureProtector.Prevention.Risk;

public sealed record FireWeatherIndexContext(
    double? FireWeatherIndex,
    double? KeetchByramDroughtIndex,
    string Provenance)
{
    public static FireWeatherIndexContext Absent { get; } = new(null, null, "absent");

    public bool HasAnyIndex => FireWeatherIndex.HasValue || KeetchByramDroughtIndex.HasValue;

    public bool IsImported =>
        Provenance.Contains("import", StringComparison.OrdinalIgnoreCase) ||
        Provenance.Contains("reference", StringComparison.OrdinalIgnoreCase);
}
