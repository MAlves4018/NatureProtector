\set coverage_gap_file :out_dir '/coverage_gap_summary.csv'
\set missing_readings_file :out_dir '/missing_readings_expected_vs_observed.csv'
\set idempotent_duplicate_file :out_dir '/idempotent_duplicate_summary.csv'
\set value_degradation_file :out_dir '/value_degradation_by_profile.csv'
\set value_degradation_extended_file :out_dir '/value_degradation_extended_summary.csv'
\set p2_expected_file :out_dir '/p2_expected_vs_observed.csv'
\set p2_traceability_file :out_dir '/p2_m5_coverage_traceability.csv'
\set blocked_eligibility_file :out_dir '/blocked_eligibility_summary.csv'
\set temporal_quality_file :out_dir '/temporal_quality_summary.csv'
\set p2_extended_expected_file :out_dir '/p2_extended_expected_vs_observed.csv'
\set p2_extended_traceability_file :out_dir '/p2_extended_m5_traceability.csv'
\if :{?run_label}
\else
\set run_label ''
\endif
\pset format csv

\o :coverage_gap_file
with expected_cases(
    fault_case_id,
    fault_layer,
    expected_outcome,
    expected_reason_code,
    expected_events,
    expected_published_events,
    expected_coverage_gap,
    value_profile,
    first_sequence
) as (
    values
        ('P2_MISSING_READINGS', 'coverage_gap', 'coverage_gap', 'missing-readings', 5, 3, 2, 'missing-readings', 1)
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as observed_inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts
    from expected_cases ec
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || ec.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r
        on (
            r."InboxEventId" = e."Id"
            or coalesce(r."RawBodyUtf8", '') like '%cv:' || :'run_label' || ':' || ec.fault_case_id || ':%'
            or coalesce(r."MetadataJson", '') like '%cv:' || :'run_label' || ':' || ec.fault_case_id || ':%'
        )
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.expected_events,
    ec.expected_published_events,
    ec.expected_coverage_gap,
    coalesce(o.observed_inbox_events, 0) as observed_inbox_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    ec.expected_events - coalesce(o.observed_inbox_events, 0) as observed_coverage_gap,
    case
        when ec.expected_events > ec.expected_published_events
         and coalesce(o.observed_inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_published_events
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        else 'unexpected'
    end as status
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id;
\o

\o :missing_readings_file
with expected_case as (
    select
        'P2_MISSING_READINGS'::text as fault_case_id,
        5 as expected_events,
        3 as expected_published_events,
        1 as first_sequence
),
expected_slots as (
    select
        ec.fault_case_id,
        generate_series(1, ec.expected_events) as expected_slot,
        ec.first_sequence + generate_series(1, ec.expected_events) - 1 as expected_sequence
    from expected_case ec
),
observed as (
    select
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:[A-Z0-9_]+:([0-9]{3})')::int as observed_sequence,
        e."Id" as inbox_event_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        ar."Id" as accepted_reading_id,
        ra."Id" as risk_assessment_id
    from pipeline.event_inbox e
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    where e."CorrelationId" like 'cv:%:P2_MISSING_READINGS:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
)
select
    es.fault_case_id,
    es.expected_slot,
    es.expected_sequence,
    o.event_id,
    o.correlation_id,
    case when o.inbox_event_id is null then false else true end as observed_published,
    case when o.accepted_reading_id is null then false else true end as observed_accepted,
    case when o.risk_assessment_id is null then false else true end as observed_risk,
    case
        when es.expected_slot <= (select expected_published_events from expected_case)
         and o.inbox_event_id is not null
        then 'matched_published'
        when es.expected_slot > (select expected_published_events from expected_case)
         and o.inbox_event_id is null
        then 'matched_missing_before_publish'
        else 'unexpected'
    end as status
from expected_slots es
left join observed o on o.observed_sequence = es.expected_sequence
order by es.expected_slot;
\o

\o :idempotent_duplicate_file
with expected_case as (
    select
        'P2_DUPLICATE_PAYLOAD_IDENTICAL'::text as fault_case_id,
        2 as expected_published_events,
        1 as expected_effective_inbox_events
),
observed as (
    select
        count(distinct e."Id") as observed_inbox_events,
        count(distinct e."EventId") as observed_event_ids,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct r."Id") filter (where r."RejectionCode" = 'duplicate_payload_mismatch') as duplicate_payload_mismatch,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events
    from pipeline.event_inbox e
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r
        on r."InboxEventId" = e."Id"
        or coalesce(r."RawBodyUtf8", '') like '%P2_DUPLICATE_PAYLOAD_IDENTICAL%'
        or coalesce(r."MetadataJson", '') like '%P2_DUPLICATE_PAYLOAD_IDENTICAL%'
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    where e."CorrelationId" like 'cv:%:P2_DUPLICATE_PAYLOAD_IDENTICAL:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
)
select
    ec.fault_case_id,
    ec.expected_published_events,
    ec.expected_effective_inbox_events,
    coalesce(o.observed_inbox_events, 0) as observed_inbox_events,
    coalesce(o.observed_event_ids, 0) as observed_event_ids,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.duplicate_payload_mismatch, 0) as duplicate_payload_mismatch,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    case
        when ec.expected_published_events = 2
         and coalesce(o.observed_inbox_events, 0) = 1
         and coalesce(o.observed_event_ids, 0) = 1
         and coalesce(o.accepted_readings, 0) = 1
         and coalesce(o.risk_assessments, 0) = 1
         and coalesce(o.duplicate_payload_mismatch, 0) = 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
        then true
        else false
    end as idempotent_duplicate_detected,
    case
        when ec.expected_published_events = 2
         and coalesce(o.observed_inbox_events, 0) = 1
         and coalesce(o.accepted_readings, 0) = 1
         and coalesce(o.risk_assessments, 0) = 1
         and coalesce(o.duplicate_payload_mismatch, 0) = 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
        then 'matched'
        else 'unexpected'
    end as status,
    'Idempotent duplicate replay is inferred because the second identical envelope is not persisted explicitly.' as limitation
