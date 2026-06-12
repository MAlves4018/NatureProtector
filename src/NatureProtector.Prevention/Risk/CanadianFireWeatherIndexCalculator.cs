namespace NatureProtector.Prevention.Risk;

/// <summary>
/// Candidate implementation of the Canadian FWI equations used for V1
/// comparison/provenance. It is not local scientific calibration.
/// </summary>
public sealed class CanadianFireWeatherIndexCalculator : IFireWeatherIndexCalculator
{
    public const double InitialFineFuelMoistureCode = 85.0;
    public const double InitialDuffMoistureCode = 150.0;
    public const double InitialDroughtCode = 650.0;

    private static readonly double[] DmcDayLengthFactors =
        [6.5, 7.5, 9.0, 12.8, 13.9, 13.9, 12.4, 10.9, 9.4, 8.0, 7.0, 6.0];

    private static readonly double[] DcDryingFactors =
        [-1.6, -1.6, -1.6, 0.9, 3.8, 5.8, 6.4, 5.0, 2.4, 0.4, -1.6, -1.6];

    public FireWeatherIndexResult Calculate(FireWeatherIndexInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var limitations = new List<string>();
        AddMissingLimitations(input, limitations);

        var providedInputs = new[]
            {
                input.TemperatureCelsius,
                input.RelativeHumidityPercent,
                input.WindSpeedMetersPerSecond,
                input.Precipitation24hMillimeters
            }
            .Count(item => item.HasValue);
        var completeness = providedInputs / 4.0;

        if (limitations.Count > 0)
        {
            return new FireWeatherIndexResult(
                providedInputs == 0
                    ? FireWeatherIndexCalculationStatus.Missing
                    : FireWeatherIndexCalculationStatus.Partial,
                completeness,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "candidate_fwi_calculator",
                limitations);
        }

        var month = Math.Clamp(input.Month, 1, 12);
        var temp = Math.Clamp(input.TemperatureCelsius!.Value, -20.0, 60.0);
        var humidity = Math.Clamp(input.RelativeHumidityPercent!.Value, 0.0, 100.0);
        var windKmh = Math.Max(input.WindSpeedMetersPerSecond!.Value * 3.6, 0.0);
        var rain = Math.Max(input.Precipitation24hMillimeters!.Value, 0.0);
        var previousFfmc = input.PreviousFineFuelMoistureCode ?? InitialFineFuelMoistureCode;
        var previousDmc = input.PreviousDuffMoistureCode ?? InitialDuffMoistureCode;
        var previousDc = input.PreviousDroughtCode ?? InitialDroughtCode;

        if (!input.PreviousFineFuelMoistureCode.HasValue ||
            !input.PreviousDuffMoistureCode.HasValue ||
            !input.PreviousDroughtCode.HasValue)
        {
            limitations.Add("antecedent_fwi_codes_candidate_defaults");
        }

        var ffmc = ComputeFfmc(temp, humidity, windKmh, rain, previousFfmc);
        var dmc = ComputeDmc(temp, humidity, rain, previousDmc, month);
        var dc = ComputeDc(temp, rain, previousDc, month);
        var isi = ComputeIsi(ffmc, windKmh);
        var bui = ComputeBui(dmc, dc);
        var fwi = ComputeFwi(isi, bui);
        var normalized = NormalizeFireWeatherIndex(fwi);

        var status = limitations.Count == 0
            ? FireWeatherIndexCalculationStatus.Complete
            : FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults;

        return new FireWeatherIndexResult(
            status,
            1.0,
            Math.Round(ffmc, 3),
            Math.Round(dmc, 3),
            Math.Round(dc, 3),
            Math.Round(isi, 3),
            Math.Round(bui, 3),
            Math.Round(fwi, 3),
            Math.Round(normalized, 6),
            "candidate_fwi_calculator",
            limitations);
    }

    public static double NormalizeFireWeatherIndex(double fireWeatherIndex)
    {
        return CandidateParameterSetV1.ClampNormalized(
            fireWeatherIndex / CandidateParameterSetV1.FireWeatherIndexNormalizationReference);
    }

    private static void AddMissingLimitations(FireWeatherIndexInput input, List<string> limitations)
    {
        if (!input.TemperatureCelsius.HasValue)
        {
            limitations.Add("temperature_missing");
        }

        if (!input.RelativeHumidityPercent.HasValue)
        {
            limitations.Add("relative_humidity_missing");
        }

        if (!input.WindSpeedMetersPerSecond.HasValue)
        {
            limitations.Add("wind_speed_missing");
        }

        if (!input.Precipitation24hMillimeters.HasValue)
        {
            limitations.Add("precipitation_24h_missing");
        }
    }

