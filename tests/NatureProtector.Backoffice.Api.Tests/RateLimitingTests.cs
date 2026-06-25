using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NatureProtector.Backoffice.Api.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task AnonymousRead_RejectsRequestsAfterConfiguredLimit()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:AnonymousRead:PermitLimit"] = "2",
            ["RateLimiting:AnonymousRead:WindowSeconds"] = "60"
        });
        using var client = factory.CreateClient();

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/control/areas")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/control/areas")).StatusCode);

        using var rejected = await client.GetAsync("/api/control/areas");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("anonymous-read", rejected.Headers.GetValues("X-RateLimit-Policy").Single());
        Assert.NotNull(rejected.Headers.RetryAfter);

        using var document = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("anonymous-read", document.RootElement.GetProperty("policy").GetString());
    }


    [Fact]
    public async Task NormalizedForwardedFor_PartitionsAnonymousClientsWhenExplicitlyTrusted()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:TrustNormalizedForwardedFor"] = "true",
            ["RateLimiting:AnonymousRead:PermitLimit"] = "1",
            ["RateLimiting:AnonymousRead:WindowSeconds"] = "60"
        });
        using var client = factory.CreateClient();

        using var firstClientRequest = new HttpRequestMessage(HttpMethod.Get, "/api/control/areas");
        firstClientRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10,198.51.100.1");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(firstClientRequest)).StatusCode);

        using var secondClientRequest = new HttpRequestMessage(HttpMethod.Get, "/api/control/areas");
        secondClientRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.11,198.51.100.1");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(secondClientRequest)).StatusCode);

        using var repeatedFirstClient = new HttpRequestMessage(HttpMethod.Get, "/api/control/areas");
        repeatedFirstClient.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10,198.51.100.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(repeatedFirstClient)).StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_IsExcludedFromApplicationRateLimit()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:Technical:PermitLimit"] = "1",
            ["RateLimiting:Technical:WindowSeconds"] = "60"
        });
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        }
    }

    [Theory]
    [InlineData("POST", "/api/users-roles/login", "authentication")]
    [InlineData("POST", "/api/control/runtime/runs", "simulation-launch")]
    [InlineData("GET", "/api/control/runtime/diagnostics", "expensive-read")]
    [InlineData("DELETE", "/api/users-roles/users/00000000-0000-0000-0000-000000000001", "administration")]
    public void Classifier_AssignsExpectedPolicy(string method, string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        Assert.Equal(expected, ApiRateLimitingExtensions.Classify(context));
    }

    private static ControlPlaneApiWebApplicationFactory CreateFactory(
        IReadOnlyDictionary<string, string?> configuration)
        => new(authenticated: false, configurationOverrides: configuration);
}
