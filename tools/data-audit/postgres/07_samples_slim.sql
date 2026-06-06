\set inbox_file :out_dir '/11_sample_event_inbox.csv'
\set attempts_file :out_dir '/12_sample_processing_attempts.csv'
\set accepted_file :out_dir '/13_sample_accepted_readings.csv'
\set risk_file :out_dir '/14_sample_risk_assessments.csv'
\set snapshots_file :out_dir '/15_sample_area_snapshots.csv'
\pset format csv

\o :inbox_file
select "Id", "EventId", "CorrelationId", "Producer", "EventType", "AreaId", "EventTime", "ReceivedAt", "Status", "AttemptCount", "LastErrorCode"
from pipeline.event_inbox
order by "ReceivedAt" desc
limit 50;
\o

\o :attempts_file
select "Id", "InboxEventId", "AttemptNumber", "Stage", "StartedAt", "FinishedAt", "Outcome", "ErrorCode"
from pipeline.processing_attempts
order by "StartedAt" desc
limit 50;
\o

\o :accepted_file
select "Id", "EventId", "AreaId", "SensorId", "MetricType", "MeasurementUnit", "OperationalState", "Value", "EventTime", "CreatedAt", "CorrelationId"
from projection.accepted_reading_log
order by "CreatedAt" desc
limit 50;
\o

\o :risk_file
select "Id", "AreaId", "SensorId", "GridCellId", "SourceEventId", "SimulationRunId", "Timestamp", "RiskScore", "RiskLevel", "ExplanationSummary", "CreatedAt"
from projection.risk_assessment_log
order by "CreatedAt" desc
limit 50;
\o

\o :snapshots_file
select "Id", "AreaId", "SimulationRunId", "SnapshotTimestamp", "AggregateRiskScore", "AggregateRiskLevel", "Summary", "AssessmentCount", "CreatedAt"
from projection.area_risk_snapshot_log
order by "CreatedAt" desc
limit 50;
\o


