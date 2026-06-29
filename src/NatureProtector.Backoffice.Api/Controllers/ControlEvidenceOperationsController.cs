using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/evidence")]
[Authorize(Policy = OperationCapabilities.EvidenceRead)]
public sealed class ControlEvidenceOperationsController : ControllerBase
{
    private readonly IEngineeringOperationsService _operations;

    public ControlEvidenceOperationsController(IEngineeringOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet("campaigns/catalog")]
    public ActionResult<IReadOnlyList<OperationDefinitionResponse>> Catalog() =>
        Ok(_operations.ListCatalog(User, "evidence"));

    [HttpGet("campaigns")]
    public async Task<ActionResult<IReadOnlyList<EngineeringOperationResponse>>> Campaigns(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _operations.ListAsync(User, "evidence", take, cancellationToken));

    [HttpPost("campaigns")]
    public async Task<ActionResult> Start(
        [FromBody] StartOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _operations.StartAsync(request, User, cancellationToken);
        return result.Operation is not null
            ? StatusCode(result.StatusCode, result.Operation)
            : StatusCode(result.StatusCode, new { message = result.Error });
    }

    [HttpGet("compare")]
    [Authorize(Policy = OperationCapabilities.EvidenceCompare)]
    public async Task<ActionResult<OperationComparisonResponse>> Compare(
        [FromQuery] Guid left,
        [FromQuery] Guid right,
        CancellationToken cancellationToken = default)
    {
        var comparison = await _operations.CompareAsync(left, right, User, cancellationToken);
        return comparison is null ? NotFound() : Ok(comparison);
    }
}
