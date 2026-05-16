using Microsoft.AspNetCore.Mvc;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Controllers;

[Route("api/control/areas")]
public sealed class ControlAreasController : ControlPlaneControllerBase
{
    public ControlAreasController(IControlPlaneService controlPlane)
        : base(controlPlane)
    {
    }

    [HttpGet]
    public async Task<ActionResult> ListAreas(
        [FromQuery] int? configurationVersion,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var areas = await ControlPlane.ListAreasAsync(configurationVersion, cancellationToken);
        return Ok(areas);
    }

    [HttpGet("{areaCode}")]
    public async Task<ActionResult> GetArea(
        string areaCode,
        [FromQuery] int? configurationVersion,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var area = await ControlPlane.GetAreaAsync(areaCode, configurationVersion, cancellationToken);
        return area is null ? NotFound() : Ok(area);
    }

    [HttpGet("{areaCode}/GeoJSON")]
    public async Task<ActionResult> GetAreaGeoJSON(
        string areaCode,
        [FromQuery] int? configurationVersion,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var geoJson = await ControlPlane.GetAreaGeoJSONAsync(areaCode, configurationVersion, cancellationToken);
        return geoJson is null ? NotFound() : Ok(geoJson);
    }

    [HttpGet("{areaCode}/grid-cells")]
    public async Task<ActionResult> ListGridCells(
        string areaCode,
        [FromQuery] int? configurationVersion,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 500,
        CancellationToken cancellationToken = default)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var cells = await ControlPlane.ListGridCellsAsync(areaCode, configurationVersion, skip, take, cancellationToken);
        return Ok(cells);
    }

    [HttpGet("{areaCode}/sensor-nodes")]
    public async Task<ActionResult> ListSensorNodes(
        string areaCode,
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

        var sensorNodes = await ControlPlane.ListSensorNodesAsync(areaCode, configurationVersion, skip, take, cancellationToken);
        return Ok(sensorNodes);
    }

    [HttpGet("{areaCode}/scenarios")]
    public async Task<ActionResult> ListScenarios(
        string areaCode,
        [FromQuery] int? configurationVersion,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var scenarios = await ControlPlane.ListScenariosAsync(areaCode, configurationVersion, cancellationToken);
        return Ok(scenarios);
    }

    [HttpGet("{areaCode}/operational-state")]
    public async Task<ActionResult> GetOperationalState(
        string areaCode,
        [FromQuery] int? configurationVersion,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var state = await ControlPlane.GetAreaOperationalStateAsync(areaCode, configurationVersion, cancellationToken);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpGet("{areaCode}/cells/operational-state")]
    public async Task<ActionResult> ListCellOperationalStates(
        string areaCode,
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

        var states = await ControlPlane.ListCellOperationalStatesAsync(
            areaCode,
            configurationVersion,
            skip,
            take,
            cancellationToken);

        return Ok(states);
    }

    [HttpGet("{areaCode}/alerts/active")]
    public async Task<ActionResult> ListActiveAlerts(
        string areaCode,
        [FromQuery] int? configurationVersion,
        CancellationToken cancellationToken)
    {
        var unavailable = EnsureControlPlaneAvailable();
        if (unavailable is not null)
        {
            return unavailable;
        }

        var alerts = await ControlPlane.ListActiveAlertsAsync(areaCode, configurationVersion, cancellationToken);
        return Ok(alerts);
    }
}
