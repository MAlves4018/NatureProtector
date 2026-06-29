#!/usr/bin/env python3
"""Independently verify NatureProtector Phase 4 runtime evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any, Sequence


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def parse_manifest(path: Path) -> list[tuple[str, str]]:
    rows = []
    for raw in path.read_text(encoding="utf-8").splitlines():
        if not raw.strip():
            continue
        digest, relative = raw.split("  ", 1)
        rows.append((digest, relative))
    return rows


def fail(message: str) -> None:
    raise RuntimeError(message)


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    parser.add_argument("--require-live", action="store_true")
    parser.add_argument("--require-database-trace", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    root = args.evidence_root.resolve()
    if not root.is_dir():
        fail(f"Evidence root does not exist: {root}")
    manifest = root / "SHA256SUMS.txt"
    if not manifest.exists():
        fail("SHA256SUMS.txt is missing")
    entries = parse_manifest(manifest)
    if not entries:
        fail("SHA256SUMS.txt is empty")
    for expected, relative in entries:
        path = root / relative
        if not path.is_file():
            fail(f"Hashed file is missing: {relative}")
        actual = sha256(path)
        if actual != expected:
            fail(f"SHA-256 mismatch for {relative}: expected {expected}, got {actual}")

    summary = read_json(root / "phase4-summary.json")
    if summary.get("phase") != 4:
        fail("phase4-summary.json does not identify Phase 4")
    if summary.get("staticRuntimeContractStatus") != "PASS":
        fail("Static runtime contract did not pass")
    counts = summary.get("staticCounts") or {}
    if int(counts.get("endpoints", 0)) < 13:
        fail("Runtime endpoint inventory is unexpectedly small")
    if int(counts.get("diagnostics", 0)) < 28:
        fail("Runtime diagnostic inventory is unexpectedly small")
    if int(counts.get("degradationProfiles", 0)) < 11:
        fail("Degradation profile inventory is unexpectedly small")
    if int(counts.get("chainEntities", 0)) != 7:
        fail("Runtime chain model must contain seven entities")

    historical_rows = list(csv.DictReader((root / "historical/historical-runs.csv").open(encoding="utf-8-sig")))
    if len(historical_rows) != 2:
        fail(f"Expected two historical B/C rows, found {len(historical_rows)}")
    by_scenario = {row["scenarioCode"]: row for row in historical_rows}
    if set(by_scenario) != {"scenario_b", "scenario_c"}:
        fail(f"Unexpected historical scenarios: {sorted(by_scenario)}")
    b, c = by_scenario["scenario_b"], by_scenario["scenario_c"]
    for row in historical_rows:
        if row.get("evidenceClass") != "HISTORICAL_REPOSITORY_EXECUTION":
            fail("Historical rows must be explicitly labelled")
        if int(row["reconciliationDelta"]) != 0:
            fail(f"Historical reconciliation failed for {row['scenarioCode']}")
    if (int(b["expectedEvents"]), int(b["inboxEvents"]), int(b["missingEvents"])) != (30, 30, 0):
        fail("Historical scenario_b values do not match the preserved evidence")
    if (int(c["expectedEvents"]), int(c["inboxEvents"]), int(c["missingEvents"])) != (30, 24, 6):
        fail("Historical scenario_c values do not match the preserved evidence")

    live_status = read_json(root / "live/live-status.json")
    if args.require_live and live_status.get("status") != "PASS":
        fail(f"Current live evidence is required but status is {live_status.get('status')}")
    db_status = read_json(root / "database-trace/database-trace-status.json")
    if args.require_database_trace and db_status.get("status") != "PASS":
        fail(f"Database trace is required but status is {db_status.get('status')}")

    sql = (root / "database-trace/runtime-trace-query.sql").read_text(encoding="utf-8")
    forbidden_sql = re.compile(r"\b(insert|update|delete|truncate|drop|alter|create)\b", re.I)
    if forbidden_sql.search(sql):
        fail("Runtime trace SQL is not read-only")
    if "risk_assessment_log" not in sql or "event_inbox" not in sql or "cell_operational_state" not in sql:
        fail("Runtime trace SQL does not cover the required chain")

    secret_patterns = [
        re.compile(r"\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b"),
        re.compile(r"(?i)(password|passwd|pwd)\s*[=:]\s*[^\s\"']{4,}"),
        re.compile(r"(?i)authorization\s*:\s*bearer\s+[A-Za-z0-9._-]{10,}"),
    ]
    scanned = 0
    for path in root.rglob("*"):
        if not path.is_file() or path.name == "SHA256SUMS.txt" or path.stat().st_size > 5 * 1024 * 1024:
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        scanned += 1
        for pattern in secret_patterns:
            if pattern.search(text):
                fail(f"Potential secret material found in {path.relative_to(root)}")

    print("PHASE_4_VERIFICATION=PASS")
    print(f"VERIFIED_HASHED_FILES={len(entries)}")
    print(f"VERIFIED_ENDPOINTS={counts.get('endpoints')}")
    print(f"VERIFIED_DIAGNOSTICS={counts.get('diagnostics')}")
    print(f"VERIFIED_HISTORICAL_RUNS={len(historical_rows)}")
    print(f"CURRENT_RUNTIME_EXECUTION_STATUS={live_status.get('status')}")
    print(f"DATABASE_TRACE_STATUS={db_status.get('status')}")
    print(f"SECRET_SCAN_FILES={scanned}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"PHASE_4_VERIFICATION=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
