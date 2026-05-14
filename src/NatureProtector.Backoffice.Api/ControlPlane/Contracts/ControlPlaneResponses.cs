namespace NatureProtector.Backoffice.Api.ControlPlane.Contracts;

public sealed record ConfigurationVersionResponse(
    int VersionNumber,
    bool IsActive,
    string? Description,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    int AreaCount,
    int GridCellCount,
    int SensorNodeCount,
    int ScenarioCount,
    int SimulationRunCount);

public sealed record AreaContextResponse(
    string VegetationType,
    double VegetationDensity,
    double PopulationExposure,
    double CriticalInfrastructureExposure,
    string Seasonality);

public sealed record AreaSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string? CountryCode,
    int ConfigurationVersionNumber,
    int GridCellCount,
    int SensorNodeCount,
    int ScenarioCount);

public sealed record AreaDetailResponse(
    string Code,
    string Name,
    string? CountryCode,
    int ConfigurationVersionNumber,
    string? GeometryGeoJson,
    string? MetadataJson,
    AreaContextResponse? Context,
    int GridCellCount,
    int SensorNodeCount,
    int ScenarioCount);

public sealed record GridCellResponse(
    string CellCode,
    int ConfigurationVersionNumber,
    double CentroidLatitude,
    double CentroidLongitude,
    double? AltitudeMeters,
    double? SlopeDegrees,
    double? AspectDegrees,
    string? LandCoverClass,
    string? DominantForestType,
    string? DominantFuelModel,
    double? TreeCoverDensity,
    string? StructuralHazard,
    string? ConjuncturalHazard,
    int SensorNodeCount);

public sealed record SensorNodeResponse(
    Guid Id,
    string Name,
    string Type,
    int ConfigurationVersionNumber,
    string CellCode,
    string ProfileName,
    string? SensorFamily,
    string? NetworkName,
    double Latitude,
    double Longitude,
    double? AltitudeMeters,
    bool IsActive,
    string? InstallationProfile);

public sealed record ScenarioResponse(
    Guid Id,
    string Code,
    string Name,
    string ScenarioKind,
    int ConfigurationVersionNumber,
    string? Description,
    string? BaseScenarioCode,
    int DatasetBindingCount);

public sealed record SimulationRunResponse(
    Guid Id,
    string AreaCode,
    string ScenarioCode,
    string ScenarioName,
    string Status,
    int ConfigurationVersionNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset LogicalStartTimestamp,
    int IntervalSeconds,
    int NumberOfCycles,
    int? ExecutionSeed,
    string? MetadataJson);

public sealed record AreaOperationalStateResponse(
    string AreaCode,
    int ConfigurationVersionNumber,
    DateTimeOffset SnapshotTimestamp,
    double AggregateRiskScore,
    string AggregateRiskLevel,
    string Severity,
    string? Summary,
    int AssessmentCount,
    DateTimeOffset UpdatedAt,
    string? AlertState = null);

public sealed record CellOperationalStateResponse(
    string AreaCode,
    string CellCode,
    int ConfigurationVersionNumber,
    DateTimeOffset SnapshotTimestamp,
    double RiskScore,
    string RiskLevel,
    string Severity,
    string? Summary,
    Guid? SensorId,
    string? SensorName,
    DateTimeOffset UpdatedAt);

public sealed record AlertStateResponse(
    Guid Id,
    string AreaCode,
    int ConfigurationVersionNumber,
    string AlertCode,
    string Severity,
    string Status,
    string Message,
    DateTimeOffset TriggeredAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? AlertState = null);
