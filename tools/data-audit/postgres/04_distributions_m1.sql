\set output_file :out_dir '/08_distributions_m1.csv'
\pset format csv
\o :output_file
select 'risk_level' as metric, "RiskLevel"::text as value, count(*) as row_count
from projection.risk_assessment_log
group by "RiskLevel"
union all
select
    'risk_score_bucket',
    case
        when "RiskScore" < 0.2 then '0.0-0.2'
        when "RiskScore" < 0.4 then '0.2-0.4'
        when "RiskScore" < 0.6 then '0.4-0.6'
        when "RiskScore" < 0.8 then '0.6-0.8'
        else '0.8-1.0+'
    end,
    count(*)
from projection.risk_assessment_log
group by 2
union all
select 'simulation_run_id', coalesce("SimulationRunId"::text, '<null>'), count(*)
from projection.risk_assessment_log
group by "SimulationRunId"
union all
select 'sensor_id', "SensorId"::text, count(*)
from projection.risk_assessment_log
group by "SensorId"
union all
select 'area_id', "AreaId"::text, count(*)
from projection.risk_assessment_log
group by "AreaId"
order by metric, row_count desc, value;
\o


