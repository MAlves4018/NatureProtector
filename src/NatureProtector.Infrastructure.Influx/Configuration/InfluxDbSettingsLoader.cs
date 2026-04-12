using System.Text;

namespace NatureProtector.Infrastructure.Influx.Configuration;

/*
 * Este helper completa a configuração de InfluxDB com valores do ambiente ou do
 * ficheiro `.env`.
 *
 * Rationale:
 * - A baseline local usa frequentemente segredos e portas definidos fora do
 *   appsettings.
 * - O mesmo mecanismo precisa de servir hosts, testes e scripts.
 */

public static class InfluxDbSettingsLoader
{
    /// <summary>
    /// Aplica fallbacks de ambiente e `.env` quando as opções não estão
    /// explicitamente preenchidas.
    /// </summary>
    public static void ApplyEnvironmentOrDotEnvFallbacks(InfluxDbOptions options, string basePath)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dotEnvValues = LoadDotEnvValues(basePath);

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            options.Url = ResolveUrl(dotEnvValues);
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            options.Token = GetValue("INFLUXDB_TOKEN", dotEnvValues, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(options.Organization))
        {
            options.Organization = GetValue("INFLUXDB_ORGANIZATION", dotEnvValues, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            options.Bucket = GetValue(
                "INFLUXDB_BUCKET",
                dotEnvValues,
                GetValue("INFLUXDB_DATABASE", dotEnvValues, string.Empty));
        }
    }

    /// <summary>
    /// Resolve a URL de ligação a partir da configuração disponível.
    /// </summary>
    private static string ResolveUrl(IReadOnlyDictionary<string, string> dotEnvValues)
    {
        var explicitUrl = GetValue("INFLUXDB_URL", dotEnvValues, string.Empty);

        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl;
        }

        var port = GetValue("INFLUXDB_PORT", dotEnvValues, "8181");
        return $"http://localhost:{port}";
    }

    /// <summary>
    /// Procura um ficheiro `.env` subindo a partir do caminho base indicado.
    /// </summary>
    private static IReadOnlyDictionary<string, string> LoadDotEnvValues(string basePath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(basePath));

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".env");

            if (File.Exists(candidate))
            {
                return ParseDotEnv(candidate);
            }

            current = current.Parent;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extrai pares chave/valor simples de um ficheiro `.env`.
    /// </summary>
    private static Dictionary<string, string> ParseDotEnv(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            values[key] = value;
        }

        return values;
    }

    /// <summary>
    /// Resolve uma chave com prioridade para variáveis de ambiente.
    /// </summary>
    private static string GetValue(string key, IReadOnlyDictionary<string, string> dotEnvValues, string fallback)
    {
        return Environment.GetEnvironmentVariable(key)
            ?? (dotEnvValues.TryGetValue(key, out var fromDotEnv) ? fromDotEnv : fallback);
    }
}
