using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/quality")]
[Authorize(Policy = OperationCapabilities.QualityRead)]
public sealed class ControlQualityController : ControllerBase
{
    private readonly IEngineeringOperationsService _operations;

    public ControlQualityController(IEngineeringOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet("suites")]
    public ActionResult<IReadOnlyList<OperationDefinitionResponse>> Suites() =>
        Ok(_operations.ListCatalog(User, "quality"));

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<EngineeringOperationResponse>>> Runs(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _operations.ListAsync(User, "quality", take, cancellationToken));

    [HttpPost("runs")]
    public async Task<ActionResult> Start(
        [FromBody] StartOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Environment = "ci" };
        var result = await _operations.StartAsync(normalized, User, cancellationToken);
        return result.Operation is not null
            ? StatusCode(result.StatusCode, result.Operation)
            : StatusCode(result.StatusCode, new { message = result.Error });
    }
}
