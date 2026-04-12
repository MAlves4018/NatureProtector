using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Prevention.Host.Tests.TestInfrastructure;

internal sealed class SqliteControlDbContextScope : IAsyncDisposable
{
    private readonly DbContextOptions<NatureProtectorControlDbContext> _options;
    private readonly SqliteConnection _connection;

    public SqliteControlDbContextScope()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<NatureProtectorControlDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var dbContext = new NatureProtectorControlDbContext(_options);
        dbContext.Database.EnsureCreated();

        Factory = new TestDbContextFactory(_options);
    }

    public IDbContextFactory<NatureProtectorControlDbContext> Factory { get; }

    public NatureProtectorControlDbContext CreateDbContext() => new(_options);

    public async Task SeedAsync(Func<NatureProtectorControlDbContext, Task> seed)
    {
        await using var dbContext = CreateDbContext();
        await seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private sealed class TestDbContextFactory(DbContextOptions<NatureProtectorControlDbContext> options)
        : IDbContextFactory<NatureProtectorControlDbContext>
    {
        public NatureProtectorControlDbContext CreateDbContext() => new(options);

        public Task<NatureProtectorControlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
