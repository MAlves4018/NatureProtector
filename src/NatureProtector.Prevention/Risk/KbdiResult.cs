namespace NatureProtector.Prevention.Risk;

public sealed record KbdiResult(
    KbdiCalculationStatus Status,
    double InputCompleteness,
    double? PreviousKeetchByramDroughtIndex,
    double? KeetchByramDroughtIndex,
    double? NormalizedKeetchByramDroughtIndex,
    string Provenance,
    IReadOnlyList<string> Limitations)
{
    public static KbdiResult Missing(params string[] limitations)
        => new(
            KbdiCalculationStatus.Missing,
            0.0,
            null,
            null,
            null,
            "absent",
            limitations);
}
