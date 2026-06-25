using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class AuthorizationMatrixTests
{
    private static readonly EndpointPolicy[] EndpointPolicies =
    [
        Anonymous("GET", "/health", "/health"),
        Anonymous("GET", "/health/live", "/health/live"),
        Anonymous("GET", "/openapi/{documentName}.json", "/openapi/v1.json"),

        Anonymous("GET", "/api/control/areas", "/api/control/areas"),
        Anonymous("GET", "/api/control/areas/{areaCode}", "/api/control/areas/proenca-a-nova"),
        Anonymous("GET", "/api/control/areas/{areaCode}/GeoJSON", "/api/control/areas/proenca-a-nova/GeoJSON"),
        Anonymous("GET", "/api/control/areas/{areaCode}/grid-cells", "/api/control/areas/proenca-a-nova/grid-cells"),
        Anonymous("GET", "/api/control/areas/{areaCode}/sensor-nodes", "/api/control/areas/proenca-a-nova/sensor-nodes"),
        Anonymous("GET", "/api/control/areas/{areaCode}/alerts/active", "/api/control/areas/proenca-a-nova/alerts/active"),

        Roles("GET", "/api/control/areas/{areaCode}/scenarios", "/api/control/areas/proenca-a-nova/scenarios", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/areas/{areaCode}/operational-state", "/api/control/areas/proenca-a-nova/operational-state", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/areas/{areaCode}/cells/operational-state", "/api/control/areas/proenca-a-nova/cells/operational-state", AccessPolicy.SimPipelineAdmin),

        Roles("GET", "/api/control/configurations", "/api/control/configurations", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/configurations/active", "/api/control/configurations/active", AccessPolicy.SimPipelineAdmin),
        Roles("POST", "/api/control/configurations/{versionNumber:int}/activate", "/api/control/configurations/1/activate", AccessPolicy.SimAdmin),

        Roles("GET", "/api/control/runtime/summary", "/api/control/runtime/summary", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/diagnostics", "/api/control/runtime/diagnostics", AccessPolicy.SimPipelineAdmin),
        Roles("POST", "/api/control/runtime/diagnostics/{diagnosticId}", "/api/control/runtime/diagnostics/runtime-table-counts", AccessPolicy.SimAdmin),
        Roles("POST", "/api/control/runtime/runs", "/api/control/runtime/runs", AccessPolicy.SimAdmin),
        Roles("GET", "/api/control/runtime/runs/latest", "/api/control/runtime/runs/latest", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/runs/{runId:guid}", "/api/control/runtime/runs/90000000-0000-0000-0000-000000000001", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/runs/{runId:guid}/audit", "/api/control/runtime/runs/90000000-0000-0000-0000-000000000001/audit", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/runs/{runId:guid}/timings", "/api/control/runtime/runs/90000000-0000-0000-0000-000000000001/timings", AccessPolicy.SimPipelineAdmin),
        Roles("POST", "/api/control/runtime/reset", "/api/control/runtime/reset", AccessPolicy.SimAdmin),

        Roles("GET", "/api/control/runtime/observability/health", "/api/control/runtime/observability/health", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/observability/rabbitmq", "/api/control/runtime/observability/rabbitmq", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/observability/evidence", "/api/control/runtime/observability/evidence", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/runtime/observability/evidence/{evidenceId}", "/api/control/runtime/observability/evidence/missing-evidence", AccessPolicy.SimPipelineAdmin),

        Roles("GET", "/api/control/simulation-runs", "/api/control/simulation-runs", AccessPolicy.SimPipelineAdmin),
        Roles("GET", "/api/control/simulation-runs/{runId:guid}", "/api/control/simulation-runs/90000000-0000-0000-0000-000000000001", AccessPolicy.SimPipelineAdmin),

        Roles("GET", "/api/dev/controlled-validation/p3", "/api/dev/controlled-validation/p3", AccessPolicy.SimAdmin),
        Roles("POST", "/api/dev/controlled-validation/p3/run", "/api/dev/controlled-validation/p3/run", AccessPolicy.SimAdmin),

        Anonymous("POST", "/api/users-roles/login", "/api/users-roles/login"),
        Authenticated("POST", "/api/users-roles/logout", "/api/users-roles/logout"),
        Roles("POST", "/api/users-roles/users", "/api/users-roles/users", AccessPolicy.Admin),
        Roles("GET", "/api/users-roles/users/{userId:guid}", "/api/users-roles/users/00000000-0000-0000-0000-000000000001", AccessPolicy.Admin),
        Roles("PUT", "/api/users-roles/users/{userId:guid}", "/api/users-roles/users/00000000-0000-0000-0000-000000000001", AccessPolicy.Admin),
        Roles("DELETE", "/api/users-roles/users/{userId:guid}", "/api/users-roles/users/00000000-0000-0000-0000-000000000001", AccessPolicy.Admin),
        Roles("POST", "/api/users-roles/roles", "/api/users-roles/roles", AccessPolicy.Admin),
        Roles("GET", "/api/users-roles/roles/{roleId}", "/api/users-roles/roles/1", AccessPolicy.Admin),
        Roles("PUT", "/api/users-roles/roles/{roleId}", "/api/users-roles/roles/1", AccessPolicy.Admin),
        Roles("DELETE", "/api/users-roles/roles/{roleId}", "/api/users-roles/roles/1", AccessPolicy.Admin),
        Roles("PUT", "/api/users-roles/users/{userId:guid}/roles/{roleId}", "/api/users-roles/users/00000000-0000-0000-0000-000000000001/roles/1", AccessPolicy.Admin),
        Roles("DELETE", "/api/users-roles/users/{userId:guid}/roles/{roleId}", "/api/users-roles/users/00000000-0000-0000-0000-000000000001/roles/1", AccessPolicy.Admin),
        Roles("GET", "/api/users-roles/roles/{roleId}/users", "/api/users-roles/roles/1/users", AccessPolicy.Admin),
        Authenticated("GET", "/api/users-roles/users/{userId:guid}/roles", "/api/users-roles/users/00000000-0000-0000-0000-000000000001/roles"),
        Authenticated("GET", "/api/users-roles/users/{userId:guid}/roles/{roleId}", "/api/users-roles/users/00000000-0000-0000-0000-000000000001/roles/1"),
        Authenticated("GET", "/api/users-roles/me", "/api/users-roles/me")
    ];

    private static readonly string[] AllProfiles = ["Anonymous", "Admin", "Sim", "Pipeline", "Reviewer"];

    [Fact]
    public async Task EndpointInventory_HasExplicitAuthorizationClassification()
    {
        await using var factory = CreateFactory("Admin");
        using var _ = factory.CreateClient();
        var runtimeEndpoints = GetRuntimeEndpoints(factory);
        var expected = EndpointPolicies.ToDictionary(policy => policy.Key);

        Assert.Empty(runtimeEndpoints.Keys.Except(expected.Keys).OrderBy(key => key.RoutePattern).ThenBy(key => key.Method));
        Assert.Empty(expected.Keys.Except(runtimeEndpoints.Keys).OrderBy(key => key.RoutePattern).ThenBy(key => key.Method));

        foreach (var policy in EndpointPolicies)
        {
            AssertEndpointMetadata(policy, runtimeEndpoints[policy.Key]);
        }
    }

    [Theory]
    [MemberData(nameof(EndpointAccessCases))]
    public async Task BackofficeEndpoints_EnforceExpectedAuthorization(
        string method,
        string path,
        AccessPolicy accessPolicy,
        string profile)
    {
        await using var factory = CreateFactory(profile);
        using var client = factory.CreateClient();

        using var response = await SendAsync(client, method, path);

        var expected = ExpectedOutcome(accessPolicy, profile);
        if (expected == AccessOutcome.Allowed)
        {
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
            return;
        }

        Assert.Equal(
            expected == AccessOutcome.Unauthorized
                ? HttpStatusCode.Unauthorized
                : HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task RuntimeEvidenceTraversal_IsRejectedAfterAuthorization()
    {
        await using var factory = CreateFactory("Admin");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/control/runtime/observability/evidence/..%2Fsecret");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static IEnumerable<object[]> EndpointAccessCases()
    {
        foreach (var policy in EndpointPolicies)
        {
            foreach (var profile in AllProfiles)
            {
                yield return [policy.Method, policy.SamplePath, policy.AccessPolicy, profile];
            }
        }
    }

    private static IReadOnlyDictionary<EndpointKey, RouteEndpoint> GetRuntimeEndpoints(
        ControlPlaneApiWebApplicationFactory factory)
    {
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = new Dictionary<EndpointKey, RouteEndpoint>();

        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var routePattern = NormalizeRoutePattern(endpoint.RoutePattern.RawText);
            foreach (var method in GetHttpMethods(endpoint))
            {
                var key = new EndpointKey(method, routePattern);
                Assert.False(
                    endpoints.ContainsKey(key),
                    $"Duplicate endpoint discovered for {key.Method} {key.RoutePattern}.");
                endpoints.Add(key, endpoint);
            }
        }

        return endpoints;
    }

    private static IEnumerable<string> GetHttpMethods(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        return methods is { Count: > 0 }
            ? methods.Select(method => method.ToUpperInvariant())
            : ["GET"];
    }

    private static void AssertEndpointMetadata(EndpointPolicy policy, RouteEndpoint endpoint)
    {
        if (policy.AccessPolicy == AccessPolicy.Anonymous)
        {
            var hasAllowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var hasAuthorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            Assert.True(
                hasAllowAnonymous || !hasAuthorize,
                $"{policy.Method} {policy.RoutePattern} must be anonymous by metadata.");
            return;
        }

        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());

        var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        Assert.NotEmpty(authorizeData);

        var effectiveRoles = ResolveEffectiveRoles(authorizeData);
        if (policy.AccessPolicy == AccessPolicy.Authenticated)
        {
            Assert.Empty(effectiveRoles);
            return;
        }

        Assert.Equal(ExpectedRoles(policy.AccessPolicy), effectiveRoles.Order(StringComparer.Ordinal).ToArray());
    }

    private static string[] ResolveEffectiveRoles(IReadOnlyList<IAuthorizeData> authorizeData)
    {
        var roleSets = authorizeData
            .Select(data => SplitRoles(data.Roles))
            .Where(roles => roles.Length > 0)
            .ToArray();

        if (roleSets.Length == 0)
        {
            return [];
        }

        var effective = roleSets[0].ToHashSet(StringComparer.Ordinal);
        foreach (var roleSet in roleSets.Skip(1))
        {
            effective.IntersectWith(roleSet);
        }

        return effective.Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] SplitRoles(string? roles)
        => string.IsNullOrWhiteSpace(roles)
            ? []
            : roles
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal)
                .ToArray();

    private static AccessOutcome ExpectedOutcome(AccessPolicy policy, string profile)
    {
        if (policy == AccessPolicy.Anonymous)
        {
            return AccessOutcome.Allowed;
        }

        if (profile == "Anonymous")
        {
            return AccessOutcome.Unauthorized;
        }

        return policy switch
        {
            AccessPolicy.Authenticated => AccessOutcome.Allowed,
            AccessPolicy.Admin => profile == "Admin" ? AccessOutcome.Allowed : AccessOutcome.Forbidden,
            AccessPolicy.SimAdmin => profile is "Admin" or "Sim" ? AccessOutcome.Allowed : AccessOutcome.Forbidden,
            AccessPolicy.SimPipelineAdmin => profile is "Admin" or "Sim" or "Pipeline" ? AccessOutcome.Allowed : AccessOutcome.Forbidden,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
        };
    }

    private static string[] ExpectedRoles(AccessPolicy policy)
        => policy switch
        {
            AccessPolicy.Admin => ["Admin"],
            AccessPolicy.SimAdmin => ["Admin", "Sim"],
            AccessPolicy.SimPipelineAdmin => ["Admin", "Pipeline", "Sim"],
            _ => []
        };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
        => method switch
        {
            "GET" => client.GetAsync(path),
            "POST" => client.PostAsync(path, CreatePostContent(path)),
            "PUT" => client.PutAsync(path, CreatePutContent(path)),
            "DELETE" => client.DeleteAsync(path),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };

    private static JsonContent? CreatePostContent(string path)
    {
        if (path.EndsWith("/login", StringComparison.Ordinal))
        {
            return JsonContent.Create(new { usernameOrEmail = "admin", password = "admin123" });
        }

        if (path.Contains("/diagnostics/", StringComparison.Ordinal))
        {
            return JsonContent.Create(new { areaCode = "proenca-a-nova", recentMinutes = 30 });
        }

        if (path.EndsWith("/runtime/runs", StringComparison.Ordinal))
        {
            return JsonContent.Create(new
            {
                areaCode = "proenca-a-nova",
                scenarioCode = "scenario_b",
                sensorCount = 1,
                numberOfCycles = 1,
                intervalSeconds = 1,
                seed = 42,
                degradationProfile = "none"
            });
        }

        if (path.EndsWith("/runtime/reset", StringComparison.Ordinal))
        {
            return JsonContent.Create(new { scope = "runtime", confirm = "RESET", dryRun = true });
        }

        if (path.EndsWith("/p3/run", StringComparison.Ordinal))
        {
            return JsonContent.Create(new { runLabel = "matrix" });
        }

        if (path.EndsWith("/users", StringComparison.Ordinal))
        {
            return JsonContent.Create(new
            {
                username = "matrix.user",
                password = "matrix-password",
                email = "matrix.user@example.local",
                organization = "tests",
                roles = new[] { "Reviewer" }
            });
        }

        if (path.EndsWith("/roles", StringComparison.Ordinal))
        {
            return JsonContent.Create(new { name = "Reviewer" });
        }

        return null;
    }

    private static JsonContent? CreatePutContent(string path)
    {
        if (path.Contains("/users/", StringComparison.Ordinal) &&
            !path.Contains("/roles/", StringComparison.Ordinal))
        {
            return JsonContent.Create(new
            {
                username = "matrix.user",
                password = "matrix-password",
                email = "matrix.user@example.local",
                organization = "tests",
                roles = new[] { "Reviewer" }
            });
        }

        if (path.Contains("/roles/", StringComparison.Ordinal))
        {
            return JsonContent.Create(new { name = "Reviewer" });
        }

        return null;
    }

    private static string NormalizeRoutePattern(string? routePattern)
    {
        var pattern = string.IsNullOrWhiteSpace(routePattern)
            ? "/"
            : routePattern;

        return pattern.StartsWith("/", StringComparison.Ordinal)
            ? pattern
            : "/" + pattern;
    }

    private static EndpointPolicy Anonymous(string method, string routePattern, string samplePath)
        => new(method, routePattern, samplePath, AccessPolicy.Anonymous);

    private static EndpointPolicy Authenticated(string method, string routePattern, string samplePath)
        => new(method, routePattern, samplePath, AccessPolicy.Authenticated);

    private static EndpointPolicy Roles(
        string method,
        string routePattern,
        string samplePath,
        AccessPolicy accessPolicy)
        => new(method, routePattern, samplePath, accessPolicy);

    private static ControlPlaneApiWebApplicationFactory CreateFactory(string profile)
        => profile switch
        {
            "Anonymous" => new ControlPlaneApiWebApplicationFactory(
                authenticated: false,
                runtimeObservabilityService: new MatrixRuntimeObservabilityService()),
            "Admin" => new ControlPlaneApiWebApplicationFactory(
                roles: ["Admin"],
                runtimeObservabilityService: new MatrixRuntimeObservabilityService()),
            "Sim" => new ControlPlaneApiWebApplicationFactory(
                roles: ["Sim"],
                runtimeObservabilityService: new MatrixRuntimeObservabilityService()),
            "Pipeline" => new ControlPlaneApiWebApplicationFactory(
                roles: ["Pipeline"],
                runtimeObservabilityService: new MatrixRuntimeObservabilityService()),
            _ => new ControlPlaneApiWebApplicationFactory(
                roles: [profile],
                runtimeObservabilityService: new MatrixRuntimeObservabilityService())
        };

    public enum AccessPolicy
    {
        Anonymous,
        Authenticated,
        Admin,
        SimAdmin,
        SimPipelineAdmin
    }

    private enum AccessOutcome
    {
        Allowed,
        Unauthorized,
        Forbidden
    }

    private readonly record struct EndpointKey(string Method, string RoutePattern);

    private sealed record EndpointPolicy(
        string Method,
        string RoutePattern,
        string SamplePath,
        AccessPolicy AccessPolicy)
    {
        public EndpointKey Key => new(Method, RoutePattern);
    }

    private sealed class MatrixRuntimeObservabilityService : IRuntimeObservabilityService
    {
        public bool IsAvailable => true;

        public string AvailabilityMessage => "Matrix observability available.";

        public Task<RuntimeOperationalHealthResponse> GetOperationalHealthAsync(CancellationToken cancellationToken)
        {
            var observedAt = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
            return Task.FromResult(new RuntimeOperationalHealthResponse(
                observedAt,
                [new RuntimeOperationalHealthComponentResponse("Backoffice.Api", RuntimeOperationalHealthStatus.Healthy, observedAt, "test", "matrix", observedAt, null, null, "test", null)],
                new RabbitMqMetricsResponse(observedAt, "test", RuntimeMetricCollectionStatus.Measured, [], []),
                []));
        }

        public Task<RabbitMqMetricsResponse> GetRabbitMqMetricsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RabbitMqMetricsResponse(
                new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
                "test",
                RuntimeMetricCollectionStatus.Measured,
                [],
                []));

        public Task<RuntimeEvidenceCatalogResponse> ListEvidenceAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RuntimeEvidenceCatalogResponse(DateTimeOffset.UtcNow, [], []));

        public Task<RuntimeEvidenceContentResponse?> GetEvidenceContentAsync(string evidenceId, CancellationToken cancellationToken)
            => Task.FromResult<RuntimeEvidenceContentResponse?>(null);
    }
}
