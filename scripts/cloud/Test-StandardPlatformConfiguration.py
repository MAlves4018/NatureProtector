#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]

PLATFORM_ROOT = (
    ROOT
    / "infra/gcp/terraform/g8-1-platform"
)

TFVARS_PATH = PLATFORM_ROOT / "terraform.staging.tfvars"
VARIABLES_PATH = PLATFORM_ROOT / "variables.tf"

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


terraform_files = sorted(PLATFORM_ROOT.glob("*.tf"))

check(
    len(terraform_files) == 9,
    f"unexpected-platform-tf-file-count:{len(terraform_files)}",
)

terraform_text = "\n".join(
    path.read_text(encoding="utf-8")
    for path in terraform_files
)

terraform_cli = shutil.which("terraform")

check(
    terraform_cli is not None,
    "terraform-cli-missing",
)

if terraform_cli is not None:
    fmt_result = subprocess.run(
        [
            terraform_cli,
            "fmt",
            "-check",
            "-recursive",
            str(PLATFORM_ROOT),
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    fmt_output = (
        fmt_result.stdout
        + "\n"
        + fmt_result.stderr
    ).strip().replace("\n", " | ")

    check(
        fmt_result.returncode == 0,
        (
            "terraform-official-parser-failed:"
            f"{fmt_output}"
        ),
    )

variables_text = VARIABLES_PATH.read_text(
    encoding="utf-8"
)

tfvars_text = TFVARS_PATH.read_text(
    encoding="utf-8"
)

for forbidden in [
    "production_project_id",
    "production_cluster_name",
    "production_cloud_deploy",
    "run_production",
    "gke_production",
    "np-production",
    'resource "google_iam_workload_identity_pool"',
    'resource "google_iam_workload_identity_pool_provider"',
    'resource "google_artifact_registry_repository" "images"',
    "roles/owner",
    "roles/editor",
    "roles/logging.configWriter",
    "OWNER_APPROVES_NEW_NON_CN_GCP_PROJECTS_AFTER_G10",
]:
    check(
        forbidden not in terraform_text,
        f"forbidden-platform-token:{forbidden}",
    )

for required in [
    'name             = "np-run-staging"',
    'name             = "np-gke-staging"',
    'name        = "natureprotector-api"',
    'name        = "natureprotector-frontend"',
    'name        = "natureprotector-prevention"',
    'account_id   = "np-deploy-staging"',
    '"roles/clouddeploy.jobRunner"',
    '"roles/clouddeploy.admin"',
    '"deploy_pipeline_roles"',
    '"roles/cloudbuild.workerPoolOwner"',
    '"roles/serviceusage.serviceUsageAdmin"',
    '"roles/resourcemanager.projectIamAdmin"',
    '"roles/run.admin"',
    '"roles/logging.viewer"',
    'data "google_artifact_registry_repository" "images"',
    'resource "google_storage_bucket" "g82_evidence"',
    'data "google_storage_bucket" "cloud_build_logs"',
    "cloud_build_logs_bucket_name",
    '"np-cloudbuild-logs-22505444922"',
    '"roles/storage.objectAdmin"',
    '"roles/storage.bucketViewer"',
    "retention_period = 31536000",
    'public_access_prevention    = "enforced"',
    "private_pool",
]:
    check(
        required in terraform_text,
        f"missing-platform-token:{required}",
    )

for required_service in [
    "artifactregistry.googleapis.com",
    "binaryauthorization.googleapis.com",
    "cloudbuild.googleapis.com",
    "clouddeploy.googleapis.com",
    "cloudtrace.googleapis.com",
    "compute.googleapis.com",
    "container.googleapis.com",
    "containeranalysis.googleapis.com",
    "dns.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "logging.googleapis.com",
    "monitoring.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "servicenetworking.googleapis.com",
    "serviceusage.googleapis.com",
    "sqladmin.googleapis.com",
    "sts.googleapis.com",
]:
    check(
        f'"{required_service}"' in terraform_text,
        f"missing-platform-service:{required_service}",
    )

check(
    EXPECTED_AUTHORIZATION in variables_text,
    "platform-authorization-contract-missing",
)

check(
    EXPECTED_AUTHORIZATION not in tfvars_text,
    "platform-authorization-must-not-be-committed",
)

for required in [
    'platform_project_id = "natureprotector-500518"',
    'staging_project_id  = "natureprotector-500518"',
    'artifact_repository_id       = "np-releases"',
    (
        'terraform_state_bucket_name  = '
        '"np-tfstate-migkxl-202606"'
    ),
    (
        'g82_evidence_bucket_name     = '
        '"np-g82-evidence-22505444922"'
    ),
    (
        'cloud_build_logs_bucket_name = '
        '"np-cloudbuild-logs-22505444922"'
    ),
    (
        'deploy_service_account_email = '
        '"np-cd-deploy@natureprotector-500518.'
        'iam.gserviceaccount.com"'
    ),
    "create_delivery_control_plane = true",
    "create_delivery_pipelines     = true",
]:
    check(
        required in tfvars_text,
        f"missing-platform-tfvars-token:{required}",
    )


RUN_PARAMETER_KEYS = {
    "api_internal_origin",
    "api_max_scale",
    "api_min_scale",
    "api_service_account_email",
    "cloud_sql_ca_secret",
    "cloud_sql_ca_version",
    "cloud_sql_private_ip",
    "frontend_max_scale",
    "frontend_min_scale",
    "frontend_service_account_email",
    "jwt_signing_key_secret",
    "jwt_signing_key_version",
    "otel_endpoint",
    "postgres_app_password_secret",
    "postgres_app_password_version",
    "rabbitmq_app_password_secret",
    "rabbitmq_app_password_version",
    "rabbitmq_app_username_secret",
    "rabbitmq_app_username_version",
    "rabbitmq_ca_secret",
    "rabbitmq_ca_version",
    "rabbitmq_private_host",
    "rabbitmq_tls_server_name",
    "runtime_project_id",
    "runtime_region",
}

GKE_PARAMETER_KEYS = {
    "cloud_sql_private_cidr",
    "cloud_sql_private_ip",
    "otel_gsa",
    "otel_load_balancer_ip",
    "prevention_gsa",
    "rabbitmq_load_balancer_ip",
    "rabbitmq_tls_server_name",
    "runtime_subnet_cidr",
    "secret_sync_gsa",
}


def parameter_keys(block_name: str, end_marker: str | None = None) -> set[str]:
    marker = f"{block_name} = {{"
    check(marker in tfvars_text, f"missing-parameter-map:{block_name}")
    if marker not in tfvars_text:
        return set()

    block = tfvars_text.split(marker, 1)[1]
    if end_marker is not None and end_marker in block:
        block = block.split(end_marker, 1)[0]

    return set(
        re.findall(
            r"(?m)^\s{2}([a-z0-9_]+)\s*=",
            block,
        )
    )


run_parameter_keys = parameter_keys(
    "staging_run_deploy_parameters",
    "staging_gke_deploy_parameters = {",
)
gke_parameter_keys = parameter_keys(
    "staging_gke_deploy_parameters",
)

check(
    run_parameter_keys == RUN_PARAMETER_KEYS,
    "run-parameter-contract-mismatch:"
    f"missing={sorted(RUN_PARAMETER_KEYS - run_parameter_keys)}:"
    f"unexpected={sorted(run_parameter_keys - RUN_PARAMETER_KEYS)}",
)
check(
    gke_parameter_keys == GKE_PARAMETER_KEYS,
    "gke-parameter-contract-mismatch:"
    f"missing={sorted(GKE_PARAMETER_KEYS - gke_parameter_keys)}:"
    f"unexpected={sorted(gke_parameter_keys - GKE_PARAMETER_KEYS)}",
)

for forbidden_secret_material in [
    "BEGIN PRIVATE KEY",
    "BEGIN RSA PRIVATE KEY",
    "BEGIN CERTIFICATE",
]:
    check(
        forbidden_secret_material not in tfvars_text,
        f"secret-material-in-platform-tfvars:{forbidden_secret_material}",
    )


check(
    "REPLACE_WITH" not in tfvars_text,
    "platform-tfvars-placeholder-found",
)

check(
    "np-platform-REPLACE" not in tfvars_text,
    "platform-project-placeholder-found",
)

result = {
    "phase": "STANDARD_PLATFORM_CONFIGURATION",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "errors": errors,
    "single_project": True,
    "staging_only": True,
    "existing_wif_reused": True,
    "existing_artifact_repository_reused": True,
    "cloud_mutation": False,
    "terraform_apply_executed": False,
    "production_authorized": False,
    "production_deployed": False,
    "delivery_pipelines_enabled": True,
    "run_parameter_count": len(run_parameter_keys),
    "gke_parameter_count": len(gke_parameter_keys),
}

print(json.dumps(result, indent=2))

raise SystemExit(0 if not errors else 1)
