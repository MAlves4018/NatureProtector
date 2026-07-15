using System.Text.Json;

namespace NatureProtector.Infrastructure.Postgres.Control;

/// <summary>
/// Reads the immutable sensor membership captured when a simulation run is created.
/// New runs persist explicit sensor identifiers; legacy runs fall back to the
/// historical sensor count and selected sensor names stored in the same metadata.
/// </summary>
public static class SimulationRunMetadata
{
    public static IReadOnlyList<Guid> ReadExpectedSensorIds(string? metadataJson)
    {
        using var document = TryParse(metadataJson);
        if (document is null)
        {
            return Array.Empty<Guid>();
        }

        if (!TryGetProperty(document.RootElement, "expected_sensor_ids", "expectedSensorIds", out var ids) ||
            ids.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Guid>();
        }

        return ids.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => Guid.TryParse(element.GetString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    public static IReadOnlySet<string> ReadSelectedSensorNames(string? metadataJson)
    {
        using var document = TryParse(metadataJson);
        if (document is null ||
            !TryGetProperty(document.RootElement, "run_overrides", "runOverrides", out var overrides) ||
            overrides.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(overrides, "resolved", "resolved", out var resolved) ||
            resolved.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(resolved, "selected_sensor_names", "selectedSensorNames", out var names) ||
            names.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return names.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString())
            .OfType<string>()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static int? ReadExpectedSensorCount(string? metadataJson)
    {
        var ids = ReadExpectedSensorIds(metadataJson);
        if (ids.Count > 0)
        {
            return ids.Count;
        }

        using var document = TryParse(metadataJson);
        if (document is null ||
            !TryGetProperty(document.RootElement, "sensor_count", "sensorCount", out var count) ||
            count.ValueKind != JsonValueKind.Number ||
            !count.TryGetInt32(out var resolvedCount) ||
            resolvedCount <= 0)
        {
            return null;
        }

        return resolvedCount;
    }

    private static JsonDocument? TryParse(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(metadataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string primaryName,
        string alternateName,
        out JsonElement property)
    {
        if (element.TryGetProperty(primaryName, out property))
        {
            return true;
        }

        return element.TryGetProperty(alternateName, out property);
    }
}
