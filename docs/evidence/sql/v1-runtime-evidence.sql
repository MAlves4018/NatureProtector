-- NatureProtector V1 runtime evidence
-- Executar no DBeaver contra a DB natureprotector.
-- Nota: colunas EF/PostgreSQL usam PascalCase quoted identifiers.

select '00_schema_tables' as section;

select table_schema, table_name
from information_schema.tables
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name;


select '01_schema_columns' as section;

select table_schema, table_name, column_name, data_type
from information_schema.columns
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name, ordinal_position;


select '02_control_counts' as section;

select
  (select count(*) from control.configuration_versions) as configuration_versions,
  (select count(*) from control.areas) as areas,
  (select count(*) from control.grid_cells) as grid_cells,
  (select count(*) from control.sensor_nodes) as sensor_nodes,
  (select count(*) from control.sensor_profiles) as sensor_profiles,
  (select count(*) from control.scenario_definitions) as scenario_definitions,
  (select count(*) from control.simulation_runs) as simulation_runs;


select '03_active_configuration' as section;

select
  "Id",
  "VersionNumber",
  "IsActive",
  "Description",
  "CreatedAt",
  "CreatedBy"
from control.configuration_versions
order by "CreatedAt" desc;


select '04_area' as section;

select
  "Id",
  "ConfigurationVersionId",
  "Code",
  "Name",
  "CountryCode"
from control.areas
order by "Code";


select '05_sensor_nodes_summary' as section;

select
  "IsActive",
  "Type",
  count(*) as count
from control.sensor_nodes
group by "IsActive", "Type"
order by "IsActive" desc, "Type";


select '06_sensor_nodes_sample' as section;

select
  "Id",
  "Name",
  "Type",
  "IsActive",
  "Latitude",
  "Longitude",
  "InstallationProfile"
from control.sensor_nodes
order by "IsActive" desc, "Name"
limit 50;


select '07_simulation_runs_latest' as section;

select *
from control.simulation_runs
order by "CreatedAt" desc
limit 20;

select '07b_latest_started_simulation_run' as section;

select
  "Id",
  "ScenarioCode",
  "ScenarioName",
  "CreatedAt",
  "StartedAt",
  "EndedAt",
  "IntervalSeconds",
  "NumberOfCycles",
  "ExecutionSeed",
  "Status",
  "MetadataJson"
from control.simulation_runs
order by "CreatedAt" desc
limit 1;


select '07c_latest_completed_simulation_run' as section;

select
  "Id",
  "ScenarioCode",
  "ScenarioName",
  "CreatedAt",
  "StartedAt",
  "EndedAt",
  "IntervalSeconds",
  "NumberOfCycles",
  "ExecutionSeed",
  "Status",
  "MetadataJson"
from control.simulation_runs
where "EndedAt" is not null
order by "EndedAt" desc
limit 1;


select '07d_running_simulation_runs' as section;

select
  "Id",
  "ScenarioCode",
  "ScenarioName",
  "CreatedAt",
  "StartedAt",
  "EndedAt",
  "IntervalSeconds",
  "NumberOfCycles",
  "ExecutionSeed",
  "Status",
  "MetadataJson"
from control.simulation_runs
where "EndedAt" is null
order by "StartedAt" desc;

select '07e_latest_completed_run_pipeline_window_summary' as section;

with latest_completed_run as (
  select
    "Id",
    "StartedAt",
    "EndedAt"
  from control.simulation_runs
  where "EndedAt" is not null
  order by "EndedAt" desc
  limit 1
)
select
  ei."Status",
  count(*) as count,
  min(ei."ReceivedAt") as first_received_at,
  max(ei."ReceivedAt") as last_received_at
from pipeline.event_inbox ei
cross join latest_completed_run lr
where ei."ReceivedAt" between lr."StartedAt" and lr."EndedAt" + interval '1 minute'
group by ei."Status"
order by ei."Status";

select '07f_latest_completed_run_processing_attempts_summary' as section;

