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
postgres_connection_settings = (
    ROOT
    / "src/NatureProtector.Infrastructure.Postgres/Configuration/PostgresControlPlaneConnectionSettings.cs"
).read_text(encoding="utf-8")
postgres_migration_settings = (
    ROOT / "src/NatureProtector.Postgres.Migrations/MigrationSettings.cs"
).read_text(encoding="utf-8")
postgres_migration_runner = (
    ROOT / "src/NatureProtector.Postgres.Migrations/PostgresMigrationRunner.cs"
).read_text(encoding="utf-8")
postgres_bootstrap_program = (
    ROOT / "src/NatureProtector.Postgres.Bootstrap/Program.cs"
).read_text(encoding="utf-8")
postgres_bootstrap_helper = (
    ROOT / "src/NatureProtector.Postgres.Bootstrap/BootstrapProgram.cs"
).read_text(encoding="utf-8")
postgres_bootstrapper = (
    ROOT
    / "src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs"
).read_text(encoding="utf-8")
deploy_runtime_jobs = (ROOT / "scripts/cloud/Deploy-G81RuntimeJobs.ps1").read_text(
    encoding="utf-8"
)
staging_kustomization = (
    ROOT / "infra/gcp/kubernetes/g8-1/overlays/staging/kustomization.yaml"
).read_text(encoding="utf-8")
gke_network_policy = (
    ROOT / "infra/gcp/kubernetes/g8-1/base/network-policy.yaml"
).read_text(encoding="utf-8")
gke_prevention = (
    ROOT / "infra/gcp/kubernetes/g8-1/base/prevention.yaml"
).read_text(encoding="utf-8")
gke_rabbitmq = (
    ROOT / "infra/gcp/kubernetes/g8-1/base/rabbitmq.yaml"
).read_text(encoding="utf-8")
gke_prevention_scaling = (
    ROOT / "infra/gcp/kubernetes/g8-1/base/prevention-scaling.yaml"
).read_text(encoding="utf-8")
prevention_worker_text = (
    ROOT / "src/NatureProtector.Prevention.Host/PreventionWorker.cs"
).read_text(encoding="utf-8")
rabbitmq_options_text = (
    ROOT / "src/NatureProtector.Shared/Configuration/RabbitMqOptions.cs"
).read_text(encoding="utf-8")
simulator_publisher_text = (
    ROOT / "src/NatureProtector.Simulator.Host/Publishing/RabbitMqReadingPublisher.cs"
).read_text(encoding="utf-8")
autopilot_foundation = (
    ROOT / "scripts/cloud/install-g81-cluster-dependencies-autopilot.sh"
)
autopilot_runner = (
    ROOT / "scripts/cloud/complete-staging-after-autopilot-remediation.sh"
)
autopilot_deploy = ROOT / "scripts/cloud/Deploy-G81Staging-Autopilot.ps1"
standard_deploy = ROOT / "scripts/cloud/Deploy-G81Staging.ps1"
prevention_skaffold_text = (
    ROOT / "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml"
).read_text(encoding="utf-8")
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
    for allowed_dirty_path in [
        "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml",
        "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml",
        "infra/gcp/kubernetes/g8-1/base/network-policy.yaml",
        "infra/gcp/kubernetes/g8-1/base/prevention.yaml",
        "infra/gcp/kubernetes/g8-1/base/rabbitmq.yaml",
        "infra/gcp/kubernetes/g8-1/overlays/staging/kustomization.yaml",
        "scripts/cloud/Deploy-G81Staging.ps1",
        "scripts/cloud/Test-G81Static.py",
        "docs/operations/g8-1-cd-and-rollout-runbook.md",
    ]:
        check(
            allowed_dirty_path in autopilot_runner_text,
            f"autopilot-runner-dirty-gate-missing:{allowed_dirty_path}",
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
        "$pipeline = [string]$spec.Pipeline" in autopilot_deploy_text
        and "$target = [string]$spec.Target" in autopilot_deploy_text
        and "$skaffold = [string]$spec.Skaffold" in autopilot_deploy_text
        and "$imageMappings = [string]$spec.Images" in autopilot_deploy_text
        and "--delivery-pipeline=$spec.Pipeline" not in autopilot_deploy_text
        and "--skaffold-file=$spec.Skaffold" not in autopilot_deploy_text
        and "--images=$spec.Images" not in autopilot_deploy_text,
        "autopilot-deploy-cloud-deploy-native-argument-property-expansion-unsafe",
    )
    check(
        'Join-Path $EvidenceDirectory "cloud-deploy-source"' in autopilot_deploy_text
        and "RabbitmqCluster.spec.image is a CRD field" in autopilot_deploy_text
        and 'Replace("RABBITMQ_IMAGE_BY_DIGEST", [string]$images.rabbitmq.reference)' in autopilot_deploy_text
        and "--source=$sourceRoot" in autopilot_deploy_text,
        "autopilot-deploy-must-render-rabbitmq-crd-image-from-signed-manifest",
    )

