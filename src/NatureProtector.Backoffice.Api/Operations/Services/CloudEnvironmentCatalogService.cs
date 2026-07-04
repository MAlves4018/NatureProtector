using System.Text.Json;
using NatureProtector.Backoffice.Api.Operations.Contracts;

namespace NatureProtector.Backoffice.Api.Operations.Services;

public interface ICloudEnvironmentCatalogService
{
    IReadOnlyList<CloudEnvironmentResponse> List();
    CloudEnvironmentResponse? Get(string environment);
}

public sealed class CloudEnvironmentCatalogService : ICloudEnvironmentCatalogService
{
    private readonly string _repositoryRoot;

    public CloudEnvironmentCatalogService(IWebHostEnvironment environment)
    {
        _repositoryRoot = ResolveRepositoryRoot(environment.ContentRootPath);
    }

    public IReadOnlyList<CloudEnvironmentResponse> List() =>
        new[] { "staging", "production" }
            .Select(Get)
            .Where(result => result is not null)
            .Cast<CloudEnvironmentResponse>()
            .ToArray();

    public CloudEnvironmentResponse? Get(string environment)
    {
        var normalized = environment.Trim().ToLowerInvariant();
        if (normalized is not ("staging" or "production"))
        {
            return null;
        }

        var relative = Path.Combine("deploy", "environments", $"{normalized}.json");
        var path = Path.Combine(_repositoryRoot, relative);
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var commonPath = Path.Combine(_repositoryRoot, "deploy", "environments", "common.json");
        using var commonDocument = File.Exists(commonPath)
            ? JsonDocument.Parse(File.ReadAllText(commonPath))
            : null;
        var common = commonDocument?.RootElement;
        var projectId = ReadString(root, "project_id") ?? ReadString(common, "project_id") ?? "unknown";
        var region = ReadString(root, "region") ?? ReadString(common, "region") ?? "unknown";
        var deployable = root.TryGetProperty("deployable", out var deployableElement) && deployableElement.GetBoolean();
        var artifactRepository = ReadString(common, "artifact_repository") ?? "not declared";
        var namespaceName = ReadString(root, "namespace") ?? "not declared";
        var statePrefix = ReadString(root, "terraform_state_prefix") ?? "not declared";
        var overlay = ReadString(root, "kustomize_overlay") ?? "not declared";
        var budget = ReadNumber(root, "budget_envelope_eur_month");
        var resources = new List<CloudResourceDeclarationResponse>
        {
            new("gcp-project", projectId, "project", "Declared", relative),
            new("region", region, "regional", "Declared", relative),
            new("artifact-registry", artifactRepository, "regional", "Declared", "deploy/environments/common.json"),
            new("cloud-deploy", "g8-1 pipeline definitions", "regional", "Declared", "infra/gcp/cloud-deploy"),
            new("kubernetes-namespace", namespaceName, "environment", "Declared", relative),
            new("kustomize-overlay", overlay, "environment", "Declared", relative),
            new("terraform-state-prefix", statePrefix, "environment", "Declared", relative),
            new("budget-envelope-eur-month", budget ?? "not declared", "environment", "Declared", relative)
        };
        var limitations = new List<string>
        {
            "This inventory is derived from repository configuration, not a live cloud API query.",
            "Observed resource health, cost and drift require a dispatched inventory operation."
        };
        if (!deployable)
        {
            limitations.Add(ReadString(root, "locked_reason") ?? "Environment is locked by repository policy.");
        }

        return new CloudEnvironmentResponse(
            normalized,
            projectId,
            region,
            deployable,
            relative.Replace('\\', '/'),
            "DeclaredNotObserved",
            "IMPLEMENTED_NOT_PROVED",
            resources,
            limitations);
    }

    private static string ResolveRepositoryRoot(string start)
    {
        var current = new DirectoryInfo(Path.GetFullPath(start));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")) &&
                Directory.Exists(Path.Combine(current.FullName, "deploy", "environments")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return Path.GetFullPath(start);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadString(JsonElement? element, string name) =>
        element is { } value ? ReadString(value, name) : null;

    private static string? ReadNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetRawText()
            : null;
}