with latest_completed_run as (
  select
    "Id",
    "StartedAt",
    "EndedAt"
  from control.simulation_runs
  where "EndedAt" is not null
  order by "EndedAt" desc
  limit 1
)
select
  pa."Outcome",
  pa."ErrorCode",
  pa."ErrorMessage",
  count(*) as count,
  min(pa."StartedAt") as first_started_at,
  max(pa."StartedAt") as last_started_at,
  min(pa."FinishedAt") as first_finished_at,
  max(pa."FinishedAt") as last_finished_at
from pipeline.processing_attempts pa
cross join latest_completed_run lr
where pa."StartedAt" between lr."StartedAt" and lr."EndedAt" + interval '1 minute'
group by pa."Outcome", pa."ErrorCode", pa."ErrorMessage"
order by count desc;

select '07g_running_run_pipeline_observation' as section;

with running_run as (
  select
    "Id",
    "StartedAt"
  from control.simulation_runs
  where "EndedAt" is null
  order by "StartedAt" desc
  limit 1
)
select
  rr."Id" as running_run_id,
  ei."Status",
  count(*) as count,
  min(ei."ReceivedAt") as first_received_at,
  max(ei."ReceivedAt") as last_received_at
from running_run rr
join pipeline.event_inbox ei
  on ei."PayloadJson" like '%' || rr."Id"::text || '%'
group by rr."Id", ei."Status"
order by ei."Status";


select '08_pipeline_counts' as section;

select
  (select count(*) from pipeline.event_inbox) as inbox_total,
  (select count(*) from pipeline.processing_attempts) as attempts_total,
  (select count(*) from pipeline.rejected_events) as rejected_total,
  (select count(*) from pipeline.quarantined_events) as quarantined_total;


select '09_pipeline_inbox_status' as section;

select
  "Status",
  count(*) as count
from pipeline.event_inbox
group by "Status"
order by "Status";

select '09b_observed_runtime_enum_values' as section;

select
  'pipeline.event_inbox.Status' as enum_source,
  "Status"::text as observed_value,
  count(*) as count
from pipeline.event_inbox
group by "Status"

union all

select
  'pipeline.processing_attempts.Outcome' as enum_source,
  "Outcome"::text as observed_value,
  count(*) as count
from pipeline.processing_attempts
group by "Outcome"

union all

select
  'control.simulation_runs.Status' as enum_source,
  "Status"::text as observed_value,
  count(*) as count
from control.simulation_runs
group by "Status"

order by enum_source, observed_value;


select '10_pipeline_inbox_time_range' as section;

select
  min("ReceivedAt") as first_inbox_received_at,
  max("ReceivedAt") as last_inbox_received_at,
  min("EventTime") as first_event_time,
  max("EventTime") as last_event_time
from pipeline.event_inbox;


select '11_pipeline_inbox_latest' as section;

select
  "Id",
  "EventId",
  "EventType",
  "Producer",
  "Status",
  "AttemptCount",
  "EventTime",
  "ReceivedAt",
  "LastAttemptAt",
  "LastProcessedAt",
  "LastErrorCode",
  "LastErrorMessage",
  "QuarantinedAt"
from pipeline.event_inbox
order by "ReceivedAt" desc
limit 25;


select '12_pipeline_last_errors_by_inbox' as section;

select
  "LastErrorCode",
  "LastErrorMessage",
  count(*) as count
from pipeline.event_inbox
where "LastErrorMessage" is not null
  and "LastErrorMessage" <> ''
group by "LastErrorCode", "LastErrorMessage"
order by count desc, "LastErrorCode";

select '12b_pipeline_last_errors_time_range_by_inbox' as section;

select
  "LastErrorCode",
  "LastErrorMessage",
  count(*) as count,
  min("ReceivedAt") as first_received_at,
  max("ReceivedAt") as last_received_at,
  min("LastAttemptAt") as first_attempt_at,
  max("LastAttemptAt") as last_attempt_at,
  min("QuarantinedAt") as first_quarantined_at,
  max("QuarantinedAt") as last_quarantined_at
