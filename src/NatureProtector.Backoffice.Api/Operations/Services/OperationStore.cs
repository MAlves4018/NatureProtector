using System.Text.Json;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Operations.Configuration;
using NatureProtector.Backoffice.Api.Operations.Contracts;

namespace NatureProtector.Backoffice.Api.Operations.Services;

public sealed class EngineeringOperationRecord
{
    public Guid Id { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; set; } = "Requested";
    public string Environment { get; init; } = string.Empty;
    public string Ref { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public string[] RequestedByRoles { get; init; } = [];
    public string[] RequestedByCapabilities { get; init; } = [];
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool CollectEvidence { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public string? Workflow { get; init; }
    public string? PlanHash { get; init; }
    public string EvidenceLevel { get; set; } = "IMPLEMENTED_NOT_PROVED";
    public Dictionary<string, string> Inputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<OperationStepResponse> Steps { get; init; } = [];
    public List<OperationArtifactResponse> Artifacts { get; init; } = [];
    public List<OperationApprovalResponse> Approvals { get; init; } = [];
    public List<string> Limitations { get; init; } = [];

    public EngineeringOperationResponse ToResponse() => new(
        Id, OperationId, Category, DisplayName, Status, Environment, Ref, RequestedBy,
        RequestedByRoles, RequestedByCapabilities, RequestedAt, UpdatedAt, CollectEvidence,
        RiskLevel, RequiresApproval, Provider, ProviderReference, Workflow, PlanHash,
        EvidenceLevel, Inputs, Steps, Artifacts, Approvals, Limitations);
}

public interface IOperationStore
{
    Task SaveAsync(EngineeringOperationRecord operation, CancellationToken cancellationToken);
    Task<EngineeringOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<EngineeringOperationRecord>> ListAsync(string? category, int take, CancellationToken cancellationToken);
}

public sealed class FileSystemOperationStore : IOperationStore
{
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileSystemOperationStore(
        IWebHostEnvironment environment,
        IOptions<OperationsOptions> options)
    {
        var configured = options.Value.StoreRoot;
        _root = Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(EngineeringOperationRecord operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_root);
            var target = Path.Combine(_root, $"{operation.Id:N}.json");
            var temporary = target + ".tmp";
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, operation, _json, cancellationToken);
            }
            File.Move(temporary, target, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EngineeringOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_root, $"{id:N}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<EngineeringOperationRecord>(stream, _json, cancellationToken);
    }

    public async Task<IReadOnlyList<EngineeringOperationRecord>> ListAsync(
        string? category,
        int take,
        CancellationToken cancellationToken)
    {
        var records = new List<EngineeringOperationRecord>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(path);
            var record = await JsonSerializer.DeserializeAsync<EngineeringOperationRecord>(stream, _json, cancellationToken);
            if (record is not null && (string.IsNullOrWhiteSpace(category) ||
                string.Equals(record.Category, category, StringComparison.OrdinalIgnoreCase)))
            {
                records.Add(record);
            }
        }

        return records
            .OrderByDescending(record => record.RequestedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();
    }
}
