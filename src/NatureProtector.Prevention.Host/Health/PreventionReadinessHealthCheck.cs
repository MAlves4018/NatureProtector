using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Prevention.Host.Runtime;

namespace NatureProtector.Prevention.Host.Health;

public sealed class PreventionReadinessHealthCheck(
    PreventionRuntimeState runtimeState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = runtimeState.Snapshot;
        var data = new Dictionary<string, object>
        {
            ["updatedAtUtc"] = snapshot.UpdatedAtUtc,
            ["reason"] = snapshot.Reason
        };

        return Task.FromResult(snapshot.Ready
            ? HealthCheckResult.Healthy(snapshot.Reason, data)
            : HealthCheckResult.Unhealthy(snapshot.Reason, data: data));
    }
}