from pipeline.event_inbox
where "LastErrorMessage" is not null
  and "LastErrorMessage" <> ''
group by "LastErrorCode", "LastErrorMessage"
order by max("ReceivedAt") desc, count desc;


select '13_processing_attempts_latest' as section;

select
  "Id",
  "InboxEventId",
  "AttemptNumber",
  "Stage",
  "StartedAt",
  "FinishedAt",
  "Outcome",
  "ErrorCode",
  "ErrorMessage"
from pipeline.processing_attempts
order by "StartedAt" desc
limit 50;


select '14_processing_attempt_errors' as section;

select
  "Stage",
  "Outcome",
  "ErrorCode",
  "ErrorMessage",
  count(*) as count
from pipeline.processing_attempts
where "ErrorMessage" is not null
  and "ErrorMessage" <> ''
group by "Stage", "Outcome", "ErrorCode", "ErrorMessage"
order by count desc;

select '14b_processing_attempt_errors_time_range' as section;

select
  "Stage",
  "Outcome",
  "ErrorCode",
  "ErrorMessage",
  count(*) as count,
  min("StartedAt") as first_started_at,
  max("StartedAt") as last_started_at,
  min("FinishedAt") as first_finished_at,
  max("FinishedAt") as last_finished_at
from pipeline.processing_attempts
where "ErrorMessage" is not null
  and "ErrorMessage" <> ''
group by "Stage", "Outcome", "ErrorCode", "ErrorMessage"
order by max("StartedAt") desc, count desc;


select '15_rejected_events_summary' as section;

select
  "RejectionCode",
  "RejectionReason",
  count(*) as count,
  min("RejectedAt") as first_rejected_at,
  max("RejectedAt") as last_rejected_at
from pipeline.rejected_events
group by "RejectionCode", "RejectionReason"
order by max("RejectedAt") desc, count desc;


select '16_rejected_events_latest' as section;

select
  "Id",
  "EventId",
  "RejectionCode",
  "RejectionReason",
  "RejectedAt",
  left("RawBodyUtf8", 500) as raw_body_sample,
  "MetadataJson"
from pipeline.rejected_events
order by "RejectedAt" desc
limit 25;


select '17_quarantined_events_summary' as section;

select
  "QuarantineCode",
  "QuarantineReason",
  count(*) as count,
  min("QuarantinedAt") as first_quarantined_at,
  max("QuarantinedAt") as last_quarantined_at
from pipeline.quarantined_events
group by "QuarantineCode", "QuarantineReason"
order by max("QuarantinedAt") desc, count desc;


select '17b_quarantined_events_latest' as section;

select
  "Id",
  "InboxEventId",
  "EventId",
  "FinalAttemptNumber",
  "QuarantineCode",
  "QuarantineReason",
  "QuarantinedAt",
  "MetadataJson"
from pipeline.quarantined_events
order by "QuarantinedAt" desc
limit 25;


select '18_projection_counts' as section;

select
  (select count(*) from projection.accepted_reading_log) as accepted_readings,
  (select count(*) from projection.risk_assessment_log) as risk_assessments,
  (select count(*) from projection.area_risk_snapshot_log) as area_risk_snapshots,
  (select count(*) from projection.cell_operational_state) as cell_operational_states,
  (select count(*) from projection.area_operational_state) as area_operational_states,
  (select count(*) from projection.alert_state) as alert_states;


select '19_projection_time_ranges' as section;

select
  (select min("CreatedAt") from projection.risk_assessment_log) as first_risk_created_at,
  (select max("CreatedAt") from projection.risk_assessment_log) as last_risk_created_at,
  (select min("SnapshotTimestamp") from projection.area_risk_snapshot_log) as first_area_snapshot_timestamp,
  (select max("SnapshotTimestamp") from projection.area_risk_snapshot_log) as last_area_snapshot_timestamp,
  (select max("UpdatedAt") from projection.area_operational_state) as last_area_state_updated_at,
  (select max("UpdatedAt") from projection.alert_state) as last_alert_updated_at;


