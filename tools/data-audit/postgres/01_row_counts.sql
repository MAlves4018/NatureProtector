\set output_file :out_dir '/05_row_counts.csv'
\pset format csv
\o :output_file
select
    t.table_schema,
    t.table_name,
    (
        xpath(
            '/row/count/text()',
            query_to_xml(
                format('select count(*) as count from %I.%I', t.table_schema, t.table_name),
                false,
                true,
                ''
            )
        )
    )[1]::text::bigint as row_count
from information_schema.tables t
where t.table_schema in ('control', 'pipeline', 'projection')
  and t.table_type = 'BASE TABLE'
order by t.table_schema, t.table_name;
\o


