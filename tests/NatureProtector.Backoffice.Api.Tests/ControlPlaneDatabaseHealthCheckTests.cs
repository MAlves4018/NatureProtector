using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Backoffice.Api.Health;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ControlPlaneDatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenControlPlaneDatabaseAcceptsConnections()
    {
        await using var scope = new SqliteControlDbContextScope();
        var healthCheck = new ControlPlaneDatabaseHealthCheck(scope.Factory);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("PostgreSQL control plane is reachable.", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenContextFactoryFails()
    {
        var failure = new InvalidOperationException("factory unavailable");
        var healthCheck = new ControlPlaneDatabaseHealthCheck(new ThrowingDbContextFactory(failure));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("PostgreSQL control plane readiness check failed.", result.Description);
        Assert.Same(failure, result.Exception);
    }

    private sealed class ThrowingDbContextFactory(Exception exception) : IDbContextFactory<NatureProtectorControlDbContext>
    {
        public NatureProtectorControlDbContext CreateDbContext()
            => throw exception;

        public Task<NatureProtectorControlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromException<NatureProtectorControlDbContext>(exception);
    }
}
