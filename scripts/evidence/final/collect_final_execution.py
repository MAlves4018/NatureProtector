#!/usr/bin/env python3
"""Create Phase 13 by linking existing canonical evidence without raising its claim ceiling."""
from __future__ import annotations

import argparse
import csv
import shutil
from collections import Counter
from pathlib import Path
from typing import Any

from final_common import (
    find_latest_run,
    read_json,
    safe_relative,
    utc_iso,
    write_csv,
    sha256,
    write_hash_manifest,
    write_json,
)

PASS_VALUES = {
    "PASS",
    "PASSED",
    "PASS_COMPLETE_REPORT_PACKAGE",
    "REPORT_EVIDENCE_PORTFOLIO_READY",
    "LONG_RUN_STABILITY_PASS",
}
LIMITED_VALUES = {
    "PASS_WITH_LIMITATIONS",
    "PASS_PARTIAL_REPORT_PACKAGE",
}
PLAN_VALUES = {"PLAN_READY", "PLANNED", "PLAN_READY_EVIDENCE_INCOMPLETE"}
BLOCKED_VALUES = {
    "BLOCKED",
    "ENVIRONMENT_BLOCKED",
    "PARTIAL_PASS_BLOCKED_ENVIRONMENT",
    "REPORT_EVIDENCE_PORTFOLIO_NOT_READY",
}
FAIL_VALUES = {"FAIL", "FAILED", "ERROR", "LONG_RUN_STABILITY_FAIL"}


def status_from_json(path: Path | None, keys: tuple[str, ...]) -> str:
    if path is None or not path.is_file():
        return "NOT_EXECUTED"
    payload = read_json(path, {})
    if not isinstance(payload, dict):
        return "INVALID"
    for key in keys:
        value = payload.get(key)
        if value is not None:
            return str(value).upper()
    return "UNKNOWN"


def normalize_status(value: str) -> str:
    upper = value.upper()
    if upper in FAIL_VALUES or "FAIL" in upper or upper == "INVALID":
        return "FAIL"
    if upper in BLOCKED_VALUES or "BLOCKED" in upper or upper == "NOT_EXECUTED":
        return "BLOCKED"
    if upper in LIMITED_VALUES:
        return "LIMITED"
    if upper in PASS_VALUES:
        return "PASS"
    if upper in PLAN_VALUES or upper.startswith("PLAN_"):
        return "PLANNED"
    return "INCONCLUSIVE"


