#!/usr/bin/env python3
"""Verify a NatureProtector Phase 1 static inventory without changing it."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def csv_rows(path: Path) -> int:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return sum(1 for _ in csv.DictReader(stream))


def require(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.inventory_root.resolve()
    failures: list[str] = []

    summary_path = root / "inventory-summary.json"
    inventory_path = root / "inventory.json"
    sums_path = root / "SHA256SUMS.txt"
    for required in (summary_path, inventory_path, sums_path):
        require(required.is_file(), f"Missing required file: {required.name}", failures)
    if failures:
        for failure in failures:
            print(f"ERROR={failure}")
        print("PHASE_1_VERIFICATION=FAIL")
        return 1

    summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    inventory = json.loads(inventory_path.read_text(encoding="utf-8-sig"))
    counts = summary.get("counts", {})

    require(summary.get("baseline_id") == inventory.get("baseline_id"), "Baseline ID mismatch", failures)
    require(inventory.get("evidence_class") == "STATIC_REPOSITORY_INVENTORY", "Unexpected evidence class", failures)
    require(inventory.get("runtime_execution_performed") is False, "Runtime execution flag must be false", failures)

    count_checks = {
        "dotnet_projects": len(inventory.get("projects", [])),
        "api_endpoints": len(inventory.get("endpoints", [])),
        "event_types": len(inventory.get("events", [])),
        "telemetry_metrics": len(inventory.get("telemetry_metrics", [])),
        "telemetry_activities": len(inventory.get("telemetry_activities", [])),
        "migrations": len(inventory.get("migrations", [])),
        "database_schemas": len(inventory.get("database_schemas", [])),
        "database_tables": len(inventory.get("database_tables", [])),
        "database_columns": len(inventory.get("database_columns", [])),
        "database_indexes": len(inventory.get("database_indexes", [])),
        "workflows": len(inventory.get("workflows", [])),
        "compose_services": len(inventory.get("compose_services", [])),
    }
    for key, observed in count_checks.items():
        require(
            counts.get(key) == observed,
            f"Count mismatch for {key}: summary={counts.get(key)} observed={observed}",
            failures,
        )

    csv_checks = {
        "projects.csv": counts.get("dotnet_projects"),
        "endpoints.csv": counts.get("api_endpoints"),
        "event-catalog.csv": counts.get("event_types"),
        "telemetry-metrics.csv": counts.get("telemetry_metrics"),
        "telemetry-activities.csv": counts.get("telemetry_activities"),
        "migrations.csv": counts.get("migrations"),
        "database-schemas.csv": counts.get("database_schemas"),
        "database-tables.csv": counts.get("database_tables"),
        "database-columns.csv": counts.get("database_columns"),
        "database-indexes.csv": counts.get("database_indexes"),
        "workflows.csv": counts.get("workflows"),
        "compose-services.csv": counts.get("compose_services"),
    }
    for filename, expected in csv_checks.items():
        path = root / filename
        require(path.is_file(), f"Missing CSV: {filename}", failures)
        if path.is_file():
            require(csv_rows(path) == expected, f"CSV row mismatch for {filename}", failures)

    endpoint_keys = [(item.get("http_method"), item.get("route")) for item in inventory.get("endpoints", [])]
    require(
        len(endpoint_keys) == len(set(endpoint_keys)), "Duplicate HTTP method/route declarations detected", failures
    )
    table_keys = [(item.get("schema"), item.get("table")) for item in inventory.get("database_tables", [])]
    require(len(table_keys) == len(set(table_keys)), "Duplicate schema/table entries detected", failures)

    for line_number, line in enumerate(sums_path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        try:
            expected, filename = line.split("  ", 1)
        except ValueError:
            failures.append(f"Malformed SHA256SUMS line {line_number}")
            continue
        path = root / filename
        require(path.is_file(), f"Hashed file missing: {filename}", failures)
        if path.is_file():
            observed = sha256_file(path)
            require(observed == expected, f"SHA-256 mismatch: {filename}", failures)

    if failures:
        for failure in failures:
            print(f"ERROR={failure}")
        print("PHASE_1_VERIFICATION=FAIL")
        return 1

    print("PHASE_1_VERIFICATION=PASS")
    print(f"BASELINE_ID={summary.get('baseline_id')}")
    print(f"INVENTORY_ROOT={root}")
    print(
        f"HASHED_FILE_COUNT={len([line for line in sums_path.read_text(encoding='utf-8').splitlines() if line.strip()])}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
