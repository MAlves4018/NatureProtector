using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
public abstract class ControlPlaneControllerBase : ControllerBase
{
    protected ControlPlaneControllerBase(IControlPlaneService controlPlane)
    {
        ControlPlane = controlPlane;
    }

    protected IControlPlaneService ControlPlane { get; }

    protected ActionResult? EnsureControlPlaneAvailable()
    {
        if (ControlPlane.IsAvailable)
        {
            return null;
        }

        return Problem(
            detail: ControlPlane.AvailabilityMessage,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Control plane unavailable");
    }
}
