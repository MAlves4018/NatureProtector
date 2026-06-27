#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]

COMMON_PATH = ROOT / "deploy/environments/common.json"
STAGING_PATH = ROOT / "deploy/environments/staging.json"
PRODUCTION_PATH = ROOT / "deploy/environments/production.json"

TFVARS_PATH = (
    ROOT
    / "infra/gcp/terraform/g8-1-environment"
    / "terraform.staging.tfvars"
)

VARIABLES_PATH = (
    ROOT
    / "infra/gcp/terraform/g8-1-environment"
    / "variables.tf"
)

SERVICES_PATH = (
    ROOT
    / "infra/gcp/terraform/g8-1-environment"
    / "services.tf"
)

IAM_PATH = (
    ROOT
    / "infra/gcp/terraform/g8-1-environment"
    / "iam.tf"
)

GKE_PATH = (
    ROOT
    / "infra/gcp/terraform/g8-1-environment"
    / "gke.tf"
)

CLOUD_DEPLOY_EXECUTION_PATH = (
    ROOT
    / "infra/gcp/terraform/g8-1-environment"
    / "cloud_deploy_execution.tf"
)

EXPECTED_AUTHORIZATION = (
    "AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H"
)

errors: list[str] = []
checks = 0


def check(condition: bool, message: str) -> None:
    global checks
    checks += 1

    if not condition:
        errors.append(message)


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(
        path.read_text(encoding="utf-8")
    )


def parse_scalar(value: str) -> Any:
    value = value.strip()

    if value.startswith('"') and value.endswith('"'):
        return json.loads(value)

    if value == "true":
        return True

    if value == "false":
        return False

    if value == "null":
        return None

    if re.fullmatch(r"-?\d+", value):
        return int(value)

    if re.fullmatch(r"-?\d+\.\d+", value):
        return float(value)

    if value == "[]":
        return []

    if value == "{}":
        return {}

    return value


def load_tfvars_scalars(path: Path) -> dict[str, Any]:
    result: dict[str, Any] = {}

    for original_line in path.read_text(
        encoding="utf-8"
    ).splitlines():
        line = original_line.strip()

        if not line or line.startswith("#"):
            continue

        match = re.fullmatch(
            r"([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)",
            line,
        )

        if not match:
            continue

        name = match.group(1)
        raw_value = match.group(2).strip()

        result[name] = parse_scalar(raw_value)

    return result


common = load_json(COMMON_PATH)
staging = load_json(STAGING_PATH)
production = load_json(PRODUCTION_PATH)

check(
    common.get("artifact_repository") == "np-releases",
    "common-artifact-repository-invalid",
)

check(
    staging.get("staging_tfvars")
    == (
        "infra/gcp/terraform/g8-1-environment/"
        "terraform.staging.tfvars"
    ),
    "staging-tfvars-path-invalid",
)

check(
    staging.get("project_id")
    == "natureprotector-500518",
    "staging-project-invalid",
)

check(
    staging.get("region") == "europe-southwest1",
    "staging-region-invalid",
)

check(
    staging.get("default_ttl_hours") == 4,
    "staging-ttl-invalid",
)

check(
    staging.get("budget_envelope_eur_month") == 20,
    "staging-budget-invalid",
)

check(
    staging.get("namespace")
    == "natureprotector-staging",
    "staging-namespace-invalid",
)

check(
    staging.get("deployable") is True,
    "staging-not-deployable",
)

for required_variable in [
    "GCP_G81_STAGING_CLUSTER_NAME",
    "GCP_G81_STAGING_CLOUD_SQL_INSTANCE",
    "GCP_G82_EVIDENCE_BUCKET",
]:
    check(
        required_variable
        in staging.get("required_variables", []),
        f"staging-runtime-variable-missing:{required_variable}",
    )

check(
    production.get("deployable") is False,
    "production-must-remain-locked",
)

check(
    production.get("guards", {}).get("allow_apply")
    is False,
    "production-apply-must-remain-disabled",
)

check(
    TFVARS_PATH.is_file(),
    "staging-tfvars-missing",
)

tfvars_text = TFVARS_PATH.read_text(
    encoding="utf-8"
)

