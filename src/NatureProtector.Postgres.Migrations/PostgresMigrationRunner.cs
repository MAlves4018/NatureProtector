using Microsoft.EntityFrameworkCore;
using Npgsql;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Postgres.Migrations;

public sealed class PostgresMigrationRunner(MigrationSettings settings)
{
    private const long MigrationAdvisoryLockKey = 580_441_091_337_001L;

    public async Task<MigrationRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        await using var dataSource = settings.BuildAdminDataSource();
        await using var lockConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        await AcquireLockAsync(lockConnection, cancellationToken);

        try
        {
            var options = new DbContextOptionsBuilder<NatureProtectorControlDbContext>()
                .UseNpgsql(dataSource)
                .Options;

            await using var dbContext = new NatureProtectorControlDbContext(options);
            var pendingBefore = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            await dbContext.Database.MigrateAsync(cancellationToken);
            await ProvisionLeastPrivilegeAppRoleAsync(lockConnection, cancellationToken);
            var pendingAfter = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            if (pendingAfter.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Migration verification failed. Pending: {string.Join(", ", pendingAfter)}.");
            }

            var applied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            return new MigrationRunSummary(pendingBefore, applied, settings.AppUsername);
        }
        finally
        {
            await ReleaseLockAsync(lockConnection, CancellationToken.None);
        }
    }

    private static async Task AcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@lock_key);", connection);
        command.Parameters.AddWithValue("lock_key", MigrationAdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@lock_key);", connection);
        command.Parameters.AddWithValue("lock_key", MigrationAdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ProvisionLeastPrivilegeAppRoleAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        using var commandBuilder = new NpgsqlCommandBuilder();
        var quotedRole = commandBuilder.QuoteIdentifier(settings.AppUsername);
        var quotedDatabase = commandBuilder.QuoteIdentifier(settings.Database);

        if (!await RoleExistsAsync(connection, settings.AppUsername, cancellationToken))
        {
            var passwordLiteral = await FormatPasswordLiteralAsync(
                connection,
                settings.AppPassword,
                cancellationToken);

            await ExecuteAsync(connection, $"""
                CREATE ROLE {quotedRole}
                    LOGIN
                    NOSUPERUSER
                    NOCREATEDB
                    NOCREATEROLE
                    NOREPLICATION
                    PASSWORD {passwordLiteral};
                """, cancellationToken);
        }

        await ExecuteAsync(connection, $"""
            GRANT CONNECT ON DATABASE {quotedDatabase} TO {quotedRole};
            GRANT USAGE ON SCHEMA public, control, pipeline, projection, user_base TO {quotedRole};
            GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO {quotedRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA control, pipeline, projection, user_base TO {quotedRole};
            GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA control, pipeline, projection, user_base TO {quotedRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA control, pipeline, projection, user_base
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA control, pipeline, projection, user_base
                GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO {quotedRole};
            """, cancellationToken);
    }

    private static async Task<bool> RoleExistsAsync(
        NpgsqlConnection connection,
        string roleName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @role_name);",
            connection);
        command.Parameters.AddWithValue("role_name", roleName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("PostgreSQL did not return role existence."));
    }

    private static async Task<string> FormatPasswordLiteralAsync(
        NpgsqlConnection connection,
        string password,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT quote_literal(@password);", connection);
        command.Parameters.AddWithValue("password", password);
        return (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("PostgreSQL did not return a password literal.");
    }

    private static string ToSqlLiteral(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record MigrationRunSummary(
    IReadOnlyList<string> AppliedDuringRun,
    IReadOnlyList<string> AppliedMigrations,
    string ApplicationRole);
