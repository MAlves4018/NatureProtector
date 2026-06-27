#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ENV = ROOT / "infra/gcp/terraform/g8-1-environment"
PLATFORM = ROOT / "infra/gcp/terraform/g8-1-platform"
GKE_PATH = ENV / "gke.tf"
G9_PATH = ROOT / "scripts/cloud/Test-G9Convergence.py"

errors: list[str] = []
checks = 0


def check(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        errors.append(message)


cloud_sql = (ENV / "cloud_sql.tf").read_text(encoding="utf-8")
variables = (ENV / "variables.tf").read_text(encoding="utf-8")
staging = (ENV / "terraform.staging.tfvars").read_text(encoding="utf-8")
qualification = (
    ENV / "terraform.qualification.tfvars.example"
).read_text(encoding="utf-8")
example = (ENV / "terraform.tfvars.example").read_text(encoding="utf-8")
iam = (ENV / "iam.tf").read_text(encoding="utf-8")
services = (PLATFORM / "services.tf").read_text(encoding="utf-8")
versions = (PLATFORM / "versions.tf").read_text(encoding="utf-8")
providers = (PLATFORM / "providers.tf").read_text(encoding="utf-8")
g9 = G9_PATH.read_text(encoding="utf-8")
gke = GKE_PATH.read_text(encoding="utf-8")
np_script = (ROOT / "scripts/np.ps1").read_text(encoding="utf-8")

check(
    re.search(
        r"(?m)^\s*edition\s*=\s*var\.database_edition\s*$",
        cloud_sql,
    )
    is not None,
    "cloud-sql-edition-not-explicit",
)
check(
    'variable "database_edition"' in variables,
    "database-edition-variable-missing",
)
check(
    '"ENTERPRISE_PLUS"' in variables
    and '"ENTERPRISE"' in variables,
    "database-edition-validation-missing",
)

for name, content in [
    ("staging", staging),
    ("qualification", qualification),
    ("example", example),
]:
    check(
        re.search(
            r'(?m)^\s*database_edition\s*=\s*"ENTERPRISE"\s*$',
            content,
        )
        is not None,
        f"{name}-database-edition-missing",
    )

check(
    re.search(
        r'(?ms)^resource\s+"google_service_account_iam_member"'
        r'\s+"gke_workload_identity"\s*\{.*?'
        r'depends_on\s*=\s*\[google_container_cluster\.main\].*?^\}',
        iam,
    )
    is not None,
    "gke-workload-identity-cluster-dependency-missing",
)

check(
    'resource "google_project_service_identity" "cloud_deploy"'
    in services,
    "cloud-deploy-service-identity-resource-missing",
)
check(
    'service  = "clouddeploy.googleapis.com"' in services
    or 'service = "clouddeploy.googleapis.com"' in services,
    "cloud-deploy-service-identity-service-mismatch",
)
check(
    'google_project_service.platform["clouddeploy.googleapis.com"]'
    in services,
    "cloud-deploy-service-identity-api-dependency-missing",
)
check(
    re.search(
        r'(?ms)^resource\s+"google_project_service_identity"'
        r'\s+"cloud_deploy"\s*\{.*?'
        r'provider\s*=\s*google-beta',
        services,
    )
    is not None,
    "cloud-deploy-service-identity-not-bound-to-google-beta",
)

check(
    'source  = "hashicorp/google-beta"' in versions,
    "google-beta-required-provider-missing",
)
check(
    'version = "= 7.36.0"' in versions,
    "google-beta-version-not-pinned",
)
check(
    'provider "google-beta"' in providers,
    "google-beta-provider-configuration-missing",
)

check(
    "platform_hcl_root" in g9,
    "g9-platform-root-parser-split-missing",
)
check(
    "terraform-platform-hcl" in g9,
    "g9-terraform-platform-parser-missing",
)
check(
    'shutil.which("terraform")' in g9,
    "g9-terraform-cli-resolution-missing",
)
check(
    "secondary_hcl_files" in g9,
    "g9-secondary-hcl-parser-scope-missing",
)
check(
    'name="environment-remediation-static"' in np_script,
    "canonical-validation-registration-missing",
)

check(
    re.search(
        r"(?m)^\s*enable_private_endpoint\s*=\s*true\s*$",
        gke,
    ) is not None,
    "gke-private-endpoint-not-explicit",
)

payload = {
    "phase": "ENVIRONMENT_REMEDIATION_STATIC",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "errors": errors,
    "cloud_mutation": False,
    "terraform_apply_executed": False,
}

print(json.dumps(payload, indent=2))
raise SystemExit(0 if not errors else 1)
