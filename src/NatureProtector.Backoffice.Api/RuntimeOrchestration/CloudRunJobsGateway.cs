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

internal sealed class MetadataGoogleAccessTokenSource(
    HttpClient httpClient,
    ILogger<MetadataGoogleAccessTokenSource> logger) : IGoogleAccessTokenSource
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _refreshAtUtc;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _refreshAtUtc)
        {
            return _token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _refreshAtUtc)
            {
                return _token;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token");
            request.Headers.TryAddWithoutValidation("Metadata-Flavor", "Google");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            _token = document.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Metadata token response did not contain access_token.");
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expires)
                ? expires.GetInt32()
                : 300;
            _refreshAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
            return _token;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Google metadata access token acquisition failed.");
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

    public async Task<string> StartAsync(RuntimeLaunchRequest request, RuntimeExecutionId executionId, CancellationToken cancellationToken)
    {
        var jobName = $"projects/{_options.CloudRunProjectId}/locations/{_options.CloudRunRegion}/jobs/{_options.CloudRunSimulatorJobName}";
        var environment = BuildEnvironment(request, executionId);
        var payload = new
        {
            overrides = new
            {
                timeout = $"{Math.Clamp((int)request.Timeout.TotalSeconds, 5, _options.MaximumTimeoutSeconds)}s",
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
            $"https://run.googleapis.com/v2/{jobName}:run")
        {
            Content = JsonContent.Create(payload)
        };
        await AuthorizeAsync(httpRequest, cancellationToken);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Cloud Run Job start failed ({(int)response.StatusCode}): {body}");
        }

        using var document = JsonDocument.Parse(body);
        var operationName = document.RootElement.GetProperty("name").GetString();
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new InvalidOperationException("Cloud Run Jobs API did not return an operation name.");
        }

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
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://run.googleapis.com/v2/{operationName.TrimStart('/')}");
        await AuthorizeAsync(request, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Cloud Run operation read failed ({(int)response.StatusCode}): {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var done = root.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean();
        var failed = root.TryGetProperty("error", out var error);
        var failureCode = failed && error.TryGetProperty("code", out var code) ? code.ToString() : null;
        var failureMessage = failed && error.TryGetProperty("message", out var message) ? message.GetString() : null;
        var executionName = TryReadExecutionName(root);
        var started = TryReadDate(root, "startTime") ?? TryReadMetadataDate(root, "createTime");
        var finished = done ? TryReadDate(root, "completionTime") ?? DateTimeOffset.UtcNow : (DateTimeOffset?)null;

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
        var target = !string.IsNullOrWhiteSpace(executionName)
            ? $"https://run.googleapis.com/v2/{executionName.TrimStart('/')}:cancel"
            : $"https://run.googleapis.com/v2/{operationName.TrimStart('/')}:cancel";
        using var request = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Content = JsonContent.Create(new { })
        };
        await AuthorizeAsync(request, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Cloud Run cancellation failed ({(int)response.StatusCode}): {body}");
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
            foreach (var property in new[] { "execution", "executionName", "name" })
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