from expected_case ec
cross join observed o;
\o

\o :value_degradation_file
with profile_cases(fault_case_id, value_profile, expected_published_events) as (
    values
        ('P2_VALUE_NOISE', 'noise', 1),
        ('P2_VALUE_BIAS', 'bias', 1),
        ('P2_VALUE_DRIFT', 'drift', 1),
        ('P2_VALUE_OUTLIER', 'outlier-nominal', 1),
        ('P2_VALUE_STUCK', 'stuck-value', 2),
        ('P2_VALUE_CLIPPING', 'clipping-nominal', 1),
        ('P2_VALUE_RANGE', 'range-boundary', 1)
),
observed as (
    select
        pc.fault_case_id,
        pc.value_profile,
        pc.expected_published_events,
        count(distinct e."Id") as inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts,
        count(distinct ar."Value") as distinct_values,
        min(ar."Value") as min_value,
        max(ar."Value") as max_value,
        avg(ar."Value") as avg_value
    from profile_cases pc
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || pc.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    group by pc.fault_case_id, pc.value_profile, pc.expected_published_events
)
select
    fault_case_id,
    value_profile,
    expected_published_events,
    inbox_events,
    accepted_readings,
    risk_assessments,
    rejected_events,
    quarantined_events,
    retry_attempts,
    distinct_values,
    min_value,
    max_value,
    avg_value,
    case
        when inbox_events = expected_published_events
         and accepted_readings = expected_published_events
         and risk_assessments = expected_published_events
         and rejected_events = 0
         and quarantined_events = 0
         and retry_attempts = 0
         and min_value is not null
         and min_value >= -50.0
         and max_value <= 60.0
        then 'matched'
        else 'unexpected'
    end as status
from observed
order by fault_case_id;
\o

