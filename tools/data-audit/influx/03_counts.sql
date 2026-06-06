select 'accepted_readings' as table_name, count(*) as row_count from iox.accepted_readings
union all
select 'risk_assessments', count(*) from iox.risk_assessments
union all
select 'area_risk_snapshots', count(*) from iox.area_risk_snapshots
order by table_name;

