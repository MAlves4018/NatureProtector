using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Configuration;
using NatureProtector.Backoffice.Api.Operations.Contracts;

namespace NatureProtector.Backoffice.Api.Operations.Services;

public sealed record OperationServiceResult(
    EngineeringOperationResponse? Operation,
    int StatusCode,
    string? Error);

public interface IEngineeringOperationsService
{
    IReadOnlyList<OperationDefinitionResponse> ListCatalog(ClaimsPrincipal user, string? category);
    Task<IReadOnlyList<EngineeringOperationResponse>> ListAsync(ClaimsPrincipal user, string? category, int take, CancellationToken cancellationToken);
    Task<EngineeringOperationResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<OperationServiceResult> StartAsync(StartOperationRequest request, ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<OperationServiceResult> DecideAsync(Guid id, OperationDecisionRequest request, ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<OperationServiceResult> CancelAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<OperationServiceResult> ApplyCallbackAsync(OperationCallbackRequest request, string? secret, CancellationToken cancellationToken);
    Task<OperationComparisonResponse?> CompareAsync(Guid left, Guid right, ClaimsPrincipal user, CancellationToken cancellationToken);
}

public sealed class EngineeringOperationsService : IEngineeringOperationsService
{
    private static readonly HashSet<string> FinalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Succeeded", "Failed", "Cancelled", "Rejected", "RolledBack"
    };

    private readonly IOperationCatalog _catalog;
    private readonly IOperationStore _store;
    private readonly IAutomationDispatcher _dispatcher;
    private readonly OperationsOptions _options;
    private readonly IWebHostEnvironment _environment;

    public EngineeringOperationsService(
        IOperationCatalog catalog,
        IOperationStore store,
        IAutomationDispatcher dispatcher,
        IOptions<OperationsOptions> options,
        IWebHostEnvironment environment)
    {
        _catalog = catalog;
        _store = store;
        _dispatcher = dispatcher;
        _options = options.Value;
        _environment = environment;
    }

    public IReadOnlyList<OperationDefinitionResponse> ListCatalog(ClaimsPrincipal user, string? category) =>
        _catalog.All
            .Where(definition => string.IsNullOrWhiteSpace(category) ||
                string.Equals(definition.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(definition => CanReadCategory(user, definition.Category))
            .Select(definition => definition.ToResponse(
                OperationRoleCatalog.HasCapability(user, definition.RequiredCapability)))
            .ToArray();

    public async Task<IReadOnlyList<EngineeringOperationResponse>> ListAsync(
        ClaimsPrincipal user,
        string? category,
        int take,
        CancellationToken cancellationToken) =>
        (await _store.ListAsync(category, take, cancellationToken))
            .Where(operation => CanReadOperation(user, operation))
            .Select(operation => operation.ToResponse())
            .ToArray();

    public async Task<EngineeringOperationResponse?> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var operation = await _store.GetAsync(id, cancellationToken);
        return operation is not null && CanReadOperation(user, operation)
            ? operation.ToResponse()
            : null;
    }

    public async Task<OperationServiceResult> StartAsync(
        StartOperationRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperationId) || string.IsNullOrWhiteSpace(request.Environment))
        {
            return Error(400, "OperationId and Environment are required.");
        }

        var definition = _catalog.Find(request.OperationId);
        if (definition is null)
        {
            return Error(404, $"Unknown operation '{request.OperationId}'.");
        }

        if (!OperationRoleCatalog.HasCapability(user, definition.RequiredCapability))
        {
            return Error(403, $"Capability '{definition.RequiredCapability}' is required.");
        }

        if (!string.Equals(definition.Availability, "implemented", StringComparison.OrdinalIgnoreCase))
        {
            return Error(409, definition.Limitation ?? $"Operation '{definition.OperationId}' is currently {definition.Availability}.");
        }

        var environment = request.Environment.Trim().ToLowerInvariant();
        if (!definition.Environments.Contains(environment, StringComparer.OrdinalIgnoreCase))
        {
            return Error(400, $"Environment '{environment}' is not allowed for '{definition.OperationId}'.");
        }

        var inputsResult = ValidateInputs(definition, request.Inputs);
        if (inputsResult.Error is not null)
        {
            return Error(400, inputsResult.Error);
        }

        var confirmation = BuildConfirmation(definition, environment, inputsResult.Inputs);
        if (definition.RequiresConfirmation && !string.Equals(request.Confirmation?.Trim(), confirmation, StringComparison.Ordinal))
        {
            return Error(400, $"Exact confirmation required: {confirmation}");
        }

        var now = DateTimeOffset.UtcNow;
        var roles = OperationRoleCatalog.GetRoles(user).ToArray();
        var capabilities = OperationRoleCatalog.GetCapabilities(roles).ToArray();
        var requestedBy = user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.Identity?.Name ??
            "authenticated-user";
        var reference = string.IsNullOrWhiteSpace(request.Ref) ? _options.DefaultRef : request.Ref.Trim();
        if (!IsSafeReference(reference))
        {
            return Error(400, "Ref contains unsupported characters.");
        }

        var operation = new EngineeringOperationRecord
        {
            Id = Guid.NewGuid(),
            OperationId = definition.OperationId,
            Category = definition.Category,
            DisplayName = definition.DisplayName,
            Status = definition.RequiresApproval ? "AwaitingApproval" : "Validated",
            Environment = environment,
            Ref = reference,
            RequestedBy = requestedBy,
            RequestedByRoles = roles,
            RequestedByCapabilities = capabilities,
            RequestedAt = now,
            UpdatedAt = now,
            CollectEvidence = request.CollectEvidence,
            RiskLevel = definition.RiskLevel,
            RequiresApproval = definition.RequiresApproval,
            Workflow = definition.Workflow,
            PlanHash = inputsResult.Inputs.GetValueOrDefault("planHash"),
            EvidenceLevel = definition.EvidenceLevel,
            Inputs = inputsResult.Inputs,
            Steps =
            [
                new OperationStepResponse(1, "Requested", "Succeeded", now, "Request accepted by the operations API."),
                new OperationStepResponse(2, "Validated", "Succeeded", now, "Role, capability, environment, closed input catalog and confirmation were validated.")
            ],
            Limitations = definition.Limitation is null ? [] : [definition.Limitation]
        };

        await _store.SaveAsync(operation, cancellationToken);
        if (!definition.RequiresApproval)
        {
            await DispatchAsync(definition, operation, cancellationToken);
        }

        return Success(operation);
    }

