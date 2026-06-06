\set output_file :out_dir '/07_time_ranges.csv'
\pset format csv
\o :output_file
select 'pipeline' as table_schema, 'event_inbox' as table_name, 'EventTime' as time_column, min("EventTime") as min_time, max("EventTime") as max_time, count(*) as row_count from pipeline.event_inbox
union all
select 'pipeline', 'event_inbox', 'ReceivedAt', min("ReceivedAt"), max("ReceivedAt"), count(*) from pipeline.event_inbox
union all
select 'pipeline', 'event_inbox', 'LastAttemptAt', min("LastAttemptAt"), max("LastAttemptAt"), count(*) from pipeline.event_inbox where "LastAttemptAt" is not null
union all
select 'pipeline', 'event_inbox', 'LastProcessedAt', min("LastProcessedAt"), max("LastProcessedAt"), count(*) from pipeline.event_inbox where "LastProcessedAt" is not null
union all
select 'pipeline', 'processing_attempts', 'StartedAt', min("StartedAt"), max("StartedAt"), count(*) from pipeline.processing_attempts
union all
select 'pipeline', 'processing_attempts', 'FinishedAt', min("FinishedAt"), max("FinishedAt"), count(*) from pipeline.processing_attempts where "FinishedAt" is not null
union all
select 'projection', 'accepted_reading_log', 'EventTime', min("EventTime"), max("EventTime"), count(*) from projection.accepted_reading_log
union all
select 'projection', 'accepted_reading_log', 'CreatedAt', min("CreatedAt"), max("CreatedAt"), count(*) from projection.accepted_reading_log
union all
select 'projection', 'risk_assessment_log', 'Timestamp', min("Timestamp"), max("Timestamp"), count(*) from projection.risk_assessment_log
union all
select 'projection', 'risk_assessment_log', 'CreatedAt', min("CreatedAt"), max("CreatedAt"), count(*) from projection.risk_assessment_log
union all
select 'projection', 'area_risk_snapshot_log', 'SnapshotTimestamp', min("SnapshotTimestamp"), max("SnapshotTimestamp"), count(*) from projection.area_risk_snapshot_log
union all
select 'projection', 'area_risk_snapshot_log', 'CreatedAt', min("CreatedAt"), max("CreatedAt"), count(*) from projection.area_risk_snapshot_log;
\o


