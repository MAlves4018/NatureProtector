#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

import yaml
from jsonschema import Draft202012Validator

from g8_state_evidence import load_required_json, validate_g8_state_document

try:
    import hcl2
except ImportError:  # pragma: no cover
    hcl2 = None

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []
checks = 0


def check(condition: bool, failure: str) -> None:
    global checks
    checks += 1
    if not condition:
        errors.append(failure)


REQUIRED = [
    "docs/decisions/ADR-G8-1-production-cloud-and-cd.md",
    "docs/implementation/cloud/g8-1-cloud-production-architecture-cd-hardening.md",
    "docs/security/g8-1-edge-runtime-supply-chain.md",
    "docs/operations/g8-1-cd-and-rollout-runbook.md",
    "docs/operations/g8-1-one-week-production-runbook.md",
    "docs/evidence/g8-1-source-references.md",
    "docs/evidence/g8-1-implementation-evidence-2026-06-20.md",
    "docs/evidence/g8-1-state.json",
    "infra/gcp/contracts/g8-1-release-manifest.schema.json",
    "infra/gcp/contracts/g8-1-release-manifest.example.json",
    "infra/gcp/production/g8-1-cloud-architecture-policy.json",
    "infra/gcp/production/g8-1-rate-limit-policy.json",
    "infra/gcp/production/g8-1-scaling-policy.json",
    "infra/gcp/production/g8-1-cd-policy.json",
    "infra/gcp/production/g8-1-one-week-runtime-policy.json",
    "infra/gcp/terraform/g8-1-state-bootstrap/state.tf",
    "infra/gcp/terraform/g8-1-state-bootstrap/versions.tf",
    "infra/gcp/terraform/g8-1-platform/cloud_deploy.tf",
    "infra/gcp/terraform/g8-1-platform/identity.tf",
    "infra/gcp/terraform/g8-1-environment/cloud_deploy_execution.tf",
    "infra/gcp/terraform/g8-1-environment/dns.tf",
    "infra/gcp/terraform/g8-1-environment/edge.tf",
    "infra/gcp/terraform/g8-1-environment/gke.tf",
    "infra/gcp/terraform/g8-1-environment/generated_secrets.tf",
    "infra/gcp/terraform/g8-1-environment/cloud_sql.tf",
    "infra/gcp/terraform/g8-1-environment/iam.tf",
    "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml",
    "infra/gcp/kubernetes/g8-1/operator-lock.json",
    "infra/gcp/kubernetes/g8-1/base/rabbitmq.yaml",
    "infra/gcp/kubernetes/g8-1/base/network-policy.yaml",
    "infra/gcp/kubernetes/g8-1/base/otel-collector.yaml",
    "infra/gcp/cloud-deploy/g8-1/api/service.yaml",
    "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml",
    "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml",
    "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml",
    "infra/gcp/cloud-deploy-verifier/Dockerfile",
    "src/NatureProtector.Backoffice.Api/Configuration/ApiRateLimitingOptions.cs",
    "src/NatureProtector.Backoffice.Api/Configuration/ApiRateLimitingExtensions.cs",
    "src/NatureProtector.Backoffice.Api/RuntimeOrchestration/CloudRunExecutionStore.cs",
    "src/NatureProtector.Backoffice.Api/RuntimeOrchestration/CloudRunJobsGateway.cs",
    "src/NatureProtector.Backoffice.Api/RuntimeOrchestration/CloudRunJobRuntimeRunOrchestrator.cs",
    "src/NatureProtector.Infrastructure.Postgres/Migrations/20260620190000_AddRuntimeOrchestratorExecutions.cs",
    "src/NatureProtector.Shared/Configuration/PrivateCertificateAuthorityValidator.cs",
    "tests/NatureProtector.Backoffice.Api.Tests/RateLimitingTests.cs",
    "tests/NatureProtector.Shared.Tests/PrivateCertificateAuthorityValidatorTests.cs",
    ".github/workflows/gcp-g8-1-production-policy.yml",
    ".github/workflows/gcp-g8-1-release.yml",
    ".github/workflows/gcp-g8-1-deploy-staging.yml",
    ".github/workflows/gcp-g8-1-promote-production.yml",
    ".github/workflows/gcp-g8-1-teardown.yml",
    "scripts/cloud/Build-G81Release.sh",
    "scripts/cloud/Install-G81ClusterDependencies.ps1",
    "scripts/cloud/Deploy-G81RuntimeJobs.ps1",
    "scripts/cloud/Invoke-G81FunctionalSmoke.ps1",
    "scripts/cloud/Deploy-G81Staging.ps1",
    "scripts/cloud/Promote-G81Production.ps1",
    "scripts/cloud/New-G81ReleaseManifest.py",
    "scripts/cloud/Test-G81ReleaseManifest.py",
    "scripts/cloud/Test-LocalCloudConfigurationContract.py",
    "scripts/cloud/requirements-validation.txt",
    "infra/gcp/terraform/g8-1-environment/terraform.qualification.tfvars.example",
    "infra/gcp/kubernetes/g8-1/overlays/staging/README.md",
    "scripts/cloud/Invoke-G81OwnerGate.ps1",
    "scripts/cloud/Remove-G81WeekEnvironment.ps1",
]
for relative in REQUIRED:
    check((ROOT / relative).is_file(), f"missing:{relative}")

