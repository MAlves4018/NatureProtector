using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

internal sealed record CloudRunOperationSnapshot(
    string OperationName,
    string? ExecutionName,
    bool Done,
    bool Failed,
    string? FailureCode,
    string? FailureMessage,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

internal interface IGoogleAccessTokenSource
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

internal interface ICloudRunJobsGateway
{
    Task<string> StartAsync(RuntimeLaunchRequest request, RuntimeExecutionId executionId, CancellationToken cancellationToken);
    Task<CloudRunOperationSnapshot> GetAsync(string operationName, CancellationToken cancellationToken);
    Task CancelAsync(string operationName, string? executionName, CancellationToken cancellationToken);
}

internal sealed class MetadataGoogleAccessTokenSource : IGoogleAccessTokenSource
{
    private static readonly Uri TokenEndpoint = new(
        "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token");
    private readonly HttpClient _httpClient;
    private readonly ILogger<MetadataGoogleAccessTokenSource> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _refreshAtUtc;

    public MetadataGoogleAccessTokenSource(
        HttpClient httpClient,
        ILogger<MetadataGoogleAccessTokenSource> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_token) && _timeProvider.GetUtcNow() < _refreshAtUtc)
        {
            return _token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_token) && _timeProvider.GetUtcNow() < _refreshAtUtc)
            {
                return _token;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get, TokenEndpoint);
            request.Headers.TryAddWithoutValidation("Metadata-Flavor", "Google");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new CloudRunGatewayException(
                    CloudRunGatewayOperation.AcquireAccessToken,
                    $"metadata_token_http_{(int)response.StatusCode}",
                    $"Google metadata access token acquisition failed with HTTP {(int)response.StatusCode}.",
                    response.StatusCode,
                    CloudRunGatewayErrorPolicy.IsRetryable(response.StatusCode));
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            _token = document.RootElement.TryGetProperty("access_token", out var token) ? token.GetString() : null;
            if (string.IsNullOrWhiteSpace(_token))
                throw new CloudRunGatewayException(CloudRunGatewayOperation.AcquireAccessToken, "metadata_token_missing", "Metadata token response did not contain access_token.");
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expires)
                ? expires.GetInt32()
                : 300;
            if (expiresIn <= 0)
                throw new CloudRunGatewayException(CloudRunGatewayOperation.AcquireAccessToken, "metadata_token_expiry_invalid", "Metadata token expiry must be positive.");
            _refreshAtUtc = _timeProvider.GetUtcNow().AddSeconds(expiresIn <= 120 ? Math.Max(1, expiresIn / 2) : expiresIn - 60);
            return _token;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Google metadata access token acquisition failed.");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }
}