    private static double ComputeFfmc(double tempC, double rhPct, double windKmh, double rainMm, double previousFfmc)
    {
        var mo = 147.2 * (101.0 - previousFfmc) / (59.5 + previousFfmc);

        if (rainMm > 0.5)
        {
            var effectiveRain = rainMm - 0.5;
            mo += 42.5 *
                effectiveRain *
                Math.Exp(-100.0 / (251.0 - mo)) *
                (1.0 - Math.Exp(-6.93 / effectiveRain));
            if (mo > 150.0)
            {
                mo += 0.0015 * Math.Pow(mo - 150.0, 2.0) * Math.Sqrt(effectiveRain);
            }

            mo = Math.Min(mo, 250.0);
        }

        var ed =
            0.942 * Math.Pow(rhPct, 0.679) +
            11.0 * Math.Exp((rhPct - 100.0) / 10.0) +
            0.18 * (21.1 - tempC) * (1.0 - Math.Exp(-0.115 * rhPct));

        double m;
        if (mo < ed)
        {
            var ew =
                0.618 * Math.Pow(rhPct, 0.753) +
                10.0 * Math.Exp((rhPct - 100.0) / 10.0) +
                0.18 * (21.1 - tempC) * (1.0 - Math.Exp(-0.115 * rhPct));
            if (mo <= ew)
            {
                m = mo;
            }
            else
            {
                var kl =
                    0.424 * (1.0 - Math.Pow((100.0 - rhPct) / 100.0, 1.7)) +
                    0.0694 * Math.Sqrt(windKmh) * (1.0 - Math.Pow((100.0 - rhPct) / 100.0, 8.0));
                var kw = kl * 0.581 * Math.Exp(0.0365 * tempC);
                m = ew - (ew - mo) / Math.Pow(10.0, kw);
            }
        }
        else
        {
            var kl =
                0.424 * (1.0 - Math.Pow(rhPct / 100.0, 1.7)) +
                0.0694 * Math.Sqrt(windKmh) * (1.0 - Math.Pow(rhPct / 100.0, 8.0));
            var kw = kl * 0.581 * Math.Exp(0.0365 * tempC);
            m = ed + (mo - ed) / Math.Pow(10.0, kw);
        }

        var ffmc = 59.5 * (250.0 - m) / (147.2 + m);
        return Math.Clamp(ffmc, 0.0, 101.0);
    }

    private static double ComputeDmc(double tempC, double rhPct, double rainMm, double previousDmc, int month)
    {
        var temperature = Math.Max(tempC, -1.1);
        var dmc = previousDmc;

        if (rainMm > 1.5)
        {
            var effectiveRain = 0.92 * rainMm - 1.27;
            var moistureContent = 20.0 + Math.Exp(5.6348 - previousDmc / 43.43);
            double b;
            if (previousDmc <= 33.0)
            {
                b = 100.0 / (0.5 + 0.3 * previousDmc);
            }
            else if (previousDmc <= 65.0)
            {
                b = 14.0 - 1.3 * Math.Log(previousDmc);
            }
            else
            {
                b = 6.2 * Math.Log(previousDmc) - 17.2;
            }

            var revisedMoisture = moistureContent + (1000.0 * effectiveRain) / (48.77 + b * effectiveRain);
            dmc = 244.72 - 43.43 * Math.Log(revisedMoisture - 20.0);
            dmc = Math.Max(dmc, 0.0);
        }

        var dryingRate =
            1.894 *
            (temperature + 1.1) *
            (100.0 - rhPct) *
            DmcDayLengthFactors[month - 1] *
            0.000001;
        return Math.Max(dmc + 100.0 * Math.Max(dryingRate, 0.0), 0.0);
    }

    private static double ComputeDc(double tempC, double rainMm, double previousDc, int month)
    {
        var dc = previousDc;

        if (rainMm > 2.8)
        {
            var effectiveRain = 0.83 * rainMm - 1.27;
            var moistureEquivalent = 800.0 * Math.Exp(-previousDc / 400.0);
            var revisedMoisture = moistureEquivalent + 3.937 * effectiveRain;
            dc = 400.0 * Math.Log(800.0 / revisedMoisture);
            dc = Math.Max(dc, 0.0);
        }

        var temperature = Math.Max(tempC, -2.8);
        var drying = 0.36 * (temperature + 2.8) + DcDryingFactors[month - 1];
        return Math.Max(dc + 0.5 * Math.Max(drying, 0.0), 0.0);
    }

    private static double ComputeIsi(double ffmc, double windKmh)
    {
        var moisture = 147.2 * (101.0 - ffmc) / (59.5 + ffmc);
        var windFunction = Math.Exp(0.05039 * windKmh);
        var fineFuelFunction =
            91.9 *
            Math.Exp(-0.1386 * moisture) *
            (1.0 + Math.Pow(moisture, 5.31) / 49_300_000.0);
        return 0.208 * windFunction * fineFuelFunction;
    }

    private static double ComputeBui(double dmc, double dc)
    {
        var bui = dmc <= 0.4 * dc
            ? (dmc + 0.4 * dc) > 0.0 ? (0.8 * dmc * dc) / (dmc + 0.4 * dc) : 0.0
            : dmc - (1.0 - (0.8 * dc) / (dmc + 0.4 * dc)) * (0.92 + Math.Pow(0.0114 * dmc, 1.7));
        return Math.Max(bui, 0.0);
    }

    private static double ComputeFwi(double isi, double bui)
    {
        var fuelAvailable = bui <= 80.0
            ? 0.626 * Math.Pow(bui, 0.809) + 2.0
            : 1000.0 / (25.0 + 108.64 * Math.Exp(-0.023 * bui));
        var spread = 0.1 * isi * fuelAvailable;
        return spread <= 1.0
            ? Math.Max(spread, 0.0)
            : Math.Exp(2.72 * Math.Pow(0.434 * Math.Log(spread), 0.647));
    }
}
