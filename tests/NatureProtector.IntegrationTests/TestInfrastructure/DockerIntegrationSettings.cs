using NatureProtector.Infrastructure.Postgres.Configuration;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Shared.Configuration;
using System.Text.Json;

namespace NatureProtector.IntegrationTests.TestInfrastructure;

internal static class DockerIntegrationSettings
{
    public static PostgresControlPlaneConnectionSettings CreatePostgresSettings(string database)
    {
        return new PostgresControlPlaneConnectionSettings(
            GetValue("NP_TEST_POSTGRES_HOST", "localhost"),
            GetIntValue("NP_TEST_POSTGRES_PORT", 5433),
            database,
            GetValue("NP_TEST_POSTGRES_USER", "np"),
            GetValue("NP_TEST_POSTGRES_PASSWORD", "np_dev_pass"));
    }

    public static RabbitMqOptions CreateRabbitMqOptions(
        string exchangeName,
        string virtualHost = "/",
        string? ingestionReadingsQueueName = null,
        string? observabilityRawQueueName = null,
        bool observabilityRawEnabled = false)
    {
        return new RabbitMqOptions
        {
            HostName = GetValue("NP_TEST_RABBITMQ_HOST", "localhost"),
            Port = GetIntValue("NP_TEST_RABBITMQ_PORT", 5672),
            UserName = GetValue("NP_TEST_RABBITMQ_USER", "np"),
            Password = GetValue("NP_TEST_RABBITMQ_PASSWORD", "np_dev_pass"),
            VirtualHost = virtualHost,
            ExchangeName = exchangeName,
            IngestionReadingsQueueName = ingestionReadingsQueueName ?? $"np.it.ingestion.{Guid.NewGuid():N}",
            ObservabilityRawEnabled = observabilityRawEnabled,
            ObservabilityRawQueueName = observabilityRawQueueName ?? $"np.it.raw.{Guid.NewGuid():N}"
        };
    }

    public static InfluxDbOptions CreateInfluxDbOptions(string? bucket = null)
    {
        return new InfluxDbOptions
        {
            Url = GetValue("NP_TEST_INFLUXDB_URL", "http://localhost:8181"),
            Token = GetValue("NP_TEST_INFLUXDB_TOKEN", LoadLocalInfluxToken()),
            Organization = GetValue("NP_TEST_INFLUXDB_ORGANIZATION", "natureprotector"),
            Bucket = bucket ?? GetValue("NP_TEST_INFLUXDB_BUCKET", "np_telemetry")
        };
    }

    private static string GetValue(string name, string fallback)
    {
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : fallback;
    }

    private static int GetIntValue(string name, int fallback)
    {
        return int.TryParse(GetValue(name, fallback.ToString()), out var value)
            ? value
            : fallback;
    }

    private static string LoadLocalInfluxToken()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tokenPath = Path.Combine(repositoryRoot, "data", "runtime", "influx", "admin-token.json");

        if (!File.Exists(tokenPath))
        {
            return "local-test-token";
        }

        using var document = JsonDocument.Parse(File.ReadAllText(tokenPath));
        return document.RootElement.TryGetProperty("token", out var token) &&
               token.GetString() is { Length: > 0 } value
            ? value
            : "local-test-token";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NatureProtector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate NatureProtector.sln from the test output directory.");
    }
}
