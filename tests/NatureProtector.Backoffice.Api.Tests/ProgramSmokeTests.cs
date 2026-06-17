using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ProgramSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProgramSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BackofficeApi:ControlPlaneEnabled", "false");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BackofficeApi:ControlPlaneEnabled"] = "false"
                });
            });
        });
    }

    [Fact]
    public async Task OpenApiEndpoint_IsAvailableInDevelopment()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"openapi\"", content, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(content);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/control/runtime/summary", out _));
        Assert.True(paths.TryGetProperty("/api/control/runtime/runs", out _));
        Assert.True(paths.TryGetProperty("/api/control/runtime/observability/health", out _));
        Assert.True(paths.TryGetProperty("/api/control/runtime/observability/evidence/{evidenceId}", out _));
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/does-not-exist");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
