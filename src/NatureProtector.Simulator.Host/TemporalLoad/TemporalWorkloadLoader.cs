using System.Text.Json;

namespace NatureProtector.Simulator.Host.TemporalLoad;

public static class TemporalWorkloadLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static TemporalWorkloadDefinition Load(string path, string workloadId)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("TemporalLoad:WorkloadPath is required when temporal load is enabled.");
        }

        if (string.IsNullOrWhiteSpace(workloadId))
        {
            throw new InvalidOperationException("TemporalLoad:WorkloadId is required when temporal load is enabled.");
        }

        using var stream = File.OpenRead(path);
        var catalog = JsonSerializer.Deserialize<TemporalWorkloadCatalog>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Temporal workload catalog '{path}' is empty or invalid.");
        var workload = catalog.Workloads.SingleOrDefault(item =>
            string.Equals(item.Id, workloadId, StringComparison.OrdinalIgnoreCase));

        return workload ?? throw new InvalidOperationException(
            $"Temporal workload '{workloadId}' was not found in '{path}'.");
    }

    public static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(value, SerializerOptions) + Environment.NewLine);
    }
}
