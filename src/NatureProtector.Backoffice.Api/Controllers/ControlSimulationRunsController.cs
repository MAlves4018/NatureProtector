using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Operations.Authorization;

namespace NatureProtector.Backoffice.Api.Controllers;

[Route("api/control/simulation-runs")]
[Authorize]
public sealed class ControlSimulationRunsController : ControlPlaneControllerBase
{
    public ControlSimulationRunsController(IControlPlaneService controlPlane)
        : base(controlPlane)
    {
    }

    [HttpGet]
    [Authorize(Policy = OperationCapabilities.SimulationRead)]
    public async Task<ActionResult> ListSimulationRuns(
        [FromQuery] string? areaCode,
        [FromQuery] string? scenarioCode,
        [FromQuery] int? configurationVersion,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var runs = await ControlPlane.ListSimulationRunsAsync(
            areaCode,
            scenarioCode,
            configurationVersion,
            skip,
            take,
            cancellationToken);

        return Ok(runs);
    }

    [HttpGet("{runId:guid}")]
    [Authorize(Policy = OperationCapabilities.SimulationRead)]
    public async Task<ActionResult> GetSimulationRun(Guid runId, CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var run = await ControlPlane.GetSimulationRunAsync(runId, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }
}
