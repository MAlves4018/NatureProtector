using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class UnavailableControlPlaneService : IControlPlaneService
{
    public UnavailableControlPlaneService(string availabilityMessage)
    {
        AvailabilityMessage = availabilityMessage;
    }

    public bool IsAvailable => false;

    public string AvailabilityMessage { get; }

    public Task<IReadOnlyList<ConfigurationVersionResponse>> ListConfigurationsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ConfigurationVersionResponse>>([]);

    public Task<ConfigurationVersionResponse?> GetActiveConfigurationAsync(CancellationToken cancellationToken)
        => Task.FromResult<ConfigurationVersionResponse?>(null);

    public Task<ConfigurationVersionResponse?> ActivateConfigurationAsync(int versionNumber, CancellationToken cancellationToken)
        => Task.FromResult<ConfigurationVersionResponse?>(null);

    public Task<IReadOnlyList<AreaSummaryResponse>> ListAreasAsync(int? configurationVersion, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AreaSummaryResponse>>([]);

    public Task<AreaDetailResponse?> GetAreaAsync(string areaCode, int? configurationVersion, CancellationToken cancellationToken)
        => Task.FromResult<AreaDetailResponse?>(null);

    public Task<AreaGeoJSONResponse?> GetAreaGeoJSONAsync(string areaCode, int? configurationVersion, CancellationToken cancellationToken)
        => Task.FromResult<AreaGeoJSONResponse?>(null);

    public Task<IReadOnlyList<GridCellResponse>> ListGridCellsAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GridCellResponse>>([]);

    public Task<IReadOnlyList<SensorNodeResponse>> ListSensorNodesAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SensorNodeResponse>>([]);

    public Task<IReadOnlyList<ScenarioResponse>> ListScenariosAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ScenarioResponse>>([]);

    public Task<IReadOnlyList<SimulationRunResponse>> ListSimulationRunsAsync(
        string? areaCode,
        string? scenarioCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SimulationRunResponse>>([]);

    public Task<SimulationRunResponse?> GetSimulationRunAsync(Guid runId, CancellationToken cancellationToken)
        => Task.FromResult<SimulationRunResponse?>(null);

    public Task<AreaOperationalStateResponse?> GetAreaOperationalStateAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
        => Task.FromResult<AreaOperationalStateResponse?>(null);

    public Task<IReadOnlyList<CellOperationalStateResponse>> ListCellOperationalStatesAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CellOperationalStateResponse>>([]);

    public Task<IReadOnlyList<AlertStateResponse>> ListActiveAlertsAsync(
        string? areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AlertStateResponse>>([]);


}
