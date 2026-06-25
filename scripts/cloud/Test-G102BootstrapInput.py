#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timedelta
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "infra/gcp/contracts/g10-2-bootstrap-input.schema.json"


def read_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path}")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate the owner-controlled G10.2 bootstrap input.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()

    input_path = Path(args.input).resolve()
    data = read_json(input_path)
    schema = read_json(SCHEMA)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    errors = sorted(validator.iter_errors(data), key=lambda error: list(error.absolute_path))
    findings: list[str] = []
    for error in errors:
        location = "/".join(str(part) for part in error.absolute_path) or "$"
        findings.append(f"schema:{location}:{error.message}")

    project_ids = [
        data.get("platform_project_id", ""),
        data.get("staging_project_id", ""),
        data.get("production_project_id", ""),
    ]
    if len(set(project_ids)) != 3:
        findings.append("semantic:platform, staging and production project IDs must be distinct")
    forbidden_project_pattern = re.compile(r"(?i)(cn2526|course|student|emailteste)")
    for project_id in project_ids:
        if forbidden_project_pattern.search(str(project_id)):
            findings.append(f"semantic:course or unrelated project identifier is forbidden: {project_id}")

    state_bucket = str(data.get("terraform_state_bucket_name", ""))
    evidence_bucket = str(data.get("evidence_bucket_name", ""))
    if state_bucket == evidence_bucket:
        findings.append("semantic:Terraform state and evidence must use different buckets")
    for bucket in (state_bucket, evidence_bucket):
        if forbidden_project_pattern.search(bucket):
            findings.append(f"semantic:course identifier is forbidden in bucket name: {bucket}")

    repository_id = str(data.get("repository_id", ""))
    owner_id = str(data.get("repository_owner_id", ""))
    if repository_id.startswith("REPLACE_") or owner_id.startswith("REPLACE_"):
        findings.append("semantic:GitHub numeric IDs must be resolved before bootstrap")

    window = data.get("qualification_window") or {}
    try:
        starts_at = datetime.fromisoformat(str(window.get("starts_at", "")).replace("Z", "+00:00"))
        ends_at = datetime.fromisoformat(str(window.get("ends_at", "")).replace("Z", "+00:00"))
        if ends_at <= starts_at:
            findings.append("semantic:qualification window must end after it starts")
        if ends_at - starts_at > timedelta(days=7):
            findings.append("semantic:qualification window must not exceed seven days")
    except ValueError:
        if not errors:
            findings.append("semantic:qualification window timestamps are invalid")

    guardrails = data.get("cost_guardrails") or {}
    observed_credit = guardrails.get("observed_credit_usd")
    preserve_credit = guardrails.get("minimum_credit_to_preserve_usd")
    if isinstance(observed_credit, (int, float)) and isinstance(preserve_credit, (int, float)):
        if preserve_credit >= observed_credit:
            findings.append("semantic:minimum preserved credit must be lower than observed credit")

    execution = data.get("execution") or {}
    if execution.get("link_billing") and not execution.get("create_projects"):
        findings.append("semantic:link_billing requires create_projects in the first bootstrap run")
    dangerous = [
        "create_state_foundation",
        "create_delivery_control_plane",
        "create_data_plane",
        "create_edge",
        "materialize_generated_secrets",
    ]
    enabled = [name for name in dangerous if execution.get(name) is True]
    if enabled:
        findings.append("semantic:Phase 3 input must not enable resource creation: " + ", ".join(enabled))

    result = {
        "phase": "G10.2_BOOTSTRAP_INPUT",
        "status": "PASS" if not findings else "FAIL",
        "input": str(input_path),
        "repository": data.get("repository"),
        "default_branch": data.get("default_branch"),
        "project_ids": project_ids,
        "billing_account_id": data.get("billing_account_id"),
        "primary_region": data.get("primary_region"),
        "resource_creation_enabled": False,
        "findings": findings,
    }
    output = json.dumps(result, indent=2, ensure_ascii=False)
    print(output)
    if args.output:
        Path(args.output).write_text(output + "\n", encoding="utf-8")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
