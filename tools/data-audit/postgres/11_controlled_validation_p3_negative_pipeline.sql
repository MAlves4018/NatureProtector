\set p3_expected_file :out_dir '/p3_expected_vs_observed.csv'
\set p3_rejected_file :out_dir '/p3_rejected_by_fault_case.csv'
\set p3_quarantined_file :out_dir '/p3_quarantined_by_fault_case.csv'
\set p3_retry_paths_file :out_dir '/p3_retry_paths_by_fault_case.csv'
\set p3_processing_attempts_file :out_dir '/p3_processing_attempts_by_fault_case.csv'
\set p3_m3_label_support_file :out_dir '/p3_m3_label_support.csv'
\set p3_traceability_file :out_dir '/p3_negative_m5_traceability.csv'
\set p3_unexpected_projection_file :out_dir '/p3_unexpected_accepted_or_risk.csv'
\set p3_blocked_file :out_dir '/p3_blocked_or_skipped_cases.csv'
\if :{?run_label}
\else
\set run_label ''
\endif
\pset format csv

\o :p3_expected_file
with expected_cases(
    fault_case_id,
    fault_layer,
    expected_outcome,
    expected_reason_code,
    expected_events,
    expected_published_events,
    expected_effective_inbox_events,
    first_sequence,
    execution_policy,
    allow_setup_projection
) as (
    values
        ('P3_REJECT_INVALID_JSON', 'event_transport', 'rejected', 'invalid_json', 1, 1, 0, 1, 'required', false),
        ('P3_REJECT_MISSING_PAYLOAD', 'event_transport', 'rejected', 'missing_payload', 1, 1, 0, 2, 'required', false),
        ('P3_REJECT_UNSUPPORTED_EVENT_TYPE', 'event_transport', 'rejected', 'unsupported_event_type', 1, 1, 0, 3, 'required', false),
        ('P3_REJECT_UNSUPPORTED_SCHEMA_VERSION', 'event_transport', 'rejected', 'unsupported_schema_version', 1, 1, 0, 4, 'required', false),
        ('P3_REJECT_INVALID_OPERATIONAL_STATE', 'event_transport', 'rejected', 'invalid_operational_state', 1, 1, 0, 5, 'required', false),
        ('P3_QUARANTINE_SENSOR_NOT_FOUND', 'processing', 'quarantined', 'sensor_not_found', 1, 1, 1, 6, 'required', false),
        ('P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH', 'event_transport', 'rejected', 'duplicate_payload_mismatch', 2, 2, 1, 7, 'required', true),
        ('P3_RETRY_TRANSIENT_THEN_SUCCESS', 'processing', 'retry_then_success', 'transient_failure', 1, 1, 1, 9, 'required', true),
        ('P3_RETRY_EXHAUSTED_TO_QUARANTINE', 'processing', 'retry_to_quarantine', 'retries_exhausted', 1, 1, 1, 10, 'required', false),
        ('P3_PERMANENT_FAILURE_TO_QUARANTINE', 'processing', 'quarantined', 'permanent_failure', 1, 1, 1, 11, 'required', false),
        ('P3_QUARANTINE_SENSOR_INACTIVE', 'processing', 'quarantined', 'sensor_inactive', 0, 0, 0, 12, 'blocked_needs_fixture', false),
        ('P3_QUARANTINE_SENSOR_AREA_MISMATCH', 'processing', 'quarantined', 'sensor_area_mismatch', 0, 0, 0, 13, 'blocked_needs_fixture', false)
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as inbox_events,
        count(distinct e."EventId") as event_ids,
        count(distinct r."Id") as rejected_events,
        count(distinct r."Id") filter (where r."RejectionCode" = ec.expected_reason_code) as expected_rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct q."Id") filter (where q."QuarantineCode" = ec.expected_reason_code) as expected_quarantined_events,
        count(distinct q."Id") filter (where q."QuarantineCode" = 'retries_exhausted') as retries_exhausted_quarantines,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct pa."Id") as processing_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_scheduled_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '1') as succeeded_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '4') as quarantined_attempts,
        count(distinct pa."Id") filter (where pa."ErrorCode" = ec.expected_reason_code) as expected_error_attempts,
        count(distinct pa."Id") filter (where pa."ErrorCode" = 'transient_failure') as transient_failure_attempts,
        max(e."Status"::text) as latest_inbox_status,
        max(q."QuarantineCode") as latest_quarantine_code,
        max(r."RejectionCode") as latest_rejection_code
    from expected_cases ec
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = ec.fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join pipeline.rejected_events r
        on (
            r."InboxEventId" = e."Id"
            or coalesce(r."RawBodyUtf8", '') like '%' || ec.fault_case_id || '%'
            or coalesce(r."MetadataJson", '') like '%' || ec.fault_case_id || '%'
        )
       and (
            :'run_label' = ''
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':' || ec.fault_case_id || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':' || ec.fault_case_id || ':%'
       )
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.fault_layer,
    ec.expected_outcome,
    ec.expected_reason_code,
    ec.expected_events,
    ec.expected_published_events,
    ec.expected_effective_inbox_events,
    ec.execution_policy,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.event_ids, 0) as event_ids,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.expected_rejected_events, 0) as expected_rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.expected_quarantined_events, 0) as expected_quarantined_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.processing_attempts, 0) as processing_attempts,
    coalesce(o.retry_scheduled_attempts, 0) as retry_scheduled_attempts,
    coalesce(o.succeeded_attempts, 0) as succeeded_attempts,
    coalesce(o.quarantined_attempts, 0) as quarantined_attempts,
    coalesce(o.expected_error_attempts, 0) as expected_error_attempts,
    coalesce(o.transient_failure_attempts, 0) as transient_failure_attempts,
    o.latest_inbox_status,
    o.latest_rejection_code,
    o.latest_quarantine_code,
    case
        when ec.execution_policy = 'blocked_needs_fixture'
        then 'blocked_needs_fixture'
        when ec.expected_outcome = 'rejected'
         and ec.allow_setup_projection = false
         and coalesce(o.expected_rejected_events, 0) > 0
         and coalesce(o.accepted_readings, 0) = 0
         and coalesce(o.risk_assessments, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
        then 'matched'
        when ec.expected_outcome = 'rejected'
         and ec.allow_setup_projection = true
         and coalesce(o.expected_rejected_events, 0) > 0
         and coalesce(o.inbox_events, 0) = ec.expected_effective_inbox_events
         and coalesce(o.accepted_readings, 0) <= 1
         and coalesce(o.risk_assessments, 0) <= 1
         and coalesce(o.quarantined_events, 0) = 0
        then 'matched_with_setup_projection'
        when ec.expected_outcome = 'quarantined'
         and coalesce(o.expected_quarantined_events, 0) > 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.accepted_readings, 0) = 0
         and coalesce(o.risk_assessments, 0) = 0
        then 'matched'
        when ec.expected_outcome = 'retry_then_success'
         and coalesce(o.retry_scheduled_attempts, 0) > 0
         and coalesce(o.succeeded_attempts, 0) > 0
         and coalesce(o.expected_error_attempts, 0) > 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.accepted_readings, 0) = 1
         and coalesce(o.risk_assessments, 0) = 1
        then 'matched'
        when ec.expected_outcome = 'retry_to_quarantine'
         and coalesce(o.retry_scheduled_attempts, 0) > 0
         and coalesce(o.transient_failure_attempts, 0) > 0
         and coalesce(o.retries_exhausted_quarantines, 0) > 0
         and coalesce(o.quarantined_attempts, 0) > 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.accepted_readings, 0) = 0
         and coalesce(o.risk_assessments, 0) = 0
        then 'matched'
        when coalesce(o.inbox_events, 0) = 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.accepted_readings, 0) = 0
         and coalesce(o.risk_assessments, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status,
    case
        when ec.execution_policy = 'blocked_needs_fixture'
        then 'No safe fixture exists in current control-plane data; do not mutate nominal sensors or areas.'
        when ec.fault_case_id = 'P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH'
        then 'The first envelope is a valid setup path; only the divergent duplicate is expected to be rejected.'
        else null
    end as limitation
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.first_sequence;
\o

\o :p3_rejected_file
with expected_cases(fault_case_id, expected_reason_code, expected_outcome) as (
    values
        ('P3_REJECT_INVALID_JSON', 'invalid_json', 'rejected'),
        ('P3_REJECT_MISSING_PAYLOAD', 'missing_payload', 'rejected'),
        ('P3_REJECT_UNSUPPORTED_EVENT_TYPE', 'unsupported_event_type', 'rejected'),
        ('P3_REJECT_UNSUPPORTED_SCHEMA_VERSION', 'unsupported_schema_version', 'rejected'),
        ('P3_REJECT_INVALID_OPERATIONAL_STATE', 'invalid_operational_state', 'rejected'),
        ('P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH', 'duplicate_payload_mismatch', 'rejected')
),
observed as (
    select
        ec.fault_case_id,
        r."RejectionCode" as rejection_code,
        count(distinct r."Id") as rejected_events,
        count(distinct r."Id") filter (where r."InboxEventId" is null) as pre_inbox_rejections,
        count(distinct r."Id") filter (where r."InboxEventId" is not null) as inbox_linked_rejections,
        min(r."RejectedAt") as first_rejected_at,
        max(r."RejectedAt") as last_rejected_at
    from expected_cases ec
    left join pipeline.rejected_events r
        on (
            coalesce(r."RawBodyUtf8", '') like '%' || ec.fault_case_id || '%'
            or coalesce(r."MetadataJson", '') like '%' || ec.fault_case_id || '%'
        )
       and (
            :'run_label' = ''
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':' || ec.fault_case_id || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':' || ec.fault_case_id || ':%'
       )
    group by ec.fault_case_id, r."RejectionCode"
)
select
    ec.fault_case_id,
    ec.expected_reason_code,
    coalesce(o.rejection_code, '<missing>') as rejection_code,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.pre_inbox_rejections, 0) as pre_inbox_rejections,
    coalesce(o.inbox_linked_rejections, 0) as inbox_linked_rejections,
    o.first_rejected_at,
    o.last_rejected_at,
    case
        when coalesce(o.rejected_events, 0) > 0 and o.rejection_code = ec.expected_reason_code
        then 'matched'
        when coalesce(o.rejected_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id, rejection_code;
\o

\o :p3_quarantined_file
with expected_cases(fault_case_id, expected_quarantine_code, execution_policy) as (
    values
        ('P3_QUARANTINE_SENSOR_NOT_FOUND', 'sensor_not_found', 'required'),
        ('P3_RETRY_EXHAUSTED_TO_QUARANTINE', 'retries_exhausted', 'required'),
        ('P3_PERMANENT_FAILURE_TO_QUARANTINE', 'permanent_failure', 'required'),
        ('P3_QUARANTINE_SENSOR_INACTIVE', 'sensor_inactive', 'blocked_needs_fixture'),
        ('P3_QUARANTINE_SENSOR_AREA_MISMATCH', 'sensor_area_mismatch', 'blocked_needs_fixture')
),
observed as (
    select
        ec.fault_case_id,
        q."QuarantineCode" as quarantine_code,
        count(distinct e."Id") as inbox_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") as processing_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_scheduled_attempts,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        min(q."QuarantinedAt") as first_quarantined_at,
        max(q."QuarantinedAt") as last_quarantined_at
    from expected_cases ec
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = ec.fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    group by ec.fault_case_id, q."QuarantineCode"
)
select
    ec.fault_case_id,
    ec.expected_quarantine_code,
    ec.execution_policy,
    coalesce(o.quarantine_code, '<missing>') as quarantine_code,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.processing_attempts, 0) as processing_attempts,
    coalesce(o.retry_scheduled_attempts, 0) as retry_scheduled_attempts,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    o.first_quarantined_at,
    o.last_quarantined_at,
    case
        when ec.execution_policy = 'blocked_needs_fixture'
        then 'blocked_needs_fixture'
        when coalesce(o.quarantined_events, 0) > 0
         and o.quarantine_code = ec.expected_quarantine_code
         and coalesce(o.accepted_readings, 0) = 0
         and coalesce(o.risk_assessments, 0) = 0
        then 'matched'
        when coalesce(o.quarantined_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id, quarantine_code;
\o

\o :p3_retry_paths_file
with expected_cases(fault_case_id, expected_path, expected_final_code) as (
    values
        ('P3_RETRY_TRANSIENT_THEN_SUCCESS', 'retry_then_success', 'transient_failure'),
        ('P3_RETRY_EXHAUSTED_TO_QUARANTINE', 'retry_to_quarantine', 'retries_exhausted')
),
rollup as (
    select
        ec.fault_case_id,
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        e."Status"::text as inbox_status,
        count(distinct pa."Id") as attempt_count,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_scheduled_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '1') as succeeded_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '4') as quarantined_attempts,
        string_agg(
            pa."AttemptNumber"::text || ':' || pa."Outcome"::text || ':' || coalesce(pa."ErrorCode", '<null>'),
            ' > '
            order by pa."AttemptNumber"
        ) filter (where pa."Id" is not null) as attempt_path,
        max(q."QuarantineCode") as quarantine_code,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments
    from expected_cases ec
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = ec.fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    group by ec.fault_case_id, e."Id", e."EventId", e."CorrelationId", e."Status"
)
select
    ec.fault_case_id,
    ec.expected_path,
    ec.expected_final_code,
    r.inbox_id,
    r.event_id,
    r.correlation_id,
    r.inbox_status,
    coalesce(r.attempt_count, 0) as attempt_count,
    coalesce(r.retry_scheduled_attempts, 0) as retry_scheduled_attempts,
    coalesce(r.succeeded_attempts, 0) as succeeded_attempts,
    coalesce(r.quarantined_attempts, 0) as quarantined_attempts,
    r.attempt_path,
    r.quarantine_code,
    coalesce(r.accepted_readings, 0) as accepted_readings,
    coalesce(r.risk_assessments, 0) as risk_assessments,
    case
        when ec.expected_path = 'retry_then_success'
         and coalesce(r.retry_scheduled_attempts, 0) > 0
         and coalesce(r.succeeded_attempts, 0) > 0
         and coalesce(r.accepted_readings, 0) = 1
         and coalesce(r.risk_assessments, 0) = 1
         and coalesce(r.quarantined_attempts, 0) = 0
        then 'matched'
        when ec.expected_path = 'retry_to_quarantine'
         and coalesce(r.retry_scheduled_attempts, 0) > 0
         and coalesce(r.quarantined_attempts, 0) > 0
         and r.quarantine_code = ec.expected_final_code
         and coalesce(r.accepted_readings, 0) = 0
         and coalesce(r.risk_assessments, 0) = 0
        then 'matched'
        when r.inbox_id is null
        then 'missing'
        else 'unexpected'
    end as status
