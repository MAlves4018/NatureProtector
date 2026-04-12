namespace NatureProtector.Infrastructure.Postgres.Bootstrap;

public sealed record ControlPlaneBootstrapSummary(
    int ConfigurationVersionNumber,
    string AreaCode,
    string AreaName,
    int GridCellCount,
    int SensorProfileCount,
    int SensorNodeCount,
    int ScenarioCount,
    int DatasetArtifactCount,
    int ScenarioDatasetBindingCount);
