using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Backoffice.Api.Health;

public sealed class ControlPlaneDatabaseHealthCheck(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL control plane is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL control plane did not accept a connection.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL control plane readiness check failed.",
                exception);
        }
    }
}