    public async Task<OperationServiceResult> DecideAsync(
        Guid id,
        OperationDecisionRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!OperationRoleCatalog.HasCapability(user, OperationCapabilities.ApprovalReview))
        {
            return Error(403, $"Capability '{OperationCapabilities.ApprovalReview}' is required.");
        }

        var operation = await _store.GetAsync(id, cancellationToken);
        if (operation is null)
        {
            return Error(404, $"Operation '{id}' was not found.");
        }

        if (!string.Equals(operation.Status, "AwaitingApproval", StringComparison.OrdinalIgnoreCase))
        {
            return Error(409, $"Operation is in state '{operation.Status}', not AwaitingApproval.");
        }

        var reviewer = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity?.Name ?? "reviewer";
        if (!_options.AllowSelfApproval && string.Equals(operation.RequestedBy, reviewer, StringComparison.OrdinalIgnoreCase))
        {
            return Error(409, "Self-approval is disabled for this environment.");
        }
        if (!_options.AllowSelfApproval && !_environment.IsDevelopment() &&
            string.Equals(reviewer, operation.RequestedBy, StringComparison.OrdinalIgnoreCase))
        {
            return Error(409, "Self-approval is disabled outside Development.");
        }

        var decision = request.Decision.Trim();
        if (!decision.Equals("approve", StringComparison.OrdinalIgnoreCase) &&
            !decision.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            return Error(400, "Decision must be 'approve' or 'reject'.");
        }

        var now = DateTimeOffset.UtcNow;
        operation.Approvals.Add(new OperationApprovalResponse(decision, reviewer, now, request.Comment));
        operation.UpdatedAt = now;
        operation.Steps.Add(new OperationStepResponse(
            operation.Steps.Count + 1,
            "Approval",
            decision.Equals("approve", StringComparison.OrdinalIgnoreCase) ? "Succeeded" : "Rejected",
            now,
            request.Comment));

        if (decision.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            operation.Status = "Rejected";
            await _store.SaveAsync(operation, cancellationToken);
            return Success(operation);
        }

        var definition = _catalog.Find(operation.OperationId);
        if (definition is null)
        {
            return Error(409, "The operation definition no longer exists.");
        }

