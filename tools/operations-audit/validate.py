#!/usr/bin/env python3
"""Validate the unified engineering operations control-plane security contract."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any


REQUIRED_FILES = [
    "src/NatureProtector.Backoffice.Api/Operations/Authorization/OperationCapabilities.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Configuration/OperationsOptions.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Contracts/OperationContracts.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Services/OperationCatalog.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Services/OperationStore.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Services/AutomationDispatcher.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Services/EngineeringOperationsService.cs",
    "src/NatureProtector.Backoffice.Api/Operations/Services/CloudEnvironmentCatalogService.cs",
    "src/NatureProtector.Backoffice.Api/Controllers/ControlOperationsController.cs",
    "src/NatureProtector.Backoffice.Api/Controllers/ControlQualityController.cs",
    "src/NatureProtector.Backoffice.Api/Controllers/ControlEvidenceOperationsController.cs",
    "src/NatureProtector.Backoffice.Api/Controllers/ControlDeploymentsController.cs",
    "src/NatureProtector.Backoffice.Api/Controllers/ControlCloudOperationsController.cs",
    "src/NatureProtector.Backoffice.Api/Controllers/ControlApprovalsController.cs",
    "webUI/src/app/operations/OperationsContext.tsx",
    "webUI/src/app/operations/OperationLauncher.tsx",
    "webUI/src/app/pages/MissionControlPage.tsx",
    "webUI/src/app/pages/QualityRunsPage.tsx",
    "webUI/src/app/pages/EvidenceExplorerPage.tsx",
    "webUI/src/app/pages/DeploymentsPage.tsx",
    "webUI/src/app/pages/CloudResourcesPage.tsx",
    "webUI/src/app/pages/ApprovalsPage.tsx",
    "webUI/src/app/pages/UserRoleAdministrationPage.tsx",
    ".github/workflows/_quality-operation.yml",
    ".github/workflows/_evidence-campaign.yml",
    ".github/workflows/_deployment-operation.yml",
    ".github/workflows/_cloud-operation.yml",
    "scripts/operations/report-operation-callback.py",
]

DANGEROUS_BACKEND_PATTERNS = [
    r"Process\.Start\s*\(",
    r"cmd\.exe",
    r"powershell\.exe",
    r"bash\s+-c",
    r"terraform\s+(?:apply|destroy)",
]

SECRET_FRONTEND_PATTERNS = [
    r"GITHUB_TOKEN",
    r"GOOGLE_APPLICATION_CREDENTIALS",
    r"service[_-]?account[_-]?key",
    r"private[_-]?key",
    r"operations[_-]?callback[_-]?secret",
]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    checks: list[dict[str, Any]] = []
    failures: list[str] = []

    def text(relative: str) -> str:
        path = repo / relative
        return path.read_text(encoding="utf-8") if path.is_file() else ""

    def check(name: str, condition: bool, detail: str) -> None:
        checks.append({"name": name, "status": "PASS" if condition else "FAIL", "detail": detail})
        if not condition:
            failures.append(f"{name}: {detail}")

    for relative in REQUIRED_FILES:
        check(f"required-file:{relative}", (repo / relative).is_file(), relative)

    capabilities = text(REQUIRED_FILES[0])
    catalog = text("src/NatureProtector.Backoffice.Api/Operations/Services/OperationCatalog.cs")
    service = text("src/NatureProtector.Backoffice.Api/Operations/Services/EngineeringOperationsService.cs")
    dispatcher = text("src/NatureProtector.Backoffice.Api/Operations/Services/AutomationDispatcher.cs")
    program = text("src/NatureProtector.Backoffice.Api/Program.cs")
    users_controller = text("src/NatureProtector.Backoffice.Api/Controllers/UserAndRolesController.cs")
    launcher = text("webUI/src/app/operations/OperationLauncher.tsx")
    api = text("webUI/src/app/services/api.ts")
    ui_context = text("webUI/src/app/state/CapabilityContext.tsx")

    required_capabilities = [
        "QualityExecuteStatic", "QualityExecuteFull", "EvidenceExecuteCampaign", "EvidenceCompare",
        "DeploymentPlan", "DeploymentDeployStaging", "DeploymentDeployProduction", "DeploymentRollback",
        "CloudOperateStaging", "CloudOperateProduction", "CloudDestroy", "ApprovalReview", "UsersManage", "RolesManage",
    ]
    for capability in required_capabilities:
        check(f"capability:{capability}", f"const string {capability}" in capabilities, capability)

    check("role:qa", '["QA"]' in capabilities, "QA role has an explicit capability profile")
    check("role:operations", '["Operations"]' in capabilities, "Operations role has an explicit capability profile")
    check("role:release-approver", '["ReleaseApprover"]' in capabilities, "ReleaseApprover role has an explicit capability profile")

    admin_block = re.search(r'\["Admin"\]\s*=\s*\[(.*?)\n\s*\]', capabilities, re.DOTALL)
    release_block = re.search(r'\["ReleaseApprover"\]\s*=\s*\[(.*?)\n\s*\]', capabilities, re.DOTALL)
    admin_text = admin_block.group(1) if admin_block else ""
    release_text = release_block.group(1) if release_block else ""
    check("admin-no-production-deploy", "DeploymentDeployProduction" not in admin_text, "Admin is not implicitly a production deployer")
    check("admin-no-destroy", "CloudDestroy" not in admin_text, "Admin is not implicitly allowed to destroy cloud resources")
    check("approver-production-deploy", "DeploymentDeployProduction" in release_text, "ReleaseApprover can review/execute approved production promotion")
    check("approver-destroy", "CloudDestroy" in release_text, "ReleaseApprover is the dedicated destroy-capable role")
    check("approver-review", "ApprovalReview" in release_text, "ReleaseApprover can decide approvals")

    operation_ids = set(re.findall(r'"((?:frontend|backend|playwright|evidence|staging|production|cloud|quality|terraform|architecture|security|accessibility|mutation)[a-z0-9-]*)"\s*,\s*"', catalog))
    expected_ids = {
        "frontend-fast", "frontend-full", "backend-unit", "backend-integration", "architecture", "security",
        "playwright-fixture", "playwright-full-stack", "accessibility", "mutation", "terraform-static", "cloud-static",
        "quality-all", "evidence-static", "evidence-quality", "evidence-full-plan", "evidence-full-execute",
        "staging-plan", "staging-deploy", "staging-rollback", "production-plan", "production-deploy", "production-rollback",
        "cloud-inventory", "cloud-costs", "cloud-smoke", "cloud-open-staging", "cloud-close-staging",
        "cloud-destroy-plan", "cloud-destroy-execute",
    }
    check("closed-operation-catalog", expected_ids.issubset(operation_ids), json.dumps(sorted(expected_ids - operation_ids)))
    check("no-arbitrary-command-input", 'new OperationInputDefinition("command"' not in catalog.lower(), "No browser-supplied command input")
    check("secret-like-input-rejected", "IsSecretKey(pair.Key)" in service, "Secret-like input names are rejected server-side")
    check("reference-allowlist", "IsSafeReference(reference)" in service, "Git references use a character allowlist")
    check("exact-confirmation", "StringComparison.Ordinal" in service and "Exact confirmation required" in service, "Confirmations are exact and server-derived")
    check("approval-gate", 'definition.RequiresApproval ? "AwaitingApproval" : "Validated"' in service and 'if (!definition.RequiresApproval)' in service, "Approval-required operations stop before dispatch")
    check("self-approval-gate", "AllowSelfApproval" in service and "Self-approval is disabled" in service, "Self approval is explicitly governed")
    check("constant-time-callback-secret", "CryptographicOperations.FixedTimeEquals" in service, "Callback secret uses constant-time comparison")
    check("callback-artifact-hash-proof", "PROVED_BY_HASHED_REPORTED_ARTIFACTS" in service and "artifact.Sha256.Length == 64" in service, "Success is only proved with referenced SHA-256 artifacts")
    check("comparison-authorization", "CanReadOperation(user, leftOperation)" in service and "ClaimsPrincipal user" in service, "Evidence comparison applies per-operation read authorization")
    check("category-read-filter", "CanReadCategory(user, definition.Category)" in service, "Catalogs and records are filtered by category read capability")

    operation_backend = "\n".join(text(relative) for relative in REQUIRED_FILES if "/Operations/" in relative or "Control" in Path(relative).name)
    for pattern in DANGEROUS_BACKEND_PATTERNS:
        check(f"backend-no-direct-exec:{pattern}", re.search(pattern, operation_backend, re.IGNORECASE) is None, pattern)

    frontend_text = "\n".join(path.read_text(encoding="utf-8") for path in (repo / "webUI/src").rglob("*.ts*") if path.is_file())
    for pattern in SECRET_FRONTEND_PATTERNS:
        check(f"frontend-no-secret:{pattern}", re.search(pattern, frontend_text, re.IGNORECASE) is None, pattern)
    check("frontend-server-capabilities", "/users-roles/me/capabilities" in api and "capabilityAuthority" in ui_context, "UI consumes server-authoritative capabilities")
    check("frontend-server-confirmation-template", "confirmationTemplate" in launcher and "confirmationByOperation" not in launcher, "UI renders the server-provided confirmation template")
    check("frontend-blocks-unauthorized", "definition.authorized" in launcher and "disabled=" in launcher and "!enabled" in launcher, "Read-only users can inspect but cannot launch")

    check("server-capability-endpoint", 'HttpGet("me/capabilities")' in users_controller, "Authenticated capability profile endpoint exists")
    check("authorization-handler-registered", "OperationCapabilityAuthorizationHandler" in program and "AddAuthorization(OperationAuthorization.Configure)" in program, "Server policies are registered")
    check("dispatcher-no-token-in-contract", "Token" not in text("src/NatureProtector.Backoffice.Api/Operations/Contracts/OperationContracts.cs"), "Operation HTTP contracts cannot carry provider tokens")
    check("dispatcher-server-token", "GithubToken" in dispatcher or "GitHubToken" in dispatcher, "GitHub credential is read only by the server-side dispatcher")
    check("simulation-labelled", "DEMONSTRATION_ONLY" in dispatcher, "Simulation mode cannot be mistaken for a remote execution proof")

    check("production-plan-blocked", '"production-plan"' in catalog and 'availability: "blocked-no-authoritative-workflow"' in catalog, "Production plan remains blocked without a canonical workflow")
    check("production-rollback-blocked", '"production-rollback"' in catalog and "No dedicated production rollback workflow" in catalog, "Production rollback remains blocked")
    check("destroy-plan-blocked", '"cloud-destroy-plan"' in catalog and '"blocked-no-destroy-plan-workflow"' in catalog, "Destroy plan remains blocked")
    check("destroy-execute-blocked", '"cloud-destroy-execute"' in catalog and '"blocked-until-approved-plan"' in catalog, "Destroy execution remains blocked")

    workflows = {
        name: text(f".github/workflows/{name}")
        for name in ["_quality-operation.yml", "_evidence-campaign.yml", "_deployment-operation.yml", "_cloud-operation.yml"]
    }
    for name, workflow in workflows.items():
        check(f"workflow-dispatch:{name}", "workflow_dispatch:" in workflow, name)
        check(f"workflow-operation-id:{name}", "operation_id" in workflow, name)
        dispatch_header = workflow.split("permissions:", 1)[0]
        check(f"workflow-no-secrets-input:{name}", not re.search(r"^\s{6}(?:token|secret|password)[a-z0-9_-]*:", dispatch_header, re.IGNORECASE | re.MULTILINE), name)
    check("staging-plan-is-plan-only", "./scripts/np.ps1 staging plan" in workflows["_deployment-operation.yml"], "Staging plan does not open or deploy staging")
    check("quality-delegates-existing-authorities", "gh workflow run" in workflows["_quality-operation.yml"] and "quality-guardrails.yml" in workflows["_quality-operation.yml"], "Quality wrapper delegates to current authoritative workflows")
    check("evidence-canonical-runner", "run-report-evidence-campaign.py" in workflows["_evidence-campaign.yml"], "Evidence wrapper invokes the canonical campaign runner")
    for name, workflow in workflows.items():
        check(f"workflow-callback:{name}", "report-operation-callback.py" in workflow, "Wrapper reports queued or direct-job completion state")
    callback_reporter = text("scripts/operations/report-operation-callback.py")
    check("callback-reporter-https", "Callback URL must use HTTPS" in callback_reporter, "Remote callback endpoints require HTTPS")
    check("callback-reporter-unconfigured-truth", "SKIPPED_UNCONFIGURED" in callback_reporter, "Missing callback plumbing is reported without a false failure claim")
    check("callback-reporter-hash", "aggregate_artifact" in callback_reporter and "hashlib.sha256" in callback_reporter, "Direct workflow output is hashed deterministically")

    environment_service = text("src/NatureProtector.Backoffice.Api/Operations/Services/CloudEnvironmentCatalogService.cs")
    check("environment-repository-root", "ResolveRepositoryRoot" in environment_service and "NatureProtector.sln" in environment_service, "Declared inventory resolves the repository root from the hosted content root")
    check("environment-no-live-claim", '"DeclaredNotObserved"' in environment_service and "not a live cloud API query" in environment_service, "Repository configuration is not labelled as observed cloud state")

    payload = {
        "schema_version": 1,
        "status": "PASS" if not failures else "FAIL",
        "summary": {"checks": len(checks), "failures": len(failures), "operation_ids": len(operation_ids)},
        "checks": checks,
        "failures": failures,
    }
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
