from __future__ import annotations
import json
import re
from pathlib import Path
from typing import Any

REQUIRED_CAMPAIGNS = {"E1", "E2", "E3", "E4", "E5", "E6"}
REQUIRED_CASE_FILES = {
    "api-run": [
        "acceptance.json",
        "operation.json",
        "database/run.json",
        "database/audit.json",
        "metrics/timings.json",
        "tables/claim-assertions.json",
        "verdict.json",
        "hashes.sha256",
    ],
    "command": [
        "configuration/command.json",
        "logs/stdout.txt",
        "logs/stderr.txt",
        "tables/claim-assertions.json",
        "verdict.json",
        "hashes.sha256",
    ],
}


def nonempty(path: Path) -> bool:
    return path.is_file() and path.stat().st_size > 0


def accounting_assertions(operation: dict[str, Any], *, nominal: bool = False) -> list[str]:
    a = operation.get("accounting") or {}
    errors = []
    expected = int(a.get("expectedObservations", 0))
    accepted = int(a.get("acceptedObservations", 0))
    processed = int(a.get("processedInbox", 0))
    quarantined = int(a.get("quarantinedInbox", 0))
    if not a.get("settled"):
        errors.append("accounting is not settled")
    for key in ("pendingInbox", "processingInbox", "retryPendingInbox"):
        if int(a.get(key, 0)) != 0:
            errors.append(f"{key} is not zero")
    if processed + quarantined != accepted:
        errors.append("processed+quarantined does not equal accepted")
    if nominal:
        if expected <= 0 or accepted != expected:
            errors.append("nominal expected/accepted accounting is incomplete")
        if quarantined != 0:
            errors.append("nominal run contains quarantined events")
    return errors


def claim_assertions(
    campaign: str,
    case: dict[str, Any],
    operation: dict[str, Any] | None,
    run: dict[str, Any] | None,
    audit: dict[str, Any] | None,
    timings: dict[str, Any] | None,
    stdout: str = "",
) -> dict[str, Any]:
    errors = []
    checks = []
    if case.get("kind") == "api-run":
        operation = operation or {}
        run = run or {}
        audit = audit or {}
        timings = timings or {}
        errors += accounting_assertions(operation, nominal=campaign == "E1")
        if operation.get("state") != "SystemCompleted" or not operation.get("simulationRunId"):
            errors.append("operation did not reach correlated SystemCompleted")
        if not run:
            errors.append("run artifact is empty")
        if not audit:
            errors.append("audit artifact is empty")
        if not timings:
            errors.append("timings artifact is empty")
        blob = json.dumps({"run": run, "audit": audit}, sort_keys=True).lower()
        profiles = [str(x).lower() for x in case.get("degradationProfiles", [])]
        if campaign == "E2":
            if not profiles:
                errors.append("degradation campaign has no profile")
            for profile in profiles:
                if profile not in blob:
                    errors.append(f"profile {profile} is not attributable in run/audit evidence")
            a = operation.get("accounting") or {}
            if "missing" in profiles and int(a.get("acceptedObservations", 0)) >= int(a.get("expectedObservations", 0)):
                errors.append("missing profile did not produce an observable accounting deficit")
        if campaign == "E3":
            required = ("cycle", "expected", "missing", "snapshot")
            for token in required:
                if token not in blob:
                    errors.append(f"temporal evidence lacks {token}")
        checks = [
            "correlated_identity",
            "terminal_system_completed",
            "settled_accounting",
            "nonempty_run_audit_timings",
        ]
    else:
        patterns = case.get("successPatterns") or [
            r"Passed!",
            r"Test Run Successful",
            r"Total tests",
            r"SYSTEM_CAPACITY_WORKLOAD_PASS",
        ]
        if not stdout.strip():
            errors.append("command stdout is empty")
        if not any(re.search(p, stdout, re.I | re.M) for p in patterns):
            errors.append("command output contains no reviewed success marker")
        checks = ["nonempty_command_output", "reviewed_success_marker"]
    return {"campaign": campaign, "caseId": case.get("id"), "checks": checks, "errors": errors, "passed": not errors}


def validate_case_tree(case_dir: Path, kind: str) -> list[str]:
    errors = []
    for rel in REQUIRED_CASE_FILES[kind]:
        if not nonempty(case_dir / rel):
            errors.append(f"missing or empty {rel}")
    # empty evidence domains are forbidden for live API evidence
    if kind == "api-run":
        required_domain = (
            "rabbitmq/queue-metrics.json",
            "influx/run-query.json",
            "grafana/dashboard-inventory.json",
            "tables/claim-assertions.json",
        )
        for rel in required_domain:
            if not nonempty(case_dir / rel):
                errors.append(f"missing or empty {rel}")
        for rel in required_domain[:3]:
            try:
                value = json.loads((case_dir / rel).read_text(encoding="utf-8"))
                if not value:
                    errors.append(f"{rel} is not collected evidence")
                elif isinstance(value, dict) and value.get("collectionContractOnly") is True:
                    errors.append(f"{rel} is not collected evidence")
            except (OSError, json.JSONDecodeError):
                errors.append(f"{rel} is not valid JSON")
    return errors
