using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Prevention.Host.Health;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;

namespace NatureProtector.Prevention.Host.Tests.Health;

public sealed class PreventionDatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenDatabaseAcceptsConnections()
    {
        await using var database = new SqliteControlDbContextScope();
        var healthCheck = new PreventionDatabaseHealthCheck(database.Factory);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("PostgreSQL prevention persistence is reachable.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenFactoryCannotCreateContext()
    {
        var failure = new InvalidOperationException("simulated postgres outage");
        var healthCheck = new PreventionDatabaseHealthCheck(
            new ThrowingDbContextFactory(failure));

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("PostgreSQL prevention readiness check failed.", result.Description);
        Assert.Same(failure, result.Exception);
    }

    private sealed class ThrowingDbContextFactory(Exception exception)
        : IDbContextFactory<NatureProtectorControlDbContext>
    {
        public NatureProtectorControlDbContext CreateDbContext()
            => throw exception;

        public Task<NatureProtectorControlDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<NatureProtectorControlDbContext>(exception);
        }
    }
}
