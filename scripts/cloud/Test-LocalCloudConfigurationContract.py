#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []
checks = 0


def check(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        errors.append(message)


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def env_map(path: str, document_index: int = 0) -> dict[str, object]:
    docs = list(yaml.safe_load_all(read(path)))
    document = docs[document_index]
    containers = document["spec"]["template"]["spec"]["containers"]
    env = containers[0].get("env", [])
    return {item["name"]: item for item in env}


loader = read("src/NatureProtector.Infrastructure.Postgres/Configuration/PostgresConnectionSettingsLoader.cs")
required_match = re.search(
    r"ThrowIfRequiredValuesAreMissing\(\s*requireExplicit,\s*dotEnvValues,\s*(.*?)\);",
    loader,
    re.DOTALL,
)
check(required_match is not None, "postgres-required-keys-not-discovered")
required_postgres = set(re.findall(r'"(POSTGRES_[A-Z_]+)"', required_match.group(1) if required_match else ""))
required_postgres.add("POSTGRES_REQUIRE_EXPLICIT")

prevention = env_map("infra/gcp/kubernetes/g8-1/base/prevention.yaml")
api = env_map("infra/gcp/cloud-deploy/g8-1/api/service.yaml")

for key in sorted(required_postgres):
    check(key in prevention, f"prevention-missing:{key}")
    check(key in api, f"api-missing:{key}")

check(prevention.get("POSTGRES_REQUIRE_EXPLICIT", {}).get("value") == "true", "prevention-explicit-postgres-not-true")
check(api.get("POSTGRES_REQUIRE_EXPLICIT", {}).get("value") == "true", "api-explicit-postgres-not-true")
check("ConnectionStrings__Postgres" not in prevention, "prevention-uses-unsupported-connection-string")
check("POSTGRES_APP_PASSWORD" not in prevention, "prevention-uses-unsupported-password-alias")
check(prevention.get("POSTGRES_SSL_MODE", {}).get("value") == "VerifyCA", "prevention-postgres-ssl-not-verifyca")
check(prevention.get("POSTGRES_ROOT_CERTIFICATE", {}).get("value") == "/var/run/secrets/cloudsql/server-ca.pem", "prevention-postgres-ca-path-mismatch")
check(prevention.get("InfluxDb__Enabled", {}).get("value") == "false", "prevention-cloud-influx-not-explicitly-disabled")

manifest_text = read("infra/gcp/kubernetes/g8-1/base/prevention.yaml")
check("mountPath: /var/run/secrets/cloudsql" in manifest_text, "prevention-cloudsql-ca-not-mounted")
check("secretName: np-cloud-sql-ca" in manifest_text, "prevention-cloudsql-ca-secret-missing")
check("${cloud_sql_private_ip}" in manifest_text, "prevention-cloudsql-target-parameter-missing")

platform_example = read("infra/gcp/terraform/g8-1-platform/terraform.tfvars.example")
check("cloud_sql_private_ip" in platform_example, "platform-example-cloudsql-ip-missing")
check("postgres_connection_string" not in platform_example, "platform-example-keeps-dead-connection-string")
for token in ["api_min_scale", "api_max_scale", "frontend_min_scale", "frontend_max_scale"]:
    check(token in platform_example, f"platform-example-scale-parameter-missing:{token}")

api_service = read("infra/gcp/cloud-deploy/g8-1/api/service.yaml")
frontend_service = read("infra/gcp/cloud-deploy/g8-1/frontend/service.yaml")
for token in ["${api_min_scale}", "${api_max_scale}"]:
    check(token in api_service, f"api-scale-parameter-missing:{token}")
for token in ["${frontend_min_scale}", "${frontend_max_scale}"]:
    check(token in frontend_service, f"frontend-scale-parameter-missing:{token}")

staging_overlay = read("infra/gcp/kubernetes/g8-1/overlays/staging/kustomization.yaml")
production_overlay = read("infra/gcp/kubernetes/g8-1/overlays/production/kustomization.yaml")
for token in [
    "deployment-profile: qualification",
    "kind: RabbitmqCluster",
    "value: 1",
    "value: 10Gi",
    "minReplicaCount",
    "maxReplicaCount",
    "natureprotector.io/claim-level: non-production-qualification",
]:
    check(token in staging_overlay, f"qualification-overlay-missing:{token}")
check("value: 3" in production_overlay, "production-overlay-minimum-replicas-weakened")

qualification_tfvars = read("infra/gcp/terraform/g8-1-environment/terraform.qualification.tfvars.example")
for token in [
    'environment            = "staging"',
    'database_availability_type = "ZONAL"',
    'database_backup_enabled    = false',
    'database_pitr_enabled      = false',
    'create_data_plane            = false',
    'create_edge                  = false',
]:
    check(token in qualification_tfvars, f"qualification-tfvars-missing:{token}")

cloud_sql = read("infra/gcp/terraform/g8-1-environment/cloud_sql.tf")
for token in [
    "var.database_availability_type",
    "var.database_disk_type",
    "var.database_backup_enabled",
    "var.database_pitr_enabled",
    "var.database_retained_backups",
    'var.environment != "production"',
]:
    check(token in cloud_sql, f"cloudsql-profile-guardrail-missing:{token}")

result = {
    "phase": "LOCAL_CLOUD_CONFIGURATION_CONTRACT",
    "status": "PASS" if not errors else "FAIL",
    "checks_total": checks,
    "checks_failed": len(errors),
    "postgres_required_keys": sorted(required_postgres),
    "errors": errors,
}
print(json.dumps(result, indent=2))
sys.exit(1 if errors else 0)
