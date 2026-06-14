using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[ApiController]
[Route("api/control/runtime/observability")]
[Authorize(Roles = "Sim,Pipeline,Admin")]
public sealed class ControlRuntimeObservabilityController : ControllerBase
{
    private readonly IRuntimeObservabilityService _observability;

    public ControlRuntimeObservabilityController(IRuntimeObservabilityService observability)
    {
        _observability = observability;
    }

    [HttpGet("health")]
    public async Task<ActionResult> GetOperationalHealth(CancellationToken cancellationToken = default)
    {
        if (!_observability.IsAvailable)
        {
            return ObservabilityUnavailable();
        }

        return Ok(await _observability.GetOperationalHealthAsync(cancellationToken));
    }

    [HttpGet("rabbitmq")]
    public async Task<ActionResult> GetRabbitMqMetrics(CancellationToken cancellationToken = default)
    {
        if (!_observability.IsAvailable)
        {
            return ObservabilityUnavailable();
        }

        return Ok(await _observability.GetRabbitMqMetricsAsync(cancellationToken));
    }

    [HttpGet("evidence")]
    public async Task<ActionResult> ListEvidence(CancellationToken cancellationToken = default)
    {
        if (!_observability.IsAvailable)
        {
            return ObservabilityUnavailable();
        }

        return Ok(await _observability.ListEvidenceAsync(cancellationToken));
    }

    [HttpGet("evidence/{evidenceId}")]
    public async Task<ActionResult> GetEvidenceContent(string evidenceId, CancellationToken cancellationToken = default)
    {
        if (!_observability.IsAvailable)
        {
            return ObservabilityUnavailable();
        }

        if (evidenceId.Contains("..", StringComparison.Ordinal) ||
            evidenceId.Contains('\\', StringComparison.Ordinal) ||
            evidenceId.Contains('/', StringComparison.Ordinal))
        {
            return BadRequest(new { message = "EvidenceId is invalid." });
        }

        var evidence = await _observability.GetEvidenceContentAsync(evidenceId, cancellationToken);
        if (evidence is null)
        {
            return NotFound(new { message = $"Evidence '{evidenceId}' was not found or is not available for HTTP content." });
        }

        Response.Headers.CacheControl = evidence.CacheControl;
        Response.Headers["X-NatureProtector-Evidence-Id"] = evidence.Metadata.EvidenceId;
        return File(evidence.Content, evidence.ContentType);
    }

    private ObjectResult ObservabilityUnavailable()
        => StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                title = "Runtime observability unavailable",
                status = StatusCodes.Status503ServiceUnavailable,
                detail = _observability.AvailabilityMessage,
                traceId = HttpContext.TraceIdentifier
            });
}
