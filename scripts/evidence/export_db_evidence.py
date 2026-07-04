#!/usr/bin/env python3
"""
NatureProtector DB Evidence Exporter
PostgreSQL / psycopg v3

Gera um relatório Markdown com:
- contexto da base de dados
- schemas
- tabelas
- colunas
- constraints
- foreign keys
- índices
- sequences
- views
- funções
- triggers
- permissões
- tamanhos
- contagens
- amostras runtime de control/pipeline/projection

Uso típico:
  python scripts/evidence/export_db_evidence.py

Configuração via variáveis de ambiente:
  PGHOST
  PGPORT
  PGDATABASE
  PGUSER
  PGPASSWORD

Ou via DSN:
  python scripts/evidence/export_db_evidence.py --dsn "postgresql://user:pass@localhost:5432/natureprotector"
"""

from __future__ import annotations

import argparse
import getpass
import os
import sys
from datetime import datetime
from decimal import Decimal
from pathlib import Path
from typing import Any

import psycopg
from psycopg import sql


TARGET_SCHEMAS = ("control", "pipeline", "projection")


EXPECTED_TABLES = [
    ("control", "area_contexts"),
    ("control", "areas"),
    ("control", "configuration_versions"),
    ("control", "dataset_artifacts"),
    ("control", "grid_cells"),
    ("control", "rule_set_versions"),
    ("control", "scenario_dataset_bindings"),
    ("control", "scenario_definitions"),
    ("control", "sensor_networks"),
    ("control", "sensor_nodes"),
    ("control", "sensor_profiles"),
    ("control", "simulation_runs"),
    ("pipeline", "event_inbox"),
    ("pipeline", "processing_attempts"),
    ("pipeline", "rejected_events"),
    ("pipeline", "quarantined_events"),
    ("projection", "accepted_reading_log"),
    ("projection", "risk_assessment_log"),
    ("projection", "area_risk_snapshot_log"),
    ("projection", "cell_operational_state"),
    ("projection", "area_operational_state"),
    ("projection", "alert_state"),
]


