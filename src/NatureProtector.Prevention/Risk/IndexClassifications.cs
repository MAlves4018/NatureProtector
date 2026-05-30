namespace NatureProtector.Prevention.Risk;

public sealed record FireWeatherIndexClassification(
    double? RawValue,
    double? NormalizedValue,
    string Status,
    string? IpmaClass,
    string? IpmaClassLabel,
    string? EffisClass,
    double? ThresholdDistanceToNextClass,
    string? NextIpmaClass,
    IReadOnlyList<string> Limitations)
{
    public static FireWeatherIndexClassification From(
        double? rawValue,
        double? normalizedValue,
        FireWeatherIndexCalculationStatus status)
    {
        if (!rawValue.HasValue || status is FireWeatherIndexCalculationStatus.Missing or FireWeatherIndexCalculationStatus.Partial)
        {
            return new FireWeatherIndexClassification(
                rawValue,
                normalizedValue,
                status.ToString(),
                null,
                null,
                null,
                null,
                null,
                ["fwi_class_unavailable"]);
        }

        var value = rawValue.Value;
        (string Code, string Label, string? NextCode, double? NextThreshold) classification = value switch
        {
            < 8.2 => ("Low", "Baixo/Reduzido", "Moderate", 8.2),
            < 17.2 => ("Moderate", "Moderado", "High", 17.2),
            < 24.6 => ("High", "Elevado", "VeryHigh", 24.6),
            < 38.3 => ("VeryHigh", "Muito Elevado", "Maximum", 38.3),
            < 50.1 => ("Maximum", "Maximo", "Extreme", 50.1),
            < 64.0 => ("Extreme", "Extremo", "Exceptional", 64.0),
            _ => ("Exceptional", "Excecional", null, (double?)null)
        };

        return new FireWeatherIndexClassification(
            rawValue,
            normalizedValue,
            status.ToString(),
            classification.Code,
            classification.Label,
            ClassifyEffis(value),
            classification.NextThreshold.HasValue ? Math.Round(classification.NextThreshold.Value - value, 3) : null,
            classification.NextCode,
            status == FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults
                ? ["fwi_uses_candidate_antecedent_defaults"]
                : []);
    }

    private static string ClassifyEffis(double value)
    {
        return value switch
        {
            < 5.2 => "VeryLow",
            < 11.2 => "Low",
            < 21.3 => "Moderate",
            < 38.0 => "High",
            < 50.0 => "VeryHigh",
            _ => "Extreme"
        };
    }
}

