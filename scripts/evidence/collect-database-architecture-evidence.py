#!/usr/bin/env python3
"""Collect reproducible NatureProtector PostgreSQL architecture evidence.

Phase 3 combines two evidence classes without confusing them:
1. STATIC_EFFECTIVE_DATABASE_MODEL reconstructed from the current EF Core model
   snapshot plus raw-SQL migrations not represented in that snapshot.
2. CURRENT_LIVE_DATABASE_INVENTORY when a PostgreSQL DSN is explicitly supplied
   and psycopg v3 is available.

The static path requires only the Python standard library. It never runs Git,
Docker, cloud commands, tests, migrations, or application services. Live mode is
read-only and only inspects PostgreSQL catalogues and table statistics.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import time
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterator, Sequence
from urllib.parse import urlsplit, urlunsplit

SCRIPT_VERSION = "1.0.0"
STATIC_EVIDENCE_CLASS = "STATIC_EFFECTIVE_DATABASE_MODEL"
LIVE_EVIDENCE_CLASS = "CURRENT_LIVE_DATABASE_INVENTORY"

SCHEMA_ROLES = {
    "control": "Configuração, território, cenários, sensores, execuções e orquestração.",
    "pipeline": "Inbox durável, tentativas, rejeições e quarentena do processamento.",
    "projection": "Logs e projeções de leituras, risco, estado operacional e alertas.",
    "user_base": "Utilizadores, roles e associação de autorização.",
}

TABLE_ROLES = {
    "control.configuration_versions": "Versões imutáveis da configuração do domínio.",
    "control.areas": "Áreas territoriais configuradas.",
    "control.area_contexts": "Contexto de vegetação, população e infraestrutura por área.",
    "control.grid_cells": "Células territoriais e atributos geográficos/combustível.",
    "control.dataset_artifacts": "Proveniência e localização dos datasets.",
    "control.rule_set_versions": "Versões e parâmetros das regras.",
    "control.scenario_definitions": "Definições de cenários e relações de derivação.",
    "control.scenario_dataset_bindings": "Associação entre cenários e datasets.",
    "control.sensor_networks": "Redes de sensores configuradas.",
    "control.sensor_profiles": "Perfis de precisão, ruído, falha e publicação.",
    "control.sensor_nodes": "Sensores e associação territorial/configuracional.",
    "control.simulation_runs": "Identidade, parâmetros e ciclo de vida das simulações.",
    "control.runtime_orchestrator_executions": "Estado durável das execuções do orquestrador de runtime.",
    "pipeline.event_inbox": "Inbox idempotente dos eventos recebidos.",
    "pipeline.processing_attempts": "Tentativas e resultados de processamento.",
    "pipeline.rejected_events": "Eventos rejeitados antes ou durante a aceitação.",
    "pipeline.quarantined_events": "Eventos enviados para quarentena após falhas.",
    "projection.accepted_reading_log": "Registo append-only de leituras aceites.",
    "projection.risk_assessment_log": "Avaliações de risco e componentes explicativos.",
    "projection.area_risk_snapshot_log": "Snapshots históricos agregados por área.",
    "projection.cell_operational_state": "Estado operacional vigente por célula/sensor.",
    "projection.area_operational_state": "Estado operacional agregado vigente por área/run.",
    "projection.daily_cell_state": "Estado diário, índices candidatos e proveniência por célula.",
    "projection.alert_state": "Ciclo de vida dos estados de alerta internos.",
    "user_base.users": "Utilizadores autenticáveis.",
    "user_base.roles": "Roles de autorização.",
    "user_base.user_roles": "Relação utilizador–role.",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def compact_utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return ""


def safe_rel(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def normalize_csv(value: Any) -> Any:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (list, tuple, set)):
        return "; ".join(str(item) for item in value)
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return value


def write_csv(path: Path, rows: Sequence[dict[str, Any]], fieldnames: Sequence[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        fieldnames = list(rows[0].keys()) if rows else []
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: normalize_csv(row.get(key)) for key in fieldnames})


def markdown_table(headers: Sequence[str], rows: Sequence[Sequence[Any]]) -> str:
    lines = [
        "| " + " | ".join(headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(str(v).replace("|", "\\|") for v in row) + " |")
    return "\n".join(lines)


def extract_brace_block(text: str, opening_brace: int) -> tuple[str, int]:
    depth = 0
    in_string = False
    verbatim = False
    escape = False
    i = opening_brace
    while i < len(text):
        ch = text[i]
        if in_string:
            if verbatim:
                if ch == '"' and i + 1 < len(text) and text[i + 1] == '"':
                    i += 2
                    continue
                if ch == '"':
                    in_string = False
                    verbatim = False
            else:
                if escape:
                    escape = False
                elif ch == "\\":
                    escape = True
                elif ch == '"':
                    in_string = False
        else:
            if ch == "@" and i + 1 < len(text) and text[i + 1] == '"':
                in_string = True
                verbatim = True
                i += 2
                continue
            if ch == '"':
                in_string = True
            elif ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    return text[opening_brace : i + 1], i + 1
        i += 1
    return text[opening_brace:], len(text)


def iter_entity_blocks(text: str) -> Iterator[tuple[str, str]]:
    pattern = re.compile(r'modelBuilder\.Entity\("([^"]+)",\s*b\s*=>\s*')
    cursor = 0
    while True:
        match = pattern.search(text, cursor)
        if not match:
            break
        brace = text.find("{", match.end())
        if brace < 0:
            break
        block, end = extract_brace_block(text, brace)
        yield match.group(1), block
        cursor = end


def split_top_level_csv(text: str) -> list[str]:
    parts: list[str] = []
    start = 0
    depth = 0
    in_single = False
    in_double = False
    i = 0
    while i < len(text):
        ch = text[i]
        if in_single:
            if ch == "'" and i + 1 < len(text) and text[i + 1] == "'":
                i += 2
                continue
            if ch == "'":
                in_single = False
        elif in_double:
            if ch == '"' and i + 1 < len(text) and text[i + 1] == '"':
                i += 2
                continue
            if ch == '"':
                in_double = False
        else:
            if ch == "'":
                in_single = True
            elif ch == '"':
                in_double = True
            elif ch == "(":
                depth += 1
            elif ch == ")":
                depth = max(0, depth - 1)
            elif ch == "," and depth == 0:
                parts.append(text[start:i].strip())
                start = i + 1
        i += 1
    tail = text[start:].strip()
    if tail:
        parts.append(tail)
    return parts


def clean_identifier(value: str) -> str:
    value = value.strip()
    if value.startswith('"') and value.endswith('"'):
        return value[1:-1].replace('""', '"')
    return value


def find_snapshot(repo: Path) -> Path:
    candidates = sorted(repo.glob("src/**/Migrations/*ModelSnapshot.cs"))
    if not candidates:
        raise FileNotFoundError("No EF Core ModelSnapshot.cs was found.")
    if len(candidates) > 1:
        exact = [p for p in candidates if p.name == "NatureProtectorControlDbContextModelSnapshot.cs"]
        if exact:
            return exact[0]
    return candidates[0]


def collect_static_model(repo: Path) -> dict[str, Any]:
    snapshot = find_snapshot(repo)
    text = read_text(snapshot)
    entity_to_table: dict[str, tuple[str, str]] = {}
    table_rows: dict[tuple[str, str], dict[str, Any]] = {}
    column_rows: dict[tuple[str, str, str], dict[str, Any]] = {}
    pk_rows: dict[tuple[str, str, str], dict[str, Any]] = {}
    index_rows: dict[tuple[str, str, str], dict[str, Any]] = {}
    entity_blocks = list(iter_entity_blocks(text))

    for entity, block in entity_blocks:
        table_match = re.search(r'b\.ToTable\("([^"]+)",\s*"([^"]+)"\)', block)
        if not table_match:
            continue
        table, schema = table_match.groups()
        entity_to_table[entity] = (schema, table)
        table_rows[(schema, table)] = {
            "schema": schema,
            "table": table,
            "qualified_table": f"{schema}.{table}",
            "model_entity": entity,
            "role": TABLE_ROLES.get(f"{schema}.{table}", ""),
            "source": safe_rel(snapshot, repo),
            "declaration_kind": "ef_model_snapshot",
            "evidence_class": STATIC_EVIDENCE_CLASS,
        }

        properties = list(re.finditer(r'b\.Property<([^>]+)>\("([^"]+)"\)', block))
        for idx, prop in enumerate(properties):
            stop = properties[idx + 1].start() if idx + 1 < len(properties) else len(block)
            segment = block[prop.start() : stop]
            cut = re.search(r"\n\s*b\.(?:HasKey|HasIndex|ToTable)\b", segment)
            if cut:
                segment = segment[: cut.start()]
            clr_type, column = prop.groups()
            sql_type_match = re.search(r'\.HasColumnType\("([^"]+)"\)', segment)
            max_len_match = re.search(r"\.HasMaxLength\((\d+)\)", segment)
            default_sql_match = re.search(r'\.HasDefaultValueSql\("([^"]+)"\)', segment)
            default_value_match = re.search(r"\.HasDefaultValue\(([^)]+)\)", segment)
            required = ".IsRequired()" in segment
            nullable = clr_type.endswith("?") or (clr_type == "string" and not required)
            column_rows[(schema, table, column)] = {
                "schema": schema,
                "table": table,
                "qualified_table": f"{schema}.{table}",
                "ordinal_position": idx + 1,
                "column": column,
                "clr_type": clr_type,
                "sql_type": sql_type_match.group(1) if sql_type_match else "",
                "nullable": nullable,
                "max_length": int(max_len_match.group(1)) if max_len_match else None,
                "value_generated": "on_add" if ".ValueGeneratedOnAdd()" in segment else "never_or_unspecified",
                "default": (
                    default_sql_match.group(1)
                    if default_sql_match
                    else (default_value_match.group(1) if default_value_match else "")
                ),
                "source": safe_rel(snapshot, repo),
                "declaration_kind": "ef_model_snapshot_property",
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }

        for key_match in re.finditer(r"b\.HasKey\(([^)]*)\)", block):
            columns = re.findall(r'"([^"]+)"', key_match.group(1))
            if not columns:
                continue
            segment = block[key_match.start() :]
            cut = re.search(r"\n\s*b\.(?:HasIndex|ToTable)\b", segment)
            if cut:
                segment = segment[: cut.start()]
            name_match = re.search(r'\.HasName\("([^"]+)"\)', segment)
            name = name_match.group(1) if name_match else f"PK_{schema}_{table}"
            pk_rows[(schema, table, name)] = {
                "schema": schema,
                "table": table,
                "qualified_table": f"{schema}.{table}",
                "constraint": name,
                "columns": columns,
                "source": safe_rel(snapshot, repo),
                "declaration_kind": "ef_model_snapshot_primary_key",
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }

        indexes = list(re.finditer(r"b\.HasIndex\(([^)]*)\)", block))
        for idx_no, index_match in enumerate(indexes):
            stop = indexes[idx_no + 1].start() if idx_no + 1 < len(indexes) else len(block)
            segment = block[index_match.start() : stop]
            cut = re.search(r"\n\s*b\.(?:HasKey|ToTable)\b", segment)
            if cut:
                segment = segment[: cut.start()]
            columns = re.findall(r'"([^"]+)"', index_match.group(1))
            if not columns:
                continue
            name_match = re.search(r'\.HasDatabaseName\("([^"]+)"\)', segment)
            name = name_match.group(1) if name_match else f"IX_{schema}_{table}_{'_'.join(columns)}"
            index_rows[(schema, table, name)] = {
                "schema": schema,
                "table": table,
                "qualified_table": f"{schema}.{table}",
                "index": name,
                "columns": columns,
                "unique": ".IsUnique()" in segment,
                "filter": (
                    re.search(r'\.HasFilter\("([^"]+)"\)', segment).group(1)
                    if re.search(r'\.HasFilter\("([^"]+)"\)', segment)
                    else ""
                ),
                "source": safe_rel(snapshot, repo),
                "declaration_kind": "ef_model_snapshot_index",
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }

    fk_rows: dict[tuple[str, str, str, str, tuple[str, ...]], dict[str, Any]] = {}
    # Relationship blocks repeat modelBuilder.Entity and do not include ToTable.
    for source_entity, block in entity_blocks:
        if source_entity not in entity_to_table or ".HasOne(" not in block:
            continue
        source_schema, source_table = entity_to_table[source_entity]
        has_ones = list(re.finditer(r'b\.HasOne\("([^"]+)"(?:,\s*(?:"[^"]+"|null))?\)', block))
        for i, has_one in enumerate(has_ones):
            stop = has_ones[i + 1].start() if i + 1 < len(has_ones) else len(block)
            segment = block[has_one.start() : stop]
            target_entity = has_one.group(1)
            target = entity_to_table.get(target_entity)
            if not target:
                continue
            fk_match = re.search(r"\.HasForeignKey\(([^)]*)\)", segment)
            if not fk_match:
                continue
            quoted = re.findall(r'"([^"]+)"', fk_match.group(1))
            source_columns = {key[2] for key in column_rows if key[0] == source_schema and key[1] == source_table}
            fk_columns = [item for item in quoted if item in source_columns]
            if not fk_columns and quoted:
                # Generic HasForeignKey<TEntity>(...) can include an entity name first.
                fk_columns = [item for item in quoted if "." not in item]
            if not fk_columns:
                continue
            target_schema, target_table = target
            target_pk = next(
                (row for key, row in pk_rows.items() if key[0] == target_schema and key[1] == target_table), None
            )
            target_columns = target_pk["columns"] if target_pk else ["Id"]
            delete_match = re.search(r"\.OnDelete\(DeleteBehavior\.([A-Za-z]+)\)", segment)
            required = ".IsRequired()" in segment
            name = f"FK_{source_schema}_{source_table}_{target_schema}_{target_table}_{'_'.join(fk_columns)}"
            key = (source_schema, source_table, target_schema, target_table, tuple(fk_columns))
            fk_rows[key] = {
                "schema": source_schema,
                "table": source_table,
                "qualified_table": f"{source_schema}.{source_table}",
                "constraint": name,
                "columns": fk_columns,
                "referenced_schema": target_schema,
                "referenced_table": target_table,
                "referenced_qualified_table": f"{target_schema}.{target_table}",
                "referenced_columns": target_columns[: len(fk_columns)],
                "required_relationship": required,
                "delete_behavior": delete_match.group(1) if delete_match else "provider_default_or_unspecified",
                "source": safe_rel(snapshot, repo),
                "declaration_kind": "ef_model_snapshot_foreign_key",
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }

    # Add raw-SQL tables/indexes/constraints absent from the effective snapshot.
    migration_files = sorted(repo.glob("src/**/Migrations/*.cs"))
    raw_migration_ids: list[str] = []
    for migration in migration_files:
        migration_text = read_text(migration)
        migration_id_match = re.search(r'\[Migration\("([^"]+)"\)\]', migration_text)
        migration_id = migration_id_match.group(1) if migration_id_match else migration.stem
        for create in re.finditer(
            r'(?is)CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:("?[A-Za-z_][A-Za-z0-9_]*"?)\.)?("?[A-Za-z_][A-Za-z0-9_]*"?)\s*\((.*?)\)\s*;',
            migration_text,
        ):
            schema = clean_identifier(create.group(1) or "public")
            table = clean_identifier(create.group(2))
            raw_migration_ids.append(migration_id)
            table_rows[(schema, table)] = {
                "schema": schema,
                "table": table,
                "qualified_table": f"{schema}.{table}",
                "model_entity": "",
                "role": TABLE_ROLES.get(f"{schema}.{table}", ""),
                "source": safe_rel(migration, repo),
                "declaration_kind": "raw_sql_create_table",
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }
            body_items = split_top_level_csv(create.group(3))
            ordinal = 0
            for item in body_items:
                stripped = item.strip()
                constraint_match = re.match(r'(?is)^CONSTRAINT\s+("?[A-Za-z_][A-Za-z0-9_]*"?)\s+(.+)$', stripped)
                if constraint_match:
                    constraint_name = clean_identifier(constraint_match.group(1))
                    specification = constraint_match.group(2)
                    pk_match = re.match(r"(?is)^PRIMARY\s+KEY\s*\(([^)]+)\)", specification)
                    if pk_match:
                        cols = [clean_identifier(v) for v in split_top_level_csv(pk_match.group(1))]
                        pk_rows[(schema, table, constraint_name)] = {
                            "schema": schema,
                            "table": table,
                            "qualified_table": f"{schema}.{table}",
                            "constraint": constraint_name,
                            "columns": cols,
                            "source": safe_rel(migration, repo),
                            "declaration_kind": "raw_sql_primary_key",
                            "evidence_class": STATIC_EVIDENCE_CLASS,
                        }
                    unique_match = re.match(r"(?is)^UNIQUE\s*\(([^)]+)\)", specification)
                    if unique_match:
                        cols = [clean_identifier(v) for v in split_top_level_csv(unique_match.group(1))]
                        index_rows[(schema, table, constraint_name)] = {
                            "schema": schema,
                            "table": table,
                            "qualified_table": f"{schema}.{table}",
                            "index": constraint_name,
                            "columns": cols,
                            "unique": True,
                            "filter": "",
                            "source": safe_rel(migration, repo),
                            "declaration_kind": "raw_sql_unique_constraint",
                            "evidence_class": STATIC_EVIDENCE_CLASS,
                        }
                    fk_match = re.match(
                        r'(?is)^FOREIGN\s+KEY\s*\(([^)]+)\)\s+REFERENCES\s+(?:("?[A-Za-z_][A-Za-z0-9_]*"?)\.)?("?[A-Za-z_][A-Za-z0-9_]*"?)\s*\(([^)]+)\)',
                        specification,
                    )
                    if fk_match:
                        cols = [clean_identifier(v) for v in split_top_level_csv(fk_match.group(1))]
                        ref_schema = clean_identifier(fk_match.group(2) or schema)
                        ref_table = clean_identifier(fk_match.group(3))
                        ref_cols = [clean_identifier(v) for v in split_top_level_csv(fk_match.group(4))]
                        key = (schema, table, ref_schema, ref_table, tuple(cols))
                        fk_rows[key] = {
                            "schema": schema,
                            "table": table,
                            "qualified_table": f"{schema}.{table}",
                            "constraint": constraint_name,
                            "columns": cols,
                            "referenced_schema": ref_schema,
                            "referenced_table": ref_table,
                            "referenced_qualified_table": f"{ref_schema}.{ref_table}",
                            "referenced_columns": ref_cols,
                            "required_relationship": False,
                            "delete_behavior": "raw_sql_declared",
                            "source": safe_rel(migration, repo),
                            "declaration_kind": "raw_sql_foreign_key",
                            "evidence_class": STATIC_EVIDENCE_CLASS,
                        }
                    continue
                if re.match(r"(?is)^(PRIMARY\s+KEY|UNIQUE|FOREIGN\s+KEY|CHECK)\b", stripped):
                    continue
                column_match = re.match(r'(?is)^("?[A-Za-z_][A-Za-z0-9_]*"?)\s+(.+)$', stripped)
                if not column_match:
                    continue
                ordinal += 1
                column = clean_identifier(column_match.group(1))
                spec = column_match.group(2).strip()
                type_text = re.split(
                    r"\s+(?:NOT\s+NULL|NULL|PRIMARY\s+KEY|UNIQUE|DEFAULT|REFERENCES|CHECK|CONSTRAINT)\b",
                    spec,
                    maxsplit=1,
                    flags=re.IGNORECASE,
                )[0].strip()
                nullable = not bool(re.search(r"(?i)\bNOT\s+NULL\b|\bPRIMARY\s+KEY\b", spec))
                default_match = re.search(
                    r"(?is)\bDEFAULT\s+(.+?)(?:\s+(?:NOT\s+NULL|NULL|PRIMARY\s+KEY|UNIQUE|REFERENCES|CHECK|CONSTRAINT)\b|$)",
                    spec,
                )
                column_rows[(schema, table, column)] = {
                    "schema": schema,
                    "table": table,
                    "qualified_table": f"{schema}.{table}",
                    "ordinal_position": ordinal,
                    "column": column,
                    "clr_type": "",
                    "sql_type": type_text,
                    "nullable": nullable,
                    "max_length": None,
                    "value_generated": "unspecified",
                    "default": default_match.group(1).strip() if default_match else "",
                    "source": safe_rel(migration, repo),
                    "declaration_kind": "raw_sql_create_table_column",
                    "evidence_class": STATIC_EVIDENCE_CLASS,
                }
                if re.search(r"(?i)\bPRIMARY\s+KEY\b", spec):
                    name = f"PK_{schema}_{table}"
                    pk_rows[(schema, table, name)] = {
                        "schema": schema,
                        "table": table,
                        "qualified_table": f"{schema}.{table}",
                        "constraint": name,
                        "columns": [column],
                        "source": safe_rel(migration, repo),
                        "declaration_kind": "raw_sql_inline_primary_key",
                        "evidence_class": STATIC_EVIDENCE_CLASS,
                    }
                if re.search(r"(?i)\bUNIQUE\b", spec):
                    name = f"UQ_{schema}_{table}_{column}"
                    index_rows[(schema, table, name)] = {
                        "schema": schema,
                        "table": table,
                        "qualified_table": f"{schema}.{table}",
                        "index": name,
                        "columns": [column],
                        "unique": True,
                        "filter": "",
                        "source": safe_rel(migration, repo),
                        "declaration_kind": "raw_sql_inline_unique",
                        "evidence_class": STATIC_EVIDENCE_CLASS,
                    }

        for index_match in re.finditer(
            r'(?is)CREATE\s+(UNIQUE\s+)?INDEX\s+("?[A-Za-z_][A-Za-z0-9_]*"?)\s+ON\s+(?:("?[A-Za-z_][A-Za-z0-9_]*"?)\.)?("?[A-Za-z_][A-Za-z0-9_]*"?)\s*\(([^)]+)\)',
            migration_text,
        ):
            unique, index_name, schema_raw, table_raw, columns_raw = index_match.groups()
            schema = clean_identifier(schema_raw or "public")
            table = clean_identifier(table_raw)
            name = clean_identifier(index_name)
            cols = [clean_identifier(v.split()[0]) for v in split_top_level_csv(columns_raw)]
            index_rows[(schema, table, name)] = {
                "schema": schema,
                "table": table,
                "qualified_table": f"{schema}.{table}",
                "index": name,
                "columns": cols,
                "unique": bool(unique),
                "filter": "",
                "source": safe_rel(migration, repo),
                "declaration_kind": "raw_sql_create_index",
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }

    tables = sorted(table_rows.values(), key=lambda row: (row["schema"], row["table"]))
    columns = sorted(column_rows.values(), key=lambda row: (row["schema"], row["table"], int(row["ordinal_position"])))
    pks = sorted(pk_rows.values(), key=lambda row: (row["schema"], row["table"], row["constraint"]))
    fks = sorted(fk_rows.values(), key=lambda row: (row["schema"], row["table"], row["constraint"]))
    indexes = sorted(index_rows.values(), key=lambda row: (row["schema"], row["table"], row["index"]))

    by_table_columns = Counter((row["schema"], row["table"]) for row in columns)
    by_table_indexes = Counter((row["schema"], row["table"]) for row in indexes)
    by_table_fks = Counter((row["schema"], row["table"]) for row in fks)
    by_table_referenced = Counter((row["referenced_schema"], row["referenced_table"]) for row in fks)
    pk_table_set = {(row["schema"], row["table"]) for row in pks}
    for table in tables:
        key = (table["schema"], table["table"])
        table["column_count"] = by_table_columns[key]
        table["primary_key_present"] = key in pk_table_set
        table["foreign_key_count"] = by_table_fks[key]
        table["referenced_by_count"] = by_table_referenced[key]
        table["index_count_including_unique_constraints"] = by_table_indexes[key]

    schemas: list[dict[str, Any]] = []
    for schema in sorted({row["schema"] for row in tables}):
        schemas.append(
            {
                "schema": schema,
                "role": SCHEMA_ROLES.get(schema, ""),
                "table_count": sum(1 for row in tables if row["schema"] == schema),
                "column_count": sum(1 for row in columns if row["schema"] == schema),
                "primary_key_count": sum(1 for row in pks if row["schema"] == schema),
                "outgoing_foreign_key_count": sum(1 for row in fks if row["schema"] == schema),
                "index_count_including_unique_constraints": sum(1 for row in indexes if row["schema"] == schema),
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }
        )

    migrations = []
    for path in migration_files:
        if path.name.endswith(".Designer.cs") or path.name.endswith("ModelSnapshot.cs"):
            continue
        source = read_text(path)
        migration_match = re.search(r'\[Migration\("([^"]+)"\)\]', source)
        migrations.append(
            {
                "migration_id": migration_match.group(1) if migration_match else path.stem,
                "path": safe_rel(path, repo),
                "uses_raw_sql": "migrationBuilder.Sql(" in source,
                "creates_table": bool(re.search(r"(?i)CreateTable\s*\(|CREATE\s+TABLE", source)),
                "creates_index": bool(re.search(r"(?i)CreateIndex\s*\(|CREATE\s+(?:UNIQUE\s+)?INDEX", source)),
                "evidence_class": STATIC_EVIDENCE_CLASS,
            }
        )

    return {
        "snapshot": safe_rel(snapshot, repo),
        "schemas": schemas,
        "tables": tables,
        "columns": columns,
        "primary_keys": pks,
        "foreign_keys": fks,
        "indexes": indexes,
        "migrations": sorted(migrations, key=lambda row: row["migration_id"]),
        "raw_sql_migrations_merged": sorted(set(raw_migration_ids)),
    }


def quote_sql_identifier(identifier: str) -> str:
    return '"' + identifier.replace('"', '""') + '"'


def build_critical_query_catalog(model: dict[str, Any]) -> list[dict[str, Any]]:
    available = {row["qualified_table"] for row in model["tables"]}
    definitions = [
        {
            "id": "DBQ-01",
            "title": "Configuração ativa e áreas",
            "category": "control_plane_read",
            "tables": ["control.configuration_versions", "control.areas"],
            "purpose": "Ler a configuração ativa e as áreas disponíveis, um percurso frequente do Backoffice.",
            "sql": """SELECT cv."Id", cv."VersionNumber", cv."CreatedAt", a."Id" AS "AreaId", a."Code", a."Name"\nFROM control.configuration_versions AS cv\nJOIN control.areas AS a ON a."ConfigurationVersionId" = cv."Id"\nWHERE cv."IsActive" = TRUE\nORDER BY a."Code";""",
            "expected_access_pattern": "Índice único de VersionNumber e índice composto ConfigurationVersionId+Code; filtro IsActive pode exigir scan se a cardinalidade crescer.",
        },
        {
            "id": "DBQ-02",
            "title": "Execuções recentes por área e estado",
            "category": "simulation_run_read",
            "tables": ["control.simulation_runs"],
            "purpose": "Listar execuções recentes e o respetivo ciclo de vida.",
            "sql": """SELECT "Id", "AreaId", "ScenarioCode", "Status", "CreatedAt", "StartedAt", "EndedAt"\nFROM control.simulation_runs\nWHERE "AreaId" = :'area_id'::uuid\nORDER BY "CreatedAt" DESC\nLIMIT 50;""",
            "expected_access_pattern": "Deve beneficiar de índice por AreaId e ordenação temporal; validar eventual sort e rows removed.",
        },
        {
            "id": "DBQ-03",
            "title": "Distribuição e backlog da inbox",
            "category": "pipeline_health",
            "tables": ["pipeline.event_inbox"],
            "purpose": "Quantificar estados da inbox, idade do backlog e número de tentativas.",
            "sql": """SELECT "Status", COUNT(*) AS event_count, MIN("ReceivedAt") AS oldest_received_at, MAX("AttemptCount") AS max_attempt_count\nFROM pipeline.event_inbox\nGROUP BY "Status"\nORDER BY "Status";""",
            "expected_access_pattern": "Agregação sobre a inbox; útil para baseline de backlog, mas naturalmente lê todas as linhas do conjunto medido.",
        },
        {
            "id": "DBQ-04",
            "title": "Próximos eventos elegíveis para processamento",
            "category": "pipeline_hot_path",
            "tables": ["pipeline.event_inbox"],
            "purpose": "Representar o percurso de seleção de eventos pendentes/retry.",
            "sql": """SELECT "Id", "EventId", "Status", "AttemptCount", "ReceivedAt", "NextAttemptNotBefore"\nFROM pipeline.event_inbox\nWHERE "Status" IN ('received', 'retry_scheduled')\n  AND ("NextAttemptNotBefore" IS NULL OR "NextAttemptNotBefore" <= NOW())\nORDER BY "ReceivedAt"\nLIMIT 100;""",
            "expected_access_pattern": "Validar índice parcial/composto por Status, NextAttemptNotBefore e ReceivedAt; percurso candidato a hot path.",
        },
        {
            "id": "DBQ-05",
            "title": "Falhas de processamento por etapa e código",
            "category": "pipeline_diagnostics",
            "tables": ["pipeline.processing_attempts"],
            "purpose": "Identificar causas dominantes de falha e custo de retry.",
            "sql": """SELECT "Stage", "Outcome", "ErrorCode", COUNT(*) AS attempt_count, AVG(EXTRACT(EPOCH FROM ("FinishedAt" - "StartedAt")) * 1000.0) AS avg_duration_ms\nFROM pipeline.processing_attempts\nGROUP BY "Stage", "Outcome", "ErrorCode"\nORDER BY attempt_count DESC;""",
            "expected_access_pattern": "Agregação analítica; medir separadamente de percursos OLTP e limitar por janela temporal quando o volume crescer.",
        },
        {
            "id": "DBQ-06",
            "title": "Estado operacional mais recente da área",
            "category": "projection_read",
            "tables": ["projection.area_operational_state"],
            "purpose": "Obter o estado vigente apresentado pela API/UI para uma área.",
            "sql": """SELECT "AreaId", "SimulationRunId", "AggregateRiskScore", "AggregateRiskLevel", "CoverageStatus", "FreshnessStatus", "AssessmentCount", "UpdatedAt"\nFROM projection.area_operational_state\nWHERE "AreaId" = :'area_id'::uuid\nORDER BY "UpdatedAt" DESC\nLIMIT 1;""",
            "expected_access_pattern": "Deve resolver por índice em AreaId/UpdatedAt ou chave de unicidade equivalente; confirmar ausência de sort global.",
        },
        {
            "id": "DBQ-07",
            "title": "Avaliações de risco de uma execução",
            "category": "risk_analysis",
            "tables": ["projection.risk_assessment_log"],
            "purpose": "Extrair resultados e componentes do scoring para uma execução identificada.",
            "sql": """SELECT "Timestamp", "GridCellId", "SensorId", "BaseRisk", "AdjustedScore", "Score100", "ConfidenceFactor", "IntegrityFactor", "RiskLevel", "CalculationStatus"\nFROM projection.risk_assessment_log\nWHERE "SimulationRunId" = :'simulation_run_id'::uuid\nORDER BY "Timestamp", "GridCellId", "SensorId";""",
            "expected_access_pattern": "Índice por SimulationRunId e Timestamp é determinante para exportações por run e comparação B/C.",
        },
        {
            "id": "DBQ-08",
            "title": "Snapshots agregados B/C por execução",
            "category": "run_comparison",
            "tables": ["projection.area_risk_snapshot_log"],
            "purpose": "Obter a série agregada usada na comparação entre execuções.",
            "sql": """SELECT "SimulationRunId", "SnapshotTimestamp", "AggregateRiskScore", "AggregateRiskLevel", "AssessmentCount"\nFROM projection.area_risk_snapshot_log\nWHERE "SimulationRunId" IN (:'run_b'::uuid, :'run_c'::uuid)\nORDER BY "SimulationRunId", "SnapshotTimestamp";""",
            "expected_access_pattern": "Índice por SimulationRunId e SnapshotTimestamp; confirmar cardinalidade e estabilidade da ordenação.",
        },
        {
            "id": "DBQ-09",
            "title": "Estado diário e índices candidatos por célula",
            "category": "method_evidence",
            "tables": ["projection.daily_cell_state"],
            "purpose": "Extrair estado antecedente, FWI/KBDI candidatos e limitações por run/célula.",
            "sql": """SELECT "LogicalDate", "GridCellId", "SensorId", "FireWeatherIndex", "KeetchByramDroughtIndex", "FireWeatherCalculationStatus", "KbdiCalculationStatus", "Provenance"\nFROM projection.daily_cell_state\nWHERE "SimulationRunId" = :'simulation_run_id'::uuid\nORDER BY "LogicalDate", "GridCellId", "SensorId";""",
            "expected_access_pattern": "Índice composto por SimulationRunId, LogicalDate e identidade da célula; validar cobertura da consulta.",
        },
        {
            "id": "DBQ-10",
            "title": "Alertas internos ativos por área",
            "category": "alert_projection",
            "tables": ["projection.alert_state"],
            "purpose": "Consultar estados de alerta ainda não resolvidos sem os tratar como avisos oficiais.",
            "sql": """SELECT "AreaId", "AlertCode", "Severity", "Status", "TriggeredAt", "UpdatedAt", "Message"\nFROM projection.alert_state\nWHERE "AreaId" = :'area_id'::uuid\n  AND "ResolvedAt" IS NULL\nORDER BY "UpdatedAt" DESC;""",
            "expected_access_pattern": "Índice por AreaId e estado/resolução; um índice parcial para ResolvedAt IS NULL pode ser avaliado com dados reais.",
        },
        {
            "id": "DBQ-11",
            "title": "Execuções do orquestrador por estado",
            "category": "runtime_control",
            "tables": ["control.runtime_orchestrator_executions"],
            "purpose": "Inspecionar operações de runtime aceites, em curso, concluídas ou falhadas.",
            "sql": """SELECT execution_id, provider, state, accepted_at, updated_at, started_at, finished_at, failure_code, log_correlation, evidence_id\nFROM control.runtime_orchestrator_executions\nWHERE state = :'orchestrator_state'\nORDER BY updated_at DESC\nLIMIT 100;""",
            "expected_access_pattern": "Coberto pelo índice state+updated_at introduzido com a tabela.",
        },
        {
            "id": "DBQ-12",
            "title": "Reconciliação da execução",
            "category": "integrity_reconciliation",
            "tables": [
                "projection.risk_assessment_log",
                "projection.area_risk_snapshot_log",
                "projection.daily_cell_state",
            ],
            "purpose": "Contabilizar artefactos produzidos para uma SimulationRunId.",
            "sql": """SELECT\n  :'simulation_run_id'::uuid AS simulation_run_id,\n  (SELECT COUNT(*) FROM projection.risk_assessment_log WHERE "SimulationRunId" = :'simulation_run_id'::uuid) AS risk_assessments,\n  (SELECT COUNT(*) FROM projection.area_risk_snapshot_log WHERE "SimulationRunId" = :'simulation_run_id'::uuid) AS area_snapshots,\n  (SELECT COUNT(*) FROM projection.daily_cell_state WHERE "SimulationRunId" = :'simulation_run_id'::uuid) AS daily_cell_states;""",
            "expected_access_pattern": "Três index scans independentes por SimulationRunId; útil como gate de integridade, não como endpoint de alta frequência.",
        },
    ]
    desired_prefixes = {
        "DBQ-01": ["control.areas:ConfigurationVersionId+Code"],
        "DBQ-02": ["control.simulation_runs:AreaId+CreatedAt"],
        "DBQ-03": ["pipeline.event_inbox:Status"],
        "DBQ-04": ["pipeline.event_inbox:Status+NextAttemptNotBefore+ReceivedAt"],
        "DBQ-05": ["pipeline.processing_attempts:Stage+Outcome+ErrorCode"],
        "DBQ-06": ["projection.area_operational_state:AreaId"],
        "DBQ-07": ["projection.risk_assessment_log:SimulationRunId+Timestamp"],
        "DBQ-08": ["projection.area_risk_snapshot_log:SimulationRunId+SnapshotTimestamp"],
        "DBQ-09": ["projection.daily_cell_state:SimulationRunId+LogicalDate+GridCellId+SensorId"],
        "DBQ-10": ["projection.alert_state:AreaId+ResolvedAt+UpdatedAt"],
        "DBQ-11": ["control.runtime_orchestrator_executions:state+updated_at"],
        "DBQ-12": [
            "projection.risk_assessment_log:SimulationRunId",
            "projection.area_risk_snapshot_log:SimulationRunId",
            "projection.daily_cell_state:SimulationRunId",
        ],
    }
    rows: list[dict[str, Any]] = []
    for definition in definitions:
        missing = [table for table in definition["tables"] if table not in available]
        rows.append(
            {
                **definition,
                "tables": definition["tables"],
                "recommended_index_prefixes": desired_prefixes.get(definition["id"], []),
                "all_tables_present_in_static_model": not missing,
                "missing_tables": missing,
                "execution_status": "PREPARED_NOT_EXECUTED",
                "evidence_class": "QUERY_PLAN_CANDIDATE",
            }
        )
    return rows


def assess_query_index_coverage(catalog: Sequence[dict[str, Any]], model: dict[str, Any]) -> list[dict[str, Any]]:
    indexes_by_table: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for index in model["indexes"]:
        indexes_by_table[index["qualified_table"]].append(index)
    rows: list[dict[str, Any]] = []
    for query in catalog:
        for recommendation in query.get("recommended_index_prefixes", []):
            table, columns_text = recommendation.split(":", 1)
            desired = columns_text.split("+") if columns_text else []
            candidates = indexes_by_table.get(table, [])
            exact = [idx for idx in candidates if idx["columns"][: len(desired)] == desired]
            partial = [
                idx
                for idx in candidates
                if desired and idx["columns"] and idx["columns"][0] == desired[0] and idx not in exact
            ]
            if exact:
                status = "DECLARED_PREFIX_MATCH"
                matched = [idx["index"] for idx in exact]
            elif partial:
                status = "PARTIAL_LEFT_PREFIX_ONLY"
                matched = [idx["index"] for idx in partial]
            else:
                status = "NO_DECLARED_LEFT_PREFIX_MATCH"
                matched = []
            rows.append(
                {
                    "query_id": query["id"],
                    "query_title": query["title"],
                    "table": table,
                    "recommended_prefix": desired,
                    "static_coverage_status": status,
                    "matching_declared_indexes": matched,
                    "interpretation": "Static declaration check only; PostgreSQL planner usage requires EXPLAIN on representative data.",
                    "evidence_class": "STATIC_INDEX_COVERAGE_CANDIDATE",
                }
            )
    return rows


def collect_static_findings(model: dict[str, Any], coverage: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    columns_by_table: dict[str, set[str]] = defaultdict(set)
    for row in model["columns"]:
        columns_by_table[row["qualified_table"]].add(row["column"])
    fks = model["foreign_keys"]
    findings: list[dict[str, Any]] = []

    raw_tables = [
        row["qualified_table"] for row in model["tables"] if row["declaration_kind"] == "raw_sql_create_table"
    ]
    if raw_tables:
        findings.append(
            {
                "id": "DBS-001",
                "severity": "MEDIUM",
                "class": "MODEL_SNAPSHOT_DRIFT",
                "finding": "Existem tabelas efetivas declaradas por SQL em bruto que ainda não aparecem no ModelSnapshot EF Core.",
                "evidence": raw_tables,
                "impact": "Ferramentas que leem apenas o snapshot podem omitir objetos atuais do schema.",
                "action": "Manter o coletor híbrido e decidir se a tabela deve ser modelada no DbContext ou explicitamente documentada como externa ao modelo EF.",
            }
        )

    user_role_cols = columns_by_table.get("user_base.user_roles", set())
    user_fks = [
        row
        for row in fks
        if row["qualified_table"] == "user_base.user_roles" and row["referenced_qualified_table"] == "user_base.users"
    ]
    if {"UserId", "UserId1"}.issubset(user_role_cols) and len(user_fks) >= 2:
        findings.append(
            {
                "id": "DBS-002",
                "severity": "HIGH",
                "class": "CANDIDATE_SHADOW_FOREIGN_KEY",
                "finding": "user_base.user_roles declara UserId e UserId1, ambos com relações para user_base.users.",
                "evidence": [f"{row['columns']} -> {row['referenced_qualified_table']}" for row in user_fks],
                "impact": "Pode representar uma shadow FK acidental, duplicação de relação ou semântica de autorização ambígua.",
                "action": "Rever o mapeamento UserRoleRecord/UserRecord, confirmar o contrato pretendido e criar migração corretiva apenas após teste de compatibilidade dos dados.",
            }
        )

    runtime_columns = columns_by_table.get("control.runtime_orchestrator_executions", set())
    if runtime_columns and any("_" in col for col in runtime_columns):
        findings.append(
            {
                "id": "DBS-003",
                "severity": "LOW",
                "class": "NAMING_CONVENTION_DIVERGENCE",
                "finding": "A tabela runtime_orchestrator_executions usa nomes snake_case, enquanto o modelo EF existente usa predominantemente colunas PascalCase com quoting.",
                "evidence": sorted(runtime_columns),
                "impact": "Aumenta o cuidado necessário em SQL manual, exportação, tooling e documentação.",
                "action": "Preservar quoting e convenção explicitamente; não renomear sem plano de migração e compatibilidade.",
            }
        )

    uncovered = [row for row in coverage if row["static_coverage_status"] != "DECLARED_PREFIX_MATCH"]
    if uncovered:
        findings.append(
            {
                "id": "DBS-004",
                "severity": "MEDIUM",
                "class": "QUERY_INDEX_COVERAGE_CANDIDATES",
                "finding": "Algumas queries críticas não têm correspondência estática completa com o prefixo de índice recomendado.",
                "evidence": [f"{row['query_id']}:{row['table']}={row['static_coverage_status']}" for row in uncovered],
                "impact": "Podem surgir sorts, scans ou amplificação quando o volume crescer; não é prova de má performance atual.",
                "action": "Executar EXPLAIN (ANALYZE, BUFFERS) com dados representativos antes de propor novos índices.",
            }
        )

    findings.append(
        {
            "id": "DBS-005",
            "severity": "POSITIVE",
            "class": "PRIMARY_KEY_COVERAGE",
            "finding": "Todas as tabelas reconstruídas apresentam uma primary key declarada.",
            "evidence": f"{len(model['primary_keys'])}/{len(model['tables'])} tabelas",
            "impact": "Facilita identidade, referências e auditabilidade estrutural.",
            "action": "Preservar este gate em recolhas futuras.",
        }
    )
    return findings


def render_query_pack(catalog: Sequence[dict[str, Any]]) -> str:
    lines = [
        "-- NatureProtector Phase 3 — critical query and EXPLAIN template pack",
        "-- This file is generated from the repository snapshot and is not execution evidence.",
        "-- Replace psql variables before running against an isolated evidence database.",
        "\\set area_id '00000000-0000-0000-0000-000000000000'",
        "\\set simulation_run_id '00000000-0000-0000-0000-000000000000'",
        "\\set run_b '00000000-0000-0000-0000-000000000000'",
        "\\set run_c '00000000-0000-0000-0000-000000000000'",
        "\\set orchestrator_state 'running'",
        "",
        "-- Recommended session settings for bounded evidence collection:",
        "SET statement_timeout = '60s';",
        "SET lock_timeout = '5s';",
        "SET idle_in_transaction_session_timeout = '60s';",
        "",
    ]
    for row in catalog:
        lines += [
            f"-- {row['id']} — {row['title']}",
            f"-- Purpose: {row['purpose']}",
            f"-- Expected access pattern: {row['expected_access_pattern']}",
            "EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, FORMAT JSON)",
            row["sql"],
            "",
        ]
    return "\n".join(lines) + "\n"


def dot_node_id(schema: str, table: str) -> str:
    return re.sub(r"[^A-Za-z0-9_]", "_", f"{schema}_{table}")


def dot_escape(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"').replace("<", "&lt;").replace(">", "&gt;")


def render_full_dot(model: dict[str, Any]) -> str:
    pk_by_table = {(r["schema"], r["table"]): set(r["columns"]) for r in model["primary_keys"]}
    fk_cols = defaultdict(set)
    for r in model["foreign_keys"]:
        fk_cols[(r["schema"], r["table"])].update(r["columns"])
    cols_by_table = defaultdict(list)
    for col in model["columns"]:
        cols_by_table[(col["schema"], col["table"])].append(col)

    lines = [
        "digraph NatureProtectorDatabase {",
        '  graph [rankdir=LR, bgcolor="white", pad=0.2, nodesep=0.45, ranksep=0.8, fontname="Arial"];',
        '  node [shape=plain, fontname="Arial"];',
        '  edge [fontname="Arial", fontsize=8, color="#555555", arrowsize=0.7];',
    ]
    for schema in [row["schema"] for row in model["schemas"]]:
        lines.append(f"  subgraph cluster_{dot_node_id(schema, 'schema')} {{")
        lines.append(f'    label="{dot_escape(schema)}"; style="rounded"; color="#AAAAAA";')
        for table in [r for r in model["tables"] if r["schema"] == schema]:
            key = (schema, table["table"])
            node = dot_node_id(*key)
            rows = []
            for col in cols_by_table[key]:
                marker = (
                    "PK"
                    if col["column"] in pk_by_table.get(key, set())
                    else ("FK" if col["column"] in fk_cols.get(key, set()) else "")
                )
                null_text = "" if not col["nullable"] else "?"
                display_marker = marker or "·"
                rows.append(
                    f'<TR><TD ALIGN="LEFT"><FONT POINT-SIZE="8">{display_marker}</FONT></TD><TD ALIGN="LEFT">{dot_escape(col["column"])}{null_text}</TD><TD ALIGN="LEFT"><FONT POINT-SIZE="8">{dot_escape(col["sql_type"] or "unknown")}</FONT></TD></TR>'
                )
            label = (
                '<<TABLE BORDER="0" CELLBORDER="1" CELLSPACING="0" CELLPADDING="3"><TR><TD COLSPAN="3"><B>'
                + dot_escape(f"{schema}.{table['table']}")
                + "</B></TD></TR>"
                + "".join(rows)
                + "</TABLE>>"
            )
            lines.append(f"    {node} [label={label}];")
        lines.append("  }")
    for fk in model["foreign_keys"]:
        source = dot_node_id(fk["schema"], fk["table"])
        target = dot_node_id(fk["referenced_schema"], fk["referenced_table"])
        label = ",".join(fk["columns"])
        lines.append(f'  {source} -> {target} [label="{dot_escape(label)}"];')
    lines.append("}")
    return "\n".join(lines) + "\n"


def render_simplified_dot(model: dict[str, Any]) -> str:
    selected_tables = {
        "control.configuration_versions",
        "control.areas",
        "control.grid_cells",
        "control.sensor_nodes",
        "control.simulation_runs",
        "control.runtime_orchestrator_executions",
        "pipeline.event_inbox",
        "pipeline.processing_attempts",
        "pipeline.quarantined_events",
        "projection.accepted_reading_log",
        "projection.risk_assessment_log",
        "projection.daily_cell_state",
        "projection.area_operational_state",
        "projection.alert_state",
        "user_base.users",
        "user_base.roles",
        "user_base.user_roles",
    }
    important_names = {
        "Id",
        "AreaId",
        "GridCellId",
        "SensorId",
        "SimulationRunId",
        "EventId",
        "InboxEventId",
        "ConfigurationVersionId",
        "Status",
        "EventTime",
        "ReceivedAt",
        "Timestamp",
        "SnapshotTimestamp",
        "UpdatedAt",
        "LogicalDate",
        "RiskScore",
        "AggregateRiskScore",
        "Score100",
        "execution_id",
        "state",
        "updated_at",
        "idempotency_key",
        "UserId",
        "RoleId",
        "UserId1",
    }
    pk_by_table = {(r["schema"], r["table"]): set(r["columns"]) for r in model["primary_keys"]}
    fk_cols = defaultdict(set)
    for r in model["foreign_keys"]:
        fk_cols[(r["schema"], r["table"])].update(r["columns"])
    cols_by_table = defaultdict(list)
    for col in model["columns"]:
        key = (col["schema"], col["table"])
        if col["qualified_table"] not in selected_tables:
            continue
        if (
            col["column"] in important_names
            or col["column"] in pk_by_table.get(key, set())
            or col["column"] in fk_cols.get(key, set())
        ):
            cols_by_table[key].append(col)

    lines = [
        "digraph NatureProtectorDatabaseSimplified {",
        '  graph [rankdir=LR, bgcolor="white", pad=0.2, nodesep=0.55, ranksep=1.0, fontname="Arial", splines=polyline, ratio=compress];',
        '  node [shape=plain, fontname="Arial"];',
        '  edge [fontname="Arial", fontsize=8, color="#666666", arrowsize=0.7];',
    ]
    for schema in [row["schema"] for row in model["schemas"]]:
        schema_tables = [
            r for r in model["tables"] if r["schema"] == schema and r["qualified_table"] in selected_tables
        ]
        omitted = sum(
            1 for r in model["tables"] if r["schema"] == schema and r["qualified_table"] not in selected_tables
        )
        lines.append(f"  subgraph cluster_{dot_node_id(schema, 'schema')} {{")
        label = f"{schema} — núcleo selecionado" + (f" (+{omitted} tabelas no ERD completo)" if omitted else "")
        lines.append(f'    label="{dot_escape(label)}"; style="rounded"; color="#AAAAAA";')
        for table in schema_tables:
            key = (schema, table["table"])
            node = dot_node_id(*key)
            rows = []
            selected = cols_by_table[key]
            for col in selected[:9]:
                marker = (
                    "PK"
                    if col["column"] in pk_by_table.get(key, set())
                    else ("FK" if col["column"] in fk_cols.get(key, set()) else "·")
                )
                rows.append(
                    f'<TR><TD ALIGN="LEFT"><FONT POINT-SIZE="8">{marker}</FONT></TD><TD ALIGN="LEFT">{dot_escape(col["column"])}</TD></TR>'
                )
            if len(selected) > 9:
                rows.append(
                    f'<TR><TD>·</TD><TD ALIGN="LEFT"><FONT POINT-SIZE="8">+{len(selected) - 9} campos chave</FONT></TD></TR>'
                )
            label_html = (
                '<<TABLE BORDER="0" CELLBORDER="1" CELLSPACING="0" CELLPADDING="4"><TR><TD COLSPAN="2"><B>'
                + dot_escape(f"{schema}.{table['table']}")
                + "</B></TD></TR>"
                + "".join(rows)
                + "</TABLE>>"
            )
            lines.append(f"    {node} [label={label_html}];")
        lines.append("  }")
    for fk in model["foreign_keys"]:
        if fk["qualified_table"] not in selected_tables or fk["referenced_qualified_table"] not in selected_tables:
            continue
        source = dot_node_id(fk["schema"], fk["table"])
        target = dot_node_id(fk["referenced_schema"], fk["referenced_table"])
        lines.append(f"  {source} -> {target};")
    lines.append("}")
    return "\n".join(lines) + "\n"


def render_mermaid(model: dict[str, Any], simplified: bool = False) -> str:
    pk_by_table = {(r["schema"], r["table"]): set(r["columns"]) for r in model["primary_keys"]}
    fk_cols = defaultdict(set)
    for r in model["foreign_keys"]:
        fk_cols[(r["schema"], r["table"])].update(r["columns"])
    cols_by_table = defaultdict(list)
    for c in model["columns"]:
        cols_by_table[(c["schema"], c["table"])].append(c)
    lines = ["erDiagram"]
    for table in model["tables"]:
        key = (table["schema"], table["table"])
        name = dot_node_id(*key)
        lines.append(f"  {name} {{")
        selected = cols_by_table[key]
        if simplified:
            selected = [
                c
                for c in selected
                if c["column"] in pk_by_table.get(key, set())
                or c["column"] in fk_cols.get(key, set())
                or c["column"]
                in {
                    "SimulationRunId",
                    "EventId",
                    "Status",
                    "Timestamp",
                    "UpdatedAt",
                    "RiskScore",
                    "AggregateRiskScore",
                    "execution_id",
                    "state",
                    "updated_at",
                }
            ]
        for col in selected[: 12 if simplified else len(selected)]:
            sql_type = re.sub(r"[^A-Za-z0-9_]", "_", col["sql_type"] or "unknown")
            marker = (
                " PK"
                if col["column"] in pk_by_table.get(key, set())
                else (" FK" if col["column"] in fk_cols.get(key, set()) else "")
            )
            lines.append(f"    {sql_type} {re.sub(r'[^A-Za-z0-9_]', '_', col['column'])}{marker}")
        lines.append("  }")
    for fk in model["foreign_keys"]:
        source = dot_node_id(fk["schema"], fk["table"])
        target = dot_node_id(fk["referenced_schema"], fk["referenced_table"])
        label = "_".join(fk["columns"])
        lines.append(f'  {target} ||--o{{ {source} : "{label}"')
    return "\n".join(lines) + "\n"


def render_diagrams(output: Path, model: dict[str, Any]) -> list[dict[str, Any]]:
    diagrams = output / "diagrams"
    diagrams.mkdir(parents=True, exist_ok=True)
    specs = [
        ("erd-full", render_full_dot(model)),
        ("erd-report-simplified", render_simplified_dot(model)),
    ]
    status_rows: list[dict[str, Any]] = []
    dot_executable = shutil.which("dot")
    for stem, dot_text in specs:
        dot_path = diagrams / f"{stem}.dot"
        dot_path.write_text(dot_text, encoding="utf-8")
        mmd_path = diagrams / f"{stem}.mmd"
        mmd_path.write_text(render_mermaid(model, simplified=(stem.endswith("simplified"))), encoding="utf-8")
        row = {
            "diagram": stem,
            "dot_source": safe_rel(dot_path, output),
            "mermaid_source": safe_rel(mmd_path, output),
            "graphviz_available": bool(dot_executable),
            "svg_status": "BLOCKED_TOOL_UNAVAILABLE",
            "png_status": "BLOCKED_TOOL_UNAVAILABLE",
        }
        if dot_executable:
            for fmt in ("svg", "png"):
                target = diagrams / f"{stem}.{fmt}"
                completed = subprocess.run(
                    [dot_executable, f"-T{fmt}", str(dot_path), "-o", str(target)],
                    capture_output=True,
                    text=True,
                    check=False,
                )
                row[f"{fmt}_status"] = (
                    "PASS" if completed.returncode == 0 and target.exists() and target.stat().st_size > 0 else "FAIL"
                )
                row[f"{fmt}_file"] = safe_rel(target, output) if target.exists() else ""
                row[f"{fmt}_stderr"] = (completed.stderr or "").strip()
        status_rows.append(row)
    write_json(output / "diagram-status.json", status_rows)
    write_csv(output / "diagram-status.csv", status_rows)
    return status_rows


def redact_dsn(dsn: str | None) -> str | None:
    if not dsn:
        return None
    try:
        parts = urlsplit(dsn)
        if parts.scheme and parts.hostname:
            user = parts.username or ""
            host = parts.hostname
            if ":" in host and not host.startswith("["):
                host = f"[{host}]"
            auth = user + ("@" if user else "")
            port = f":{parts.port}" if parts.port else ""
            return urlunsplit((parts.scheme, f"{auth}{host}{port}", parts.path, parts.query, parts.fragment))
    except ValueError:
        pass
    return "provided_non_url_dsn_redacted"


def collect_live_database(output: Path, dsn: str | None, require_live: bool) -> dict[str, Any]:
    live_dir = output / "live"
    live_dir.mkdir(parents=True, exist_ok=True)
    result: dict[str, Any] = {
        "requested": bool(dsn) or require_live,
        "dsn_redacted": redact_dsn(dsn),
        "status": "NOT_REQUESTED",
        "reason": "No DSN was supplied; static model evidence remains valid but live PostgreSQL facts were not collected.",
        "evidence_class": LIVE_EVIDENCE_CLASS,
    }
    if not dsn:
        if require_live:
            result["status"] = "BLOCKED_MISSING_DSN"
            result["reason"] = "Live mode was required but no DSN was supplied."
        write_json(live_dir / "live-status.json", result)
        return result
    try:
        import psycopg  # type: ignore
        from psycopg.rows import dict_row  # type: ignore
    except ImportError:
        result["status"] = "BLOCKED_PSYCOPG_UNAVAILABLE"
        result["reason"] = "psycopg v3 is not installed in this Python environment."
        write_json(live_dir / "live-status.json", result)
        return result

    queries = {
        "database-context": """
            SELECT current_database() AS database_name, current_user AS current_user,
                   version() AS postgres_version, current_setting('TimeZone') AS timezone;
        """,
        "tables": """
            SELECT t.table_schema AS schema, t.table_name AS table
            FROM information_schema.tables t
            WHERE t.table_schema IN ('control','pipeline','projection','user_base')
              AND t.table_type = 'BASE TABLE'
            ORDER BY t.table_schema, t.table_name;
        """,
        "columns": """
            SELECT table_schema AS schema, table_name AS table, ordinal_position,
                   column_name AS column, data_type AS sql_type, is_nullable,
                   column_default AS default_expression
            FROM information_schema.columns
            WHERE table_schema IN ('control','pipeline','projection','user_base')
            ORDER BY table_schema, table_name, ordinal_position;
        """,
        "primary-keys": """
            SELECT n.nspname AS schema, c.relname AS table, con.conname AS constraint,
                   array_agg(a.attname ORDER BY u.ordinality) AS columns
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS u(attnum, ordinality)
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = u.attnum
            WHERE con.contype = 'p' AND n.nspname IN ('control','pipeline','projection','user_base')
            GROUP BY n.nspname, c.relname, con.conname
            ORDER BY n.nspname, c.relname;
        """,
        "foreign-keys": """
            SELECT ns.nspname AS schema, src.relname AS table, con.conname AS constraint,
                   array_agg(sa.attname ORDER BY u.ordinality) AS columns,
                   nt.nspname AS referenced_schema, tgt.relname AS referenced_table,
                   array_agg(ta.attname ORDER BY u.ordinality) AS referenced_columns
            FROM pg_constraint con
            JOIN pg_class src ON src.oid = con.conrelid
            JOIN pg_namespace ns ON ns.oid = src.relnamespace
            JOIN pg_class tgt ON tgt.oid = con.confrelid
            JOIN pg_namespace nt ON nt.oid = tgt.relnamespace
            CROSS JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS u(src_attnum, tgt_attnum, ordinality)
            JOIN pg_attribute sa ON sa.attrelid = src.oid AND sa.attnum = u.src_attnum
            JOIN pg_attribute ta ON ta.attrelid = tgt.oid AND ta.attnum = u.tgt_attnum
            WHERE con.contype = 'f' AND ns.nspname IN ('control','pipeline','projection','user_base')
            GROUP BY ns.nspname, src.relname, con.conname, nt.nspname, tgt.relname
            ORDER BY ns.nspname, src.relname, con.conname;
        """,
        "indexes": """
            SELECT schemaname AS schema, tablename AS table, indexname AS index,
                   indexdef AS definition
            FROM pg_indexes
            WHERE schemaname IN ('control','pipeline','projection','user_base')
            ORDER BY schemaname, tablename, indexname;
        """,
        "table-statistics": """
            SELECT n.nspname AS schema, c.relname AS table,
                   c.reltuples::bigint AS estimated_rows,
                   pg_relation_size(c.oid) AS table_bytes,
                   pg_indexes_size(c.oid) AS indexes_bytes,
                   pg_total_relation_size(c.oid) AS total_bytes,
                   st.n_live_tup AS stats_live_rows, st.n_dead_tup AS stats_dead_rows,
                   st.seq_scan, st.idx_scan, st.last_analyze, st.last_autoanalyze
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_stat_user_tables st ON st.relid = c.oid
            WHERE n.nspname IN ('control','pipeline','projection','user_base')
              AND c.relkind IN ('r','p')
            ORDER BY pg_total_relation_size(c.oid) DESC, n.nspname, c.relname;
        """,
        "migration-history": """
            SELECT "MigrationId", "ProductVersion"
            FROM "__EFMigrationsHistory"
            ORDER BY "MigrationId";
        """,
    }
    started = time.monotonic()
    try:
        with psycopg.connect(
            dsn,
            connect_timeout=15,
            row_factory=dict_row,
            options="-c default_transaction_read_only=on -c statement_timeout=60000",
        ) as connection:
            for name, query in queries.items():
                try:
                    with connection.cursor() as cursor:
                        cursor.execute(query)
                        rows = [dict(row) for row in cursor.fetchall()]
                    write_json(live_dir / f"{name}.json", rows)
                    if rows:
                        write_csv(live_dir / f"{name}.csv", rows)
                except Exception as exc:  # keep partial evidence explicit
                    write_json(
                        live_dir / f"{name}-error.json",
                        {"query": name, "error_type": type(exc).__name__, "error": str(exc)},
                    )
                    connection.rollback()
        result["status"] = "PASS"
        result["reason"] = "Read-only PostgreSQL catalogue and table statistics were collected."
    except Exception as exc:
        result["status"] = "FAIL"
        result["reason"] = f"PostgreSQL connection or catalogue collection failed: {type(exc).__name__}: {exc}"
    result["duration_seconds"] = round(time.monotonic() - started, 3)
    write_json(live_dir / "live-status.json", result)
    return result


def generate_hashes(output: Path) -> tuple[Path, int]:
    manifest = output / "SHA256SUMS.txt"
    files = sorted(path for path in output.rglob("*") if path.is_file() and path != manifest)
    lines = [f"{sha256_file(path)}  {safe_rel(path, output)}" for path in files]
    manifest.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return manifest, len(files)


def render_summary(summary: dict[str, Any], model: dict[str, Any], live: dict[str, Any]) -> str:
    counts = summary["counts"]
    schema_rows = [
        (
            s["schema"],
            s["table_count"],
            s["column_count"],
            s["outgoing_foreign_key_count"],
            s["index_count_including_unique_constraints"],
            s["role"],
        )
        for s in model["schemas"]
    ]
    lines = [
        "# NatureProtector — Fase 3: modelo SQL físico e evidência PostgreSQL",
        "",
        f"- Gerado em UTC: `{summary['generated_at_utc']}`",
        f"- Baseline: `{summary['baseline_id']}`",
        f"- Run da Fase 3: `{summary['run_id']}`",
        f"- Estado estático: **{summary['static_status']}**",
        f"- Estado live PostgreSQL: **{live['status']}**",
        "",
        "## Contagens do modelo efetivo reconstruído",
        "",
        markdown_table(
            ["Métrica", "Valor"],
            [
                ("Schemas", counts["schemas"]),
                ("Tabelas", counts["tables"]),
                ("Colunas", counts["columns"]),
                ("Primary keys", counts["primary_keys"]),
                ("Foreign keys", counts["foreign_keys"]),
                ("Índices e constraints únicas", counts["indexes"]),
                ("Migrações", counts["migrations"]),
                ("Queries críticas preparadas", counts["critical_queries"]),
                ("Avaliações estáticas query–índice", counts["query_index_assessments"]),
                ("Findings estáticos", counts["static_findings"]),
            ],
        ),
        "",
        "## Distribuição por schema",
        "",
        markdown_table(["Schema", "Tabelas", "Colunas", "FK de saída", "Índices", "Papel"], schema_rows),
        "",
        "## Interpretação da evidência",
        "",
        "O modelo estático foi reconstruído do `ModelSnapshot` EF Core atual e complementado pelas migrações SQL em bruto ainda não refletidas no snapshot. Isto permite gerar um ERD atual e inventários determinísticos sem afirmar que as migrações estão aplicadas numa instância PostgreSQL.",
        "",
        f"A recolha live ficou em `{live['status']}`: {live['reason']}",
        "",
        "## Artefactos principais",
        "",
        "- `static/schema-model.json` — modelo consolidado legível por máquina.",
        "- `static/schemas.csv`, `tables.csv`, `columns.csv`, `primary-keys.csv`, `foreign-keys.csv`, `indexes.csv`.",
        "- `diagrams/erd-full.svg` e `diagrams/erd-report-simplified.svg` quando Graphviz está disponível.",
        "- `queries/critical-query-catalog.csv`, `query-index-coverage.csv` e `critical-queries-explain.sql`.",
        "- `static/static-findings.csv` — findings estruturais e respetivo limite de interpretação.",
        "- `live/` — catálogo e estatísticas atuais apenas quando foi fornecido um DSN.",
        "- `report-ready/database-summary.csv` — tabela compacta para o relatório.",
        "- `SHA256SUMS.txt` — integridade dos outputs.",
        "",
        "## Limites",
        "",
        "- O ERD estático descreve a intenção efetiva do código e das migrações, não prova o estado de uma base em execução.",
        "- Cardinalidades, tamanhos, row counts, utilização de índices e planos reais exigem a recolha live.",
        "- As queries críticas estão preparadas, mas não foram executadas nem cronometradas nesta fase.",
        "- `EXPLAIN ANALYZE` deve ser corrido numa base isolada e com parâmetros representativos; essa execução pertence à avaliação de desempenho.",
        "",
    ]
    return "\n".join(lines)


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd(), help="Repository root.")
    parser.add_argument("--baseline-id", required=True, help="Phase 0 baseline identifier.")
    parser.add_argument("--run-id", default=compact_utc_now(), help="Phase 3 run identifier.")
    parser.add_argument("--output", type=Path, help="Output directory; defaults under artifacts/report-evidence.")
    parser.add_argument(
        "--dsn",
        default=os.getenv("NATUREPROTECTOR_POSTGRES_DSN") or os.getenv("DATABASE_URL"),
        help="Optional PostgreSQL DSN. Never written with password.",
    )
    parser.add_argument(
        "--require-live", action="store_true", help="Return non-zero unless live PostgreSQL inventory passes."
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    repo = args.repo.resolve()
    if not (repo / "NatureProtector.sln").exists():
        print(f"ERROR: repository root is invalid: {repo}", file=sys.stderr)
        return 2
    output = (
        args.output.resolve()
        if args.output
        else repo / "artifacts" / "report-evidence" / args.baseline_id / "03-database" / args.run_id
    )
    output.mkdir(parents=True, exist_ok=True)

    environment = {
        "generated_at_utc": utc_now(),
        "collector_version": SCRIPT_VERSION,
        "python_version": platform.python_version(),
        "platform": platform.platform(),
        "repository_path": str(repo),
        "baseline_id": args.baseline_id,
        "run_id": args.run_id,
        "graphviz_dot": shutil.which("dot"),
        "psql": shutil.which("psql"),
        "dsn_supplied": bool(args.dsn),
        "dsn_redacted": redact_dsn(args.dsn),
        "collector_path": safe_rel(Path(__file__), repo),
        "collector_sha256": sha256_file(Path(__file__)),
        "verifier_path": "scripts/evidence/verify-database-architecture-evidence.py",
        "verifier_sha256": (
            sha256_file(repo / "scripts/evidence/verify-database-architecture-evidence.py")
            if (repo / "scripts/evidence/verify-database-architecture-evidence.py").exists()
            else None
        ),
    }
    write_json(output / "environment.json", environment)

    try:
        model = collect_static_model(repo)
    except Exception as exc:
        write_json(output / "phase3-error.json", {"type": type(exc).__name__, "error": str(exc)})
        print(f"PHASE_3_STATIC_STATUS=FAIL\nERROR={type(exc).__name__}: {exc}", file=sys.stderr)
        return 1

    static_dir = output / "static"
    static_dir.mkdir(parents=True, exist_ok=True)
    write_json(static_dir / "schema-model.json", model)
    for stem, key in [
        ("schemas", "schemas"),
        ("tables", "tables"),
        ("columns", "columns"),
        ("primary-keys", "primary_keys"),
        ("foreign-keys", "foreign_keys"),
        ("indexes", "indexes"),
        ("migrations", "migrations"),
    ]:
        write_json(static_dir / f"{stem}.json", model[key])
        write_csv(static_dir / f"{stem}.csv", model[key])

    query_catalog = build_critical_query_catalog(model)
    query_index_coverage = assess_query_index_coverage(query_catalog, model)
    static_findings = collect_static_findings(model, query_index_coverage)
    query_dir = output / "queries"
    query_dir.mkdir(parents=True, exist_ok=True)
    write_json(query_dir / "critical-query-catalog.json", query_catalog)
    write_csv(query_dir / "critical-query-catalog.csv", query_catalog)
    write_json(query_dir / "query-index-coverage.json", query_index_coverage)
    write_csv(query_dir / "query-index-coverage.csv", query_index_coverage)
    (query_dir / "critical-queries-explain.sql").write_text(render_query_pack(query_catalog), encoding="utf-8")
    write_json(static_dir / "static-findings.json", static_findings)
    write_csv(static_dir / "static-findings.csv", static_findings)

    diagram_status = render_diagrams(output, model)
    live = collect_live_database(output, args.dsn, args.require_live)

    report_dir = output / "report-ready"
    report_dir.mkdir(parents=True, exist_ok=True)
    report_rows = [
        {
            "schema": s["schema"],
            "role": s["role"],
            "tables": s["table_count"],
            "columns": s["column_count"],
            "primary_keys": s["primary_key_count"],
            "foreign_keys_outgoing": s["outgoing_foreign_key_count"],
            "indexes_including_unique_constraints": s["index_count_including_unique_constraints"],
        }
        for s in model["schemas"]
    ]
    write_csv(report_dir / "database-summary.csv", report_rows)
    write_json(report_dir / "database-summary.json", report_rows)
    (report_dir / "database-summary.md").write_text(
        markdown_table(
            ["Schema", "Papel", "Tabelas", "Colunas", "PK", "FK", "Índices"],
            [
                (
                    r["schema"],
                    r["role"],
                    r["tables"],
                    r["columns"],
                    r["primary_keys"],
                    r["foreign_keys_outgoing"],
                    r["indexes_including_unique_constraints"],
                )
                for r in report_rows
            ],
        )
        + "\n",
        encoding="utf-8",
    )

    counts = {
        "schemas": len(model["schemas"]),
        "tables": len(model["tables"]),
        "columns": len(model["columns"]),
        "primary_keys": len(model["primary_keys"]),
        "foreign_keys": len(model["foreign_keys"]),
        "indexes": len(model["indexes"]),
        "migrations": len(model["migrations"]),
        "critical_queries": len(query_catalog),
        "query_index_assessments": len(query_index_coverage),
        "static_findings": len(static_findings),
    }
    diagram_pass = (
        all(row["svg_status"] == "PASS" and row["png_status"] == "PASS" for row in diagram_status)
        if shutil.which("dot")
        else True
    )
    static_status = (
        "PASS"
        if counts["tables"] > 0 and counts["columns"] > 0 and counts["primary_keys"] > 0 and diagram_pass
        else "FAIL"
    )
    phase_status = (
        "PASS"
        if static_status == "PASS" and (not args.require_live or live["status"] == "PASS")
        else ("PARTIAL_PASS_LIVE_BLOCKED" if static_status == "PASS" else "FAIL")
    )
    summary = {
        "schema_version": "1.0",
        "collector_version": SCRIPT_VERSION,
        "generated_at_utc": environment["generated_at_utc"],
        "baseline_id": args.baseline_id,
        "run_id": args.run_id,
        "static_status": static_status,
        "live_status": live["status"],
        "phase_status": phase_status,
        "counts": counts,
        "snapshot": model["snapshot"],
        "raw_sql_migrations_merged": model["raw_sql_migrations_merged"],
        "diagram_status": diagram_status,
        "static_findings": static_findings,
        "query_index_coverage": query_index_coverage,
        "claims": {
            "allowed": [
                "The current repository snapshot declares the exported schemas, tables, columns, keys and indexes.",
                "The ERD was generated from the EF model snapshot plus raw-SQL migrations.",
            ],
            "not_allowed_without_live_evidence": [
                "The live PostgreSQL instance contains exactly these objects.",
                "The indexes are used efficiently or the critical queries meet a latency target.",
                "The row counts and table sizes are current.",
            ],
        },
    }
    write_json(output / "phase3-summary.json", summary)
    (output / "phase3-summary.md").write_text(render_summary(summary, model, live), encoding="utf-8")

    manifest, file_count = generate_hashes(output)
    latest = output.parent / "LATEST.txt"
    latest.write_text(str(output.resolve()) + "\n", encoding="utf-8")

    print(f"PHASE_3_STATUS={phase_status}")
    print(f"STATIC_MODEL_STATUS={static_status}")
    print(f"LIVE_DATABASE_STATUS={live['status']}")
    print(f"SCHEMAS={counts['schemas']}")
    print(f"TABLES={counts['tables']}")
    print(f"COLUMNS={counts['columns']}")
    print(f"PRIMARY_KEYS={counts['primary_keys']}")
    print(f"FOREIGN_KEYS={counts['foreign_keys']}")
    print(f"INDEXES={counts['indexes']}")
    print(f"CRITICAL_QUERIES={counts['critical_queries']}")
    print(f"QUERY_INDEX_ASSESSMENTS={counts['query_index_assessments']}")
    print(f"STATIC_FINDINGS={counts['static_findings']}")
    print(f"HASHED_FILE_COUNT={file_count}")
    print(f"EVIDENCE_ROOT={output}")
    print(f"SHA256_MANIFEST={manifest}")
    if static_status != "PASS":
        return 1
    if args.require_live and live["status"] != "PASS":
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
