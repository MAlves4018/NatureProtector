using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Services;
using NatureProtector.Backoffice.Api.Operations.Authorization;

namespace NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Controllers;

[ApiController]
[Route("api/control/data-explorer")]
public class ControlDataExplorerController : ControllerBase
{
    private readonly IReadOnlyDataExplorerService _service;

    public ControlDataExplorerController(IReadOnlyDataExplorerService service)
    {
        _service = service;
    }

    [Authorize(Policy = OperationCapabilities.DBRead)]
    [HttpGet("datasets")]
    public async Task<ActionResult<IReadOnlyList<DatasetDefinition>>> ListDatasets(CancellationToken cancellationToken)
    {
        var datasets = await _service.ListDatasetsAsync(cancellationToken);
        return Ok(datasets);
    }

    [Authorize(Policy = OperationCapabilities.DBRead)]
    [HttpPost("query")]
    public async Task<ActionResult<DataExplorerQueryResponse>> Query(
        DataExplorerQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.QueryAsync(request, cancellationToken);
        if (result is null)
        {
            return BadRequest(new { error = $"Unknown dataset '{request.DatasetId}'." });
        }

        return Ok(result);
    }
}
