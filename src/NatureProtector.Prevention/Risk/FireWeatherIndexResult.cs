namespace NatureProtector.Prevention.Risk;

public sealed record FireWeatherIndexResult(
    FireWeatherIndexCalculationStatus Status,
    double InputCompleteness,
    double? FineFuelMoistureCode,
    double? DuffMoistureCode,
    double? DroughtCode,
    double? InitialSpreadIndex,
    double? BuildupIndex,
    double? FireWeatherIndex,
    double? NormalizedFireWeatherIndex,
    string Provenance,
    IReadOnlyList<string> Limitations)
{
    public static FireWeatherIndexResult Missing(params string[] limitations)
        => new(
            FireWeatherIndexCalculationStatus.Missing,
            0.0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "absent",
            limitations);
}
