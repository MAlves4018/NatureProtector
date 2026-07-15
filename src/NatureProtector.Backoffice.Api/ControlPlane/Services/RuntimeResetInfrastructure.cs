using System.Data.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

/// <summary>
/// Coordinates the non-relational stores that participate in a development runtime reset.
/// PostgreSQL deletion remains owned by <see cref="PostgresControlPlaneService"/> so it can
/// be protected by the same admission lock and transaction as the control-plane checks.
/// </summary>
public interface IRuntimeDataResetCoordinator
{
    Task<IReadOnlyList<RuntimeResetStoreResponse>> InspectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RuntimeResetStoreResponse>> ResetAsync(
        Guid resetId,
        CancellationToken cancellationToken);
}

public sealed class DatabaseOnlyRuntimeDataResetCoordinator : IRuntimeDataResetCoordinator
{
    public static DatabaseOnlyRuntimeDataResetCoordinator Instance { get; } = new();

    private DatabaseOnlyRuntimeDataResetCoordinator()
    {
    }

    public Task<IReadOnlyList<RuntimeResetStoreResponse>> InspectAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RuntimeResetStoreResponse>>(
        [
            new("RabbitMQ", "Unavailable", null, null,
                "No RabbitMQ reset coordinator was supplied to this service instance."),
            new("InfluxDB", "Unavailable", null, null,
                "No InfluxDB reset coordinator was supplied to this service instance.")
        ]);

    public Task<IReadOnlyList<RuntimeResetStoreResponse>> ResetAsync(
        Guid resetId,
        CancellationToken cancellationToken)
        => InspectAsync(cancellationToken);
}

/// <summary>
/// Development-only external store reset implementation. RabbitMQ is purged through the
/// management API after proving there are no unacknowledged deliveries. InfluxDB 3 is
/// health-checked and cleared through table hard-delete plus schema recreation APIs. Failures are returned
/// explicitly and never converted
/// into a successful PostgreSQL-only reset when external stores were required by the caller.
/// </summary>
public sealed class RuntimeDataResetCoordinator : IRuntimeDataResetCoordinator
{
    private const string InfluxSectionName = "InfluxDb";
    private static readonly InfluxRuntimeTableSchema[] InfluxRuntimeTables =
    [
        new(
            "accepted_readings",
            [
                "event_id",
                "simulation_run_id",
                "area_id",
                "sensor_id",
                "sensor_name",
                "metric_type",
                "unit",
                "operational_state"
            ],
            [
                new("value", "float64"),
                new("latitude", "float64"),
                new("longitude", "float64")
            ]),
        new(
            "risk_assessments",
            [
                "area_id",
                "sensor_id",
                "risk_level",
                "event_id",
                "simulation_run_id"
            ],
            [
                new("risk_score", "float64"),
                new("has_explanation", "int64")
            ]),
        new(
            "area_risk_snapshots",
            [
                "area_id",
                "aggregate_risk_level",
                "severity",
                "event_id",
                "simulation_run_id"
            ],
            [
                new("aggregate_risk_score", "float64"),
                new("assessment_count", "int64")
            ])
    ];
    private static readonly JsonSerializerOptions CamelCaseJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RuntimeDataResetCoordinator> _logger;

    public RuntimeDataResetCoordinator(
        IHttpClientFactory httpClientFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IConfiguration configuration,
        ILogger<RuntimeDataResetCoordinator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RuntimeResetStoreResponse>> InspectAsync(
        CancellationToken cancellationToken)
    {
        var rabbit = await InspectRabbitMqAsync(cancellationToken).ConfigureAwait(false);
        var influx = await InspectInfluxAsync(cancellationToken).ConfigureAwait(false);
        return [rabbit, influx];
    }

    public async Task<IReadOnlyList<RuntimeResetStoreResponse>> ResetAsync(
        Guid resetId,
        CancellationToken cancellationToken)
    {
        var rabbitBefore = await InspectRabbitMqAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(rabbitBefore.Status, "Ready", StringComparison.Ordinal))
        {
            return
            [
                rabbitBefore with { Message = $"Reset {resetId:D} did not purge RabbitMQ: {rabbitBefore.Message}" },
                (await InspectInfluxAsync(cancellationToken).ConfigureAwait(false)) with
                {
                    Status = "NotAttempted",
                    Message = "InfluxDB was not modified because RabbitMQ was not quiescent."
                }
            ];
        }

        var influxBefore = await InspectInfluxAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(influxBefore.Status, "Ready", StringComparison.Ordinal))
        {
            return
            [
                rabbitBefore with
                {
                    Status = "NotAttempted",
                    Message = "RabbitMQ was not modified because InfluxDB was not ready."
                },
                influxBefore
            ];
        }

