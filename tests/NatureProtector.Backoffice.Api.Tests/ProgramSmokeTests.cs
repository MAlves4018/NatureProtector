using Microsoft.AspNetCore.Mvc.Testing;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ProgramSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProgramSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
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
