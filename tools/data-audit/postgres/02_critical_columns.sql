\set output_file :out_dir '/06_critical_columns.csv'
\pset format csv
\o :output_file
select
    table_schema,
    table_name,
    ordinal_position,
    column_name,
    data_type,
    is_nullable
from information_schema.columns
where table_schema in ('control', 'pipeline', 'projection')
  and (
    lower(column_name) like '%id%'
    or lower(column_name) like '%event%'
    or lower(column_name) like '%message%'
    or lower(column_name) like '%correlation%'
    or lower(column_name) like '%causation%'
    or lower(column_name) like '%simulation%'
    or lower(column_name) like '%scenario%'
    or lower(column_name) like '%sensor%'
    or lower(column_name) like '%cell%'
    or lower(column_name) like '%area%'
    or lower(column_name) like '%status%'
    or lower(column_name) like '%reason%'
    or lower(column_name) like '%stage%'
    or lower(column_name) like '%risk%'
    or lower(column_name) like '%score%'
    or lower(column_name) like '%level%'
    or lower(column_name) like '%alert%'
    or lower(column_name) like '%quality%'
    or lower(column_name) like '%flag%'
    or lower(column_name) like '%timestamp%'
    or lower(column_name) like '%created%'
    or lower(column_name) like '%processed%'
    or lower(column_name) like '%received%'
    or lower(column_name) like '%occurred%'
  )
order by table_schema, table_name, ordinal_position;
\o


