select 'risk_level' as metric, risk_level as value, count(*) as row_count
from iox.risk_assessments
group by risk_level
union all
select 'sensor_id', sensor_id, count(*)
from iox.risk_assessments
group by sensor_id
union all
select 'area_id', area_id, count(*)
from iox.risk_assessments
group by area_id
union all
select 'aggregate_risk_level', aggregate_risk_level, count(*)
from iox.area_risk_snapshots
group by aggregate_risk_level
union all
select 'accepted_metric_type', metric_type, count(*)
from iox.accepted_readings
group by metric_type
order by metric, row_count desc, value;

