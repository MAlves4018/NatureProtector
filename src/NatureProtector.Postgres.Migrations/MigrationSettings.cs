using Npgsql;

namespace NatureProtector.Postgres.Migrations;

public sealed record MigrationSettings(
    string Host,
    int Port,
    string Database,
    string AdminUsername,
    string AdminPassword,
    string AppUsername,
    string AppPassword,
    string SslModeName,
    string? RootCertificate)
{
    public static MigrationSettings LoadFromEnvironment()
    {
        var host = Required("POSTGRES_HOST");
        var database = Required("POSTGRES_DB");
        var adminUsername = Required("POSTGRES_MIGRATION_USER");
        var adminPassword = Required("POSTGRES_MIGRATION_PASSWORD");
        var appUsername = Required("POSTGRES_APP_USER");
        var appPassword = Required("POSTGRES_APP_PASSWORD");
        var portRaw = Required("POSTGRES_PORT");
        var sslModeName = Optional("POSTGRES_SSL_MODE", "Prefer");
        var rootCertificate = Environment.GetEnvironmentVariable("POSTGRES_ROOT_CERTIFICATE");

        if (!int.TryParse(portRaw, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("POSTGRES_PORT must be an integer between 1 and 65535.");
        }

        if (!Enum.TryParse<SslMode>(sslModeName, ignoreCase: true, out _))
        {
            throw new InvalidOperationException(
                $"POSTGRES_SSL_MODE '{sslModeName}' is not a valid Npgsql SSL mode.");
        }

        ValidateRoleName(adminUsername, "POSTGRES_MIGRATION_USER");
        ValidateRoleName(appUsername, "POSTGRES_APP_USER");

        return new MigrationSettings(
            host,
            port,
            database,
            adminUsername,
            adminPassword,
            appUsername,
            appPassword,
            sslModeName,
            rootCertificate);
    }

    public string BuildAdminConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = AdminUsername,
            Password = AdminPassword,
            IncludeErrorDetail = false,
            ApplicationName = "NatureProtector.Postgres.Migrations",
            Timeout = 15,
            CommandTimeout = 120,
            SslMode = Enum.Parse<SslMode>(SslModeName, ignoreCase: true)
        };

        if (!string.IsNullOrWhiteSpace(RootCertificate))
        {
            builder.RootCertificate = RootCertificate;
        }

        return builder.ConnectionString;
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{name} is required by the migration job.")
            : value;
    }

    private static string Optional(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void ValidateRoleName(string value, string settingName)
    {
        if (value.Length > 63 ||
            (!char.IsLower(value[0]) && value[0] != '_') ||
            value.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character == '_')))
        {
            throw new InvalidOperationException(
                $"{settingName} must be a lowercase PostgreSQL identifier using only a-z, 0-9 and underscore.");
        }
    }
}
