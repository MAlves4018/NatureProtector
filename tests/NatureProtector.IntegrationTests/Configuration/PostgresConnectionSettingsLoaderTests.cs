using NatureProtector.Infrastructure.Postgres.Configuration;

namespace NatureProtector.IntegrationTests.Configuration;

[Collection("EnvironmentVariables")]
public sealed class PostgresConnectionSettingsLoaderTests : IDisposable
{
    private static readonly string[] Keys =
    [
        "POSTGRES_REQUIRE_EXPLICIT",
        "POSTGRES_HOST",
        "POSTGRES_PORT",
        "POSTGRES_DB",
        "POSTGRES_USER",
        "POSTGRES_PASSWORD",
        "POSTGRES_SSL_MODE",
        "POSTGRES_ROOT_CERTIFICATE"
    ];

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"natureprotector-postgres-config-{Guid.NewGuid():N}");

    public PostgresConnectionSettingsLoaderTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        ClearEnvironment();
    }

    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WhenStrictAndValuesMissing_ThrowsWithoutSecretValues()
    {
        Environment.SetEnvironmentVariable("POSTGRES_REQUIRE_EXPLICIT", "true");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(_temporaryDirectory));

        Assert.Contains("POSTGRES_HOST", exception.Message, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("np_dev_pass", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WhenStrictAndExplicit_ReturnsConfiguredValues()
    {
        Environment.SetEnvironmentVariable("POSTGRES_REQUIRE_EXPLICIT", "true");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "10.20.0.10");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "5432");
        Environment.SetEnvironmentVariable("POSTGRES_DB", "natureprotector");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "np_runtime");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "secret-from-runtime");
        Environment.SetEnvironmentVariable("POSTGRES_SSL_MODE", "VerifyCA");
        Environment.SetEnvironmentVariable("POSTGRES_ROOT_CERTIFICATE", "/var/run/secrets/cloudsql/server-ca.pem");

        var settings = PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(_temporaryDirectory);

        Assert.Equal("10.20.0.10", settings.Host);
        Assert.Equal(5432, settings.Port);
        Assert.Equal("natureprotector", settings.Database);
        Assert.Equal("np_runtime", settings.Username);
        Assert.Equal("secret-from-runtime", settings.Password);
        Assert.Equal("VerifyCA", settings.SslModeName);
        Assert.Equal("/var/run/secrets/cloudsql/server-ca.pem", settings.RootCertificate);
        Assert.Contains("SSL Mode=VerifyCA", settings.BuildConnectionString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Root Certificate=/var/run/secrets/cloudsql/server-ca.pem", settings.BuildConnectionString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WhenStrictPortInvalid_Throws()
    {
        Environment.SetEnvironmentVariable("POSTGRES_REQUIRE_EXPLICIT", "true");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "postgres");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "70000");
        Environment.SetEnvironmentVariable("POSTGRES_DB", "natureprotector");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "np_runtime");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "secret-from-runtime");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(_temporaryDirectory));

        Assert.Contains("between 1 and 65535", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        ClearEnvironment();
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static void ClearEnvironment()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}

[CollectionDefinition("EnvironmentVariables", DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection;