\o :value_degradation_extended_file
with extended_cases(fault_case_id, value_profile, expected_published_events, nominal_min, nominal_max) as (
    values
        ('P2_VALUE_OUTLIER', 'outlier-nominal', 1, -50.0::double precision, 60.0::double precision),
        ('P2_VALUE_STUCK', 'stuck-value', 2, -50.0::double precision, 60.0::double precision),
        ('P2_VALUE_CLIPPING', 'clipping-nominal', 1, -50.0::double precision, 60.0::double precision),
        ('P2_VALUE_RANGE', 'range-boundary', 1, -50.0::double precision, 60.0::double precision)
),
observed as (
    select
        ec.fault_case_id,
        ec.value_profile,
        ec.expected_published_events,
        count(distinct e."Id") as inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts,
        count(distinct ar."Value") as distinct_values,
        min(ar."Value") as min_value,
        max(ar."Value") as max_value
    from extended_cases ec
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || ec.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    group by ec.fault_case_id, ec.value_profile, ec.expected_published_events
)
select
    ec.fault_case_id,
    ec.value_profile,
    ec.expected_published_events,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    coalesce(o.distinct_values, 0) as distinct_values,
    o.min_value,
    o.max_value,
    case when o.min_value >= ec.nominal_min and o.max_value <= ec.nominal_max then true else false end as within_candidate_range,
    case
        when ec.fault_case_id = 'P2_VALUE_STUCK'
         and coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_published_events
         and coalesce(o.distinct_values, 0) = 1
         and o.min_value >= ec.nominal_min
         and o.max_value <= ec.nominal_max
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_case_id <> 'P2_VALUE_STUCK'
         and coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_published_events
         and o.min_value >= ec.nominal_min
         and o.max_value <= ec.nominal_max
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when coalesce(o.inbox_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status,
    'P2.4A nominal value degradation only; no runtime flatline classifier is claimed.' as limitation
from extended_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id;
\o

\o :p2_expected_file
with expected_cases(
    fault_case_id,
    fault_layer,
    expected_outcome,
    expected_reason_code,
    expected_events,
    expected_published_events,
    expected_effective_inbox_events,
    expected_coverage_gap,
    value_profile
) as (
    values
        ('P2_MISSING_READINGS', 'coverage_gap', 'coverage_gap', 'missing-readings', 5, 3, 3, 2, 'missing-readings'),
        ('P2_DUPLICATE_PAYLOAD_IDENTICAL', 'idempotency', 'idempotent_duplicate', 'idempotent_duplicate', 2, 2, 1, 0, 'duplicate'),
        ('P2_VALUE_NOISE', 'value_degradation', 'value_degraded', 'noise', 1, 1, 1, 0, 'noise'),
        ('P2_VALUE_BIAS', 'value_degradation', 'value_degraded', 'bias', 1, 1, 1, 0, 'bias'),
        ('P2_VALUE_DRIFT', 'value_degradation', 'value_degraded', 'drift', 1, 1, 1, 0, 'drift'),
        ('P2_VALUE_OUTLIER', 'value_degradation', 'value_degraded', 'outlier-nominal', 1, 1, 1, 0, 'outlier-nominal'),
        ('P2_VALUE_STUCK', 'value_degradation', 'value_degraded', 'stuck-value', 2, 2, 2, 0, 'stuck-value'),
        ('P2_VALUE_CLIPPING', 'value_degradation', 'value_degraded', 'clipping-nominal', 1, 1, 1, 0, 'clipping-nominal'),
        ('P2_VALUE_RANGE', 'value_degradation', 'value_degraded', 'range-boundary', 1, 1, 1, 0, 'range-boundary'),
        ('P2_BLOCKED_RANGE_ELIGIBILITY', 'eligibility', 'blocked_eligibility', 'temperature_out_of_candidate_range', 1, 1, 1, 0, 'blocked-range'),
        ('P2_TEMPORAL_DELAYED', 'temporal_quality', 'temporal_quality', 'delayed-reading', 1, 1, 1, 0, 'temporal-delay')
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as inbox_events,
        count(distinct e."EventId") as event_ids,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct ra."Id") filter (where coalesce(ra."CalculationStatus", '') = 'PartialButUsable') as partial_risk_assessments,
        count(distinct ar."Id") filter (where coalesce(ar."OperationalState", '') = 'Delayed') as delayed_readings,
        min(ar."Value") as min_value,
        max(ar."Value") as max_value,
        count(distinct r."Id") filter (where r."RejectionCode" = 'duplicate_payload_mismatch') as duplicate_payload_mismatch,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts
    from expected_cases ec
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || ec.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r
        on r."InboxEventId" = e."Id"
        or coalesce(r."RawBodyUtf8", '') like '%' || ec.fault_case_id || '%'
        or coalesce(r."MetadataJson", '') like '%' || ec.fault_case_id || '%'
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.fault_layer,
    ec.expected_outcome,
    ec.expected_reason_code,
    ec.value_profile,
    ec.expected_events,
    ec.expected_published_events,
    ec.expected_effective_inbox_events,
    ec.expected_coverage_gap,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.event_ids, 0) as event_ids,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.partial_risk_assessments, 0) as partial_risk_assessments,
    coalesce(o.delayed_readings, 0) as delayed_readings,
    o.min_value,
    o.max_value,
    coalesce(o.duplicate_payload_mismatch, 0) as duplicate_payload_mismatch,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    case
        when ec.fault_case_id = 'P2_MISSING_READINGS'
         and coalesce(o.inbox_events, 0) = ec.expected_effective_inbox_events
         and coalesce(o.accepted_readings, 0) = ec.expected_effective_inbox_events
         and coalesce(o.risk_assessments, 0) = ec.expected_effective_inbox_events
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_case_id = 'P2_DUPLICATE_PAYLOAD_IDENTICAL'
         and coalesce(o.inbox_events, 0) = 1
         and coalesce(o.event_ids, 0) = 1
         and coalesce(o.accepted_readings, 0) = 1
         and coalesce(o.risk_assessments, 0) = 1
         and coalesce(o.duplicate_payload_mismatch, 0) = 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
        then 'matched'
        when ec.fault_layer = 'value_degradation'
         and coalesce(o.inbox_events, 0) = ec.expected_effective_inbox_events
         and coalesce(o.accepted_readings, 0) = ec.expected_effective_inbox_events
         and coalesce(o.risk_assessments, 0) = ec.expected_effective_inbox_events
         and o.min_value >= -50.0
         and o.max_value <= 60.0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_layer = 'eligibility'
         and coalesce(o.inbox_events, 0) = ec.expected_effective_inbox_events
         and coalesce(o.accepted_readings, 0) = ec.expected_effective_inbox_events
         and coalesce(o.risk_assessments, 0) = 0
         and o.max_value > 60.0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_layer = 'temporal_quality'
         and coalesce(o.inbox_events, 0) = ec.expected_effective_inbox_events
         and coalesce(o.accepted_readings, 0) = ec.expected_effective_inbox_events
         and coalesce(o.risk_assessments, 0) = ec.expected_effective_inbox_events
         and coalesce(o.partial_risk_assessments, 0) = ec.expected_effective_inbox_events
         and coalesce(o.delayed_readings, 0) = ec.expected_effective_inbox_events
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when coalesce(o.inbox_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id;
\o

\o :p2_traceability_file
with expected_cases(fault_case_id, expected_outcome, expected_events, first_sequence) as (
    values
        ('P2_MISSING_READINGS', 'coverage_gap', 5, 1),
        ('P2_DUPLICATE_PAYLOAD_IDENTICAL', 'idempotent_duplicate', 2, 4),
        ('P2_VALUE_NOISE', 'value_degraded', 1, 6),
        ('P2_VALUE_BIAS', 'value_degraded', 1, 7),
        ('P2_VALUE_DRIFT', 'value_degraded', 1, 8),
        ('P2_VALUE_OUTLIER', 'value_degraded', 1, 9),
        ('P2_VALUE_STUCK', 'value_degraded', 2, 10),
        ('P2_VALUE_CLIPPING', 'value_degraded', 1, 12),
        ('P2_VALUE_RANGE', 'value_degraded', 1, 13),
        ('P2_BLOCKED_RANGE_ELIGIBILITY', 'blocked_eligibility', 1, 14),
        ('P2_TEMPORAL_DELAYED', 'temporal_quality', 1, 15)
),
expected_slots as (
    select
        ec.fault_case_id,
        ec.expected_outcome,
        slot.slot_index,
        case
            when ec.fault_case_id = 'P2_DUPLICATE_PAYLOAD_IDENTICAL' then ec.first_sequence
            else ec.first_sequence + slot.slot_index - 1
        end as expected_sequence
    from expected_cases ec
    cross join lateral generate_series(1, ec.expected_events) as slot(slot_index)
),
observed as (
    select
        e."Id" as inbox_event_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') as fault_case_id,
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:[A-Z0-9_]+:([0-9]{3})')::int as sequence,
        count(distinct pa."Id") as attempt_count,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts,
        max(pa."Outcome")::text as latest_attempt_outcome,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    where e."CorrelationId" like 'cv:%:P2_%:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    group by e."Id", e."EventId", e."CorrelationId"
)
select
    es.fault_case_id,
    es.expected_outcome,
    es.slot_index,
    es.expected_sequence,
    o.event_id,
    o.correlation_id,
    o.inbox_event_id,
    coalesce(o.attempt_count, 0) as attempt_count,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    o.latest_attempt_outcome,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    case
        when es.fault_case_id = 'P2_MISSING_READINGS' and es.slot_index > 3 and o.inbox_event_id is null
        then 'missing_before_publish'
        when es.fault_case_id = 'P2_DUPLICATE_PAYLOAD_IDENTICAL' and es.slot_index = 2
        then 'idempotent_duplicate_replay_not_persisted'
        when es.fault_case_id = 'P2_BLOCKED_RANGE_ELIGIBILITY'
         and o.inbox_event_id is not null
         and coalesce(o.accepted_readings, 0) > 0
         and coalesce(o.risk_assessments, 0) = 0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
        then 'blocked_without_risk'
        when o.inbox_event_id is not null and coalesce(o.accepted_readings, 0) > 0 and coalesce(o.risk_assessments, 0) > 0
        then 'positive_path'
        when o.inbox_event_id is null
        then 'missing'
        else 'unexpected'
    end as trace_status
from expected_slots es
left join observed o
    on o.fault_case_id = es.fault_case_id
   and o.sequence = es.expected_sequence
order by es.fault_case_id, es.slot_index;
\o

\o :blocked_eligibility_file
with expected_case as (
    select
        'P2_BLOCKED_RANGE_ELIGIBILITY'::text as fault_case_id,
        1 as expected_published_events,
        'temperature_out_of_candidate_range'::text as expected_reason_code
),
observed as (
    select
        count(distinct e."Id") as inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct ra."Id") filter (where ra."RiskScore" = 0) as zero_score_risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts,
        min(ar."Value") as min_value,
        max(ar."Value") as max_value
    from expected_case ec
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || ec.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
)
select
    ec.fault_case_id,
    ec.expected_published_events,
    ec.expected_reason_code,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.zero_score_risk_assessments, 0) as zero_score_risk_assessments,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    o.min_value,
    o.max_value,
    case when o.max_value > 60.0 then ec.expected_reason_code else null end as inferred_reason_code,
    case
        when coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = 0
         and coalesce(o.zero_score_risk_assessments, 0) = 0
         and o.max_value > 60.0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when coalesce(o.inbox_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status,
    'Eligibility reason is inferred from the accepted reading value and candidate V1 range rule; it is not persisted in projection tables.' as limitation
from expected_case ec
cross join observed o;
\o

\o :temporal_quality_file
with expected_cases(fault_case_id, expected_outcome, expected_published_events, status_policy) as (
    values
        ('P2_TEMPORAL_DELAYED', 'temporal_quality', 1, 'partial_but_usable'),
        ('P2_TEMPORAL_OUT_OF_ORDER', 'blocked_ambiguous_temporal_semantics', 0, 'blocked_not_implemented')
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct ra."Id") filter (where coalesce(ra."CalculationStatus", '') = 'PartialButUsable') as partial_risk_assessments,
        count(distinct ar."Id") filter (where coalesce(ar."OperationalState", '') = 'Delayed') as delayed_readings,
        max(extract(epoch from (ar."IngestTime" - ar."EventTime"))) as max_lag_seconds,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts
    from expected_cases ec
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || ec.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.expected_outcome,
    ec.expected_published_events,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.partial_risk_assessments, 0) as partial_risk_assessments,
    coalesce(o.delayed_readings, 0) as delayed_readings,
    o.max_lag_seconds,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    case
        when ec.fault_case_id = 'P2_TEMPORAL_DELAYED'
         and coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_published_events
         and coalesce(o.partial_risk_assessments, 0) = ec.expected_published_events
         and coalesce(o.delayed_readings, 0) = ec.expected_published_events
         and coalesce(o.max_lag_seconds, 0) >= 120
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_case_id = 'P2_TEMPORAL_OUT_OF_ORDER'
         and coalesce(o.inbox_events, 0) = 0
        then 'blocked_ambiguous_temporal_semantics'
        when coalesce(o.inbox_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status,
    case
        when ec.fault_case_id = 'P2_TEMPORAL_OUT_OF_ORDER'
        then 'Runtime does not pass latestObservedEventTime/window state into ReadingTemporalClassifier; out-of-order is not implemented.'
        else 'Delayed uses SensorOperationalState.Delayed and explicit IngestTime/EventTime lag.'
    end as limitation
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id;
\o

\o :p2_extended_expected_file
with expected_cases(fault_case_id, fault_layer, expected_outcome, expected_published_events, expected_risk_assessments) as (
    values
        ('P2_VALUE_OUTLIER', 'value_degradation', 'value_degraded', 1, 1),
        ('P2_VALUE_STUCK', 'value_degradation', 'value_degraded', 2, 2),
        ('P2_VALUE_CLIPPING', 'value_degradation', 'value_degraded', 1, 1),
        ('P2_VALUE_RANGE', 'value_degradation', 'value_degraded', 1, 1),
        ('P2_BLOCKED_RANGE_ELIGIBILITY', 'eligibility', 'blocked_eligibility', 1, 0),
        ('P2_TEMPORAL_DELAYED', 'temporal_quality', 'temporal_quality', 1, 1),
        ('P2_TEMPORAL_OUT_OF_ORDER', 'temporal_quality', 'blocked_ambiguous_temporal_semantics', 0, 0)
),
observed as (
    select
        ec.fault_case_id,
        count(distinct e."Id") as inbox_events,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct ra."Id") filter (where coalesce(ra."CalculationStatus", '') = 'PartialButUsable') as partial_risk_assessments,
        count(distinct ar."Id") filter (where coalesce(ar."OperationalState", '') = 'Delayed') as delayed_readings,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts,
        min(ar."Value") as min_value,
        max(ar."Value") as max_value
    from expected_cases ec
    left join pipeline.event_inbox e
        on e."CorrelationId" like 'cv:%:' || ec.fault_case_id || ':%'
       and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
       )
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    group by ec.fault_case_id
)
select
    ec.fault_case_id,
    ec.fault_layer,
    ec.expected_outcome,
    ec.expected_published_events,
    ec.expected_risk_assessments,
    coalesce(o.inbox_events, 0) as inbox_events,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.partial_risk_assessments, 0) as partial_risk_assessments,
    coalesce(o.delayed_readings, 0) as delayed_readings,
    o.min_value,
    o.max_value,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    case
        when ec.fault_layer = 'value_degradation'
         and coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_risk_assessments
         and o.min_value >= -50.0
         and o.max_value <= 60.0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_layer = 'eligibility'
         and coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_risk_assessments
         and o.max_value > 60.0
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_case_id = 'P2_TEMPORAL_DELAYED'
         and coalesce(o.inbox_events, 0) = ec.expected_published_events
         and coalesce(o.accepted_readings, 0) = ec.expected_published_events
         and coalesce(o.risk_assessments, 0) = ec.expected_risk_assessments
         and coalesce(o.partial_risk_assessments, 0) = ec.expected_risk_assessments
         and coalesce(o.delayed_readings, 0) = ec.expected_published_events
         and coalesce(o.rejected_events, 0) = 0
         and coalesce(o.quarantined_events, 0) = 0
         and coalesce(o.retry_attempts, 0) = 0
        then 'matched'
        when ec.fault_case_id = 'P2_TEMPORAL_OUT_OF_ORDER'
         and coalesce(o.inbox_events, 0) = 0
        then 'blocked_ambiguous_temporal_semantics'
        when coalesce(o.inbox_events, 0) = 0
        then 'missing'
        else 'unexpected'
    end as status