select '20_accepted_reading_latest' as section;

select
  "Id",
  "EventId",
  "AreaId",
  "SensorId",
  "MetricType",
  "MeasurementUnit",
  "OperationalState",
  "Value",
  "EventTime",
  "Producer"
from projection.accepted_reading_log
order by "EventTime" desc
limit 25;


select '21_risk_assessment_columns' as section;

select column_name, data_type
from information_schema.columns
where table_schema = 'projection'
  and table_name = 'risk_assessment_log'
order by ordinal_position;


select '22_risk_assessment_score_range' as section;

select
  count(*) as risk_assessments,
  min("RiskScore") as min_risk_score,
  max("RiskScore") as max_risk_score,
  avg("RiskScore") as avg_risk_score
from projection.risk_assessment_log;


select '23_risk_assessment_by_level' as section;

select
  "RiskLevel",
  count(*) as count,
  min("RiskScore") as min_score,
  max("RiskScore") as max_score
from projection.risk_assessment_log
group by "RiskLevel"
order by min_score;


select '24_risk_assessment_latest' as section;

select
  "Id",
  "AreaId",
  "SensorId",
  "GridCellId",
  "SourceEventId",
  "Timestamp",
  "RiskScore",
  "RiskLevel",
  "ExplanationSummary",
  "CreatedAt"
from projection.risk_assessment_log
order by "CreatedAt" desc
limit 25;


select '25_area_operational_state_latest' as section;

select
  "Id",
  "AreaId",
  "ConfigurationVersionId",
  "SimulationRunId",
  "SnapshotTimestamp",
  "AggregateRiskScore",
  "AggregateRiskLevel",
  "Severity",
  "Summary",
  "AssessmentCount",
  "UpdatedAt"
from projection.area_operational_state
order by "UpdatedAt" desc
limit 10;

select '25b_area_operational_state_api_comparison_source' as section;

select
  a."Code" as area_code,
  cv."VersionNumber" as configuration_version_number,
  aos."SnapshotTimestamp" as snapshot_timestamp,
  aos."AggregateRiskScore" as aggregate_risk_score,
  aos."AggregateRiskLevel" as aggregate_risk_level,
  aos."Severity" as severity,
  aos."Summary" as summary,
  aos."AssessmentCount" as assessment_count,
  aos."UpdatedAt" as updated_at
from projection.area_operational_state aos
join control.areas a
  on a."Id" = aos."AreaId"
join control.configuration_versions cv
  on cv."Id" = aos."ConfigurationVersionId"
order by aos."UpdatedAt" desc
limit 1;


select '26_cell_operational_state_latest' as section;

select
  "Id",
  "AreaId",
  "GridCellId",
  "SensorId",
  "LatestAssessmentId",
  "SnapshotTimestamp",
  "RiskScore",
  "RiskLevel",
  "Severity",
  "Summary",
  "UpdatedAt"
from projection.cell_operational_state
order by "UpdatedAt" desc
limit 25;


select '27_area_risk_snapshot_latest' as section;

select *
from projection.area_risk_snapshot_log
order by "SnapshotTimestamp" desc
limit 25;

select '27b_area_risk_snapshots_latest_completed_run' as section;

with latest_completed_run as (
  select "Id"
  from control.simulation_runs
  where "EndedAt" is not null
  order by "EndedAt" desc
  limit 1
)
select
  arsl."Id",
  arsl."AreaId",
  arsl."SimulationRunId",
  arsl."SnapshotTimestamp",
  arsl."AggregateRiskScore",
  arsl."AggregateRiskLevel",
  arsl."Summary",
  arsl."AssessmentCount",
  arsl."CreatedAt"
from projection.area_risk_snapshot_log arsl
join latest_completed_run lr
  on arsl."SimulationRunId" = lr."Id"
order by arsl."SnapshotTimestamp" desc
limit 25;


