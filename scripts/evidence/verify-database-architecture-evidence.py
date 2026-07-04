#!/usr/bin/env python3
"""Verify NatureProtector Phase 3 database architecture evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sys
from pathlib import Path
from typing import Any, Sequence

SCRIPT_VERSION = "1.0.0"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as stream:
        return list(csv.DictReader(stream))


def parse_manifest(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        digest, relative = line.split("  ", 1)
        result[relative] = digest
    return result


def fail(errors: list[str], message: str) -> None:
    errors.append(message)
    print(f"FAIL: {message}")


def verify(evidence: Path, require_live: bool = False) -> tuple[bool, dict[str, Any]]:
    errors: list[str] = []
    required = [
        "environment.json",
        "phase3-summary.json",
        "phase3-summary.md",
        "diagram-status.json",
        "static/schema-model.json",
        "static/schemas.csv",
        "static/tables.csv",
        "static/columns.csv",
        "static/primary-keys.csv",
        "static/foreign-keys.csv",
        "static/indexes.csv",
        "static/static-findings.json",
        "static/static-findings.csv",
        "queries/critical-query-catalog.json",
        "queries/critical-query-catalog.csv",
        "queries/query-index-coverage.json",
        "queries/query-index-coverage.csv",
        "queries/critical-queries-explain.sql",
        "diagrams/erd-full.dot",
        "diagrams/erd-full.mmd",
        "diagrams/erd-report-simplified.dot",
        "diagrams/erd-report-simplified.mmd",
        "report-ready/database-summary.csv",
        "SHA256SUMS.txt",
    ]
    for relative in required:
        path = evidence / relative
        if not path.is_file() or path.stat().st_size == 0:
            fail(errors, f"Missing or empty required file: {relative}")

    if errors:
        return False, {"errors": errors}

    summary = load_json(evidence / "phase3-summary.json")
    model = load_json(evidence / "static/schema-model.json")
    queries = load_json(evidence / "queries/critical-query-catalog.json")
    query_index_coverage = load_json(evidence / "queries/query-index-coverage.json")
    static_findings = load_json(evidence / "static/static-findings.json")
    diagram_status = load_json(evidence / "diagram-status.json")
    live_status = load_json(evidence / "live/live-status.json")

    if summary.get("static_status") != "PASS":
        fail(errors, f"Static status is not PASS: {summary.get('static_status')}")
    if require_live and live_status.get("status") != "PASS":
        fail(errors, f"Live database status is not PASS: {live_status.get('status')}")

    expected_counts = {
        "schemas": len(model.get("schemas", [])),
        "tables": len(model.get("tables", [])),
        "columns": len(model.get("columns", [])),
        "primary_keys": len(model.get("primary_keys", [])),
        "foreign_keys": len(model.get("foreign_keys", [])),
        "indexes": len(model.get("indexes", [])),
        "migrations": len(model.get("migrations", [])),
        "critical_queries": len(queries),
        "query_index_assessments": len(query_index_coverage),
        "static_findings": len(static_findings),
    }
    for key, expected in expected_counts.items():
        actual = summary.get("counts", {}).get(key)
        if actual != expected:
            fail(errors, f"Count mismatch for {key}: summary={actual}, model={expected}")

    table_set = {(row["schema"], row["table"]) for row in model["tables"]}
    column_set = {(row["schema"], row["table"], row["column"]) for row in model["columns"]}
    pk_table_set = {(row["schema"], row["table"]) for row in model["primary_keys"]}

    if len(pk_table_set) != len(table_set):
        missing = sorted(table_set - pk_table_set)
        fail(errors, f"Tables without a primary key declaration: {missing}")

    for row in model["primary_keys"]:
        for column in row["columns"]:
            if (row["schema"], row["table"], column) not in column_set:
                fail(errors, f"Primary-key column not found: {row['qualified_table']}.{column}")

    for row in model["foreign_keys"]:
        source = (row["schema"], row["table"])
        target = (row["referenced_schema"], row["referenced_table"])
        if source not in table_set:
            fail(errors, f"Foreign-key source table missing: {source}")
        if target not in table_set:
            fail(errors, f"Foreign-key target table missing: {target}")
        for column in row["columns"]:
            if (source[0], source[1], column) not in column_set:
                fail(errors, f"Foreign-key source column missing: {source}.{column}")
        for column in row["referenced_columns"]:
            if (target[0], target[1], column) not in column_set:
                fail(errors, f"Foreign-key target column missing: {target}.{column}")

    for row in model["indexes"]:
        key = (row["schema"], row["table"])
        if key not in table_set:
            fail(errors, f"Index table missing: {key}")
        for column in row["columns"]:
            # Expression indexes would require a different class; current model only declares columns.
            if (key[0], key[1], column) not in column_set:
                fail(errors, f"Index column missing: {key}.{column} ({row['index']})")

    finding_ids = [row.get("id") for row in static_findings]
    if len(finding_ids) != len(set(finding_ids)):
        fail(errors, "Static finding identifiers are not unique.")

    valid_coverage_statuses = {"DECLARED_PREFIX_MATCH", "PARTIAL_LEFT_PREFIX_ONLY", "NO_DECLARED_LEFT_PREFIX_MATCH"}
    query_ids = {row.get("id") for row in queries}
    for row in query_index_coverage:
        if row.get("query_id") not in query_ids:
            fail(errors, f"Index coverage references unknown query: {row.get('query_id')}")
        if row.get("static_coverage_status") not in valid_coverage_statuses:
            fail(errors, f"Invalid static coverage status: {row.get('static_coverage_status')}")

    for row in queries:
        if not row.get("all_tables_present_in_static_model"):
            fail(errors, f"Critical query {row.get('id')} references missing tables: {row.get('missing_tables')}")
        if row.get("execution_status") != "PREPARED_NOT_EXECUTED":
            fail(errors, f"Unexpected execution status for {row.get('id')}: {row.get('execution_status')}")

    dot_available = bool(load_json(evidence / "environment.json").get("graphviz_dot"))
    for row in diagram_status:
        for source_key in ("dot_source", "mermaid_source"):
            source_path = evidence / row[source_key]
            if not source_path.is_file() or source_path.stat().st_size == 0:
                fail(errors, f"Diagram source missing: {row[source_key]}")
        if dot_available:
            for fmt in ("svg", "png"):
                if row.get(f"{fmt}_status") != "PASS":
                    fail(errors, f"Graphviz output failed for {row['diagram']} {fmt}: {row.get(f'{fmt}_stderr')}")
                target = evidence / row.get(f"{fmt}_file", "")
                if not target.is_file() or target.stat().st_size == 0:
                    fail(errors, f"Graphviz output missing: {target}")

    # CSV/JSON pair cardinalities.
    for stem, key in [
        ("schemas", "schemas"),
        ("tables", "tables"),
        ("columns", "columns"),
        ("primary-keys", "primary_keys"),
        ("foreign-keys", "foreign_keys"),
        ("indexes", "indexes"),
        ("migrations", "migrations"),
    ]:
        rows = read_csv(evidence / "static" / f"{stem}.csv")
        if len(rows) != len(model[key]):
            fail(errors, f"CSV/JSON cardinality mismatch for {stem}: csv={len(rows)} json={len(model[key])}")

    manifest_path = evidence / "SHA256SUMS.txt"
    manifest = parse_manifest(manifest_path)
    actual_files = {
        path.relative_to(evidence).as_posix()
        for path in evidence.rglob("*")
        if path.is_file() and path != manifest_path
    }
    manifest_files = set(manifest)
    if actual_files != manifest_files:
        missing = sorted(actual_files - manifest_files)
        stale = sorted(manifest_files - actual_files)
        fail(errors, f"Hash manifest file set mismatch; missing_entries={missing}, stale_entries={stale}")
    for relative, expected in manifest.items():
        actual = sha256_file(evidence / relative)
        if actual != expected:
            fail(errors, f"SHA-256 mismatch for {relative}: expected={expected} actual={actual}")

    result = {
        "verifier_version": SCRIPT_VERSION,
        "evidence_root": str(evidence.resolve()),
        "status": "PASS" if not errors else "FAIL",
        "errors": errors,
        "counts": expected_counts,
        "live_status": live_status.get("status"),
        "hashed_file_count": len(manifest),
    }
    return not errors, result


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path, help="Phase 3 evidence directory.")
    parser.add_argument("--require-live", action="store_true", help="Require current live PostgreSQL evidence.")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    evidence = args.evidence.resolve()
    if not evidence.is_dir():
        print(f"PHASE_3_VERIFICATION=FAIL\nERROR=Evidence directory not found: {evidence}")
        return 2
    ok, result = verify(evidence, args.require_live)
    print(f"PHASE_3_VERIFICATION={result['status']}")
    if result.get("counts"):
        for key, value in result["counts"].items():
            print(f"VERIFIED_{key.upper()}={value}")
    print(f"VERIFIED_HASHED_FILE_COUNT={result.get('hashed_file_count', 0)}")
    print(f"LIVE_DATABASE_STATUS={result.get('live_status', 'UNKNOWN')}")
    if not ok:
        for error in result["errors"]:
            print(f"ERROR={error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
