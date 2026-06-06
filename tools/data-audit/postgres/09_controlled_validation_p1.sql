\set retry_summary_file :out_dir '/retry_summary.csv'
\set retry_transitions_file :out_dir '/retry_transitions.csv'
\set retry_then_success_file :out_dir '/retry_then_success.csv'
\set retry_to_quarantine_file :out_dir '/retry_to_quarantine.csv'
\set processing_faults_file :out_dir '/processing_faults_by_case.csv'
\set p1_expected_vs_observed_file :out_dir '/p1_expected_vs_observed.csv'
\set p1_traceability_file :out_dir '/p1_negative_traceability_m5.csv'
\if :{?run_label}
\else
\set run_label ''
\endif
\pset format csv

\o :retry_summary_file
with attempts as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        e."Status"::text as inbox_status,
        pa."AttemptNumber" as attempt_number,
        pa."Outcome"::text as attempt_outcome,
        pa."ErrorCode" as error_code,
        pa."StartedAt" as started_at,
        pa."FinishedAt" as finished_at
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
)
select
    fault_case_id,
    inbox_id,
    event_id,
    correlation_id,
    max(inbox_status) as inbox_status,
    count(attempt_number) as attempt_count,
    count(*) filter (where attempt_outcome = '3') as retry_scheduled_count,
    count(*) filter (where attempt_outcome = '1') as succeeded_count,
    count(*) filter (where attempt_outcome = '4') as quarantined_attempt_count,
    string_agg(distinct coalesce(error_code, '<null>'), ';' order by coalesce(error_code, '<null>')) as error_codes,
    min(started_at) as first_attempt_started_at,
    max(finished_at) as last_attempt_finished_at
from attempts
group by fault_case_id, inbox_id, event_id, correlation_id
order by last_attempt_finished_at desc nulls last, fault_case_id, correlation_id;
\o

\o :retry_transitions_file
with ordered_attempts as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        pa."AttemptNumber" as attempt_number,
        pa."Outcome"::text as attempt_outcome,
        pa."ErrorCode" as error_code,
        lag(pa."Outcome"::text) over (
            partition by e."Id"
            order by pa."AttemptNumber"
        ) as previous_attempt_outcome,
        lag(pa."ErrorCode") over (
            partition by e."Id"
            order by pa."AttemptNumber"
        ) as previous_error_code,
        pa."StartedAt" as started_at,
        pa."FinishedAt" as finished_at
    from pipeline.event_inbox e
    join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
)
select
    fault_case_id,
    inbox_id,
    event_id,
    correlation_id,
    attempt_number,
    previous_attempt_outcome,
    previous_error_code,
    attempt_outcome,
    error_code,
    started_at,
    finished_at
from ordered_attempts
order by correlation_id, attempt_number;
\o

\o :retry_then_success_file
with rollup as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        count(pa."Id") as attempt_count,
        count(*) filter (where pa."Outcome"::text = '3') as retry_scheduled_count,
        count(*) filter (where pa."Outcome"::text = '1') as succeeded_count,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        max(pa."ErrorCode") filter (where pa."ErrorCode" is not null) as latest_error_code,
        max(e."LastProcessedAt") as last_processed_at
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    group by e."Id", e."EventId", e."CorrelationId"
)
select *
from rollup
where retry_scheduled_count > 0
  and succeeded_count > 0
  and accepted_readings = 1
order by last_processed_at desc nulls last, fault_case_id, correlation_id;
\o

\o :retry_to_quarantine_file
with rollup as (
    select
        coalesce(
            substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
            '<unmapped>'
        ) as fault_case_id,
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        count(pa."Id") as attempt_count,
        count(*) filter (where pa."Outcome"::text = '3') as retry_scheduled_count,
        count(*) filter (where pa."Outcome"::text = '4') as quarantined_attempt_count,
        max(q."QuarantineCode") as quarantine_code,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        max(q."QuarantinedAt") as quarantined_at
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    where e."CorrelationId" like 'cv:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    group by e."Id", e."EventId", e."CorrelationId"
)
select *
from rollup
where quarantined_attempt_count > 0
  or quarantine_code is not null
order by quarantined_at desc nulls last, fault_case_id, correlation_id;
\o

\o :processing_faults_file
select
    coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ) as fault_case_id,
    coalesce(pa."ErrorCode", '<null>') as error_code,
    pa."Outcome"::text as processing_outcome,
    count(*) as attempt_count,
    min(pa."StartedAt") as first_started_at,
    max(pa."FinishedAt") as last_finished_at
