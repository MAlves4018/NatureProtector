using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.Operations.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/cloud")]
[Authorize(Policy = OperationCapabilities.CloudRead)]
public sealed class ControlCloudOperationsController : ControllerBase
{
    private readonly IEngineeringOperationsService _operations;
    private readonly ICloudEnvironmentCatalogService _environments;

    public ControlCloudOperationsController(
        IEngineeringOperationsService operations,
        ICloudEnvironmentCatalogService environments)
    {
        _operations = operations;
        _environments = environments;
    }

    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<OperationDefinitionResponse>> Catalog() =>
        Ok(_operations.ListCatalog(User, "cloud"));

    [HttpGet("environments")]
    public ActionResult<IReadOnlyList<CloudEnvironmentResponse>> Environments() =>
        Ok(_environments.List());

    [HttpGet("environments/{environment}/resources")]
    public ActionResult<CloudEnvironmentResponse> Resources(string environment)
    {
        var result = _environments.Get(environment);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("operations")]
    public async Task<ActionResult<IReadOnlyList<EngineeringOperationResponse>>> Operations(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _operations.ListAsync(User, "cloud", take, cancellationToken));

    [HttpPost("environments/{environment}/operations")]
    public async Task<ActionResult> Start(
        string environment,
        [FromBody] StartOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Environment = environment };
        var result = await _operations.StartAsync(normalized, User, cancellationToken);
        return result.Operation is not null
            ? StatusCode(result.StatusCode, result.Operation)
            : StatusCode(result.StatusCode, new { message = result.Error });
    }
}
