using System.Net;
using System.Text;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeEvidenceHttpSecurityTests
{
    private const string ValidEvidenceId = "safe-evidence-123abc";

    [Theory]
    [InlineData("..%2Fsecret")]
    [InlineData("..%5Csecret")]
    [InlineData("%2E%2E%2Fsecret")]
    [InlineData("%252E%252E%252Fsecret")]
    [InlineData("%2Fetc%2Fpasswd")]
    [InlineData("C:%5CWindows%5Cwin.ini")]
    [InlineData("safe%00id")]
    [InlineData("CON")]
    public async Task EvidenceContent_RejectsInvalidEvidenceIdsBeforeService(string evidenceId)
    {
        var observability = new CapturingRuntimeObservabilityService();
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            roles: ["Admin"],
            runtimeObservabilityService: observability);
        using var client = factory.CreateClient();

        if (evidenceId.Contains("%00", StringComparison.OrdinalIgnoreCase))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetAsync($"/api/control/runtime/observability/evidence/{evidenceId}"));
            Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(observability.RequestedEvidenceIds);
            return;
        }

        using var response = await client.GetAsync($"/api/control/runtime/observability/evidence/{evidenceId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(observability.RequestedEvidenceIds);
    }

    [Fact]
    public async Task EvidenceContent_RequiresAuthentication()
    {
        var observability = new CapturingRuntimeObservabilityService();
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            authenticated: false,
            runtimeObservabilityService: observability);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/control/runtime/observability/evidence/{ValidEvidenceId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(observability.RequestedEvidenceIds);
    }

    [Fact]
    public async Task EvidenceContent_RejectsUnauthorizedRole()
    {
        var observability = new CapturingRuntimeObservabilityService();
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            roles: ["Reviewer"],
            runtimeObservabilityService: observability);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/control/runtime/observability/evidence/{ValidEvidenceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(observability.RequestedEvidenceIds);
    }

    [Fact]
    public async Task EvidenceContent_AllowsGeneratedEvidenceIdShape()
    {
        var observability = new CapturingRuntimeObservabilityService();
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            roles: ["Admin"],
            runtimeObservabilityService: observability);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/control/runtime/observability/evidence/{ValidEvidenceId}");

        response.EnsureSuccessStatusCode();
        Assert.Equal([ValidEvidenceId], observability.RequestedEvidenceIds);
        Assert.Equal("safe evidence", await response.Content.ReadAsStringAsync());
    }

    private sealed class CapturingRuntimeObservabilityService : IRuntimeObservabilityService
    {
        private readonly List<string> _requestedEvidenceIds = [];

        public IReadOnlyList<string> RequestedEvidenceIds => _requestedEvidenceIds;

        public bool IsAvailable => true;

        public string AvailabilityMessage => "Runtime observability available for evidence HTTP security tests.";

        public Task<RuntimeOperationalHealthResponse> GetOperationalHealthAsync(CancellationToken cancellationToken)
        {
            var observedAt = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
            return Task.FromResult(new RuntimeOperationalHealthResponse(
                observedAt,
                [],
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
            => Task.FromResult(new RuntimeEvidenceCatalogResponse(
                new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
                [],
                []));

        public Task<RuntimeEvidenceContentResponse?> GetEvidenceContentAsync(
            string evidenceId,
            CancellationToken cancellationToken)
        {
            _requestedEvidenceIds.Add(evidenceId);
            return Task.FromResult<RuntimeEvidenceContentResponse?>(new RuntimeEvidenceContentResponse(
                new RuntimeEvidenceItemResponse(
                    evidenceId,
                    "safe evidence",
                    "txt",
                    new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
                    "test",
                    "docs/evidence",
                    null,
                    true,
                    true,
                    13,
                    "Available",
                    null),
                "text/plain; charset=utf-8",
                Encoding.UTF8.GetBytes("safe evidence"),
                "no-store"));
        }
    }
}