STATIC_QUERIES: list[tuple[str, str]] = [
    (
        "00_database_context",
        """
        select
          current_database() as database_name,
          current_user as current_user,
          session_user as session_user,
          current_schema() as current_schema,
          inet_server_addr() as server_address,
          inet_server_port() as server_port,
          version() as postgres_version;
        """,
    ),
    (
        "00_database_size",
        """
        select
          current_database() as database_name,
          pg_size_pretty(pg_database_size(current_database())) as database_size,
          pg_database_size(current_database()) as database_size_bytes;
        """,
    ),
    (
        "00_database_settings_subset",
        """
        select
          name,
          setting,
          unit,
          short_desc
        from pg_settings
        where name in (
          'server_version',
          'max_connections',
          'shared_buffers',
          'work_mem',
          'maintenance_work_mem',
          'timezone',
          'DateStyle'
        )
        order by name;
        """,
    ),
    (
        "01_schemas",
        """
        select
          n.nspname as schema_name,
          pg_get_userbyid(n.nspowner) as owner,
          obj_description(n.oid, 'pg_namespace') as comment
        from pg_namespace n
        where n.nspname not like 'pg_%'
          and n.nspname <> 'information_schema'
        order by n.nspname;
        """,
    ),
    (
        "01_target_schema_presence",
        """
        select
          expected.schema_name,
          case when n.nspname is null then false else true end as exists
        from (
          values
            ('control'),
            ('pipeline'),
            ('projection')
        ) as expected(schema_name)
        left join pg_namespace n
          on n.nspname = expected.schema_name
        order by expected.schema_name;
        """,
    ),
    (
        "02_objects_by_schema",
        """
        select
          n.nspname as schema_name,
          c.relname as object_name,
          case c.relkind
            when 'r' then 'table'
            when 'p' then 'partitioned_table'
            when 'v' then 'view'
            when 'm' then 'materialized_view'
            when 'S' then 'sequence'
            when 'f' then 'foreign_table'
            else c.relkind::text
          end as object_type,
          pg_get_userbyid(c.relowner) as owner,
          c.relpersistence as persistence,
          obj_description(c.oid, 'pg_class') as comment
        from pg_class c
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p', 'v', 'm', 'S', 'f')
        order by n.nspname, object_type, c.relname;
        """,
    ),
    (
        "03_tables",
        """
        select
          t.table_schema,
          t.table_name,
          t.table_type
        from information_schema.tables t
        where t.table_schema in ('control', 'pipeline', 'projection')
        order by t.table_schema, t.table_name;
        """,
    ),
    (
        "03_tables_with_size_estimates",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          c.reltuples::bigint as estimated_rows,
          pg_size_pretty(pg_relation_size(c.oid)) as table_size,
          pg_size_pretty(pg_indexes_size(c.oid)) as indexes_size,
          pg_size_pretty(pg_total_relation_size(c.oid)) as total_size,
          pg_relation_size(c.oid) as table_size_bytes,
          pg_indexes_size(c.oid) as indexes_size_bytes,
          pg_total_relation_size(c.oid) as total_size_bytes
        from pg_class c
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p')
        order by pg_total_relation_size(c.oid) desc, n.nspname, c.relname;
        """,
    ),
    (
        "04_columns_information_schema",
        """
        select
          table_schema,
          table_name,
          ordinal_position,
          column_name,
          data_type,
          udt_name,
          is_nullable,
          column_default,
          character_maximum_length,
          numeric_precision,
          numeric_scale,
          datetime_precision
        from information_schema.columns
        where table_schema in ('control', 'pipeline', 'projection')
        order by table_schema, table_name, ordinal_position;
        """,
    ),
    (
        "05_columns_pg_catalog",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          a.attnum as ordinal_position,
          a.attname as column_name,
          format_type(a.atttypid, a.atttypmod) as formatted_data_type,
          t.typname as internal_type_name,
          case when a.attnotnull then 'NO' else 'YES' end as is_nullable,
          pg_get_expr(ad.adbin, ad.adrelid) as default_expression,
          case a.attidentity
            when '' then null
            when 'a' then 'generated_always_identity'
            when 'd' then 'generated_by_default_identity'
            else a.attidentity::text
          end as identity_type,
          case a.attgenerated
            when '' then null
            when 's' then 'stored_generated_column'
            else a.attgenerated::text
          end as generated_column_type,
          col_description(c.oid, a.attnum) as column_comment
        from pg_attribute a
        join pg_class c
          on c.oid = a.attrelid
        join pg_namespace n
          on n.oid = c.relnamespace
        join pg_type t
          on t.oid = a.atttypid
        left join pg_attrdef ad
          on ad.adrelid = a.attrelid
         and ad.adnum = a.attnum
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p', 'v', 'm')
          and a.attnum > 0
          and not a.attisdropped
        order by n.nspname, c.relname, a.attnum;
        """,
    ),
    (
        "06_columns_requiring_quotes",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          a.attname as column_name,
          lower(a.attname) as unquoted_name_that_postgres_would_search
        from pg_attribute a
        join pg_class c
          on c.oid = a.attrelid
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p', 'v', 'm')
          and a.attnum > 0
          and not a.attisdropped
          and a.attname <> lower(a.attname)
        order by n.nspname, c.relname, a.attnum;
        """,
    ),
    (
        "07_primary_keys",
        """
        select
          tc.table_schema,
          tc.table_name,
          tc.constraint_name,
          string_agg(kcu.column_name, ', ' order by kcu.ordinal_position) as pk_columns
        from information_schema.table_constraints tc
        join information_schema.key_column_usage kcu
          on kcu.constraint_schema = tc.constraint_schema
         and kcu.constraint_name = tc.constraint_name
         and kcu.table_schema = tc.table_schema
         and kcu.table_name = tc.table_name
        where tc.table_schema in ('control', 'pipeline', 'projection')
          and tc.constraint_type = 'PRIMARY KEY'
        group by
          tc.table_schema,
          tc.table_name,
          tc.constraint_name
        order by tc.table_schema, tc.table_name;
        """,
    ),
    (
        "08_unique_constraints",
        """
        select
          tc.table_schema,
          tc.table_name,
          tc.constraint_name,
          string_agg(kcu.column_name, ', ' order by kcu.ordinal_position) as unique_columns
        from information_schema.table_constraints tc
        join information_schema.key_column_usage kcu
          on kcu.constraint_schema = tc.constraint_schema
         and kcu.constraint_name = tc.constraint_name
         and kcu.table_schema = tc.table_schema
         and kcu.table_name = tc.table_name
        where tc.table_schema in ('control', 'pipeline', 'projection')
          and tc.constraint_type = 'UNIQUE'
        group by
          tc.table_schema,
          tc.table_name,
          tc.constraint_name
        order by tc.table_schema, tc.table_name, tc.constraint_name;
        """,
    ),
    (
        "09_foreign_keys",
        """
        select
          src_ns.nspname as source_schema,
          src.relname as source_table,
          con.conname as constraint_name,
          string_agg(src_att.attname, ', ' order by cols.ord) as source_columns,
          ref_ns.nspname as referenced_schema,
          ref.relname as referenced_table,
          string_agg(ref_att.attname, ', ' order by cols.ord) as referenced_columns,
          case con.confupdtype
            when 'a' then 'no_action'
            when 'r' then 'restrict'
            when 'c' then 'cascade'
            when 'n' then 'set_null'
            when 'd' then 'set_default'
            else con.confupdtype::text
          end as on_update,
          case con.confdeltype
            when 'a' then 'no_action'
            when 'r' then 'restrict'
            when 'c' then 'cascade'
            when 'n' then 'set_null'
            when 'd' then 'set_default'
            else con.confdeltype::text
          end as on_delete,
          pg_get_constraintdef(con.oid) as constraint_definition
        from pg_constraint con
        join pg_class src
          on src.oid = con.conrelid
        join pg_namespace src_ns
          on src_ns.oid = src.relnamespace
        join pg_class ref
          on ref.oid = con.confrelid
        join pg_namespace ref_ns
          on ref_ns.oid = ref.relnamespace
        join unnest(con.conkey, con.confkey) with ordinality as cols(src_attnum, ref_attnum, ord)
          on true
        join pg_attribute src_att
          on src_att.attrelid = src.oid
         and src_att.attnum = cols.src_attnum
        join pg_attribute ref_att
          on ref_att.attrelid = ref.oid
         and ref_att.attnum = cols.ref_attnum
        where con.contype = 'f'
          and src_ns.nspname in ('control', 'pipeline', 'projection')
        group by
          src_ns.nspname,
          src.relname,
          con.conname,
          ref_ns.nspname,
          ref.relname,
          con.confupdtype,
          con.confdeltype,
          con.oid
        order by source_schema, source_table, constraint_name;
        """,
    ),
    (
        "10_check_constraints",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          con.conname as constraint_name,
          pg_get_constraintdef(con.oid) as constraint_definition
        from pg_constraint con
        join pg_class c
          on c.oid = con.conrelid
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and con.contype = 'c'
        order by n.nspname, c.relname, con.conname;
        """,
    ),
    (
        "11_all_constraints",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          con.conname as constraint_name,
          case con.contype
            when 'p' then 'primary_key'
            when 'f' then 'foreign_key'
            when 'u' then 'unique'
            when 'c' then 'check'
            when 'x' then 'exclusion'
            else con.contype::text
          end as constraint_type,
          pg_get_constraintdef(con.oid) as constraint_definition
        from pg_constraint con
        join pg_class c
          on c.oid = con.conrelid
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
        order by n.nspname, c.relname, constraint_type, con.conname;
        """,
    ),
    (
        "12_indexes",
        """
        select
          tn.nspname as table_schema,
          t.relname as table_name,
          ix.relname as index_name,
          i.indisprimary as is_primary,
          i.indisunique as is_unique,
          i.indisvalid as is_valid,
          i.indisready as is_ready,
          pg_size_pretty(pg_relation_size(ix.oid)) as index_size,
          pg_get_indexdef(ix.oid) as index_definition
        from pg_index i
        join pg_class t
          on t.oid = i.indrelid
        join pg_namespace tn
          on tn.oid = t.relnamespace
        join pg_class ix
          on ix.oid = i.indexrelid
        where tn.nspname in ('control', 'pipeline', 'projection')
        order by tn.nspname, t.relname, ix.relname;
        """,
    ),
    (
        "13_invalid_or_not_ready_indexes",
        """
        select
          tn.nspname as table_schema,
          t.relname as table_name,
          ix.relname as index_name,
          i.indisvalid as is_valid,
          i.indisready as is_ready,
          pg_get_indexdef(ix.oid) as index_definition
        from pg_index i
        join pg_class t
          on t.oid = i.indrelid
        join pg_namespace tn
          on tn.oid = t.relnamespace
        join pg_class ix
          on ix.oid = i.indexrelid
        where tn.nspname in ('control', 'pipeline', 'projection')
          and (not i.indisvalid or not i.indisready)
        order by tn.nspname, t.relname, ix.relname;
        """,
    ),
    (
        "14_sequences",
        """
        select
          sequence_schema,
          sequence_name,
          data_type,
          start_value,
          minimum_value,
          maximum_value,
          increment,
          cycle_option
        from information_schema.sequences
        where sequence_schema in ('control', 'pipeline', 'projection')
        order by sequence_schema, sequence_name;
        """,
    ),
    (
        "15_views",
        """
        select
          table_schema,
          table_name,
          view_definition
        from information_schema.views
        where table_schema in ('control', 'pipeline', 'projection')
        order by table_schema, table_name;
        """,
    ),
    (
        "15_materialized_views",
        """
        select
          schemaname,
          matviewname,
          matviewowner,
          ispopulated,
          definition
        from pg_matviews
        where schemaname in ('control', 'pipeline', 'projection')
        order by schemaname, matviewname;
        """,
    ),
    (
        "16_user_defined_types",
        """
        select
          n.nspname as type_schema,
          t.typname as type_name,
          case t.typtype
            when 'e' then 'enum'
            when 'c' then 'composite'
            when 'd' then 'domain'
            when 'r' then 'range'
            else t.typtype::text
          end as type_kind,
          string_agg(e.enumlabel, ', ' order by e.enumsortorder) as enum_values
        from pg_type t
        join pg_namespace n
          on n.oid = t.typnamespace
        left join pg_enum e
          on e.enumtypid = t.oid
        where n.nspname in ('control', 'pipeline', 'projection', 'public')
          and t.typtype in ('e', 'c', 'd', 'r')
        group by n.nspname, t.typname, t.typtype
        order by n.nspname, t.typname;
        """,
    ),
    (
        "17_functions_and_procedures",
        """
        select
          n.nspname as routine_schema,
          p.proname as routine_name,
          case p.prokind
            when 'f' then 'function'
            when 'p' then 'procedure'
            when 'a' then 'aggregate'
            when 'w' then 'window'
            else p.prokind::text
          end as routine_type,
          pg_get_function_arguments(p.oid) as arguments,
          pg_get_function_result(p.oid) as result_type,
          l.lanname as language,
          p.provolatile as volatility,
          p.prosecdef as security_definer
        from pg_proc p
        join pg_namespace n
          on n.oid = p.pronamespace
        join pg_language l
          on l.oid = p.prolang
        where n.nspname in ('control', 'pipeline', 'projection', 'public')
        order by n.nspname, p.proname;
        """,
    ),
    (
        "18_triggers",
        """
        select
          event_object_schema as table_schema,
          event_object_table as table_name,
          trigger_name,
          action_timing,
          event_manipulation,
          action_statement
        from information_schema.triggers
        where event_object_schema in ('control', 'pipeline', 'projection')
        order by event_object_schema, event_object_table, trigger_name;
        """,
    ),
    (
        "19_row_level_security_policies",
        """
        select
          schemaname,
          tablename,
          policyname,
          permissive,
          roles,
          cmd,
          qual,
          with_check
        from pg_policies
        where schemaname in ('control', 'pipeline', 'projection')
        order by schemaname, tablename, policyname;
        """,
    ),
    (
        "20_table_privileges",
        """
        select
          table_schema,
          table_name,
          grantee,
          privilege_type,
          is_grantable
        from information_schema.table_privileges
        where table_schema in ('control', 'pipeline', 'projection')
        order by table_schema, table_name, grantee, privilege_type;
        """,
    ),
    (
        "21_table_comments",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          obj_description(c.oid, 'pg_class') as table_comment
        from pg_class c
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p', 'v', 'm')
        order by n.nspname, c.relname;
        """,
    ),
    (
        "21_column_comments",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          a.attname as column_name,
          col_description(c.oid, a.attnum) as column_comment
        from pg_attribute a
        join pg_class c
          on c.oid = a.attrelid
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p', 'v', 'm')
          and a.attnum > 0
          and not a.attisdropped
        order by n.nspname, c.relname, a.attnum;
        """,
    ),
    (
        "22_pg_stats_columns",
        """
        select
          schemaname,
          tablename,
          attname as column_name,
          inherited,
          null_frac,
          avg_width,
          n_distinct,
          most_common_vals,
          most_common_freqs,
          histogram_bounds
        from pg_stats
        where schemaname in ('control', 'pipeline', 'projection')
        order by schemaname, tablename, attname;
        """,
    ),
    (
        "23_estimated_row_counts",
        """
        select
          n.nspname as table_schema,
          c.relname as table_name,
          c.reltuples::bigint as estimated_rows,
          c.relpages as estimated_pages
        from pg_class c
        join pg_namespace n
          on n.oid = c.relnamespace
        where n.nspname in ('control', 'pipeline', 'projection')
          and c.relkind in ('r', 'p')
        order by n.nspname, c.relname;
        """,
    ),
]


RUNTIME_QUERIES: list[tuple[str, str]] = [
    (
        "31_control_counts",
        """
        select
          (select count(*) from control.configuration_versions) as configuration_versions,
          (select count(*) from control.areas) as areas,
          (select count(*) from control.area_contexts) as area_contexts,
          (select count(*) from control.grid_cells) as grid_cells,
          (select count(*) from control.sensor_networks) as sensor_networks,
          (select count(*) from control.sensor_nodes) as sensor_nodes,
          (select count(*) from control.sensor_profiles) as sensor_profiles,
          (select count(*) from control.scenario_definitions) as scenario_definitions,
          (select count(*) from control.scenario_dataset_bindings) as scenario_dataset_bindings,
          (select count(*) from control.dataset_artifacts) as dataset_artifacts,
          (select count(*) from control.rule_set_versions) as rule_set_versions,
          (select count(*) from control.simulation_runs) as simulation_runs;
        """,
    ),
    (
        "31_control_configuration_versions",
        """
        select *
        from control.configuration_versions
        order by "CreatedAt" desc;
        """,
    ),
    (
        "31_control_areas",
        """
        select *
        from control.areas
        order by "Code";
        """,
    ),
    (
        "31_control_sensor_nodes_summary",
        """
        select
          "IsActive",
          "Type",
          count(*) as count
        from control.sensor_nodes
        group by "IsActive", "Type"
        order by "IsActive" desc, "Type";
        """,
    ),
    (
        "31_control_sensor_nodes_sample",
        """
        select *
        from control.sensor_nodes
        order by "IsActive" desc, "Name"
        limit 50;
        """,
    ),
    (
        "31_control_simulation_runs_latest",
        """
        select *
        from control.simulation_runs
        order by "CreatedAt" desc
        limit 25;
        """,
    ),
    (
        "32_pipeline_counts",
        """
        select
          (select count(*) from pipeline.event_inbox) as inbox_total,
          (select count(*) from pipeline.processing_attempts) as attempts_total,
          (select count(*) from pipeline.rejected_events) as rejected_total,
          (select count(*) from pipeline.quarantined_events) as quarantined_total;
        """,
    ),
    (
        "32_pipeline_event_inbox_status",
        """
        select
          "Status",
          count(*) as count
        from pipeline.event_inbox
        group by "Status"
        order by "Status";
        """,
    ),
    (
        "32_pipeline_event_inbox_time_range",
        """
        select
          min("ReceivedAt") as first_received_at,
          max("ReceivedAt") as last_received_at,
          min("EventTime") as first_event_time,
          max("EventTime") as last_event_time,
          min("LastAttemptAt") as first_attempt_at,
          max("LastAttemptAt") as last_attempt_at,
          min("LastProcessedAt") as first_processed_at,
          max("LastProcessedAt") as last_processed_at
        from pipeline.event_inbox;
        """,
    ),
    (
        "32_pipeline_event_inbox_latest",
        """
        select *
        from pipeline.event_inbox
        order by "ReceivedAt" desc
        limit 25;
        """,
    ),
    (
        "32_pipeline_event_types",
        """
        select
          "EventType",
          "Producer",
          count(*) as count,
          min("ReceivedAt") as first_received_at,
          max("ReceivedAt") as last_received_at
        from pipeline.event_inbox
        group by "EventType", "Producer"
        order by count desc, "EventType", "Producer";
        """,
    ),
    (
        "32_pipeline_last_errors",
        """
        select
          "LastErrorCode",
          "LastErrorMessage",
          count(*) as count,
          min("ReceivedAt") as first_received_at,
          max("ReceivedAt") as last_received_at,
          min("LastAttemptAt") as first_attempt_at,
          max("LastAttemptAt") as last_attempt_at
        from pipeline.event_inbox
        where "LastErrorMessage" is not null
          and "LastErrorMessage" <> ''
        group by "LastErrorCode", "LastErrorMessage"
        order by count desc, "LastErrorCode";
        """,
    ),
    (
        "32_processing_attempts_by_stage_outcome",
        """
        select
          "Stage",
          "Outcome",
          count(*) as count,
          min("StartedAt") as first_started_at,
          max("StartedAt") as last_started_at,
          min("FinishedAt") as first_finished_at,
          max("FinishedAt") as last_finished_at
        from pipeline.processing_attempts
        group by "Stage", "Outcome"
        order by "Stage", "Outcome";
        """,
    ),
    (
        "32_processing_attempt_errors",
        """
        select
          "Stage",
          "Outcome",
          "ErrorCode",
          "ErrorMessage",
          count(*) as count,
          min("StartedAt") as first_started_at,
          max("StartedAt") as last_started_at
        from pipeline.processing_attempts
        where "ErrorMessage" is not null
          and "ErrorMessage" <> ''
        group by "Stage", "Outcome", "ErrorCode", "ErrorMessage"
        order by count desc;
        """,
    ),
    (
        "32_processing_attempts_latest",
        """
        select *
        from pipeline.processing_attempts
        order by "StartedAt" desc
        limit 50;
        """,
    ),
    (
        "32_rejected_events_summary",
        """
        select
          "ErrorCode",
          "ErrorMessage",
          count(*) as count,
          min("CreatedAt") as first_created_at,
          max("CreatedAt") as last_created_at
        from pipeline.rejected_events
        group by "ErrorCode", "ErrorMessage"
        order by count desc;
        """,
    ),
    (
        "32_rejected_events_latest",
        """
        select *
        from pipeline.rejected_events
        order by "CreatedAt" desc
        limit 25;
        """,
    ),
    (
        "32_quarantined_events_summary",
        """
        select
          "ErrorCode",
          "ErrorMessage",
          count(*) as count,
          min("CreatedAt") as first_created_at,
          max("CreatedAt") as last_created_at
        from pipeline.quarantined_events
        group by "ErrorCode", "ErrorMessage"
        order by count desc;
        """,
    ),
    (
        "32_quarantined_events_latest",
        """
        select *
        from pipeline.quarantined_events
        order by "CreatedAt" desc
        limit 25;
        """,
    ),
    (
        "33_projection_counts",
        """
        select
          (select count(*) from projection.accepted_reading_log) as accepted_readings,
          (select count(*) from projection.risk_assessment_log) as risk_assessments,
          (select count(*) from projection.area_risk_snapshot_log) as area_risk_snapshots,
          (select count(*) from projection.cell_operational_state) as cell_operational_states,
          (select count(*) from projection.area_operational_state) as area_operational_states,
          (select count(*) from projection.alert_state) as alert_states;
        """,
    ),
    (
        "33_projection_time_ranges",
        """
        select
          (select min("EventTime") from projection.accepted_reading_log) as first_accepted_event_time,
          (select max("EventTime") from projection.accepted_reading_log) as last_accepted_event_time,
          (select min("CreatedAt") from projection.risk_assessment_log) as first_risk_created_at,
          (select max("CreatedAt") from projection.risk_assessment_log) as last_risk_created_at,
          (select min("SnapshotTimestamp") from projection.area_risk_snapshot_log) as first_area_snapshot_timestamp,
          (select max("SnapshotTimestamp") from projection.area_risk_snapshot_log) as last_area_snapshot_timestamp,
          (select max("UpdatedAt") from projection.area_operational_state) as last_area_state_updated_at,
          (select max("UpdatedAt") from projection.cell_operational_state) as last_cell_state_updated_at,
          (select max("UpdatedAt") from projection.alert_state) as last_alert_state_updated_at;
        """,
    ),
    (
        "33_accepted_reading_latest",
        """
        select *
        from projection.accepted_reading_log
        order by "EventTime" desc
        limit 25;
        """,
    ),
    (
        "33_accepted_reading_by_metric_state",
        """
        select
          "MetricType",
          "OperationalState",
          "MeasurementUnit",
          count(*) as count,
          min("Value") as min_value,
          max("Value") as max_value,
          avg("Value") as avg_value,
          min("EventTime") as first_event_time,
          max("EventTime") as last_event_time
        from projection.accepted_reading_log
        group by "MetricType", "OperationalState", "MeasurementUnit"
        order by "MetricType", "OperationalState", "MeasurementUnit";
        """,
    ),
    (
        "33_risk_assessment_score_range",
        """
        select
          count(*) as risk_assessments,
          min("RiskScore") as min_risk_score,
          max("RiskScore") as max_risk_score,
          avg("RiskScore") as avg_risk_score
        from projection.risk_assessment_log;
        """,
    ),
    (
        "33_risk_assessment_by_level",
        """
        select
          "RiskLevel",
          count(*) as count,
          min("RiskScore") as min_score,
          max("RiskScore") as max_score,
          avg("RiskScore") as avg_score
        from projection.risk_assessment_log
        group by "RiskLevel"
        order by min_score;
        """,
    ),
    (
        "33_risk_assessment_latest",
        """
        select *
        from projection.risk_assessment_log
        order by "CreatedAt" desc
        limit 25;
        """,
    ),
    (
        "33_area_operational_state_latest",
        """
        select *
        from projection.area_operational_state
        order by "UpdatedAt" desc
        limit 25;
        """,
    ),
    (
        "33_cell_operational_state_latest",
        """
        select *
        from projection.cell_operational_state
        order by "UpdatedAt" desc
        limit 25;
        """,
    ),
    (
        "33_area_risk_snapshot_latest",
        """
        select *
        from projection.area_risk_snapshot_log
        order by "SnapshotTimestamp" desc
        limit 25;
        """,
    ),
    (
        "33_alert_state_latest",
        """
        select *
        from projection.alert_state
        order by "UpdatedAt" desc
        limit 25;
        """,
    ),
    (
        "33_alert_state_by_status",
        """
        select
          "AlertCode",
          "Severity",
          "Status",
          count(*) as count,
          min("TriggeredAt") as first_triggered_at,
          max("UpdatedAt") as last_updated_at,
          max("ResolvedAt") as last_resolved_at
        from projection.alert_state
        group by "AlertCode", "Severity", "Status"
        order by "AlertCode", "Severity", "Status";
        """,
    ),
    (
        "34_area_state_join_alerts",
        """
        select
          aos."Id" as area_operational_state_id,
          aos."AreaId",
          aos."ConfigurationVersionId",
          aos."SimulationRunId",
          aos."AggregateRiskScore",
          aos."AggregateRiskLevel",
          aos."Severity" as area_severity,
          aos."Summary" as area_summary,
          aos."AssessmentCount",
          aos."SnapshotTimestamp",
          aos."UpdatedAt" as area_updated_at,
          als."Id" as alert_state_id,
          als."AlertCode",
          als."Severity" as alert_severity,
          als."Status" as alert_status,
          als."Message" as alert_message,
          als."TriggeredAt",
          als."UpdatedAt" as alert_updated_at,
          als."ResolvedAt"
        from projection.area_operational_state aos
        left join projection.alert_state als
          on als."AreaOperationalStateId" = aos."Id"
        order by aos."UpdatedAt" desc, als."UpdatedAt" desc
        limit 50;
        """,
    ),
    (
        "34_latest_area_state_with_area",
        """
        select
          a."Code" as area_code,
          a."Name" as area_name,
          aos."AggregateRiskScore",
          aos."AggregateRiskLevel",
          aos."Severity",
          aos."Summary",
          aos."AssessmentCount",
          aos."SnapshotTimestamp",
          aos."UpdatedAt"
        from projection.area_operational_state aos
        left join control.areas a
          on a."Id" = aos."AreaId"
        order by aos."UpdatedAt" desc
        limit 25;
        """,
    ),
    (
        "34_latest_cell_state_with_area_sensor",
        """
        select
          a."Code" as area_code,
          a."Name" as area_name,
          sn."Name" as sensor_name,
          sn."Type" as sensor_type,
          cos."GridCellId",
          cos."RiskScore",
          cos."RiskLevel",
          cos."Severity",
          cos."Summary",
          cos."SnapshotTimestamp",
          cos."UpdatedAt"
        from projection.cell_operational_state cos
        left join control.areas a
          on a."Id" = cos."AreaId"
        left join control.sensor_nodes sn
          on sn."Id" = cos."SensorId"
        order by cos."UpdatedAt" desc
        limit 50;
        """,
    ),
    (
        "35_blocked_partial_zero_risk_probe",
        """
        select
          count(*) filter (where "RiskScore" = 0) as zero_risk_assessments,
          count(*) filter (where lower(coalesce("ExplanationSummary", '')) like '%blocked%') as explanations_containing_blocked,
          count(*) filter (where lower(coalesce("ExplanationSummary", '')) like '%partial%') as explanations_containing_partial,
          count(*) filter (where lower(coalesce("ExplanationSummary", '')) like '%complete%') as explanations_containing_complete
        from projection.risk_assessment_log;
        """,
    ),
    (
        "35_zero_risk_assessment_samples",
        """
        select *
        from projection.risk_assessment_log
        where "RiskScore" = 0
        order by "CreatedAt" desc
        limit 25;
        """,
    ),
    (
        "37_final_runtime_summary",
        """
        select
          (select count(*) from control.configuration_versions) as configuration_versions,
          (select count(*) from control.areas) as areas,
          (select count(*) from control.grid_cells) as grid_cells,
          (select count(*) from control.sensor_nodes) as sensor_nodes,
          (select count(*) from control.simulation_runs) as simulation_runs,

          (select count(*) from pipeline.event_inbox) as inbox_total,
          (select count(*) from pipeline.processing_attempts) as attempts_total,
          (select count(*) from pipeline.rejected_events) as rejected_total,
          (select count(*) from pipeline.quarantined_events) as quarantined_total,

          (select count(*) from projection.accepted_reading_log) as accepted_total,
          (select count(*) from projection.risk_assessment_log) as risk_total,
          (select count(*) from projection.area_risk_snapshot_log) as area_snapshot_total,
          (select count(*) from projection.cell_operational_state) as cell_state_total,
          (select count(*) from projection.area_operational_state) as area_state_total,
          (select count(*) from projection.alert_state) as alert_total;
        """,
    ),
]


def value_to_text(value: Any) -> str:
    if value is None:
        return ""

    if isinstance(value, Decimal):
        return str(value)

    if isinstance(value, (datetime,)):
        return value.isoformat()

    text = str(value)
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    return text


def markdown_escape(value: Any, max_cell_chars: int = 500) -> str:
    text = value_to_text(value)
    text = text.replace("|", "\\|")
    text = text.replace("\n", "<br>")
    if len(text) > max_cell_chars:
        return text[:max_cell_chars] + "…"
    return text


def render_markdown_table(columns: list[str], rows: list[tuple[Any, ...]]) -> str:
    if not columns:
        return "_Sem colunas._\n"

    if not rows:
        return "_Sem resultados._\n"

    out: list[str] = []
    out.append("| " + " | ".join(columns) + " |")
    out.append("| " + " | ".join(["---"] * len(columns)) + " |")

    for row in rows:
        out.append("| " + " | ".join(markdown_escape(v) for v in row) + " |")

    return "\n".join(out) + "\n"


def run_query(conn: psycopg.Connection, query: str) -> tuple[list[str], list[tuple[Any, ...]]]:
    with conn.cursor() as cur:
        cur.execute(query)
        if cur.description is None:
            return [], []

        columns = [desc.name for desc in cur.description]
        rows = cur.fetchall()
        return columns, rows


def append_section(
    report: list[str],
    section: str,
    query: str,
    columns: list[str],
    rows: list[tuple[Any, ...]],
) -> None:
    report.append(f"\n## {section}\n")
    report.append("```sql\n" + query.strip() + "\n```\n")
    report.append(f"\nLinhas devolvidas: **{len(rows)}**\n\n")
    report.append(render_markdown_table(columns, rows))


def append_error(report: list[str], section: str, query: str, exc: Exception) -> None:
    report.append(f"\n## {section}\n")
    report.append("**ERRO AO EXECUTAR ESTA SECÇÃO**\n\n")
    report.append("```sql\n" + query.strip() + "\n```\n\n")
    report.append("```text\n" + f"{type(exc).__name__}: {exc}" + "\n```\n")


def get_existing_tables(conn: psycopg.Connection) -> set[tuple[str, str]]:
    query = """
        select table_schema, table_name
        from information_schema.tables
        where table_schema in ('control', 'pipeline', 'projection')
          and table_type = 'BASE TABLE'
        order by table_schema, table_name;
    """
    _, rows = run_query(conn, query)
    return {(str(schema), str(table)) for schema, table in rows}


def run_static_sections(conn: psycopg.Connection, report: list[str]) -> None:
    for section, query in STATIC_QUERIES:
        try:
            columns, rows = run_query(conn, query)
            append_section(report, section, query, columns, rows)
        except Exception as exc:
            append_error(report, section, query, exc)


def run_expected_table_presence(conn: psycopg.Connection, report: list[str]) -> None:
    existing = get_existing_tables(conn)

    rows = []
    for schema_name, table_name in EXPECTED_TABLES:
        rows.append((schema_name, table_name, (schema_name, table_name) in existing))

    query = "-- Generated in Python from EXPECTED_TABLES"
    append_section(
        report,
        "30_expected_tables_presence",
        query,
        ["table_schema", "table_name", "exists"],
        rows,
    )


def run_exact_row_counts(conn: psycopg.Connection, report: list[str]) -> None:
    existing = sorted(get_existing_tables(conn))
    rows = []

    for schema_name, table_name in existing:
        query = sql.SQL("select count(*) from {}.{}").format(
            sql.Identifier(schema_name),
            sql.Identifier(table_name),
        )

        try:
            with conn.cursor() as cur:
                cur.execute(query)
                count = cur.fetchone()[0]
                rows.append((schema_name, table_name, count))
        except Exception as exc:
            rows.append((schema_name, table_name, f"ERROR: {type(exc).__name__}: {exc}"))

    append_section(
        report,
        "24_exact_row_counts",
        "-- Generated dynamically in Python using SELECT count(*) FROM schema.table",
        ["table_schema", "table_name", "exact_rows"],
        rows,
    )


def run_table_samples(conn: psycopg.Connection, report: list[str], sample_limit: int) -> None:
    existing = sorted(get_existing_tables(conn))

    for schema_name, table_name in existing:
        section = f"40_sample_{schema_name}_{table_name}"

        query_obj = sql.SQL("select * from {}.{} limit %s").format(
            sql.Identifier(schema_name),
            sql.Identifier(table_name),
        )

        query_for_report = f'select * from "{schema_name}"."{table_name}" limit {sample_limit};'

        try:
            with conn.cursor() as cur:
                cur.execute(query_obj, (sample_limit,))
                columns = [desc.name for desc in cur.description]
                rows = cur.fetchall()
            append_section(report, section, query_for_report, columns, rows)
        except Exception as exc:
            append_error(report, section, query_for_report, exc)


def run_runtime_sections(conn: psycopg.Connection, report: list[str]) -> None:
    existing = get_existing_tables(conn)
    expected_missing = [t for t in EXPECTED_TABLES if t not in existing]

    report.append("\n# Runtime Sections\n")

    if expected_missing:
        report.append(
            "\n> Algumas tabelas esperadas não existem. "
            "As queries runtime vão ser tentadas na mesma, mas podem falhar. "
            "Isto é útil para documentar diferenças entre branch/migration/schema.\n"
        )
        for schema_name, table_name in expected_missing:
            report.append(f"- Ausente: `{schema_name}.{table_name}`\n")

    for section, query in RUNTIME_QUERIES:
        try:
            columns, rows = run_query(conn, query)
            append_section(report, section, query, columns, rows)
        except Exception as exc:
            append_error(report, section, query, exc)


def build_connection(args: argparse.Namespace) -> psycopg.Connection:
    if args.dsn:
        return psycopg.connect(args.dsn, autocommit=True)

    password = args.password or os.getenv("PGPASSWORD")

    if password is None and not args.no_password_prompt:
        password = getpass.getpass("PostgreSQL password, ENTER para vazio: ")
        if password == "":
            password = None

    return psycopg.connect(
        host=args.host,
        port=args.port,
        dbname=args.database,
        user=args.user,
        password=password,
        autocommit=True,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Exporta evidência estrutural e runtime da DB NatureProtector para Markdown."
    )

    parser.add_argument("--dsn", default=os.getenv("DATABASE_URL"), help="PostgreSQL DSN completo.")
    parser.add_argument("--host", default=os.getenv("PGHOST", "localhost"))
    parser.add_argument("--port", default=int(os.getenv("PGPORT", "5432")), type=int)
    parser.add_argument("--database", default=os.getenv("PGDATABASE", "natureprotector"))
    parser.add_argument("--user", default=os.getenv("PGUSER", "postgres"))
    parser.add_argument("--password", default=os.getenv("PGPASSWORD"))
    parser.add_argument("--no-password-prompt", action="store_true")

    parser.add_argument(
        "--output",
        default=None,
        help="Ficheiro Markdown de saída. Default: docs/evidence/db-evidence-<timestamp>.md",
    )

    parser.add_argument(
        "--sample-limit",
        default=25,
        type=int,
        help="Número de linhas por amostra dinâmica de cada tabela.",
    )

    parser.add_argument(
        "--skip-samples",
        action="store_true",
        help="Não exporta SELECT * LIMIT N por cada tabela.",
    )

    parser.add_argument(
        "--skip-runtime",
        action="store_true",
        help="Não executa queries runtime específicas do NatureProtector.",
    )

    return parser.parse_args()


def main() -> int:
    args = parse_args()

    timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")

    if args.output:
        output_path = Path(args.output)
    else:
        output_path = Path("docs") / "evidence" / f"db-evidence-{timestamp}.md"

    output_path.parent.mkdir(parents=True, exist_ok=True)

    report: list[str] = []
    report.append("# NatureProtector DB Evidence Export\n")
    report.append(f"- GeneratedAt: `{datetime.now().isoformat(timespec='seconds')}`\n")
    report.append(f"- Target schemas: `{', '.join(TARGET_SCHEMAS)}`\n")
    report.append(f"- Output file: `{output_path}`\n")

    try:
        with build_connection(args) as conn:
            report.append("\n# Structural Inventory\n")
            run_static_sections(conn, report)
            run_exact_row_counts(conn, report)
            run_expected_table_presence(conn, report)

            if not args.skip_runtime:
                run_runtime_sections(conn, report)

            if not args.skip_samples:
                report.append("\n# Dynamic Table Samples\n")
                run_table_samples(conn, report, args.sample_limit)

    except Exception as exc:
        report.append("\n# Fatal Error\n")
        report.append("```text\n")
        report.append(f"{type(exc).__name__}: {exc}\n")
        report.append("```\n")

        output_path.write_text("".join(report), encoding="utf-8")
        print(f"[ERROR] Falhou. Relatório parcial escrito em: {output_path}", file=sys.stderr)
        print(f"{type(exc).__name__}: {exc}", file=sys.stderr)
        return 1

    output_path.write_text("".join(report), encoding="utf-8")
    print(f"[OK] Relatório escrito em: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
