#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from Test_G102_import_helper import validate_input


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a non-executing G10.3 state/platform foundation plan.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-directory", required=True)
    args = parser.parse_args()

    data = validate_input(Path(args.input))
    output = Path(args.output_directory).resolve()
    output.mkdir(parents=True, exist_ok=True)

    state_tfvars = {
        "platform_project_id": data["platform_project_id"],
        "region": data["primary_region"],
        "state_bucket_name": data["terraform_state_bucket_name"],
        "state_retention_days": 30,
        "create_state_foundation": False,
        "owner_creation_confirmation": "",
    }
    platform_tfvars = {
        "platform_project_id": data["platform_project_id"],
        "staging_project_id": data["staging_project_id"],
        "production_project_id": data["production_project_id"],
        "region": data["primary_region"],
        "repository": data["repository"],
        "repository_id": data["repository_id"],
        "repository_owner_id": data["repository_owner_id"],
        "default_branch": data["default_branch"],
        "evidence_bucket_name": data["evidence_bucket_name"],
        "terraform_state_bucket_name": data["terraform_state_bucket_name"],
        "staging_cloud_deploy_worker_pool": f'projects/{data["staging_project_id"]}/locations/{data["primary_region"]}/workerPools/np-staging-deploy',
        "production_cloud_deploy_worker_pool": f'projects/{data["production_project_id"]}/locations/{data["primary_region"]}/workerPools/np-production-deploy',
        "staging_gke_node_service_account": f'np-staging-gke-nodes@{data["staging_project_id"]}.iam.gserviceaccount.com',
        "production_gke_node_service_account": f'np-production-gke-nodes@{data["production_project_id"]}.iam.gserviceaccount.com',
        "create_delivery_control_plane": False,
        "create_delivery_pipelines": False,
        "owner_creation_confirmation": "",
    }
    backend = f'bucket = "{data["terraform_state_bucket_name"]}"\nprefix = "platform"\n'

    write_json(output / "state-bootstrap.tfvars.json", state_tfvars)
    write_json(output / "platform.tfvars.json", platform_tfvars)
    (output / "platform.backend.hcl").write_text(backend, encoding="utf-8")

    plan = {
        "schema_version": 1,
        "phase": "G10.3",
        "mode": "PLAN_ONLY",
        "repository": data["repository"],
        "billing_account_id": data["billing_account_id"],
        "platform_project_id": data["platform_project_id"],
        "state_bucket_name": data["terraform_state_bucket_name"],
        "generated_files": [
            "state-bootstrap.tfvars.json",
            "platform.tfvars.json",
            "platform.backend.hcl",
        ],
        "safe_validation_commands": [
            "terraform -chdir=infra/gcp/terraform/g8-1-state-bootstrap fmt -check -recursive",
            "terraform -chdir=infra/gcp/terraform/g8-1-state-bootstrap init -backend=false -input=false",
            "terraform -chdir=infra/gcp/terraform/g8-1-state-bootstrap validate",
            f"terraform -chdir=infra/gcp/terraform/g8-1-state-bootstrap plan -input=false -var-file={output / 'state-bootstrap.tfvars.json'}",
            "terraform -chdir=infra/gcp/terraform/g8-1-platform fmt -check -recursive",
            "terraform -chdir=infra/gcp/terraform/g8-1-platform init -backend=false -input=false",
            "terraform -chdir=infra/gcp/terraform/g8-1-platform validate",
        ],
        "later_mutating_steps_not_authorized": [
            "enable create_state_foundation only after project and budget evidence pass",
            "terraform apply the reviewed state-bootstrap plan with exact owner confirmation",
            "reinitialize the platform root with platform.backend.hcl after the bucket exists",
            "keep create_delivery_control_plane=false until WIF/IAM and cost plan are separately approved",
        ],
        "gates": [
            "projects exist and billing links are proved",
            "project-specific budget alerts exist",
            "current credit is rechecked immediately before apply",
            "Terraform validate passes with the pinned provider",
            "state bucket name is globally available",
            "owner approves the exact state-bootstrap plan",
        ],
        "state_foundation_created": False,
        "delivery_control_plane_created": False,
        "data_plane_created": False,
        "deployment_proved": False,
    }
    write_json(output / "foundation-plan.json", plan)
    print(output / "foundation-plan.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
