using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Prevention.Host.Tests.TestInfrastructure;

internal sealed class SqliteControlDbContextScope : IAsyncDisposable
{
    private readonly DbContextOptions<NatureProtectorControlDbContext> _options;
    private readonly DbContextOptions<NatureProtectorControlDbContext> _plainOptions;
    private readonly SqliteConnection? _connection;
    private readonly string? _databasePath;

    public SqliteControlDbContextScope(
        Action<DbContextOptionsBuilder<NatureProtectorControlDbContext>>? configureOptions = null,
        bool useFileDatabase = false,
        string? databasePath = null)
    {
        if (useFileDatabase)
        {
            _databasePath = databasePath ?? Path.Combine(
                Path.GetTempPath(),
                $"natureprotector-tests-{Guid.NewGuid():N}.sqlite");

            _plainOptions = BuildOptions(
                connectionString: $"Data Source={_databasePath}",
                configureOptions: null,
                connection: null);
            _options = BuildOptions(
                connectionString: $"Data Source={_databasePath}",
                configureOptions: configureOptions,
                connection: null);
        }
        else
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _plainOptions = BuildOptions(
                connectionString: null,
                configureOptions: null,
                connection: _connection);
            _options = BuildOptions(
                connectionString: null,
                configureOptions: configureOptions,
                connection: _connection);
        }

        using var dbContext = new NatureProtectorControlDbContext(_plainOptions);
        dbContext.Database.EnsureCreated();

        Factory = new TestDbContextFactory(_options);
    }

    public IDbContextFactory<NatureProtectorControlDbContext> Factory { get; }
    public DbContextOptions<NatureProtectorControlDbContext> PlainOptions => _plainOptions;

    public NatureProtectorControlDbContext CreateDbContext() => new(_options);
    public NatureProtectorControlDbContext CreatePlainDbContext() => new(_plainOptions);

    public async Task SeedAsync(Func<NatureProtectorControlDbContext, Task> seed)
    {
        await using var dbContext = CreateDbContext();
        await seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_databasePath is not null && File.Exists(_databasePath))
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static DbContextOptions<NatureProtectorControlDbContext> BuildOptions(
        string? connectionString,
        Action<DbContextOptionsBuilder<NatureProtectorControlDbContext>>? configureOptions,
        SqliteConnection? connection)
    {
        var builder = new DbContextOptionsBuilder<NatureProtectorControlDbContext>();

        if (connection is not null)
        {
            builder.UseSqlite(connection);
        }
        else if (connectionString is not null)
        {
            builder.UseSqlite(connectionString);
        }

        configureOptions?.Invoke(builder);

        return builder.Options;
    }

    private sealed class TestDbContextFactory(DbContextOptions<NatureProtectorControlDbContext> options)
        : IDbContextFactory<NatureProtectorControlDbContext>
    {
        public NatureProtectorControlDbContext CreateDbContext() => new(options);

        public Task<NatureProtectorControlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
