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
    "infra/gcp/kubernetes/g8-1/verifier-support/base/kustomization.yaml",
    "infra/gcp/kubernetes/g8-1/verifier-support/base/service-account.yaml",
    "infra/gcp/kubernetes/g8-1/verifier-support/base/role.yaml",
    "infra/gcp/kubernetes/g8-1/verifier-support/base/role-binding.yaml",
    "infra/gcp/kubernetes/g8-1/verifier-support/base/network-policy.yaml",
    "infra/gcp/kubernetes/g8-1/verifier-support/overlays/staging/kustomization.yaml",
    "infra/gcp/kubernetes/g8-1/verifier-support/overlays/production/kustomization.yaml",
    "infra/gcp/cloud-deploy/g8-1/api/service.yaml",
    "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml",
    "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml",
    "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml",
    "infra/gcp/cloud-deploy-verifier/Dockerfile",
    "scripts/cloud/EvidenceChecksums.ps1",
    "tests/cloud/Test-EvidenceChecksumPortability.ps1",
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
    "scripts/cloud/Test-BuildG81ReleaseStatic.py",
    "scripts/cloud/Install-G81ClusterDependencies.ps1",
    "scripts/cloud/Ensure-G81PreventionVerifierSupport.ps1",
    "scripts/cloud/Deploy-G81RuntimeJobs.ps1",
    "scripts/cloud/Invoke-G81FunctionalSmoke.ps1",
    "scripts/cloud/Deploy-G81Staging.ps1",
    "scripts/cloud/Promote-G81Production.ps1",
    "scripts/cloud/New-G81ReleaseManifest.py",
    "scripts/cloud/Test-G81ReleaseManifest.py",
    "scripts/cloud/Test-PreventionInClusterVerifierStatic.py",
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
    "GetRelativePath",
    "DirectorySeparatorChar",
    "Write-G81EvidenceChecksums",
    "CHECKSUM_PORTABILITY_RUNTIME_TEST=PASS",
    "request.path != '/api/users-roles/login'",
    "opt_out_rule_ids",
    "owasp-crs-v030301-id942200-sqli",
    "request.method == 'POST'",
    "request.path == '/api/users-roles/users'",
    "owasp_sqli_user_create",
    "'sensitivity': 1",
    "'sensitivity': 4",
    "owasp-crs-v030301-id942432-sqli",
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
    ".TrimStart('\\\\','/')",
    'TrimStart("\\\\","/")',
]:
    check(forbidden.lower() not in deployable_text.lower(), f"forbidden:{forbidden}")

checksum_scripts = [
    ROOT / "scripts/cloud/Deploy-G81Staging.ps1",
    ROOT / "scripts/cloud/Deploy-G81Staging-Autopilot.ps1",
    ROOT / "scripts/cloud/Promote-G81Production.ps1",
]
checksum_helper = (ROOT / "scripts/cloud/EvidenceChecksums.ps1").read_text(encoding="utf-8")
check("TrimStart" not in checksum_helper, "checksum-helper-uses-trimstart")
check("GetRelativePath" in checksum_helper, "checksum-helper-getrelativepath")
check("DirectorySeparatorChar" in checksum_helper, "checksum-helper-directoryseparatorchar")
check("[char]'/'" in checksum_helper or "[char]\"/\"" in checksum_helper, "checksum-helper-forward-slash-char")
for script in checksum_scripts:
    text = script.read_text(encoding="utf-8", errors="ignore")
    check("EvidenceChecksums.ps1" in text, f"checksum-helper-not-sourced:{script.name}")
    check("Write-G81EvidenceChecksums -EvidenceDirectory $EvidenceDirectory" in text, f"checksum-helper-not-used:{script.name}")
    check("TrimStart" not in text, f"checksum-script-uses-trimstart:{script.name}")

np_text = (ROOT / "scripts/np.ps1").read_text(encoding="utf-8", errors="ignore")
check("Test-EvidenceChecksumPortability.ps1" in np_text, "checksum-runtime-test-not-in-np-validate")

for workflow in sorted((ROOT / ".github/workflows").glob("gcp-g8-1-*.yml")):
    text = workflow.read_text(encoding="utf-8")
    for match in re.finditer(r"uses:\s*([^\s#]+)", text):
        value = match.group(1)
        if value.startswith("./"):
            continue
        check(bool(re.search(r"@[0-9a-f]{40}$", value)), f"unpinned-action:{workflow.name}:{value}")

