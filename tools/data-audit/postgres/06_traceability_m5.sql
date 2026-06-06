\set output_file :out_dir '/10_traceability_m5.csv'
\pset format csv
\o :output_file
select
    e."Id" as inbox_id,
    e."EventId" as event_id,
    e."CorrelationId" as correlation_id,
    e."EventType" as event_type,
    e."AreaId" as area_id,
    e."EventTime" as event_time,
    count(distinct pa."Id") as processing_attempts,
    max(pa."Outcome")::text as latest_attempt_outcome,
    ar."Id" as accepted_reading_id,
    ra."Id" as risk_assessment_id,
    ra."SimulationRunId" as risk_simulation_run_id,
    ra."RiskScore" as risk_score,
    ra."RiskLevel" as risk_level,
    snap."Id" as area_snapshot_id,
    snap."AggregateRiskScore" as aggregate_risk_score,
    snap."AggregateRiskLevel" as aggregate_risk_level
from pipeline.event_inbox e
left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
left join lateral (
    select s.*
    from projection.area_risk_snapshot_log s
    where s."SimulationRunId" is not distinct from ra."SimulationRunId"
      and s."AreaId" = ra."AreaId"
      and s."SnapshotTimestamp" = ra."Timestamp"
    order by s."CreatedAt" desc, s."Id"
    limit 1
) snap on true
group by
    e."Id",
    e."EventId",
    e."CorrelationId",
    e."EventType",
    e."AreaId",
    e."EventTime",
    ar."Id",
    ra."Id",
    ra."SimulationRunId",
    ra."RiskScore",
    ra."RiskLevel",
    snap."Id",
    snap."AggregateRiskScore",
    snap."AggregateRiskLevel"
order by e."EventTime" desc
limit 200;
\o


