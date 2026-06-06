\set tables_file :out_dir '/01_tables.csv'
\set columns_file :out_dir '/02_columns.csv'
\set constraints_file :out_dir '/03_constraints.csv'
\set indexes_file :out_dir '/04_indexes.csv'
\pset format csv

\o :tables_file
select table_schema, table_name, table_type
from information_schema.tables
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name;
\o

\o :columns_file
select table_schema, table_name, ordinal_position, column_name, data_type, udt_name, character_maximum_length, numeric_precision, numeric_scale, is_nullable, column_default
from information_schema.columns
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name, ordinal_position;
\o

\o :constraints_file
select n.nspname as table_schema, c.relname as table_name, con.conname as constraint_name, con.contype as constraint_type, pg_get_constraintdef(con.oid) as constraint_definition
from pg_constraint con
join pg_class c on c.oid = con.conrelid
join pg_namespace n on n.oid = c.relnamespace
where n.nspname in ('control', 'pipeline', 'projection')
order by n.nspname, c.relname, con.conname;
\o

\o :indexes_file
select schemaname, tablename, indexname, indexdef
from pg_indexes
where schemaname in ('control', 'pipeline', 'projection')
order by schemaname, tablename, indexname;
\o


