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
    Task<AreaGeoJSONResponse?> GetAreaGeoJSONAsync(string areaCode, int? configurationVersion, CancellationToken cancellationToken);
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
    Task<RuntimeRunAuditResponse?> GetRuntimeRunAuditAsync(Guid runId, CancellationToken cancellationToken);
    Task<RuntimeRunTimingSummaryResponse?> GetRuntimeRunTimingsAsync(Guid runId, CancellationToken cancellationToken);
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
    Task<RuntimeSummaryResponse> GetRuntimeSummaryAsync(
        string? areaCode,
        int recentMinutes,
        CancellationToken cancellationToken);
    Task<RuntimeDiagnosticCatalogResponse> ListRuntimeDiagnosticsAsync(CancellationToken cancellationToken);
    Task<RuntimeDiagnosticResultResponse?> ExecuteRuntimeDiagnosticAsync(
        string diagnosticId,
        RuntimeDiagnosticRequest request,
        CancellationToken cancellationToken);
    Task<RuntimeRunStartResponse> StartRuntimeRunAsync(
        RuntimeRunStartRequest request,
        CancellationToken cancellationToken);
    Task<RuntimeOperationResponse?> GetRuntimeOperationAsync(Guid operationId, CancellationToken cancellationToken);
    Task<RuntimeOperationResponse?> GetRuntimeOperationByRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<RuntimeOperationResponse?> GetCurrentRuntimeOperationAsync(CancellationToken cancellationToken);
    Task<RuntimeOperationResponse?> GetRuntimeOperationByRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<RuntimeOperationResponse?> ReconcileRuntimeOperationWithProviderAsync(Guid operationId, CancellationToken cancellationToken);
    Task EnsureRuntimeEvidenceAsync(Guid operationId, CancellationToken cancellationToken);
    Task<ControlledValidationP3RunResponse> StartControlledValidationP3Async(
        ControlledValidationP3RunRequest request,
        string environmentName,
        CancellationToken cancellationToken);
    Task<RuntimeResetResponse> ResetRuntimeStateAsync(
        RuntimeResetRequest request,
        CancellationToken cancellationToken);
}
