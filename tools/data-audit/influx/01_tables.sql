select table_schema, table_name, table_type
from information_schema.tables
where table_schema = 'iox'
  and table_name in ('accepted_readings', 'risk_assessments', 'area_risk_snapshots')
order by table_name;

