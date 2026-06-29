using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/operations")]
[Authorize]
public sealed class ControlOperationsController : ControllerBase
{
    private readonly IEngineeringOperationsService _operations;

    public ControlOperationsController(IEngineeringOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<OperationDefinitionResponse>> ListCatalog([FromQuery] string? category) =>
        Ok(_operations.ListCatalog(User, category));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EngineeringOperationResponse>>> List(
        [FromQuery] string? category,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _operations.ListAsync(User, category, take, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EngineeringOperationResponse>> Get(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var operation = await _operations.GetAsync(id, User, cancellationToken);
        return operation is null ? NotFound() : Ok(operation);
    }

    [HttpPost]
    public async Task<ActionResult<EngineeringOperationResponse>> Start(
        [FromBody] StartOperationRequest request,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await _operations.StartAsync(request, User, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<EngineeringOperationResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await _operations.CancelAsync(id, User, cancellationToken));

    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<ActionResult<EngineeringOperationResponse>> Callback(
        [FromBody] OperationCallbackRequest request,
        [FromHeader(Name = "X-NatureProtector-Operations-Secret")] string? secret,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await _operations.ApplyCallbackAsync(request, secret, cancellationToken));

    private ActionResult<EngineeringOperationResponse> ToActionResult(OperationServiceResult result) =>
        result.Operation is not null
            ? StatusCode(result.StatusCode, result.Operation)
            : StatusCode(result.StatusCode, new { message = result.Error });
}
