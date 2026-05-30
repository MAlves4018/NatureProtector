namespace NatureProtector.Simulator.Host.Services;

public static class SimulationDegradationProfiles
{
    public const string None = "none";
    public const string MissingReadings = "missing-readings";
    public const string Noise = "noise";
    public const string Bias = "bias";
    public const string Drift = "drift";
    public const string StuckValue = "stuck-value";
    public const string Outlier = "outlier";
    public const string ClippingRange = "clipping/range";
    public const string LagDelay = "lag/delay";
    public const string Duplicate = "duplicate";
    public const string OutOfOrder = "out-of-order";

    private static readonly char[] Separators = [',', ';', '|', '+'];

    public static IReadOnlyList<string> Normalize(
        IEnumerable<string?>? profiles,
        string? legacyProfile = null)
    {
        var values = new List<string>();

        AddValues(values, profiles);
        AddValue(values, legacyProfile);

        var normalized = values
            .Select(NormalizeSingle)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count > 1)
        {
            normalized.RemoveAll(value => string.Equals(value, None, StringComparison.OrdinalIgnoreCase));
        }

        return normalized;
    }

    public static IReadOnlyList<string> Resolve(
        IEnumerable<string?>? requestedProfiles,
        string? requestedLegacyProfile,
        IEnumerable<string?>? scenarioProfiles,
        string? scenarioLegacyProfile)
    {
        var requested = Normalize(requestedProfiles, requestedLegacyProfile);
        if (requested.Count > 0)
        {
            return requested;
        }

        return Normalize(scenarioProfiles, scenarioLegacyProfile);
    }

    public static IReadOnlyList<string> GetResolvedProfiles(SimulationContext context)
        => context.RunOverrides?.Resolved.DegradationProfiles
           ?? Normalize(null, context.RunOverrides?.Resolved.DegradationProfile);

    public static string? ToLegacyProfile(IReadOnlyCollection<string> profiles)
    {
        if (profiles.Count == 0)
        {
            return null;
        }

        return profiles.Count == 1
            ? profiles.First()
            : string.Join("+", profiles);
    }

    public static bool IsNoneOrEmpty(IReadOnlyCollection<string> profiles)
        => profiles.Count == 0 ||
           (profiles.Count == 1 && string.Equals(profiles.First(), None, StringComparison.OrdinalIgnoreCase));

    public static bool Contains(IReadOnlyCollection<string> profiles, string profile)
        => profiles.Any(value => string.Equals(value, profile, StringComparison.OrdinalIgnoreCase));

    private static void AddValues(List<string> values, IEnumerable<string?>? profiles)
    {
        if (profiles is null)
        {
            return;
        }

        foreach (var profile in profiles)
        {
            AddValue(values, profile);
        }
    }

    private static void AddValue(List<string> values, string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return;
        }

        foreach (var part in profile.Split(Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            values.Add(part);
        }
    }

    private static string NormalizeSingle(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "deterministic-missing-readings" => MissingReadings,
            "missing" => MissingReadings,
            "noisy-readings" => Noise,
            "noisy" => Noise,
            "stuck" => StuckValue,
            "stuck-value" => StuckValue,
            "flatline" => StuckValue,
            "range" => ClippingRange,
            "clipping" => ClippingRange,
            "clipping-range" => ClippingRange,
            "delay" => LagDelay,
            "delayed" => LagDelay,
            "lag" => LagDelay,
            "late" => LagDelay,
            "duplicate-events" => Duplicate,
            "out-of-order-events" => OutOfOrder,
            "outoforder" => OutOfOrder,
            _ => normalized
        };
    }
}
