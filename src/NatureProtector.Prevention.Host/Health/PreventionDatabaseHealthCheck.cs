using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Prevention.Host.Health;

/// <summary>
/// Verifica a dependência PostgreSQL exigida pelo pipeline persistente da Prevention.
/// </summary>
public sealed class PreventionDatabaseHealthCheck(
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
                ? HealthCheckResult.Healthy("PostgreSQL prevention persistence is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL prevention persistence did not accept a connection.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL prevention readiness check failed.",
                exception);
        }
    }
}
