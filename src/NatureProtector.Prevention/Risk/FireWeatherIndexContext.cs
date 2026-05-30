namespace NatureProtector.Prevention.Risk;

public sealed record FireWeatherIndexContext
{
    public FireWeatherIndexContext(
        double? FireWeatherIndex,
        double? KeetchByramDroughtIndex,
        string Provenance,
        double? FineFuelMoistureCode = null,
        double? DuffMoistureCode = null,
        double? DroughtCode = null,
        double? InitialSpreadIndex = null,
        double? BuildupIndex = null,
        double? NormalizedFireWeatherIndex = null,
        double? PreviousKeetchByramDroughtIndex = null,
        double? NormalizedKeetchByramDroughtIndex = null,
        FireWeatherIndexCalculationStatus? CalculationStatus = null,
        KbdiCalculationStatus? KbdiStatus = null,
        string? Limitations = null)
    {
        this.FireWeatherIndex = FireWeatherIndex;
        this.KeetchByramDroughtIndex = KeetchByramDroughtIndex;
        this.Provenance = string.IsNullOrWhiteSpace(Provenance) ? "absent" : Provenance.Trim();
        this.FineFuelMoistureCode = FineFuelMoistureCode;
        this.DuffMoistureCode = DuffMoistureCode;
        this.DroughtCode = DroughtCode;
        this.InitialSpreadIndex = InitialSpreadIndex;
        this.BuildupIndex = BuildupIndex;
        this.NormalizedFireWeatherIndex = NormalizedFireWeatherIndex ??
            (FireWeatherIndex.HasValue
                ? CanadianFireWeatherIndexCalculator.NormalizeFireWeatherIndex(FireWeatherIndex.Value)
                : null);
        this.PreviousKeetchByramDroughtIndex = PreviousKeetchByramDroughtIndex;
        this.NormalizedKeetchByramDroughtIndex = NormalizedKeetchByramDroughtIndex ??
            (KeetchByramDroughtIndex.HasValue
                ? CandidateKbdiCalculator.NormalizeKbdi(KeetchByramDroughtIndex.Value)
                : null);
        this.CalculationStatus = CalculationStatus ?? (FireWeatherIndex.HasValue
            ? FireWeatherIndexCalculationStatus.Complete
            : FireWeatherIndexCalculationStatus.Missing);
        this.KbdiCalculationStatus = KbdiStatus ?? (KeetchByramDroughtIndex.HasValue
            ? KbdiCalculationStatus.Complete
            : KbdiCalculationStatus.Missing);
        this.Limitations = string.IsNullOrWhiteSpace(Limitations) ? null : Limitations.Trim();
    }

    public static FireWeatherIndexContext Absent { get; } = new(null, null, "absent");

    public double? FireWeatherIndex { get; }

    public double? KeetchByramDroughtIndex { get; }

    public string Provenance { get; }

    public double? FineFuelMoistureCode { get; }

    public double? DuffMoistureCode { get; }

    public double? DroughtCode { get; }

    public double? InitialSpreadIndex { get; }

    public double? BuildupIndex { get; }

    public double? NormalizedFireWeatherIndex { get; }

    public double? PreviousKeetchByramDroughtIndex { get; }

    public double? NormalizedKeetchByramDroughtIndex { get; }

    public FireWeatherIndexCalculationStatus CalculationStatus { get; }

    public KbdiCalculationStatus KbdiCalculationStatus { get; }

    public string? Limitations { get; }

    public bool HasAnyIndex => FireWeatherIndex.HasValue || KeetchByramDroughtIndex.HasValue;

    public bool IsImported =>
        Provenance.Contains("import", StringComparison.OrdinalIgnoreCase) ||
        Provenance.Contains("reference", StringComparison.OrdinalIgnoreCase);

    public static FireWeatherIndexContext FromResult(FireWeatherIndexResult result, double? kbdi = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new FireWeatherIndexContext(
            FireWeatherIndex: result.FireWeatherIndex,
            KeetchByramDroughtIndex: kbdi,
            Provenance: result.Provenance,
            FineFuelMoistureCode: result.FineFuelMoistureCode,
            DuffMoistureCode: result.DuffMoistureCode,
            DroughtCode: result.DroughtCode,
            InitialSpreadIndex: result.InitialSpreadIndex,
            BuildupIndex: result.BuildupIndex,
            NormalizedFireWeatherIndex: result.NormalizedFireWeatherIndex,
            CalculationStatus: result.Status,
            Limitations: result.Limitations.Count == 0
                ? null
                : string.Join(";", result.Limitations));
    }

    public FireWeatherIndexContext WithKbdi(KbdiResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new FireWeatherIndexContext(
            FireWeatherIndex: FireWeatherIndex,
            KeetchByramDroughtIndex: result.KeetchByramDroughtIndex,
            Provenance: Provenance == "absent" ? result.Provenance : $"{Provenance};{result.Provenance}",
            FineFuelMoistureCode: FineFuelMoistureCode,
            DuffMoistureCode: DuffMoistureCode,
            DroughtCode: DroughtCode,
            InitialSpreadIndex: InitialSpreadIndex,
            BuildupIndex: BuildupIndex,
            NormalizedFireWeatherIndex: NormalizedFireWeatherIndex,
            PreviousKeetchByramDroughtIndex: result.PreviousKeetchByramDroughtIndex,
            NormalizedKeetchByramDroughtIndex: result.NormalizedKeetchByramDroughtIndex,
            CalculationStatus: CalculationStatus,
            KbdiStatus: result.Status,
            Limitations: CombineLimitations(Limitations, result.Limitations));
    }

    private static string? CombineLimitations(string? existing, IReadOnlyList<string> incoming)
    {
        var limitations = new List<string>();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            limitations.AddRange(existing.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        limitations.AddRange(incoming.Where(item => !string.IsNullOrWhiteSpace(item)));
        var distinct = limitations.Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 0 ? null : string.Join(";", distinct);
    }
}
