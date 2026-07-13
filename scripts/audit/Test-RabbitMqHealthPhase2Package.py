#!/usr/bin/env python3
"""Offline consistency check for the RabbitMQ/health Phase 2 contract package."""

from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]

REQUIRED = [
    ROOT / "docs/decisions/ADR-RMQ-01-bounded-auxiliary-queue-and-topology-ownership.md",
    ROOT / "docs/decisions/ADR-HEALTH-01-conditional-readiness-and-rabbitmq-management-tls.md",
    ROOT / "docs/contracts/rabbitmq-runtime-topology-and-delivery-contract.md",
    ROOT / "docs/contracts/runtime-health-readiness-contract.md",
    ROOT / "docs/operations/rabbitmq-health-remediation-rollout-runbook.md",
    ROOT / "config/operations/rabbitmq-health-contract.json",
    ROOT / "docs/contracts/README.md",
    ROOT / "docs/implementation/cloud/g1-container-readiness-orchestration-decoupling.md",
    ROOT / "docs/implementation/observability-and-runtime-evidence.md",
    ROOT / "src/NatureProtector.Shared/README.md",
]


def fail(message: str) -> None:
    print(f"PHASE2_PACKAGE_STATIC_CHECK=FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


for path in REQUIRED:
    if not path.is_file():
        fail(f"missing {path.relative_to(ROOT)}")

contract_path = ROOT / "config/operations/rabbitmq-health-contract.json"
try:
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
except (OSError, json.JSONDecodeError) as exc:
    fail(f"invalid contract JSON: {exc}")

contract_status = contract.get("status", "")
if contract_status != "TARGET_CONTRACT_NOT_YET_IMPLEMENTED" and not contract_status.startswith("IMPLEMENTED_NOT_PROVED_PHASE"):
    fail("contract must remain target or implemented-not-proved")

queues = {item.get("role"): item for item in contract["rabbitmq"]["queues"]}
primary = queues.get("PrimaryWorkQueue")
auxiliary = queues.get("AuxiliaryDiagnosticQueue")
if primary is None or auxiliary is None:
    fail("primary and auxiliary roles are required")

if not primary.get("blocks_pipeline") or not primary.get("required_consumer"):
    fail("primary queue role is inconsistent")

if auxiliary.get("enabled_by_default"):
    fail("auxiliary raw queue must default to disabled")
if auxiliary.get("blocks_pipeline"):
    fail("auxiliary raw queue must not block the pipeline")
retention = auxiliary.get("retention", {})
if retention.get("overflow_required") != "drop-head":
    fail("auxiliary overflow must be drop-head")
if retention.get("reject_publish_forbidden") is not True:
    fail("reject-publish must be forbidden for auxiliary raw")

backoffice_ready = contract["health"]["backoffice"]["readiness"]
if not any(item.get("dependency") == "postgresql-control-plane" for item in backoffice_ready):
    fail("Backoffice PostgreSQL readiness dependency missing")

prevention_ready = contract["health"]["prevention"]["readiness"]
required_prevention = {item.get("dependency") for item in prevention_ready}
if required_prevention != {"rabbitmq-consumer", "postgresql-pipeline"}:
    fail("Prevention readiness dependencies are inconsistent")

management = contract["rabbitmq_management"]
if not management.get("https_private_ca_supported"):
    fail("private CA support must be required")
if not management.get("silent_http_fallback_forbidden"):
    fail("silent HTTP fallback must be forbidden")

joined = "\n".join(path.read_text(encoding="utf-8") for path in REQUIRED if path.suffix == ".md")
for marker in [
    "ObservabilityRawEnabled=false",
    "drop-head",
    "PostgreSQL",
    "Management",
    "dry-run",
    "Current-state correction — 2026-07-13",
    "não pode ser classificada como tecnicamente não bloqueante",
    "A fila raw só é declarada e ligada quando",
]:
    if marker not in joined:
        fail(f"required marker missing from documentation: {marker}")


if "IMPLEMENTATION_PENDING" not in joined and "IMPLEMENTED_NOT_PROVED" not in joined:
    fail("documentation must expose pending or implemented-not-proved state")

for forbidden in ["terraform apply", "terraform destroy", "gcloud run deploy", "kubectl apply"]:
    if forbidden in joined.lower():
        fail(f"mutating cloud command must not be embedded in Phase 2 contract package: {forbidden}")

print("PHASE2_PACKAGE_STATIC_CHECK=PASS")