def resolve_input(explicit: Path | None, fallback_root: Path) -> Path | None:
    if explicit:
        return explicit.resolve()
    return find_latest_run(fallback_root)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--mode", choices=("plan", "quick", "full", "analyze"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--phase8-root", type=Path)
    parser.add_argument("--portfolio-root", type=Path)
    parser.add_argument("--long-run-root", type=Path)
    parser.add_argument("--screenshots-root", type=Path)
    parser.add_argument("--command-ledger", type=Path)
    parser.add_argument("--require-live", action="store_true")
    args = parser.parse_args()

    repo = args.repo.resolve()
    baseline_root = repo / "artifacts" / "report-evidence" / args.baseline_id
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    phase8 = resolve_input(args.phase8_root, baseline_root / "08-campaign")
    portfolio = args.portfolio_root.resolve() if args.portfolio_root else None
    long_run = args.long_run_root.resolve() if args.long_run_root else None
    screenshots = args.screenshots_root.resolve() if args.screenshots_root else None

    inputs: list[dict[str, Any]] = []
    specs = [
        ("phase8", phase8, "campaign-summary.json", ("status",)),
        ("finalPortfolio", portfolio, "verdict.json", ("status",)),
        ("longRun", long_run, "summary.json", ("status", "legacyStatus")),
    ]
    for name, root, summary_name, keys in specs:
        summary_path = root / summary_name if root else None
        raw = status_from_json(summary_path, keys)
        inputs.append(
            {
                "input": name,
                "path": safe_relative(root, repo),
                "summary": safe_relative(summary_path, repo),
                "sourceStatus": raw,
                "normalizedStatus": normalize_status(raw),
                "exists": bool(root and root.is_dir()),
                "evidenceClass": "SOURCE_DEFINED",
                "claimCeiling": "Inherited from source summary; Phase 13 does not promote it.",
            }
        )

    capture_register = screenshots / "manual-captures" / "capture-register.json" if screenshots else None
    capture_rows = read_json(capture_register, []) if capture_register else []
    screenshot_status = "PASS" if isinstance(capture_rows, list) and len(capture_rows) >= 2 else "NOT_EXECUTED"
    inputs.append(
        {
            "input": "screenshots",
            "path": safe_relative(screenshots, repo),
            "summary": safe_relative(capture_register, repo),
            "sourceStatus": screenshot_status,
            "normalizedStatus": normalize_status(screenshot_status),
            "exists": bool(screenshots and screenshots.is_dir()),
            "evidenceClass": "CURRENT_EXECUTION" if screenshot_status == "PASS" else "BLOCKED_OR_PENDING",
            "claimCeiling": "Interface demonstration only; not usability validation or operational deployment.",
        }
    )

    normalized = {row["input"]: row["normalizedStatus"] for row in inputs}
    limitations: list[str] = []
    required = ["phase8"]
    if args.mode == "full":
        required += ["finalPortfolio", "longRun", "screenshots"]
    elif args.mode == "quick":
        required += ["finalPortfolio"]

    missing = [name for name in required if normalized.get(name) not in {"PASS", "PLANNED"}]
    failures = [name for name in required if normalized.get(name) == "FAIL"]
    blocked = [name for name in required if normalized.get(name) in {"BLOCKED", "INCONCLUSIVE"}]
    limited = [name for name in required if normalized.get(name) == "LIMITED"]

    if args.mode == "plan":
        status = "PLAN_READY_EVIDENCE_INCOMPLETE"
        limitations.append("Plan mode did not execute runtime evidence.")
    elif failures:
        status = "FAIL"
        limitations.append("Required source failures: " + ", ".join(failures))
    elif args.require_live and missing:
        status = "FAIL"
        limitations.append("Strict live gate not satisfied: " + ", ".join(missing))
    elif blocked:
        status = "PARTIAL_PASS_BLOCKED_ENVIRONMENT"
        limitations.append("Required sources blocked or inconclusive: " + ", ".join(blocked))
    elif limited:
        status = "PASS_WITH_LIMITATIONS"
        limitations.append("Required sources with explicit limitations: " + ", ".join(limited))
    elif args.mode == "full" and not missing:
        status = "PASS"
    else:
        status = "PASS_WITH_LIMITATIONS"
        limitations.append(f"Mode {args.mode} does not execute the complete final campaign.")

    command_rows: list[dict[str, str]] = []
    if args.command_ledger and args.command_ledger.is_file():
        target = output / "command-ledger.csv"
        target.write_bytes(args.command_ledger.read_bytes())
        with args.command_ledger.open(encoding="utf-8", newline="") as handle:
            command_rows = list(csv.DictReader(handle))
    command_counts = Counter(str(row.get("status", "UNKNOWN")).upper() for row in command_rows)
    command_failures = [
        str(row.get("stage"))
        for row in command_rows
        if str(row.get("status", "")).upper() == "FAIL"
    ]
    command_blocked = [
        str(row.get("stage"))
        for row in command_rows
        if str(row.get("status", "")).upper() in {"BLOCKED", "NOT_EXECUTED", "PLANNED"}
    ]
    known_command_statuses = {"PASS", "FAIL", "BLOCKED", "NOT_EXECUTED", "PLANNED"}
    command_invalid = [
        str(row.get("stage"))
        for row in command_rows
        if str(row.get("status", "")).upper() not in known_command_statuses
    ]

    failure_rows = [
        {
            "stage": str(row.get("stage", "")),
            "status": str(row.get("status", "")),
            "exitCode": str(row.get("exitCode", "")),
            "limitation": str(row.get("limitation", "")),
            "stdout": str(row.get("stdout", "")),
            "stderr": str(row.get("stderr", "")),
        }
        for row in command_rows
        if str(row.get("status", "")).upper() in {"FAIL", "BLOCKED", "NOT_EXECUTED"}
    ]
    write_csv(
        output / "failures.csv",
        failure_rows,
        ["stage", "status", "exitCode", "limitation", "stdout", "stderr"],
    )

    if args.command_ledger and args.command_ledger.is_file():
        orchestration_source = args.command_ledger.resolve().parent
        orchestration_target = output / "orchestration"
        orchestration_target.mkdir(parents=True, exist_ok=True)
        for directory_name in ("logs", "states"):
            source_directory = orchestration_source / directory_name
            if source_directory.is_dir():
                shutil.copytree(
                    source_directory,
                    orchestration_target / directory_name,
                    dirs_exist_ok=True,
                )
        for file_name in ("command-ledger.csv", "execution-error.json", "orchestration-summary.json"):
            source_file = orchestration_source / file_name
            if source_file.is_file():
                shutil.copy2(source_file, orchestration_target / file_name)

    if args.mode != "plan" and not (args.command_ledger and args.command_ledger.is_file()):
        status = "FAIL"
        limitations.append("The execution command ledger is missing.")
    elif args.mode != "plan" and command_invalid:
        status = "FAIL"
        limitations.append("Unknown orchestration command statuses: " + ", ".join(command_invalid))
    elif args.mode != "plan" and command_failures:
        status = "FAIL"
        limitations.append("Failed orchestration commands: " + ", ".join(command_failures))
    elif args.mode != "plan" and command_blocked and status != "FAIL":
        status = "PARTIAL_PASS_BLOCKED_ENVIRONMENT"
        limitations.append("Environment-blocked commands: " + ", ".join(command_blocked))

    write_csv(
        output / "evidence-inputs.csv",
        inputs,
        ["input", "path", "summary", "sourceStatus", "normalizedStatus", "exists", "evidenceClass", "claimCeiling"],
    )
    write_json(output / "evidence-inputs.json", inputs)

    matrix = [
        {
            "claimId": "P13-C01",
            "claim": "The final evidence campaign is tied to one baseline and one run-scoped command ledger.",
            "source": "phase13-summary.json; command-ledger.csv",
            "status": "SUPPORTED" if args.command_ledger and args.command_ledger.is_file() else "PARTIAL",
            "limitation": "This is a provenance claim, not proof that every runtime stage succeeded.",
        },
        {
            "claimId": "P13-C02",
            "claim": "Long-duration outcomes distinguish successful completion, configured timeout and request rejection.",
            "source": safe_relative(long_run / "LONG_RUN_TERMINATION_MATRIX.csv", repo) if long_run else "",
            "status": "SUPPORTED" if normalized.get("longRun") == "PASS" else "NOT_DEMONSTRATED",
            "limitation": "Only the executed matrix and environment are covered.",
        },
        {
            "claimId": "P13-C03",
            "claim": "UI captures are registered with baseline, run, hash and limitations.",
            "source": safe_relative(capture_register, repo),
            "status": "SUPPORTED" if screenshot_status == "PASS" else "NOT_DEMONSTRATED",
            "limitation": "Screenshots demonstrate the interface state; they do not validate usability with external operators.",
        },
    ]
    write_csv(output / "claim-evidence-matrix.csv", matrix, ["claimId", "claim", "source", "status", "limitation"])

    summary = {
        "schemaVersion": 2,
        "phaseId": "phase13",
        "title": "Final integrated execution and evidence closure",
        "baselineId": args.baseline_id,
        "runId": args.run_id,
        "mode": args.mode,
        "generatedAtUtc": utc_iso(),
        "status": status,
        "evidenceClass": "CURRENT_EXECUTION" if status in {"PASS", "PASS_WITH_LIMITATIONS"} and args.mode == "full" else "MIXED_OR_PARTIAL",
        "claimCeiling": "Phase 13 only links and verifies source outputs. Each source phase remains authoritative for its evidence class and limitations.",
        "strictLiveGate": bool(args.require_live),
        "requiredInputs": required,
        "missingOrNonPassingInputs": missing,
        "inputs": inputs,
        "commandStatusCounts": dict(sorted(command_counts.items())),
        "failedCommands": command_failures,
        "blockedCommands": command_blocked,
        "limitations": limitations,
    }
    write_json(output / "phase13-summary.json", summary)
    (output / "phase13-summary.md").write_text(
        "\n".join(
            [
                "# Fase 13 — execução final integrada",
                "",
                f"- Baseline: `{args.baseline_id}`",
                f"- Run: `{args.run_id}`",
                f"- Modo: `{args.mode}`",
                f"- Estado: `{status}`",
                "",
                "A Fase 13 reutiliza as fases e verificadores existentes. Não converte planos em execução nem aumenta o teto de afirmação das fontes.",
                "",
                "## Limitações",
                *(f"- {item}" for item in limitations),
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (output / "limitations.md").write_text(
        "# Limitações\n\n" + ("\n".join(f"- {item}" for item in limitations) if limitations else "- Consultar as limitações das fases de origem.") + "\n",
        encoding="utf-8",
    )

    index_rows = []
    for path in sorted(item for item in output.rglob("*") if item.is_file() and item.name not in {"SHA256SUMS.txt", "evidence-index.csv"}):
        relative = path.relative_to(output).as_posix()
        index_rows.append(
            {
                "path": relative,
                "sizeBytes": path.stat().st_size,
                "sha256": sha256(path),
                "category": relative.split("/", 1)[0] if "/" in relative else "phase13",
            }
        )
    write_csv(output / "evidence-index.csv", index_rows, ["path", "sizeBytes", "sha256", "category"])
    write_hash_manifest(output)
    phase_root = baseline_root / "13-final-execution"
    phase_root.mkdir(parents=True, exist_ok=True)
    (phase_root / "LATEST.txt").write_text(args.run_id + "\n", encoding="utf-8")
    print(f"PHASE_13_STATUS={status}")
    print(f"PHASE_13_OUTPUT={output}")
    return 0 if status in {"PASS", "PASS_WITH_LIMITATIONS", "PLAN_READY_EVIDENCE_INCOMPLETE"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
