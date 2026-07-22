using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Operations.Configuration;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class EngineeringOperationsServiceTests
{
    [Fact]
    public async Task StartAsync_RejectsUnknownOperationsUnsafeReferencesAndForbiddenInputs()
    {
        var service = CreateService();
        var user = Principal("qa-user", "QA");

        var missing = await service.StartAsync(
            new StartOperationRequest("missing", "ci", "master", null, false, null),
            user,
            CancellationToken.None);
        var unsafeRef = await service.StartAsync(
            new StartOperationRequest("frontend-fast", "ci", "refs/heads/main;rm", null, false, null),
            user,
            CancellationToken.None);
        var forbiddenInput = await service.StartAsync(
            new StartOperationRequest(
                "frontend-fast",
                "ci",
                "master",
                new Dictionary<string, string> { ["token"] = "secret" },
                false,
                null),
            user,
            CancellationToken.None);

        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(400, unsafeRef.StatusCode);
        Assert.Equal("Ref contains unsupported characters.", unsafeRef.Error);
        Assert.Equal(400, forbiddenInput.StatusCode);
        Assert.Contains("not allowed", forbiddenInput.Error);
    }

    [Fact]
    public async Task StartAsync_DispatchesImplementedOperationAndPersistsAuditableSteps()
    {
        var dispatcher = new RecordingDispatcher(new AutomationDispatchResult(
            "Queued",
            "simulation",
            "simulation://operation",
            "DEMONSTRATION_ONLY",
            "local simulation only"));
        var store = new MemoryOperationStore();
        var service = CreateService(store, dispatcher);

        var result = await service.StartAsync(
            new StartOperationRequest("frontend-fast", "ci", "refs/heads/feature", null, true, null),
            Principal("qa-user", "QA"),
            CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Operation);
        Assert.Equal("Queued", result.Operation.Status);
        Assert.Equal("simulation", result.Operation.Provider);
        Assert.Equal("DEMONSTRATION_ONLY", result.Operation.EvidenceLevel);
        Assert.Equal("refs/heads/feature", result.Operation.Ref);
        Assert.Equal("master", result.Operation.Inputs["ref"]);
        Assert.Contains("quality.execute.static", result.Operation.RequestedByCapabilities);
        Assert.Contains(result.Operation.Steps, step => step.Name == "Validated" && step.Status == "Succeeded");
        Assert.Contains(result.Operation.Steps, step => step.Name == "Dispatch result" && step.Status == "Queued");
        Assert.Single(dispatcher.Dispatches);
        Assert.NotNull(await store.GetAsync(result.Operation.Id, CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_RequiresExactConfirmationAndSupportedEnvironment()
    {
        var service = CreateService();
        var user = Principal("operator", "Operations");

        var wrongEnvironment = await service.StartAsync(
            new StartOperationRequest("staging-plan", "production", "master", null, false, "PLAN staging"),
            user,
            CancellationToken.None);
        var wrongConfirmation = await service.StartAsync(
            new StartOperationRequest("staging-plan", "staging", "master", null, false, "PLAN"),
            user,
            CancellationToken.None);

        Assert.Equal(400, wrongEnvironment.StatusCode);
        Assert.Contains("not allowed", wrongEnvironment.Error);
        Assert.Equal(400, wrongConfirmation.StatusCode);
        Assert.Equal("Exact confirmation required: PLAN staging", wrongConfirmation.Error);
    }

    [Fact]
    public async Task DecideAsync_ApprovesPendingOperationAndDispatchesWithSeparateReviewer()
    {
        var dispatcher = new RecordingDispatcher(new AutomationDispatchResult(
            "Queued",
            "github-actions",
            "https://github.test/run",
            "IMPLEMENTED_NOT_PROVED",
            "awaiting provider callback"));
        var store = new MemoryOperationStore();
        var service = CreateService(
            store,
            dispatcher,
            new OperationsOptions { AllowSelfApproval = false },
            "Production");
        var start = await service.StartAsync(
            new StartOperationRequest(
                "production-deploy",
                "production",
                "release/1",
                new Dictionary<string, string>
                {
                    ["stagingRunId"] = "run-123",
                    ["releaseName"] = "release-20260721"
                },
                true,
                "PROMOTE VERIFIED RELEASE TO PRODUCTION"),
            Principal("requester", "ReleaseApprover"),
            CancellationToken.None);

        Assert.Equal(200, start.StatusCode);
        Assert.Equal("AwaitingApproval", start.Operation!.Status);
        Assert.Empty(dispatcher.Dispatches);

        var decision = await service.DecideAsync(
            start.Operation.Id,
            new OperationDecisionRequest("approve", "reviewed evidence"),
            Principal("reviewer", "ReleaseApprover"),
            CancellationToken.None);

        Assert.Equal(200, decision.StatusCode);
        Assert.NotNull(decision.Operation);
        Assert.Equal("Queued", decision.Operation.Status);
        Assert.Single(decision.Operation.Approvals);
        Assert.Equal("approve", decision.Operation.Approvals[0].Decision);
        Assert.Equal("reviewed evidence", decision.Operation.Approvals[0].Comment);
        Assert.Single(dispatcher.Dispatches);
    }

    [Fact]
    public async Task ApplyCallbackAsync_AuthenticatesSecretAndPromotesOnlyVerifiableSucceededArtifacts()
    {
        var store = new MemoryOperationStore();
        var service = CreateService(
            store,
            new RecordingDispatcher(new AutomationDispatchResult("Queued", "simulation", null, "IMPLEMENTED_NOT_PROVED", null)),
            new OperationsOptions { CallbackSecret = "callback-secret" });
        var operation = new EngineeringOperationRecord
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            OperationId = "frontend-fast",
            Category = "quality",
            DisplayName = "Frontend fast",
            Status = "Queued",
            Environment = "ci",
            Ref = "master",
            RequestedBy = "qa",
            RequestedAt = DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture),
            Workflow = "_quality-operation.yml",
            EvidenceLevel = "IMPLEMENTED_NOT_PROVED"
        };
        await store.SaveAsync(operation, CancellationToken.None);

        var denied = await service.ApplyCallbackAsync(
            new OperationCallbackRequest(operation.Id, "Succeeded", null, null, "done"),
            "wrong",
            CancellationToken.None);
        var accepted = await service.ApplyCallbackAsync(
            new OperationCallbackRequest(
                operation.Id,
                "Succeeded",
                "https://github.test/actions/1",
                [
                    new OperationArtifactResponse(
                        "artifact-1",
                        "manifest",
                        "csv",
                        "artifacts/manifest.csv",
                        new string('a', 64),
                        123,
                        "HASHED"),
                    new OperationArtifactResponse(
                        "artifact-1",
                        "manifest",
                        "csv",
                        "artifacts/manifest.csv",
                        new string('a', 64),
                        123,
                        "HASHED")
                ],
                "done"),
            "callback-secret",
            CancellationToken.None);

        Assert.Equal(403, denied.StatusCode);
        Assert.Equal(200, accepted.StatusCode);
        Assert.Equal("Succeeded", accepted.Operation!.Status);
        Assert.Equal("PROVED_BY_HASHED_REPORTED_ARTIFACTS", accepted.Operation.EvidenceLevel);
        Assert.Single(accepted.Operation.Artifacts);
        Assert.Equal("https://github.test/actions/1", accepted.Operation.ProviderReference);
    }

    [Fact]
    public async Task CompareAsync_ReturnsArtifactSetDifferencesOnlyForReadableOperations()
    {
        var store = new MemoryOperationStore();
        var service = CreateService(store);
        var left = StoredOperation(Guid.Parse("11111111-2222-3333-4444-555555555555"), "quality", "frontend-fast");
        var right = StoredOperation(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "quality", "frontend-fast");
        left.Artifacts.Add(new OperationArtifactResponse("shared", "manifest.csv", "csv", "left/manifest.csv", new string('1', 64), 1, "HASHED"));
        left.Artifacts.Add(new OperationArtifactResponse("left", "stdout.log", "log", "left/stdout.log", new string('2', 64), 2, "HASHED"));
        right.Artifacts.Add(new OperationArtifactResponse("shared", "manifest.csv", "csv", "right/manifest.csv", new string('3', 64), 3, "HASHED"));
        right.Artifacts.Add(new OperationArtifactResponse("right", "stderr.log", "log", "right/stderr.log", new string('4', 64), 4, "HASHED"));
        await store.SaveAsync(left, CancellationToken.None);
        await store.SaveAsync(right, CancellationToken.None);

        var comparison = await service.CompareAsync(left.Id, right.Id, Principal("qa", "QA"), CancellationToken.None);
        var forbidden = await service.CompareAsync(left.Id, right.Id, Principal("sim", "Sim"), CancellationToken.None);

        Assert.NotNull(comparison);
        Assert.Equal(["stdout.log"], comparison.OnlyOnLeft);
        Assert.Equal(["stderr.log"], comparison.OnlyOnRight);
        Assert.Equal(["manifest.csv"], comparison.SharedArtifacts);
        Assert.Equal("DERIVED_COMPARISON", comparison.EvidenceLevel);
        Assert.Null(forbidden);
    }

    private static EngineeringOperationsService CreateService(
        MemoryOperationStore? store = null,
        RecordingDispatcher? dispatcher = null,
        OperationsOptions? options = null,
        string environmentName = "Development") => new(
            new OperationCatalog(),
            store ?? new MemoryOperationStore(),
            dispatcher ?? new RecordingDispatcher(new AutomationDispatchResult("Queued", "simulation", null, "DEMONSTRATION_ONLY", null)),
            Options.Create(options ?? new OperationsOptions()),
            new TestWebHostEnvironment { EnvironmentName = environmentName });

    private static ClaimsPrincipal Principal(string name, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, name), new(ClaimTypes.Name, name) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static EngineeringOperationRecord StoredOperation(Guid id, string category, string operationId) => new()
    {
        Id = id,
        OperationId = operationId,
        Category = category,
        DisplayName = operationId,
        Status = "Succeeded",
        Environment = "ci",
        Ref = "master",
        RequestedBy = "qa",
        RequestedAt = DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture),
        UpdatedAt = DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture)
    };

    private sealed class MemoryOperationStore : IOperationStore
    {
        private readonly Dictionary<Guid, EngineeringOperationRecord> _records = [];

        public Task SaveAsync(EngineeringOperationRecord operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _records[operation.Id] = operation;
            return Task.CompletedTask;
        }

        public Task<EngineeringOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_records.GetValueOrDefault(id));
        }

        public Task<IReadOnlyList<EngineeringOperationRecord>> ListAsync(
            string? category,
            int take,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<EngineeringOperationRecord>>(
                _records.Values
                    .Where(record => string.IsNullOrWhiteSpace(category) ||
                        string.Equals(record.Category, category, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(record => record.RequestedAt)
                    .Take(take)
                    .ToArray());
        }
    }

    private sealed class RecordingDispatcher(AutomationDispatchResult result) : IAutomationDispatcher
    {
        public List<(OperationDefinition Definition, EngineeringOperationRecord Operation)> Dispatches { get; } = [];

        public Task<AutomationDispatchResult> DispatchAsync(
            OperationDefinition definition,
            EngineeringOperationRecord operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dispatches.Add((definition, operation));
            return Task.FromResult(result);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
