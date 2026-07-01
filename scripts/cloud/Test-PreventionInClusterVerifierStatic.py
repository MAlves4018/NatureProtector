#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import sys
import yaml

ROOT = Path(__file__).resolve().parents[2]
failures: list[str] = []

def require(condition: bool, message: str) -> None:
    if not condition:
        failures.append(message)

skaffold_path = ROOT / "infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml"
job_path = ROOT / "infra/gcp/cloud-deploy/g8-1/prevention/verify-job-staging.yaml"
rbac_path = ROOT / "infra/gcp/kubernetes/g8-1/base/deploy-verifier-rbac.yaml"
network_path = ROOT / "infra/gcp/kubernetes/g8-1/base/deploy-verifier-network-policy.yaml"
kustomization_path = ROOT / "infra/gcp/kubernetes/g8-1/base/kustomization.yaml"

for path in (skaffold_path, job_path, rbac_path, network_path, kustomization_path):
    require(path.is_file(), f"missing:{path.relative_to(ROOT)}")

if not failures:
    skaffold_text = skaffold_path.read_text(encoding="utf-8")
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

    job = yaml.safe_load(job_path.read_text(encoding="utf-8"))
    pod_spec = job["spec"]["template"]["spec"]
    container = pod_spec["containers"][0]

    require(job["metadata"].get("namespace") == "natureprotector-staging", "job:wrong-namespace")
    require(
        pod_spec.get("serviceAccountName") == "natureprotector-deploy-verifier",
        "job:wrong-service-account",
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
        pod_spec["containers"][0].get("resources", {}).get("requests", {}).get("cpu") == "50m",
        "job:cpu-request-unexpected",
    )
    environment = {
        item.get("name"): item.get("value")
        for item in container.get("env", [])
    }
    require(environment.get("HOME") == "/tmp", "job:writable-home-missing")

    docs = list(yaml.safe_load_all(rbac_path.read_text(encoding="utf-8")))
    kinds = {doc["kind"] for doc in docs}
    require({"ServiceAccount", "Role", "RoleBinding"} <= kinds, "rbac:objects-missing")
    role = next(doc for doc in docs if doc["kind"] == "Role")
    role_binding = next(doc for doc in docs if doc["kind"] == "RoleBinding")
    service_account_subject = next(
        subject
        for subject in role_binding.get("subjects", [])
        if subject.get("kind") == "ServiceAccount"
        and subject.get("name") == "natureprotector-deploy-verifier"
    )
    require(
        "namespace" not in service_account_subject,
        "rbac:service-account-subject-namespace-must-be-overlay-transformed",
    )
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
        require(token in role_text, f"rbac:missing:{token}")

    network = yaml.safe_load(network_path.read_text(encoding="utf-8"))
    require(
        network["spec"]["podSelector"]["matchLabels"].get("np.network/deploy-verifier") == "true",
        "network:selector-mismatch",
    )
    network_text = yaml.safe_dump(network)
    require("0.0.0.0/0" in network_text and "443" in network_text, "network:api-egress-missing")

    kustomization = yaml.safe_load(kustomization_path.read_text(encoding="utf-8"))
    resources = set(kustomization.get("resources", []))
    require("deploy-verifier-rbac.yaml" in resources, "kustomize:rbac-not-included")
    require(
        "deploy-verifier-network-policy.yaml" in resources,
        "kustomize:network-policy-not-included",
    )

if failures:
    print("PREVENTION_IN_CLUSTER_VERIFIER_STATIC_FAIL")
    for failure in failures:
        print(f" - {failure}")
    sys.exit(1)

print("PREVENTION_IN_CLUSTER_VERIFIER_STATIC_PASS")