from expected_cases ec
left join rollup r on r.fault_case_id = ec.fault_case_id
order by ec.fault_case_id, r.correlation_id;
\o

\o :p3_processing_attempts_file
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
    pa."ErrorMessage" as error_message,
    pa."StartedAt" as started_at,
    pa."FinishedAt" as finished_at
from pipeline.event_inbox e
join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
where substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') like 'P3_%'
  and (
        :'run_label' = ''
        or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
  )
order by fault_case_id, correlation_id, attempt_number;
\o

\o :p3_m3_label_support_file
with expected_labels(label_source, label_code, expected_fault_case_id, execution_policy) as (
    values
        ('rejected', 'invalid_json', 'P3_REJECT_INVALID_JSON', 'required'),
        ('rejected', 'missing_payload', 'P3_REJECT_MISSING_PAYLOAD', 'required'),
        ('rejected', 'unsupported_event_type', 'P3_REJECT_UNSUPPORTED_EVENT_TYPE', 'required'),
        ('rejected', 'unsupported_schema_version', 'P3_REJECT_UNSUPPORTED_SCHEMA_VERSION', 'required'),
        ('rejected', 'invalid_operational_state', 'P3_REJECT_INVALID_OPERATIONAL_STATE', 'required'),
        ('rejected', 'duplicate_payload_mismatch', 'P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH', 'required'),
        ('quarantined', 'sensor_not_found', 'P3_QUARANTINE_SENSOR_NOT_FOUND', 'required'),
        ('quarantined', 'retries_exhausted', 'P3_RETRY_EXHAUSTED_TO_QUARANTINE', 'required'),
        ('quarantined', 'permanent_failure', 'P3_PERMANENT_FAILURE_TO_QUARANTINE', 'required'),
        ('attempt_error', 'transient_failure', 'P3_RETRY_TRANSIENT_THEN_SUCCESS', 'required'),
        ('blocked', 'sensor_inactive', 'P3_QUARANTINE_SENSOR_INACTIVE', 'blocked_needs_fixture'),
        ('blocked', 'sensor_area_mismatch', 'P3_QUARANTINE_SENSOR_AREA_MISMATCH', 'blocked_needs_fixture')
),
support as (
    select
        el.label_source,
        el.label_code,
        el.expected_fault_case_id,
        count(distinct r."Id") as support_count
    from expected_labels el
    left join pipeline.rejected_events r
        on el.label_source = 'rejected'
       and r."RejectionCode" = el.label_code
       and (
            coalesce(r."RawBodyUtf8", '') like '%' || el.expected_fault_case_id || '%'
            or coalesce(r."MetadataJson", '') like '%' || el.expected_fault_case_id || '%'
       )
       and (
            :'run_label' = ''
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':' || el.expected_fault_case_id || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':' || el.expected_fault_case_id || ':%'
       )
    group by el.label_source, el.label_code, el.expected_fault_case_id
    union all
    select
        el.label_source,
        el.label_code,
        el.expected_fault_case_id,
        count(distinct q."Id") as support_count
    from expected_labels el
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = el.expected_fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join pipeline.quarantined_events q
        on el.label_source = 'quarantined'
       and q."InboxEventId" = e."Id"
       and q."QuarantineCode" = el.label_code
    group by el.label_source, el.label_code, el.expected_fault_case_id
    union all
    select
        el.label_source,
        el.label_code,
        el.expected_fault_case_id,
        count(distinct pa."Id") as support_count
    from expected_labels el
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = el.expected_fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join pipeline.processing_attempts pa
        on el.label_source = 'attempt_error'
       and pa."InboxEventId" = e."Id"
       and pa."ErrorCode" = el.label_code
    group by el.label_source, el.label_code, el.expected_fault_case_id
)
select
    el.label_source,
    el.label_code,
    el.expected_fault_case_id,
    el.execution_policy,
    coalesce(sum(s.support_count), 0) as support_count,
    case
        when el.execution_policy = 'blocked_needs_fixture'
        then 'blocked_needs_fixture'
        when coalesce(sum(s.support_count), 0) > 0
        then 'supported'
        else 'missing'
    end as status,
    case
        when el.execution_policy = 'blocked_needs_fixture'
        then 'Current control-plane data has no safe inactive sensor or second-area fixture.'
        else null
    end as limitation
