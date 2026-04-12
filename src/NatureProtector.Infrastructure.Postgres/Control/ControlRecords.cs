using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;

namespace NatureProtector.Infrastructure.Postgres.Control;

public sealed class ConfigurationVersionRecord
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public List<AreaRecord> Areas { get; set; } = [];
    public List<GridCellRecord> GridCells { get; set; } = [];
    public List<SensorProfileRecord> SensorProfiles { get; set; } = [];
    public List<SensorNetworkRecord> SensorNetworks { get; set; } = [];
    public List<SensorNodeRecord> SensorNodes { get; set; } = [];
    public List<ScenarioDefinitionRecord> Scenarios { get; set; } = [];
    public List<SimulationRunRecord> SimulationRuns { get; set; } = [];
    public List<RuleSetVersionRecord> RuleSetVersions { get; set; } = [];
}

public sealed class AreaRecord
{
    public Guid Id { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? GeometryGeoJson { get; set; }
    public string? MetadataJson { get; set; }

    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public AreaContextRecord? Context { get; set; }
    public List<GridCellRecord> GridCells { get; set; } = [];
    public List<SensorNodeRecord> SensorNodes { get; set; } = [];
    public List<ScenarioDefinitionRecord> Scenarios { get; set; } = [];
    public List<SimulationRunRecord> SimulationRuns { get; set; } = [];
}

public sealed class AreaContextRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public string VegetationType { get; set; } = string.Empty;
    public double VegetationDensity { get; set; }
    public double PopulationExposure { get; set; }
    public double CriticalInfrastructureExposure { get; set; }
    public string Seasonality { get; set; } = string.Empty;

    public AreaRecord? Area { get; set; }
}

public sealed class GridCellRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public string CellCode { get; set; } = string.Empty;
    public double CentroidLatitude { get; set; }
    public double CentroidLongitude { get; set; }
    public string? PolygonGeoJson { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SlopeDegrees { get; set; }
    public double? AspectDegrees { get; set; }
    public string? LandCoverClass { get; set; }
    public string? DominantForestType { get; set; }
    public string? DominantFuelModel { get; set; }
    public double? TreeCoverDensity { get; set; }
    public string? StructuralHazard { get; set; }
    public string? ConjuncturalHazard { get; set; }

    public AreaRecord? Area { get; set; }
    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public List<SensorNodeRecord> SensorNodes { get; set; } = [];
}

public sealed class SensorProfileRecord
{
    public Guid Id { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SensorFamily { get; set; }
    public string? AccuracyProfileJson { get; set; }
    public string? NoiseProfileJson { get; set; }
    public string? FaultProfileJson { get; set; }
    public string? PublicationPolicyJson { get; set; }

    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public List<SensorNodeRecord> SensorNodes { get; set; } = [];
}

public sealed class SensorNetworkRecord
{
    public Guid Id { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public List<SensorNodeRecord> SensorNodes { get; set; } = [];
}

public sealed class SensorNodeRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid GridCellId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public Guid? NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SensorType Type { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public bool IsActive { get; set; }
    public string? InstallationProfile { get; set; }

    public AreaRecord? Area { get; set; }
    public GridCellRecord? GridCell { get; set; }
    public SensorProfileRecord? Profile { get; set; }
    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public SensorNetworkRecord? Network { get; set; }
}

public sealed class ScenarioDefinitionRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public Guid? BaseScenarioId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ScenarioCategory ScenarioKind { get; set; }
    public string? Description { get; set; }
    public string ParametersJson { get; set; } = "{}";

    public AreaRecord? Area { get; set; }
    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public ScenarioDefinitionRecord? BaseScenario { get; set; }
    public List<ScenarioDefinitionRecord> DerivedScenarios { get; set; } = [];
    public List<ScenarioDatasetBindingRecord> DatasetBindings { get; set; } = [];
    public List<SimulationRunRecord> SimulationRuns { get; set; } = [];
}

public sealed class SimulationRunRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid ScenarioId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public string ScenarioCode { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset LogicalStartTimestamp { get; set; }
    public int IntervalSeconds { get; set; }
    public int NumberOfCycles { get; set; }
    public int? ExecutionSeed { get; set; }
    public SimulationRunStatus Status { get; set; }
    public string? MetadataJson { get; set; }

    public AreaRecord? Area { get; set; }
    public ScenarioDefinitionRecord? Scenario { get; set; }
    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
}

public sealed class RuleSetVersionRecord
{
    public Guid Id { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public bool IsActive { get; set; }

    public ConfigurationVersionRecord? ConfigurationVersion { get; set; }
}

public sealed class DatasetArtifactRecord
{
    public Guid Id { get; set; }
    public string DatasetCode { get; set; } = string.Empty;
    public string DatasetType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string AreaCode { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string? MetadataJson { get; set; }

    public List<ScenarioDatasetBindingRecord> ScenarioBindings { get; set; } = [];
}

public sealed class ScenarioDatasetBindingRecord
{
    public Guid Id { get; set; }
    public Guid ScenarioId { get; set; }
    public Guid DatasetArtifactId { get; set; }
    public string BindingRole { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ScenarioDefinitionRecord? Scenario { get; set; }
    public DatasetArtifactRecord? DatasetArtifact { get; set; }
}
