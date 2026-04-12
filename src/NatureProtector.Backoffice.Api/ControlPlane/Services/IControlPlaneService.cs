using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public interface IControlPlaneService
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    Task<IReadOnlyList<ConfigurationVersionResponse>> ListConfigurationsAsync(CancellationToken cancellationToken);
    Task<ConfigurationVersionResponse?> GetActiveConfigurationAsync(CancellationToken cancellationToken);
    Task<ConfigurationVersionResponse?> ActivateConfigurationAsync(int versionNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaSummaryResponse>> ListAreasAsync(int? configurationVersion, CancellationToken cancellationToken);
    Task<AreaDetailResponse?> GetAreaAsync(string areaCode, int? configurationVersion, CancellationToken cancellationToken);
    Task<IReadOnlyList<GridCellResponse>> ListGridCellsAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SensorNodeResponse>> ListSensorNodesAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ScenarioResponse>> ListScenariosAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SimulationRunResponse>> ListSimulationRunsAsync(
        string? areaCode,
        string? scenarioCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken);
    Task<SimulationRunResponse?> GetSimulationRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<AreaOperationalStateResponse?> GetAreaOperationalStateAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CellOperationalStateResponse>> ListCellOperationalStatesAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AlertStateResponse>> ListActiveAlertsAsync(
        string? areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken);
}
