namespace NatureProtector.Prevention.Risk;

public enum QualityFlag
{
    Missing = 0,
    MissingValue = 1,
    Duplicate = 2,
    Delayed = 3,
    Stale = 4,
    OutOfOrder = 5,
    UnsupportedMetric = 6,
    InvalidUnit = 7,
    Dropped = 8,
    HeldLastValid = 9,
    SemanticMismatch = 10,
    DailyCellStateMissing = 11,
    Outlier = 12,
    StuckFlatline = 13,
    RangeClipping = 14,
    DegradedSensor = 15,
    LowCoverage = 16
}

public static class QualityFlagCatalog
{
    public static string ToWireName(this QualityFlag flag)
    {
        return flag switch
        {
            QualityFlag.DailyCellStateMissing => RiskInput.MissingDailyCellStateFlag,
            QualityFlag.StuckFlatline => "stuck_flatline",
            QualityFlag.RangeClipping => "range_clipping",
            QualityFlag.DegradedSensor => "degraded_sensor",
            QualityFlag.LowCoverage => "low_coverage",
            _ => flag.ToString()
        };
    }

    public static bool TryParse(string? value, out QualityFlag flag)
    {
        var normalized = NormalizeWireName(value);
        if (string.Equals(normalized, RiskInput.MissingDailyCellStateFlag, StringComparison.Ordinal))
        {
            flag = QualityFlag.DailyCellStateMissing;
            return true;
        }

        var mapped = normalized switch
        {
            "stuck_flatline" => nameof(QualityFlag.StuckFlatline),
            "range_clipping" => nameof(QualityFlag.RangeClipping),
            "degraded_sensor" => nameof(QualityFlag.DegradedSensor),
            "low_coverage" => nameof(QualityFlag.LowCoverage),
            _ => normalized
        };

        return Enum.TryParse(mapped, ignoreCase: true, out flag);
    }

    public static IReadOnlyList<QualityFlag> ParseMany(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<QualityFlag>();
        }

        return values
            .Select(value => TryParse(value, out var flag) ? (QualityFlag?)flag : null)
            .Where(flag => flag.HasValue)
            .Select(flag => flag!.Value)
            .Distinct()
            .ToArray();
    }

    private static string? NormalizeWireName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Replace("-", "_", StringComparison.Ordinal);
    }
}