if standard_deploy.is_file():
    standard_deploy_text = standard_deploy.read_text(encoding="utf-8")
    check(
        '$pipeline = [string]$spec["Pipeline"]' in standard_deploy_text
        and '$target = [string]$spec["Target"]' in standard_deploy_text
        and '$skaffold = [string]$spec["Skaffold"]' in standard_deploy_text
        and '$imagesArg = [string]$spec["Images"]' in standard_deploy_text
        and "--delivery-pipeline=$spec.Pipeline" not in standard_deploy_text
        and "--skaffold-file=$spec.Skaffold" not in standard_deploy_text
        and "--images=$spec.Images" not in standard_deploy_text,
        "standard-deploy-cloud-deploy-native-argument-property-expansion-unsafe",
    )
    check(
        'Join-Path $EvidenceDirectory "cloud-deploy-source"' in standard_deploy_text
        and "RabbitmqCluster.spec.image is a CRD field" in standard_deploy_text
        and 'Replace("RABBITMQ_IMAGE_BY_DIGEST", [string]$images.rabbitmq.reference)' in standard_deploy_text
        and "--images=$imagesArg" in standard_deploy_text,
        "standard-deploy-must-render-rabbitmq-crd-image-from-signed-manifest",
    )

api_skaffold = (ROOT / "infra/gcp/cloud-deploy/g8-1/api/skaffold.yaml").read_text(
    encoding="utf-8"
)
frontend_skaffold = (
    ROOT / "infra/gcp/cloud-deploy/g8-1/frontend/skaffold.yaml"
).read_text(encoding="utf-8")
for name, text, health_path in [
    ("api", api_skaffold, "/health/live"),
    ("frontend", frontend_skaffold, "/healthz"),
]:
    check(
        "CLOUD_RUN_SERVICE_URLS" in text
        and "protected edge smoke" in text
        and health_path in text,
        f"cloud-run-{name}-verify-must-defer-http-to-edge-smoke",
    )
    check(
        'curl -fsS "$url' not in text
        and "curl -fsS \"$url" not in text,
        f"cloud-run-{name}-verify-must-not-probe-restricted-run-app",
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
    "UseSslClientAuthenticationOptionsCallback" in postgres_data_source_factory
    and "CertificateChainPolicy" in postgres_data_source_factory
    and "X509CertificateLoader.LoadCertificateFromFile" in postgres_data_source_factory,
    "postgres-datasource-root-certificate-builder-missing",
)
check(
    "X509ChainTrustMode.CustomRootTrust" in postgres_data_source_factory
    and "ValidateServerCertificateForCertificateAuthority" in postgres_data_source_factory,
    "postgres-datasource-verifyca-custom-root-validation-missing",
)
check(
    "ownsServerCertificate" in postgres_data_source_factory
    and "if (ownsServerCertificate)" in postgres_data_source_factory
    and "serverCertificate.Dispose()" in postgres_data_source_factory
    and "using var serverCertificate" not in postgres_data_source_factory
    and "X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert))" in postgres_data_source_factory,
    "postgres-datasource-must-not-dispose-tls-owned-certificate",
)
check(
    'connectionBuilder["Trust Server Certificate"] = false' in postgres_data_source_factory
    and "builder.ChannelBinding = ChannelBinding.Require" in postgres_connection_settings
    and "builder.ChannelBinding = ChannelBinding.Require" in postgres_migration_settings
    and "connectionBuilder.SslMode = SslMode.Require" not in postgres_data_source_factory
    and "connectionBuilder.SslMode = SslMode.Prefer" not in postgres_data_source_factory
    and "RemoteCertificateValidationCallback" not in postgres_data_source_factory,
    "postgres-datasource-verifyca-must-not-downgrade-overwrite-callback-or-allow-channel-binding-fallback",
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
check(
    "RoleExistsAsync" in postgres_migration_runner
    and "CREATE ROLE" in postgres_migration_runner
    and "ALTER ROLE" not in postgres_migration_runner,
    "postgres-migration-runner-must-not-alter-cloud-sql-managed-role",
)
check(
    "NP_BOOTSTRAP_SKIP_SCHEMA_MIGRATION" in postgres_bootstrap_helper
    and "ShouldSkipSchemaMigration" in postgres_bootstrap_helper
    and "return false;" in postgres_bootstrap_helper,
    "postgres-bootstrap-skip-schema-env-default-missing",
)
check(
    "skipSchemaMigration" in postgres_bootstrap_program
    and "BootstrapProgram.ShouldSkipSchemaMigration()" in postgres_bootstrap_program
    and "new ControlPlaneBootstrapper(dbContext, contentRoot, skipSchemaMigration)" in postgres_bootstrap_program
    and "Schema migration skipped" in postgres_bootstrap_program,
    "postgres-bootstrap-program-skip-schema-wiring-missing",
)
check(
    "bool skipSchemaMigration = false" in postgres_bootstrapper
    and "if (!_skipSchemaMigration)" in postgres_bootstrapper
    and "EnsureSchemaAsync" in postgres_bootstrapper
    and "Database.MigrateAsync" in postgres_bootstrapper,
    "postgres-bootstrapper-skip-schema-guard-missing",
)
check(
    "NP_BOOTSTRAP_SKIP_SCHEMA_MIGRATION=true" in deploy_runtime_jobs
    and "POSTGRES_USER=np_app" in deploy_runtime_jobs
    and "POSTGRES_MIGRATION_USER=np_migration" in deploy_runtime_jobs,
    "postgres-bootstrap-cloud-job-skip-schema-env-missing",
)

check(
    "cloud_sql_ca_secret_resources" not in (PLATFORM / "terraform.staging.tfvars").read_text(encoding="utf-8")
    and "rabbitmq_tls_secret_resources" not in (PLATFORM / "terraform.staging.tfvars").read_text(encoding="utf-8")
    and "runtime_secret_resources" not in (PLATFORM / "terraform.staging.tfvars").read_text(encoding="utf-8"),
    "platform-gke-deploy-parameters-must-not-use-multiline-secret-resource-sets",
)
check(
    "np-staging-cloud-sql-server-ca/versions/1" in staging_kustomization
    and "np-staging-rabbitmq-tls-certificate/versions/latest" in staging_kustomization
    and "np-staging-rabbitmq-tls-private-key/versions/latest" in staging_kustomization
    and "np-staging-rabbitmq-ca-certificate/versions/latest" in staging_kustomization
    and "np-staging-postgres-app-password/versions/1" in staging_kustomization,
    "staging-kustomization-secret-provider-resources-missing",
)
check(
    "containers:\n              - name: rabbitmq" in gke_rabbitmq
    and "np.network/rabbitmq" in gke_rabbitmq,
    "gke-rabbitmq-statefulset-override-must-keep-required-container-entry",
)
check(
    "K8S_HOSTNAME_SUFFIX" in gke_rabbitmq
    and "$(MY_POD_NAMESPACE).svc.cluster.local" in gke_rabbitmq
    and "RABBITMQ_NODENAME" in gke_rabbitmq,
    "gke-rabbitmq-nodename-must-use-headless-service-fqdn",
)
check(
    "requests: {cpu: \"1\", memory: 4Gi}" in gke_rabbitmq
    and "limits: {cpu: \"2\", memory: 4Gi}" in gke_rabbitmq
    and "path: /spec/resources/requests/memory\n        value: 512Mi" in staging_kustomization
    and "path: /spec/resources/limits/memory\n        value: 512Mi" in staging_kustomization,
    "gke-rabbitmq-memory-request-and-limit-must-match",
)
check(
    "management.tcp.port = none" not in gke_rabbitmq
    and "management.tcp.port = none" not in staging_kustomization
    and "management.ssl.port = 15671" in gke_rabbitmq
    and "management.ssl.port = 15671" in staging_kustomization,
    "gke-rabbitmq-management-tcp-port-must-not-use-non-integer-none",
)
check(
    "ssl_options.fail_if_no_peer_cert = false" in gke_rabbitmq
    and "ssl_options.fail_if_no_peer_cert = false" in staging_kustomization,
    "gke-rabbitmq-server-tls-must-not-require-client-certificates",
)
check(
    "port: 15672" in gke_rabbitmq
    and "connectionSecret: {name: natureprotector-rabbitmq-default-user}" in (ROOT / "infra/gcp/kubernetes/g8-1/base/rabbitmq-topology.yaml").read_text(encoding="utf-8")
    and "{protocol: TCP, port: 15672}" in gke_network_policy,
    "gke-rabbitmq-topology-operator-must-use-internal-management-connection-secret",
)
check(
    "kubectl patch secret natureprotector-rabbitmq-default-user" in prevention_skaffold_text
    and "http://natureprotector-rabbitmq.natureprotector-staging.svc:15672" in prevention_skaffold_text
    and "kubectl wait --for=condition=Ready user/natureprotector-app" in prevention_skaffold_text
    and "kubectl wait --for=condition=Ready permission/natureprotector-app" in prevention_skaffold_text
    and "kubectl wait --for=condition=Ready policy/natureprotector-quorum-policy" in prevention_skaffold_text,
    "gke-cloud-deploy-verify-must-reconcile-rabbitmq-topology-secret-uri",
)
check(
    "amqps://natureprotector-rabbitmq.natureprotector-staging.svc.cluster.local:5671/" in gke_prevention_scaling
    and "amqps://natureprotector-rabbitmq:5671/" not in gke_prevention_scaling,
    "gke-keda-rabbitmq-host-must-use-cluster-fqdn",
)
check(
    "{name: RabbitMq__TlsCertificateAuthorityPath, value: /var/run/secrets/rabbitmq/ca.crt}" in gke_prevention
    and "public bool TlsEnabled" in rabbitmq_options_text
    and "public string? TlsServerName" in rabbitmq_options_text
    and "public string? TlsCertificateAuthorityPath" in rabbitmq_options_text
    and "factory.Ssl = new SslOption" in prevention_worker_text
    and "PrivateCertificateAuthorityValidator.Create(options.TlsCertificateAuthorityPath)" in prevention_worker_text
    and "factory.Ssl = new SslOption" in simulator_publisher_text
    and "PrivateCertificateAuthorityValidator.Create(options.TlsCertificateAuthorityPath)" in simulator_publisher_text,
    "rabbitmq-clients-must-enable-tls-with-private-ca",
)
check(
    "cidr: 127.0.0.1/32 # from-param: ${cloud_sql_private_cidr}" not in gke_network_policy
    and "cidr: 10.255.255.255/32 # from-param: ${cloud_sql_private_cidr}" in gke_network_policy
    and "cloud_sql_private_cidr" in (ROOT / "infra/gcp/terraform/g8-1-platform/terraform.staging.tfvars").read_text(encoding="utf-8"),
    "gke-prevention-cloud-sql-egress-default-must-not-use-loopback",
)
check(
    "commonLabels:" not in staging_kustomization
    and "includeSelectors: false" in staging_kustomization
    and "includeTemplates: true" in staging_kustomization,
    "gke-staging-labels-must-not-mutate-networkpolicy-selectors",
)
check(
    "runAsNonRoot: true" in gke_prevention
    and "runAsUser: 1654" in gke_prevention
    and "runAsGroup: 1654" in gke_prevention
    and "fsGroup: 1654" in gke_prevention
    and "allowPrivilegeEscalation: false" in gke_prevention,
    "gke-prevention-must-use-explicit-numeric-non-root-identity",
)
check(
    "169.254.169.252/32" in gke_network_policy
    and "port: 988" in gke_network_policy
    and "port: 987" in gke_network_policy
    and "169.254.169.254/32" in gke_network_policy
    and "port: 80" in gke_network_policy
    and "port: 8080" in gke_network_policy
    and "metadata.google.internal" not in gke_network_policy,
    "gke-otel-network-policy-must-allow-gke-metadata-server-only",
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
