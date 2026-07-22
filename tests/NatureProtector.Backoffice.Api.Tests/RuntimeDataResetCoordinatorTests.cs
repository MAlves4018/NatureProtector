using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeDataResetCoordinatorTests
{
    [Fact]
    public async Task DatabaseOnlyCoordinator_ReportsExternalStoresUnavailable()
    {
        var results = await DatabaseOnlyRuntimeDataResetCoordinator.Instance.ResetAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Collection(
            results,
            rabbit =>
            {
                Assert.Equal("RabbitMQ", rabbit.Store);
                Assert.Equal("Unavailable", rabbit.Status);
                Assert.Contains("No RabbitMQ reset coordinator", rabbit.Message, StringComparison.Ordinal);
            },
            influx =>
            {
                Assert.Equal("InfluxDB", influx.Store);
                Assert.Equal("Unavailable", influx.Status);
                Assert.Contains("No InfluxDB reset coordinator", influx.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task InspectAsync_RequiresEveryEnabledRabbitMqQueueAndReachableInflux()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/queues")
            {
                return JsonResponse(BuildRabbitQueuesJson(
                    ("np.ingestion.test", 3, 0),
                    ("np.raw.test", 1, 0)));
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/health")
            {
                return JsonResponse("""{ "status": "pass" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = new HttpClient(handler);
        var coordinator = CreateCoordinator(client);

        var results = await coordinator.InspectAsync(CancellationToken.None);

        Assert.Collection(
            results,
            rabbit =>
            {
                Assert.Equal("RabbitMQ", rabbit.Store);
                Assert.Equal("Ready", rabbit.Status);
                Assert.Equal(4, rabbit.Before);
                Assert.Equal(4, rabbit.After);
                Assert.Contains("no unacknowledged", rabbit.Message, StringComparison.Ordinal);
            },
            influx =>
            {
                Assert.Equal("InfluxDB", influx.Store);
                Assert.Equal("Ready", influx.Status);
                Assert.Contains("np-test", influx.Message, StringComparison.Ordinal);
            });
        Assert.All(handler.Requests, request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/queues")
            {
                Assert.Equal("Basic", request.Authorization?.Scheme);
                Assert.Equal(
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("np-reset:np-reset-pass")),
                    request.Authorization?.Parameter);
            }
        });
    }

    [Fact]
    public async Task ResetAsync_DoesNotModifyStores_WhenRabbitMqHasUnacknowledgedDeliveries()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/queues")
            {
                return JsonResponse(BuildRabbitQueuesJson(("np.ingestion.test", 5, 2)));
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/health")
            {
                return JsonResponse("""{ "status": "pass" }""");
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        using var client = new HttpClient(handler);
        var coordinator = CreateCoordinator(client, observabilityRawEnabled: false);

        var results = await coordinator.ResetAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        Assert.Collection(
            results,
            rabbit =>
            {
                Assert.Equal("RabbitMQ", rabbit.Store);
                Assert.Equal("Busy", rabbit.Status);
                Assert.Equal(5, rabbit.Before);
                Assert.Contains("did not purge RabbitMQ", rabbit.Message, StringComparison.Ordinal);
                Assert.Contains("unacknowledged", rabbit.Message, StringComparison.Ordinal);
            },
            influx =>
            {
                Assert.Equal("InfluxDB", influx.Store);
                Assert.Equal("NotAttempted", influx.Status);
                Assert.Contains("RabbitMQ was not quiescent", influx.Message, StringComparison.Ordinal);
            });
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ResetAsync_PurgesRabbitMqBeforeHardDeletingAndRecreatingInfluxTables()
    {
        var resetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var queueInspectionCount = 0;
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/queues")
            {
                queueInspectionCount++;
                return JsonResponse(queueInspectionCount == 1
                    ? BuildRabbitQueuesJson(("np.ingestion.test", 2, 0), ("np.raw.test", 1, 0))
                    : BuildRabbitQueuesJson(("np.ingestion.test", 0, 0), ("np.raw.test", 0, 0)));
            }

            if (request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath.EndsWith("/contents", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/health")
            {
                return JsonResponse("""{ "status": "pass" }""");
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v3/query_sql")
            {
                return JsonResponse("""
                    [
                      { "table_schema": "iox", "table_name": "accepted_readings" },
                      { "table_schema": "iox", "table_name": "accepted_readings-archive" },
                      { "table_schema": "iox", "table_name": "risk_assessments" },
                      { "table_schema": "custom", "table_name": "ignored_table" }
                    ]
                    """);
            }

            if (request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath == "/api/v3/configure/table")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v3/configure/table")
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = new HttpClient(handler);
        var coordinator = CreateCoordinator(client);

        var results = await coordinator.ResetAsync(resetId, CancellationToken.None);

        Assert.Collection(
            results,
            rabbit =>
            {
                Assert.Equal("RabbitMQ", rabbit.Store);
                Assert.Equal("Cleared", rabbit.Status);
                Assert.Equal(3, rabbit.Before);
                Assert.Equal(0, rabbit.After);
            },
            influx =>
            {
                Assert.Equal("InfluxDB", influx.Store);
                Assert.Equal("Cleared", influx.Status);
                Assert.Equal(0, influx.After);
                Assert.Contains("hard-deleted 4 runtime table(s)", influx.Message, StringComparison.Ordinal);
                Assert.Contains("recreated 3 schema definition(s)", influx.Message, StringComparison.Ordinal);
            });

        var requests = handler.Requests;
        var firstRabbitPurge = requests.FindIndex(request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri?.AbsolutePath.EndsWith("/contents", StringComparison.Ordinal) == true);
        var firstInfluxDelete = requests.FindIndex(request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri?.AbsolutePath == "/api/v3/configure/table");
        Assert.True(firstRabbitPurge >= 0);
        Assert.True(firstInfluxDelete > firstRabbitPurge);
        Assert.Equal(2, requests.Count(request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri?.AbsolutePath.EndsWith("/contents", StringComparison.Ordinal) == true));
        Assert.Equal(4, requests.Count(request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri?.AbsolutePath == "/api/v3/configure/table"));
        Assert.Equal(3, requests.Count(request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri?.AbsolutePath == "/api/v3/configure/table"));
        Assert.All(requests.Where(request =>
            request.RequestUri?.AbsolutePath == "/api/v3/configure/table"), request =>
            Assert.Equal(resetId.ToString("D"), request.ResetId));
    }

    private static RuntimeDataResetCoordinator CreateCoordinator(
        HttpClient client,
        bool observabilityRawEnabled = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InfluxDb:Enabled"] = "true",
                ["InfluxDb:Url"] = "https://influx.local",
                ["InfluxDb:Token"] = "test-token",
                ["InfluxDb:Bucket"] = "np-test"
            })
            .Build();
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = "rabbitmq.local",
            UserName = "np-app",
            Password = "np-app-pass",
            VirtualHost = "np/test",
            ManagementScheme = "https",
            ManagementHost = "rabbitmq-management.local",
            ManagementPort = 15692,
            ManagementUserName = "np-reset",
            ManagementPassword = "np-reset-pass",
            IngestionReadingsQueueName = "np.ingestion.test",
            ObservabilityRawQueueName = "np.raw.test",
            ObservabilityRawEnabled = observabilityRawEnabled
        };

        return new RuntimeDataResetCoordinator(
            new SingleClientFactory(client),
            Options.Create(rabbitMqOptions),
            configuration,
            NullLogger<RuntimeDataResetCoordinator>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string BuildRabbitQueuesJson(params (string Name, long Messages, long Unacknowledged)[] queues)
        => "[" + string.Join(",", queues.Select(queue =>
            $$"""
            {
              "name": "{{queue.Name}}",
              "messages": {{queue.Messages}},
              "messages_unacknowledged": {{queue.Unacknowledged}}
            }
            """)) + "]";

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization,
                request.Headers.TryGetValues("X-NatureProtector-Reset-Id", out var values)
                    ? values.Single()
                    : null));
            return Task.FromResult(respond(request));
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        AuthenticationHeaderValue? Authorization,
        string? ResetId);
}
