using NatureProtector.Infrastructure.Influx.Configuration;

namespace NatureProtector.Infrastructure.Influx.Tests.Configuration;

[Collection("InfluxEnvironmentVariables")]
public sealed class InfluxDbSettingsLoaderTests
{
    private static readonly string[] InfluxEnvironmentKeys =
    [
        "INFLUXDB_URL",
        "INFLUXDB_PORT",
        "INFLUXDB_TOKEN",
        "INFLUXDB_ORGANIZATION",
        "INFLUXDB_BUCKET",
        "INFLUXDB_DATABASE"
    ];

    [Fact]
    public void ApplyEnvironmentOrDotEnvFallbacks_DotEnvExplicitUrl_UsesExplicitUrlAndTrimsValues()
    {
        var previousEnvironment = ClearInfluxEnvironment();
        var basePath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(basePath, ".env"),
                """
                # local test settings
                INFLUXDB_URL = "http://influx.local:9999"
                INFLUXDB_TOKEN = test-token
                INFLUXDB_ORGANIZATION = test-org
                INFLUXDB_BUCKET = test-bucket
                malformed-line-without-separator
                =missing-key
                """);
            var options = new InfluxDbOptions();

            InfluxDbSettingsLoader.ApplyEnvironmentOrDotEnvFallbacks(options, basePath);

            Assert.Equal("http://influx.local:9999", options.Url);
            Assert.Equal("test-token", options.Token);
            Assert.Equal("test-org", options.Organization);
            Assert.Equal("test-bucket", options.Bucket);
        }
        finally
        {
            RestoreInfluxEnvironment(previousEnvironment);
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void ApplyEnvironmentOrDotEnvFallbacks_EmptyDotEnv_UsesPortDefaultAndEmptySecrets()
    {
        var previousEnvironment = ClearInfluxEnvironment();
        var basePath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(basePath, ".env"),
                """

                # comments and whitespace only
                   
                """);
            var options = new InfluxDbOptions();

            InfluxDbSettingsLoader.ApplyEnvironmentOrDotEnvFallbacks(options, basePath);

            Assert.Equal("http://localhost:8181", options.Url);
            Assert.Equal(string.Empty, options.Token);
            Assert.Equal(string.Empty, options.Organization);
            Assert.Equal(string.Empty, options.Bucket);
        }
        finally
        {
            RestoreInfluxEnvironment(previousEnvironment);
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void ApplyEnvironmentOrDotEnvFallbacks_EnvironmentVariablesOverrideDotEnvValues()
    {
        var previousEnvironment = ClearInfluxEnvironment();
        var basePath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(basePath, ".env"),
                """
                INFLUXDB_URL=http://from-dotenv:8086
                INFLUXDB_TOKEN=dotenv-token
                INFLUXDB_ORGANIZATION=dotenv-org
                INFLUXDB_BUCKET=dotenv-bucket
                """);
            Environment.SetEnvironmentVariable("INFLUXDB_URL", "http://from-env:8086");
            Environment.SetEnvironmentVariable("INFLUXDB_TOKEN", "env-token");
            Environment.SetEnvironmentVariable("INFLUXDB_ORGANIZATION", "env-org");
            Environment.SetEnvironmentVariable("INFLUXDB_BUCKET", "env-bucket");
            var options = new InfluxDbOptions();

            InfluxDbSettingsLoader.ApplyEnvironmentOrDotEnvFallbacks(options, basePath);

            Assert.Equal("http://from-env:8086", options.Url);
            Assert.Equal("env-token", options.Token);
            Assert.Equal("env-org", options.Organization);
            Assert.Equal("env-bucket", options.Bucket);
        }
        finally
        {
            RestoreInfluxEnvironment(previousEnvironment);
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void ApplyEnvironmentOrDotEnvFallbacks_DatabaseFallback_UsesDatabaseWhenBucketIsMissing()
    {
        var previousEnvironment = ClearInfluxEnvironment();
        var basePath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(basePath, ".env"),
                """
                INFLUXDB_PORT=9099
                INFLUXDB_DATABASE=legacy-db
                """);
            var options = new InfluxDbOptions();

            InfluxDbSettingsLoader.ApplyEnvironmentOrDotEnvFallbacks(options, basePath);

            Assert.Equal("http://localhost:9099", options.Url);
            Assert.Equal("legacy-db", options.Bucket);
        }
        finally
        {
            RestoreInfluxEnvironment(previousEnvironment);
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void ApplyEnvironmentOrDotEnvFallbacks_ExistingOptions_PreservesExplicitOptions()
    {
        var previousEnvironment = ClearInfluxEnvironment();
        var basePath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(basePath, ".env"),
                """
                INFLUXDB_URL=http://from-dotenv:8086
                INFLUXDB_TOKEN=dotenv-token
                INFLUXDB_ORGANIZATION=dotenv-org
                INFLUXDB_BUCKET=dotenv-bucket
                """);
            var options = new InfluxDbOptions
            {
                Url = "http://explicit:8086",
                Token = "explicit-token",
                Organization = "explicit-org",
                Bucket = "explicit-bucket"
            };

            InfluxDbSettingsLoader.ApplyEnvironmentOrDotEnvFallbacks(options, basePath);

            Assert.Equal("http://explicit:8086", options.Url);
            Assert.Equal("explicit-token", options.Token);
            Assert.Equal("explicit-org", options.Organization);
            Assert.Equal("explicit-bucket", options.Bucket);
        }
        finally
        {
            RestoreInfluxEnvironment(previousEnvironment);
            Directory.Delete(basePath, recursive: true);
        }
    }

    private static Dictionary<string, string?> ClearInfluxEnvironment()
    {
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var key in InfluxEnvironmentKeys)
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }

        return previous;
    }

    private static void RestoreInfluxEnvironment(IReadOnlyDictionary<string, string?> previous)
    {
        foreach (var (key, value) in previous)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "natureprotector-influx-loader-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }
}
