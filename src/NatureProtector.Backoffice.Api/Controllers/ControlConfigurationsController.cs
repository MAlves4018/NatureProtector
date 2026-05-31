using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[Route("api/control/configurations")]
[Authorize (Roles = "Sim,Pipeline,Admin")]
public sealed class ControlConfigurationsController : ControlPlaneControllerBase
{
    public ControlConfigurationsController(IControlPlaneService controlPlane)
        : base(controlPlane)
    {
    }

    [HttpGet]
    public async Task<ActionResult> ListConfigurations(CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var configurations = await ControlPlane.ListConfigurationsAsync(cancellationToken);
        return Ok(configurations);
    }

    [HttpGet("active")]
    public async Task<ActionResult> GetActiveConfiguration(CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var configuration = await ControlPlane.GetActiveConfigurationAsync(cancellationToken);
        return configuration is null ? NotFound() : Ok(configuration);
    }

    [HttpPost("{versionNumber:int}/activate")]
    [Authorize(Roles = "Sim,Admin")]
    public async Task<ActionResult> ActivateConfiguration(int versionNumber, CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var configuration = await ControlPlane.ActivateConfigurationAsync(versionNumber, cancellationToken);
        return configuration is null ? NotFound() : Ok(configuration);
    }
}