from expected_cases ec
left join observed o on o.fault_case_id = ec.fault_case_id
order by ec.fault_case_id;
\o

\o :p2_extended_traceability_file
with expected_cases(fault_case_id, expected_outcome, expected_events, first_sequence) as (
    values
        ('P2_VALUE_OUTLIER', 'value_degraded', 1, 9),
        ('P2_VALUE_STUCK', 'value_degraded', 2, 10),
        ('P2_VALUE_CLIPPING', 'value_degraded', 1, 12),
        ('P2_VALUE_RANGE', 'value_degraded', 1, 13),
        ('P2_BLOCKED_RANGE_ELIGIBILITY', 'blocked_eligibility', 1, 14),
        ('P2_TEMPORAL_DELAYED', 'temporal_quality', 1, 15),
        ('P2_TEMPORAL_OUT_OF_ORDER', 'blocked_ambiguous_temporal_semantics', 0, 16)
),
expected_slots as (
    select
        ec.fault_case_id,
        ec.expected_outcome,
        slot.slot_index,
        ec.first_sequence + slot.slot_index - 1 as expected_sequence
    from expected_cases ec
    cross join lateral generate_series(1, greatest(ec.expected_events, 1)) as slot(slot_index)
),
observed as (
    select
        e."Id" as inbox_event_id,
        e."EventId" as event_id,
        e."CorrelationId" as correlation_id,
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:([A-Z0-9_]+):[0-9]{3}') as fault_case_id,
        substring(coalesce(e."CorrelationId", '') from 'cv:[^:]+:[A-Z0-9_]+:([0-9]{3})')::int as sequence,
        count(distinct pa."Id") as attempt_count,
        count(distinct pa."Id") filter (where pa."Outcome"::text = '3') as retry_attempts,
        max(pa."Outcome")::text as latest_attempt_outcome,
        count(distinct ar."Id") as accepted_readings,
        count(distinct ra."Id") as risk_assessments,
        count(distinct ra."Id") filter (where coalesce(ra."CalculationStatus", '') = 'PartialButUsable') as partial_risk_assessments,
        count(distinct r."Id") as rejected_events,
        count(distinct q."Id") as quarantined_events
    from pipeline.event_inbox e
    left join pipeline.processing_attempts pa on pa."InboxEventId" = e."Id"
    left join projection.accepted_reading_log ar on ar."EventId" = e."EventId"
    left join projection.risk_assessment_log ra on ra."SourceEventId" = e."EventId"
    left join pipeline.rejected_events r on r."InboxEventId" = e."Id"
    left join pipeline.quarantined_events q on q."InboxEventId" = e."Id"
    where e."CorrelationId" like 'cv:%:P2_%:%'
      and (
            :'run_label' = ''
            or e."CorrelationId" like 'cv:' || :'run_label' || ':%'
      )
    group by e."Id", e."EventId", e."CorrelationId"
)
select
    es.fault_case_id,
    es.expected_outcome,
    es.slot_index,
    es.expected_sequence,
    o.event_id,
    o.correlation_id,
    o.inbox_event_id,
    coalesce(o.attempt_count, 0) as attempt_count,
    coalesce(o.retry_attempts, 0) as retry_attempts,
    o.latest_attempt_outcome,
    coalesce(o.accepted_readings, 0) as accepted_readings,
    coalesce(o.risk_assessments, 0) as risk_assessments,
    coalesce(o.partial_risk_assessments, 0) as partial_risk_assessments,
    coalesce(o.rejected_events, 0) as rejected_events,
    coalesce(o.quarantined_events, 0) as quarantined_events,
    case
        when es.fault_case_id = 'P2_TEMPORAL_OUT_OF_ORDER' and o.inbox_event_id is null
        then 'blocked_ambiguous_temporal_semantics'
        when es.fault_case_id = 'P2_BLOCKED_RANGE_ELIGIBILITY'
         and o.inbox_event_id is not null
         and coalesce(o.accepted_readings, 0) > 0
         and coalesce(o.risk_assessments, 0) = 0
        then 'blocked_without_risk'
        when es.fault_case_id = 'P2_TEMPORAL_DELAYED'
         and o.inbox_event_id is not null
         and coalesce(o.accepted_readings, 0) > 0
         and coalesce(o.risk_assessments, 0) > 0
         and coalesce(o.partial_risk_assessments, 0) > 0
        then 'partial_but_usable_positive_path'
        when o.inbox_event_id is not null and coalesce(o.accepted_readings, 0) > 0 and coalesce(o.risk_assessments, 0) > 0
        then 'positive_path'
        when o.inbox_event_id is null
        then 'missing'
        else 'unexpected'
    end as trace_status
from expected_slots es
left join observed o
    on o.fault_case_id = es.fault_case_id
   and o.sequence = es.expected_sequence
order by es.fault_case_id, es.slot_index;
\o
