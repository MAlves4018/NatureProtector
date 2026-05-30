using System.Globalization;
using System.Text.Json;
using NatureProtector.Core.Scenarios;

/*
 * Este helper aplica ao simulador as opções derivadas de um manifesto de
 * cenário previamente gerado.
 *
 * Rationale:
 * - Os cenários construídos offline devem poder alimentar o simulador sem
 *   obrigar a duplicar parâmetros no appsettings.
 * - A resolução do manifesto fica isolada da composição do host para manter o
 *   arranque simples.
 *
 * Design considerations:
 * - O ficheiro pode representar um único cenário ou um catálogo com vários.
 * - Os valores do manifesto são lidos de forma tolerante para acomodar dados
 *   vindos dos scripts de geração.
 * - Este mecanismo não é suportado quando o simulador usa o control plane.
 */

namespace NatureProtector.Simulator.Host.Configuration;

internal static class GeneratedScenarioManifestLoader
{
    /// <summary>
    /// Aplica as opções de um manifesto gerado quando a configuração o pede.
    /// </summary>
    public static void ApplyIfConfigured(SimulatorOptions options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (string.IsNullOrWhiteSpace(options.ScenarioManifestPath))
        {
            return;
        }

        var fullPath = Path.IsPathRooted(options.ScenarioManifestPath)
            ? options.ScenarioManifestPath
            : Path.GetFullPath(Path.Combine(contentRootPath, options.ScenarioManifestPath));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Configured ScenarioManifestPath was not found: {fullPath}",
                fullPath);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        var selectedScenario = ResolveScenario(document.RootElement, options.ScenarioManifestScenarioKey);

        ApplyScenarioOptions(options, selectedScenario);
    }

    private static JsonElement ResolveScenario(JsonElement root, string? scenarioKey)
    {
        if (root.TryGetProperty("scenarios", out var scenariosElement))
        {
            if (scenariosElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Generated scenario catalog contains an invalid 'scenarios' node.");
            }

            if (string.IsNullOrWhiteSpace(scenarioKey))
            {
                throw new InvalidOperationException(
                    "ScenarioManifestScenarioKey must be configured when ScenarioManifestPath points to a catalog file.");
            }

            foreach (var scenario in scenariosElement.EnumerateArray())
            {
                var key = ReadString(scenario, "scenario_key");
                var id = ReadString(scenario, "scenario_id");
                if (string.Equals(key, scenarioKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(id, scenarioKey, StringComparison.OrdinalIgnoreCase))
                {
                    return scenario;
                }
            }

            throw new InvalidOperationException(
                $"Scenario key '{scenarioKey}' was not found in the generated scenario catalog.");
        }

        return root;
    }

    private static void ApplyScenarioOptions(SimulatorOptions options, JsonElement scenario)
    {
        var simulatorOptions = scenario.TryGetProperty("simulator_options", out var simulatorOptionsElement)
            ? simulatorOptionsElement
            : throw new InvalidOperationException("Generated scenario manifest does not contain 'simulator_options'.");

        options.AreaId = ReadGuid(simulatorOptions, "AreaId", options.AreaId);
        options.ScenarioId = ReadGuid(simulatorOptions, "ScenarioId", options.ScenarioId);
        options.ScenarioName = ReadString(simulatorOptions, "ScenarioName") ?? options.ScenarioName;
        options.ScenarioDescription = ReadString(simulatorOptions, "ScenarioDescription") ?? options.ScenarioDescription;
        options.ScenarioCategory = ReadScenarioCategory(simulatorOptions, "ScenarioCategory", options.ScenarioCategory);
        options.StartTimestamp = ReadDateTimeOffset(simulatorOptions, "StartTimestamp", options.StartTimestamp);
        options.BaseTemperature = ReadDouble(simulatorOptions, "BaseTemperature", options.BaseTemperature);
        options.BaseHumidity = ReadDouble(simulatorOptions, "BaseHumidity", options.BaseHumidity);
        options.BaseWindSpeed = ReadDouble(simulatorOptions, "BaseWindSpeed", options.BaseWindSpeed);
        options.FailureRate = ReadDouble(simulatorOptions, "FailureRate", options.FailureRate);
        options.NoiseLevel = ReadDouble(simulatorOptions, "NoiseLevel", options.NoiseLevel);
        options.DegradationProfile = ReadString(simulatorOptions, "DegradationProfile") ?? options.DegradationProfile;
        options.DegradationProfiles = ReadStringArray(simulatorOptions, "DegradationProfiles", options.DegradationProfiles);
        options.TimeAcceleration = ReadDouble(simulatorOptions, "TimeAcceleration", options.TimeAcceleration);
        options.NumberOfCycles = ReadInt(simulatorOptions, "NumberOfCycles", options.NumberOfCycles);
        options.IntervalSeconds = ReadInt(simulatorOptions, "IntervalSeconds", options.IntervalSeconds);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.GetString();
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName, List<string> fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }

    private static Guid ReadGuid(JsonElement element, string propertyName, Guid fallback)
    {
        var raw = ReadString(element, propertyName);
        return Guid.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static double ReadDouble(JsonElement element, string propertyName, double fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.GetDouble(),
            JsonValueKind.String when double.TryParse(
                property.GetString(),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => fallback
        };
    }

    private static int ReadInt(JsonElement element, string propertyName, int fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.GetInt32(),
            JsonValueKind.String when int.TryParse(
                property.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => fallback
        };
    }

    private static DateTimeOffset? ReadDateTimeOffset(
        JsonElement element,
        string propertyName,
        DateTimeOffset? fallback)
    {
        var raw = ReadString(element, propertyName);
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static ScenarioCategory ReadScenarioCategory(
        JsonElement element,
        string propertyName,
        ScenarioCategory fallback)
    {
        var raw = ReadString(element, propertyName);
        return Enum.TryParse<ScenarioCategory>(raw, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
