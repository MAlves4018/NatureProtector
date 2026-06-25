using NatureProtector.Postgres.Migrations;

namespace NatureProtector.IntegrationTests.Configuration;

[Collection("EnvironmentVariables")]
public sealed class PostgresMigrationSettingsTests : IDisposable
{
    private static readonly string[] Keys =
    [
        "POSTGRES_HOST",
        "POSTGRES_PORT",
        "POSTGRES_DB",
        "POSTGRES_MIGRATION_USER",
        "POSTGRES_MIGRATION_PASSWORD",
        "POSTGRES_APP_USER",
        "POSTGRES_APP_PASSWORD",
        "POSTGRES_SSL_MODE",
        "POSTGRES_ROOT_CERTIFICATE"
    ];

    private readonly Dictionary<string, string?> _previous = Keys.ToDictionary(
        key => key,
        Environment.GetEnvironmentVariable);

    [Fact]
    public void LoadFromEnvironment_RequiresAllAdministrativeAndApplicationCredentials()
    {
        Clear();
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "10.20.0.2");

        var exception = Assert.Throws<InvalidOperationException>(MigrationSettings.LoadFromEnvironment);

        Assert.Contains("POSTGRES_DB", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("10.20.0.2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromEnvironment_ReturnsValidatedSettingsWithoutLoggingSecrets()
    {
        SetValid();

        var settings = MigrationSettings.LoadFromEnvironment();
        var connectionString = settings.BuildAdminConnectionString();

        Assert.Equal("10.20.0.2", settings.Host);
        Assert.Equal(5432, settings.Port);
        Assert.Equal("np_migration", settings.AdminUsername);
        Assert.Equal("np_app", settings.AppUsername);
        Assert.Equal("VerifyCA", settings.SslModeName);
        Assert.Equal("/var/run/secrets/cloudsql/server-ca.pem", settings.RootCertificate);
        Assert.Contains("Include Error Detail=False", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SSL Mode=VerifyCA", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Root Certificate=/var/run/secrets/cloudsql/server-ca.pem", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("np-app")]
    [InlineData("9admin")]
    [InlineData("")]
    public void LoadFromEnvironment_RejectsUnsafeRoleNames(string roleName)
    {
        SetValid();
        Environment.SetEnvironmentVariable("POSTGRES_APP_USER", roleName);

        Assert.Throws<InvalidOperationException>(MigrationSettings.LoadFromEnvironment);
    }


    [Fact]
    public void LoadFromEnvironment_RejectsUnknownSslMode()
    {
        SetValid();
        Environment.SetEnvironmentVariable("POSTGRES_SSL_MODE", "NotARealMode");

        var exception = Assert.Throws<InvalidOperationException>(MigrationSettings.LoadFromEnvironment);

        Assert.Contains("POSTGRES_SSL_MODE", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        foreach (var pair in _previous)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private static void Clear()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private static void SetValid()
    {
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "10.20.0.2");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "5432");
        Environment.SetEnvironmentVariable("POSTGRES_DB", "natureprotector");
        Environment.SetEnvironmentVariable("POSTGRES_MIGRATION_USER", "np_migration");
        Environment.SetEnvironmentVariable("POSTGRES_MIGRATION_PASSWORD", "migration-secret");
        Environment.SetEnvironmentVariable("POSTGRES_APP_USER", "np_app");
        Environment.SetEnvironmentVariable("POSTGRES_APP_PASSWORD", "application-secret");
        Environment.SetEnvironmentVariable("POSTGRES_SSL_MODE", "VerifyCA");
        Environment.SetEnvironmentVariable("POSTGRES_ROOT_CERTIFICATE", "/var/run/secrets/cloudsql/server-ca.pem");
    }
}
