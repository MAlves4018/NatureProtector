using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Controllers;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ControlOperationControllersTests
{
    [Fact]
    public void DeploymentCatalog_RequestsDeploymentDefinitions()
    {
        var operations = new RecordingOperationsService();
        var controller = new ControlDeploymentsController(operations);

        var result = Assert.IsType<OkObjectResult>(controller.Catalog().Result);

        Assert.Same(operations.CatalogResponses, result.Value);
        Assert.Equal("deployment", operations.LastCatalogCategory);
    }

    [Fact]
    public async Task DeploymentStart_NormalizesOperationIdAndKeepsRouteEnvironment()
    {
        var operations = new RecordingOperationsService();
        var controller = new ControlDeploymentsController(operations);

        var result = Assert.IsType<ObjectResult>(await controller.Start(
            " Staging ",
            " Deploy ",
            new StartOperationRequest("ignored", "ignored", "main", null, CollectEvidence: true, null),
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.Equal("staging-deploy", operations.LastStartRequest?.OperationId);
        Assert.Equal(" Staging ", operations.LastStartRequest?.Environment);
        Assert.True(operations.LastStartRequest?.CollectEvidence);
    }

    [Fact]
    public async Task QualityStart_ForcesCiEnvironment()
    {
        var operations = new RecordingOperationsService();
        var controller = new ControlQualityController(operations);

        var result = Assert.IsType<ObjectResult>(await controller.Start(
            new StartOperationRequest("quality-smoke", "production", "main", null, CollectEvidence: false, null),
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.Equal("quality-smoke", operations.LastStartRequest?.OperationId);
        Assert.Equal("ci", operations.LastStartRequest?.Environment);
    }

    [Fact]
    public async Task EvidenceEndpoints_PassThroughCategoryStartAndComparisonResults()
    {
        var operations = new RecordingOperationsService();
        var controller = new ControlEvidenceOperationsController(operations);

        var catalog = Assert.IsType<OkObjectResult>(controller.Catalog().Result);
        var campaigns = Assert.IsType<OkObjectResult>((await controller.Campaigns(7, CancellationToken.None)).Result);
        var started = Assert.IsType<ObjectResult>(await controller.Start(
            new StartOperationRequest("evidence-smoke", "local", "head", null, CollectEvidence: true, null),
            CancellationToken.None));
        var compared = Assert.IsType<OkObjectResult>((await controller.Compare(
            operations.Comparison.LeftOperationId,
            operations.Comparison.RightOperationId,
            CancellationToken.None)).Result);

        Assert.Same(operations.CatalogResponses, catalog.Value);
        Assert.Same(operations.ListResponses, campaigns.Value);
        Assert.Same(operations.Operation, started.Value);
        Assert.Same(operations.Comparison, compared.Value);
        Assert.Equal(StatusCodes.Status202Accepted, started.StatusCode);
        Assert.Equal("evidence", operations.LastCatalogCategory);
        Assert.Equal("evidence", operations.LastListCategory);
        Assert.Equal(7, operations.LastListTake);
        Assert.Equal("evidence-smoke", operations.LastStartRequest?.OperationId);
    }

    [Fact]
    public async Task EvidenceCompare_ReturnsNotFound_WhenServiceHasNoComparison()
    {
        var operations = new RecordingOperationsService { ComparisonResponse = null };
        var controller = new ControlEvidenceOperationsController(operations);

        var result = (await controller.Compare(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None)).Result;

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovalsPending_ReturnsOnlyAwaitingApprovalOperations()
    {
        var awaiting = Operation(status: "AwaitingApproval");
        var succeeded = Operation(status: "Succeeded");
        var operations = new RecordingOperationsService
        {
            ListResponses = [awaiting, succeeded]
        };
        var controller = new ControlApprovalsController(operations);

        var result = Assert.IsType<OkObjectResult>((await controller.Pending(20, CancellationToken.None)).Result);
        var pending = Assert.IsType<EngineeringOperationResponse[]>(result.Value);

        var operation = Assert.Single(pending);
        Assert.Same(awaiting, operation);
        Assert.Null(operations.LastListCategory);
        Assert.Equal(20, operations.LastListTake);
    }

    [Fact]
    public async Task ApprovalDecision_MapsServiceSuccessAndErrorsToStatusCodes()
    {
        var operationId = Guid.NewGuid();
        var operations = new RecordingOperationsService();
        var controller = new ControlApprovalsController(operations);

        var accepted = Assert.IsType<ObjectResult>(await controller.Decide(
            operationId,
            new OperationDecisionRequest("approve", "ship it"),
            CancellationToken.None));

        operations.DecideResponse = new OperationServiceResult(null, StatusCodes.Status409Conflict, "not awaiting approval");
        var rejected = Assert.IsType<ObjectResult>(await controller.Decide(
            operationId,
            new OperationDecisionRequest("approve", "again"),
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Same(operations.Operation, accepted.Value);
        Assert.Equal(operationId, operations.LastDecisionId);
        Assert.Equal("approve", operations.LastDecisionRequest?.Decision);
        Assert.Equal(StatusCodes.Status409Conflict, rejected.StatusCode);
        Assert.NotNull(rejected.Value);
    }

    [Fact]
    public async Task CloudEndpoints_ReturnCatalogResourcesOperationsAndNormalizedStart()
    {
        var operations = new RecordingOperationsService();
        var environments = new RecordingCloudEnvironmentCatalogService();
        var controller = new ControlCloudOperationsController(operations, environments);

        var catalog = Assert.IsType<OkObjectResult>(controller.Catalog().Result);
        var listed = Assert.IsType<OkObjectResult>(controller.Environments().Result);
        var resources = Assert.IsType<OkObjectResult>(controller.Resources("Staging").Result);
        var operationsResult = Assert.IsType<OkObjectResult>((await controller.Operations(3, CancellationToken.None)).Result);
        var started = Assert.IsType<ObjectResult>(await controller.Start(
            " Production ",
            new StartOperationRequest("cloud-inventory", "ignored", "main", null, CollectEvidence: true, null),
            CancellationToken.None));

        Assert.Same(operations.CatalogResponses, catalog.Value);
        Assert.Same(environments.Responses, listed.Value);
        Assert.Same(environments.Responses[0], resources.Value);
        Assert.Same(operations.ListResponses, operationsResult.Value);
        Assert.Same(operations.Operation, started.Value);
        Assert.Equal("cloud", operations.LastCatalogCategory);
        Assert.Equal("cloud", operations.LastListCategory);
        Assert.Equal(3, operations.LastListTake);
        Assert.Equal("Staging", environments.LastGetEnvironment);
        Assert.Equal(" Production ", operations.LastStartRequest?.Environment);
    }

    [Fact]
    public void CloudResources_ReturnsNotFoundForUnknownEnvironment()
    {
        var controller = new ControlCloudOperationsController(
            new RecordingOperationsService(),
            new RecordingCloudEnvironmentCatalogService { GetResponse = null });

        var result = controller.Resources("unknown").Result;

        Assert.IsType<NotFoundResult>(result);
    }

    private static EngineeringOperationResponse Operation(string status = "Validated") => new(
        Guid.NewGuid(),
        "operation-id",
        "quality",
        "Operation",
        status,
        "ci",
        "main",
        "tester",
        ["admin"],
        ["quality:run"],
        new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 21, 12, 0, 1, TimeSpan.Zero),
        CollectEvidence: true,
        "low",
        RequiresApproval: false,
        Provider: null,
        ProviderReference: null,
        Workflow: "workflow",
        PlanHash: null,
        "runtime",
        new Dictionary<string, string>(),
        [],
        [],
        [],
        []);

    private sealed class RecordingOperationsService : IEngineeringOperationsService
    {
        public IReadOnlyList<OperationDefinitionResponse> CatalogResponses { get; } =
        [
            new(
                "quality-smoke",
                "quality",
                "Quality smoke",
                "Runs a quality smoke suite.",
                "quality:run",
                "low",
                RequiresConfirmation: false,
                RequiresApproval: false,
                ["ci"],
                [],
                "workflow",
                "CONFIRM",
                Authorized: true,
                "implemented",
                "runtime",
                null)
        ];

        public IReadOnlyList<EngineeringOperationResponse> ListResponses { get; init; } = [Operation()];

        public EngineeringOperationResponse Operation { get; } = Operation();

        public OperationComparisonResponse Comparison { get; } = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Succeeded",
            "Succeeded",
            ["left-only"],
            ["right-only"],
            ["shared"],
            "runtime");

        public OperationComparisonResponse? ComparisonResponse { get; init; }

        public OperationServiceResult DecideResponse { get; set; }

        public string? LastCatalogCategory { get; private set; }

        public string? LastListCategory { get; private set; }

        public int LastListTake { get; private set; }

        public StartOperationRequest? LastStartRequest { get; private set; }

        public Guid LastDecisionId { get; private set; }

        public OperationDecisionRequest? LastDecisionRequest { get; private set; }

        public RecordingOperationsService()
        {
            ComparisonResponse = Comparison;
            DecideResponse = new OperationServiceResult(Operation, StatusCodes.Status202Accepted, null);
        }

        public IReadOnlyList<OperationDefinitionResponse> ListCatalog(ClaimsPrincipal user, string? category)
        {
            LastCatalogCategory = category;
            return CatalogResponses;
        }

        public Task<IReadOnlyList<EngineeringOperationResponse>> ListAsync(
            ClaimsPrincipal user,
            string? category,
            int take,
            CancellationToken cancellationToken)
        {
            LastListCategory = category;
            LastListTake = take;
            return Task.FromResult(ListResponses);
        }

        public Task<EngineeringOperationResponse?> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            Task.FromResult<EngineeringOperationResponse?>(Operation);

        public Task<OperationServiceResult> StartAsync(
            StartOperationRequest request,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            LastStartRequest = request;
            return Task.FromResult(new OperationServiceResult(Operation, StatusCodes.Status202Accepted, null));
        }

        public Task<OperationServiceResult> DecideAsync(
            Guid id,
            OperationDecisionRequest request,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            LastDecisionId = id;
            LastDecisionRequest = request;
            return Task.FromResult(DecideResponse);
        }

        public Task<OperationServiceResult> CancelAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            Task.FromResult(new OperationServiceResult(Operation, StatusCodes.Status202Accepted, null));

        public Task<OperationServiceResult> ApplyCallbackAsync(
            OperationCallbackRequest request,
            string? secret,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OperationServiceResult(Operation, StatusCodes.Status202Accepted, null));

        public Task<OperationComparisonResponse?> CompareAsync(
            Guid left,
            Guid right,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Task.FromResult(ComparisonResponse);
    }

    private sealed class RecordingCloudEnvironmentCatalogService : ICloudEnvironmentCatalogService
    {
        public IReadOnlyList<CloudEnvironmentResponse> Responses { get; } =
        [
            new(
                "staging",
                "np-project",
                "europe-southwest1",
                Deployable: true,
                "deploy/environments/staging.json",
                "DeclaredNotObserved",
                "IMPLEMENTED_NOT_PROVED",
                [],
                [])
        ];

        public CloudEnvironmentResponse? GetResponse { get; init; }

        public string? LastGetEnvironment { get; private set; }

        public RecordingCloudEnvironmentCatalogService()
        {
            GetResponse = Responses[0];
        }

        public IReadOnlyList<CloudEnvironmentResponse> List() => Responses;

        public CloudEnvironmentResponse? Get(string environment)
        {
            LastGetEnvironment = environment;
            return GetResponse;
        }
    }
}
