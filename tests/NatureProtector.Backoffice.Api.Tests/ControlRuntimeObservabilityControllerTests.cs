using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Controllers;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ControlRuntimeObservabilityControllerTests
{
    [Fact]
    public async Task HealthRabbitMqAndEvidenceEndpoints_ReturnServiceUnavailable_WhenObservabilityIsDisabled()
    {
        var observability = new StubRuntimeObservabilityService(isAvailable: false);
        var controller = CreateController(observability);

        var health = Assert.IsType<ObjectResult>(await controller.GetOperationalHealth(CancellationToken.None));
        var rabbitMq = Assert.IsType<ObjectResult>(await controller.GetRabbitMqMetrics(CancellationToken.None));
        var evidence = Assert.IsType<ObjectResult>(await controller.ListEvidence(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, health.StatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, rabbitMq.StatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, evidence.StatusCode);
        Assert.Equal(0, observability.HealthCalls);
        Assert.Equal(0, observability.RabbitMqCalls);
        Assert.Equal(0, observability.EvidenceListCalls);
    }

    [Fact]
    public async Task GetEvidenceContent_RejectsInvalidIdBeforeCallingService()
    {
        var observability = new StubRuntimeObservabilityService(isAvailable: true);
        var controller = CreateController(observability);

        var result = Assert.IsType<BadRequestObjectResult>(
            await controller.GetEvidenceContent("../secret", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Empty(observability.RequestedEvidenceIds);
    }

    [Fact]
    public async Task GetEvidenceContent_ReturnsNotFound_WhenEvidenceContentIsUnavailable()
    {
        var observability = new StubRuntimeObservabilityService(isAvailable: true)
        {
            EvidenceContent = null
        };
        var controller = CreateController(observability);

        var result = Assert.IsType<NotFoundObjectResult>(
            await controller.GetEvidenceContent("missing-evidence", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(["missing-evidence"], observability.RequestedEvidenceIds);
    }

    [Fact]
    public async Task GetEvidenceContent_ReturnsFileWithNoStoreHeaders_WhenEvidenceIsAvailable()
    {
        var observability = new StubRuntimeObservabilityService(isAvailable: true);
        var controller = CreateController(observability);

        var result = Assert.IsType<FileContentResult>(
            await controller.GetEvidenceContent("run-summary", CancellationToken.None));

        Assert.Equal("text/plain; charset=utf-8", result.ContentType);
        Assert.Equal("evidence payload", Encoding.UTF8.GetString(result.FileContents));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("run-summary", controller.Response.Headers["X-NatureProtector-Evidence-Id"].ToString());
        Assert.Equal(["run-summary"], observability.RequestedEvidenceIds);
    }

    private static ControlRuntimeObservabilityController CreateController(IRuntimeObservabilityService observability)
    {
        var controller = new ControlRuntimeObservabilityController(observability)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-test"
                }
            }
        };
        return controller;
    }

    private sealed class StubRuntimeObservabilityService : IRuntimeObservabilityService
    {
        private readonly DateTimeOffset _observedAt = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        private readonly List<string> _requestedEvidenceIds = [];

        public StubRuntimeObservabilityService(bool isAvailable)
        {
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; }

        public string AvailabilityMessage => "Observability disabled for controller test.";

        public int HealthCalls { get; private set; }

        public int RabbitMqCalls { get; private set; }

        public int EvidenceListCalls { get; private set; }

        public IReadOnlyList<string> RequestedEvidenceIds => _requestedEvidenceIds;

        public RuntimeEvidenceContentResponse? EvidenceContent { get; init; } = new(
            new RuntimeEvidenceItemResponse(
                "run-summary",
                "Run summary",
                "txt",
                new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero),
                "local",
                "docs/evidence",
                "v1",
                ContentAvailable: true,
                DownloadAvailable: true,
                Size: 16,
                Status: "Available",
                Limitation: null),
            "text/plain; charset=utf-8",
            Encoding.UTF8.GetBytes("evidence payload"),
            "no-store");

        public Task<RuntimeOperationalHealthResponse> GetOperationalHealthAsync(CancellationToken cancellationToken)
        {
            HealthCalls++;
            return Task.FromResult(new RuntimeOperationalHealthResponse(
                _observedAt,
                [],
                new RabbitMqMetricsResponse(_observedAt, "test", RuntimeMetricCollectionStatus.Measured, [], []),
                []));
        }

        public Task<RabbitMqMetricsResponse> GetRabbitMqMetricsAsync(CancellationToken cancellationToken)
        {
            RabbitMqCalls++;
            return Task.FromResult(new RabbitMqMetricsResponse(
                _observedAt,
                "test",
                RuntimeMetricCollectionStatus.Measured,
                [],
                []));
        }

        public Task<RuntimeEvidenceCatalogResponse> ListEvidenceAsync(CancellationToken cancellationToken)
        {
            EvidenceListCalls++;
            return Task.FromResult(new RuntimeEvidenceCatalogResponse(_observedAt, [], []));
        }

        public Task<RuntimeEvidenceContentResponse?> GetEvidenceContentAsync(
            string evidenceId,
            CancellationToken cancellationToken)
        {
            _requestedEvidenceIds.Add(evidenceId);
            return Task.FromResult(EvidenceContent);
        }
    }
}