check(
    "REPLACE_WITH" not in tfvars_text,
    "staging-tfvars-placeholder-found",
)

check(
    "np-staging-REPLACE" not in tfvars_text,
    "staging-project-placeholder-found",
)

check(
    EXPECTED_AUTHORIZATION not in tfvars_text,
    "authorization-must-not-be-committed-in-tfvars",
)

tfvars = load_tfvars_scalars(TFVARS_PATH)

expected_values: dict[str, Any] = {
    "project_id": "natureprotector-500518",
    "platform_project_id": "natureprotector-500518",
    "environment": "staging",
    "region": "europe-southwest1",
    "cluster_name": "np-staging",
    "runtime_namespace": "natureprotector-staging",
    "database_instance_name": "np-staging-postgres",
    "database_tier": "db-f1-micro",
    "database_availability_type": "ZONAL",
    "database_disk_type": "PD_HDD",
    "workflow_deployer_service_account": (
        "np-cd-deploy@natureprotector-500518."
        "iam.gserviceaccount.com"
    ),
    "cloud_deploy_execution_service_account": (
        "np-deploy-staging@natureprotector-500518."
        "iam.gserviceaccount.com"
    ),
}

for name, expected in expected_values.items():
    actual = tfvars.get(name)

    check(
        actual == expected,
        (
            f"staging-tfvars-invalid:{name}:"
            f"expected={expected!r}:actual={actual!r}"
        ),
    )

expected_booleans = {
    "create_data_plane": True,
    "create_edge": False,
    "materialize_generated_secrets": True,
    "database_backup_enabled": False,
    "database_pitr_enabled": False,
    "deletion_protection": False,
}

for name, expected in expected_booleans.items():
    actual = tfvars.get(name)

    check(
        actual is expected,
        (
            f"staging-tfvars-invalid:{name}:"
            f"expected={expected!r}:actual={actual!r}"
        ),
    )

check(
    tfvars.get("database_disk_size_gb") == 10,
    "staging-database-disk-size-invalid",
)

check(
    tfvars.get("database_retained_backups") == 1,
    "staging-database-retention-invalid",
)

check(
    tfvars.get("secret_generation") == 1,
    "staging-secret-generation-invalid",
)

variables_text = VARIABLES_PATH.read_text(
    encoding="utf-8"
)

check(
    EXPECTED_AUTHORIZATION in variables_text,
    "environment-authorization-contract-missing",
)

check(
    (
        "OWNER_APPROVES_NEW_NON_CN_GCP_PROJECTS_AFTER_G10"
        not in variables_text
    ),
    "stale-environment-authorization-contract",
)

services_text = SERVICES_PATH.read_text(encoding="utf-8")
iam_text = IAM_PATH.read_text(encoding="utf-8")
gke_text = GKE_PATH.read_text(encoding="utf-8")
cloud_deploy_execution_text = (
    CLOUD_DEPLOY_EXECUTION_PATH.read_text(encoding="utf-8")
)

check(
    'resource "google_project_service"' not in services_text,
    "environment-project-services-must-be-platform-owned",
)

check(
    "google_project_service.required" not in gke_text,
    "gke-must-not-depend-on-environment-project-services",
)

check(
    "google_project_service.required"
    not in cloud_deploy_execution_text,
    "worker-pool-must-not-depend-on-environment-project-services",
)

check(
    'resource "google_project_iam_member" "workflow_environment_roles"'
    not in iam_text,
    "workflow-project-roles-must-be-platform-owned",
)

check(
    'mode               = "BASIC"' in gke_text,
    "gke-security-posture-mode-invalid",
)

check(
    'vulnerability_mode = "VULNERABILITY_DISABLED"' in gke_text,
    "removed-gke-vulnerability-scanning-must-be-disabled",
)

check(
    "VULNERABILITY_ENTERPRISE" not in gke_text,
    "removed-gke-vulnerability-scanning-still-present",
)

result = {
    "phase": "STANDARD_STAGING_CONFIGURATION",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "errors": errors,
    "cloud_mutation": False,
    "terraform_apply_executed": False,
    "production_authorized": False,
    "production_deployed": False,
}

print(
    json.dumps(
        result,
        indent=2,
    )
)

raise SystemExit(0 if not errors else 1)
