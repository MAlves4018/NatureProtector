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
postgres_data_source_factory = (
    ROOT
    / "src/NatureProtector.Infrastructure.Postgres/Configuration/PostgresDataSourceFactory.cs"
).read_text(encoding="utf-8")
postgres_service_collection = (
    ROOT
    / "src/NatureProtector.Infrastructure.Postgres/DependencyInjection/ServiceCollectionExtensions.cs"
).read_text(encoding="utf-8")
postgres_migration_settings = (
    ROOT / "src/NatureProtector.Postgres.Migrations/MigrationSettings.cs"
).read_text(encoding="utf-8")
postgres_migration_runner = (
    ROOT / "src/NatureProtector.Postgres.Migrations/PostgresMigrationRunner.cs"
).read_text(encoding="utf-8")
autopilot_foundation = (
    ROOT / "scripts/cloud/install-g81-cluster-dependencies-autopilot.sh"
)
autopilot_runner = (
    ROOT / "scripts/cloud/complete-staging-after-autopilot-remediation.sh"
)
autopilot_deploy = ROOT / "scripts/cloud/Deploy-G81Staging-Autopilot.ps1"
operator_lock = ROOT / "infra/gcp/kubernetes/g8-1/operator-lock.json"

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
    autopilot_foundation.is_file(),
    "autopilot-operator-foundation-script-missing",
)
check(
    autopilot_runner.is_file(),
    "autopilot-staging-runner-script-missing",
)
check(
    autopilot_deploy.is_file(),
    "autopilot-staging-deploy-script-missing",
)

if autopilot_foundation.is_file():
    autopilot_foundation_text = autopilot_foundation.read_text(
        encoding="utf-8"
    )
    check(
        "gcloud builds submit" in autopilot_foundation_text
        and "logsBucket" in autopilot_foundation_text
        and "CLOUD_BUILD_MIRROR_STATUS" in autopilot_foundation_text,
        "autopilot-foundation-cloud-build-mirror-missing",
    )
    check(
        "$(date -u +%Y%m%d%H%M%S)" in autopilot_foundation_text
        and "%Y%m%dT%H%M%SZ" not in autopilot_foundation_text,
        "autopilot-foundation-mirror-namespace-not-lowercase",
    )
    check(
        "<<'PY' || probe_rc=$?" in autopilot_foundation_text
        and "raise SystemExit(1)" in autopilot_foundation_text,
        "autopilot-foundation-readiness-probe-exit-code-not-captured",
    )
    check(
        "OPERATOR_CLEAN_REINSTALL_STARTED" in autopilot_foundation_text
        and "kubectl -n cert-manager delete deployment" in autopilot_foundation_text,
        "autopilot-foundation-clean-reinstall-missing",
    )
    check(
        "keda-metrics-apiserver" in autopilot_foundation_text
        and "keda-admission" in autopilot_foundation_text
        and "keda-operator-metrics-apiserver" not in autopilot_foundation_text
        and "keda-admission-webhooks" not in autopilot_foundation_text,
        "autopilot-foundation-keda-deployment-names-stale",
    )

if operator_lock.is_file():
    lock = json.loads(operator_lock.read_text(encoding="utf-8"))
    keda_entries = [
        item for item in lock.get("dependencies", [])
        if item.get("name") == "keda"
    ]
    keda_rollouts = set(keda_entries[0].get("rollouts", [])) if keda_entries else set()
    check(
        {
            "deployment/keda-operator",
            "deployment/keda-metrics-apiserver",
            "deployment/keda-admission",
        }.issubset(keda_rollouts)
        and "deployment/keda-operator-metrics-apiserver" not in keda_rollouts
        and "deployment/keda-admission-webhooks" not in keda_rollouts,
        "operator-lock-keda-deployment-names-stale",
    )

