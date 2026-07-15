#!/usr/bin/env python3
"""Verify a NatureProtector long-run proof evidence directory."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    summary = json.loads((root / "summary.json").read_text(encoding="utf-8"))
    if summary.get("status") != "LONG_RUN_STABILITY_PASS":
        raise RuntimeError(f"Unexpected summary status: {summary.get('status')}")
    cases = summary.get("cases") or []
    required = {"LR-030-EVIDENCE", "LR-090-EVIDENCE", "LR-180-NO-EVIDENCE", "LR-300-EVIDENCE"}
    observed = {case.get("caseId") for case in cases}
    if observed != required:
        raise RuntimeError(f"Expected {sorted(required)}, got {sorted(observed)}")
    for case in cases:
        if case.get("status") != "PASS":
            raise RuntimeError(f"Case failed: {case}")
        if case.get("terminationReason") == "Unknown":
            raise RuntimeError(f"Unknown termination reason: {case.get('caseId')}")
        if not case.get("operationId") or not case.get("simulationRunId"):
            raise RuntimeError(f"Missing correlation identity: {case.get('caseId')}")
    hashes = root / "hashes.sha256"
    listed = {}
    for line in hashes.read_text(encoding="utf-8").splitlines():
        expected, relative = line.split("  ", 1)
        listed[relative] = expected
    observed = {path.relative_to(root).as_posix() for path in root.rglob("*") if path.is_file() and path != hashes}
    if observed != set(listed):
        raise RuntimeError(f"Hash inventory mismatch; extra={sorted(observed-set(listed))}, missing={sorted(set(listed)-observed)}")
    for relative, expected in listed.items():
        path = root / relative
        if not path.is_file() or sha256(path) != expected:
            raise RuntimeError(f"Hash mismatch: {relative}")
    print("LONG_RUN_STABILITY_PASS")
    print(f"VERIFIED_CASES={len(cases)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
