#!/usr/bin/env python3
"""Offline structural validation for RabbitMQ/health phase 3F."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]


def read(path: str) -> str:
    target = ROOT / path
    if not target.is_file():
        raise AssertionError(f"missing file: {path}")
    return target.read_text(encoding="utf-8-sig")


def require(path: str, *needles: str) -> str:
    content = read(path)
    for needle in needles:
        if needle not in content:
            raise AssertionError(f"{path}: missing expected text: {needle}")
    return content


def forbid(path: str, *needles: str) -> None:
    content = read(path)
    for needle in needles:
        if needle in content:
            raise AssertionError(f"{path}: forbidden stale text remains: {needle}")


def main() -> int:
    migration = require(
        "scripts/operations/Invoke-RabbitMqRawQueueMigration.ps1",
        "Inventory', 'Plan', 'Protect', 'RetireLegacyPolicy', 'Unbind', 'Verify', 'Rollback",
        "SupportsShouldProcess",
        "RABBITMQ_MANAGEMENT_USERNAME",
        "RABBITMQ_MANAGEMENT_PASSWORD",
        "CustomRootTrust",
        "AllowAutoRedirect = $false",
        "PROTECT_RAW:",
        "RETIRE_LEGACY_POLICY:",
        "UNBIND_RAW:",
        "ROLLBACK_RAW:",
        "overflow = 'drop-head'",
        "'message-ttl' = $MessageTtlMilliseconds",
        "'max-length-bytes' = $MaxLengthBytes",
        "PHASE3F_RAW_DISABLED_AND_UNBOUND",
        "automaticPurge = $false",
        "automaticQueueDelete = $false",
    )
    if re.search(r"Invoke-RabbitMqManagementRequest[^\n]*-Method DELETE[^\n]*-Path [^\n]*api/queues", migration):
        raise AssertionError("migration script must not delete queues")
    if "purge" in migration.lower() and "automaticPurge" not in migration:
        raise AssertionError("migration script contains an unexpected purge path")

    inventory = require(
        "scripts/cloud/Get-G81RabbitMqRawQueueMigrationInventory.ps1",
        "mode = 'READ_ONLY'",
        "cloudMutationsExecuted = $false",
        "kubectl config current-context",
        "simulatorRawDisabled",
        "preventionRawDisabled",
        "noRunningSimulatorExecutions",
        "PHASE3F_CLOUD_INVENTORY_READ_ONLY_COMPLETE",
        "does not run get-credentials",
    )
    for forbidden_command in (
        "'run', 'jobs', 'deploy'",
        "'run', 'services', 'update'",
        "'apply'",
        "'patch'",
        "'delete'",
    ):
        if forbidden_command in inventory:
            raise AssertionError(f"cloud inventory contains mutating command token: {forbidden_command}")

    topology = require(
        "infra/gcp/kubernetes/g8-1/base/rabbitmq-topology.yaml",
        "name: natureprotector-primary-work-queue-policy",
        "name: natureprotector-primary-work-queue",
        "pattern: '^np\\.ingestion\\.readings$'",
        "overflow: reject-publish",
        "max-length-bytes: 1073741824",
        "priority: 20",
    )
    if "pattern: '^np\\.'" in topology:
        raise AssertionError("broad ^np\\. policy remains")
    if "natureprotector-quorum-policy" in topology:
        raise AssertionError("legacy broad policy CRD remains")
    list(yaml.safe_load_all(topology))

    require(
        "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml",
        "policy/natureprotector-primary-work-queue-policy",
    )
    forbid(
        "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml",
        "policy/natureprotector-quorum-policy",
    )
    require(
        "infra/gcp/kubernetes/g8-1/base/prevention.yaml",
        'RabbitMq__ObservabilityRawEnabled, value: "false"',
    )
    require(
        "infra/gcp/cloud-deploy/g8-1/api/service.yaml",
        'RabbitMq__ObservabilityRawEnabled, value: "false"',
    )
    require(
        "scripts/cloud/Deploy-G81RuntimeJobs.ps1",
        "RabbitMq__ObservabilityRawEnabled=false",
    )

    contract = json.loads(read("config/operations/rabbitmq-health-contract.json"))
    if contract.get("status") not in {
        "IMPLEMENTED_NOT_PROVED_PHASE3F",
        "IMPLEMENTED_NOT_PROVED_PHASE3G",
    }:
        raise AssertionError("contract status is not Phase 3F or a compatible later phase")
    ownership = contract["rabbitmq"]["policy_ownership"]
    if ownership["primary_policy"]["pattern"] != r"^np\.ingestion\.readings$":
        raise AssertionError("primary policy pattern is not exact")
    if ownership["raw_migration_policy"]["overflow"] != "drop-head":
        raise AssertionError("raw migration policy must use drop-head")
    if ownership["raw_migration_policy"]["message_ttl_milliseconds"] != "REQUIRED_OPERATOR_INPUT":
        raise AssertionError("raw TTL must remain an explicit operator input")
    if contract["rabbitmq"]["migration_tooling"]["automatic_purge"]:
        raise AssertionError("automatic purge must remain false")
    if contract["rabbitmq"]["migration_tooling"]["automatic_queue_delete"]:
        raise AssertionError("automatic queue delete must remain false")

    require(
        "docs/operations/rabbitmq-health-remediation-rollout-runbook.md",
        "## 14. Comandos Phase 3F",
        "Get-G81RabbitMqRawQueueMigrationInventory.ps1",
        "Invoke-RabbitMqRawQueueMigration.ps1",
        "PHASE3F_RAW_DISABLED_AND_UNBOUND",
        "O script não executa purge nem delete da queue.",
    )
    require(
        "docs/contracts/rabbitmq-runtime-topology-and-delivery-contract.md",
        "IMPLEMENTED_NOT_PROVED_PHASE3F",
        "## Implementação Phase 3F",
    )
    require(
        "docs/decisions/ADR-RMQ-01-bounded-auxiliary-queue-and-topology-ownership.md",
        "IMPLEMENTED_NOT_PROVED_PHASE3F",
        "## Nota de implementação Phase 3F",
    )

    require(
        "scripts/cloud/Test-EnvironmentRemediationStatic.py",
        "gke-rabbitmq-primary-policy-must-not-match-auxiliary-queues",
        "cloud-runtime-declarers-must-explicitly-disable-raw-queue",
    )
    require(
        "scripts/cloud/Test-G81Static.py",
        "semantic:rabbitmq-broad-reject-policy-forbidden",
        "semantic:rabbitmq-legacy-policy-crd-forbidden",
    )
    require(
        "scripts/audit/Invoke-RabbitMqHealthPhase3FValidation.ps1",
        "PHASE3F_PACKAGE_STATIC_CHECK=PASS",
        "PHASE3F_LOCAL_MIGRATION_EXERCISE_PROVED",
        "PHASE3F_VALIDATION=PASS",
    )

    print("PHASE3F_PACKAGE_STATIC_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (AssertionError, KeyError, json.JSONDecodeError, yaml.YAMLError) as exc:
        print(f"PHASE3F_PACKAGE_STATIC_CHECK=FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