if autopilot_runner.is_file():
    autopilot_runner_text = autopilot_runner.read_text(encoding="utf-8")
    historical_sha = (
        "d70dd7437cc6c1f1748bd61188ce363b207"
        "ea4cd"
    )
    historical_run_id = "283055" + "59246"
    historical_artifact_digest = (
        "sha256:8b16593c65e1866d83d92fc95aa63dc6b"
        "3dc9ffa37c7e8aae1419f2dbc03c443"
    )
    check(
        "GIT_DIRTY_CANONICAL_CHANGES_ACCEPTED" in autopilot_runner_text
        and "status-porcelain-before.txt" in autopilot_runner_text,
        "autopilot-runner-bounded-dirty-gate-missing",
    )
    check(
        "managed resources: expected 53" in autopilot_runner_text
        and "data resources: expected 3" in autopilot_runner_text,
        "autopilot-runner-foundation-resource-count-mismatch",
    )
    check(
        "install-g81-cluster-dependencies-autopilot.sh" in autopilot_runner_text
        and "Deploy-G81Staging-Autopilot.ps1" in autopilot_runner_text,
        "autopilot-runner-canonical-script-links-missing",
    )
    check(
        "OPERATOR_FOUNDATION_ALREADY_READY" in autopilot_runner_text
        and "rollout status deployment/cert-manager" in autopilot_runner_text
        and "rollout status deployment/keda-operator" in autopilot_runner_text
        and "rollout status deployment/rabbitmq-cluster-operator" in autopilot_runner_text,
        "autopilot-runner-healthy-operator-reuse-missing",
    )
    check(
        historical_sha not in autopilot_runner_text,
        "autopilot-runner-historical-head-hardcoded",
    )
    check(
        historical_run_id not in autopilot_runner_text,
        "autopilot-runner-historical-release-run-id-hardcoded",
    )
    check(
        historical_artifact_digest not in autopilot_runner_text,
        "autopilot-runner-historical-artifact-digest-hardcoded",
    )
    check(
        "SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED" in autopilot_runner_text
        and "source_commit\") != expected_head" in autopilot_runner_text
        and "gh attestation verify" in autopilot_runner_text
        and "manifest-attestation-verification.txt" in autopilot_runner_text,
        "autopilot-runner-current-head-release-resolution-missing",
    )
    check(
        'RELEASE_ARTIFACT="g81-release"' in autopilot_runner_text
        and "standard-cd-release" not in autopilot_runner_text,
        "autopilot-runner-release-artifact-name-mismatch",
    )
    check(
        "CLOUD_SQL_CA_SECRET_VERSION_ADDED" in autopilot_runner_text
        and "latest_enabled_secret_version" in autopilot_runner_text
        and '"CLOUD_SQL_CA_VERSION": "1"' not in autopilot_runner_text,
        "autopilot-runner-cloud-sql-ca-rotation-missing",
    )
    check(
        "NP_EXPECTED_HEAD" in np_script
        and "git -C $RepoRoot rev-parse HEAD" in np_script,
        "np-cloud-up-current-head-interface-missing",
    )

if autopilot_deploy.is_file():
    autopilot_deploy_text = autopilot_deploy.read_text(encoding="utf-8")
    check(
        "NP_G81_OPERATORS_READY" in autopilot_deploy_text
        and "Autopilot-aware operator foundation" in autopilot_deploy_text,
        "autopilot-deploy-operator-evidence-gate-missing",
    )

check(
    "complete-staging-after-autopilot-remediation.sh" in np_script
    and "NP_CONFIRM_STAGING_RESUME" in np_script
    and '"cloud"' in np_script,
    "canonical-cloud-up-autopilot-entrypoint-missing",
)

check(
    re.search(
        r"(?m)^\s*enable_private_endpoint\s*=\s*true\s*$",
        gke,
    ) is not None,
    "gke-private-endpoint-not-explicit",
)
check(
    "UseRootCertificate" in postgres_data_source_factory
    and "X509CertificateLoader.LoadCertificateFromFile" in postgres_data_source_factory,
    "postgres-datasource-root-certificate-builder-missing",
)
check(
    "UseSslClientAuthenticationOptionsCallback" in postgres_data_source_factory
    and "X509ChainTrustMode.CustomRootTrust" in postgres_data_source_factory
    and "RemoteCertificateValidationCallback" in postgres_data_source_factory,
    "postgres-datasource-verifyca-custom-root-validation-missing",
)
check(
    "AddSingleton<NpgsqlDataSource>" in postgres_service_collection
    and "BuildDataSource()" in postgres_service_collection,
    "postgres-runtime-datasource-registration-missing",
)
check(
    "BuildAdminDataSource" in postgres_migration_settings
    and "PostgresDataSourceFactory.Build" in postgres_migration_settings,
    "postgres-migration-datasource-builder-missing",
)
check(
    "BuildAdminDataSource()" in postgres_migration_runner
    and ".UseNpgsql(dataSource)" in postgres_migration_runner,
    "postgres-migration-runner-datasource-missing",
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
