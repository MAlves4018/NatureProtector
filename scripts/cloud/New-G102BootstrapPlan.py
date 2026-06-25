#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from Test_G102_import_helper import validate_input


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a non-executing G10.2 project/bootstrap plan.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    data = validate_input(Path(args.input))
    projects = [
        ("platform", data["platform_project_id"], "NatureProtector Platform"),
        ("staging", data["staging_project_id"], "NatureProtector Staging"),
        ("production", data["production_project_id"], "NatureProtector Production"),
    ]
    commands: list[str] = []
    for _, project_id, display_name in projects:
        commands.append(f'gcloud projects create "{project_id}" --name="{display_name}"')
        commands.append(
            f'gcloud billing projects link "{project_id}" --billing-account="{data["billing_account_id"]}"'
        )
    plan = {
        "schema_version": 1,
        "phase": "G10.2",
        "mode": "PLAN_ONLY",
        "repository": data["repository"],
        "default_branch": data["default_branch"],
        "primary_region": data["primary_region"],
        "projects": [
            {"role": role, "project_id": project_id, "display_name": display_name}
            for role, project_id, display_name in projects
        ],
        "billing_account_id": data["billing_account_id"],
        "state_bucket": data["terraform_state_bucket_name"],
        "evidence_bucket": data["evidence_bucket_name"],
        "commands_not_executed": commands,
        "gates": [
            "owner reviews globally unique project IDs",
            "active gcloud account matches expected_gcloud_account",
            "billing association permission is proved",
            "budget alerts are configured before any data plane",
            "Terraform plans and cost estimate are reviewed",
            "same-session teardown is rehearsed",
        ],
        "resource_creation": False,
        "data_plane_created": False,
    }
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(plan, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
