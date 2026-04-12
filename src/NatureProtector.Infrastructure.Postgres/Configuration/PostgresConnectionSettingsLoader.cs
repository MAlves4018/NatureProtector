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

        return new PostgresControlPlaneConnectionSettings(
            GetValue("POSTGRES_HOST", dotEnvValues, "localhost"),
            GetIntValue("POSTGRES_PORT", dotEnvValues, 5432),
            GetValue("POSTGRES_DB", dotEnvValues, "natureprotector"),
            GetValue("POSTGRES_USER", dotEnvValues, "np"),
            GetValue("POSTGRES_PASSWORD", dotEnvValues, "np_dev_pass"));
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

    /// <summary>
    /// Resolve um inteiro com fallback seguro quando a configuração é inválida.
    /// </summary>
    private static int GetIntValue(string key, IReadOnlyDictionary<string, string> dotEnvValues, int fallback)
    {
        var raw = GetValue(key, dotEnvValues, fallback.ToString());
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
    }
}