json_files = sorted((ROOT / "infra/gcp/production").glob("g8-1-*.json")) + [
    ROOT / "infra/gcp/contracts/g8-1-release-manifest.schema.json",
    ROOT / "infra/gcp/contracts/g8-1-release-manifest.example.json",
    ROOT / "docs/evidence/g8-1-state.json",
    ROOT / "infra/gcp/kubernetes/g8-1/operator-lock.json",
]
parsed_json: dict[Path, object] = {}
for path in json_files:
    result = load_required_json(path, ROOT)
    if result.error is not None:
        check(False, result.error)
        continue

    parsed_json[path] = result.data
    check(True, "")

state_path = ROOT / "docs/evidence/g8-1-state.json"
if state_path in parsed_json:
    for issue in validate_g8_state_document(parsed_json[state_path], "G8.1"):
        check(False, issue)

schema_path = ROOT / "infra/gcp/contracts/g8-1-release-manifest.schema.json"
example_path = ROOT / "infra/gcp/contracts/g8-1-release-manifest.example.json"
if schema_path in parsed_json and example_path in parsed_json:
    issues = list(Draft202012Validator(parsed_json[schema_path]).iter_errors(parsed_json[example_path]))
    check(not issues, "manifest-example:" + "; ".join(issue.message for issue in issues))

yaml_files = sorted((ROOT / "infra/gcp/kubernetes/g8-1").rglob("*.yaml"))
yaml_files += sorted((ROOT / "infra/gcp/cloud-deploy/g8-1").rglob("*.yaml"))
yaml_files += sorted((ROOT / ".github/workflows").glob("gcp-g8-1-*.yml"))
for path in yaml_files:
    try:
        list(yaml.safe_load_all(path.read_text(encoding="utf-8")))
        check(True, "")
    except Exception as exc:  # noqa: BLE001
        check(False, f"yaml:{path.relative_to(ROOT)}:{exc}")

state_bootstrap_hcl_files = sorted(
    (ROOT / "infra/gcp/terraform/g8-1-state-bootstrap").glob("*.tf")
)
platform_hcl_root = ROOT / "infra/gcp/terraform/g8-1-platform"
platform_hcl_files = sorted(platform_hcl_root.glob("*.tf"))
environment_hcl_files = sorted(
    (ROOT / "infra/gcp/terraform/g8-1-environment").glob("*.tf")
)
hcl_files = (
    state_bootstrap_hcl_files
    + platform_hcl_files
    + environment_hcl_files
)

# python-hcl2 remains the lightweight parser for the unchanged Terraform
# roots. The platform root uses valid Terraform conditional expressions that
# this secondary parser rejects, so Terraform itself parses that root.
check(hcl2 is not None, "hcl2-module-missing")
if hcl2 is not None:
    for path in state_bootstrap_hcl_files + environment_hcl_files:
        try:
            with path.open("r", encoding="utf-8") as handle:
                hcl2.load(handle)
            check(True, "")
        except Exception as exc:  # noqa: BLE001
            check(False, f"hcl:{path.relative_to(ROOT)}:{exc}")

