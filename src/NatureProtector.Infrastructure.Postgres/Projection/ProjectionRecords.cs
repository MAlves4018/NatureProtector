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
    public string FireIndexProvenance { get; set; } = string.Empty;
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
    public DateTimeOffset SnapshotTimestamp { get; set; }
    public double AggregateRiskScore { get; set; }
    public string AggregateRiskLevel { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
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