from expected_labels el
left join support s
    on s.label_source = el.label_source
   and s.label_code = el.label_code
   and s.expected_fault_case_id = el.expected_fault_case_id
group by el.label_source, el.label_code, el.expected_fault_case_id, el.execution_policy
order by el.label_source, el.label_code;
\o

\o :p3_traceability_file
with expected_cases(fault_case_id, expected_outcome, expected_events, first_sequence, setup_projection_allowed) as (
    values
        ('P3_REJECT_INVALID_JSON', 'rejected', 1, 1, false),
        ('P3_REJECT_MISSING_PAYLOAD', 'rejected', 1, 2, false),
        ('P3_REJECT_UNSUPPORTED_EVENT_TYPE', 'rejected', 1, 3, false),
        ('P3_REJECT_UNSUPPORTED_SCHEMA_VERSION', 'rejected', 1, 4, false),
        ('P3_REJECT_INVALID_OPERATIONAL_STATE', 'rejected', 1, 5, false),
        ('P3_QUARANTINE_SENSOR_NOT_FOUND', 'quarantined', 1, 6, false),
        ('P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH', 'rejected', 2, 7, true),
        ('P3_RETRY_TRANSIENT_THEN_SUCCESS', 'retry_then_success', 1, 9, true),
        ('P3_RETRY_EXHAUSTED_TO_QUARANTINE', 'retry_to_quarantine', 1, 10, false),
        ('P3_PERMANENT_FAILURE_TO_QUARANTINE', 'quarantined', 1, 11, false),
        ('P3_QUARANTINE_SENSOR_INACTIVE', 'blocked_needs_fixture', 0, 12, false),
        ('P3_QUARANTINE_SENSOR_AREA_MISMATCH', 'blocked_needs_fixture', 0, 13, false)
),
expected_slots as (
    select
        ec.fault_case_id,
        ec.expected_outcome,
        slot.slot_index,
        ec.first_sequence + slot.slot_index - 1 as expected_sequence,
        ec.setup_projection_allowed
    from expected_cases ec
    cross join lateral generate_series(1, greatest(ec.expected_events, 1)) as slot(slot_index)
),
observed_inbox as (
    select
        e."Id" as inbox_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        e."Status"::text as inbox_status,
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') as fault_case_id,
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:[A-Z0-9_]+:([0-9]{3})')::int as sequence,
        count(distinct pa."Id") as attempt_count,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_scheduled_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '1') as succeeded_attempts,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '4') as quarantined_attempts,
        max(pa."ErrorCode") filter (where pa."ErrorCode" is not null) as latest_error_code,
        max(q."QuarantineCode") as quarantine_code,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    where substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') like 'P3_%'
      and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
      )
    group by e."Id", e."EventId", e."CorrelationId", e."Status"
),
observed_rejected as (
    select
        es.fault_case_id,
        es.expected_sequence,
        count(distinct r."Id") as rejected_events,
        max(r."RejectionCode") as rejection_code,
        max(r."InboxEventId"::text) as rejected_inbox_id
    from expected_slots es
    left join pipeline.rejected_events r
        on (
            coalesce(r."RawBodyUtf8", '') like '%:' || es.fault_case_id || ':' || lpad(es.expected_sequence::text, 3, '0') || '%'
            or coalesce(r."MetadataJson", '') like '%:' || es.fault_case_id || ':' || lpad(es.expected_sequence::text, 3, '0') || '%'
            or (
                es.fault_case_id = 'P3_REJECT_INVALID_JSON'
                and coalesce(r."RawBodyUtf8", '') like '%' || es.fault_case_id || '%'
            )
        )
       and (
            :'run_label' = ''
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':' || es.fault_case_id || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':' || es.fault_case_id || ':%'
       )
    group by es.fault_case_id, es.expected_sequence
)
select
    es.fault_case_id,
    es.expected_outcome,
    es.slot_index,
    es.expected_sequence,
    oi.event_id,
    oi.correlation_id,
    oi.inbox_id,
    oi.inbox_status,
    coalesce(oi.attempt_count, 0) as attempt_count,
    coalesce(oi.retry_scheduled_attempts, 0) as retry_scheduled_attempts,
    coalesce(oi.succeeded_attempts, 0) as succeeded_attempts,
    coalesce(oi.quarantined_attempts, 0) as quarantined_attempts,
    oi.latest_error_code,
    oi.quarantine_code,
    coalesce(orj.rejected_events, 0) as rejected_events,
    orj.rejection_code,
    orj.rejected_inbox_id,
    coalesce(oi.accepted_readings, 0) as accepted_readings,
    coalesce(oi.risk_assessments, 0) as risk_assessments,
    case
        when es.expected_outcome = 'blocked_needs_fixture'
        then 'blocked_needs_fixture'
        when es.fault_case_id = 'P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH'
         and es.slot_index = 1
         and oi.inbox_id is not null
         and coalesce(oi.accepted_readings, 0) > 0
         and coalesce(oi.risk_assessments, 0) > 0
        then 'setup_positive_path_allowed'
        when es.expected_outcome = 'rejected'
         and coalesce(orj.rejected_events, 0) > 0
         and (
            es.setup_projection_allowed = true
            or (coalesce(oi.accepted_readings, 0) = 0 and coalesce(oi.risk_assessments, 0) = 0)
         )
        then 'negative_rejected_path'
        when es.expected_outcome = 'quarantined'
         and coalesce(oi.quarantined_attempts, 0) > 0
         and oi.quarantine_code is not null
         and coalesce(oi.accepted_readings, 0) = 0
         and coalesce(oi.risk_assessments, 0) = 0
        then 'negative_quarantine_path'
        when es.expected_outcome = 'retry_to_quarantine'
         and coalesce(oi.retry_scheduled_attempts, 0) > 0
         and coalesce(oi.quarantined_attempts, 0) > 0
         and oi.quarantine_code = 'retries_exhausted'
         and coalesce(oi.accepted_readings, 0) = 0
         and coalesce(oi.risk_assessments, 0) = 0
        then 'retry_to_negative_quarantine_path'
        when es.expected_outcome = 'retry_then_success'
         and coalesce(oi.retry_scheduled_attempts, 0) > 0
         and coalesce(oi.succeeded_attempts, 0) > 0
         and coalesce(oi.accepted_readings, 0) = 1
         and coalesce(oi.risk_assessments, 0) = 1
        then 'retry_to_positive_path'
        when oi.inbox_id is null and coalesce(orj.rejected_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as trace_status
from expected_slots es
left join observed_inbox oi
    on oi.fault_case_id = es.fault_case_id
   and oi.sequence = es.expected_sequence
left join observed_rejected orj
    on orj.fault_case_id = es.fault_case_id
   and orj.expected_sequence = es.expected_sequence
order by es.expected_sequence, es.slot_index;
\o

\o :p3_unexpected_projection_file
with expected_cases(fault_case_id, expected_outcome, allow_projection, allowed_max_accepted, allowed_max_risk) as (
    values
        ('P3_REJECT_INVALID_JSON', 'rejected', false, 0, 0),
        ('P3_REJECT_MISSING_PAYLOAD', 'rejected', false, 0, 0),
        ('P3_REJECT_UNSUPPORTED_EVENT_TYPE', 'rejected', false, 0, 0),
        ('P3_REJECT_UNSUPPORTED_SCHEMA_VERSION', 'rejected', false, 0, 0),
        ('P3_REJECT_INVALID_OPERATIONAL_STATE', 'rejected', false, 0, 0),
        ('P3_QUARANTINE_SENSOR_NOT_FOUND', 'quarantined', false, 0, 0),
        ('P3_QUARANTINE_DUPLICATE_PAYLOAD_MISMATCH', 'rejected_with_setup', true, 1, 1),
        ('P3_RETRY_EXHAUSTED_TO_QUARANTINE', 'retry_to_quarantine', false, 0, 0),
        ('P3_PERMANENT_FAILURE_TO_QUARANTINE', 'quarantined', false, 0, 0)
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events
    from expected_cases ec
    left join pipeline.event_inbox e
        on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = ec.fault_case_id
       and (
            :'run_label' = ''
            or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r
        on r."InboxEventId" = e."Id"
        or coalesce(r."RawBodyUtf8", '') like '%' || ec.fault_case_id || '%'
        or coalesce(r."MetadataJson", '') like '%' || ec.fault_case_id || '%'
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.expected_outcome,
    ec.allow_projection,
    ec.allowed_max_accepted,
    ec.allowed_max_risk,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    'unexpected_positive_projection_for_negative_case' as issue
from expected_cases ec
join observed o on o.fault_case_id = ec.fault_case_id
where coalesce(o.accepted_readings, 0) > ec.allowed_max_accepted
   or coalesce(o.risk_assessments, 0) > ec.allowed_max_risk
order by ec.fault_case_id;
\o

\o :p3_blocked_file
with expected_cases(fault_case_id, expected_reason_code, blocked_reason) as (
    values
        (
            'P3_QUARANTINE_SENSOR_INACTIVE',
            'sensor_inactive',
            'No inactive sensor exists in current control-plane fixture; mutating nominal sensors would contaminate runtime evidence.'
        ),
        (
            'P3_QUARANTINE_SENSOR_AREA_MISMATCH',
            'sensor_area_mismatch',
            'Only one area exists in current control-plane fixture; creating area mismatch needs a safe fixture, not production-like data mutation.'
        )
)
select
    ec.fault_case_id,
    ec.expected_reason_code,
    'blocked_needs_fixture' as status,
    ec.blocked_reason,
    count(distinct e."Id") as observed_inbox_events,
    count(distinct q."Id") as observed_quarantined_events,
    count(distinct ar."Id") as observed_accepted_readings,
    count(distinct ra."Id") as observed_risk_assessments
from expected_cases ec
left join pipeline.event_inbox e
    on substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') = ec.fault_case_id
   and (
        :'run_label' = ''
        or coalesce(e."CorrelationId", '') like 'cv:' || :'run_label' || ':%'
   )
left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
group by ec.fault_case_id, ec.expected_reason_code, ec.blocked_reason
order by ec.fault_case_id;
\o
