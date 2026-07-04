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

    [Fact]
    public void Host_ExposesKubernetesHealthEndpointsBackedByRuntimeReadiness()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(
            Path.Combine(root, "src", "NatureProtector.Prevention.Host", "Program.cs"));
        var project = File.ReadAllText(
            Path.Combine(root, "src", "NatureProtector.Prevention.Host", "NatureProtector.Prevention.Host.csproj"));

        Assert.Contains("WebApplication.CreateBuilder(args)", program);
        Assert.Contains("AddSingleton<PreventionRuntimeState>()", program);
        Assert.Contains("AddCheck<PreventionReadinessHealthCheck>(\"prevention-ready\")", program);
        Assert.Contains("MapHealthChecks(\"/health/live\"", program);
        Assert.Contains("Predicate = _ => false", program);
        Assert.Contains("MapHealthChecks(\"/health/ready\")", program);
        Assert.Contains("Microsoft.AspNetCore.App", project);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
