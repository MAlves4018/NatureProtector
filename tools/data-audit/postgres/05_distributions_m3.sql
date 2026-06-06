\set output_file :out_dir '/09_distributions_m3.csv'
\pset format csv
\o :output_file
select 'event_inbox_status' as metric, "Status"::text as value, count(*) as row_count
from pipeline.event_inbox
group by "Status"
union all
select 'processing_stage', "Stage"::text, count(*)
from pipeline.processing_attempts
group by "Stage"
union all
select 'processing_outcome', "Outcome"::text, count(*)
from pipeline.processing_attempts
group by "Outcome"
union all
select 'processing_error_code', coalesce("ErrorCode", '<null>'), count(*)
from pipeline.processing_attempts
group by "ErrorCode"
union all
select 'rejection_code', "RejectionCode"::text, count(*)
from pipeline.rejected_events
group by "RejectionCode"
union all
select 'quarantine_code', "QuarantineCode"::text, count(*)
from pipeline.quarantined_events
group by "QuarantineCode"
order by metric, row_count desc, value;
\o


