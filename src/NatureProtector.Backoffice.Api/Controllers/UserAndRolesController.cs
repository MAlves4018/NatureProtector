using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.UserPlane.Contracts;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Contracts;
using NatureProtector.Backoffice.Api.UserPlane.Services;

[Authorize]
[Route("api/users-roles")]
[ApiController]
public sealed class UserAndRolesController : UserAndRolesControllerBase
{
    public UserAndRolesController(IUserRolePlaneService userPlane)
        : base(userPlane)
    {

    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.LoginAsync(request, cancellationToken);
        if (response is null)
        {
            return Unauthorized();
        }

        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<ActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        await UserPlane.LogoutAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("users")]
    [Authorize(Policy = OperationCapabilities.UsersManage)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        return Ok(await UserPlane.ListUsersAsync(cancellationToken));
    }

    [HttpGet("roles")]
    [Authorize(Policy = OperationCapabilities.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        return Ok(await UserPlane.ListRolesAsync(cancellationToken));
    }

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> CreateUserAsync(
        [FromBody] UserRequest request,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.CreateUserAsync(request, cancellationToken);
        if (response is null)
        {
            return Conflict();
        }

        return Ok(response);
    }

    [HttpGet("users/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> GetUserAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.GetUserAsync(userId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPut("users/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> UpdateUserAsync(
        [FromRoute] Guid userId,
        [FromBody] UserRequest request,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.UpdateUserAsync(userId, request, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpDelete("users/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteUserAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var removed = await UserPlane.DeleteUserAsync(userId, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("roles")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoleResponse>> CreateRoleAsync(
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.CreateRoleAsync(request.Name, cancellationToken);
        if (response is null)
        {
            return Conflict();
        }

        return Ok(response);
    }

    [HttpGet("roles/{roleId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoleResponse>> GetRoleAsync(
        [FromRoute] short roleId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.GetRoleAsync(roleId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPut("roles/{roleId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoleResponse>> UpdateRoleAsync(
        [FromRoute] short roleId,
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.UpdateRoleAsync(roleId, request.Name, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpDelete("roles/{roleId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteRoleAsync(
        [FromRoute] short roleId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var removed = await UserPlane.DeleteRoleAsync(roleId, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("users/{userId:guid}/roles/{roleId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserRoleResponse>> AddRoleToUserAsync(
        [FromRoute] Guid userId,
        [FromRoute] short roleId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.AddRoleToUserAsync(userId, roleId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpDelete("users/{userId:guid}/roles/{roleId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserRoleResponse>> RemoveRoleFromUserAsync(
        [FromRoute] Guid userId,
        [FromRoute] short roleId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.RemoveRoleFromUserAsync(userId, roleId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpGet("roles/{roleId}/users")]
    [Authorize(Roles = "Admin")]

    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsersInRoleAsync(
        [FromRoute] short roleId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.GetUsersInRoleAsync(roleId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("users/{userId:guid}/roles")]
    public async Task<ActionResult<IEnumerable<RoleResponse>>> GetRolesForUserAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.GetRolesForUserAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("users/{userId:guid}/roles/{roleId}")]
    public async Task<ActionResult> CheckUserRoleAsync(
        [FromRoute] Guid userId,
        [FromRoute] short roleId,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var hasRole = await UserPlane.CheckUserRoleAsync(userId, roleId, cancellationToken);
        return hasRole ? Ok() : NotFound();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrentUserAsync([FromHeader] string? Authorization, CancellationToken cancellationToken)
    {
        var unavailable = EnsureUserPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var response = await UserPlane.GetCurrentUserAsync(Authorization, cancellationToken);
        return Ok(response);
    }

    [HttpGet("me/capabilities")]
    public ActionResult<CapabilityProfileResponse> GetCurrentCapabilities()
    {
        var roles = OperationRoleCatalog.GetRoles(User);
        var capabilities = OperationRoleCatalog.GetCapabilities(roles);
        return Ok(new CapabilityProfileResponse(
            roles,
            capabilities,
            "server-role-capability-policy",
            DateTimeOffset.UtcNow));
    }

}