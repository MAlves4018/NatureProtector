select 'accepted_readings' as table_name, min(time) as min_time, max(time) as max_time, count(*) as row_count from iox.accepted_readings
union all
select 'risk_assessments', min(time), max(time), count(*) from iox.risk_assessments
union all
select 'area_risk_snapshots', min(time), max(time), count(*) from iox.area_risk_snapshots
order by table_name;

