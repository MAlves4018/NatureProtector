
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.UserPlane.Services;

[ApiController]
public abstract class UserAndRolesControllerBase : ControllerBase
{
    protected UserAndRolesControllerBase(IUserRolePlaneService userPlane)
    {
        UserPlane = userPlane;
    }

    protected IUserRolePlaneService UserPlane { get; }

    protected ActionResult? EnsureUserPlaneAvailable()
    {
        if (UserPlane.IsAvailable)
        {
            return null;
        }

        return Problem(
            detail: UserPlane.AvailabilityMessage,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "User plane unavailable");
    }
}