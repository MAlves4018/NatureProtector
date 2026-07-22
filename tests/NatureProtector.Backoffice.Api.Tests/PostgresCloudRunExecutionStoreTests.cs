using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;
using NatureProtector.Infrastructure.Postgres.Persistence;
using Npgsql;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class PostgresCloudRunExecutionStoreTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task ReserveAsync_InsertsNewExecutionAndReusesSameIdempotencyKey()
    {
        await using var database = await TemporaryPostgresCloudRunDatabase.CreateAsync();
        var store = new PostgresCloudRunExecutionStore(database.CreateFactory());
        var request = Request("reserve-new");

        var first = await store.ReserveAsync(request, TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = await store.ReserveAsync(Request("reserve-new"), TimeSpan.FromMinutes(5), CancellationToken.None);
        var stored = await store.GetAsync(request.ExecutionId, CancellationToken.None);

        Assert.True(first.OwnsLaunch);
        Assert.False(first.ReusedExistingExecution);
        Assert.Equal(request.ExecutionId, first.Record.ExecutionId);
        Assert.Equal(request.IdempotencyKey, first.Record.IdempotencyKey);
        Assert.Equal(request.Simulation.OrchestratorCorrelationId, first.Record.LogCorrelation);
        Assert.Equal(request.Evidence, first.Record.Evidence);
        Assert.Equal(first.LeaseToken, first.Record.LaunchLeaseToken);
        Assert.NotNull(first.Record.LaunchLeaseUntilUtc);

        Assert.False(second.OwnsLaunch);
        Assert.True(second.ReusedExistingExecution);
        Assert.Equal(request.ExecutionId, second.Record.ExecutionId);
        Assert.Equal(stored, second.Record);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task ReserveAsync_ReclaimsExpiredLaunchLeaseWithoutChangingExecutionIdentity()
    {
        await using var database = await TemporaryPostgresCloudRunDatabase.CreateAsync();
        var store = new PostgresCloudRunExecutionStore(database.CreateFactory());
        var request = Request("expired-lease");
        var first = await store.ReserveAsync(request, TimeSpan.FromMilliseconds(-1), CancellationToken.None);

        await Task.Delay(5);
        var second = await store.ReserveAsync(Request("expired-lease"), TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.True(first.OwnsLaunch);
        Assert.True(second.OwnsLaunch);
        Assert.True(second.ReusedExistingExecution);
        Assert.Equal(request.ExecutionId, second.Record.ExecutionId);
        Assert.NotEqual(first.LeaseToken, second.LeaseToken);
        Assert.Equal(second.LeaseToken, second.Record.LaunchLeaseToken);
        Assert.True(second.Record.LaunchLeaseUntilUtc > first.Record.LaunchLeaseUntilUtc);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task AttachOperationAsync_RequiresLeaseAndClearsLaunchLease()
    {
        await using var database = await TemporaryPostgresCloudRunDatabase.CreateAsync();
        var store = new PostgresCloudRunExecutionStore(database.CreateFactory());
        var request = Request("attach");
        var reservation = await store.ReserveAsync(request, TimeSpan.FromMinutes(5), CancellationToken.None);

        var rejected = await store.AttachOperationAsync(
            request.ExecutionId,
            Guid.NewGuid(),
            "projects/p/locations/europe-southwest1/operations/rejected",
            CancellationToken.None);
        var accepted = await store.AttachOperationAsync(
            request.ExecutionId,
            reservation.LeaseToken,
            "projects/p/locations/europe-southwest1/operations/accepted",
            CancellationToken.None);
        var stored = await store.GetAsync(request.ExecutionId, CancellationToken.None);

        Assert.False(rejected);
        Assert.True(accepted);
        Assert.Equal(RuntimeExecutionState.Running, stored!.State);
        Assert.Equal("projects/p/locations/europe-southwest1/operations/accepted", stored.ProviderOperationName);
        Assert.NotNull(stored.StartedAtUtc);
        Assert.Null(stored.LaunchLeaseToken);
        Assert.Null(stored.LaunchLeaseUntilUtc);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task UpdateAsync_PersistsTerminalSnapshotAndFinishedAt()
    {
        await using var database = await TemporaryPostgresCloudRunDatabase.CreateAsync();
        var store = new PostgresCloudRunExecutionStore(database.CreateFactory());
        var request = Request("update-terminal");
        var reservation = await store.ReserveAsync(request, TimeSpan.FromMinutes(5), CancellationToken.None);
        var startedAt = DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture);
        var finishedAt = DateTimeOffset.Parse("2026-07-21T12:01:30Z", CultureInfo.InvariantCulture);

        await store.UpdateAsync(reservation.Record with
        {
            ProviderExecutionName = "projects/p/locations/europe-southwest1/jobs/simulator/executions/ex-1",
            State = RuntimeExecutionState.Failed,
            UpdatedAtUtc = finishedAt,
            StartedAtUtc = startedAt,
            FinishedAtUtc = finishedAt,
            FailureCode = "13",
            FailureMessage = "provider failed"
        }, CancellationToken.None);

        var stored = await store.GetAsync(request.ExecutionId, CancellationToken.None);

        Assert.Equal(RuntimeExecutionState.Failed, stored!.State);
        Assert.Equal("projects/p/locations/europe-southwest1/jobs/simulator/executions/ex-1", stored.ProviderExecutionName);
        Assert.Equal(startedAt, stored.StartedAtUtc);
        Assert.Equal(finishedAt, stored.FinishedAtUtc);
        Assert.Equal("13", stored.FailureCode);
        Assert.Equal("provider failed", stored.FailureMessage);
        Assert.Null(stored.LaunchLeaseToken);
        Assert.Null(stored.LaunchLeaseUntilUtc);
    }

    private static RuntimeLaunchRequest Request(string idempotencyKey) => new(
        new RuntimeExecutionId(Guid.NewGuid()),
        Guid.NewGuid(),
        idempotencyKey,
        "local",
        RuntimeLaunchProfile.Simulation,
        new RuntimeSimulationParameters("PT-11", "scenario_a", 10, 3, 1, 123, null, ["none"], $"corr-{idempotencyKey}"),
        null,
        CollectEvidence: true,
        WaitForCompletion: false,
        TimeSpan.FromSeconds(30),
        new RuntimeEvidenceReference($"evidence-{idempotencyKey}", $"location-{idempotencyKey}"));

    private sealed class TemporaryPostgresCloudRunDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseConnectionString;

        private TemporaryPostgresCloudRunDatabase(
            string databaseName,
            string adminConnectionString,
            string databaseConnectionString)
        {
            DatabaseName = databaseName;
            _adminConnectionString = adminConnectionString;
            _databaseConnectionString = databaseConnectionString;
        }

        public string DatabaseName { get; }

        public static async Task<TemporaryPostgresCloudRunDatabase> CreateAsync(
            CancellationToken cancellationToken = default)
        {
            var databaseName = $"np_cloudrun_store_{Guid.NewGuid():N}";
            var adminConnectionString = ConnectionString("postgres");
            var databaseConnectionString = ConnectionString(databaseName);
            TemporaryPostgresCloudRunDatabase? database = null;

            try
            {
                await using (var connection = new NpgsqlConnection(adminConnectionString))
                {
                    await connection.OpenAsync(cancellationToken);
                    await using var createCommand = connection.CreateCommand();
                    createCommand.CommandText = $"""CREATE DATABASE "{databaseName}";""";
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                database = new TemporaryPostgresCloudRunDatabase(
                    databaseName,
                    adminConnectionString,
                    databaseConnectionString);
                await using var context = database.CreateDbContext();
                await context.Database.MigrateAsync(cancellationToken);
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

        public IDbContextFactory<NatureProtectorControlDbContext> CreateFactory()
            => new TestDbContextFactory(_databaseConnectionString);

        private NatureProtectorControlDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<NatureProtectorControlDbContext>()
                .UseNpgsql(_databaseConnectionString)
                .Options;
            return new NatureProtectorControlDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await DropDatabaseIfExistsAsync(_adminConnectionString, DatabaseName);
            NpgsqlConnection.ClearAllPools();
        }

        private static string ConnectionString(string database)
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = Environment.GetEnvironmentVariable("NP_TEST_POSTGRES_HOST") ?? "localhost",
                Port = int.TryParse(Environment.GetEnvironmentVariable("NP_TEST_POSTGRES_PORT"), out var port) ? port : 5433,
                Database = database,
                Username = Environment.GetEnvironmentVariable("NP_TEST_POSTGRES_USER") ?? "np",
                Password = Environment.GetEnvironmentVariable("NP_TEST_POSTGRES_PASSWORD") ?? "np_dev_pass",
                IncludeErrorDetail = true
            };
            return builder.ConnectionString;
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
                => Task.FromResult(CreateDbContext());
        }
    }
}