        await _store.SaveAsync(operation, cancellationToken);
        await DispatchAsync(definition, operation, cancellationToken);
        return Success(operation);
    }

    public async Task<OperationServiceResult> CancelAsync(
        Guid id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var operation = await _store.GetAsync(id, cancellationToken);
        if (operation is null)
        {
            return Error(404, $"Operation '{id}' was not found.");
        }

        var definition = _catalog.Find(operation.OperationId);
        if (definition is null || !OperationRoleCatalog.HasCapability(user, definition.RequiredCapability))
        {
            return Error(403, "The current user cannot cancel this operation.");
        }

        if (FinalStatuses.Contains(operation.Status))
        {
            return Error(409, $"Operation is already final: {operation.Status}.");
        }

        operation.Status = "Cancelled";
        operation.UpdatedAt = DateTimeOffset.UtcNow;
        operation.Steps.Add(new OperationStepResponse(
            operation.Steps.Count + 1,
            "Cancellation",
            "Succeeded",
            operation.UpdatedAt,
            "Local operation tracking was cancelled. A remotely running workflow may require provider-side cancellation."));
        operation.Limitations.Add("Remote provider cancellation is not claimed by this endpoint.");
        await _store.SaveAsync(operation, cancellationToken);
        return Success(operation);
    }

    public async Task<OperationServiceResult> ApplyCallbackAsync(
        OperationCallbackRequest request,
        string? secret,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.CallbackSecret) && !SecretsEqual(secret, _options.CallbackSecret))
        {
            return Error(403, "Invalid operations callback secret.");
        }

        var operation = await _store.GetAsync(request.OperationId, cancellationToken);
        if (operation is null)
        {
            operation = new EngineeringOperationRecord
            {
                Id = request.OperationId,
                OperationId = "push-ci",
                Category = "quality",
                DisplayName = "Push CI",
                Status = request.Status,
                Environment = "ci",
                Ref = "master",
                RequestedBy = "github-actions",
                RequestedByRoles = [],
                RequestedByCapabilities = [],
                RequestedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CollectEvidence = false,
                RiskLevel = "low",
                RequiresApproval = false,
                Provider = "github-actions",
                ProviderReference = request.ProviderReference,
                Workflow = "engineering-foundations.yml",
                EvidenceLevel = "IMPLEMENTED_NOT_PROVED",
                Inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Steps = [],
                Limitations = []
            };
        }

        var allowed = new[] { "Queued", "Running", "Succeeded", "Failed", "Cancelled", "RolledBack" };
        if (!allowed.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Error(400, "Unsupported callback status.");
        }

        operation.Status = request.Status;
        operation.ProviderReference = request.ProviderReference ?? operation.ProviderReference;
        operation.UpdatedAt = DateTimeOffset.UtcNow;
        operation.Steps.Add(new OperationStepResponse(
            operation.Steps.Count + 1,
            "Provider callback",
            request.Status,
            operation.UpdatedAt,
            request.Detail));
        if (request.Artifacts is not null)
        {
            foreach (var artifact in request.Artifacts)
            {
                if (!operation.Artifacts.Any(existing => existing.ArtifactId == artifact.ArtifactId))
                {
                    operation.Artifacts.Add(artifact);
                }
            }
        }
        var artifactsAreVerifiable = operation.Artifacts.Count > 0 && operation.Artifacts.All(artifact =>
            !string.IsNullOrWhiteSpace(artifact.Reference) &&
            !string.IsNullOrWhiteSpace(artifact.Sha256) &&
            artifact.Sha256.Length == 64 &&
            artifact.Sha256.All(Uri.IsHexDigit));
        if (request.Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) && artifactsAreVerifiable)
        {
            operation.EvidenceLevel = "PROVED_BY_HASHED_REPORTED_ARTIFACTS";
        }
        else if (request.Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            operation.EvidenceLevel = "SUCCEEDED_WITHOUT_VERIFIABLE_ARTIFACT_PROOF";
            operation.Limitations.Add("Success was reported, but at least one artifact lacks a reference or valid SHA-256.");
        }
        await _store.SaveAsync(operation, cancellationToken);
        return Success(operation);
    }

    public async Task<OperationComparisonResponse?> CompareAsync(
        Guid left,
        Guid right,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var leftOperation = await _store.GetAsync(left, cancellationToken);
        var rightOperation = await _store.GetAsync(right, cancellationToken);
        if (leftOperation is null || rightOperation is null ||
            !CanReadOperation(user, leftOperation) || !CanReadOperation(user, rightOperation))
        {
            return null;
        }

        var leftArtifacts = leftOperation.Artifacts.Select(artifact => artifact.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightArtifacts = rightOperation.Artifacts.Select(artifact => artifact.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new OperationComparisonResponse(
            left,
            right,
            leftOperation.Status,
            rightOperation.Status,
            leftArtifacts.Except(rightArtifacts, StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            rightArtifacts.Except(leftArtifacts, StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            leftArtifacts.Intersect(rightArtifacts, StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            "DERIVED_COMPARISON");
    }

    private async Task DispatchAsync(
        OperationDefinition definition,
        EngineeringOperationRecord operation,
        CancellationToken cancellationToken)
    {
        operation.Status = "Queued";
        operation.UpdatedAt = DateTimeOffset.UtcNow;
        operation.Steps.Add(new OperationStepResponse(
            operation.Steps.Count + 1,
            "Dispatch",
            "Running",
            operation.UpdatedAt,
            $"Dispatching closed operation through {definition.Workflow}."));
        await _store.SaveAsync(operation, cancellationToken);

        var result = await _dispatcher.DispatchAsync(definition, operation, cancellationToken);
        operation.Status = result.Status;
        operation.Provider = result.Provider;
        operation.ProviderReference = result.ProviderReference;
        operation.EvidenceLevel = result.EvidenceLevel;
        operation.UpdatedAt = DateTimeOffset.UtcNow;
        operation.Steps.Add(new OperationStepResponse(
            operation.Steps.Count + 1,
            "Dispatch result",
            result.Status,
            operation.UpdatedAt,
            result.ProviderReference));
        if (!string.IsNullOrWhiteSpace(result.Limitation))
        {
            operation.Limitations.Add(result.Limitation);
        }
        await _store.SaveAsync(operation, cancellationToken);
    }

    private bool CanReadOperation(ClaimsPrincipal user, EngineeringOperationRecord operation)
    {
        var definition = _catalog.Find(operation.OperationId);
        return definition is null
            ? CanReadCategory(user, operation.Category)
            : CanReadCategory(user, operation.Category) ||
              definition.RequiresApproval && OperationRoleCatalog.HasCapability(user, OperationCapabilities.ApprovalReview);
    }

    private static bool CanReadCategory(ClaimsPrincipal user, string category) => category.ToLowerInvariant() switch
    {
        "quality" => OperationRoleCatalog.HasCapability(user, OperationCapabilities.QualityRead),
        "evidence" => OperationRoleCatalog.HasCapability(user, OperationCapabilities.EvidenceRead),
        "deployment" => OperationRoleCatalog.HasCapability(user, OperationCapabilities.DeploymentRead),
        "cloud" => OperationRoleCatalog.HasCapability(user, OperationCapabilities.CloudRead),
        _ => false
    };

    private static (Dictionary<string, string> Inputs, string? Error) ValidateInputs(
        OperationDefinition definition,
        IReadOnlyDictionary<string, string>? supplied)
    {
        var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allowed = definition.Inputs.Select(input => input.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in supplied ?? new Dictionary<string, string>())
        {
            if (!allowed.Contains(pair.Key))
            {
                return (inputs, $"Input '{pair.Key}' is not allowed for '{definition.OperationId}'.");
            }
            if (IsSecretKey(pair.Key))
            {
                return (inputs, $"Secret-like input '{pair.Key}' is forbidden.");
            }
            if (pair.Value is null || pair.Value.Length > 500 || pair.Value.Any(char.IsControl))
            {
                return (inputs, $"Input '{pair.Key}' is invalid or too long.");
            }
            inputs[pair.Key] = pair.Value.Trim();
        }

        foreach (var input in definition.Inputs)
        {
            if (!inputs.ContainsKey(input.Name) && input.DefaultValue is not null)
            {
                inputs[input.Name] = input.DefaultValue;
            }
            if (input.Required && (!inputs.TryGetValue(input.Name, out var value) || string.IsNullOrWhiteSpace(value)))
            {
                return (inputs, $"Input '{input.Name}' is required.");
            }
        }
        return (inputs, null);
    }

    private static string BuildConfirmation(
        OperationDefinition definition,
        string environment,
        IReadOnlyDictionary<string, string> inputs) => definition.ConfirmationPhrase
            .Replace("{environment}", environment, StringComparison.Ordinal)
            .Replace("{planHash}", inputs.GetValueOrDefault("planHash") ?? "<missing-plan-hash>", StringComparison.Ordinal);

    private static bool IsSecretKey(string key) =>
        new[] { "token", "password", "secret", "credential", "privatekey", "serviceaccountkey" }
            .Any(fragment => key.Replace("_", string.Empty).Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool IsSafeReference(string reference) => reference.Length <= 200 &&
        reference.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or '/');

    private static bool SecretsEqual(string? supplied, string expected)
    {
        if (supplied is null)
        {
            return false;
        }
        var left = Encoding.UTF8.GetBytes(supplied);
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static OperationServiceResult Success(EngineeringOperationRecord operation) =>
        new(operation.ToResponse(), 200, null);

    private static OperationServiceResult Error(int statusCode, string error) =>
        new(null, statusCode, error);
}
