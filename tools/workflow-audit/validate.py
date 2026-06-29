#!/usr/bin/env python3
"""Protect the canonical deployment workflows while validating added tooling workflows."""

from __future__ import annotations
import argparse
import hashlib
import json
import re
from pathlib import Path

USES = re.compile(r"(?m)^\s*uses:\s*([^\s#]+)")
EXTERNAL_ACTION = re.compile(r"^(?!\./)(?!docker://)([^@\s]+)@([0-9a-f]{40})$")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()
    workflows = repo / ".github/workflows"
    contract_path = repo / "config/quality/workflow-convergence.json"
    failures = []
    checks = []

    def check(name: str, condition: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if condition else "FAIL", "detail": detail})
        if not condition:
            failures.append(f"{name}: {detail}")

    check("workflow-directory-exists", workflows.is_dir(), str(workflows))
    check("contract-exists", contract_path.is_file(), str(contract_path))
    if not workflows.is_dir() or not contract_path.is_file():
        return 1
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    files = sorted(path for path in workflows.iterdir() if path.suffix in {".yml", ".yaml"})
    by_name = {path.name: path for path in files}
    check("schema-version", contract.get("schema_version") == 3, str(contract.get("schema_version")))
    check(
        "workflow-count-minimum",
        len(files) >= int(contract.get("minimum_workflow_count", 0)),
        f"{len(files)} workflows",
    )
    workflow_snapshot_drift = []
    for name, expected in contract["canonical_deployment_workflows"].items():
        path = by_name.get(name)
        check(f"canonical-workflow-exists:{name}", path is not None, name)
        if path and sha256(path) != expected:
            workflow_snapshot_drift.append({"workflow": name, "expected": expected, "actual": sha256(path)})
    for path in files:
        text = path.read_text(encoding="utf-8")
        for use in USES.findall(text):
            if use.startswith("./"):
                if use.startswith("./.github/workflows/"):
                    check(
                        f"local-call-resolves:{path.name}:{use}",
                        use.removeprefix("./.github/workflows/") in by_name,
                        use,
                    )
            elif not use.startswith("docker://"):
                check(f"external-action-pinned:{path.name}:{use}", bool(EXTERNAL_ACTION.match(use)), use)
    quality = by_name.get(contract["tooling_workflow"])
    check("quality-workflow-exists", quality is not None, contract["tooling_workflow"])
    if quality:
        text = quality.read_text(encoding="utf-8")
        check("quality-workflow-read-only", "contents: read" in text and "contents: write" not in text, quality.name)
        check("quality-workflow-runs-guardrails", "tools/quality-gates/run.py" in text, quality.name)
        check("quality-workflow-runs-evidence-tests", "tests/evidence" in text, quality.name)
    check(
        "deprecated-staging-rewrite-absent",
        not (workflows / "_staging-operation.yml").exists(),
        "Current deployment remains authoritative; old reusable staging rewrite is not imported.",
    )
    for name, markers in contract.get("production_markers", {}).items():
        text = by_name[name].read_text(encoding="utf-8") if name in by_name else ""
        for marker in markers:
            check(f"production-marker:{name}:{marker}", marker in text, marker)
    payload = {
        "schema_version": 3,
        "status": "PASS" if not failures else "FAIL",
        "summary": {
            "checks": len(checks),
            "failures": len(failures),
            "workflows": len(files),
            "historical_snapshot_workflows": len(contract["canonical_deployment_workflows"]),
            "snapshot_drift": len(workflow_snapshot_drift),
        },
        "workflow_snapshot_observations": workflow_snapshot_drift,
        "checks": checks,
        "failures": failures,
    }
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    if args.output:
        Path(args.output).write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
