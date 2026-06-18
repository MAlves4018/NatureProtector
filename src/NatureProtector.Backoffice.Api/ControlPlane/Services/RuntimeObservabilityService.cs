using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class RuntimeObservabilityService : IRuntimeObservabilityService
{
    private const string RabbitMqSource = "RabbitMQ Management HTTP API";
    private readonly IDbContextFactory<NatureProtectorControlDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RuntimeEvidenceCatalog _evidenceCatalog;

    public RuntimeObservabilityService(
        IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _evidenceCatalog = new RuntimeEvidenceCatalog(ResolveRepositoryRoot(environment.ContentRootPath));
    }

    public bool IsAvailable => true;

    public string AvailabilityMessage => "Runtime observability endpoints are available.";

    public async Task<RuntimeOperationalHealthResponse> GetOperationalHealthAsync(CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var components = new List<RuntimeOperationalHealthComponentResponse>
        {
            Component("Backoffice.Api", RuntimeOperationalHealthStatus.Healthy, observedAt, "HTTP request reached authenticated controller.", "current request", null)
        };

        components.Add(await CheckPostgresAsync(observedAt, cancellationToken));
        var rabbitMq = await GetRabbitMqMetricsAsync(cancellationToken);
        components.Add(BuildRabbitMqHealth(rabbitMq, observedAt));
        components.Add(BuildPreventionHealth(rabbitMq, observedAt));
        components.Add(await BuildSimulatorHealthAsync(observedAt, cancellationToken));
        components.Add(await CheckHttpHealthAsync(
            "InfluxDB",
            GetConfiguredUri("InfluxDb:Url", "http://localhost:8181/health"),
            observedAt,
            expectedJsonProperty: null,
            cancellationToken));
        components.Add(await CheckHttpHealthAsync(
            "Grafana",
            GetConfiguredUri("Grafana:Url", "http://localhost:3000/api/health"),
            observedAt,
            expectedJsonProperty: "database",
            cancellationToken));

        var limitations = new List<RuntimeLimitationResponse>
        {
            new("publisher_timestamp_hard_gate", "EventEnvelope has EventTime and optional IngestTime, but no persisted PublishedAt timestamp. End-to-end publish latency is not claimed."),
            new("quality_classifier_projection_unavailable", "Detailed classifier payloads and aggregate quality projections are not persisted by this endpoint.")
        };

        if (components.Any(component => component.Status is RuntimeOperationalHealthStatus.Unknown or RuntimeOperationalHealthStatus.NotInstrumented))
        {
            limitations.Add(new RuntimeLimitationResponse(
                "health_unknown_is_explicit",
                "Missing service signals are represented as Unknown or NotInstrumented, not Healthy."));
        }

        return new RuntimeOperationalHealthResponse(observedAt, components, rabbitMq, limitations);
    }

    public async Task<RabbitMqMetricsResponse> GetRabbitMqMetricsAsync(CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var queues = NatureProtectorRabbitMqTopology.Bindings
            .Select(binding => binding.QueueName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        try
        {
            var options = GetRabbitMqOptions();
            var managementUri = GetRabbitMqManagementUri(options);
            var client = _httpClientFactory.CreateClient(nameof(RuntimeObservabilityService));
            using var request = new HttpRequestMessage(HttpMethod.Get, managementUri);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.UserName}:{options.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return RabbitMqUnavailable(
                    observedAt,
                    queues,
                    RuntimeMetricCollectionStatus.Unavailable,
                    $"RabbitMQ Management API returned HTTP {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var byQueue = document.RootElement
                .EnumerateArray()
                .Where(item => item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                .ToDictionary(item => item.GetProperty("name").GetString() ?? string.Empty, StringComparer.Ordinal);

            var metrics = queues.Select(queueName =>
            {
                if (!byQueue.TryGetValue(queueName, out var queue))
                {
                    return new RabbitMqQueueMetricResponse(
                        queueName,
                        null,
                        null,
                        null,
                        null,
                        observedAt,
                        RabbitMqSource,
                        RuntimeMetricCollectionStatus.Unavailable,
                        "Queue was not returned by RabbitMQ Management API.");
                }

                return new RabbitMqQueueMetricResponse(
                    queueName,
                    TryGetInt(queue, "messages_ready"),
                    TryGetInt(queue, "messages_unacknowledged"),
                    TryGetInt(queue, "messages"),
                    TryGetInt(queue, "consumers"),
                    observedAt,
                    RabbitMqSource,
                    RuntimeMetricCollectionStatus.Measured,
                    null);
            }).ToArray();

            return new RabbitMqMetricsResponse(
                observedAt,
                RabbitMqSource,
                metrics.All(metric => metric.CollectionStatus == RuntimeMetricCollectionStatus.Measured)
                    ? RuntimeMetricCollectionStatus.Measured
                    : RuntimeMetricCollectionStatus.Unavailable,
                metrics,
                []);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return RabbitMqUnavailable(
                observedAt,
                queues,
                RuntimeMetricCollectionStatus.Error,
                $"RabbitMQ metrics collection failed: {exception.GetType().Name}.");
        }
    }

    public Task<RuntimeEvidenceCatalogResponse> ListEvidenceAsync(CancellationToken cancellationToken)
        => Task.FromResult(_evidenceCatalog.List(DateTimeOffset.UtcNow));

    public Task<RuntimeEvidenceContentResponse?> GetEvidenceContentAsync(string evidenceId, CancellationToken cancellationToken)
        => _evidenceCatalog.GetContentAsync(evidenceId, DateTimeOffset.UtcNow, cancellationToken);

    private async Task<RuntimeOperationalHealthComponentResponse> CheckPostgresAsync(
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return Component(
                "PostgreSQL",
                canConnect ? RuntimeOperationalHealthStatus.Healthy : RuntimeOperationalHealthStatus.Unhealthy,
                observedAt,
                canConnect ? "Database.CanConnectAsync succeeded." : "Database.CanConnectAsync returned false.",
                "EF Core database connectivity probe",
                canConnect ? null : "PostgreSQL connectivity is unavailable.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateException)
        {
            return Component(
                "PostgreSQL",
                RuntimeOperationalHealthStatus.Unhealthy,
                observedAt,
                $"PostgreSQL health probe failed: {exception.GetType().Name}.",
                "EF Core database connectivity probe",
                "PostgreSQL connectivity probe failed.");
        }
    }

    private RuntimeOperationalHealthComponentResponse BuildRabbitMqHealth(
        RabbitMqMetricsResponse rabbitMq,
        DateTimeOffset observedAt)
    {
        if (rabbitMq.CollectionStatus != RuntimeMetricCollectionStatus.Measured)
        {
            return Component(
                "RabbitMQ",
                RuntimeOperationalHealthStatus.Unknown,
                observedAt,
                "RabbitMQ Management API metrics were unavailable.",
                RabbitMqSource,
                "RabbitMQ broker health cannot be inferred without a positive management API signal.");
        }

        var hasUnconsumedBacklog = rabbitMq.Queues.Any(queue =>
            queue.MessagesTotal.GetValueOrDefault() > 0 &&
            queue.Consumers.GetValueOrDefault() == 0);
        return Component(
            "RabbitMQ",
            hasUnconsumedBacklog ? RuntimeOperationalHealthStatus.Degraded : RuntimeOperationalHealthStatus.Healthy,
            observedAt,
            hasUnconsumedBacklog
                ? "At least one relevant queue has measured messages and no measured consumers."
                : "Relevant queues were measured by the management API.",
            RabbitMqSource,
            hasUnconsumedBacklog ? "Broker is reachable, but queue consumption is degraded." : null);
    }

    private RuntimeOperationalHealthComponentResponse BuildPreventionHealth(
        RabbitMqMetricsResponse rabbitMq,
        DateTimeOffset observedAt)
    {
        var ingestion = rabbitMq.Queues.SingleOrDefault(queue =>
            string.Equals(queue.QueueName, NatureProtectorRabbitMqTopology.IngestionReadingsQueue, StringComparison.Ordinal));
        if (ingestion is null || ingestion.CollectionStatus != RuntimeMetricCollectionStatus.Measured)
        {
            return Component(
                "Prevention.Host",
                RuntimeOperationalHealthStatus.Unknown,
                observedAt,
                "No positive consumer signal was available for the ingestion queue.",
                RabbitMqSource,
                "Prevention.Host health is a RabbitMQ consumer proxy until a dedicated heartbeat exists.");
        }

        return Component(
            "Prevention.Host",
            ingestion.Consumers.GetValueOrDefault() > 0
                ? RuntimeOperationalHealthStatus.Healthy
                : RuntimeOperationalHealthStatus.Degraded,
            observedAt,
            ingestion.Consumers.GetValueOrDefault() > 0
                ? "At least one consumer is attached to the ingestion queue."
                : "The ingestion queue was measured with zero consumers.",
            $"{RabbitMqSource}: {NatureProtectorRabbitMqTopology.IngestionReadingsQueue}",
            ingestion.Consumers.GetValueOrDefault() > 0 ? null : "Expected prevention consumer was not observed.");
    }

    private async Task<RuntimeOperationalHealthComponentResponse> BuildSimulatorHealthAsync(
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runs = await dbContext.SimulationRuns
            .AsNoTracking()
            .Select(entity => new
            {
                entity.Status,
                entity.CreatedAt,
                entity.StartedAt,
                entity.EndedAt
            })
            .ToListAsync(cancellationToken);
        var latestRun = runs.OrderByDescending(entity => entity.CreatedAt).FirstOrDefault();
        if (latestRun is null)
        {
            return Component(
                "Simulator.Host",
                RuntimeOperationalHealthStatus.NotApplicable,
                observedAt,
                "No simulation run exists; simulator runtime is not expected.",
                "control.simulation_runs",
                null);
        }

        var status = latestRun.Status.ToString();
        var healthStatus = status switch
        {
            "Running" or "Starting" => RuntimeOperationalHealthStatus.Healthy,
            "Failed" => RuntimeOperationalHealthStatus.Degraded,
            "Completed" => RuntimeOperationalHealthStatus.NotApplicable,
            _ => RuntimeOperationalHealthStatus.Unknown
        };

        return Component(
            "Simulator.Host",
            healthStatus,
            observedAt,
            healthStatus == RuntimeOperationalHealthStatus.NotApplicable
                ? "Latest simulation run is completed; simulator is not expected to remain active."
                : $"Latest simulation run status is {status}.",
            "control.simulation_runs latest row",
            healthStatus == RuntimeOperationalHealthStatus.Unknown ? "Simulator lifecycle status is not mapped to operational health." : null,
            latestRun.EndedAt ?? latestRun.StartedAt ?? latestRun.CreatedAt);
    }

    private async Task<RuntimeOperationalHealthComponentResponse> CheckHttpHealthAsync(
        string component,
        Uri uri,
        DateTimeOffset observedAt,
        string? expectedJsonProperty,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(RuntimeObservabilityService));
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Component(
                    component,
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? RuntimeOperationalHealthStatus.Unknown
                        : RuntimeOperationalHealthStatus.Degraded,
                    observedAt,
                    $"{uri} returned HTTP {(int)response.StatusCode}.",
                    uri.ToString(),
                    "Health endpoint did not provide an authenticated positive healthy signal.");
            }

            if (!string.IsNullOrWhiteSpace(expectedJsonProperty))
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
                if (!document.RootElement.TryGetProperty(expectedJsonProperty, out var property) ||
                    !string.Equals(property.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return Component(
                        component,
                        RuntimeOperationalHealthStatus.Degraded,
                        observedAt,
                        $"{uri} responded but did not report {expectedJsonProperty}=ok.",
                        uri.ToString(),
                        "Health response did not match expected readiness field.");
                }
            }

            return Component(component, RuntimeOperationalHealthStatus.Healthy, observedAt, $"{uri} returned a positive health response.", uri.ToString(), null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Component(
                component,
                RuntimeOperationalHealthStatus.Unknown,
                observedAt,
                $"Health endpoint probe failed: {exception.GetType().Name}.",
                uri.ToString(),
                "Service health endpoint was not reachable from Backoffice.Api.");
        }
    }

    private RabbitMqMetricsResponse RabbitMqUnavailable(
        DateTimeOffset observedAt,
        IReadOnlyList<string> queues,
        string status,
        string reason)
        => new(
            observedAt,
            RabbitMqSource,
            status,
            queues.Select(queue => new RabbitMqQueueMetricResponse(
                queue,
                null,
                null,
                null,
                null,
                observedAt,
                RabbitMqSource,
                status,
                reason)).ToArray(),
            [new RuntimeLimitationResponse("rabbitmq_metrics_unavailable", reason)]);

    private RuntimeOperationalHealthComponentResponse Component(
        string name,
        string status,
        DateTimeOffset observedAt,
        string reason,
        string source,
        string? limitation,
        DateTimeOffset? positiveTimestamp = null)
        => new(
            name,
            status,
            observedAt,
            source,
            reason,
            status is RuntimeOperationalHealthStatus.Healthy or RuntimeOperationalHealthStatus.NotApplicable
                ? positiveTimestamp ?? observedAt
                : null,
            status is RuntimeOperationalHealthStatus.Unhealthy or RuntimeOperationalHealthStatus.Degraded
                ? observedAt
                : null,
            positiveTimestamp.HasValue ? Math.Max(0, (observedAt - positiveTimestamp.Value).TotalSeconds) : null,
            "runtime-observability",
            limitation);

    private RabbitMqOptions GetRabbitMqOptions()
        => _configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

    private Uri GetRabbitMqManagementUri(RabbitMqOptions options)
    {
        var managementPort = _configuration.GetValue<int?>("RabbitMq:ManagementPort") ?? 15672;
        var hostName = string.IsNullOrWhiteSpace(options.HostName) ? "localhost" : options.HostName;
        return new Uri($"http://{hostName}:{managementPort}/api/queues");
    }

    private Uri GetConfiguredUri(string key, string fallback)
    {
        var configured = _configuration[key];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new Uri(fallback);
        }

        var uri = configured.EndsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                  configured.EndsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            ? configured
            : configured.TrimEnd('/') + (key.StartsWith("Grafana", StringComparison.Ordinal) ? "/api/health" : "/health");
        return new Uri(uri);
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : null;

    private static string ResolveRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "evidence")) ||
                File.Exists(Path.Combine(directory.FullName, "NatureProtector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startPath);
    }
}
