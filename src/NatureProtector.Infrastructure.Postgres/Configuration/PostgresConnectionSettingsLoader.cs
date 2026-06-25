using System.Text;

namespace NatureProtector.Infrastructure.Postgres.Configuration;

/*
 * Este helper resolve as definições de ligação PostgreSQL a partir do ambiente
 * do processo e do ficheiro `.env` do repositório.
 *
 * Rationale:
 * - Os hosts e scripts precisam de uma forma consistente de descobrir a ligação
 *   ao control plane sem duplicar parsing de configuração.
 * - Permitir fallback para `.env` simplifica a baseline local.
 */

public static class PostgresConnectionSettingsLoader
{
    /// <summary>
    /// Carrega as definições de ligação dando prioridade ao ambiente e usando o
    /// `.env` do repositório como fallback.
    /// </summary>
    public static PostgresControlPlaneConnectionSettings LoadFromEnvironmentOrDotEnv(string basePath)
    {
        var dotEnvValues = LoadDotEnvValues(basePath);
        var requireExplicit = GetBooleanValue("POSTGRES_REQUIRE_EXPLICIT", dotEnvValues);
        ThrowIfRequiredValuesAreMissing(
            requireExplicit,
            dotEnvValues,
            "POSTGRES_HOST",
            "POSTGRES_PORT",
            "POSTGRES_DB",
            "POSTGRES_USER",
            "POSTGRES_PASSWORD");

        return new PostgresControlPlaneConnectionSettings(
            GetValue("POSTGRES_HOST", dotEnvValues, "localhost", requireExplicit),
            GetPortValue("POSTGRES_PORT", dotEnvValues, 5432, requireExplicit),
            GetValue("POSTGRES_DB", dotEnvValues, "natureprotector", requireExplicit),
            GetValue("POSTGRES_USER", dotEnvValues, "np", requireExplicit),
            GetValue("POSTGRES_PASSWORD", dotEnvValues, "np_dev_pass", requireExplicit),
            GetOptionalValue("POSTGRES_SSL_MODE", dotEnvValues),
            GetOptionalValue("POSTGRES_ROOT_CERTIFICATE", dotEnvValues));
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
    private static string GetValue(
        string key,
        IReadOnlyDictionary<string, string> dotEnvValues,
        string fallback,
        bool required)
    {
        var value = GetOptionalValue(key, dotEnvValues);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (required)
        {
            throw new InvalidOperationException($"PostgreSQL configuration key '{key}' is required when POSTGRES_REQUIRE_EXPLICIT=true.");
        }

        return fallback;
    }

    /// <summary>
    /// Resolve um inteiro com fallback seguro quando a configuração é inválida.
    /// </summary>
    private static int GetPortValue(
        string key,
        IReadOnlyDictionary<string, string> dotEnvValues,
        int fallback,
        bool required)
    {
        var raw = GetValue(key, dotEnvValues, fallback.ToString(), required);
        if (int.TryParse(raw, out var parsed) && parsed is >= 1 and <= 65535)
        {
            return parsed;
        }

        throw new InvalidOperationException($"PostgreSQL configuration key '{key}' must be an integer between 1 and 65535.");
    }

    private static string? GetOptionalValue(string key, IReadOnlyDictionary<string, string> dotEnvValues)
    {
        var value = Environment.GetEnvironmentVariable(key)
            ?? (dotEnvValues.TryGetValue(key, out var fromDotEnv) ? fromDotEnv : null);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void ThrowIfRequiredValuesAreMissing(
        bool required,
        IReadOnlyDictionary<string, string> dotEnvValues,
        params string[] keys)
    {
        if (!required)
        {
            return;
        }

        var missing = keys
            .Where(key => string.IsNullOrWhiteSpace(GetOptionalValue(key, dotEnvValues)))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "PostgreSQL explicit configuration is missing required keys: " +
                string.Join(", ", missing) +
                ".");
        }
    }

    private static bool GetBooleanValue(string key, IReadOnlyDictionary<string, string> dotEnvValues)
    {
        var value = GetOptionalValue(key, dotEnvValues);
        return bool.TryParse(value, out var parsed) && parsed;
    }
}
