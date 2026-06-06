using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[Route("api/dev/controlled-validation")]
[Authorize(Roles = "Sim,Admin")]
public sealed class DevControlledValidationController : ControlPlaneControllerBase
{
    private const string Phase = "P3NegativePipeline";
    private const int MessageCount = 11;
    private const int ExecutableCases = 10;
    private const int BlockedCases = 2;

    private readonly IWebHostEnvironment _environment;

    public DevControlledValidationController(
        IControlPlaneService controlPlane,
        IWebHostEnvironment environment)
        : base(controlPlane)
    {
        _environment = environment;
    }

    [HttpGet("p3")]
    public ActionResult<ControlledValidationP3AvailabilityResponse> GetP3Availability()
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var available = IsAllowedEnvironment(_environment.EnvironmentName);
        return Ok(new ControlledValidationP3AvailabilityResponse(
            Phase,
            _environment.EnvironmentName,
            available,
            available
                ? "Controlled validation P3 execution is available in this environment."
                : "Controlled validation P3 execution is only available in Development or Evidence.",
            MessageCount,
            ExecutableCases,
            BlockedCases));
    }

    [HttpPost("p3/run")]
    public async Task<ActionResult<ControlledValidationP3RunResponse>> RunP3(
        [FromBody] ControlledValidationP3RunRequest? request,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        if (!IsAllowedEnvironment(_environment.EnvironmentName))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Controlled validation P3 execution is only available in Development or Evidence.",
                    environment = _environment.EnvironmentName
                });
        }

        var result = await ControlPlane.StartControlledValidationP3Async(
            request ?? new ControlledValidationP3RunRequest(),
            _environment.EnvironmentName,
            cancellationToken);

        return result.Status is "Rejected" or "Blocked" or "Failed"
            ? BadRequest(result)
            : Ok(result);
    }

    private static bool IsAllowedEnvironment(string? environmentName)
        => string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(environmentName, "Evidence", StringComparison.OrdinalIgnoreCase);
}
