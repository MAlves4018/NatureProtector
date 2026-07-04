using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Operations.Configuration;

namespace NatureProtector.Backoffice.Api.Operations.Services;

public sealed record AutomationDispatchResult(
    string Status,
    string Provider,
    string? ProviderReference,
    string EvidenceLevel,
    string? Limitation);

public interface IAutomationDispatcher
{
    Task<AutomationDispatchResult> DispatchAsync(
        OperationDefinition definition,
        EngineeringOperationRecord operation,
        CancellationToken cancellationToken);
}

public sealed class SafeAutomationDispatcher : IAutomationDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OperationsOptions _options;
    private readonly IWebHostEnvironment _environment;

    public SafeAutomationDispatcher(
        IHttpClientFactory httpClientFactory,
        IOptions<OperationsOptions> options,
        IWebHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<AutomationDispatchResult> DispatchAsync(
        OperationDefinition definition,
        EngineeringOperationRecord operation,
        CancellationToken cancellationToken)
    {
        if (string.Equals(_options.Mode, "Simulation", StringComparison.OrdinalIgnoreCase))
        {
            return new AutomationDispatchResult(
                "Queued",
                "simulation",
                $"simulation://{operation.Id:N}",
                "DEMONSTRATION_ONLY",
                "The request was recorded and simulated; no external workflow or cloud mutation was executed.");
        }

        if (!string.Equals(_options.Mode, "GitHub", StringComparison.OrdinalIgnoreCase))
        {
            return new AutomationDispatchResult(
                "Blocked",
                "disabled",
                null,
                "NOT_PROVED",
                "Operations dispatch is disabled. Configure Operations:Mode=GitHub and a server-side installation token.");
        }

        if (string.IsNullOrWhiteSpace(_options.GitHubRepository) ||
            string.IsNullOrWhiteSpace(_options.GitHubToken))
        {
            return new AutomationDispatchResult(
                "Blocked",
                "github-actions",
                null,
                "NOT_PROVED",
                "GitHub repository or server-side automation token is not configured.");
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.GitHubApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NatureProtector-Operations-Control-Plane/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.GitHubToken);
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        var inputs = new Dictionary<string, string>(operation.Inputs, StringComparer.OrdinalIgnoreCase)
        {
            ["operation_id"] = operation.Id.ToString("D"),
            ["operation_kind"] = operation.OperationId,
            ["environment"] = operation.Environment,
            ["collect_evidence"] = operation.CollectEvidence ? "true" : "false"
        };

        var workflow = Uri.EscapeDataString(definition.Workflow);
        var repository = _options.GitHubRepository.Trim('/');
        var response = await client.PostAsJsonAsync(
            $"repos/{repository}/actions/workflows/{workflow}/dispatches",
            new { @ref = operation.Ref, inputs },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            return new AutomationDispatchResult(
                "Failed",
                "github-actions",
                null,
                "NOT_PROVED",
                $"GitHub workflow dispatch failed with HTTP {(int)response.StatusCode}: {Limit(detail, 300)}");
        }

        return new AutomationDispatchResult(
            "Queued",
            "github-actions",
            $"https://github.com/{repository}/actions/workflows/{definition.Workflow}",
            "IMPLEMENTED_NOT_PROVED",
            "The workflow was accepted for dispatch. Completion and artifacts must be reported back before the operation can be classified as proved.");
    }

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}