terraform_cli = shutil.which("terraform")
check(terraform_cli is not None, "terraform-cli-missing-for-platform-hcl")
if terraform_cli is not None:
    terraform_parse = subprocess.run(
        [
            terraform_cli,
            "fmt",
            "-check",
            "-recursive",
            str(platform_hcl_root),
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    parser_output = (
        terraform_parse.stdout + "\n" + terraform_parse.stderr
    ).strip().replace("\n", " | ")
    check(
        terraform_parse.returncode == 0,
        f"terraform-platform-hcl:{parser_output}",
    )

scope_paths = [ROOT / item for item in REQUIRED if (ROOT / item).is_file()]
scope_paths += hcl_files + yaml_files + json_files
scope_text = "\n".join(path.read_text(encoding="utf-8", errors="ignore") for path in scope_paths)
for token in [
    "OWNER_APPROVES_NEW_NON_CN_GCP_PROJECTS_AFTER_G10",
    "MAlves4018/NatureProtector",
    "europe-southwest1",
    "internal-and-cloud-load-balancing",
    "rate_based_ban",
    "evaluatePreconfiguredWaf",
    "kind: ScaledObject",
    "type: rabbitmq",
    "kind: TriggerAuthentication",
    "kind: PodDisruptionBudget",
    "default_queue_type = quorum",
    "point_in_time_recovery_enabled",
    "single-project-staging-only",
    "np-deploy-staging",
    "np-releases",
    "AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H",
    "Status429TooManyRequests",
    "CloudRunJob",
    "roles/run.jobsExecutorWithOverrides",
    "roles/cloudbuild.workerPoolUser",
    "roles/clouddeploy.jobRunner",
    "roles/artifactregistry.reader",
    "roles/container.defaultNodeServiceAccount",
    "serverless-robot-prod.iam.gserviceaccount.com",
    "create_delivery_pipelines",
    "rabbitmq.staging.natureprotector.internal",
    "cloud-deploy-verifier",
    "I_ACCEPT_FIRST_RELEASE_HAS_NO_CANARY_BASELINE",
]:
    check(token in scope_text, f"guardrail-missing:{token}")

deployable_paths = hcl_files + yaml_files + [
    ROOT / ".github/workflows/gcp-g8-1-release.yml",
    ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml",
    ROOT / ".github/workflows/gcp-g8-1-promote-production.yml",
    ROOT / ".github/workflows/gcp-g8-1-teardown.yml",
    ROOT / "scripts/cloud/Build-G81Release.sh",
    ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1",
    ROOT / "scripts/cloud/Deploy-G81RuntimeJobs.ps1",
    ROOT / "scripts/cloud/Deploy-G81Staging.ps1",
    ROOT / "scripts/cloud/Promote-G81Production.ps1",
    ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1",
]
deployable_text = "\n".join(path.read_text(encoding="utf-8", errors="ignore") for path in deployable_paths if path.is_file())
for forbidden in [
    "-".join(["0109B8", "93144E", "B93C1C"]),
    "cn2526-t4-g04",
    "CN2526-T4-G04-billacc",
    "roles/owner",
    "roles/editor",
    "google_service_account_key",
    "secret_data = ",
    'pass' + 'word = "',
    'required_version = "= 1.15.6"',
]:
    check(forbidden.lower() not in deployable_text.lower(), f"forbidden:{forbidden}")

for workflow in sorted((ROOT / ".github/workflows").glob("gcp-g8-1-*.yml")):
    text = workflow.read_text(encoding="utf-8")
    for match in re.finditer(r"uses:\s*([^\s#]+)", text):
        value = match.group(1)
        if value.startswith("./"):
            continue
        check(bool(re.search(r"@[0-9a-f]{40}$", value)), f"unpinned-action:{workflow.name}:{value}")

for example in [
    ROOT / "infra/gcp/terraform/g8-1-state-bootstrap/terraform.tfvars.example",
    ROOT / "infra/gcp/terraform/g8-1-platform/terraform.tfvars.example",
    ROOT / "infra/gcp/terraform/g8-1-environment/terraform.tfvars.example",
]:
    text = example.read_text(encoding="utf-8")
    check(not re.search(r"create_(state_foundation|delivery_control_plane|delivery_pipelines|data_plane)\s*=\s*true", text), f"unsafe-default:{example.relative_to(ROOT)}")

semantic_checks = {
    "api-cloud-run-job-mode": (ROOT / "infra/gcp/cloud-deploy/g8-1/api/service.yaml", "RuntimeOrchestration__Mode, value: CloudRunJob"),
    "durable-orchestrator-table": (ROOT / "src/NatureProtector.Infrastructure.Postgres/Migrations/20260620190000_AddRuntimeOrchestratorExecutions.cs", "runtime_orchestrator_executions"),
    "orchestrator-provider-id-matches-store-id": (ROOT / "src/NatureProtector.Backoffice.Api/RuntimeOrchestration/CloudRunJobsGateway.cs", "executionId.Value.ToString"),
    "orchestrator-timeout-is-terminal": (ROOT / "src/NatureProtector.Backoffice.Api/RuntimeOrchestration/CloudRunJobRuntimeRunOrchestrator.cs", "cloud_run_execution_timed_out"),
    "orchestrator-lease-attach-is-checked": (ROOT / "src/NatureProtector.Backoffice.Api/RuntimeOrchestration/CloudRunJobRuntimeRunOrchestrator.cs", "if (!attached)"),
    "private-dns-zone": (ROOT / "infra/gcp/terraform/g8-1-environment/dns.tf", "natureprotector.internal."),
    "private-worker-pool": (ROOT / "infra/gcp/terraform/g8-1-environment/cloud_deploy_execution.tf", "no_external_ip = true"),
    "target-deploy-parameters": (ROOT / "infra/gcp/terraform/g8-1-platform/cloud_deploy.tf", "deploy_parameters ="),
    "staging-waits-rollout": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Wait-CloudDeployRollout"),
    "production-waits-rollout": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Wait-ProductionRollout"),
    "staging-runtime-jobs": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Deploy-G81RuntimeJobs.ps1"),
    "production-runtime-jobs": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Deploy-G81RuntimeJobs.ps1"),
    "staging-functional-smoke": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Invoke-G81FunctionalSmoke.ps1"),
    "production-functional-smoke": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Invoke-G81FunctionalSmoke.ps1"),
    "smoke-uses-edge-https": (ROOT / "scripts/cloud/Invoke-G81FunctionalSmoke.ps1", "FrontendOrigin must be an absolute HTTPS origin"),
    "smoke-rejects-direct-run-app": (ROOT / "scripts/cloud/Invoke-G81FunctionalSmoke.ps1", "not a direct Cloud Run run.app URL"),
    "staging-edge-bootstrap-is-explicit": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "BOOTSTRAP_SERVICES_BEFORE_EDGE"),
    "production-edge-bootstrap-is-explicit": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "BOOTSTRAP_SERVICES_BEFORE_EDGE"),
    "staging-release-is-idempotent": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Existing Cloud Deploy release"),
    "production-rollout-is-idempotent": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "reused_existing_rollout"),
    "staging-bootstrap-is-not-verified": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "$stagingVerified = $false"),
    "production-bootstrap-is-not-verified": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "$productionVerified = $false"),
    "release-waits-for-parallel-gates": (ROOT / ".github/workflows/gcp-g8-1-release.yml", "Waiting for $name on $SOURCE_SHA"),
    "release-gates-default-branch": (ROOT / ".github/workflows/gcp-g8-1-release.yml", ".headBranch==$branch"),
    "staging-verifies-release-attestation": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "gh attestation verify g81-release/release-manifest.json"),
    "staging-attests-sealed-checksums": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "g81-staging-evidence/checksums.sha256"),
    "production-verifies-staging-attestation": (ROOT / ".github/workflows/gcp-g8-1-promote-production.yml", "gh attestation verify g81-staging-evidence/checksums.sha256"),
    "production-attests-sealed-checksums": (ROOT / ".github/workflows/gcp-g8-1-promote-production.yml", "g81-production-evidence/checksums.sha256"),
    "terraform-cli-patchline-pin-exists": (ROOT / "infra/gcp/terraform/g8-1-platform/versions.tf", 'required_version = "~> 1.15.5"'),
    "google-provider-pin-current": (ROOT / "infra/gcp/terraform/g8-1-platform/versions.tf", 'version = "= 7.36.0"'),
    "random-provider-pin-current": (ROOT / "infra/gcp/terraform/g8-1-environment/versions.tf", 'version = "= 3.9.0"'),
    "frontend-healthz": (ROOT / "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml", "/healthz"),
    "cloud-run-verify-url-variable": (ROOT / "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml", "CLOUD_RUN_SERVICE_URLS"),
    "eleven-images": (ROOT / "scripts/cloud/Build-G81Release.sh", "cloud-deploy-verifier"),
    "custom-root-trust": (ROOT / "src/NatureProtector.Shared/Configuration/PrivateCertificateAuthorityValidator.cs", "X509ChainTrustMode.CustomRootTrust"),
    "rate-limit-health-bypass": (ROOT / "src/NatureProtector.Backoffice.Api/Configuration/ApiRateLimitingExtensions.cs", "unrestricted-health"),
    "normalized-forwarded-client-ip": (ROOT / "src/NatureProtector.Backoffice.Api/Configuration/ApiRateLimitingExtensions.cs", "TrustNormalizedForwardedFor"),
    "load-balancer-overwrites-forwarded-for": (ROOT / "infra/gcp/terraform/g8-1-environment/edge.tf", "X-Forwarded-For:{client_ip_address},{server_ip_address}"),
    "dedicated-gke-node-identity": (ROOT / "infra/gcp/terraform/g8-1-environment/iam.tf", "google_service_account\" \"gke_nodes"),
    "autopilot-uses-dedicated-node-identity": (ROOT / "infra/gcp/terraform/g8-1-environment/gke.tf", "service_account = google_service_account.gke_nodes[0].email"),
    "cross-project-runtime-image-pull": (ROOT / "infra/gcp/terraform/g8-1-platform/artifact_registry.tf", "google_artifact_registry_repository_iam_member\" \"runtime_readers"),
    "cloud-run-service-agent-registry-reader": (ROOT / "infra/gcp/terraform/g8-1-platform/artifact_registry.tf", "serverless-robot-prod.iam.gserviceaccount.com"),
    "non-circular-platform-bootstrap": (ROOT / "infra/gcp/terraform/g8-1-platform/variables.tf", "create_delivery_pipelines"),
    "platform-staging-execution-identity": (ROOT / "infra/gcp/terraform/g8-1-platform/identity.tf", 'account_id   = "np-deploy-staging"'),
    "platform-reuses-existing-deploy-identity": (ROOT / "infra/gcp/terraform/g8-1-platform/variables.tf", "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"),
    "platform-reuses-existing-artifact-repository": (ROOT / "infra/gcp/terraform/g8-1-platform/artifact_registry.tf", 'data "google_artifact_registry_repository" "images"'),
    "platform-is-single-project-staging-only": (ROOT / "infra/gcp/terraform/g8-1-platform/outputs.tf", 'value = "single-project-staging-only"'),
    "edge-requires-domain": (ROOT / "infra/gcp/terraform/g8-1-environment/variables.tf", "At least one managed certificate domain is required"),
    "production-requires-alert-channel": (ROOT / "infra/gcp/terraform/g8-1-environment/variables.tf", "Production requires at least one Monitoring notification channel"),
    "g81-smoke-identity": (ROOT / "infra/gcp/smoke/smoke.sh", "g81-smoke-"),
    "remote-state-backend-platform": (ROOT / "infra/gcp/terraform/g8-1-platform/versions.tf", "backend \"gcs\""),
    "remote-state-backend-environment": (ROOT / "infra/gcp/terraform/g8-1-environment/versions.tf", "backend \"gcs\""),
    "protected-state-bootstrap": (ROOT / "infra/gcp/terraform/g8-1-state-bootstrap/state.tf", "prevent_destroy = true"),
    "teardown-reinitializes-remote-state": (ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1", "-backend-config=\"prefix=$TerraformStatePrefix\""),
    "teardown-verifies-evidence-checksums": (ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1", "Test-EvidenceChecksums -Directory $EvidenceDirectory"),
    "promotion-verifies-staging-checksums": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Test-EvidenceChecksums -Directory $StagingEvidenceDirectory"),
    "teardown-disables-deletion-protection": (ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1", "-var=\"deletion_protection=false\""),
    "generated-secrets-are-write-only": (ROOT / "infra/gcp/terraform/g8-1-environment/generated_secrets.tf", "secret_data_wo"),
    "cloud-sql-passwords-are-write-only": (ROOT / "infra/gcp/terraform/g8-1-environment/cloud_sql.tf", "password_wo"),
    "migration-job-uses-contract-env": (ROOT / "scripts/cloud/Deploy-G81RuntimeJobs.ps1", "POSTGRES_MIGRATION_USER=np_migration"),
    "migration-password-uses-contract-env": (ROOT / "scripts/cloud/Deploy-G81RuntimeJobs.ps1", "POSTGRES_MIGRATION_" + "PASS" + "WORD="),
    "keda-rabbitmq-autoscaling": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "queueName: np.ingestion.readings"),
    "keda-private-ca-authentication": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "parameter: ca"),
    "keda-safe-fallback": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "failureThreshold: 3"),
    "operator-assets-use-github-digests": (ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1", "GitHub did not publish a sha256 digest"),
    "operator-assets-server-side-applied": (ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1", "--server-side --field-manager=natureprotector-g81-foundation"),
    "operator-lock-exact-keda-version": (ROOT / "infra/gcp/kubernetes/g8-1/operator-lock.json", '"tag": "v2.18.2"'),
    "staging-installs-cluster-dependencies": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Install-G81ClusterDependencies.ps1"),
    "production-installs-cluster-dependencies": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Install-G81ClusterDependencies.ps1"),
    "cluster-bootstrap-uses-dns-endpoint": (ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1", "--dns-endpoint"),
    "workflow-cluster-bootstrap-role": (ROOT / "infra/gcp/terraform/g8-1-platform/identity.tf", "roles/container.admin"),
    "removed-gke-vulnerability-scanning-disabled": (ROOT / "infra/gcp/terraform/g8-1-environment/gke.tf", 'vulnerability_mode = "VULNERABILITY_DISABLED"'),
    "prevention-postgres-explicit": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "POSTGRES_REQUIRE_EXPLICIT"),
    "prevention-postgres-cloudsql-ip": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "${cloud_sql_private_ip}"),
    "prevention-influx-explicitly-disabled": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "InfluxDb__Enabled"),
    "staging-is-qualification-profile": (ROOT / "infra/gcp/kubernetes/g8-1/overlays/staging/kustomization.yaml", "deployment-profile: qualification"),
    "production-cloudsql-guardrail": (ROOT / "infra/gcp/terraform/g8-1-environment/cloud_sql.tf", "Production requires regional Cloud SQL"),
}
for name, (path, token) in semantic_checks.items():
    check(token in path.read_text(encoding="utf-8"), f"semantic:{name}")

