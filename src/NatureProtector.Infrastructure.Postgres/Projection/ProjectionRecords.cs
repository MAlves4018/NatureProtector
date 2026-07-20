namespace NatureProtector.Infrastructure.Postgres.Projection;

public enum OperationalAlertStatus
{
    Open = 0,
    Resolved = 1
}

public sealed class AcceptedReadingLogRecord
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid AreaId { get; set; }
    public Guid SensorId { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public string MeasurementUnit { get; set; } = string.Empty;
    public string OperationalState { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public DateTimeOffset? IngestTime { get; set; }
    public string Producer { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.SensorNodeRecord? SensorNode { get; set; }
}

public sealed class RiskAssessmentLogRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid? SimulationRunId { get; set; }
    public Guid SensorId { get; set; }
    public Guid? GridCellId { get; set; }
    public Guid SourceEventId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double RiskScore { get; set; }
    public double BaseRisk { get; set; }
    public double AdjustedScore { get; set; }
    public int Score100 { get; set; }
    public double MeteorologyComponent { get; set; }
    public double DroughtComponent { get; set; }
    public double TerritoryComponent { get; set; }
    public double HazardComponent { get; set; }
    public double FuelComponent { get; set; }
    public double GeomorphologyComponent { get; set; }
    public double ConfidenceFactor { get; set; }
    public double IntegrityFactor { get; set; }
    public string DominantDriver { get; set; } = string.Empty;
    public string ParameterSetVersion { get; set; } = string.Empty;
    public string CalculationStatus { get; set; } = string.Empty;
    public string? Limitations { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? ExplanationSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.SimulationRunRecord? SimulationRun { get; set; }
    public Control.SensorNodeRecord? SensorNode { get; set; }
    public Control.GridCellRecord? GridCell { get; set; }
}

public sealed class AreaRiskSnapshotLogRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid? SimulationRunId { get; set; }
    public DateTimeOffset SnapshotTimestamp { get; set; }
    public double AggregateRiskScore { get; set; }
    public string AggregateRiskLevel { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int AssessmentCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.SimulationRunRecord? SimulationRun { get; set; }
}

public sealed class DailyCellStateRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid GridCellId { get; set; }
    public Guid? SensorId { get; set; }
    public Guid? SimulationRunId { get; set; }
    public Guid? ConfigurationVersionId { get; set; }
    public DateTimeOffset LogicalDate { get; set; }
    public double? DailyPrecipitationMillimeters { get; set; }
    public double? MaxTemperatureCelsius { get; set; }
    public double? LatestHumidityPercent { get; set; }
    public double? LatestWindSpeedMetersPerSecond { get; set; }
    public string AntecedentState { get; set; } = string.Empty;
    public string DroughtContext { get; set; } = string.Empty;
    public double? FireWeatherIndex { get; set; }
    public double? KeetchByramDroughtIndex { get; set; }
    public double? PreviousKeetchByramDroughtIndex { get; set; }
    public double? NormalizedKeetchByramDroughtIndex { get; set; }
    public string KbdiCalculationStatus { get; set; } = string.Empty;
    public string? KbdiLimitations { get; set; }
    public string FireIndexProvenance { get; set; } = string.Empty;
    public double? FineFuelMoistureCode { get; set; }
    public double? DuffMoistureCode { get; set; }
    public double? DroughtCode { get; set; }
    public double? InitialSpreadIndex { get; set; }
    public double? BuildupIndex { get; set; }
    public double? NormalizedFireWeatherIndex { get; set; }
    public string FireWeatherCalculationStatus { get; set; } = string.Empty;
    public string? FireWeatherLimitations { get; set; }
    public string CandidateParameterSetVersion { get; set; } = string.Empty;
    public string Provenance { get; set; } = string.Empty;
    public Guid? LastSourceEventId { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.GridCellRecord? GridCell { get; set; }
    public Control.SensorNodeRecord? SensorNode { get; set; }
    public Control.SimulationRunRecord? SimulationRun { get; set; }
    public Control.ConfigurationVersionRecord? ConfigurationVersion { get; set; }
}

public sealed class CellOperationalStateRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid GridCellId { get; set; }
    public Guid? SensorId { get; set; }
    public Guid? LatestAssessmentId { get; set; }
    public DateTimeOffset SnapshotTimestamp { get; set; }
    public double RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string CoverageStatus { get; set; } = string.Empty;
    public string FreshnessStatus { get; set; } = string.Empty;
    public string CarryForwardStatus { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.GridCellRecord? GridCell { get; set; }
    public Control.SensorNodeRecord? SensorNode { get; set; }
}

public sealed class AreaOperationalStateRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public Guid? SimulationRunId { get; set; }
    public int? CycleIndex { get; set; }
    public DateTimeOffset SnapshotTimestamp { get; set; }
    public double AggregateRiskScore { get; set; }
    public string AggregateRiskLevel { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string CoverageStatus { get; set; } = string.Empty;
    public string FreshnessStatus { get; set; } = string.Empty;
    public string CarryForwardStatus { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int AssessmentCount { get; set; }
    public string PendingAlertState { get; set; } = string.Empty;
    public int PendingAlertCycles { get; set; }
    public DateTimeOffset? AlertCooldownUntil { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public Control.SimulationRunRecord? SimulationRun { get; set; }
    public List<AlertStateRecord> Alerts { get; set; } = [];
}

public sealed class CycleSettlementRecord
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public int CycleIndex { get; set; }
    public Guid AreaId { get; set; }
    public string ExpectedSensorIdsJson { get; set; } = "[]";
    public string ObservedSensorIdsJson { get; set; } = "[]";
    public string MissingSensorIdsJson { get; set; } = "[]";
    public string BlockedSensorIdsJson { get; set; } = "[]";
    public string EligibleSensorIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "Open";
    public bool IsOperational { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public string? FinalizationReason { get; set; }
}

public sealed class CycleObservationRecord
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public int CycleIndex { get; set; }
    public Guid AreaId { get; set; }
    public Guid SensorId { get; set; }
    public Guid GridCellId { get; set; }
    public Guid EventId { get; set; }
    public string MetricOrigin { get; set; } = "Observed";
    public string Outcome { get; set; } = "Eligible";
    public double? RiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CellCycleSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public int CycleIndex { get; set; }
    public Guid AreaId { get; set; }
    public Guid GridCellId { get; set; }
    public int ExpectedCount { get; set; }
    public int ObservedCount { get; set; }
    public int MissingCount { get; set; }
    public int BlockedCount { get; set; }
    public int EligibleCount { get; set; }
    public double? AggregateRiskScore { get; set; }
    public string AggregateRiskLevel { get; set; } = "Unknown";
    public string AggregationStatus { get; set; } = "Available";
    public string? AggregationReason { get; set; }
    public DateTimeOffset SnapshotTimestamp { get; set; }
}

public sealed class AreaCycleSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public int CycleIndex { get; set; }
    public Guid AreaId { get; set; }
    public int CellCount { get; set; }
    public int ExpectedCount { get; set; }
    public int ObservedCount { get; set; }
    public int MissingCount { get; set; }
    public int BlockedCount { get; set; }
    public int EligibleCount { get; set; }
    public double? AggregateRiskScore { get; set; }
    public string AggregateRiskLevel { get; set; } = "Unknown";
    public string AggregationStatus { get; set; } = "Available";
    public string? AggregationReason { get; set; }
    public DateTimeOffset SnapshotTimestamp { get; set; }
    public DateTimeOffset AlertEvaluatedAt { get; set; }
    public string AlertOutcome { get; set; } = "None";
    public bool IsOperational { get; set; }
}

public sealed class AlertStateRecord
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public Guid AreaOperationalStateId { get; set; }
    public string AlertCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public Control.AreaRecord? Area { get; set; }
    public Control.ConfigurationVersionRecord? ConfigurationVersion { get; set; }
    public AreaOperationalStateRecord? AreaOperationalState { get; set; }
}