        var rabbitResult = await PurgeRabbitMqAsync(rabbitBefore, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(rabbitResult.Status, "Cleared", StringComparison.Ordinal))
        {
            return
            [
                rabbitResult,
                (await InspectInfluxAsync(cancellationToken).ConfigureAwait(false)) with
                {
                    Status = "NotAttempted",
                    Message = "InfluxDB was not modified because RabbitMQ purge verification failed."
                }
            ];
        }

        var influxResult = await DeleteInfluxAsync(resetId, cancellationToken).ConfigureAwait(false);
        return [rabbitResult, influxResult];
    }

    private async Task<RuntimeResetStoreResponse> InspectRabbitMqAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                RabbitMqManagementHttpClient.BuildQueuesUri(_rabbitMqOptions));
            AddRabbitMqAuthorization(request);

            var client = _httpClientFactory.CreateClient(RabbitMqManagementHttpClient.ClientName);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new RuntimeResetStoreResponse(
                    "RabbitMQ",
                    "Failed",
                    null,
                    null,
                    $"Management API returned HTTP {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(payload);
            var enabledQueues = _rabbitMqOptions.GetEnabledQueueDefinitions()
                .Select(queue => queue.QueueName)
                .ToHashSet(StringComparer.Ordinal);
            long totalMessages = 0;
            long unacknowledged = 0;
            var seenQueues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var queue in document.RootElement.EnumerateArray())
            {
                if (!queue.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var name = nameElement.GetString();
                if (name is null || !enabledQueues.Contains(name))
                {
                    continue;
                }

                seenQueues.Add(name);
                totalMessages += ReadLong(queue, "messages");
                unacknowledged += ReadLong(queue, "messages_unacknowledged");
            }

            var missingQueues = enabledQueues.Except(seenQueues, StringComparer.Ordinal).ToArray();
            if (missingQueues.Length > 0)
            {
                return new RuntimeResetStoreResponse(
                    "RabbitMQ",
                    "Failed",
                    totalMessages,
                    totalMessages,
                    $"Management API did not expose required queue(s): {string.Join(", ", missingQueues)}.");
            }

            var status = unacknowledged == 0 ? "Ready" : "Busy";
            var message = unacknowledged == 0
                ? $"Enabled queues contain {totalMessages} message(s) and no unacknowledged deliveries."
                : $"Reset is unsafe while {unacknowledged} delivery/deliveries are unacknowledged.";
            return new RuntimeResetStoreResponse("RabbitMQ", status, totalMessages, totalMessages, message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "RabbitMQ reset inspection failed.");
            return new RuntimeResetStoreResponse(
                "RabbitMQ",
                "Failed",
                null,
                null,
                "RabbitMQ management inspection failed; see server logs for the redacted exception.");
        }
    }

    private async Task<RuntimeResetStoreResponse> PurgeRabbitMqAsync(
        RuntimeResetStoreResponse before,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(RabbitMqManagementHttpClient.ClientName);
            foreach (var queue in _rabbitMqOptions.GetEnabledQueueDefinitions())
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    BuildRabbitMqQueueContentsUri(queue.QueueName));
                AddRabbitMqAuthorization(request);
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    return new RuntimeResetStoreResponse(
                        "RabbitMQ",
                        "Failed",
                        before.Before,
                        null,
                        $"Queue '{queue.QueueName}' purge returned HTTP {(int)response.StatusCode}.");
                }
            }

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var verified = await InspectRabbitMqAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(verified.Status, "Ready", StringComparison.Ordinal) && verified.Before == 0)
                {
                    return new RuntimeResetStoreResponse(
                        "RabbitMQ",
                        "Cleared",
                        before.Before,
                        0,
                        "All enabled queues were purged and verified empty.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            }

            return new RuntimeResetStoreResponse(
                "RabbitMQ",
                "Failed",
                before.Before,
                null,
                "Queue purge completed but the enabled queues did not converge to zero messages.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "RabbitMQ purge failed.");
            return new RuntimeResetStoreResponse(
                "RabbitMQ",
                "Failed",
                before.Before,
                null,
                "RabbitMQ purge failed; see server logs for the redacted exception.");
        }
    }

    private async Task<RuntimeResetStoreResponse> InspectInfluxAsync(
        CancellationToken cancellationToken)
    {
        var section = _configuration.GetSection(InfluxSectionName);
        var enabled = section.GetValue<bool?>("Enabled") ?? true;
        if (!enabled)
        {
            return new RuntimeResetStoreResponse(
                "InfluxDB",
                "Unavailable",
                null,
                null,
                "InfluxDB persistence is disabled, so a systemic reset cannot prove time-series cleanup.");
        }

        var url = section["Url"];
        var token = section["Token"];
        var database = section["Bucket"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(database))
        {
            return new RuntimeResetStoreResponse(
                "InfluxDB",
                "Unavailable",
                null,
                null,
                "InfluxDB data-only delete API configuration is incomplete.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/health");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new RuntimeResetStoreResponse(
                    "InfluxDB",
                    "Failed",
                    null,
                    null,
                    $"InfluxDB health endpoint returned HTTP {(int)response.StatusCode}.");
            }

            return new RuntimeResetStoreResponse(
                "InfluxDB",
                "Ready",
                null,
                null,
                $"InfluxDB database '{database}' is configured and the authenticated health endpoint is reachable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "InfluxDB reset inspection failed.");
            return new RuntimeResetStoreResponse(
                "InfluxDB",
                "Failed",
                null,
                null,
                "InfluxDB health inspection failed; see server logs for the redacted exception.");
        }
    }

    private async Task<RuntimeResetStoreResponse> DeleteInfluxAsync(
        Guid resetId,
        CancellationToken cancellationToken)
    {
        var ready = await InspectInfluxAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(ready.Status, "Ready", StringComparison.Ordinal))
        {
            return ready;
        }

        var section = _configuration.GetSection(InfluxSectionName);
        var baseUrl = section["Url"]!.TrimEnd('/');
        var bucket = section["Bucket"]!;
        var token = section["Token"]!;

        try
        {
            var client = _httpClientFactory.CreateClient();
            var deletedTables = 0;
            var recreatedTables = 0;
            var existingTables = await ListInfluxTablesAsync(client, baseUrl, bucket, token, cancellationToken).ConfigureAwait(false);
            var runtimeTableNames = InfluxRuntimeTables
                .SelectMany(schema => existingTables
                    .Where(table => IsInfluxRuntimeTableName(table, schema.Table))
                    .DefaultIfEmpty(schema.Table))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var table in runtimeTableNames)
            {
                var deleteUri = $"{baseUrl}/api/v3/configure/table?db={Uri.EscapeDataString(bucket)}&table={Uri.EscapeDataString(table)}&hard_delete=now";
                using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteUri);
                deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                deleteRequest.Headers.Add("X-NatureProtector-Reset-Id", resetId.ToString("D"));

                using var deleteResponse = await client.SendAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
                if (!deleteResponse.IsSuccessStatusCode &&
                    deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound &&
                    deleteResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
                {
                    return new RuntimeResetStoreResponse(
                        "InfluxDB",
                        "Failed",
                        null,
                        null,
                        $"InfluxDB table hard-delete for '{table}' returned HTTP {(int)deleteResponse.StatusCode}.");
                }

                if (deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    deletedTables++;
                }
            }

            foreach (var schema in InfluxRuntimeTables)
            {
                var createPayload = new
                {
                    db = bucket,
                    table = schema.Table,
                    tags = schema.Tags,
                    fields = schema.Fields.Select(field => new { name = field.Name, type = field.Type }).ToArray()
                };
                var payloadJson = JsonSerializer.Serialize(createPayload, CamelCaseJsonSerializerOptions);
                using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v3/configure/table");
                createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                createRequest.Headers.Add("X-NatureProtector-Reset-Id", resetId.ToString("D"));
                createRequest.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                using var createResponse = await client.SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
                if (!createResponse.IsSuccessStatusCode && createResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
                {
                    return new RuntimeResetStoreResponse(
                        "InfluxDB",
                        "Failed",
                        null,
                        null,
                        $"InfluxDB schema recreation for '{schema.Table}' returned HTTP {(int)createResponse.StatusCode}.");
                }

                if (createResponse.IsSuccessStatusCode || createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    recreatedTables++;
                }
            }

            return new RuntimeResetStoreResponse(
                "InfluxDB",
                "Cleared",
                null,
                0,
                $"InfluxDB 3 hard-deleted {deletedTables} runtime table(s) and recreated {recreatedTables} schema definition(s), preserving the database and authentication resources.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "InfluxDB reset failed.");
            return new RuntimeResetStoreResponse(
                "InfluxDB",
                "Failed",
                null,
                null,
                "InfluxDB delete failed; see server logs for the redacted exception.");
        }
    }

    private void AddRabbitMqAuthorization(HttpRequestMessage request)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{_rabbitMqOptions.GetEffectiveManagementUserName()}:{_rabbitMqOptions.GetEffectiveManagementPassword()}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private Uri BuildRabbitMqQueueContentsUri(string queueName)
    {
        var scheme = _rabbitMqOptions.ManagementScheme.Trim().ToLowerInvariant();
        var host = _rabbitMqOptions.GetEffectiveManagementHost();
        var vhost = Uri.EscapeDataString(_rabbitMqOptions.VirtualHost);
        var queue = Uri.EscapeDataString(queueName);
        return new Uri($"{scheme}://{host}:{_rabbitMqOptions.ManagementPort}/api/queues/{vhost}/{queue}/contents");
    }

    private static long ReadLong(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var parsed)
            ? parsed
            : 0;

    private static bool IsInfluxRuntimeTableName(string tableName, string canonicalTable)
        => string.Equals(tableName, canonicalTable, StringComparison.Ordinal) ||
            tableName.StartsWith(canonicalTable + "-", StringComparison.Ordinal);

    private static async Task<IReadOnlyList<string>> ListInfluxTablesAsync(
        HttpClient client,
        string baseUrl,
        string bucket,
        string token,
        CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString("SHOW TABLES");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/api/v3/query_sql?db={Uri.EscapeDataString(bucket)}&q={query}&format=json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var tables = new List<string>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (row.TryGetProperty("table_schema", out var schema) &&
                !string.Equals(schema.GetString(), "iox", StringComparison.Ordinal))
            {
                continue;
            }

            if (row.TryGetProperty("table_name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
            {
                tables.Add(name.GetString()!);
            }
        }

        return tables;
    }

    private sealed record InfluxRuntimeTableSchema(
        string Table,
        string[] Tags,
        InfluxRuntimeField[] Fields);

    private sealed record InfluxRuntimeField(string Name, string Type);
}

/// <summary>Serializes runtime lifecycle state transitions across API replicas.</summary>
internal sealed class RuntimeOperationStateLock : IAsyncDisposable
{
    private const long AdvisoryLockKey = 5638591602766115925L;
    private static readonly SemaphoreSlim FallbackLock = new(1, 1);
    private readonly DbConnection? _connection;
    private readonly bool _opened;
    private readonly bool _fallback;

    private RuntimeOperationStateLock(DbConnection? connection, bool opened, bool fallback)
        => (_connection, _opened, _fallback) = (connection, opened, fallback);

    public static async Task<RuntimeOperationStateLock> AcquireAsync(NatureProtectorControlDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            await FallbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new RuntimeOperationStateLock(null, false, true);
        }
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "SELECT pg_advisory_lock(@key);", cancellationToken).ConfigureAwait(false);
        return new RuntimeOperationStateLock(connection, opened, false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fallback) { FallbackLock.Release(); return; }
        if (_connection is null) return;
        try { await ExecuteAsync(_connection, "SELECT pg_advisory_unlock(@key);", CancellationToken.None).ConfigureAwait(false); }
        finally { if (_opened) await _connection.CloseAsync().ConfigureAwait(false); }
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = AdvisoryLockKey; command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Serializes API runtime admission and reset across all Backoffice instances. PostgreSQL
/// uses a session advisory lock; non-PostgreSQL test providers use a process-local semaphore.
/// </summary>
internal sealed class RuntimeMaintenanceLock : IAsyncDisposable
{
    private const long AdvisoryLockKey = 5638591602766115924L;
    private static readonly SemaphoreSlim FallbackLock = new(1, 1);
    private readonly DbConnection? _connection;
    private readonly bool _openedConnection;
    private readonly bool _fallback;

    private RuntimeMaintenanceLock(DbConnection? connection, bool openedConnection, bool fallback)
    {
        _connection = connection;
        _openedConnection = openedConnection;
        _fallback = fallback;
    }

    public static async Task<RuntimeMaintenanceLock> AcquireAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            await FallbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new RuntimeMaintenanceLock(null, false, true);
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedConnection = connection.State != System.Data.ConnectionState.Open;
        if (openedConnection)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAdvisoryCommandAsync(
            connection,
            "SELECT pg_advisory_lock(@key);",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeMaintenanceLock(connection, openedConnection, false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fallback)
        {
            FallbackLock.Release();
            return;
        }

        if (_connection is null)
        {
            return;
        }

        try
        {
            await ExecuteAdvisoryCommandAsync(
                _connection,
                "SELECT pg_advisory_unlock(@key);",
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (_openedConnection)
            {
                await _connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task ExecuteAdvisoryCommandAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = AdvisoryLockKey;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
