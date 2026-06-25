#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "infra/gcp/contracts/g10-3-budget-input.schema.json"


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path}")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate the owner-controlled G10.3 budget input.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()

    input_path = Path(args.input).resolve()
    data = read_json(input_path)
    schema = read_json(SCHEMA)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    schema_errors = sorted(validator.iter_errors(data), key=lambda error: list(error.absolute_path))
    findings: list[str] = []
    for error in schema_errors:
        location = "/".join(str(part) for part in error.absolute_path) or "$"
        findings.append(f"schema:{location}:{error.message}")

    budgets = data.get("budgets") or []
    roles = [item.get("role") for item in budgets if isinstance(item, dict)]
    if len(roles) != len(set(roles)):
        findings.append("semantic:budget roles must be unique")
    if "staging" not in roles:
        findings.append("semantic:a staging project budget is required")

    currencies = {item.get("currency") for item in budgets if isinstance(item, dict)}
    if len(currencies) > 1:
        findings.append("semantic:all budgets must use one billing-account currency")

    forbidden = re.compile(r"(?i)(cn2526|course|student|emailteste)")
    for item in budgets:
        if not isinstance(item, dict):
            continue
        role = item.get("role")
        scope = item.get("scope")
        project_id = item.get("project_id")
        if scope == "project" and role == "billing":
            findings.append("semantic:billing role cannot use project scope")
        if scope == "billing-account" and role != "billing":
            findings.append(f"semantic:{role} must use project scope")
        if project_id and forbidden.search(str(project_id)):
            findings.append(f"semantic:forbidden project identifier: {project_id}")
        amount = item.get("amount")
        if isinstance(amount, (int, float)) and float(amount) != int(amount):
            findings.append(f"semantic:{role} budget amount must be a whole currency unit for gcloud budget CLI execution")

    thresholds = data.get("thresholds") or []
    if thresholds != sorted(thresholds):
        findings.append("semantic:thresholds must be sorted ascending")
    if 1.0 not in thresholds:
        findings.append("semantic:a 100% threshold is required")

    result = {
        "phase": "G10.3_BUDGET_INPUT",
        "status": "PASS" if not findings else "FAIL",
        "input": str(input_path),
        "billing_account_id": data.get("billing_account_id"),
        "roles": roles,
        "budget_count": len(budgets),
        "budget_is_hard_cap": False,
        "findings": findings,
    }
    output = json.dumps(result, indent=2, ensure_ascii=False)
    print(output)
    if args.output:
        Path(args.output).write_text(output + "\n", encoding="utf-8")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
