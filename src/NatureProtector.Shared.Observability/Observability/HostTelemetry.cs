using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NatureProtector.Shared.Observability;

public static class TelemetryTags
{
    public const string Host = "np.host";
    public const string Operation = "np.operation";
    public const string Outcome = "np.outcome";
    public const string AreaId = "np.area_id";
    public const string ScenarioId = "np.scenario_id";
    public const string ScenarioCode = "np.scenario_code";
    public const string SimulationRunId = "np.simulation_run_id";
    public const string SensorId = "np.sensor_id";
    public const string SensorName = "np.sensor_name";
    public const string EventId = "np.event_id";
    public const string CorrelationId = "np.correlation_id";
    public const string InboxEventId = "np.inbox_event_id";
    public const string AttemptNumber = "np.attempt_number";
    public const string MetricType = "np.metric_type";
    public const string RiskLevel = "np.risk_level";
    public const string Severity = "np.severity";
    public const string Stage = "np.stage";
    public const string ErrorCode = "np.error_code";
    public const string RejectionCode = "np.rejection_code";
    public const string QuarantineCode = "np.quarantine_code";
    public const string RetryKind = "np.retry_kind";
    public const string Measurement = "np.measurement";
    public const string ConfigurationVersion = "np.configuration_version";
    public const string HasAcceptedReadings = "np.has_accepted_readings";
    public const string HasRiskAssessments = "np.has_risk_assessments";
    public const string HasAreaRiskSnapshots = "np.has_area_risk_snapshots";
}

public static class PostgresBootstrapTelemetry
{
    public const string ServiceName = "NatureProtector.Postgres.Bootstrap";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> BootstrapRuns = Meter.CreateCounter<long>("natureprotector.bootstrap.runs");
    public static readonly Counter<long> UpsertOperations = Meter.CreateCounter<long>("natureprotector.bootstrap.upsert.operations");
    public static readonly Histogram<double> BootstrapDurationMs = Meter.CreateHistogram<double>("natureprotector.bootstrap.duration", unit: "ms");
    public static readonly Histogram<double> UpsertDurationMs = Meter.CreateHistogram<double>("natureprotector.bootstrap.upsert.duration", unit: "ms");
    public static readonly Histogram<long> UpsertRows = Meter.CreateHistogram<long>("natureprotector.bootstrap.upsert.rows");
}

public static class SimulatorHostTelemetry
{
    public const string ServiceName = "NatureProtector.Simulator.Host";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> ContextCreations = Meter.CreateCounter<long>("natureprotector.simulator.context.creations");
    public static readonly Histogram<double> ContextCreationDurationMs = Meter.CreateHistogram<double>("natureprotector.simulator.context.creation.duration", unit: "ms");
    public static readonly Counter<long> SimulationRuns = Meter.CreateCounter<long>("natureprotector.simulator.runs");
    public static readonly Histogram<double> SimulationRunDurationMs = Meter.CreateHistogram<double>("natureprotector.simulator.run.duration", unit: "ms");
    public static readonly Counter<long> PublishedMessages = Meter.CreateCounter<long>("natureprotector.simulator.publish.messages");
    public static readonly Histogram<long> PublishBatchSize = Meter.CreateHistogram<long>("natureprotector.simulator.publish.batch.size");
    public static readonly Histogram<double> PublishDurationMs = Meter.CreateHistogram<double>("natureprotector.simulator.publish.duration", unit: "ms");
}

public static class PreventionHostTelemetry
{
    public const string ServiceName = "NatureProtector.Prevention.Host";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> ReceivedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.received");
    public static readonly Counter<long> ValidatedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.validated");
    public static readonly Counter<long> RejectedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.rejected");
    public static readonly Counter<long> AckedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.acked");
    public static readonly Counter<long> ProcessedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.processed");
    public static readonly Counter<long> RetryScheduledEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.retry_scheduled");
    public static readonly Counter<long> QuarantinedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.quarantined");
    public static readonly Counter<long> RetryPickedEvents = Meter.CreateCounter<long>("natureprotector.prevention.events.retry_picked");
    public static readonly Histogram<double> InboxStoreDurationMs = Meter.CreateHistogram<double>("natureprotector.prevention.inbox.store.duration", unit: "ms");
    public static readonly Histogram<double> ProcessingDurationMs = Meter.CreateHistogram<double>("natureprotector.prevention.processing.duration", unit: "ms");
    public static readonly Histogram<double> PostgresWriteDurationMs = Meter.CreateHistogram<double>("natureprotector.prevention.postgres.write.duration", unit: "ms");
    public static readonly Histogram<double> InfluxWriteDurationMs = Meter.CreateHistogram<double>("natureprotector.prevention.influx.write.duration", unit: "ms");
    public static readonly Histogram<double> InfluxBatchWriteDurationMs = Meter.CreateHistogram<double>("natureprotector.prevention.influx.batch.write.duration", unit: "ms");
    public static readonly Histogram<long> InfluxBatchPoints = Meter.CreateHistogram<long>("natureprotector.prevention.influx.batch.points");
}

public static class BackofficeApiTelemetry
{
    public const string ServiceName = "NatureProtector.Backoffice.Api";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("natureprotector.backoffice.requests");
    public static readonly Histogram<double> QueryDurationMs = Meter.CreateHistogram<double>("natureprotector.backoffice.query.duration", unit: "ms");
}
