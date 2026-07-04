using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/approvals")]
[Authorize(Policy = OperationCapabilities.ApprovalReview)]
public sealed class ControlApprovalsController : ControllerBase
{
    private readonly IEngineeringOperationsService _operations;

    public ControlApprovalsController(IEngineeringOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EngineeringOperationResponse>>> Pending(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var all = await _operations.ListAsync(User, null, take, cancellationToken);
        return Ok(all.Where(operation => operation.Status == "AwaitingApproval").ToArray());
    }

    [HttpPost("{operationId:guid}/decision")]
    public async Task<ActionResult> Decide(
        Guid operationId,
        [FromBody] OperationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _operations.DecideAsync(operationId, request, User, cancellationToken);
        return result.Operation is not null
            ? StatusCode(result.StatusCode, result.Operation)
            : StatusCode(result.StatusCode, new { message = result.Error });
    }
}
