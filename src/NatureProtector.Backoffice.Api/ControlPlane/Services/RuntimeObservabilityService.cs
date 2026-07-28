using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class RuntimeObservabilityService : IRuntimeObservabilityService
{
    private const string RabbitMqSource = "RabbitMQ Management API";
    private readonly IDbContextFactory<NatureProtectorControlDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly RuntimeEvidenceCatalog _evidenceCatalog;

    public RuntimeObservabilityService(
        IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IWebHostEnvironment environment)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _rabbitMqOptions = rabbitMqOptions.Value;
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
            GetGrafanaHealthUri(),
            observedAt,
            expectedJsonProperty: "database",
            cancellationToken));

        var limitations = new List<RuntimeLimitationResponse>
        {
            new("publisher_timestamp_optional", "PublishedAt is persisted for new RabbitMQ-published readings; older rows and non-RabbitMQ publishers may not carry it."),
            new("quality_classifier_projection_unavailable", "Detailed classifier payloads and aggregate quality projections are not persisted by this endpoint.")
        };

        if (components.Any(component => component.Status is RuntimeOperationalHealthStatus.Unknown or RuntimeOperationalHealthStatus.NotInstrumented))
        {
            limitations.Add(new RuntimeLimitationResponse(
                "health_unknown_is_explicit",
                "Missing service signals are represented as Unknown or NotInstrumented, not Healthy."));
        }

        if (components.Any(component => component.Status is RuntimeOperationalHealthStatus.AuthRequired))
        {
            limitations.Add(new RuntimeLimitationResponse(
                "health_auth_required_is_explicit",
                "Authenticated service health endpoints are represented as AuthRequired, not as runtime failure."));
        }

        return new RuntimeOperationalHealthResponse(observedAt, components, rabbitMq, limitations);
    }

    public async Task<RabbitMqMetricsResponse> GetRabbitMqMetricsAsync(CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var options = _rabbitMqOptions;
        var queueDefinitions = options.GetQueueDefinitions().ToArray();

        try
        {
            var managementUri = RabbitMqManagementHttpClient.BuildQueuesUri(options);
            var client = _httpClientFactory.CreateClient(RabbitMqManagementHttpClient.ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, managementUri);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{options.GetEffectiveManagementUserName()}:{options.GetEffectiveManagementPassword()}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return RabbitMqUnavailable(
                    observedAt,
                    queueDefinitions,
                    RuntimeMetricCollectionStatus.Unavailable,
                    $"RabbitMQ Management API returned HTTP {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var byQueue = document.RootElement
                .EnumerateArray()
                .Where(item => item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                .ToDictionary(item => item.GetProperty("name").GetString() ?? string.Empty, StringComparer.Ordinal);

            var metrics = queueDefinitions
                .Select(definition => BuildQueueMetric(definition, byQueue, observedAt))
                .ToArray();
            var enabledMetrics = metrics.Where(metric => metric.Enabled).ToArray();
            var limitations = metrics
                .Where(metric =>
                    !metric.Enabled &&
                    metric.CollectionStatus == RuntimeMetricCollectionStatus.Measured)
                .Select(metric => new RuntimeLimitationResponse(
                    "rabbitmq_disabled_queue_present",
                    $"Queue {metric.QueueName} is disabled by configuration but remains present in RabbitMQ; durable resource or binding cleanup may be pending."))
                .ToArray();

            return new RabbitMqMetricsResponse(
                observedAt,
                RabbitMqSource,
                enabledMetrics.All(metric => metric.CollectionStatus == RuntimeMetricCollectionStatus.Measured)
                    ? RuntimeMetricCollectionStatus.Measured
                    : RuntimeMetricCollectionStatus.Unavailable,
                metrics,
                limitations);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return RabbitMqUnavailable(
                observedAt,
                queueDefinitions,
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
        var blockingQueues = rabbitMq.Queues
            .Where(queue => queue.Enabled && queue.BlocksRuntimeHealth)
            .ToArray();
        if (blockingQueues.Length == 0 ||
            blockingQueues.Any(queue => queue.CollectionStatus != RuntimeMetricCollectionStatus.Measured))
        {
            return Component(
                "RabbitMQ",
                RuntimeOperationalHealthStatus.Unknown,
                observedAt,
                "RabbitMQ Management API metrics were unavailable for at least one enabled blocking runtime queue.",
                RabbitMqSource,
                "RabbitMQ runtime health cannot be inferred without a positive signal for every enabled blocking queue.");
        }

        var hasBlockingUnconsumedBacklog = blockingQueues.Any(queue =>
            queue.MessagesTotal.GetValueOrDefault() > 0 &&
            queue.Consumers.GetValueOrDefault() == 0);

        var auxiliaryUnconsumedBacklogQueues = rabbitMq.Queues
            .Where(queue =>
                queue.Enabled &&
                !queue.BlocksRuntimeHealth &&
                queue.CollectionStatus == RuntimeMetricCollectionStatus.Measured &&
                queue.MessagesTotal.GetValueOrDefault() > 0 &&
                queue.Consumers.GetValueOrDefault() == 0)
            .Select(queue => queue.QueueName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var auxiliaryUnavailableQueues = rabbitMq.Queues
            .Where(queue =>
                queue.Enabled &&
                !queue.BlocksRuntimeHealth &&
                queue.CollectionStatus != RuntimeMetricCollectionStatus.Measured)
            .Select(queue => queue.QueueName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var disabledQueuesStillPresent = rabbitMq.Queues
            .Where(queue =>
                !queue.Enabled &&
                queue.CollectionStatus == RuntimeMetricCollectionStatus.Measured)
            .Select(queue => queue.QueueName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return Component(
            "RabbitMQ",
            hasBlockingUnconsumedBacklog ? RuntimeOperationalHealthStatus.Degraded : RuntimeOperationalHealthStatus.Healthy,
            observedAt,
            hasBlockingUnconsumedBacklog
                ? "At least one enabled blocking runtime queue has measured messages and no measured consumers."
                : "Enabled blocking runtime queues were measured by the management API.",
            RabbitMqSource,
            hasBlockingUnconsumedBacklog
                ? "Broker is reachable, but blocking queue consumption is degraded."
                : BuildRabbitMqAdvisoryLimitation(
                    auxiliaryUnconsumedBacklogQueues,
                    auxiliaryUnavailableQueues,
                    disabledQueuesStillPresent));
    }

    private RuntimeOperationalHealthComponentResponse BuildPreventionHealth(
        RabbitMqMetricsResponse rabbitMq,
        DateTimeOffset observedAt)
    {
        var ingestion = rabbitMq.Queues.SingleOrDefault(queue =>
            queue.Enabled &&
            queue.ConsumerRequired &&
            string.Equals(queue.QueueRole, RabbitMqQueueRoles.PrimaryWorkQueue, StringComparison.Ordinal));
        if (ingestion is null || ingestion.CollectionStatus != RuntimeMetricCollectionStatus.Measured)
        {
            return Component(
                "Prevention.Host",
                RuntimeOperationalHealthStatus.Unknown,
                observedAt,
                "No positive consumer signal was available for the enabled primary work queue.",
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
                ? "At least one consumer is attached to the primary work queue."
                : "The primary work queue was measured with zero consumers.",
            $"{RabbitMqSource}: {ingestion.QueueName}",
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
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Component(
                    component,
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? RuntimeOperationalHealthStatus.AuthRequired
                        : RuntimeOperationalHealthStatus.Degraded,
                    observedAt,
                    $"{uri} returned HTTP {(int)response.StatusCode}.",
                    uri.ToString(),
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "Health endpoint is reachable but requires authentication; no unauthenticated healthy signal is claimed."
                        : "Health endpoint did not provide a positive healthy signal.");
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
        IReadOnlyList<RabbitMqQueueDefinition> queueDefinitions,
        string status,
        string reason)
        => new(
            observedAt,
            RabbitMqSource,
            status,
            queueDefinitions.Select(definition => new RabbitMqQueueMetricResponse(
                definition.QueueName,
                definition.QueueRole,
                definition.Enabled,
                definition.ConsumerRequired,
                definition.BlocksRuntimeHealth,
                null,
                null,
                null,
                null,
                observedAt,
                RabbitMqSource,
                definition.Enabled ? status : RuntimeMetricCollectionStatus.NotApplicable,
                definition.Enabled ? reason : "Queue is disabled by configuration; no broker metric is required."))
                .ToArray(),
            [new RuntimeLimitationResponse("rabbitmq_metrics_unavailable", reason)]);

    private static RabbitMqQueueMetricResponse BuildQueueMetric(
        RabbitMqQueueDefinition definition,
        IReadOnlyDictionary<string, JsonElement> byQueue,
        DateTimeOffset observedAt)
    {
        if (!byQueue.TryGetValue(definition.QueueName, out var queue))
        {
            return new RabbitMqQueueMetricResponse(
                definition.QueueName,
                definition.QueueRole,
                definition.Enabled,
                definition.ConsumerRequired,
                definition.BlocksRuntimeHealth,
                null,
                null,
                null,
                null,
                observedAt,
                RabbitMqSource,
                definition.Enabled
                    ? RuntimeMetricCollectionStatus.Unavailable
                    : RuntimeMetricCollectionStatus.NotApplicable,
                definition.Enabled
                    ? "Enabled queue was not returned by RabbitMQ Management API."
                    : "Queue is disabled by configuration.");
        }

        return new RabbitMqQueueMetricResponse(
            definition.QueueName,
            definition.QueueRole,
            definition.Enabled,
            definition.ConsumerRequired,
            definition.BlocksRuntimeHealth,
            TryGetInt(queue, "messages_ready"),
            TryGetInt(queue, "messages_unacknowledged"),
            TryGetInt(queue, "messages"),
            TryGetInt(queue, "consumers"),
            observedAt,
            RabbitMqSource,
            RuntimeMetricCollectionStatus.Measured,
            definition.Enabled
                ? null
                : "Queue is disabled by configuration but remains present in RabbitMQ; durable resource or binding cleanup may be pending.");
    }

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

    private static string? BuildRabbitMqAdvisoryLimitation(
        IReadOnlyList<string> auxiliaryUnconsumedBacklogQueues,
        IReadOnlyList<string> auxiliaryUnavailableQueues,
        IReadOnlyList<string> disabledQueuesStillPresent)
    {
        var limitations = new List<string>();
        if (auxiliaryUnconsumedBacklogQueues.Count > 0)
        {
            limitations.Add(
                "Enabled auxiliary diagnostic queues have measured messages and no consumers: " +
                string.Join(", ", auxiliaryUnconsumedBacklogQueues) +
                ".");
        }

        if (auxiliaryUnavailableQueues.Count > 0)
        {
            limitations.Add(
                "Enabled auxiliary diagnostic queue metrics are unavailable: " +
                string.Join(", ", auxiliaryUnavailableQueues) +
                ". Blocking runtime health remains based on primary work queues.");
        }

        if (disabledQueuesStillPresent.Count > 0)
        {
            limitations.Add(
                "Queues disabled by configuration remain present in RabbitMQ: " +
                string.Join(", ", disabledQueuesStillPresent) +
                ". Durable topology cleanup may be pending.");
        }

        return limitations.Count == 0 ? null : string.Join(" ", limitations);
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

    private Uri GetGrafanaHealthUri()
    {
        var configured = _configuration["Grafana:Url"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return GetConfiguredUri("Grafana:Url", configured);
        }

        var configuredPort = _configuration["GRAFANA_PORT"];
        if (string.IsNullOrWhiteSpace(configuredPort))
        {
            configuredPort = _configuration["NP_ACCEPTANCE_GRAFANA_PORT"];
        }

        return int.TryParse(configuredPort, out var port) && port is > 0 and <= 65535
            ? new Uri($"http://localhost:{port}/api/health")
            : new Uri("http://localhost:3000/api/health");
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
