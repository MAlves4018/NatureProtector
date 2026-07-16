#!/usr/bin/env python3
"""Verify complete or partial Phase 7 integration packages."""

from __future__ import annotations
import argparse
import csv
import hashlib
import json
from pathlib import Path

ALLOWED_STATUSES = {"PASS_COMPLETE_REPORT_PACKAGE", "PASS_PARTIAL_REPORT_PACKAGE"}
ALLOWED_CLASSES = {
    "CURRENT_EXECUTION",
    "CURRENT_STATIC_VERIFICATION",
    "CURRENT_ANALYTICAL_EVIDENCE",
    "HISTORICAL_EXECUTION",
    "IMPLEMENTED_NOT_EXECUTED",
    "BLOCKED_OR_PENDING",
    "NO_SOURCE_EVIDENCE",
}


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id")
    parser.add_argument("--evidence-root")
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()
    if args.evidence_root:
        root = Path(args.evidence_root).resolve()
    else:
        phase = repo / "artifacts" / "report-evidence" / args.baseline_id / "07-report-integration"
        run_id = args.run_id or (phase / "LATEST.txt").read_text(encoding="utf-8").strip()
        root = (phase / run_id).resolve()
    errors = []
    summary_path = root / "phase7-summary.json"
    if not summary_path.is_file():
        errors.append("missing phase7-summary.json")
        summary = {}
    else:
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
        status = summary.get("status")
        if status not in ALLOWED_STATUSES:
            errors.append(f"unexpected summary status: {status}")
        if args.require_complete and status != "PASS_COMPLETE_REPORT_PACKAGE":
            errors.append(f"package is not complete: {status}")
        if summary.get("baselineId") != args.baseline_id:
            errors.append("baseline mismatch")
    for name in summary.get("generatedTables", []):
        for ext in ("csv", "md"):
            if not (root / "tables" / f"{name}.{ext}").is_file():
                errors.append(f"missing table {name}.{ext}")
        if not (root / "latex" / f"table-{name}.tex").is_file():
            errors.append(f"missing latex table {name}")
    for relative in summary.get("generatedFigures", []):
        if not (root / "figures" / relative).is_file():
            errors.append(f"missing figure {relative}")
    for relative in summary.get("generatedAssets", []):
        if not (root / relative).is_file():
            errors.append(f"missing report asset {relative}")
    missing_register = root / "tables" / "missing-evidence-register.csv"
    if not missing_register.is_file():
        errors.append("missing missing-evidence-register.csv")
    claim_file = root / "claims" / "claim-evidence-register.csv"
    claims = []
    if not claim_file.is_file():
        errors.append("missing claim register")
    else:
        with claim_file.open(encoding="utf-8", newline="") as handle:
            claims = list(csv.DictReader(handle))
        if not claims:
            errors.append("empty claim register")
        for claim in claims:
            if claim.get("evidence_class") not in ALLOWED_CLASSES:
                errors.append(f"invalid evidence class {claim.get('claim_id')}")
            source = repo / claim.get("source", "")
            if not source.is_file():
                errors.append(f"missing claim source {claim.get('claim_id')}: {claim.get('source')}")
    hashes = root / "SHA256SUMS.txt"
    checked = 0
    if not hashes.is_file():
        errors.append("missing SHA256SUMS.txt")
    else:
        for line in hashes.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            expected, relative = line.split("  ", 1)
            path = root / relative
            if not path.is_file():
                errors.append(f"missing hashed file {relative}")
                continue
            if digest(path) != expected:
                errors.append(f"hash mismatch {relative}")
            checked += 1
    if errors:
        print("PHASE_7_VERIFICATION=FAILED")
        for error in errors:
            print(f"ERROR={error}")
        return 1
    print("PHASE_7_VERIFICATION=PASS")
    print(f"VERIFIED_PACKAGE_STATUS={summary.get('status')}")
    print(f"VERIFIED_HASHED_FILES={checked}")
    print(f"VERIFIED_CLAIMS={len(claims)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
