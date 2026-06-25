using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Prevention.Host.Health;
using NatureProtector.Prevention.Host.Runtime;

namespace NatureProtector.Prevention.Host.Tests;

public sealed class PreventionRuntimeStateTests
{
    [Fact]
    public async Task Readiness_IsUnhealthyUntilConsumerIsMarkedReady()
    {
        var state = new PreventionRuntimeState();
        var healthCheck = new PreventionReadinessHealthCheck(state);

        var before = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);
        state.MarkReady("consumer active");
        var after = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, before.Status);
        Assert.Equal(HealthStatus.Healthy, after.Status);
        Assert.Equal("consumer active", after.Description);
    }

    [Fact]
    public async Task Readiness_ReturnsToUnhealthyWhenConsumerStops()
    {
        var state = new PreventionRuntimeState();
        state.MarkReady("consumer active");
        state.MarkNotReady("consumer stopped");
        var healthCheck = new PreventionReadinessHealthCheck(state);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("consumer stopped", result.Description);
    }
}
