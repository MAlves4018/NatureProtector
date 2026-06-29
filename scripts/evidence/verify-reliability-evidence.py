#!/usr/bin/env python3
"""Verify NatureProtector Phase 6 reliability evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path


def fail(message: str) -> None:
    raise RuntimeError(message)


def read_json(path: Path):
    if not path.exists():
        fail(f"Missing required file: {path}")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_csv(path: Path):
    if not path.exists():
        fail(f"Missing required file: {path}")
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    parser.add_argument("--require-p3", action="store_true")
    parser.add_argument("--require-audit", action="store_true")
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    summary = read_json(root / "phase6-summary.json")
    if summary.get("phase") != 6:
        fail("phase6-summary.json does not identify Phase 6")
    if summary.get("staticContractStatus") != "PASS":
        fail("Static reliability contract did not pass")

    manifest = root / "SHA256SUMS.txt"
    if not manifest.exists():
        fail("SHA256SUMS.txt is missing")
    entries = []
    for line in manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        digest, relative = line.split("  ", 1)
        target = root / relative
        if not target.exists():
            fail(f"Hashed file missing: {relative}")
        if sha256(target) != digest:
            fail(f"Hash mismatch: {relative}")
        entries.append(relative)

    faults = read_csv(root / "static/fault-case-catalog.csv")
    p3_exec = [row for row in faults if row["phase"] == "P3NegativePipeline" and row["executionPolicy"] == "executable"]
    p3_blocked = [
        row
        for row in faults
        if row["phase"] == "P3NegativePipeline" and row["executionPolicy"] == "blocked_needs_fixture"
    ]
    if len(p3_exec) != 10 or len(p3_blocked) != 2:
        fail(f"Unexpected P3 catalog counts: executable={len(p3_exec)} blocked={len(p3_blocked)}")
    required_ids = {
        "P3_REJECT_INVALID_JSON",
        "P3_REJECT_UNSUPPORTED_EVENT_TYPE",
        "P3_RETRY_TRANSIENT_THEN_SUCCESS",
        "P3_RETRY_EXHAUSTED_TO_QUARANTINE",
        "P3_PERMANENT_FAILURE_TO_QUARANTINE",
        "P3_QUARANTINE_SENSOR_INACTIVE",
        "P3_QUARANTINE_SENSOR_AREA_MISMATCH",
    }
    observed_ids = {row["faultCaseId"] for row in faults}
    if not required_ids.issubset(observed_ids):
        fail(f"Required fault cases missing: {sorted(required_ids - observed_ids)}")

    retry = read_json(root / "static/retry-policy-summary.json")
    if retry.get("maxProcessingAttempts") != 3 or retry.get("retryDelaySeconds") != [5, 30]:
        fail("Retry policy does not match current repository contract")
    packs = read_csv(root / "static/query-pack-catalog.csv")
    if len(packs) != 4 or any(row["readOnlyStaticCheck"] != "true" for row in packs):
        fail("Controlled-validation query packs did not pass the read-only static gate")
    if sum(int(row["outputCount"]) for row in packs) < 28:
        fail("Unexpectedly low query-pack output count")

    audit_status = read_json(root / "execution/postgres-audit-status.json")
    execution_status = read_json(root / "execution/p3-execution-status.json")
    if args.require_p3 and execution_status.get("status") != "PASS_AUDIT_REQUIRED":
        fail(f"P3 execution required but status is {execution_status.get('status')}")
    if args.require_audit and audit_status.get("status") != "PASS":
        fail(f"P3 audit required but status is {audit_status.get('status')}")
    if audit_status.get("status") == "PASS":
        if audit_status.get("expectedCaseRows") != 12 or audit_status.get("matchedExecutableCases") != 10:
            fail("Passing audit has inconsistent case counts")
        if audit_status.get("blockedCases") != 2 or audit_status.get("unexpectedPositiveProjectionRows") != 0:
            fail("Passing audit has inconsistent blocked/unexpected projection counts")

    claims = "\n".join(summary.get("claimBoundaries", []))
    for required in (
        "P3 processing-fault injection does not prove RabbitMQ, PostgreSQL or InfluxDB outage recovery.",
        "Configured retry delays are not observed recovery time.",
        "No event-loss rate may be claimed without complete run-specific reconciliation.",
    ):
        if required not in claims:
            fail(f"Required claim boundary missing: {required}")

    secret_patterns = [
        re.compile(r"\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b"),
        re.compile(r"(?i)(password|passwd|pwd)\s*[=:]\s*[^\s\"']{4,}"),
        re.compile(r"(?i)authorization\s*:\s*bearer\s+[A-Za-z0-9._-]{10,}"),
    ]
    scanned = 0
    for path in root.rglob("*"):
        if not path.is_file() or path.name == "SHA256SUMS.txt" or path.stat().st_size > 5 * 1024 * 1024:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        scanned += 1
        for pattern in secret_patterns:
            if pattern.search(text):
                fail(f"Potential secret material found in {path.relative_to(root)}")

    counts = summary.get("counts", {})
    print("PHASE_6_VERIFICATION=PASS")
    print(f"VERIFIED_HASHED_FILES={len(entries)}")
    print(f"VERIFIED_FAULT_CASES={len(faults)}")
    print(f"VERIFIED_P3_EXECUTABLE_CASES={len(p3_exec)}")
    print(f"VERIFIED_P3_BLOCKED_CASES={len(p3_blocked)}")
    print(f"VERIFIED_STATE_TRANSITIONS={counts.get('stateTransitions')}")
    print(f"VERIFIED_FAILURE_RULES={counts.get('failureClassificationRules')}")
    print(f"VERIFIED_TEST_FILES={counts.get('reliabilityTestFiles')}")
    print(f"VERIFIED_QUERY_PACKS={len(packs)}")
    print(f"P3_EXECUTION_STATUS={execution_status.get('status')}")
    print(f"POSTGRES_AUDIT_STATUS={audit_status.get('status')}")
    print(f"SECRET_SCAN_FILES={scanned}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"PHASE_6_VERIFICATION=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