internal sealed class CloudRunJobsRestGateway(
    HttpClient httpClient,
    IGoogleAccessTokenSource accessTokenSource,
    IOptions<RuntimeOrchestrationOptions> options,
    ILogger<CloudRunJobsRestGateway> logger) : ICloudRunJobsGateway
{
    private readonly RuntimeOrchestrationOptions _options = options.Value;
    private readonly CloudRunResourceNamePolicy _resourceNames = new(options.Value);

    public async Task<string> StartAsync(RuntimeLaunchRequest request, RuntimeExecutionId executionId, CancellationToken cancellationToken)
    {
        var environment = BuildEnvironment(request, executionId);
        var payload = new
        {
            overrides = new
            {
                taskCount = 1,
                timeout = $"{Math.Clamp(checked((int)Math.Ceiling(request.Timeout.TotalSeconds)), 5, _options.MaximumTimeoutSeconds)}s",
                containerOverrides = new[]
                {
                    new
                    {
                        name = _options.CloudRunSimulatorContainerName,
                        env = environment.Select(pair => new { name = pair.Key, value = pair.Value }).ToArray()
                    }
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://run.googleapis.com/v2/{_resourceNames.JobName}:run")
        {
            Content = JsonContent.Create(payload)
        };
        await AuthorizeAsync(httpRequest, cancellationToken);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CloudRunGatewayErrorPolicy.FromHttpFailure(CloudRunGatewayOperation.StartJob, response.StatusCode, body);
        }

        using var document = JsonDocument.Parse(body);
        var operationName = document.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new CloudRunGatewayException(CloudRunGatewayOperation.ParseProviderResponse, "cloud_run_operation_name_missing", "Cloud Run Jobs API did not return an operation name.");
        }
        operationName = _resourceNames.ValidateOperationName(operationName);

        logger.LogInformation(
            "Cloud Run Simulator job accepted. Operation={OperationName} Correlation={Correlation}",
            operationName,
            request.Simulation.OrchestratorCorrelationId);
        return operationName;
    }

    public async Task<CloudRunOperationSnapshot> GetAsync(
        string operationName,
        CancellationToken cancellationToken)
    {
        operationName = _resourceNames.ValidateOperationName(operationName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://run.googleapis.com/v2/{operationName.TrimStart('/')}");
        await AuthorizeAsync(request, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CloudRunGatewayErrorPolicy.FromHttpFailure(CloudRunGatewayOperation.ReadOperation, response.StatusCode, body);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var done = root.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean();
        var failed = root.TryGetProperty("error", out var error);
        var failureCode = failed && error.TryGetProperty("code", out var code) ? code.ToString() : null;
        var failureMessage = failed && error.TryGetProperty("message", out var message)
            ? CloudRunGatewayErrorPolicy.ExtractSafeProviderSummary(message.GetString() ?? string.Empty)
            : null;
        var executionName = TryReadExecutionName(root);
        if (!string.IsNullOrWhiteSpace(executionName)) executionName = _resourceNames.ValidateExecutionName(executionName);
        if (done && !failed && string.IsNullOrWhiteSpace(executionName))
            throw new CloudRunGatewayException(CloudRunGatewayOperation.ParseProviderResponse, "cloud_run_execution_name_missing", "Completed Cloud Run operation did not return an execution name.");
        var started = TryReadDate(root, "startTime") ?? TryReadMetadataDate(root, "createTime");
        var finished = done ? TryReadDate(root, "completionTime") ?? TryReadMetadataDate(root, "completionTime") : null;

        return new CloudRunOperationSnapshot(
            operationName,
            executionName,
            done,
            failed,
            failureCode,
            failureMessage,
            started,
            finished);
    }

    public async Task CancelAsync(
        string operationName,
        string? executionName,
        CancellationToken cancellationToken)
    {
        _ = _resourceNames.ValidateOperationName(operationName);
        if (string.IsNullOrWhiteSpace(executionName))
            throw new CloudRunGatewayException(CloudRunGatewayOperation.CancelExecution, "cloud_run_execution_name_unavailable", "Cloud Run cancellation requires an execution name.", isRetryable: true);
        var target = $"https://run.googleapis.com/v2/{_resourceNames.ValidateExecutionName(executionName)}:cancel";
        using var request = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Content = JsonContent.Create(new { })
        };
        await AuthorizeAsync(request, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CloudRunGatewayErrorPolicy.FromHttpFailure(CloudRunGatewayOperation.CancelExecution, response.StatusCode, body);
        }
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await accessTokenSource.GetAccessTokenAsync(cancellationToken));
    }

    private static Dictionary<string, string> BuildEnvironment(RuntimeLaunchRequest request, RuntimeExecutionId executionId)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_ENVIRONMENT"] = request.Environment,
            ["Simulator__ControlPlaneEnabled"] = "true",
            ["Simulator__ControlPlaneAreaCode"] = request.Simulation.AreaCode,
            ["Simulator__ControlPlaneScenarioCode"] = request.Simulation.ScenarioCode,
            ["Simulator__RunOverrides__OrchestratorCorrelationId"] = request.Simulation.OrchestratorCorrelationId,
            ["RuntimeExecution__ExecutionId"] = executionId.Value.ToString("D"),
            ["RuntimeExecution__RequestId"] = request.RequestId.ToString("D")
        };
        Add(values, "Simulator__RunOverrides__SensorCount", request.Simulation.SensorCount);
        Add(values, "Simulator__RunOverrides__NumberOfCycles", request.Simulation.NumberOfCycles);
        Add(values, "Simulator__RunOverrides__IntervalSeconds", request.Simulation.IntervalSeconds);
        Add(values, "Simulator__RunOverrides__Seed", request.Simulation.Seed);
        Add(values, "Simulator__RunOverrides__DegradationProfile", request.Simulation.LegacyDegradationProfile);
        for (var index = 0; index < request.Simulation.DegradationProfiles.Count; index++)
        {
            values[$"Simulator__RunOverrides__DegradationProfiles__{index}"] = request.Simulation.DegradationProfiles[index];
        }

        if (request.Profile == RuntimeLaunchProfile.ControlledValidationP3 && request.ControlledValidation is not null)
        {
            var controlled = request.ControlledValidation;
            values["ControlledValidation__Enabled"] = "true";
            values["ControlledValidation__Phase"] = controlled.Phase;
            values["ControlledValidation__ControlledValidationRunId"] = controlled.ControlledValidationRunId.ToString("D");
            values["ControlledValidation__RunLabel"] = controlled.RunLabel;
            values["ControlledValidation__ScenarioCode"] = controlled.ScenarioCode;
            values["ControlledValidation__AreaId"] = controlled.AreaId.ToString("D");
            values["ControlledValidation__SimulationRunId"] = controlled.SimulationRunId.ToString("D");
            values["ControlledValidation__NominalSensorId"] = controlled.NominalSensorId.ToString("D");
            values["ControlledValidation__NominalSensorName"] = controlled.NominalSensorName;
            values["ControlledValidation__SensorNotFoundId"] = controlled.SensorNotFoundId.ToString("D");
            values["ControlledValidation__EventTime"] = controlled.EventTime.ToString("o");
            values["ControlledValidation__WriteEvidenceSidecar"] = "false";
        }
        return values;
    }

    private static void Add(Dictionary<string, string> values, string key, object? value)
    {
        if (value is not null && !string.IsNullOrWhiteSpace(value.ToString())) values[key] = value.ToString()!;
    }

    private static string? TryReadExecutionName(JsonElement root)
    {
        if (root.TryGetProperty("response", out var response) &&
            response.TryGetProperty("name", out var responseName))
        {
            return responseName.GetString();
        }
        if (root.TryGetProperty("metadata", out var metadata))
        {
            foreach (var property in new[] { "target", "execution", "executionName", "name" })
            {
                if (metadata.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
                    value.GetString()?.Contains("/executions/", StringComparison.Ordinal) == true)
                {
                    return value.GetString();
                }
            }
        }
        return null;
    }

    private static DateTimeOffset? TryReadDate(JsonElement root, string property)
        => root.TryGetProperty("response", out var response) && response.TryGetProperty(property, out var value) &&
           DateTimeOffset.TryParse(value.GetString(), out var parsed) ? parsed : null;

    private static DateTimeOffset? TryReadMetadataDate(JsonElement root, string property)
        => root.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty(property, out var value) &&
           DateTimeOffset.TryParse(value.GetString(), out var parsed) ? parsed : null;
}
