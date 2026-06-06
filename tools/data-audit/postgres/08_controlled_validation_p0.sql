\set rejected_file :out_dir '/rejected_by_reason.csv'
\set quarantined_file :out_dir '/quarantined_by_reason.csv'
\set processing_file :out_dir '/processing_errors_by_code.csv'
\set duplicate_file :out_dir '/duplicate_mismatch_summary.csv'
\set negative_traceability_file :out_dir '/negative_traceability_m5.csv'
\set expected_vs_observed_file :out_dir '/expected_vs_observed_fault_cases.csv'
\set scenario_profile_file :out_dir '/scenario_profile_summary.csv'
\if :{?run_label}
\else
\set run_label ''
\endif
\pset format csv

\o :rejected_file
with rejected as (
    select
        r."Id",
        r."InboxEventId",
        r."EventId",
        r."RejectionCode",
        r."RejectionReason",
        r."RejectedAt",
        coalesce(
            substring(coalesce(r."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(r."RawBodyUtf8", '') from '"faultCaseId":"([^"]+)"'),
            substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}')
        ) as fault_case_id
    from pipeline.rejected_events r
    where :'run_label' = ''
       or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
       or coalesce(r."RawBodyUtf8", '') like '%"runLabel":"' || :'run_label' || '"%'
       or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':%'
)
select
    coalesce(fault_case_id, '<unmapped>') as fault_case_id,
    "RejectionCode" as rejection_code,
    count(*) as rejected_count,
    min("RejectedAt") as first_rejected_at,
    max("RejectedAt") as last_rejected_at
from rejected
group by coalesce(fault_case_id, '<unmapped>'), "RejectionCode"
order by rejected_count desc, fault_case_id, rejection_code;
\o

\o :quarantined_file
with quarantined as (
    select
        q."Id",
        q."InboxEventId",
        q."EventId",
        q."QuarantineCode",
        q."QuarantineReason",
        q."QuarantinedAt",
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(q."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}')
        ) as fault_case_id
    from pipeline.quarantined_events q
    left join pipeline.event_inbox e on e."Id" = q."InboxEventId"
    where :'run_label' = ''
       or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       or coalesce(q."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
)
select
    coalesce(fault_case_id, '<unmapped>') as fault_case_id,
    "QuarantineCode" as quarantine_code,
    count(*) as quarantined_count,
    min("QuarantinedAt") as first_quarantined_at,
    max("QuarantinedAt") as last_quarantined_at
from quarantined
group by coalesce(fault_case_id, '<unmapped>'), "QuarantineCode"
order by quarantined_count desc, fault_case_id, quarantine_code;
\o

\o :processing_file
select
    coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ) as fault_case_id,
    pa."Stage" as processing_stage,
    pa."Outcome"::text as processing_outcome,
    coalesce(pa."ErrorCode", '<null>') as error_code,
    count(*) as attempt_count,
    min(pa."StartedAt") as first_started_at,
    max(pa."FinishedAt") as last_finished_at
from pipeline.processing_attempts pa
left join pipeline.event_inbox e on e."Id" = pa."InboxEventId"
where :'run_label' = ''
   or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
group by
    coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ),
    pa."Stage",
    pa."Outcome",
    coalesce(pa."ErrorCode", '<null>')
order by attempt_count desc, fault_case_id, processing_stage, processing_outcome, error_code;
\o

\o :duplicate_file
select
    coalesce(
        substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        substring(coalesce(r."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ) as fault_case_id,
    r."InboxEventId" as original_inbox_id,
    r."EventId" as duplicate_event_id,
    count(*) as rejection_count,
    min(r."RejectedAt") as first_rejected_at,
    max(r."RejectedAt") as last_rejected_at
from pipeline.rejected_events r
where r."RejectionCode" = 'duplicate_payload_mismatch'
  and (
        :'run_label' = ''
        or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':%'
        or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
  )
group by
    coalesce(
        substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        substring(coalesce(r."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ),
    r."InboxEventId",
    r."EventId"
order by last_rejected_at desc;
\o

\o :negative_traceability_file
with inbox_paths as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        e."Status"::text as inbox_status,
        count(distinct pa."Id") as processing_attempts,
        max(pa."Outcome")::text as latest_attempt_outcome,
        max(pa."ErrorCode") as latest_error_code,
        max(q."QuarantineCode") as quarantine_code,
        max(r."RejectionCode") as rejection_code,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        min(e."ReceivedAt") as first_seen_at,
        max(coalesce(q."QuarantinedAt", r."RejectedAt", e."LastProcessedAt", e."ReceivedAt")) as last_seen_at
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    group by e."Id", e."EventId", e."CorrelationId", e."Status"
),
pre_inbox_rejections as (
    select
        coalesce(
            substring(coalesce(r."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(r."RawBodyUtf8", '') from '"faultCaseId":"([^"]+)"'),
            substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        r."InboxEventId" as inbox_id,
        r."EventId" as event_id,
        substring(coalesce(r."RawBodyUtf8", '') from '(cv:[^"]*:[A-Z0-9_]+:[0-9]{3})') as correlation_id,
        '<pre_inbox>' as inbox_status,
        0 as processing_attempts,
        null as latest_attempt_outcome,
        null as latest_error_code,
        null as quarantine_code,
        r."RejectionCode" as rejection_code,
        0 as accepted_readings,
        0 as risk_assessments,
        r."RejectedAt" as first_seen_at,
        r."RejectedAt" as last_seen_at
    from pipeline.rejected_events r
    where r."InboxEventId" is null
      and (
            coalesce(r."RawBodyUtf8", '') like '%controlledValidationRunId%'
            or coalesce(r."RawBodyUtf8", '') like '%cv:%'
            or coalesce(r."MetadataJson", '') like '%cv:%'
      )
      and (
            :'run_label' = ''
            or coalesce(r."RawBodyUtf8", '') like '%"runLabel":"' || :'run_label' || '"%'
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
      )
)
select *
from inbox_paths
union all
select *
from pre_inbox_rejections
order by last_seen_at desc, fault_case_id;
\o

\o :expected_vs_observed_file
with expected_cases(fault_case_id, fault_layer, expected_outcome, expected_reason_code) as (
    values
        ('N1_INVALID_JSON', 'event_transport', 'rejected', 'invalid_json'),
        ('N1_MISSING_PAYLOAD', 'event_transport', 'rejected', 'missing_payload'),
        ('N2_INVALID_OPERATIONAL_STATE', 'event_transport', 'rejected', 'invalid_operational_state'),
        ('N3_SENSOR_NOT_FOUND', 'processing', 'quarantined', 'sensor_not_found'),
        ('N4_DUPLICATE_PAYLOAD_MISMATCH', 'event_transport', 'rejected', 'duplicate_payload_mismatch')
),
rejected_observed as (
    select
        coalesce(
            substring(coalesce(r."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(r."RawBodyUtf8", '') from '"faultCaseId":"([^"]+)"'),
            substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}')
        ) as fault_case_id,
        r."RejectionCode" as reason_code,
        count(*) as rejected_count
    from pipeline.rejected_events r
    where :'run_label' = ''
       or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
       or coalesce(r."RawBodyUtf8", '') like '%"runLabel":"' || :'run_label' || '"%'
       or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':%'
    group by
        coalesce(
            substring(coalesce(r."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(r."RawBodyUtf8", '') from '"faultCaseId":"([^"]+)"'),
            substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}')
        ),
        r."RejectionCode"
),
quarantine_observed as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(q."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}')
        ) as fault_case_id,
        q."QuarantineCode" as reason_code,
        count(*) as quarantined_count
    from pipeline.quarantined_events q
    left join pipeline.event_inbox e on e."Id" = q."InboxEventId"
    where :'run_label' = ''
       or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       or coalesce(q."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
    group by
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            substring(coalesce(q."MetadataJson", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}')
        ),
        q."QuarantineCode"
),
projection_observed as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        count(distinct e."Id") as inbox_count,
        count(distinct pa."Id") as attempt_count,
        count(distinct ar."Id") as accepted_count,
        count(distinct ra."Id") as risk_count
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    group by coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^"]*:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    )
),
observed as (
    select
        e.fault_case_id,
        coalesce(sum(ro.rejected_count), 0) as observed_rejected_count,
        coalesce(sum(qo.quarantined_count), 0) as observed_quarantined_count,
        coalesce(max(po.inbox_count), 0) as observed_inbox_count,
        coalesce(max(po.attempt_count), 0) as observed_attempt_count,
        coalesce(max(po.accepted_count), 0) as observed_accepted_count,
        coalesce(max(po.risk_count), 0) as observed_risk_count
    from expected_cases e
    left join rejected_observed ro
        on ro.fault_case_id = e.fault_case_id
       and ro.reason_code = e.expected_reason_code
    left join quarantine_observed qo
        on qo.fault_case_id = e.fault_case_id
       and qo.reason_code = e.expected_reason_code
    left join projection_observed po
        on po.fault_case_id = e.fault_case_id
    group by e.fault_case_id
)
select
    e.fault_case_id,
    e.fault_layer,
    e.expected_outcome,
    e.expected_reason_code,
    o.observed_inbox_count,
    o.observed_attempt_count,
    o.observed_rejected_count,
    o.observed_quarantined_count,
    o.observed_accepted_count,
    o.observed_risk_count,
    case
        when e.expected_outcome = 'rejected'
         and e.fault_case_id <> 'N4_DUPLICATE_PAYLOAD_MISMATCH'
         and o.observed_rejected_count > 0
         and o.observed_quarantined_count = 0
         and o.observed_accepted_count = 0
         and o.observed_risk_count = 0
            then 'matched'
        when e.fault_case_id = 'N4_DUPLICATE_PAYLOAD_MISMATCH'
         and o.observed_rejected_count > 0
         and o.observed_quarantined_count = 0
         and o.observed_accepted_count <= 1
         and o.observed_risk_count <= 1
            then 'matched'
        when e.expected_outcome = 'quarantined'
         and o.observed_quarantined_count > 0
         and o.observed_rejected_count = 0
         and o.observed_accepted_count = 0
         and o.observed_risk_count = 0
            then 'matched'
        when o.observed_inbox_count = 0
         and o.observed_attempt_count = 0
         and o.observed_rejected_count = 0
         and o.observed_quarantined_count = 0
         and o.observed_accepted_count = 0
         and o.observed_risk_count = 0
            then 'missing'
        else 'unexpected'
    end as status,
    case
        when e.fault_case_id = 'N1_INVALID_JSON'
            then 'invalid_json may require sidecar raw body hash because no structured envelope metadata is available pre-inbox.'
        when e.fault_case_id = 'N4_DUPLICATE_PAYLOAD_MISMATCH'
            then 'one accepted/risk row is allowed for the setup message; more than one indicates duplicate leakage.'
        else null
    end as limitation
from expected_cases e
join observed o on o.fault_case_id = e.fault_case_id
order by e.fault_case_id;
\o

\o :scenario_profile_file
with cv_events as (
    select
        substring(e."CorrelationId" from 'cv:([^:]+):[A-Z0-9_]+:[0-9]{3}') as run_label,
        substring(e."CorrelationId" from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') as fault_case_id,
        e."ReceivedAt" as observed_at
    from pipeline.event_inbox e
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    union all
    select
        coalesce(
            substring(coalesce(r."RawBodyUtf8", '') from '"runLabel":"([^"]+)"'),
            substring(coalesce(r."RawBodyUtf8", '') from 'cv:([^:]+):[A-Z0-9_]+:[0-9]{3}')
        ) as run_label,
        coalesce(
            substring(coalesce(r."RawBodyUtf8", '') from '"faultCaseId":"([^"]+)"'),
            substring(coalesce(r."RawBodyUtf8", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}')
        ) as fault_case_id,
        r."RejectedAt" as observed_at
    from pipeline.rejected_events r
    where (
            coalesce(r."RawBodyUtf8", '') like '%controlledValidationRunId%'
            or coalesce(r."RawBodyUtf8", '') like '%cv:%'
            or coalesce(r."MetadataJson", '') like '%cv:%'
    )
      and (
            :'run_label' = ''
            or coalesce(r."RawBodyUtf8", '') like '%"runLabel":"' || :'run_label' || '"%'
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':%'
      )
)
select
    coalesce(run_label, '<unmapped>') as run_label,
    '<sidecar_manifest_required>' as scenario_code,
    count(distinct fault_case_id) as observed_fault_case_count,
    count(*) as observed_rows,
    min(observed_at) as first_observed_at,
    max(observed_at) as last_observed_at,
    'scenario_code, controlled_validation_run_id and raw body hashes are expected from the sidecar manifest produced by the controlled validation runner.' as limitation
from cv_events
group by coalesce(run_label, '<unmapped>')
order by last_observed_at desc;
\o
