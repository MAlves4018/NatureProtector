using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Operations.Authorization;

namespace NatureProtector.Backoffice.Api.Controllers;

[Route("api/control/configurations")]
[Authorize]
public sealed class ControlConfigurationsController : ControlPlaneControllerBase
{
    public ControlConfigurationsController(IControlPlaneService controlPlane)
        : base(controlPlane)
    {
    }

    [HttpGet]
    [Authorize(Policy = OperationCapabilities.AdminRead)] 
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
    [Authorize(Policy = OperationCapabilities.AdminRead)]
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
    [Authorize(Policy = OperationCapabilities.AdminExecute)]
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