release_workflow = ROOT / ".github/workflows/gcp-g8-1-release.yml"
action_pin_lock_path = ROOT / "scripts/cloud/g81-release-action-pins.json"
try:
    action_pin_lock = json.loads(action_pin_lock_path.read_text(encoding="utf-8"))
except (OSError, json.JSONDecodeError) as exc:
    action_pin_lock = {}
    check(False, f"action-pin-lock:{action_pin_lock_path.relative_to(ROOT)}:{exc}")

release_action_pins = action_pin_lock.get("pins", [])
check(action_pin_lock.get("scope") == ".github/workflows/gcp-g8-1-release.yml", "action-pin-lock-scope")
check(action_pin_lock.get("validation_source") == "GitHub REST API", "action-pin-lock-validation-source")
locked_release_pins = {
    (pin.get("repository"), pin.get("sha")): pin
    for pin in release_action_pins
    if isinstance(pin, dict)
}
for pin in release_action_pins:
    repository = pin.get("repository") if isinstance(pin, dict) else None
    sha = pin.get("sha") if isinstance(pin, dict) else None
    check(bool(re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", str(repository))), f"action-pin-lock-repository:{repository}")
    check(bool(re.fullmatch(r"[0-9a-f]{40}", str(sha))), f"action-pin-lock-sha:{repository}@{sha}")
    check(bool(pin.get("resolved_by")) if isinstance(pin, dict) else False, f"action-pin-lock-resolution:{repository}@{sha}")

release_text = release_workflow.read_text(encoding="utf-8")
check("actions/attest-build-provenance@59d89421af93a897026c735860bf21b6eb4f7b26" not in release_text, "release-invalid-attest-build-provenance-pin")
for match in re.finditer(r"uses:\s*([^\s#]+)", release_text):
    value = match.group(1)
    if value.startswith("./"):
        continue
    action_match = re.fullmatch(r"([^@]+)@([0-9a-f]{40})", value)
    check(action_match is not None, f"release-action-pin-format:{value}")
    if action_match is None:
        continue
    repository, sha = action_match.groups()
    check((repository, sha) in locked_release_pins, f"unresolved-release-action-pin:{value}")

attest_pin = locked_release_pins.get(("actions/attest-build-provenance", "0f67c3f4856b2e3261c31976d6725780e5e4c373"), {})
check(attest_pin.get("tag") == "v4.1.1", "attest-build-provenance-version-comment-lock")
check("actions/attest-build-provenance@0f67c3f4856b2e3261c31976d6725780e5e4c373 # v4.1.1" in release_text, "attest-build-provenance-version-comment")

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
    "staging-rollout-id-is-explicit": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Get-CloudDeployRolloutId"),
    "staging-disables-oversized-auto-rollout-id": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "--disable-initial-rollout"),
    "staging-promotes-with-short-rollout-id": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "--rollout-id=$rolloutId"),
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
    "staging-preserves-ready-operators": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "OPERATOR_FOUNDATION_ALREADY_READY"),
    "staging-ready-operator-check-has-kubecontext": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "--dns-endpoint --quiet"),
    "staging-does-not-force-operator-conflicts": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "preserve-existing-operator-field-managers"),
    "release-waits-for-parallel-gates": (ROOT / ".github/workflows/gcp-g8-1-release.yml", "Waiting for $name on $SOURCE_SHA"),
    "release-gates-default-branch": (ROOT / ".github/workflows/gcp-g8-1-release.yml", ".headBranch==$branch"),
    "staging-verifies-release-attestation": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "gh attestation verify g81-release/release-manifest.json"),
    "staging-auth-uses-standard-wif-var": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "workload_identity_provider: ${{ vars.WIF_PROVIDER }}"),
    "staging-auth-uses-standard-deploy-sa-var": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "service_account: ${{ vars.DEPLOY_SERVICE_ACCOUNT }}"),
    "staging-attests-sealed-checksums": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "g81-staging-evidence/checksums.sha256"),
    "production-verifies-staging-attestation": (ROOT / ".github/workflows/gcp-g8-1-promote-production.yml", "gh attestation verify g81-staging-evidence/checksums.sha256"),
    "production-attests-sealed-checksums": (ROOT / ".github/workflows/gcp-g8-1-promote-production.yml", "g81-production-evidence/checksums.sha256"),
    "terraform-cli-patchline-pin-exists": (ROOT / "infra/gcp/terraform/g8-1-platform/versions.tf", 'required_version = "~> 1.15.5"'),
    "google-provider-pin-current": (ROOT / "infra/gcp/terraform/g8-1-platform/versions.tf", 'version = "= 7.36.0"'),
    "random-provider-pin-current": (ROOT / "infra/gcp/terraform/g8-1-environment/versions.tf", 'version = "= 3.9.0"'),
    "frontend-healthz": (ROOT / "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml", "/healthz"),
    "cloud-run-verify-url-variable": (ROOT / "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml", "CLOUD_RUN_SERVICE_URLS"),
    "cloud-run-verify-defers-http-to-edge": (ROOT / "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml", "protected edge smoke"),
    "frontend-verify-defers-http-to-edge": (ROOT / "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml", "protected edge smoke"),
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
    "platform-reuses-existing-cloud-deploy-bucket": (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf", 'data "google_storage_bucket" "cloud_deploy_source"'),
    "platform-reuses-existing-cloud-deploy-frontend-bucket": (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf", 'data "google_storage_bucket" "cloud_deploy_frontend_source"'),
    "platform-reuses-existing-cloud-deploy-prevention-bucket": (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf", 'data "google_storage_bucket" "cloud_deploy_prevention_source"'),
    "platform-cloud-deploy-bucket-metadata-iam": (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf", 'google_storage_bucket_iam_member" "cloud_deploy_source_metadata"'),
    "platform-cloud-deploy-frontend-bucket-metadata-iam": (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf", 'google_storage_bucket_iam_member" "cloud_deploy_frontend_source_metadata"'),
    "platform-cloud-deploy-prevention-bucket-metadata-iam": (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf", 'google_storage_bucket_iam_member" "cloud_deploy_prevention_source_metadata"'),
    "platform-cloud-deploy-bucket-get-custom-role": (ROOT / "infra/gcp/terraform/g8-1-platform/identity.tf", '"storage.buckets.get"'),
    "platform-cloud-deploy-bucket-list-custom-role": (ROOT / "infra/gcp/terraform/g8-1-platform/identity.tf", '"storage.buckets.list"'),
    "platform-is-single-project-staging-only": (ROOT / "infra/gcp/terraform/g8-1-platform/outputs.tf", 'value = "single-project-staging-only"'),
    "edge-requires-domain": (ROOT / "infra/gcp/terraform/g8-1-environment/variables.tf", "At least one managed certificate domain is required"),
    "production-requires-alert-channel": (ROOT / "infra/gcp/terraform/g8-1-environment/variables.tf", "Production requires at least one Monitoring notification channel"),
    "g81-smoke-identity": (ROOT / "infra/gcp/smoke/smoke.sh", "g81-smoke-"),
    "remote-state-backend-platform": (ROOT / "infra/gcp/terraform/g8-1-platform/versions.tf", "backend \"gcs\""),
    "remote-state-backend-environment": (ROOT / "infra/gcp/terraform/g8-1-environment/versions.tf", "backend \"gcs\""),
    "protected-state-bootstrap": (ROOT / "infra/gcp/terraform/g8-1-state-bootstrap/state.tf", "prevent_destroy = true"),
    "teardown-reinitializes-remote-state": (ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1", "-backend-config=\"prefix=$TerraformStatePrefix\""),
    "teardown-verifies-evidence-checksums": (ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1", "Test-EvidenceChecksums -Directory $EvidenceDirectory"),
    "promotion-verifies-staging-checksums": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Test-G81EvidenceChecksums -Directory $StagingEvidenceDirectory"),
    "teardown-disables-deletion-protection": (ROOT / "scripts/cloud/Remove-G81WeekEnvironment.ps1", "-var=\"deletion_protection=false\""),
    "generated-secrets-are-write-only": (ROOT / "infra/gcp/terraform/g8-1-environment/generated_secrets.tf", "secret_data_wo"),
    "cloud-sql-passwords-are-write-only": (ROOT / "infra/gcp/terraform/g8-1-environment/cloud_sql.tf", "password_wo"),
    "migration-job-uses-contract-env": (ROOT / "scripts/cloud/Deploy-G81RuntimeJobs.ps1", "POSTGRES_MIGRATION_USER=np_migration"),
    "migration-password-uses-contract-env": (ROOT / "scripts/cloud/Deploy-G81RuntimeJobs.ps1", "POSTGRES_MIGRATION_" + "PASS" + "WORD="),
    "keda-rabbitmq-autoscaling": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "queueName: np.ingestion.readings"),
    "keda-rabbitmq-host-uses-cluster-fqdn": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "amqps://natureprotector-rabbitmq.natureprotector-staging.svc.cluster.local:5671/"),
    "keda-private-ca-authentication": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "parameter: ca"),
    "keda-safe-fallback": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml", "failureThreshold: 3"),
    "operator-assets-use-github-digests": (ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1", "GitHub did not publish a sha256 digest"),
    "operator-assets-server-side-applied": (ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1", "--server-side --field-manager=natureprotector-g81-foundation"),
    "operator-lock-exact-keda-version": (ROOT / "infra/gcp/kubernetes/g8-1/operator-lock.json", '"tag": "v2.18.2"'),
    "staging-installs-cluster-dependencies": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Install-G81ClusterDependencies.ps1"),
    "staging-ensures-prevention-verifier-support": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Ensure-G81PreventionVerifierSupport.ps1"),
    "staging-foundation-readiness-script": (ROOT / "scripts/cloud/Test-G81StagingFoundationReadiness.ps1", "STAGING_FOUNDATION_READINESS=PASS"),
    "staging-deploy-runs-foundation-readiness": (ROOT / "scripts/cloud/Deploy-G81Staging.ps1", "Test-G81StagingFoundationReadiness.ps1"),
    "staging-workflow-runs-foundation-readiness": (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml", "Verify staging foundation readiness"),
    "standard-deploy-runs-foundation-readiness": (ROOT / ".github/workflows/_deploy.yml", "Verify staging foundation readiness"),
    "np-validate-runs-foundation-readiness": (ROOT / "scripts/np.ps1", "Test-G81StagingFoundationReadiness.ps1"),
    "prevention-verifier-support-server-dry-run": (ROOT / "scripts/cloud/Ensure-G81PreventionVerifierSupport.ps1", "--dry-run=server"),
    "prevention-verifier-support-field-manager": (ROOT / "scripts/cloud/Ensure-G81PreventionVerifierSupport.ps1", "natureprotector-verifier-support-foundation"),
    "prevention-verifier-support-staging-namespace": (ROOT / "infra/gcp/kubernetes/g8-1/verifier-support/overlays/staging/kustomization.yaml", "namespace: natureprotector-staging"),
    "prevention-verifier-support-production-namespace": (ROOT / "infra/gcp/kubernetes/g8-1/verifier-support/overlays/production/kustomization.yaml", "namespace: natureprotector-production"),
    "prevention-verifier-support-role-binding": (ROOT / "infra/gcp/kubernetes/g8-1/verifier-support/base/role-binding.yaml", "name: natureprotector-deploy-verifier"),
    "production-installs-cluster-dependencies": (ROOT / "scripts/cloud/Promote-G81Production.ps1", "Install-G81ClusterDependencies.ps1"),
    "cluster-bootstrap-uses-dns-endpoint": (ROOT / "scripts/cloud/Install-G81ClusterDependencies.ps1", "--dns-endpoint"),
    "workflow-cluster-bootstrap-role": (ROOT / "infra/gcp/terraform/g8-1-platform/identity.tf", "roles/container.admin"),
    "removed-gke-vulnerability-scanning-disabled": (ROOT / "infra/gcp/terraform/g8-1-environment/gke.tf", 'vulnerability_mode = "VULNERABILITY_DISABLED"'),
    "prevention-postgres-explicit": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "POSTGRES_REQUIRE_EXPLICIT"),
    "prevention-postgres-cloudsql-ip": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "${cloud_sql_private_ip}"),
    "prevention-rabbitmq-private-ca": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "RabbitMq__TlsCertificateAuthorityPath"),
    "prevention-influx-explicitly-disabled": (ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml", "InfluxDb__Enabled"),
    "prevention-host-uses-web-health-server": (ROOT / "src/NatureProtector.Prevention.Host/Program.cs", "WebApplication.CreateBuilder(args)"),
    "prevention-host-registers-runtime-readiness": (ROOT / "src/NatureProtector.Prevention.Host/Program.cs", "AddSingleton<PreventionRuntimeState>()"),
    "prevention-host-registers-readiness-check": (ROOT / "src/NatureProtector.Prevention.Host/Program.cs", 'AddCheck<PreventionReadinessHealthCheck>("prevention-ready")'),
    "prevention-host-exposes-liveness": (ROOT / "src/NatureProtector.Prevention.Host/Program.cs", 'MapHealthChecks("/health/live"'),
    "prevention-host-exposes-readiness": (ROOT / "src/NatureProtector.Prevention.Host/Program.cs", 'MapHealthChecks("/health/ready")'),
    "prevention-host-uses-aspnet-runtime": (ROOT / "src/NatureProtector.Prevention.Host/NatureProtector.Prevention.Host.csproj", "Microsoft.AspNetCore.App"),
    "staging-is-qualification-profile": (ROOT / "infra/gcp/kubernetes/g8-1/overlays/staging/kustomization.yaml", "deployment-profile: qualification"),
    "production-cloudsql-guardrail": (ROOT / "infra/gcp/terraform/g8-1-environment/cloud_sql.tf", "Production requires regional Cloud SQL"),
}
for name, (path, token) in semantic_checks.items():
    check(token in path.read_text(encoding="utf-8"), f"semantic:{name}")

staging_kustomization = yaml.safe_load(
    (ROOT / "infra/gcp/kubernetes/g8-1/overlays/staging/kustomization.yaml").read_text(encoding="utf-8")
)
staging_patches = staging_kustomization.get("patches", [])
selector_labels = {
    "environment": "staging",
    "phase": "g8-1",
    "deployment-profile": "qualification",
}
for deployment_name in ("natureprotector-prevention", "natureprotector-otel"):
    deployment_patches = [
        patch
        for patch in staging_patches
        if patch.get("target", {}).get("kind") == "Deployment"
        and patch.get("target", {}).get("name") == deployment_name
    ]
    check(deployment_patches, f"semantic:staging-selector-patch-present:{deployment_name}")
    patch_ops = []
    for deployment_patch in deployment_patches:
        patch_ops.extend(yaml.safe_load(deployment_patch.get("patch", "[]")))
    for label_name, label_value in selector_labels.items():
        check(
            any(
                op.get("op") == "add"
                and op.get("path") == f"/spec/selector/matchLabels/{label_name}"
                and op.get("value") == label_value
                for op in patch_ops
            ),
            f"semantic:staging-preserves-immutable-selector:{deployment_name}:{label_name}",
        )
        check(
            any(
                op.get("op") == "add"
                and op.get("path")
                == f"/spec/template/spec/topologySpreadConstraints/0/labelSelector/matchLabels/{label_name}"
                and op.get("value") == label_value
                for op in patch_ops
            ),
            f"semantic:staging-preserves-topology-selector:{deployment_name}:{label_name}",
        )

network_policy_documents = [
    document
    for document in yaml.safe_load_all(
        (ROOT / "infra/gcp/kubernetes/g8-1/base/network-policy.yaml").read_text(encoding="utf-8")
    )
    if isinstance(document, dict) and document.get("kind") == "NetworkPolicy"
]
for policy_name in ("prevention-runtime", "rabbitmq-runtime", "otel-runtime"):
    policy = next(
        (document for document in network_policy_documents if document.get("metadata", {}).get("name") == policy_name),
        None,
    )
    check(policy is not None, f"semantic:network-policy-present:{policy_name}")
    egress_rules = policy.get("spec", {}).get("egress", []) if policy else []
    dns_rules = [
        rule
        for rule in egress_rules
        if any(target.get("ipBlock", {}).get("cidr") == "169.254.20.10/32" for target in rule.get("to", []))
    ]
    check(dns_rules, f"semantic:gke-node-local-dns-egress-present:{policy_name}")
    for protocol in ("UDP", "TCP"):
        check(
            any(
                any(port.get("protocol") == protocol and port.get("port") == 53 for port in rule.get("ports", []))
                for rule in dns_rules
            ),
            f"semantic:gke-node-local-dns-egress-port:{policy_name}:{protocol}",
        )

staging_script = (ROOT / "scripts/cloud/Deploy-G81Staging.ps1").read_text(encoding="utf-8")
staging_workflow = (ROOT / ".github/workflows/gcp-g8-1-deploy-staging.yml").read_text(encoding="utf-8")
check("GCP_G81_STAGING_WIF_PROVIDER" not in staging_workflow, "semantic:staging-wif-provider-var-must-exist")
check("GCP_G81_STAGING_SERVICE_ACCOUNT" not in staging_workflow, "semantic:staging-service-account-var-must-exist")
check(not re.search(r"\.Replace\(\"(?:API_|FRONTEND_|OTEL_|RUNTIME_|CLOUD_SQL_|POSTGRES_|JWT_|RABBITMQ_(?!IMAGE_BY_DIGEST))", staging_script), "semantic:staging-must-not-bake-target-values")
check('Replace("RABBITMQ_IMAGE_BY_DIGEST", [string]$images.rabbitmq.reference)' in staging_script, "semantic:rabbitmq-crd-image-must-use-signed-manifest-digest")
credentials_index = staging_script.find("gcloud container clusters get-credentials")
operator_ready_index = staging_script.find("Test-OperatorFoundationReady -OutputDirectory")
check(credentials_index >= 0 and operator_ready_index >= 0 and credentials_index < operator_ready_index, "semantic:staging-operator-readiness-requires-kubecontext-first")
readiness_index = staging_script.find("Test-G81StagingFoundationReadiness.ps1")
check(readiness_index >= 0 and credentials_index >= 0 and readiness_index < credentials_index, "semantic:staging-foundation-readiness-precedes-kubecontext")
workflow_readiness_index = staging_workflow.find("Verify staging foundation readiness")
workflow_deploy_index = staging_workflow.find("Deploy runtime prerequisites and verified staging rollouts")
check(workflow_readiness_index >= 0 and workflow_deploy_index >= 0 and workflow_readiness_index < workflow_deploy_index, "semantic:staging-workflow-readiness-precedes-deploy")
standard_deploy_workflow = (ROOT / ".github/workflows/_deploy.yml").read_text(encoding="utf-8")
standard_readiness_index = standard_deploy_workflow.find("Verify staging foundation readiness")
standard_deploy_index = standard_deploy_workflow.find("Deploy by digest authority")
check(standard_readiness_index >= 0 and standard_deploy_index >= 0 and standard_readiness_index < standard_deploy_index, "semantic:standard-deploy-readiness-precedes-deploy")
check("$spec.Images" not in staging_script and "$spec.Pipeline" not in staging_script and "$spec.Skaffold" not in staging_script, "semantic:cloud-deploy-release-specs-use-explicit-indexers")
check("CLOUD_RUN_SERVICE_URL'" not in scope_text and "CLOUD_RUN_SERVICE_URL/" not in scope_text, "semantic:singular-cloud-run-url-variable-is-invalid")
ca_validator = (ROOT / "src/NatureProtector.Shared/Configuration/PrivateCertificateAuthorityValidator.cs").read_text(encoding="utf-8")
check("policyErrors == SslPolicyErrors.None" not in ca_validator, "semantic:private-ca-must-not-fallback-to-system-trust")
platform_identity = (ROOT / "infra/gcp/terraform/g8-1-platform/identity.tf").read_text(encoding="utf-8")
role_match = re.search(
    r'resource\s+"google_project_iam_custom_role"\s+"cloud_deploy_source_bucket_lister"\s*\{(?P<body>.*?)\n\}',
    platform_identity,
    re.DOTALL,
)
check(role_match is not None, "semantic:cloud-deploy-source-bucket-lister-role-present")
role_body = role_match.group("body") if role_match else ""
for forbidden_permission in ['"storage.admin"', '"storage.buckets.delete"', '"storage.objects.delete"']:
    check(forbidden_permission not in role_body, f"semantic:cloud-deploy-source-bucket-lister-forbids:{forbidden_permission}")

build_release_static = subprocess.run(
    [sys.executable, str(ROOT / "scripts/cloud/Test-BuildG81ReleaseStatic.py")],
    cwd=ROOT,
    capture_output=True,
    text=True,
    check=False,
)
build_release_output = (build_release_static.stdout + "\n" + build_release_static.stderr).strip().replace("\n", " | ")
check(build_release_static.returncode == 0, f"build-g81-release-static:{build_release_output}")

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
