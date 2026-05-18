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
    DailyCellStateMissing = 11
}

public static class QualityFlagCatalog
{
    public static string ToWireName(this QualityFlag flag)
    {
        return flag switch
        {
            QualityFlag.DailyCellStateMissing => RiskInput.MissingDailyCellStateFlag,
            _ => flag.ToString()
        };
    }

    public static bool TryParse(string? value, out QualityFlag flag)
    {
        if (string.Equals(value, RiskInput.MissingDailyCellStateFlag, StringComparison.Ordinal))
        {
            flag = QualityFlag.DailyCellStateMissing;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: false, out flag);
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
}
