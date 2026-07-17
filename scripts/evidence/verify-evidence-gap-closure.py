#!/usr/bin/env python3
"""Verify a Phase 11 evidence-gap closure package."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
from typing import Any

CLOSED_STATES = {"CLOSED_CURRENT", "CLOSED_STATIC", "CLOSED_ANALYTICAL", "CLOSED_HISTORICAL"}
REQUIRED = {
    "phase11-summary.json",
    "phase11-summary.md",
    "closure-matrix.json",
    "closure-matrix.csv",
    "environment-readiness.json",
    "environment-readiness.csv",
    "historical-admission-audit.json",
    "report-ready/tables/evidence-closure-matrix.md",
    "report-ready/tables/evidence-closure-matrix.csv",
    "report-ready/tables/completion-readiness.csv",
    "report-ready/tables/completion-readiness.md",
    "report-ready/figures/evidence-completeness-and-readiness.svg",
    "report-ready/report-integration-note.md",
    "handoff/windows-closure-runbook.ps1",
    "handoff/unix-closure-runbook.sh",
    "handoff/closure-checklist.md",
    "SHA256SUMS.txt",
}


def read_json(path: Path, default: Any = None) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return default


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def csv_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    issues: list[str] = []
    for rel in sorted(REQUIRED):
        if not (root / rel).is_file():
            issues.append(f"Missing required file: {rel}")
    if issues:
        for issue in issues:
            print(f"ERROR={issue}")
        print("PHASE_11_VERIFICATION=FAIL")
        return 1

    summary = read_json(root / "phase11-summary.json", {})
    matrix = read_json(root / "closure-matrix.json", [])
    audit = read_json(root / "historical-admission-audit.json", {})
    if not isinstance(summary, dict):
        issues.append("Invalid phase11-summary.json")
        summary = {}
    if not isinstance(matrix, list) or not matrix:
        issues.append("Closure matrix must contain requirements")
        matrix = []
    ids = [str(row.get("requirementId")) for row in matrix if isinstance(row, dict)]
    if len(ids) != len(set(ids)):
        issues.append("Duplicate requirement IDs")
    closed = 0
    for row in matrix:
        if not isinstance(row, dict):
            issues.append("Invalid closure matrix row")
            continue
        state = str(row.get("closureState"))
        if state in CLOSED_STATES:
            closed += 1
            if not row.get("sourceFile") or not row.get("sourceSha256"):
                issues.append(f"Closed requirement lacks source/hash: {row.get('requirementId')}")
            # Source may be outside the phase directory; only verify the admitted historical source locally.
            if state == "CLOSED_HISTORICAL":
                local = root / "admitted" / "historical-runs.csv"
                if not local.is_file():
                    issues.append("Historical requirement closed without admitted/historical-runs.csv")
        elif not row.get("closureCommand"):
            issues.append(f"Open requirement lacks closure command: {row.get('requirementId')}")
        if bool(row.get("countsAsEvidence")) != (state in CLOSED_STATES):
            issues.append(f"countsAsEvidence mismatch: {row.get('requirementId')}")

    total = len(matrix)
    expected_coverage = round(100.0 * closed / total, 1) if total else 0.0
    if round(float(summary.get("evidenceCoveragePercent", -1)), 1) != expected_coverage:
        issues.append("Evidence coverage does not reconcile with closure matrix")
    planned = sum(1 for row in matrix if row.get("closureState") in CLOSED_STATES or row.get("hasExecutableClosurePlan"))
    expected_readiness = round(100.0 * planned / total, 1) if total else 0.0
    if round(float(summary.get("closureReadinessPercent", -1)), 1) != expected_readiness:
        issues.append("Closure readiness does not reconcile with closure matrix")
    if summary.get("status") not in {"PASS_GAP_CLOSURE_READY", "PASS_EVIDENCE_COMPLETE", "PLAN_READY_EVIDENCE_INCOMPLETE", "PASS_WITH_LIMITATIONS", "NEEDS_REVISION"}:
        issues.append(f"Invalid Phase 11 status: {summary.get('status')}")

    if audit.get("status") == "ADMITTED_HISTORICAL":
        history_json = root / "admitted" / "historical-runs.json"
        history_csv = root / "admitted" / "historical-runs.csv"
        if not history_json.is_file() or not history_csv.is_file():
            issues.append("Historical admission files are missing")
        else:
            rows = read_json(history_json, [])
            csv_data = csv_rows(history_csv)
            if len(rows) != 2 or len(csv_data) != 2:
                issues.append("Historical B/C admission must contain exactly two scenarios")
            scenarios = {row.get("scenario") for row in rows if isinstance(row, dict)}
            if scenarios != {"scenario_b", "scenario_c"}:
                issues.append("Historical admission must contain scenario_b and scenario_c")
            for row in rows:
                if int(row.get("expected", -1)) != int(row.get("inbox", -1)) + int(row.get("missing", -1)):
                    issues.append(f"Historical reconciliation failed: {row.get('scenario')}")
                if row.get("evidenceClass") != "HISTORICAL_EXECUTION":
                    issues.append(f"Unexpected historical evidence class: {row.get('scenario')}")

    sums = root / "SHA256SUMS.txt"
    lines = [line for line in sums.read_text(encoding="utf-8").splitlines() if line.strip()]
    for index, line in enumerate(lines, start=1):
        try:
            expected, rel = line.split("  ", 1)
        except ValueError:
            issues.append(f"Malformed SHA256SUMS line {index}")
            continue
        path = root / rel
        if not path.is_file():
            issues.append(f"Hashed file missing: {rel}")
        elif sha256(path) != expected:
            issues.append(f"SHA-256 mismatch: {rel}")
    actual = [p for p in root.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt"]
    if len(lines) != len(actual):
        issues.append("SHA256SUMS does not cover every evidence file")

    # Runbooks must not contain concrete secret assignments.
    for rel in ("handoff/windows-closure-runbook.ps1", "handoff/unix-closure-runbook.sh"):
        text = (root / rel).read_text(encoding="utf-8")
        lowered = text.lower()
        for marker in ("password=", "token=", "connectionstring=", "dsn="):
            if marker in lowered and "<redacted>" not in lowered:
                issues.append(f"Potential unredacted secret marker in {rel}")

    if issues:
        for issue in issues:
            print(f"ERROR={issue}")
        print("PHASE_11_VERIFICATION=FAIL")
        return 1
    print("PHASE_11_VERIFICATION=PASS")
    print(f"PHASE_11_STATUS={summary.get('status')}")
    print(f"EVIDENCE_COVERAGE={summary.get('evidenceCoveragePercent')}")
    print(f"CLOSURE_READINESS={summary.get('closureReadinessPercent')}")
    print(f"HASHED_FILE_COUNT={len(lines)}")
    print(f"EVIDENCE_ROOT={root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
