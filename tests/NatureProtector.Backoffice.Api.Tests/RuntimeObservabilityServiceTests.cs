using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeObservabilityServiceTests
{
    [Fact]
    public async Task GetRabbitMqMetricsAsync_UsesEffectiveTopologyAndQueueRoles_WhenRawIsDisabled()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(_ => JsonResponse(BuildQueueMetricsJson(["np.custom.ingestion"])));
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: false);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Measured, metrics.CollectionStatus);
        Assert.Equal("RabbitMQ Management API", metrics.Source);
        Assert.Empty(metrics.Limitations);
        Assert.Collection(
            metrics.Queues,
            primary =>
            {
                Assert.Equal("np.custom.ingestion", primary.QueueName);
                Assert.Equal(RabbitMqQueueRoles.PrimaryWorkQueue, primary.QueueRole);
                Assert.True(primary.Enabled);
                Assert.True(primary.ConsumerRequired);
                Assert.True(primary.BlocksRuntimeHealth);
                Assert.Equal(RuntimeMetricCollectionStatus.Measured, primary.CollectionStatus);
                Assert.Equal(3, primary.MessagesReady);
                Assert.Equal(1, primary.MessagesUnacknowledged);
                Assert.Equal(4, primary.MessagesTotal);
                Assert.Equal(2, primary.Consumers);
                Assert.Null(primary.Limitation);
            },
            auxiliary =>
            {
                Assert.Equal("np.custom.raw", auxiliary.QueueName);
                Assert.Equal(RabbitMqQueueRoles.AuxiliaryDiagnosticQueue, auxiliary.QueueRole);
                Assert.False(auxiliary.Enabled);
                Assert.False(auxiliary.ConsumerRequired);
                Assert.False(auxiliary.BlocksRuntimeHealth);
                Assert.Equal(RuntimeMetricCollectionStatus.NotApplicable, auxiliary.CollectionStatus);
                Assert.Null(auxiliary.MessagesReady);
                Assert.Equal("Queue is disabled by configuration.", auxiliary.Limitation);
            });
        Assert.Equal(new Uri("https://rabbitmq-management.local:15692/api/queues"), handler.RequestUri);
        Assert.Equal("Basic", handler.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("np-monitor:np-monitor-pass")),
            handler.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetRabbitMqMetricsAsync_ReportsDisabledDurableQueue_WhenItStillExists()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(_ => JsonResponse(BuildQueueMetricsJson(
            ["np.custom.ingestion", "np.custom.raw"])));
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: false);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Measured, metrics.CollectionStatus);
        var staleRaw = Assert.Single(metrics.Queues, queue => queue.QueueRole == RabbitMqQueueRoles.AuxiliaryDiagnosticQueue);
        Assert.False(staleRaw.Enabled);
        Assert.Equal(RuntimeMetricCollectionStatus.Measured, staleRaw.CollectionStatus);
        Assert.Contains("cleanup may be pending", staleRaw.Limitation, StringComparison.Ordinal);
        Assert.Contains(metrics.Limitations, limitation =>
            limitation.Code == "rabbitmq_disabled_queue_present" &&
            limitation.Message.Contains("np.custom.raw", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetRabbitMqMetricsAsync_MarksEnabledRawQueueUnavailable_WhenItIsMissing()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(_ => JsonResponse(BuildQueueMetricsJson(["np.custom.ingestion"])));
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: true);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, metrics.CollectionStatus);
        var missingRaw = Assert.Single(metrics.Queues, queue => queue.QueueName == "np.custom.raw");
        Assert.True(missingRaw.Enabled);
        Assert.Equal(RabbitMqQueueRoles.AuxiliaryDiagnosticQueue, missingRaw.QueueRole);
        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, missingRaw.CollectionStatus);
        Assert.Equal("Enabled queue was not returned by RabbitMQ Management API.", missingRaw.Limitation);
    }

    [Fact]
    public async Task GetOperationalHealthAsync_KeepsPrimaryHealthy_WhenEnabledAuxiliaryMetricsAreUnavailable()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson(
                    ["np.custom.ingestion"],
                    messagesReady: 0,
                    messagesUnacknowledged: 0,
                    messagesTotal: 0,
                    consumers: 1));
            }

            return JsonResponse("""{ "status": "ok", "database": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: true);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, health.RabbitMq.CollectionStatus);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["RabbitMQ"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["Prevention.Host"].Status);
        Assert.Contains("np.custom.raw", components["RabbitMQ"].Limitation, StringComparison.Ordinal);
        Assert.Contains("primary work queues", components["RabbitMQ"].Limitation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRabbitMqMetricsAsync_ReturnsErrorOnlyForEnabledQueues_WhenPayloadIsMalformed()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid json", Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler);
        var service = CreateService(db, client, rawEnabled: false);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Error, metrics.CollectionStatus);
        var primary = Assert.Single(metrics.Queues, queue => queue.Enabled);
        Assert.Equal(RuntimeMetricCollectionStatus.Error, primary.CollectionStatus);
        Assert.Equal("RabbitMQ metrics collection failed: JsonReaderException.", primary.Limitation);
        var raw = Assert.Single(metrics.Queues, queue => !queue.Enabled);
        Assert.Equal(RuntimeMetricCollectionStatus.NotApplicable, raw.CollectionStatus);
        Assert.Contains(metrics.Limitations, limitation =>
            limitation.Code == "rabbitmq_metrics_unavailable" &&
            limitation.Message == "RabbitMQ metrics collection failed: JsonReaderException.");
    }

    [Fact]
    public async Task GetRabbitMqMetricsAsync_ReturnsUnavailableOnlyForEnabledQueues_WhenManagementApiFails()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var service = CreateService(db, client, rawEnabled: false);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, metrics.CollectionStatus);
        var primary = Assert.Single(metrics.Queues, queue => queue.Enabled);
        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, primary.CollectionStatus);
        Assert.Contains("HTTP 503", primary.Limitation, StringComparison.Ordinal);
        var raw = Assert.Single(metrics.Queues, queue => !queue.Enabled);
        Assert.Equal(RuntimeMetricCollectionStatus.NotApplicable, raw.CollectionStatus);
        Assert.Contains(metrics.Limitations, limitation =>
            limitation.Code == "rabbitmq_metrics_unavailable" &&
            limitation.Message.Contains("HTTP 503", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetOperationalHealthAsync_UsesPrimaryRoleForCustomQueueName()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson(
                    ["np.custom.ingestion"],
                    messagesReady: 5,
                    messagesUnacknowledged: 0,
                    messagesTotal: 5,
                    consumers: 0));
            }

            if (string.Equals(path, "/api/health", StringComparison.Ordinal))
            {
                return JsonResponse("""{ "database": "failed" }""");
            }

            return JsonResponse("""{ "status": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: false);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeOperationalHealthStatus.Degraded, components["RabbitMQ"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Degraded, components["Prevention.Host"].Status);
        Assert.Contains("np.custom.ingestion", components["Prevention.Host"].Source, StringComparison.Ordinal);
        Assert.Equal(RuntimeMetricCollectionStatus.Measured, health.RabbitMq.CollectionStatus);
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsUnknownRabbitMqAndPrevention_WhenPrimaryQueueIsMissing()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson([NatureProtectorRabbitMqTopology.ObservabilityRawQueue]));
            }

            return JsonResponse("""{ "status": "ok", "database": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(db, client, rawEnabled: true);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, health.RabbitMq.CollectionStatus);
        Assert.Equal(RuntimeOperationalHealthStatus.Unknown, components["RabbitMQ"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Unknown, components["Prevention.Host"].Status);
        Assert.Contains("positive signal", components["RabbitMQ"].Limitation, StringComparison.Ordinal);
        Assert.Contains(health.Limitations, limitation => limitation.Code == "health_unknown_is_explicit");
    }

    [Fact]
    public async Task GetOperationalHealthAsync_DoesNotDegradeRabbitMq_ForEnabledAuxiliaryBacklog()
    {
        await using var db = new SqliteControlDbContextScope();
        var queueMetrics = new[]
        {
            (QueueName: "np.custom.ingestion", MessagesReady: 0, MessagesUnacknowledged: 0, MessagesTotal: 0, Consumers: 1),
            (QueueName: "np.custom.raw", MessagesReady: 12, MessagesUnacknowledged: 0, MessagesTotal: 12, Consumers: 0)
        };
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson(queueMetrics));
            }

            return JsonResponse("""{ "status": "ok", "database": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: true);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["RabbitMQ"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["Prevention.Host"].Status);
        Assert.Contains("np.custom.raw", components["RabbitMQ"].Limitation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsLegacyDisabledQueueAsAdvisoryOnly()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson(
                    ["np.custom.ingestion", "np.custom.raw"],
                    messagesReady: 0,
                    messagesUnacknowledged: 0,
                    messagesTotal: 0,
                    consumers: 1));
            }

            return JsonResponse("""{ "status": "ok", "database": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(
            db,
            client,
            ingestionQueueName: "np.custom.ingestion",
            rawQueueName: "np.custom.raw",
            rawEnabled: false);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var rabbitMq = Assert.Single(health.Components, component => component.Component == "RabbitMQ");
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, rabbitMq.Status);
        Assert.Contains("disabled by configuration", rabbitMq.Limitation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("np.custom.raw", rabbitMq.Limitation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsAuthRequired_WhenHttpHealthEndpointReturnsUnauthorized()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson([NatureProtectorRabbitMqTopology.IngestionReadingsQueue]));
            }

            if (string.Equals(path, "/health", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return JsonResponse("""{ "database": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(db, client);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeOperationalHealthStatus.AuthRequired, components["InfluxDB"].Status);
        Assert.Null(components["InfluxDB"].LastFailureAt);
        Assert.Contains("requires authentication", components["InfluxDB"].Limitation, StringComparison.Ordinal);
        Assert.Contains(health.Limitations, limitation => limitation.Code == "health_auth_required_is_explicit");
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsUnknown_WhenGrafanaHealthPayloadIsMalformed()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson([NatureProtectorRabbitMqTopology.IngestionReadingsQueue]));
            }

            if (string.Equals(path, "/api/health", StringComparison.Ordinal))
            {
                return JsonResponse("{ malformed");
            }

            return JsonResponse("""{ "status": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(db, client);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var grafana = Assert.Single(health.Components, component => component.Component == "Grafana");
        Assert.Equal(RuntimeOperationalHealthStatus.Unknown, grafana.Status);
        Assert.Contains("JsonReaderException", grafana.Reason, StringComparison.Ordinal);
        Assert.Contains(health.Limitations, limitation => limitation.Code == "health_unknown_is_explicit");
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsUnknownSimulatorLifecycle_WhenLatestRunStatusIsUnmapped()
    {
        await using var db = new SqliteControlDbContextScope();
        var observedRunTime = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var areaId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var scenarioId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var configurationVersionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        await db.SeedAsync(context =>
        {
            context.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = configurationVersionId,
                VersionNumber = 1,
                Description = "test",
                IsActive = true,
                CreatedAt = observedRunTime,
                CreatedBy = "tests"
            });
            context.Areas.Add(new AreaRecord
            {
                Id = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "PT-11",
                Name = "Test Area",
                CountryCode = "PT"
            });
            context.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "scenario_unknown",
                Name = "Unknown",
                ScenarioKind = ScenarioCategory.Exercise
            });
            context.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = Guid.NewGuid(),
                AreaId = areaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = configurationVersionId,
                ScenarioCode = "scenario_unknown",
                ScenarioName = "Unknown",
                CreatedAt = observedRunTime,
                LogicalStartTimestamp = observedRunTime,
                IntervalSeconds = 1,
                NumberOfCycles = 1,
                Status = (SimulationRunStatus)999
            });
            return Task.CompletedTask;
        });
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson([NatureProtectorRabbitMqTopology.IngestionReadingsQueue]));
            }

            return JsonResponse("""{ "status": "ok", "database": "ok" }""");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(db, client);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var simulator = Assert.Single(health.Components, component => component.Component == "Simulator.Host");
        Assert.Equal(RuntimeOperationalHealthStatus.Unknown, simulator.Status);
        Assert.Contains("999", simulator.Reason, StringComparison.Ordinal);
        Assert.Contains("not mapped", simulator.Limitation, StringComparison.Ordinal);
    }

    private static RuntimeObservabilityService CreateService(
        SqliteControlDbContextScope db,
        HttpClient client,
        string? ingestionQueueName = null,
        string? rawQueueName = null,
        bool rawEnabled = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = "rabbitmq-amqp.local",
            UserName = "np-app",
            Password = "np-app-pass",
            ManagementScheme = "https",
            ManagementHost = "rabbitmq-management.local",
            ManagementPort = 15692,
            ManagementUserName = "np-monitor",
            ManagementPassword = "np-monitor-pass",
            IngestionReadingsQueueName =
                ingestionQueueName ?? NatureProtectorRabbitMqTopology.IngestionReadingsQueue,
            ObservabilityRawQueueName =
                rawQueueName ?? NatureProtectorRabbitMqTopology.ObservabilityRawQueue,
            ObservabilityRawEnabled = rawEnabled
        };

        return new RuntimeObservabilityService(
            db.Factory,
            configuration,
            new SingleClientFactory(client),
            Options.Create(rabbitMqOptions),
            new TestWebHostEnvironment());
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string BuildQueueMetricsJson(
        IEnumerable<string> queueNames,
        int messagesReady = 3,
        int messagesUnacknowledged = 1,
        int messagesTotal = 4,
        int consumers = 2)
        => BuildQueueMetricsJson(queueNames.Select(queueName =>
            (queueName, messagesReady, messagesUnacknowledged, messagesTotal, consumers)));

    private static string BuildQueueMetricsJson(
        IEnumerable<(string QueueName, int MessagesReady, int MessagesUnacknowledged, int MessagesTotal, int Consumers)> queueMetrics)
    {
        var items = queueMetrics.Select(queue =>
            $$"""
            {
              "name": "{{queue.QueueName}}",
              "messages_ready": {{queue.MessagesReady}},
              "messages_unacknowledged": {{queue.MessagesUnacknowledged}},
              "messages": {{queue.MessagesTotal}},
              "consumers": {{queue.Consumers}}
            }
            """);

        return "[" + string.Join(",", items) + "]";
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
