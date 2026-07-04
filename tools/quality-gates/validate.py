#!/usr/bin/env python3
"""Validate the progressive quality-gate contract without executing external tools."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

EXPECTED_ENFORCED = {
    "quality-policy-static",
    "control-plane-decomposition",
    "frontend-decomposition",
    "workflow-convergence",
    "operations-control-plane",
    "operations-python-tests",
    "repository-final-cleanup",
    "frontend-typecheck-strict",
    "python-ruff-critical",
    "frontend-biome-full",
    "frontend-format-full",
}
EXPECTED_REPORT_ONLY = {
    "dotnet-analyzers",
    "powershell-psscriptanalyzer",
    "shell-shellcheck",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()
    failures: list[str] = []
    checks: list[dict[str, Any]] = []

    def check(name: str, condition: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if condition else "FAIL", "detail": detail})
        if not condition:
            failures.append(f"{name}: {detail}")

    config_path = repo / "config/quality/quality-gates.json"
    baseline_path = repo / "config/quality/quality-baseline.json"
    check("quality-config-exists", config_path.is_file(), str(config_path))
    check("quality-baseline-exists", baseline_path.is_file(), str(baseline_path))
    if not config_path.is_file() or not baseline_path.is_file():
        return 1

    config = json.loads(config_path.read_text(encoding="utf-8"))
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    gates = config.get("gates", [])
    ids = [gate.get("id") for gate in gates]
    check("schema-version", config.get("schema_version") == 1, str(config.get("schema_version")))
    check("default-mode-enforce", config.get("default_mode") == "enforce", str(config.get("default_mode")))
    check("gate-ids-unique", len(ids) == len(set(ids)), json.dumps(ids))
    check(
        "enforced-set-exact",
        {g["id"] for g in gates if g.get("rollout") == "enforce"} == EXPECTED_ENFORCED,
        json.dumps(ids),
    )
    check(
        "report-set-exact",
        {g["id"] for g in gates if g.get("rollout") == "report"} == EXPECTED_REPORT_ONLY,
        json.dumps(ids),
    )
    for gate in gates:
        gate_id = gate.get("id", "missing")
        check(f"rollout-valid:{gate_id}", gate.get("rollout") in {"report", "enforce"}, str(gate.get("rollout")))
        command = gate.get("command")
        check(
            f"command-list:{gate_id}",
            isinstance(command, list) and all(isinstance(x, str) and x for x in command),
            repr(command),
        )
        cwd = repo / gate.get("cwd", "")
        check(f"cwd-exists:{gate_id}", cwd.is_dir(), str(cwd))

    expected_files = [
        ".editorconfig",
        ".shellcheckrc",
        "PSScriptAnalyzerSettings.psd1",
        "pyproject.toml",
        "tools/quality-gates/run.py",
        "tools/quality-gates/requirements.txt",
        "tools/operations-audit/validate.py",
        "scripts/operations/report-operation-callback.py",
        "webUI/biome.quality.jsonc",
        ".github/workflows/quality-guardrails.yml",
    ]
    for relative in expected_files:
        check(f"required-file:{relative}", (repo / relative).is_file(), relative)

    tsconfig = json.loads((repo / "webUI/tsconfig.json").read_text(encoding="utf-8"))
    check("typescript-strict-enabled", tsconfig.get("compilerOptions", {}).get("strict") is True, "webUI/tsconfig.json")
    package = json.loads((repo / "webUI/package.json").read_text(encoding="utf-8"))
    scripts = package.get("scripts", {})
    for name in ("typecheck:strict", "lint:all", "format:all:check"):
        check(f"frontend-script:{name}", name in scripts, scripts.get(name, "missing"))

    props = (repo / "Directory.Build.props").read_text(encoding="utf-8")
    check(
        "dotnet-report-default",
        "<NPQualityMode Condition=" in props and ">report</NPQualityMode>" in props,
        "Directory.Build.props",
    )
    check(
        "dotnet-enforce-opt-in",
        "'$(NPQualityMode)' == 'enforce'" in props and "<TreatWarningsAsErrors" in props,
        "Directory.Build.props",
    )
    check("baseline-promotion-rule", bool(baseline.get("promotion_rule")), "config/quality/quality-baseline.json")

    payload = {
        "schema_version": 1,
        "status": "PASS" if not failures else "FAIL",
        "summary": {"checks": len(checks), "failures": len(failures), "gates": len(gates)},
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
