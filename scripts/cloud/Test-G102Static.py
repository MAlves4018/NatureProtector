#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []
checks = 0


def check(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        errors.append(message)


required = [
    "infra/gcp/contracts/g10-2-bootstrap-input.schema.json",
    "infra/gcp/contracts/g10-2-bootstrap-input.example.json",
    "scripts/cloud/Test-G102BootstrapInput.py",
    "scripts/cloud/Test-G102ProjectBootstrapIdempotency.py",
    "scripts/cloud/New-G102BootstrapPlan.py",
    "scripts/cloud/Invoke-G102ExecutablePreflight.ps1",
    "scripts/cloud/Invoke-G102ProjectBootstrap.ps1",
    "docs/decisions/ADR-G10-2-executable-preflight-and-bootstrap-gates.md",
    "docs/operations/g10-2-preflight-bootstrap-runbook.md",
    "docs/evidence/g10-2-phase3-state.json",
]
for relative in required:
    check((ROOT / relative).is_file(), f"missing:{relative}")

schema = json.loads((ROOT / required[0]).read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
check(schema["properties"]["execution"]["properties"]["create_data_plane"]["const"] is False, "schema:data-plane-must-be-false")
check(schema["properties"]["execution"]["properties"]["create_edge"]["const"] is False, "schema:edge-must-be-false")
check("pattern" in schema["properties"]["billing_account_id"], "schema:billing-account-format")
check("const" not in schema["properties"]["billing_account_id"], "schema:billing-account-not-hardcoded")
check(schema["properties"]["repository"]["const"] == "MAlves4018/NatureProtector", "schema:repository")
check(schema["properties"]["default_branch"]["const"] == "master", "schema:branch")

preflight = (ROOT / "scripts/cloud/Invoke-G102ExecutablePreflight.ps1").read_text(encoding="utf-8")
check("PREFLIGHT_READ_ONLY" in preflight, "preflight:read-only-marker")
check("cloud_mutations = $false" in preflight, "preflight:no-cloud-mutations")
for forbidden in ("gcloud projects create", "gcloud billing projects link", "terraform apply", "terraform destroy"):
    check(forbidden not in preflight, f"preflight:forbidden:{forbidden}")

bootstrap = (ROOT / "scripts/cloud/Invoke-G102ProjectBootstrap.ps1").read_text(encoding="utf-8")
check("[switch]$Execute" in bootstrap, "bootstrap:execute-switch")
check("CREATE_EMPTY_NATUREPROTECTOR_PROJECTS_AND_LINK_APPROVED_BILLING" in bootstrap, "bootstrap:exact-confirmation")
check("No APIs, state bucket, control plane or data plane" in bootstrap, "bootstrap:scope-message")
check("execution.create_projects=true" in bootstrap, "bootstrap:requires-create-projects-input-flag")
check("execution.link_billing=true" in bootstrap, "bootstrap:requires-link-billing-input-flag")
check("project-bootstrap-summary.json" in bootstrap, "bootstrap:evidence-summary")
check("if ($WhatIfPreference)" in bootstrap, "bootstrap:whatif-does-not-describe-missing-projects")
check("NO_OP_ALREADY_COMPLIANT" in bootstrap, "bootstrap:idempotent-compliant-billing-noop")
check("Automatic relink is not allowed" in bootstrap, "bootstrap:wrong-billing-blocks")
check("Test-ExpectedBillingAccount" in bootstrap, "bootstrap:exact-billing-account-helper")
for forbidden in ("terraform apply", "gcloud services enable", "gcloud run deploy", "kubectl apply"):
    check(forbidden not in bootstrap, f"bootstrap:forbidden:{forbidden}")

example = (ROOT / "infra/gcp/contracts/g10-2-bootstrap-input.example.json").read_text(encoding="utf-8")
check('"create_data_plane": false' in example, "example:data-plane-false")
check('"create_delivery_control_plane": false' in example, "example:control-plane-false")
check(not re.search(r'(?i)password|private[_-]?key|token\s*"\s*:', example), "example:no-secret-fields")

result = {
    "phase": "G10.2_STATIC",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "errors": errors,
    "cloud_provisioned": False,
    "data_plane_created": False,
    "deployment_proved": False,
}
print(json.dumps(result, indent=2))
raise SystemExit(1 if errors else 0)
