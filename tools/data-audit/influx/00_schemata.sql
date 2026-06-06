select catalog_name, schema_name
from information_schema.schemata
where schema_name in ('iox', 'system')
order by schema_name;