staging_script = (ROOT / "scripts/cloud/Deploy-G81Staging.ps1").read_text(encoding="utf-8")
check(not re.search(r"\.Replace\(\"(?:API_|FRONTEND_|OTEL_|RUNTIME_|CLOUD_SQL_|POSTGRES_|JWT_|RABBITMQ_)", staging_script), "semantic:staging-must-not-bake-target-values")
check("CLOUD_RUN_SERVICE_URL'" not in scope_text and "CLOUD_RUN_SERVICE_URL/" not in scope_text, "semantic:singular-cloud-run-url-variable-is-invalid")
ca_validator = (ROOT / "src/NatureProtector.Shared/Configuration/PrivateCertificateAuthorityValidator.cs").read_text(encoding="utf-8")
check("policyErrors == SslPolicyErrors.None" not in ca_validator, "semantic:private-ca-must-not-fallback-to-system-trust")

result = {
    "phase": "G8.1",
    "status": "passed" if not errors else "failed",
    "required_files": len(REQUIRED),
    "json_files": len(json_files),
    "yaml_files": len(yaml_files),
    "hcl_files": len(hcl_files),
    "checks": checks,
    "errors": errors,
    "cloud_provisioned": False,
    "production_authorized": False,
    "production_deployed": False,
}
print(json.dumps(result, indent=2))
sys.exit(1 if errors else 0)