from pipeline.processing_attempts pa
left join pipeline.event_inbox e on e."Id" = pa."InboxEventId"
where e."CorrelationId" like 'cv:%'
  and (
        :'run_label' = ''
        or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
  )
  and coalesce(pa."ErrorCode", '') in (
        'transient_failure',
        'permanent_failure',
        'sensor_inactive',
        'sensor_area_mismatch'
  )
group by
    coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ),
    coalesce(pa."ErrorCode", '<null>'),
    pa."Outcome"::text
order by attempt_count desc, fault_case_id, error_code, processing_outcome;
\o

\o :p1_expected_vs_observed_file
with expected_cases(fault_case_id, fault_layer, expected_outcome, expected_reason_code) as (
    values
        ('N5_TRANSIENT_FAILURE', 'processing', 'retry_then_success', 'transient_failure'),
        ('N6_PERMANENT_FAILURE', 'processing', 'quarantined', 'permanent_failure'),
        ('N7_SENSOR_INACTIVE', 'processing', 'quarantined', 'sensor_inactive'),
        ('N8_AREA_MISMATCH', 'processing', 'quarantined', 'sensor_area_mismatch')
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as observed_inbox_count,
        count(distinct pa."Id") as observed_attempt_count,
        count(*) filter (where pa."Outcome"::text = '3') as observed_retry_scheduled_count,
        count(*) filter (where pa."Outcome"::text = '1') as observed_succeeded_attempt_count,
        count(*) filter (where pa."Outcome"::text = '4') as observed_quarantined_attempt_count,
        count(distinct r."Id") as observed_rejected_count,
        count(distinct q."Id") as observed_quarantined_count,
        count(distinct ar."Id") as observed_accepted_count,
        count(distinct ra."Id") as observed_risk_count,
        count(*) filter (where pa."ErrorCode" = ec.expected_reason_code) as observed_expected_error_count,
        count(*) filter (where q."QuarantineCode" = ec.expected_reason_code) as observed_expected_quarantine_count
    from expected_cases ec
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = ec.fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.fault_layer,
    ec.expected_outcome,
    ec.expected_reason_code,
    o.observed_inbox_count,
    o.observed_attempt_count,
    o.observed_retry_scheduled_count,
    o.observed_succeeded_attempt_count,
    o.observed_quarantined_attempt_count,
    o.observed_rejected_count,
    o.observed_quarantined_count,
    o.observed_accepted_count,
    o.observed_risk_count,
    case
        when ec.expected_outcome = 'retry_then_success'
         and o.observed_retry_scheduled_count > 0
         and o.observed_succeeded_attempt_count > 0
         and o.observed_expected_error_count > 0
         and o.observed_quarantined_count = 0
         and o.observed_rejected_count = 0
         and o.observed_accepted_count = 1
         and o.observed_risk_count <= 1
            then 'matched'
        when ec.expected_outcome = 'quarantined'
         and o.observed_quarantined_count > 0
         and (o.observed_expected_error_count > 0 or o.observed_expected_quarantine_count > 0)
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
        when ec.fault_case_id in ('N7_SENSOR_INACTIVE', 'N8_AREA_MISMATCH')
            then 'N7/N8 dependem de fixture segura; se nao foram executados, classificar como P1.5 no relatorio.'
        else null
    end as limitation
from expected_cases ec
join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id;
\o

\o :p1_traceability_file
select
    coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
    ) as fault_case_id,
    e."Id" as inbox_id,
    e."EventId" as event_id,
    e."CorrelationId" as correlation_id,
    e."Status"::text as inbox_status,
    count(distinct pa."Id") as processing_attempts,
    (array_agg(pa."Outcome"::text order by pa."AttemptNumber" desc) filter (where pa."Id" is not null))[1] as latest_attempt_outcome,
    (array_agg(pa."ErrorCode" order by pa."AttemptNumber" desc) filter (where pa."Id" is not null))[1] as latest_error_code,
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
  and coalesce(
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}'),
        '<unmapped>'
      ) in (
        'N5_TRANSIENT_FAILURE',
        'N6_PERMANENT_FAILURE',
        'N7_SENSOR_INACTIVE',
        'N8_AREA_MISMATCH'
      )
group by e."Id", e."EventId", e."CorrelationId", e."Status"
order by last_seen_at desc nulls last, fault_case_id, correlation_id;
\o
