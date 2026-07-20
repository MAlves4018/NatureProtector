#!/usr/bin/env python3
"""Verify a NatureProtector long-run proof against the matrix that produced it."""
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


def verify_hashes(root: Path) -> list[str]:
    errors: list[str] = []
    manifest = root / "hashes.sha256"
    if not manifest.is_file():
        return ["Missing hashes.sha256"]
    listed: dict[str, str] = {}
    for line in manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            expected, relative = line.split("  ", 1)
        except ValueError:
            errors.append(f"Malformed hash line: {line[:120]}")
            continue
        listed[relative] = expected
    observed = {
        path.relative_to(root).as_posix()
        for path in root.rglob("*")
        if path.is_file() and path != manifest
    }
    for relative in sorted(observed - set(listed)):
        errors.append(f"Unlisted file: {relative}")
    for relative in sorted(set(listed) - observed):
        errors.append(f"Missing hashed file: {relative}")
    for relative, expected in listed.items():
        path = root / relative
        if path.is_file() and sha256(path) != expected:
            errors.append(f"Hash mismatch: {relative}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence_root", type=Path)
    parser.add_argument("--matrix", type=Path, default=Path("config/runtime/long-run-proof-matrix.json"))
    args = parser.parse_args()
    root = args.evidence_root.resolve()
    matrix = json.loads(args.matrix.resolve().read_text(encoding="utf-8"))
    configured = matrix.get("cases") or []
    expected_ids = {str(case.get("id")) for case in configured}
    expected_by_id = {str(case.get("id")): case for case in configured}

    errors = verify_hashes(root)
    required_files = {
        "summary.json",
        "LONG_RUN_TERMINATION_MATRIX.csv",
        "matrix.csv",
        "timeline.csv",
        "process-observations.csv",
    }
    for name in required_files:
        if not (root / name).is_file():
            errors.append(f"Missing {name}")
    if errors:
        print("\n".join(errors))
        return 1

    summary = json.loads((root / "summary.json").read_text(encoding="utf-8"))
    if summary.get("status") != "PASS" and summary.get("legacyStatus") != "LONG_RUN_STABILITY_PASS":
        errors.append(f"Unexpected summary status: {summary.get('status')}")
    cases = summary.get("cases") or []
    observed_ids = {str(case.get("caseId")) for case in cases}
    if observed_ids != expected_ids:
        errors.append(f"Expected {sorted(expected_ids)}, got {sorted(observed_ids)}")

    for case in cases:
        case_id = str(case.get("caseId"))
        configured_case = expected_by_id.get(case_id, {})
        expected_outcomes = configured_case.get("expectedTerminalOutcomes") or [configured_case.get("expectedOutcome", "SystemCompleted")]
        if case.get("status") != "PASS":
            errors.append(f"Case failed: {case_id}")
        if case.get("terminalOutcome") not in expected_outcomes:
            errors.append(f"Unexpected outcome for {case_id}: {case.get('terminalOutcome')} not in {expected_outcomes}")
        if case.get("terminationReason") == "Unknown":
            errors.append(f"Unknown termination reason: {case_id}")
        if "Rejected" not in expected_outcomes and not case.get("operationId"):
            errors.append(f"Missing OperationId: {case_id}")
        if "SystemCompleted" in expected_outcomes and not case.get("simulationRunId"):
            errors.append(f"Missing SimulationRunId: {case_id}")
        if not (root / "termination-manifests" / f"{case_id}.json").is_file():
            errors.append(f"Missing termination manifest: {case_id}")

    if errors:
        print("\n".join(errors))
        return 1
    print("LONG_RUN_STABILITY_PASS")
    print(f"VERIFIED_CASES={len(cases)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
