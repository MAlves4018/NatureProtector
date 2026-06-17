using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[Route("api/control/runtime")]
[Authorize (Roles = "Sim,Pipeline,Admin")]
public sealed class ControlRuntimeController : ControlPlaneControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public ControlRuntimeController(
        IControlPlaneService controlPlane,
        IWebHostEnvironment environment)
        : base(controlPlane)
    {
        _environment = environment;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(RuntimeSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> GetSummary(
        [FromQuery] string? areaCode,
        [FromQuery] int recentMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var summary = await ControlPlane.GetRuntimeSummaryAsync(areaCode, recentMinutes, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("diagnostics")]
    [ProducesResponseType(typeof(RuntimeDiagnosticCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> ListDiagnostics(CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        return Ok(await ControlPlane.ListRuntimeDiagnosticsAsync(cancellationToken));
    }

    [HttpPost("diagnostics/{diagnosticId}")]
    [Authorize(Roles = "Sim,Admin")]
    [ProducesResponseType(typeof(RuntimeDiagnosticResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> ExecuteDiagnostic(
        string diagnosticId,
        [FromBody] RuntimeDiagnosticRequest? request,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var result = await ControlPlane.ExecuteRuntimeDiagnosticAsync(
            diagnosticId,
            request ?? new RuntimeDiagnosticRequest(),
            cancellationToken);

        return result is null ? NotFound(new { message = $"Unknown runtime diagnostic '{diagnosticId}'." }) : Ok(result);
    }

    [HttpPost("runs")]
    [Authorize(Roles = "Sim,Admin")]
    [ProducesResponseType(typeof(RuntimeRunStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RuntimeRunStartResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> StartRun(
        [FromBody] RuntimeRunStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        if (!_environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Runtime run orchestration is only available in Development." });
        }

        var result = await ControlPlane.StartRuntimeRunAsync(request, cancellationToken);
        return result.Status == "Rejected" ? BadRequest(result) : Ok(result);
    }

    [HttpGet("runs/latest")]
    [ProducesResponseType(typeof(RuntimeRunSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetLatestRun(
        [FromQuery] string? areaCode,
        CancellationToken cancellationToken = default)
    {
        var runs = await ControlPlane.ListSimulationRunsAsync(areaCode, null, null, 0, 1, cancellationToken);
        var run = runs.FirstOrDefault();
        return run is null ? NotFound(new { message = "No simulation run found." }) : Ok(run);
    }

    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(RuntimeRunSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetRun(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await ControlPlane.GetSimulationRunAsync(runId, cancellationToken);
        return run is null ? NotFound(new { message = $"Simulation run '{runId}' was not found." }) : Ok(run);
    }

    [HttpGet("runs/{runId:guid}/audit")]
    [ProducesResponseType(typeof(RuntimeRunAuditResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetRunAudit(Guid runId, CancellationToken cancellationToken = default)
    {
        var audit = await ControlPlane.GetRuntimeRunAuditAsync(runId, cancellationToken);
        return audit is null ? NotFound(new { message = $"Simulation run '{runId}' was not found." }) : Ok(audit);
    }

    [HttpGet("runs/{runId:guid}/timings")]
    [ProducesResponseType(typeof(RuntimeRunTimingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetRunTimings(Guid runId, CancellationToken cancellationToken = default)
    {
        var timings = await ControlPlane.GetRuntimeRunTimingsAsync(runId, cancellationToken);
        return timings is null ? NotFound(new { message = $"Simulation run '{runId}' was not found." }) : Ok(timings);
    }

    [HttpPost("reset")]
    [Authorize(Roles = "Sim,Admin")]
    [ProducesResponseType(typeof(RuntimeResetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RuntimeResetResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> ResetRuntimeState(
        [FromBody] RuntimeResetRequest request,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        if (!_environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Runtime reset is only available in Development." });
        }

        var result = await ControlPlane.ResetRuntimeStateAsync(request, cancellationToken);
        return result.Status == "Rejected" ? BadRequest(result) : Ok(result);
    }
}
