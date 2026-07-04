using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/deployments")]
[Authorize(Policy = OperationCapabilities.DeploymentRead)]
public sealed class ControlDeploymentsController : ControllerBase
{
    private readonly IEngineeringOperationsService _operations;

    public ControlDeploymentsController(IEngineeringOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<OperationDefinitionResponse>> Catalog() =>
        Ok(_operations.ListCatalog(User, "deployment"));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EngineeringOperationResponse>>> List(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _operations.ListAsync(User, "deployment", take, cancellationToken));

    [HttpPost("{environment}/{deploymentAction}")]
    public async Task<ActionResult> Start(
        string environment,
        string deploymentAction,
        [FromBody] StartOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var operationId = $"{environment.Trim().ToLowerInvariant()}-{deploymentAction.Trim().ToLowerInvariant()}";
        var normalized = request with { OperationId = operationId, Environment = environment };
        var result = await _operations.StartAsync(normalized, User, cancellationToken);
        return result.Operation is not null
            ? StatusCode(result.StatusCode, result.Operation)
            : StatusCode(result.StatusCode, new { message = result.Error });
    }
}
