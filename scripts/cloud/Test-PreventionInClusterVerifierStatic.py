#!/usr/bin/env python3
from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from typing import Iterable

import yaml

ROOT = Path(__file__).resolve().parents[2]
failures: list[str] = []


def require(condition: bool, message: str) -> None:
    if not condition:
        failures.append(message)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def render_kustomize(path: Path) -> list[dict]:
    result = subprocess.run(
        ["kubectl", "kustomize", str(path)],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    require(result.returncode == 0, f"render:{path.relative_to(ROOT)}:{result.stderr.strip()}")
    if result.returncode != 0:
        return []
    try:
        return [
            document
            for document in yaml.safe_load_all(result.stdout)
            if isinstance(document, dict)
        ]
    except Exception as exc:  # noqa: BLE001
        require(False, f"render-yaml:{path.relative_to(ROOT)}:{exc}")
        return []


def find_doc(docs: Iterable[dict], kind: str, name: str) -> dict | None:
    return next(
        (
            document
            for document in docs
            if document.get("kind") == kind
            and document.get("metadata", {}).get("name") == name
        ),
        None,
    )


skaffold_path = ROOT / "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml"
job_path = ROOT / "infra/gcp/cloud-deploy/g8-1/prevention/verify-job-staging.yaml"
app_overlay_path = ROOT / "infra/gcp/kubernetes/g8-1/overlays/staging"
base_kustomization_path = ROOT / "infra/gcp/kubernetes/g8-1/base/kustomization.yaml"
deploy_script_path = ROOT / "scripts/cloud/Deploy-G81Staging.ps1"
ensure_script_path = ROOT / "scripts/cloud/Ensure-G81PreventionVerifierSupport.ps1"
support_base_path = ROOT / "infra/gcp/kubernetes/g8-1/verifier-support/base"
support_staging_overlay_path = ROOT / "infra/gcp/kubernetes/g8-1/verifier-support/overlays/staging"
support_production_overlay_path = ROOT / "infra/gcp/kubernetes/g8-1/verifier-support/overlays/production"

support_files = [
    support_base_path / "kustomization.yaml",
    support_base_path / "service-account.yaml",
    support_base_path / "role.yaml",
    support_base_path / "role-binding.yaml",
    support_base_path / "network-policy.yaml",
    support_staging_overlay_path / "kustomization.yaml",
    support_production_overlay_path / "kustomization.yaml",
]

required_paths = [
    skaffold_path,
    job_path,
    app_overlay_path / "kustomization.yaml",
    base_kustomization_path,
    deploy_script_path,
    ensure_script_path,
    *support_files,
]
for path in required_paths:
    require(path.exists(), f"missing:{path.relative_to(ROOT)}")

if not failures:
    skaffold_text = read(skaffold_path)
    skaffold = yaml.safe_load(skaffold_text)
    verify = skaffold["verify"][0]
    cluster_mode = verify.get("executionMode", {}).get("kubernetesCluster", {})

    require("SECONDS" not in skaffold_text, "skaffold:SECONDS-must-not-be-used")
    require(
        cluster_mode.get("jobManifestPath") == "verify-job-staging.yaml",
        "skaffold:in-cluster-job-manifest-missing",
    )
    require(
        'service_account_dir="/var/run/secrets/kubernetes.io/serviceaccount"'
        in skaffold_text,
        "skaffold:service-account-directory-missing",
    )
    require(
        'namespace_file="${service_account_dir}/namespace"' in skaffold_text,
        "skaffold:namespace-must-come-from-service-account",
    )
    require(
        'token_file="${service_account_dir}/token"' in skaffold_text,
        "skaffold:service-account-token-missing",
    )
    require(
        'ca_file="${service_account_dir}/ca.crt"' in skaffold_text,
        "skaffold:service-account-ca-missing",
    )
    require(
        "KUBERNETES_SERVICE_HOST" in skaffold_text,
        "skaffold:kubernetes-service-host-missing",
    )
    require(
        "kubectl config set-cluster natureprotector-in-cluster" in skaffold_text,
        "skaffold:in-cluster-kubeconfig-cluster-missing",
    )
    require(
        "kubectl config set-credentials natureprotector-deploy-verifier" in skaffold_text,
        "skaffold:in-cluster-kubeconfig-credentials-missing",
    )
    require(
        "kubectl config set-context natureprotector-deploy-verifier" in skaffold_text,
        "skaffold:in-cluster-kubeconfig-context-missing",
    )
    require(
        "kubectl get secret/np-runtime-secrets" in skaffold_text,
        "skaffold:kubernetes-api-access-check-missing",
    )
    verify_environment = {
        item.get("name"): item.get("value")
        for item in verify["container"].get("env", [])
    }
    require(verify_environment.get("HOME") == "/tmp", "skaffold:writable-home-missing")
    require(
        verify_environment.get("KUBECONFIG")
        == "/tmp/natureprotector-verifier-kubeconfig",
        "skaffold:explicit-kubeconfig-missing",
    )
    require(
        ">/dev/null 2>&1" not in skaffold_text,
        "skaffold:kubectl-errors-must-not-be-hidden",
    )
    require(
        "is_fatal_kubectl_error" in skaffold_text,
        "skaffold:fatal-kubectl-errors-must-fail-fast",
    )
    require(
        "kubectl patch secret natureprotector-rabbitmq-default-user" in skaffold_text,
        "skaffold:rabbitmq-secret-reconciliation-missing",
    )
    require(
        '\\"data\\":{\\"uri\\"' in skaffold_text
        and "rabbitmq_management_uri_b64" in skaffold_text
        and "stringData" not in skaffold_text,
        "skaffold:rabbitmq-secret-uri-must-patch-data",
    )

    job = yaml.safe_load(read(job_path))
    pod_spec = job["spec"]["template"]["spec"]
    container = pod_spec["containers"][0]

    require(job["metadata"].get("namespace") == "natureprotector-staging", "job:wrong-namespace")
    require(
        pod_spec.get("serviceAccountName") == "natureprotector-deploy-verifier",
        "VERIFY_JOB_REFERENCES_EXISTING_SERVICE_ACCOUNT",
    )
    require(pod_spec.get("automountServiceAccountToken") is True, "job:token-not-mounted")
    require(job["spec"].get("backoffLimit") == 0, "job:backoff-must-be-zero")
    require(job["spec"].get("activeDeadlineSeconds", 0) <= 3300, "job:deadline-too-long")
    require(container.get("image") == "CLOUDSDK_IMAGE_BY_DIGEST", "job:image-placeholder-mismatch")
    require(
        container.get("securityContext", {}).get("readOnlyRootFilesystem") is True,
        "job:root-filesystem-not-read-only",
    )
    require(
        container.get("resources", {}).get("requests", {}).get("cpu") == "50m",
        "job:cpu-request-unexpected",
    )
    environment = {
        item.get("name"): item.get("value")
        for item in container.get("env", [])
    }
    require(environment.get("HOME") == "/tmp", "job:writable-home-missing")

    support_render = render_kustomize(support_staging_overlay_path)
    support_names = {
        (document.get("kind"), document.get("metadata", {}).get("name"))
        for document in support_render
    }
    expected_support = {
        ("ServiceAccount", "natureprotector-deploy-verifier"),
        ("Role", "natureprotector-deploy-verifier"),
        ("RoleBinding", "natureprotector-deploy-verifier"),
        ("NetworkPolicy", "natureprotector-deploy-verifier"),
    }
    require(expected_support <= support_names, "VERIFIER_SUPPORT_RENDER_VALID")
    require(
        all(
            document.get("metadata", {}).get("namespace") == "natureprotector-staging"
            for document in support_render
        ),
        "VERIFIER_SUPPORT_STAGING_NAMESPACE_CORRECT",
    )

    role = find_doc(support_render, "Role", "natureprotector-deploy-verifier")
    role_binding = find_doc(support_render, "RoleBinding", "natureprotector-deploy-verifier")
    network = find_doc(support_render, "NetworkPolicy", "natureprotector-deploy-verifier")
    require(role is not None, "support-role:missing")
    require(role_binding is not None, "support-role-binding:missing")
    require(network is not None, "support-network-policy:missing")

    if role is not None:
        role_text = yaml.safe_dump(role)
        for token in (
            "secrets",
            "rabbitmq.com",
            "users",
            "permissions",
            "policies",
            "keda.sh",
            "scaledobjects",
            "apps",
            "deployments",
            "patch",
            "watch",
        ):
            require(token in role_text, f"support-role:missing:{token}")

    if role_binding is not None:
        service_account_subject = next(
            (
                subject
                for subject in role_binding.get("subjects", [])
                if subject.get("kind") == "ServiceAccount"
                and subject.get("name") == "natureprotector-deploy-verifier"
            ),
            None,
        )
        require(
            service_account_subject is not None
            and service_account_subject.get("namespace") == "natureprotector-staging",
            "VERIFIER_ROLE_BINDING_SUBJECT_CORRECT",
        )
        require(
            role_binding.get("roleRef", {}).get("kind") == "Role"
            and role_binding.get("roleRef", {}).get("name") == "natureprotector-deploy-verifier",
            "support-role-binding:role-ref-mismatch",
        )

    if network is not None:
        require(
            network["spec"]["podSelector"]["matchLabels"].get("np.network/deploy-verifier")
            == "true",
            "support-network:selector-mismatch",
        )
        network_text = yaml.safe_dump(network)
        require("0.0.0.0/0" in network_text and "443" in network_text, "support-network:api-egress-missing")

    support_base_role_binding = yaml.safe_load(read(support_base_path / "role-binding.yaml"))
    base_subject = support_base_role_binding.get("subjects", [])[0]
    require(
        "namespace" not in base_subject,
        "support-base:service-account-subject-namespace-must-be-overlay-transformed",
    )

    base_kustomization = yaml.safe_load(read(base_kustomization_path))
    base_resources = set(base_kustomization.get("resources", []))
    require("deploy-verifier-rbac.yaml" not in base_resources, "application-base:rbac-still-included")
    require(
        "deploy-verifier-network-policy.yaml" not in base_resources,
        "application-base:network-policy-still-included",
    )

    app_render = render_kustomize(app_overlay_path)
    app_verifier_docs = [
        document
        for document in app_render
        if document.get("metadata", {}).get("name") == "natureprotector-deploy-verifier"
    ]
    app_verifier_kinds = {document.get("kind") for document in app_verifier_docs}
    require(
        {"ServiceAccount", "Role", "RoleBinding"}.isdisjoint(app_verifier_kinds),
        "APPLICATION_RENDER_EXCLUDES_VERIFIER_RBAC",
    )
    require(
        "NetworkPolicy" not in app_verifier_kinds,
        "APPLICATION_RENDER_EXCLUDES_VERIFIER_NETWORK_POLICY",
    )

    deploy_script = read(deploy_script_path)
    ensure_index = deploy_script.find("Ensure-G81PreventionVerifierSupport.ps1")
    prevention_index = deploy_script.find('Pipeline = "natureprotector-prevention"')
    require(
        ensure_index >= 0 and prevention_index >= 0 and ensure_index < prevention_index,
        "STAGING_DEPLOY_ENSURES_SUPPORT_BEFORE_PREVENTION_RELEASE",
    )
    require("-Environment staging" in deploy_script, "deploy:staging-support-environment-missing")
    require("-Environment production" not in deploy_script, "PRODUCTION_NOT_EXECUTED")
    require("-AllowProduction" not in deploy_script, "PRODUCTION_NOT_EXECUTED")

    ensure_script = read(ensure_script_path)
    for token in (
        'ValidateSet("staging", "production")',
        "AllowProduction",
        "--dry-run=server",
        "--field-manager=$fieldManager",
        "kubectl auth can-i",
        "kubectl create namespace $Namespace",
        "pod-security.kubernetes.io/enforce=restricted",
        "kubectl apply",
        "rolebinding/natureprotector-deploy-verifier",
        "VERIFIER_SUPPORT_ENSURED",
    ):
        require(token in ensure_script, f"ensure-script:missing:{token}")
    require(
        "production verifier support apply requires -allowproduction" in ensure_script.lower(),
        "ensure-script:production-guard-missing",
    )
    require(
        "natureprotector-verifier-support-foundation" in ensure_script,
        "ENSURE_SCRIPT_IS_IDEMPOTENT",
    )

if failures:
    print("PREVENTION_IN_CLUSTER_VERIFIER_STATIC_FAIL")
    for failure in failures:
        print(f" - {failure}")
    sys.exit(1)

print("PREVENTION_IN_CLUSTER_VERIFIER_STATIC_PASS")
