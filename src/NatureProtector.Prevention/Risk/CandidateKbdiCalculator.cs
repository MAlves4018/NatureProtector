namespace NatureProtector.Prevention.Risk;

/// <summary>
/// Candidate KBDI implementation for V1 drought context. Values are for
/// operational comparison/provenance, not local calibration.
/// </summary>
public sealed class CandidateKbdiCalculator : IKbdiCalculator
{
    public const double InitialKeetchByramDroughtIndex = 650.0;

    public KbdiResult Calculate(KbdiInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var limitations = new List<string>();
        if (!input.MaxTemperatureCelsius.HasValue)
        {
            limitations.Add("max_temperature_missing");
        }

        if (!input.Precipitation24hMillimeters.HasValue)
        {
            limitations.Add("precipitation_24h_missing");
        }

        var providedInputs = new[]
            {
                input.MaxTemperatureCelsius,
                input.Precipitation24hMillimeters
            }
            .Count(item => item.HasValue);
        var completeness = providedInputs / 2.0;

        if (limitations.Count > 0)
        {
            return new KbdiResult(
                providedInputs == 0 ? KbdiCalculationStatus.Missing : KbdiCalculationStatus.Partial,
                completeness,
                input.PreviousKeetchByramDroughtIndex,
                null,
                null,
                "candidate_kbdi_calculator",
                limitations);
        }

        var previousKbdi = input.PreviousKeetchByramDroughtIndex ?? InitialKeetchByramDroughtIndex;
        var hasAntecedent = input.PreviousKeetchByramDroughtIndex.HasValue;
        if (!hasAntecedent)
        {
            limitations.Add("antecedent_kbdi_candidate_default");
            limitations.Add("limited_antecedent_history");
        }

        var meanAnnualRainInches = input.MeanAnnualRainInches ??
            CandidateParameterSetV1.CandidateMeanAnnualRainInches;
        if (!input.MeanAnnualRainInches.HasValue)
        {
            limitations.Add("mean_annual_rain_candidate_default");
        }

        meanAnnualRainInches = Math.Max(
            meanAnnualRainInches,
            CandidateParameterSetV1.MinimumMeanAnnualRainInches);

        var kbdi = ComputeKbdi(
            input.MaxTemperatureCelsius!.Value,
            input.Precipitation24hMillimeters!.Value,
            previousKbdi,
            meanAnnualRainInches);
        var normalized = NormalizeKbdi(kbdi);

        var status = !hasAntecedent
            ? KbdiCalculationStatus.LimitedAntecedentHistory
            : limitations.Count == 0
                ? KbdiCalculationStatus.Complete
                : KbdiCalculationStatus.CompleteWithCandidateDefaults;

        return new KbdiResult(
            status,
            1.0,
            Math.Round(previousKbdi, 3),
            Math.Round(kbdi, 3),
            Math.Round(normalized, 6),
            "candidate_kbdi_calculator",
            limitations);
    }

    public static double NormalizeKbdi(double kbdi)
    {
        return CandidateParameterSetV1.ClampNormalized(
            kbdi / CandidateParameterSetV1.KeetchByramDroughtIndexMaximum);
    }

    private static double ComputeKbdi(
        double tempCelsius,
        double rainMillimeters,
        double previousKbdi,
        double meanAnnualRainInches)
    {
        var kbdi = Math.Clamp(
            previousKbdi,
            0.0,
            CandidateParameterSetV1.KeetchByramDroughtIndexMaximum);
        var effectiveRainMillimeters = Math.Max(rainMillimeters - 5.08, 0.0);

        if (effectiveRainMillimeters > 0.0)
        {
            kbdi = Math.Max(kbdi - (effectiveRainMillimeters / 0.254), 0.0);
        }

        var tempFahrenheit = tempCelsius * 9.0 / 5.0 + 32.0;
        var droughtFactor =
            ((CandidateParameterSetV1.KeetchByramDroughtIndexMaximum - kbdi) *
             (0.968 * Math.Exp(0.0486 * tempFahrenheit) - 8.30) *
             0.001) /
            (1.0 + 10.88 * Math.Exp(-0.0441 * meanAnnualRainInches));

        return Math.Clamp(
            kbdi + Math.Max(droughtFactor, 0.0),
            0.0,
            CandidateParameterSetV1.KeetchByramDroughtIndexMaximum);
    }
}
