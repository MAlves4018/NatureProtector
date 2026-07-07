using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeObservabilityServiceTests
{
    [Fact]
    public async Task GetRabbitMqMetricsAsync_ReturnsMeasuredQueues_FromManagementApi()
    {
        await using var db = new SqliteControlDbContextScope();
        var queues = DistinctTopologyQueues();
        var handler = new CapturingHandler(_ => JsonResponse(BuildQueueMetricsJson(queues)));
        using var client = new HttpClient(handler);
        var service = CreateService(db, client);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Measured, metrics.CollectionStatus);
        Assert.Equal("RabbitMQ Management HTTP API", metrics.Source);
        Assert.Empty(metrics.Limitations);
        Assert.Equal(queues.Length, metrics.Queues.Count);
        Assert.All(metrics.Queues, queue =>
        {
            Assert.Equal(RuntimeMetricCollectionStatus.Measured, queue.CollectionStatus);
            Assert.Null(queue.Limitation);
            Assert.Equal(3, queue.MessagesReady);
            Assert.Equal(1, queue.MessagesUnacknowledged);
            Assert.Equal(4, queue.MessagesTotal);
            Assert.Equal(2, queue.Consumers);
        });
        Assert.Equal(new Uri("http://rabbitmq.local:15692/api/queues"), handler.RequestUri);
        Assert.Equal(
            "Basic",
            handler.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("np-user:np-pass")),
            handler.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetRabbitMqMetricsAsync_MarksMissingQueueUnavailable()
    {
        await using var db = new SqliteControlDbContextScope();
        var queues = DistinctTopologyQueues();
        var returnedQueues = queues.Skip(1).ToArray();
        var handler = new CapturingHandler(_ => JsonResponse(BuildQueueMetricsJson(returnedQueues)));
        using var client = new HttpClient(handler);
        var service = CreateService(db, client);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, metrics.CollectionStatus);
        var missingQueue = Assert.Single(metrics.Queues, queue => queue.QueueName == queues[0]);
        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, missingQueue.CollectionStatus);
        Assert.Equal("Queue was not returned by RabbitMQ Management API.", missingQueue.Limitation);
        Assert.All(metrics.Queues.Where(queue => queue.QueueName != queues[0]), queue =>
            Assert.Equal(RuntimeMetricCollectionStatus.Measured, queue.CollectionStatus));
    }

    [Fact]
    public async Task GetRabbitMqMetricsAsync_ReturnsError_WhenManagementApiPayloadIsMalformed()
    {
        await using var db = new SqliteControlDbContextScope();
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid json", Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler);
        var service = CreateService(db, client);

        var metrics = await service.GetRabbitMqMetricsAsync(CancellationToken.None);

        Assert.Equal(RuntimeMetricCollectionStatus.Error, metrics.CollectionStatus);
        Assert.All(metrics.Queues, queue =>
        {
            Assert.Equal(RuntimeMetricCollectionStatus.Error, queue.CollectionStatus);
            Assert.Equal("RabbitMQ metrics collection failed: JsonReaderException.", queue.Limitation);
        });
        Assert.Contains(metrics.Limitations, limitation =>
            limitation.Code == "rabbitmq_metrics_unavailable" &&
            limitation.Message == "RabbitMQ metrics collection failed: JsonReaderException.");
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsDegradedRuntimeSignals_WhenQueuesHaveNoConsumersAndGrafanaIsNotReady()
    {
        await using var db = new SqliteControlDbContextScope();
        var queues = DistinctTopologyQueues();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson(
                    queues,
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
        var service = CreateService(db, client);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["Backoffice.Api"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["PostgreSQL"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Degraded, components["RabbitMQ"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Degraded, components["Prevention.Host"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.NotApplicable, components["Simulator.Host"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["InfluxDB"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Degraded, components["Grafana"].Status);
        Assert.Equal(RuntimeMetricCollectionStatus.Measured, health.RabbitMq.CollectionStatus);
        Assert.Contains("Health response", components["Grafana"].Limitation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOperationalHealthAsync_DoesNotDegradeRabbitMq_ForDiagnosticRawQueueBacklog()
    {
        await using var db = new SqliteControlDbContextScope();
        var queues = DistinctTopologyQueues();
        var queueMetrics = queues.Select(queueName =>
            string.Equals(queueName, NatureProtectorRabbitMqTopology.IngestionReadingsQueue, StringComparison.Ordinal)
                ? (queueName, MessagesReady: 0, MessagesUnacknowledged: 0, MessagesTotal: 0, Consumers: 1)
                : (queueName, MessagesReady: 12, MessagesUnacknowledged: 0, MessagesTotal: 12, Consumers: 0));
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
        var service = CreateService(db, client);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);

        var components = health.Components.ToDictionary(component => component.Component, StringComparer.Ordinal);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["RabbitMQ"].Status);
        Assert.Equal(RuntimeOperationalHealthStatus.Healthy, components["Prevention.Host"].Status);
        Assert.Contains(NatureProtectorRabbitMqTopology.ObservabilityRawQueue, components["RabbitMQ"].Limitation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOperationalHealthAsync_ReportsAuthRequired_WhenHttpHealthEndpointReturnsUnauthorized()
    {
        await using var db = new SqliteControlDbContextScope();
        var queues = DistinctTopologyQueues();
        var handler = new CapturingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (string.Equals(path, "/api/queues", StringComparison.Ordinal))
            {
                return JsonResponse(BuildQueueMetricsJson(queues));
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

    private static RuntimeObservabilityService CreateService(SqliteControlDbContextScope db, HttpClient client)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RabbitMqOptions.SectionName}:HostName"] = "rabbitmq.local",
                [$"{RabbitMqOptions.SectionName}:ManagementPort"] = "15692",
                [$"{RabbitMqOptions.SectionName}:UserName"] = "np-user",
                [$"{RabbitMqOptions.SectionName}:Password"] = "np-pass"
            })
            .Build();

        return new RuntimeObservabilityService(
            db.Factory,
            configuration,
            new SingleClientFactory(client),
            new TestWebHostEnvironment());
    }

    private static string[] DistinctTopologyQueues()
        => NatureProtectorRabbitMqTopology.Bindings
            .Select(binding => binding.QueueName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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
