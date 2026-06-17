using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NatureProtector.Infrastructure.Postgres.Persistence;
using Npgsql;

namespace NatureProtector.IntegrationTests.TestInfrastructure;

internal sealed class TemporaryPostgresDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseConnectionString;

    private TemporaryPostgresDatabase(
        string databaseName,
        string adminConnectionString,
        string databaseConnectionString)
    {
        DatabaseName = databaseName;
        _adminConnectionString = adminConnectionString;
        _databaseConnectionString = databaseConnectionString;
    }

    public string DatabaseName { get; }

    public static async Task<TemporaryPostgresDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        return await CreateAsync(
            static async (database, setupCancellationToken) =>
            {
                await using var dbContext = database.CreateDbContext();
                await dbContext.Database.MigrateAsync(setupCancellationToken);
            },
            cancellationToken);
    }

    internal static async Task<TemporaryPostgresDatabase> CreateAsync(
        Func<TemporaryPostgresDatabase, CancellationToken, Task> setupAsync,
        CancellationToken cancellationToken = default)
    {
        var databaseName = $"np_it_{Guid.NewGuid():N}";
        var adminConnectionString = DockerIntegrationSettings
            .CreatePostgresSettings("postgres")
            .BuildConnectionString();
        var databaseConnectionString = DockerIntegrationSettings
            .CreatePostgresSettings(databaseName)
            .BuildConnectionString();
        TemporaryPostgresDatabase? database = null;

        try
        {
            await using (var connection = new NpgsqlConnection(adminConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var createCommand = connection.CreateCommand();
                createCommand.CommandText = $"""CREATE DATABASE "{databaseName}";""";
                await createCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            database = new TemporaryPostgresDatabase(
                databaseName,
                adminConnectionString,
                databaseConnectionString);

            await setupAsync(database, cancellationToken);

            return database;
        }
        catch
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }
            else
            {
                await DropDatabaseIfExistsAsync(adminConnectionString, databaseName);
            }

            throw;
        }
    }

    public NatureProtectorControlDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NatureProtectorControlDbContext>()
            .UseNpgsql(_databaseConnectionString)
            .Options;

        return new NatureProtectorControlDbContext(options);
    }

    public IDbContextFactory<NatureProtectorControlDbContext> CreateFactory()
    {
        return new TestDbContextFactory(_databaseConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await DropDatabaseIfExistsAsync(_adminConnectionString, DatabaseName);
    }

    public static async Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        var adminConnectionString = DockerIntegrationSettings
            .CreatePostgresSettings("postgres")
            .BuildConnectionString();

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName);";
        existsCommand.Parameters.AddWithValue("databaseName", databaseName);
        return (bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task DropDatabaseIfExistsAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}" WITH (FORCE);""";
        await dropCommand.ExecuteNonQueryAsync();
    }

    private sealed class TestDbContextFactory(string connectionString)
        : IDbContextFactory<NatureProtectorControlDbContext>
    {
        public NatureProtectorControlDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<NatureProtectorControlDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new NatureProtectorControlDbContext(options);
        }

        public Task<NatureProtectorControlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
