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
    "infra/gcp/contracts/g10-3-budget-input.schema.json",
    "infra/gcp/contracts/g10-3-budget-input.example.json",
    "scripts/cloud/Test-G103BudgetInput.py",
    "scripts/cloud/Invoke-G103BudgetBootstrap.ps1",
    "scripts/cloud/Get-G103CloudInventory.ps1",
    "scripts/cloud/New-G103FoundationPlan.py",
    "docs/decisions/ADR-G10-3-owner-bootstrap-budget-and-foundation-plan.md",
    "docs/operations/g10-3-owner-bootstrap-runbook.md",
    "docs/evidence/g10-3-phase4-state.json",
]
for relative in required:
    check((ROOT / relative).is_file(), f"missing:{relative}")

schema = json.loads((ROOT / required[0]).read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
check(schema["properties"]["budget_is_hard_cap"]["const"] is False, "schema:budget-not-hard-cap")
check(schema["properties"]["execution"]["properties"]["create_pubsub_notifications"]["const"] is False, "schema:no-budget-pubsub")

example = json.loads((ROOT / required[1]).read_text(encoding="utf-8"))
check(example["execution"]["create_budget_alerts"] is False, "example:budget-execution-false")
check(example["budget_is_hard_cap"] is False, "example:budget-not-hard-cap")
check({item["role"] for item in example["budgets"]} == {"billing", "platform", "staging", "production"}, "example:all-budget-roles")

bootstrap = (ROOT / "scripts/cloud/Invoke-G102ProjectBootstrap.ps1").read_text(encoding="utf-8")
check("execution.create_projects=true" in bootstrap, "project-bootstrap:requires-create-flag")
check("execution.link_billing=true" in bootstrap, "project-bootstrap:requires-link-flag")
check("if ($WhatIfPreference)" in bootstrap, "project-bootstrap:whatif-safe")
check("project-bootstrap-summary.json" in bootstrap, "project-bootstrap:evidence-summary")

budget = (ROOT / "scripts/cloud/Invoke-G103BudgetBootstrap.ps1").read_text(encoding="utf-8")
check("CREATE_NATUREPROTECTOR_BUDGET_ALERTS_ONLY" in budget, "budget:exact-confirmation")
check("execution.create_budget_alerts=true" in budget, "budget:requires-input-flag")
check("budgets generate alerts; they do not stop or cap spending" in budget, "budget:not-hard-cap-warning")
for forbidden in ("terraform apply", "gcloud projects create", "gcloud run deploy", "kubectl apply"):
    check(forbidden not in budget, f"budget:forbidden:{forbidden}")

inventory = (ROOT / "scripts/cloud/Get-G103CloudInventory.ps1").read_text(encoding="utf-8")
check('mode = "READ_ONLY"' in inventory, "inventory:read-only")
check('mutations = $false' in inventory, "inventory:no-mutations")
for forbidden in ("projects create", "billing projects link", "services enable", "terraform apply", "kubectl apply"):
    check(forbidden not in inventory, f"inventory:forbidden:{forbidden}")

foundation = (ROOT / "scripts/cloud/New-G103FoundationPlan.py").read_text(encoding="utf-8")
check('"mode": "PLAN_ONLY"' in foundation, "foundation:plan-only")
check('"create_state_foundation": False' in foundation, "foundation:state-disabled")
check('"create_delivery_control_plane": False' in foundation, "foundation:control-plane-disabled")
check('"data_plane_created": False' in foundation, "foundation:data-plane-false")
check(not re.search(r"subprocess|os\.system|Popen\(|run\(", foundation), "foundation:no-command-execution")

platform_variables = (ROOT / "infra/gcp/terraform/g8-1-platform/variables.tf").read_text(encoding="utf-8")
platform_services = (ROOT / "infra/gcp/terraform/g8-1-platform/services.tf").read_text(encoding="utf-8")
platform_evidence = (ROOT / "infra/gcp/terraform/g8-1-platform/evidence.tf").read_text(encoding="utf-8")
state_services = (ROOT / "infra/gcp/terraform/g8-1-state-bootstrap/services.tf").read_text(encoding="utf-8")
check('variable "create_evidence_storage"' in platform_variables, "platform:evidence-storage-flag")
check("var.create_delivery_control_plane || var.create_evidence_storage" in platform_variables, "platform:evidence-storage-confirmation")
check('"storage.googleapis.com"' in state_services, "state-bootstrap:storage-api")
check("local.enabled_platform_services" in platform_services, "platform:service-phase-selector")
check("var.create_delivery_control_plane ? local.platform_services : toset([])" in platform_services, "platform:evidence-storage-no-service-double-ownership")
check('"storage.googleapis.com"' not in platform_services, "platform:storage-api-not-double-owned")
check("create_delivery_control_plane || var.create_evidence_storage" in platform_evidence, "platform:evidence-bucket-decoupled")

result = {
    "phase": "G10.3_STATIC",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "errors": errors,
    "cloud_provisioned": False,
    "budgets_created": False,
    "state_foundation_created": False,
    "data_plane_created": False,
    "deployment_proved": False,
}
print(json.dumps(result, indent=2))
raise SystemExit(1 if errors else 0)