select '27c_area_risk_snapshots_latest_completed_run_summary' as section;

with latest_completed_run as (
  select "Id"
  from control.simulation_runs
  where "EndedAt" is not null
  order by "EndedAt" desc
  limit 1
)
select
  count(*) as snapshots_for_latest_completed_run,
  min(arsl."SnapshotTimestamp") as first_snapshot_timestamp,
  max(arsl."SnapshotTimestamp") as last_snapshot_timestamp,
  min(arsl."AggregateRiskScore") as min_aggregate_risk_score,
  max(arsl."AggregateRiskScore") as max_aggregate_risk_score,
  avg(arsl."AggregateRiskScore") as avg_aggregate_risk_score
from projection.area_risk_snapshot_log arsl
join latest_completed_run lr
  on arsl."SimulationRunId" = lr."Id";


select '28_alert_state_columns' as section;

select column_name, data_type
from information_schema.columns
where table_schema = 'projection'
  and table_name = 'alert_state'
order by ordinal_position;


select '29_alert_state_latest' as section;

select
  "Id",
  "AreaId",
  "ConfigurationVersionId",
  "AreaOperationalStateId",
  "AlertCode",
  "Severity",
  "Status",
  "Message",
  "TriggeredAt",
  "UpdatedAt",
  "ResolvedAt"
from projection.alert_state
order by "UpdatedAt" desc
limit 25;


select '30_alert_state_by_status' as section;

select
  "AlertCode",
  "Severity",
  "Status",
  count(*) as count,
  min("TriggeredAt") as first_triggered_at,
  max("UpdatedAt") as last_updated_at
from projection.alert_state
group by "AlertCode", "Severity", "Status"
order by "AlertCode", "Severity", "Status";


select '31_area_state_join_alerts' as section;

select
  aos."AreaId",
  aos."AggregateRiskScore",
  aos."AggregateRiskLevel",
  aos."Severity" as area_severity,
  aos."SnapshotTimestamp",
  aos."UpdatedAt" as area_updated_at,
  als."AlertCode",
  als."Severity" as alert_severity,
  als."Status" as alert_status,
  als."Message" as alert_message,
  als."TriggeredAt",
  als."ResolvedAt"
from projection.area_operational_state aos
left join projection.alert_state als
  on als."AreaOperationalStateId" = aos."Id"
order by aos."UpdatedAt" desc, als."UpdatedAt" desc
limit 25;


select '32_blocked_or_zero_risk_probe' as section;

select
  count(*) filter (where "RiskScore" = 0) as zero_risk_assessments,
  count(*) filter (where lower("ExplanationSummary") like '%blocked%') as explanations_containing_blocked,
  count(*) filter (where lower("ExplanationSummary") like '%partial%') as explanations_containing_partial
from projection.risk_assessment_log;

select '32b_zero_risk_assessments_latest' as section;

select
  "Id",
  "AreaId",
  "SensorId",
  "GridCellId",
  "SourceEventId",
  "Timestamp",
  "RiskScore",
  "RiskLevel",
  "ExplanationSummary",
  "CreatedAt"
from projection.risk_assessment_log
where "RiskScore" = 0
order by "CreatedAt" desc
limit 25;


select '33_final_runtime_summary' as section;

select
  (select count(*) from pipeline.event_inbox) as inbox_total,
  (select count(*) from pipeline.processing_attempts) as attempts_total,
  (select count(*) from projection.accepted_reading_log) as accepted_total,
  (select count(*) from projection.risk_assessment_log) as risk_total,
  (select count(*) from projection.area_risk_snapshot_log) as area_snapshot_total,
  (select count(*) from projection.cell_operational_state) as cell_state_total,
  (select count(*) from projection.area_operational_state) as area_state_total,
  (select count(*) from projection.alert_state) as alert_total,
  (select count(*) from pipeline.rejected_events) as rejected_total,
  (select count(*) from pipeline.quarantined_events) as quarantined_total;