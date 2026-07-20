#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path

from final_common import verify_hash_manifest

BEARER_RE = re.compile(r"(?i)\bbearer\s+([A-Za-z0-9._~+/=-]{20,})")
ASSIGNMENT_RE = re.compile(
    r"""(?ix)
    \b(?:password|token)\b
    \s*[=:]\s*
    ["']?
    ([A-Za-z0-9._~+/=-]{12,})
    """
)
NON_SECRET_VALUES = {
    "authorization",
    "bearertoken",
    "missing",
    "none",
    "null",
    "password",
    "placeholder",
    "redacted",
    "token",
    "tokenpersisted",
    "undefined",
    "unknown",
}
NON_SECRET_REFERENCE_PREFIXES = (
    "process.env.",
    "import.meta.env.",
    "os.environ.",
    "environment.",
)
ALLOWED = {"PASS", "PASS_WITH_LIMITATIONS", "PLAN_READY_EVIDENCE_INCOMPLETE"}


def potential_secret(text: str) -> str | None:
    if BEARER_RE.search(text):
        return "bearer credential"

    for match in ASSIGNMENT_RE.finditer(text):
        candidate = match.group(1)
        lowered = candidate.lower()
        normalized = re.sub(r"[^a-z0-9]", "", lowered)
        if normalized in NON_SECRET_VALUES:
            continue
        if lowered.startswith(NON_SECRET_REFERENCE_PREFIXES):
            continue
        return "credential assignment"

    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase13_root", type=Path)
    parser.add_argument("--require-live", action="store_true")
    args = parser.parse_args()
    root = args.phase13_root.resolve()
    errors = verify_hash_manifest(root)
    for name in ("phase13-summary.json", "evidence-inputs.csv", "claim-evidence-matrix.csv", "limitations.md"):
        if not (root / name).is_file():
            errors.append(f"Missing {name}")
    summary = {}
    if (root / "phase13-summary.json").is_file():
        summary = json.loads((root / "phase13-summary.json").read_text(encoding="utf-8"))
        if summary.get("phaseId") != "phase13":
            errors.append("Unexpected phaseId")
        if not summary.get("baselineId") or not summary.get("runId"):
            errors.append("Missing baselineId/runId")
        status = str(summary.get("status", ""))
        mode = str(summary.get("mode", ""))
        if status not in ALLOWED:
            errors.append(f"Unexpected Phase 13 status: {status}")
        if status == "PLAN_READY_EVIDENCE_INCOMPLETE" and mode != "plan":
            errors.append("Incomplete plan status is only valid in plan mode")
        if status in {"PASS", "PASS_WITH_LIMITATIONS"}:
            if summary.get("failedCommands"):
                errors.append("Passing summary contains failed commands")
            if summary.get("blockedCommands"):
                errors.append("Passing summary contains blocked or unexecuted commands")
            counts = summary.get("commandStatusCounts", {})
            for command_status in ("FAIL", "BLOCKED", "NOT_EXECUTED"):
                if int(counts.get(command_status, 0) or 0) > 0:
                    errors.append(f"Passing summary counts {command_status} commands")

            inputs = summary.get("inputs", [])
            by_name = {
                str(row.get("input")): str(row.get("normalizedStatus"))
                for row in inputs
                if isinstance(row, dict)
            }
            required = [str(value) for value in summary.get("requiredInputs", [])]
            invalid_required = [
                name
                for name in required
                if by_name.get(name) in {None, "FAIL", "BLOCKED", "INCONCLUSIVE", "PLANNED"}
            ]
            if invalid_required:
                errors.append("Passing summary has non-passing required inputs: " + ", ".join(invalid_required))
            limited_required = [name for name in required if by_name.get(name) == "LIMITED"]
            if status == "PASS" and limited_required:
                errors.append("PASS summary promotes limited required inputs")
            if status == "PASS" and summary.get("missingOrNonPassingInputs"):
                errors.append("PASS summary has missing/non-passing inputs")
            if status == "PASS_WITH_LIMITATIONS" and mode == "full" and not limited_required:
                errors.append("Full limited summary has no limited required input")

            ledger = root / "command-ledger.csv"
            if not ledger.is_file():
                errors.append("Passing execution is missing command-ledger.csv")
            else:
                with ledger.open(encoding="utf-8", newline="") as handle:
                    ledger_rows = list(csv.DictReader(handle))
                bad_rows = [
                    str(row.get("stage", ""))
                    for row in ledger_rows
                    if str(row.get("status", "")).upper() != "PASS"
                ]
                if bad_rows:
                    errors.append("Passing execution ledger has non-PASS rows: " + ", ".join(bad_rows))

            evidence_inputs = root / "evidence-inputs.json"
            if evidence_inputs.is_file():
                recorded_inputs = json.loads(evidence_inputs.read_text(encoding="utf-8"))
                if recorded_inputs != inputs:
                    errors.append("Summary inputs differ from evidence-inputs.json")
        if args.require_live and (status != "PASS" or mode != "full"):
            errors.append("Strict live Phase 13 gate not satisfied")
        if args.require_live and summary.get("missingOrNonPassingInputs"):
            errors.append("Strict live gate has missing/non-passing inputs")
    for path in root.rglob("*"):
        if path.is_file() and path.suffix.lower() in {".json", ".csv", ".md", ".txt", ".log"}:
            text = path.read_text(encoding="utf-8", errors="replace")
            finding = potential_secret(text)
            if finding:
                errors.append(
                    f"Potential secret material ({finding}) in {path.relative_to(root).as_posix()}"
                )
    if errors:
        print("\n".join(errors))
        return 1
    print(summary.get("status"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