public sealed record KbdiDrynessClassification(
    double? RawValue,
    double? NormalizedValue,
    string Status,
    string? DrynessClass,
    string? DrynessClassLabel,
    string AntecedentHistoryQuality,
    IReadOnlyList<string> Limitations)
{
    public static KbdiDrynessClassification From(
        double? rawValue,
        double? normalizedValue,
        KbdiCalculationStatus status,
        string? limitations)
    {
        if (!rawValue.HasValue || status is KbdiCalculationStatus.Missing or KbdiCalculationStatus.Partial)
        {
            return new KbdiDrynessClassification(
                rawValue,
                normalizedValue,
                status.ToString(),
                null,
                null,
                "NotAvailable",
                Merge(limitations, "kbdi_class_unavailable"));
        }

        var value = Math.Clamp(rawValue.Value, 0.0, CandidateParameterSetV1.KeetchByramDroughtIndexMaximum);
        var (code, label) = value switch
        {
            < 200.0 => ("VeryLowDryness", "Secura muito baixa"),
            < 400.0 => ("LowModerateDryness", "Secura baixa a moderada"),
            < 600.0 => ("HighDryness", "Secura elevada"),
            < 700.0 => ("SevereDryness", "Secura severa"),
            _ => ("ExtremeDryness", "Secura extrema")
        };

        var history = status switch
        {
            KbdiCalculationStatus.LimitedAntecedentHistory => "LimitedAntecedentHistory",
            KbdiCalculationStatus.CompleteWithCandidateDefaults => "CandidateDefaults",
            KbdiCalculationStatus.CalculatedFromHistory => "CalculatedFromHistory",
            KbdiCalculationStatus.ReferenceImported => "ReferenceImported",
            KbdiCalculationStatus.Complete => "Complete",
            _ => "NotAvailable"
        };

        return new KbdiDrynessClassification(
            rawValue,
            normalizedValue,
            status.ToString(),
            code,
            label,
            history,
            Merge(limitations));
    }

    private static IReadOnlyList<string> Merge(string? limitations, params string[] additional)
    {
        return (limitations ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(additional.Where(value => !string.IsNullOrWhiteSpace(value)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record NatureProtectorRiskClassification(
    double? Score,
    string Status,
    string? RiskClass,
    string? RiskClassLabel,
    string ParameterSetVersion,
    IReadOnlyList<string> Limitations)
{
    public static NatureProtectorRiskClassification From(double? score)
    {
        if (!score.HasValue)
        {
            return new NatureProtectorRiskClassification(
                null,
                "Missing",
                null,
                null,
                CandidateParameterSetV1.Version,
                ["np_score_missing"]);
        }

        var value = CandidateParameterSetV1.ClampNormalized(score.Value);
        var (code, label) = value switch
        {
            < 0.2 => ("VeryLow", "Muito baixo"),
            < 0.4 => ("Low", "Baixo"),
            < 0.6 => ("Moderate", "Moderado"),
            < 0.8 => ("High", "Elevado"),
            _ => ("VeryHigh", "Muito elevado")
        };

        return new NatureProtectorRiskClassification(
            value,
            "Complete",
            code,
            label,
            CandidateParameterSetV1.Version,
            []);
    }
}

public sealed record PortugueseContextRiskProxy(
    string Status,
    string? ProxyClass,
    string? ProxyClassLabel,
    string? TerritorialHazardClass,
    string MatrixVersion,
    string Provenance,
    IReadOnlyList<string> Limitations)
{
    public static PortugueseContextRiskProxy From(
        FireWeatherIndexClassification fwiClassification,
        double? territoryComponent)
    {
        ArgumentNullException.ThrowIfNull(fwiClassification);

        if (string.IsNullOrWhiteSpace(fwiClassification.IpmaClass) || !territoryComponent.HasValue)
        {
            return new PortugueseContextRiskProxy(
                "Missing",
                null,
                null,
                territoryComponent.HasValue ? ClassifyTerritory(territoryComponent.Value) : null,
                CandidateParameterSetV1.Version,
                "candidate_portuguese_context_proxy",
                ["not_official_rcm", "missing_fwi_or_territory"]);
        }

        var territory = ClassifyTerritory(territoryComponent.Value);
        var proxy = Combine(fwiClassification.IpmaClass, territory);
        return new PortugueseContextRiskProxy(
            "Complete",
            proxy,
            Label(proxy),
            territory,
            CandidateParameterSetV1.Version,
            "candidate_portuguese_context_proxy",
            ["not_official_rcm", "does_not_use_official_icnf_rural_hazard"]);
    }

    public static string ClassifyTerritory(double territoryComponent)
    {
        var value = CandidateParameterSetV1.ClampNormalized(territoryComponent);
        return value switch
        {
            < 0.2 => "VeryLow",
            < 0.4 => "Low",
            < 0.6 => "Moderate",
            < 0.8 => "High",
            _ => "VeryHigh"
        };
    }

    private static string Combine(string fwiClass, string territoryClass)
    {
        var fwiRank = fwiClass switch
        {
            "Low" => 0,
            "Moderate" => 1,
            "High" => 2,
            "VeryHigh" => 3,
            "Maximum" => 4,
            "Extreme" => 5,
            "Exceptional" => 6,
            _ => -1
        };
        var territoryRank = territoryClass switch
        {
            "VeryLow" => 0,
            "Low" => 1,
            "Moderate" => 2,
            "High" => 3,
            "VeryHigh" => 4,
            _ => -1
        };

        if (fwiRank < 0 || territoryRank < 0)
        {
            return "Partial";
        }

        var combined = Math.Max(fwiRank, territoryRank);
        if (fwiRank >= 4 && territoryRank >= 3)
        {
            return "Extreme";
        }

        if (fwiRank >= 3 && territoryRank >= 3)
        {
            return "VeryHigh";
        }

        if (fwiRank >= 2 && territoryRank >= 3)
        {
            return "VeryHigh";
        }

        if (fwiRank >= 1 && territoryRank >= 3)
        {
            return "High";
        }

        return combined switch
        {
            <= 1 => "Low",
            2 => "Moderate",
            3 => "High",
            _ => "VeryHigh"
        };
    }

    private static string Label(string code)
    {
        return code switch
        {
            "Low" => "Baixo",
            "Moderate" => "Moderado",
            "High" => "Elevado",
            "VeryHigh" => "Muito elevado",
            "Extreme" => "Extremo",
            _ => code
        };
    }
}

public sealed record LocalFwiPercentileResult(
    string Status,
    double? Percentile,
    string? Reason)
{
    public static LocalFwiPercentileResult NotAvailable()
        => new("NotAvailable", null, "historical_local_fwi_distribution_not_materialized");
}
