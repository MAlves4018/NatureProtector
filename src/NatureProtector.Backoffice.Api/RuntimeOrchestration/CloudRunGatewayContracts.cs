using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

internal enum CloudRunGatewayOperation { AcquireAccessToken, StartJob, ReadOperation, CancelExecution, ValidateProviderReference, ParseProviderResponse }

internal sealed class CloudRunGatewayException : Exception
{
    public CloudRunGatewayException(CloudRunGatewayOperation operation, string code, string message,
        HttpStatusCode? statusCode = null, bool isRetryable = false, Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        Code = code;
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }

    public CloudRunGatewayOperation Operation { get; }
    public string Code { get; }
    public HttpStatusCode? StatusCode { get; }
    public bool IsRetryable { get; }
}

internal static partial class CloudRunGatewayErrorPolicy
{
    internal const int MaximumSafeProviderMessageCharacters = 512;

    public static CloudRunGatewayException FromHttpFailure(CloudRunGatewayOperation operation, HttpStatusCode statusCode, string body)
        => new(operation, $"cloud_run_http_{(int)statusCode}",
            $"Cloud Run request failed with HTTP {(int)statusCode} ({statusCode}). Provider={ExtractSafeProviderSummary(body)}",
            statusCode, IsRetryable(statusCode));

    public static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    public static string ExtractSafeProviderSummary(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "<empty>";
        var candidate = TryReadGoogleError(body) ?? body;
        candidate = BearerTokenRegex().Replace(candidate, "Bearer [REDACTED]");
        candidate = SecretRegex().Replace(candidate, match => $"{match.Groups[1].Value}=[REDACTED]");
        candidate = candidate.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return candidate.Length <= MaximumSafeProviderMessageCharacters
            ? candidate
            : candidate[..MaximumSafeProviderMessageCharacters] + "...";
    }

    private static string? TryReadGoogleError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return null;
            var code = error.TryGetProperty("code", out var c) ? c.ToString() : null;
            var status = error.TryGetProperty("status", out var s) ? s.GetString() : null;
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
            return string.Join(" ", new[] { code is null ? null : $"code={code}", status is null ? null : $"status={status}", message is null ? null : $"message={message}" }.Where(x => x is not null));
        }
        catch (JsonException) { return null; }
    }

    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)\b(access[_-]?token|id[_-]?token|token|authorization|secret|password|credential|private[_-]?key)\b\s*[:=]\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretRegex();
}

internal sealed class CloudRunResourceNamePolicy
{
    private readonly string _operationPrefix;
    private readonly string _executionPrefix;

    public CloudRunResourceNamePolicy(RuntimeOrchestrationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CloudRunProjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CloudRunRegion);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CloudRunSimulatorJobName);
        JobName = $"projects/{options.CloudRunProjectId}/locations/{options.CloudRunRegion}/jobs/{options.CloudRunSimulatorJobName}";
        _operationPrefix = $"projects/{options.CloudRunProjectId}/locations/{options.CloudRunRegion}/operations/";
        _executionPrefix = $"{JobName}/executions/";
    }

    public string JobName { get; }
    public string ValidateOperationName(string value) => Validate(value, _operationPrefix, "operation");
    public string ValidateExecutionName(string value) => Validate(value, _executionPrefix, "execution");

    private static string Validate(string value, string requiredPrefix, string resourceType)
    {
        var normalized = value?.Trim().TrimStart('/') ?? string.Empty;
        var suffix = normalized.StartsWith(requiredPrefix, StringComparison.Ordinal) ? normalized[requiredPrefix.Length..] : string.Empty;
        if (normalized.Contains("://", StringComparison.Ordinal) || normalized.Contains('\\') ||
            normalized.Contains('?') || normalized.Contains('#') || normalized.Contains("..", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(suffix) || suffix.Contains('/'))
        {
            throw new CloudRunGatewayException(CloudRunGatewayOperation.ValidateProviderReference,
                $"invalid_cloud_run_{resourceType}_name", $"Provider {resourceType} reference is outside the configured Cloud Run boundary.");
        }
        return normalized;
    }
}
