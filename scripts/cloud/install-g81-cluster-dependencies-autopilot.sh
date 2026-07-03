#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C
export CLOUDSDK_CORE_DISABLE_PROMPTS=1

PROJECT_ID="${1:?project id required}"
REGION="${2:?region required}"
CLUSTER_NAME="${3:?cluster name required}"
LOCK_PATH="${4:?operator lock path required}"
EVIDENCE_DIR="${5:?evidence directory required}"

ARTIFACT_REPOSITORY="np-releases"
ARTIFACT_HOST="${REGION}-docker.pkg.dev"
DEPLOY_SERVICE_ACCOUNT="np-cd-deploy@${PROJECT_ID}.iam.gserviceaccount.com"
MIRROR_ATTEMPT_ID="operator-mirror-r3-amd64-$(date -u +%Y%m%d%H%M%S)-${BASHPID}"
MIRROR_ROOT="${ARTIFACT_HOST}/${PROJECT_ID}/${ARTIFACT_REPOSITORY}/${MIRROR_ATTEMPT_ID}"
FIELD_MANAGER="natureprotector-g81-autopilot-r3"
ROLLOUT_TIMEOUT_SECONDS="${NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS:-1800}"
[[ "$ROLLOUT_TIMEOUT_SECONDS" =~ ^[0-9]+$ ]] || {
  echo "ERROR: NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS must be numeric." >&2
  exit 2
}
if (( ROLLOUT_TIMEOUT_SECONDS < 300 || ROLLOUT_TIMEOUT_SECONDS > 3600 )); then
  echo "ERROR: NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS must be between 300 and 3600." >&2
  exit 2
fi

mkdir -p "$EVIDENCE_DIR"/{downloads,patched,release-metadata,diagnostics,mirror}
exec > >(tee "$EVIDENCE_DIR/operator-bootstrap.log") 2>&1

for tool in gh gcloud kubectl py sha256sum; do
  command -v "$tool" >/dev/null 2>&1 || {
    echo "ERROR: required operator-bootstrap tool unavailable: $tool" >&2
    exit 2
  }
done

[[ -f "$LOCK_PATH" ]] || {
  echo "ERROR: operator lock not found: $LOCK_PATH" >&2
  exit 2
}

PROJECT_NUMBER="$(
  gcloud projects describe "$PROJECT_ID" \
    --format='value(projectNumber)' | tr -d '\r\n'
)"
[[ "$PROJECT_NUMBER" =~ ^[0-9]+$ ]] || {
  echo "ERROR: project number could not be resolved." >&2
  exit 2
}
EVIDENCE_BUCKET="np-g82-evidence-${PROJECT_NUMBER}"
CLOUD_BUILD_LOG_BUCKET="np-cloudbuild-logs-${PROJECT_NUMBER}"
GCS_MIRROR_PREFIX="gs://${EVIDENCE_BUCKET}/operator-mirror/${MIRROR_ATTEMPT_ID}"

printf '%s\n' "$MIRROR_ATTEMPT_ID" > "$EVIDENCE_DIR/mirror-attempt-id.txt"
printf '%s\n' "$DEPLOY_SERVICE_ACCOUNT" > "$EVIDENCE_DIR/mirror-service-account.txt"
printf '%s\n' "$GCS_MIRROR_PREFIX" > "$EVIDENCE_DIR/mirror-evidence-prefix.txt"
printf '%s\n' 'fresh-single-platform-linux-amd64' > "$EVIDENCE_DIR/mirror-mode.txt"

capture_namespace() {
  local namespace="$1"
  local label="$2"
  local out="$EVIDENCE_DIR/diagnostics/${label}-${namespace}"
  mkdir -p "$out"

  kubectl -n "$namespace" get deployments,statefulsets,replicasets,pods,services,endpoints,events,leases \
    -o wide > "$out/resources-wide.txt" 2>&1 || true
  kubectl -n "$namespace" get deployments,statefulsets,replicasets,pods,services,endpoints,events,leases \
    -o json > "$out/resources.json" 2>&1 || true

  for deployment in $(kubectl -n "$namespace" get deployment -o name 2>/dev/null || true); do
    local safe="${deployment//\//-}"
    kubectl -n "$namespace" describe "$deployment" \
      > "$out/describe-${safe}.txt" 2>&1 || true
  done

  for pod in $(kubectl -n "$namespace" get pod -o name 2>/dev/null || true); do
    local pod_name="${pod#pod/}"
    local safe="${pod_name//\//-}"
    kubectl -n "$namespace" describe "$pod" \
      > "$out/describe-pod-${safe}.txt" 2>&1 || true

    kubectl -n "$namespace" get "$pod" -o json \
      > "$out/pod-${safe}.json" 2>&1 || true

    for container in $(
      kubectl -n "$namespace" get "$pod" -o json 2>/dev/null |
      py -3.12 -c '
import json, sys
try:
    data=json.load(sys.stdin)
except Exception:
    raise SystemExit(0)
for item in data.get("spec", {}).get("initContainers", []):
    print(item.get("name", ""))
for item in data.get("spec", {}).get("containers", []):
    print(item.get("name", ""))
'
    ); do
      [[ -n "$container" ]] || continue
      kubectl -n "$namespace" logs "$pod" -c "$container" --tail=500 \
        > "$out/log-${safe}-${container}.txt" 2>&1 || true
      kubectl -n "$namespace" logs "$pod" -c "$container" --previous --tail=500 \
        > "$out/log-${safe}-${container}-previous.txt" 2>&1 || true
    done
  done
}

capture_all_operator_diagnostics() {
  local label="$1"
  capture_namespace cert-manager "$label"
  capture_namespace rabbitmq-system "$label"
  capture_namespace keda "$label"
  kubectl get storageclass -o json \
    > "$EVIDENCE_DIR/diagnostics/${label}-storageclasses.json" 2>&1 || true
  kubectl get apiservices -o json \
    > "$EVIDENCE_DIR/diagnostics/${label}-apiservices.json" 2>&1 || true
  kubectl get crd -o json \
    > "$EVIDENCE_DIR/diagnostics/${label}-crds.json" 2>&1 || true
}

on_error() {
  local rc=$?
  local line="${BASH_LINENO[0]:-unknown}"
  trap - ERR
  echo "ERROR: operator foundation failed at line $line with exit code $rc." >&2
  capture_all_operator_diagnostics failure || true
  exit "$rc"
}
trap on_error ERR

on_interrupt() {
  local signal="$1"
  trap - ERR INT TERM
  echo "ERROR: operator foundation interrupted by ${signal}." >&2
  capture_all_operator_diagnostics "interrupted-${signal}" || true
  exit 130
}
trap 'on_interrupt INT' INT
trap 'on_interrupt TERM' TERM

echo "OPERATOR_FOUNDATION_PROJECT=$PROJECT_ID"
echo "OPERATOR_FOUNDATION_REGION=$REGION"
echo "OPERATOR_FOUNDATION_CLUSTER=$CLUSTER_NAME"
echo "CLUSTER_DEPENDENCY=cert-manager"
echo "CLUSTER_DEPENDENCY_STATUS=WAITING"
echo "CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS=$ROLLOUT_TIMEOUT_SECONDS"

gcloud container clusters get-credentials "$CLUSTER_NAME" \
  --project="$PROJECT_ID" \
  --region="$REGION" \
  --dns-endpoint \
  --quiet

kubectl get namespace default --request-timeout=60s -o json \
  > "$EVIDENCE_DIR/diagnostics/default-namespace.json"

kubectl auth can-i create customresourcedefinitions.apiextensions.k8s.io \
  > "$EVIDENCE_DIR/diagnostics/can-create-crd.txt"
kubectl auth can-i create deployments.apps --namespace cert-manager \
  > "$EVIDENCE_DIR/diagnostics/can-create-cert-manager-deployment.txt"
kubectl auth can-i create persistentvolumeclaims --namespace natureprotector-staging \
  > "$EVIDENCE_DIR/diagnostics/can-create-pvc.txt"

grep -qx 'yes' "$EVIDENCE_DIR/diagnostics/can-create-crd.txt"
grep -qx 'yes' "$EVIDENCE_DIR/diagnostics/can-create-cert-manager-deployment.txt"
grep -qx 'yes' "$EVIDENCE_DIR/diagnostics/can-create-pvc.txt"

kubectl get storageclass -o json \
  > "$EVIDENCE_DIR/diagnostics/storageclasses-before.json"

py -3.12 - "$EVIDENCE_DIR/diagnostics/storageclasses-before.json" <<'PY'
from pathlib import Path
import json, sys
data=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
defaults=[]
for item in data.get("items", []):
    annotations=item.get("metadata", {}).get("annotations", {})
    if annotations.get("storageclass.kubernetes.io/is-default-class")=="true" or \
       annotations.get("storageclass.beta.kubernetes.io/is-default-class")=="true":
        defaults.append(item.get("metadata", {}).get("name"))
if not defaults:
    raise SystemExit("No default StorageClass is available for RabbitMQ PVCs")
print("DEFAULT_STORAGE_CLASSES=" + ",".join(defaults))
PY

gcloud container binauthz policy export \
  --project="$PROJECT_ID" \
  > "$EVIDENCE_DIR/diagnostics/binary-authorization-policy.yaml" 2>&1 || true

capture_all_operator_diagnostics before-remediation || true

py -3.12 - "$LOCK_PATH" "$EVIDENCE_DIR/dependencies.tsv" <<'PY'
from pathlib import Path
import json, sys
lock=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
if int(lock.get("schema_version", 0)) != 1:
    raise SystemExit("Unsupported operator lock schema")
lines=[]
for dep in lock.get("dependencies", []):
    rollouts=",".join(dep.get("rollouts", []))
    fields=[
        dep["name"], dep["repository"], dep["tag"], dep["asset"],
        dep["namespace"], rollouts,
    ]
    if any("\t" in value or "\n" in value for value in fields):
        raise SystemExit("Unsupported tab/newline in dependency lock")
    lines.append("\t".join(fields))
Path(sys.argv[2]).write_text(
    "\n".join(lines)+"\n",
    encoding="utf-8",
    newline="\n",
)
PY

while IFS=$'\t' read -r name repository tag asset namespace rollouts; do
  [[ -n "$name" ]] || continue
  rollouts="${rollouts//$'\r'/}"
  release_json="$EVIDENCE_DIR/release-metadata/${name}-release.json"
  gh api "repos/${repository}/releases/tags/${tag}" > "$release_json"

  py -3.12 - "$release_json" "$asset" "$EVIDENCE_DIR/release-metadata/${name}-asset.env" <<'PY'
from pathlib import Path
import json, shlex, sys
release=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
asset_name=sys.argv[2]
matches=[item for item in release.get("assets", []) if item.get("name")==asset_name]
if len(matches)!=1:
    raise SystemExit(f"Expected one asset {asset_name}, got {len(matches)}")
asset=matches[0]
digest=asset.get("digest")
if not isinstance(digest, str) or not digest.startswith("sha256:") or len(digest)!=71:
    raise SystemExit(f"GitHub asset digest is missing or invalid: {digest!r}")
values={
    "RELEASE_ID": str(release.get("id", "")),
    "PUBLISHED_AT": str(release.get("published_at", "")),
    "ASSET_ID": str(asset.get("id", "")),
    "EXPECTED_SHA256": digest[7:].lower(),
}
Path(sys.argv[3]).write_text(
    "\n".join(f"{k}={shlex.quote(v)}" for k,v in values.items())+"\n",
    encoding="utf-8",
    newline="\n",
)
PY

  # shellcheck disable=SC1090
  source "$EVIDENCE_DIR/release-metadata/${name}-asset.env"

  destination="$EVIDENCE_DIR/downloads/$asset"
  rm -f "$destination"
  gh release download "$tag" \
    --repo "$repository" \
    --pattern "$asset" \
    --dir "$EVIDENCE_DIR/downloads" \
    --clobber

  actual_sha256="$(sha256sum "$destination" | awk '{print $1}')"
  [[ "$actual_sha256" == "$EXPECTED_SHA256" ]] || {
    echo "ERROR: $name asset digest mismatch." >&2
    exit 3
  }

  py -3.12 - \
    "$name" "$repository" "$tag" "$asset" "$namespace" "$rollouts" \
    "$RELEASE_ID" "$ASSET_ID" "$PUBLISHED_AT" "$actual_sha256" \
    "$EVIDENCE_DIR/release-metadata/${name}-resolved.json" <<'PY'
from pathlib import Path
import json, sys
keys=[
    "name","repository","tag","asset","namespace","rollouts",
    "release_id","asset_id","published_at","sha256",
]
values=sys.argv[1:11]
record=dict(zip(keys, values))
record["rollouts"]=[item for item in record["rollouts"].split(",") if item]
Path(sys.argv[11]).write_text(
    json.dumps(record, indent=2)+"\n",
    encoding="utf-8",
    newline="\n",
)
PY
done < "$EVIDENCE_DIR/dependencies.tsv"

py -3.12 - "$EVIDENCE_DIR/downloads" "$EVIDENCE_DIR/mirror/image-map.json" "$MIRROR_ROOT" <<'PY'
from pathlib import Path
import hashlib, json, re, sys, yaml

root=Path(sys.argv[1])
out=Path(sys.argv[2])
mirror_root=sys.argv[3]
images=set()

def collect(obj):
    if isinstance(obj, dict):
        for key, value in obj.items():
            if key=="image" and isinstance(value, str):
                images.add(value)
            collect(value)
    elif isinstance(obj, list):
        for item in obj:
            collect(item)
    elif isinstance(obj, str):
        marker="--acme-http01-solver-image="
        if obj.startswith(marker):
            images.add(obj[len(marker):])

for path in root.iterdir():
    if path.suffix.lower() not in {".yaml",".yml"}:
        continue
    for doc in yaml.safe_load_all(path.read_text(encoding="utf-8")):
        collect(doc)

mapping={}
for source in sorted(images):
    if source.startswith(mirror_root + "/"):
        mapping[source]=source
        continue
    tail=source.rsplit("/",1)[-1]
    basename=tail.split("@",1)[0].split(":",1)[0]
    tag="pinned"
    if "@" not in tail and ":" in tail:
        tag=tail.rsplit(":",1)[1]
    tag=re.sub(r"[^A-Za-z0-9_.-]+","-",tag)[:80] or "pinned"
    safe=re.sub(r"[^a-z0-9._-]+","-",basename.lower()).strip("-") or "image"
    digest=hashlib.sha256(source.encode()).hexdigest()[:12]
    destination=f"{mirror_root}/{safe}-{digest}:{tag}"
    mapping[source]=destination

out.write_text(
    json.dumps(mapping, indent=2)+"\n",
    encoding="utf-8",
    newline="\n",
)
print(f"OPERATOR_IMAGE_COUNT={len(mapping)}")
for source, destination in mapping.items():
    print(f"OPERATOR_IMAGE={source} -> {destination}")
PY

gcloud artifacts repositories describe "$ARTIFACT_REPOSITORY" \
  --project="$PROJECT_ID" \
  --location="$REGION" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/artifact-repository.json"

gcloud artifacts repositories get-iam-policy "$ARTIFACT_REPOSITORY" \
  --project="$PROJECT_ID" \
  --location="$REGION" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/artifact-repository-policy.json"

gcloud container clusters describe "$CLUSTER_NAME" \
  --project="$PROJECT_ID" \
  --region="$REGION" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/cluster.json"

GKE_NODE_SERVICE_ACCOUNT="$(
  py -3.12 - "$EVIDENCE_DIR/mirror/cluster.json" <<'PY'
from pathlib import Path
import json, sys
cluster=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
service_account=cluster.get("nodeConfig", {}).get("serviceAccount") or ""
if not service_account:
    pools=cluster.get("nodePools", [])
    if pools:
        service_account=pools[0].get("config", {}).get("serviceAccount") or ""
if not service_account or service_account == "default":
    raise SystemExit("GKE node service account could not be resolved")
print(service_account)
PY
)"
printf '%s\n' "$GKE_NODE_SERVICE_ACCOUNT" > "$EVIDENCE_DIR/mirror/gke-node-service-account.txt"

gcloud artifacts repositories add-iam-policy-binding "$ARTIFACT_REPOSITORY" \
  --project="$PROJECT_ID" \
  --location="$REGION" \
  --member="serviceAccount:${GKE_NODE_SERVICE_ACCOUNT}" \
  --role="roles/artifactregistry.reader" \
  --condition=None \
  --quiet \
  > "$EVIDENCE_DIR/mirror/gke-node-artifact-reader-iam.txt"

gcloud artifacts repositories get-iam-policy "$ARTIFACT_REPOSITORY" \
  --project="$PROJECT_ID" \
  --location="$REGION" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/artifact-repository-policy-after-node-reader.json"

gcloud storage buckets describe "gs://${EVIDENCE_BUCKET}" \
  --project="$PROJECT_ID" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/evidence-bucket.json"

gcloud storage buckets get-iam-policy "gs://${EVIDENCE_BUCKET}" \
  --project="$PROJECT_ID" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/evidence-bucket-policy.json"

gcloud iam service-accounts describe "$DEPLOY_SERVICE_ACCOUNT" \
  --project="$PROJECT_ID" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/deploy-service-account.json"

gcloud iam service-accounts get-iam-policy "$DEPLOY_SERVICE_ACCOUNT" \
  --project="$PROJECT_ID" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/deploy-service-account-policy.json"

gcloud projects get-iam-policy "$PROJECT_ID" \
  --format=json \
  > "$EVIDENCE_DIR/mirror/project-iam-policy.json"

py -3.12 - \
  "$EVIDENCE_DIR/mirror/artifact-repository.json" \
  "$EVIDENCE_DIR/mirror/artifact-repository-policy-after-node-reader.json" \
  "$EVIDENCE_DIR/mirror/evidence-bucket.json" \
  "$EVIDENCE_DIR/mirror/evidence-bucket-policy.json" \
  "$EVIDENCE_DIR/mirror/project-iam-policy.json" \
  "$DEPLOY_SERVICE_ACCOUNT" \
  "$GKE_NODE_SERVICE_ACCOUNT" \
  "$EVIDENCE_DIR/mirror/mirror-preflight.json" <<'PY'
from pathlib import Path
import json, sys
repo=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
repo_policy=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
bucket=json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
bucket_policy=json.loads(Path(sys.argv[4]).read_text(encoding="utf-8-sig"))
project_policy=json.loads(Path(sys.argv[5]).read_text(encoding="utf-8-sig"))
service_account=sys.argv[6]
node_service_account=sys.argv[7]
service_account_member=f"serviceAccount:{service_account}"
node_service_account_member=f"serviceAccount:{node_service_account}"

def roles_for(policy, member):
    return {
        binding.get("role", "")
        for binding in policy.get("bindings", [])
        if member in binding.get("members", [])
    }

project_roles=roles_for(project_policy, service_account_member)
repository_roles=roles_for(repo_policy, service_account_member)
bucket_roles=roles_for(bucket_policy, service_account_member)
node_repository_roles=roles_for(repo_policy, node_service_account_member)
artifact_roles=project_roles | repository_roles
writer_roles={
    "roles/artifactregistry.writer",
    "roles/artifactregistry.repoAdmin",
    "roles/artifactregistry.admin",
    "roles/artifactregistry.createOnPushWriter",
    "roles/owner",
}
evidence_writer_roles={
    "roles/storage.objectCreator",
    "roles/storage.objectAdmin",
    "roles/storage.admin",
    "roles/owner",
}
checks={
    "repository_format_docker": repo.get("format")=="DOCKER",
    "repository_immutable_tags": repo.get("dockerConfig", {}).get("immutableTags") is True,
    "evidence_bucket_present": bool(bucket.get("name")),
    "deploy_service_account_can_write_artifacts": bool(artifact_roles & writer_roles),
    "deploy_service_account_can_write_evidence": bool((project_roles | bucket_roles) & evidence_writer_roles),
    "gke_node_service_account_can_pull_artifacts": "roles/artifactregistry.reader" in node_repository_roles,
}
errors=[name for name, passed in checks.items() if not passed]
result={
    "status":"PASS" if not errors else "FAIL",
    "service_account":service_account,
    "gke_node_service_account":node_service_account,
    "project_roles":sorted(project_roles),
    "repository_roles":sorted(repository_roles),
    "evidence_bucket_roles":sorted(bucket_roles),
    "gke_node_repository_roles":sorted(node_repository_roles),
    "checks":checks,
    "errors":errors,
}
Path(sys.argv[8]).write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Operator mirror preflight failed")
PY
if ! gcloud storage buckets describe "gs://${CLOUD_BUILD_LOG_BUCKET}" \
  --project="$PROJECT_ID" --format=json \
  > "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket-before.json" 2> "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket-before.stderr"; then
  echo "CLOUD_BUILD_LOG_BUCKET_CREATE=gs://${CLOUD_BUILD_LOG_BUCKET}"
  gcloud storage buckets create "gs://${CLOUD_BUILD_LOG_BUCKET}" \
    --project="$PROJECT_ID" \
    --location="$REGION" \
    --uniform-bucket-level-access \
    --public-access-prevention \
    --soft-delete-duration=0 \
    --quiet
fi

gcloud storage buckets add-iam-policy-binding "gs://${CLOUD_BUILD_LOG_BUCKET}" \
  --member="serviceAccount:${DEPLOY_SERVICE_ACCOUNT}" \
  --role="roles/storage.objectAdmin" \
  --condition=None \
  --quiet \
  > "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket-iam.txt"

gcloud storage buckets describe "gs://${CLOUD_BUILD_LOG_BUCKET}" \
  --project="$PROJECT_ID" --format=json \
  > "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket.json"
gcloud storage buckets get-iam-policy "gs://${CLOUD_BUILD_LOG_BUCKET}" \
  --project="$PROJECT_ID" --format=json \
  > "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket-policy.json"

py -3.12 - \
  "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket.json" \
  "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket-policy.json" \
  "$DEPLOY_SERVICE_ACCOUNT" <<'PYBUCKET'
from pathlib import Path
import json, sys
bucket=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
policy=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
member=f"serviceAccount:{sys.argv[3]}"
roles={
    binding.get("role", "")
    for binding in policy.get("bindings", [])
    if member in binding.get("members", [])
}
def bool_enabled(value):
    if isinstance(value, bool):
        return value
    if isinstance(value, dict):
        return bool(value.get("enabled"))
    return False

retention_values=[
    bucket.get("retention_policy"),
    bucket.get("retentionPolicy"),
]
retention=next(
    (
        value
        for value in retention_values
        if value not in (None, False, "", {}, [])
    ),
    None,
)

uniform=bool_enabled(bucket.get("uniform_bucket_level_access"))
if not uniform:
    uniform=bool_enabled(
        (bucket.get("iamConfiguration") or {}).get("uniformBucketLevelAccess")
    )

pap=(
    bucket.get("public_access_prevention")
    or (bucket.get("iamConfiguration") or {}).get("publicAccessPrevention")
)
checks={
    "cloud_build_log_bucket_present": bool(bucket.get("name")),
    "cloud_build_log_bucket_has_no_retention_policy": retention is None,
    "cloud_build_log_bucket_uniform_access": uniform,
    "cloud_build_log_bucket_public_access_prevention": pap == "enforced",
    "deploy_service_account_can_write_cloud_build_logs": bool(
        roles & {"roles/storage.objectAdmin", "roles/storage.admin", "roles/owner"}
    ),
}
errors=[name for name, passed in checks.items() if not passed]
print(json.dumps({"status":"PASS" if not errors else "FAIL", "checks":checks, "roles":sorted(roles), "errors":errors}, indent=2))
if errors:
    raise SystemExit("Cloud Build log bucket validation failed")
PYBUCKET
printf '%s\n' "gs://${CLOUD_BUILD_LOG_BUCKET}" > "$EVIDENCE_DIR/mirror/cloudbuild-log-bucket-uri.txt"
echo "CLOUD_BUILD_LOG_BUCKET_READY=gs://${CLOUD_BUILD_LOG_BUCKET}"

py -3.12 - "$EVIDENCE_DIR/mirror/image-map.json" "$EVIDENCE_DIR/mirror/image-map.tsv" <<'PY'
from pathlib import Path
import json, sys
mapping=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
Path(sys.argv[2]).write_text(
    "".join(f"{source}\t{destination}\n" for source,destination in mapping.items()),
    encoding="utf-8",
    newline="\n",
)
PY

while IFS=$'\t' read -r source destination; do
  [[ -n "$source" ]] || continue
  source="${source//$'\r'/}"
  destination="${destination//$'\r'/}"
  if [[ "$source" == *[[:space:]]* || "$destination" == *[[:space:]]* ]]; then
    echo "ERROR: whitespace remains in an operator image reference." >&2
    printf 'SOURCE=%q\nDESTINATION=%q\n' "$source" "$destination" >&2
    exit 5
  fi
  [[ "$destination" == "$MIRROR_ROOT/"* ]] || {
    echo "ERROR: destination is outside the new mirror namespace: $destination" >&2
    exit 5
  }
done < "$EVIDENCE_DIR/mirror/image-map.tsv"

cloudbuild_config="$EVIDENCE_DIR/mirror/cloudbuild-mirror.yaml"
py -3.12 - \
  "$cloudbuild_config" \
  "$EVIDENCE_DIR/mirror/image-map.json" \
  "$PROJECT_ID" \
  "$DEPLOY_SERVICE_ACCOUNT" \
  "$GCS_MIRROR_PREFIX" \
  "$CLOUD_BUILD_LOG_BUCKET" <<'PY'
from pathlib import Path
import json, shlex, sys, yaml
out=Path(sys.argv[1])
mapping=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
project_id=sys.argv[3]
service_account=sys.argv[4]
gcs_prefix=sys.argv[5]
cloud_build_log_bucket=sys.argv[6]
rows="\n".join(f"{source}\t{destination}" for source,destination in mapping.items())
script=f'''set -u
log=/workspace/operator-mirror.log
status=/workspace/operator-mirror-status.tsv
overall_file=/workspace/operator-mirror-overall.txt
: > "$log"
: > "$status"
exec > >(tee -a "$log") 2>&1

retry_command() {{
  label="$1"
  shift
  attempt=1
  rc=1
  retry_delay="${{NP_MIRROR_RETRY_DELAY_SECONDS:-10}}"
  while [ "$attempt" -le 3 ]; do
    printf 'RETRY_COMMAND label=%s attempt=%s/3\n' "$label" "$attempt"
    "$@"
    rc=$?
    if [ "$rc" -eq 0 ]; then
      return 0
    fi
    if [ "$attempt" -lt 3 ]; then
      sleep $((attempt * retry_delay))
    fi
    attempt=$((attempt + 1))
  done
  return "$rc"
}}

overall=0
while IFS=$'\t' read -r source destination; do
  [ -n "$source" ] || continue
  printf 'MIRROR_START source=%s destination=%s\n' "$source" "$destination"
  retry_command pull docker pull --platform=linux/amd64 "$source"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    printf '%s\t%s\tpull\t%s\n' "$source" "$destination" "$rc" >> "$status"
    overall=1
    continue
  fi
  source_platform="$(docker image inspect --format '{{{{.Os}}}}/{{{{.Architecture}}}}' "$source" 2>/dev/null || true)"
  if [ "$source_platform" != "linux/amd64" ]; then
    printf '%s\t%s\tsource-platform-%s\t9\n' "$source" "$destination" "$source_platform" >> "$status"
    echo "ERROR: source platform is $source_platform, expected linux/amd64" >&2
    overall=1
    continue
  fi
  docker tag "$source" "$destination"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    printf '%s\t%s\ttag\t%s\n' "$source" "$destination" "$rc" >> "$status"
    overall=1
    continue
  fi
  retry_command push docker push "$destination"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    printf '%s\t%s\tpush\t%s\n' "$source" "$destination" "$rc" >> "$status"
    overall=1
    continue
  fi
  docker image rm "$destination" >/dev/null 2>&1 || true
  retry_command verify-pull docker pull --platform=linux/amd64 "$destination"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    printf '%s\t%s\tverify-pull\t%s\n' "$source" "$destination" "$rc" >> "$status"
    overall=1
    continue
  fi
  destination_platform="$(docker image inspect --format '{{{{.Os}}}}/{{{{.Architecture}}}}' "$destination" 2>/dev/null || true)"
  if [ "$destination_platform" != "linux/amd64" ]; then
    printf '%s\t%s\tdestination-platform-%s\t9\n' "$source" "$destination" "$destination_platform" >> "$status"
    echo "ERROR: destination platform is $destination_platform, expected linux/amd64" >&2
    overall=1
    continue
  fi
  printf '%s\t%s\tverified-linux-amd64\t0\n' "$source" "$destination" >> "$status"
  printf 'MIRROR_PUSHED source=%s destination=%s platform=%s\n' "$source" "$destination" "$destination_platform"
done <<'NP_IMAGE_MAP'
{rows}
NP_IMAGE_MAP
printf '%s\n' "$overall" > "$overall_file"
printf 'MIRROR_OVERALL=%s\n' "$overall"
exit 0
'''
upload_script=f'''set -euo pipefail
for name in operator-mirror.log operator-mirror-status.tsv operator-mirror-overall.txt; do
  attempt=1
  while true; do
    if gcloud storage cp "/workspace/$name" {shlex.quote(gcs_prefix)}/"$name"; then
      break
    fi
    if [ "$attempt" -ge 3 ]; then
      echo "ERROR: failed to upload mirror evidence file $name" >&2
      exit 1
    fi
    sleep $((attempt * 10))
    attempt=$((attempt + 1))
  done
done
'''
verdict_script='''set -euo pipefail
value="$(tr -d '\\r\\n' < /workspace/operator-mirror-overall.txt)"
if [ "$value" != "0" ]; then
  echo "ERROR: one or more operator images failed to mirror." >&2
  cat /workspace/operator-mirror-status.tsv >&2
  exit 1
fi
echo "OPERATOR_MIRROR_BUILD_SUCCEEDED"
'''
config={
    "steps":[
        {
            "id":"mirror",
            "name":"gcr.io/cloud-builders/docker",
            "entrypoint":"bash",
            "args":["-c", script],
        },
        {
            "id":"upload-evidence",
            "name":"gcr.io/google.com/cloudsdktool/google-cloud-cli:stable",
            "entrypoint":"bash",
            "args":["-c", upload_script],
            "waitFor":["mirror"],
        },
        {
            "id":"mirror-verdict",
            "name":"gcr.io/google.com/cloudsdktool/google-cloud-cli:stable",
            "entrypoint":"bash",
            "args":["-c", verdict_script],
            "waitFor":["upload-evidence"],
        },
    ],
    "serviceAccount":f"projects/{project_id}/serviceAccounts/{service_account}",
    "logsBucket":f"gs://{cloud_build_log_bucket}",
    "options":{"logging":"GCS_ONLY"},
    "timeout":"3600s",
}
out.write_text(
    yaml.safe_dump(config, sort_keys=False, width=1000000),
    encoding="utf-8",
    newline="\n",
)
PY

CLOUD_BUILD_SERVICE_ACCOUNT_RESOURCE="projects/${PROJECT_ID}/serviceAccounts/${DEPLOY_SERVICE_ACCOUNT}"
printf '%s\n' "$CLOUD_BUILD_SERVICE_ACCOUNT_RESOURCE" \
  > "$EVIDENCE_DIR/mirror/cloudbuild-service-account.txt"
echo "CLOUD_BUILD_MIRROR_SERVICE_ACCOUNT=$DEPLOY_SERVICE_ACCOUNT"
echo "CLOUD_BUILD_MIRROR_NAMESPACE=$MIRROR_ROOT"

set +e
BUILD_ID="$(
  gcloud builds submit \
    --project="$PROJECT_ID" \
    --region="$REGION" \
    --no-source \
    --config="$cloudbuild_config" \
    --service-account="$CLOUD_BUILD_SERVICE_ACCOUNT_RESOURCE" \
    --timeout=3600s \
    --async \
    --format='value(id)' \
    --quiet \
    2> "$EVIDENCE_DIR/mirror/cloudbuild-submit.stderr" |
  tr -d '\r\n'
)"
SUBMIT_RC=$?
set -e
printf '%s\n' "$BUILD_ID" > "$EVIDENCE_DIR/mirror/cloudbuild-build-id.txt"
if (( SUBMIT_RC != 0 )) || [[ -z "$BUILD_ID" ]]; then
  cat "$EVIDENCE_DIR/mirror/cloudbuild-submit.stderr" >&2 || true
  echo "ERROR: Cloud Build mirror submission failed. The active principal may lack iam.serviceAccounts.actAs on $DEPLOY_SERVICE_ACCOUNT." >&2
  exit 6
fi

echo "CLOUD_BUILD_MIRROR_ID=$BUILD_ID"
BUILD_STATUS=""
BUILD_DEADLINE=$(( $(date +%s) + 3900 ))
while (( $(date +%s) < BUILD_DEADLINE )); do
  gcloud builds describe "$BUILD_ID" \
    --project="$PROJECT_ID" \
    --region="$REGION" \
    --format=json \
    > "$EVIDENCE_DIR/mirror/cloudbuild-mirror-latest.json"
  BUILD_STATUS="$(
    py -3.12 - "$EVIDENCE_DIR/mirror/cloudbuild-mirror-latest.json" <<'PY'
from pathlib import Path
import json, sys
print(json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig")).get("status", ""))
PY
  )"
  echo "CLOUD_BUILD_MIRROR_STATUS=$BUILD_STATUS"
  case "$BUILD_STATUS" in
    SUCCESS|FAILURE|INTERNAL_ERROR|TIMEOUT|CANCELLED|EXPIRED)
      break
      ;;
  esac
  sleep 5
done

cp "$EVIDENCE_DIR/mirror/cloudbuild-mirror-latest.json" \
  "$EVIDENCE_DIR/mirror/cloudbuild-mirror-result.json"
mkdir -p "$EVIDENCE_DIR/mirror/cloudbuild-evidence"
: > "$EVIDENCE_DIR/mirror/cloudbuild-evidence-download.log"
for evidence_name in operator-mirror.log operator-mirror-status.tsv operator-mirror-overall.txt; do
  gcloud storage cp \
    "${GCS_MIRROR_PREFIX}/${evidence_name}" \
    "$EVIDENCE_DIR/mirror/cloudbuild-evidence/${evidence_name}" \
    >> "$EVIDENCE_DIR/mirror/cloudbuild-evidence-download.log" 2>&1 || true
done

if [[ "$BUILD_STATUS" != "SUCCESS" ]]; then
  cat "$EVIDENCE_DIR/mirror/cloudbuild-evidence/operator-mirror.log" 2>/dev/null || true
  cat "$EVIDENCE_DIR/mirror/cloudbuild-evidence/operator-mirror-status.tsv" 2>/dev/null || true
  echo "ERROR: Cloud Build operator mirror ended with status $BUILD_STATUS." >&2
  exit 6
fi

: > "$EVIDENCE_DIR/mirror/image-map-digest.tsv"
while IFS=$'\t' read -r source destination; do
  [[ -n "$source" ]] || continue
  source="${source//$'\r'/}"
  destination="${destination//$'\r'/}"
  digest="$(
    gcloud artifacts docker images describe "$destination" \
      --project="$PROJECT_ID" \
      --format='value(image_summary.digest)' |
    tr -d '\r\n'
  )"
  [[ "$digest" =~ ^sha256:[0-9a-f]{64}$ ]] || {
    echo "ERROR: invalid Artifact Registry digest for $destination: $digest" >&2
    exit 7
  }
  image_without_tag="${destination%:*}"
  digest_reference="${image_without_tag}@${digest}"
  printf '%s\t%s\t%s\n' "$source" "$destination" "$digest_reference" \
    >> "$EVIDENCE_DIR/mirror/image-map-digest.tsv"
  gcloud artifacts docker images describe "$destination" \
    --project="$PROJECT_ID" \
    --format=json \
    > "$EVIDENCE_DIR/mirror/$(echo "$destination" | sha256sum | cut -c1-16)-verified.json"
  echo "MIRROR_DIGEST_VERIFIED=$source -> $digest_reference"
done < "$EVIDENCE_DIR/mirror/image-map.tsv"

py -3.12 - \
  "$EVIDENCE_DIR/mirror/image-map-digest.tsv" \
  "$EVIDENCE_DIR/mirror/image-map-digest.json" \
  "$EVIDENCE_DIR/mirror/operator-mirror-result.json" \
  "$BUILD_ID" \
  "$DEPLOY_SERVICE_ACCOUNT" \
  "$MIRROR_ATTEMPT_ID" <<'PY'
from pathlib import Path
import json, sys
rows=[]
mapping={}
for raw in Path(sys.argv[1]).read_text(encoding="utf-8-sig").splitlines():
    if not raw:
        continue
    source, destination_tag, destination_digest=raw.split("\t")
    mapping[source]=destination_digest
    rows.append({
        "source":source,
        "destination_tag":destination_tag,
        "destination_digest":destination_digest,
        "verified":True,
    })
if len(rows) != 9 or len(mapping) != 9:
    raise SystemExit(f"Expected 9 verified operator images, got {len(rows)}")
Path(sys.argv[2]).write_text(json.dumps(mapping, indent=2)+"\n", encoding="utf-8")
result={
    "status":"PASS",
    "build_id":sys.argv[4],
    "service_account":sys.argv[5],
    "mirror_attempt_id":sys.argv[6],
    "image_count":len(rows),
    "images":rows,
}
Path(sys.argv[3]).write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
print(json.dumps(result, indent=2))
PY

echo "FRESH_LINUX_AMD64_OPERATOR_MIRROR_PROVED"

py -3.12 - \
  "$EVIDENCE_DIR/downloads" \
  "$EVIDENCE_DIR/patched" \
  "$EVIDENCE_DIR/mirror/image-map-digest.json" <<'PY'
from pathlib import Path
import json, sys, yaml

source_root=Path(sys.argv[1])
output_root=Path(sys.argv[2])
mapping=json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
output_root.mkdir(parents=True, exist_ok=True)

def rewrite(obj):
    if isinstance(obj, dict):
        for key, value in list(obj.items()):
            if key=="image" and isinstance(value, str) and value in mapping:
                obj[key]=mapping[value]
            else:
                obj[key]=rewrite(value)
        return obj
    if isinstance(obj, list):
        return [rewrite(item) for item in obj]
    if isinstance(obj, str):
        if obj in mapping:
            return mapping[obj]
        marker="--acme-http01-solver-image="
        if obj.startswith(marker):
            image=obj[len(marker):]
            if image in mapping:
                return marker + mapping[image]
        return obj
    return obj

leader_changes=0
leader_rbac_changes=0
deployment_patch_count=0
resource_patch_count=0
for path in source_root.iterdir():
    if path.suffix.lower() not in {".yaml", ".yml"}:
        continue
    docs=[]
    for doc in yaml.safe_load_all(path.read_text(encoding="utf-8")):
        if doc is None:
            continue
        doc=rewrite(doc)
        if (
            path.name=="cert-manager.yaml"
            and isinstance(doc, dict)
            and doc.get("kind") in {"Role", "RoleBinding"}
            and doc.get("metadata",{}).get("name") in {
                "cert-manager:leaderelection",
                "cert-manager-cainjector:leaderelection",
            }
        ):
            metadata=doc.setdefault("metadata", {})
            if metadata.get("namespace")=="kube-system":
                metadata["namespace"]="cert-manager"
                leader_rbac_changes += 1
        if isinstance(doc, dict) and doc.get("kind")=="Deployment":
            deployment_patch_count += 1
            spec=doc.setdefault("spec", {})
            spec["progressDeadlineSeconds"]=600
            spec["strategy"]={"type":"Recreate"}
            template=spec.setdefault("template", {})
            annotations=template.setdefault("metadata", {}).setdefault("annotations", {})
            annotations["natureprotector.io/gke-autopilot-patched"]="phase3-clean-amd64"
            pod_spec=template.setdefault("spec", {})
            node_selector=pod_spec.setdefault("nodeSelector", {})
            node_selector["kubernetes.io/os"]="linux"
            node_selector["kubernetes.io/arch"]="amd64"
            for container in pod_spec.get("containers", []):
                resources=container.setdefault("resources", {})
                resources["requests"]={
                    "cpu":"100m",
                    "memory":"128Mi",
                    "ephemeral-storage":"1Gi",
                }
                resources["limits"]={"ephemeral-storage":"1Gi"}
                resource_patch_count += 1
                if path.name=="cert-manager.yaml":
                    args=container.get("args", [])
                    replaced=[]
                    for arg in args:
                        if arg=="--leader-election-namespace=kube-system":
                            replaced.append("--leader-election-namespace=cert-manager")
                            leader_changes += 1
                        else:
                            replaced.append(arg)
                    container["args"]=replaced
        docs.append(doc)

    (output_root/path.name).write_text(
        yaml.safe_dump_all(docs, sort_keys=False),
        encoding="utf-8",
        newline="\n",
    )

if leader_changes != 2:
    raise SystemExit(
        f"Expected two cert-manager leader-election argument replacements, got {leader_changes}"
    )
if leader_rbac_changes != 4:
    raise SystemExit(
        f"Expected four cert-manager leader-election RBAC namespace replacements, got {leader_rbac_changes}"
    )
if deployment_patch_count != 8:
    raise SystemExit(f"Expected eight operator Deployments, got {deployment_patch_count}")
if resource_patch_count != 8:
    raise SystemExit(f"Expected eight operator containers, got {resource_patch_count}")
print("CERT_MANAGER_AUTOPILOT_PATCH_CONFIRMED")
print("CERT_MANAGER_LEADER_ELECTION_RBAC_CONFIRMED")
print("OPERATOR_RECREATE_STRATEGY_CONFIRMED")
print("OPERATOR_EXPLICIT_RESOURCE_REQUESTS_CONFIRMED")
print("OPERATOR_AMD64_NODE_SELECTION_CONFIRMED")
PY

py -3.12 - \
  "$EVIDENCE_DIR/patched" \
  "$EVIDENCE_DIR/mirror/image-map-digest.json" \
  "$ARTIFACT_HOST" \
  "$PROJECT_ID" \
  "$ARTIFACT_REPOSITORY" \
  "$EVIDENCE_DIR/mirror/patched-image-audit.json" <<'PY'
from pathlib import Path
import json, re, sys, yaml
root=Path(sys.argv[1])
mapping=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
prefix=f"{sys.argv[3]}/{sys.argv[4]}/{sys.argv[5]}/"
refs=set()
def collect(obj):
    if isinstance(obj, dict):
        for key, value in obj.items():
            if key=="image" and isinstance(value, str):
                refs.add(value)
            collect(value)
    elif isinstance(obj, list):
        for item in obj:
            collect(item)
    elif isinstance(obj, str):
        marker="--acme-http01-solver-image="
        if obj.startswith(marker):
            refs.add(obj[len(marker):])
for path in root.iterdir():
    if path.suffix.lower() not in {".yaml", ".yml"}:
        continue
    for doc in yaml.safe_load_all(path.read_text(encoding="utf-8")):
        collect(doc)
expected=set(mapping.values())
invalid=[ref for ref in sorted(refs) if not ref.startswith(prefix) or not re.search(r"@sha256:[0-9a-f]{64}$", ref)]
missing=sorted(expected-refs)
unexpected=sorted(refs-expected)
result={
    "status":"PASS" if not (invalid or missing or unexpected) else "FAIL",
    "image_count":len(refs),
    "invalid":invalid,
    "missing":missing,
    "unexpected":unexpected,
}
Path(sys.argv[6]).write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if result["status"] != "PASS":
    raise SystemExit("Patched operator manifests are not fully digest-pinned")
PY

wait_deployment_ready() {
  local namespace="$1"
  local name="$2"
  local timeout_seconds="${3:-900}"
  local deadline=$(( $(date +%s) + timeout_seconds ))
  local deployment_snapshot="$EVIDENCE_DIR/diagnostics/wait-${namespace}-${name}.json"
  local pods_snapshot="$EVIDENCE_DIR/diagnostics/wait-${namespace}-${name}-pods.json"
  local fatal_streak=0

  while (( $(date +%s) < deadline )); do
    echo "CLUSTER_DEPENDENCY=${namespace}/${name}"
    echo "CLUSTER_DEPENDENCY_STATUS=WAITING"
    if kubectl -n "$namespace" get deployment "$name" -o json > "$deployment_snapshot" 2>/dev/null; then
      kubectl -n "$namespace" get pods -o json > "$pods_snapshot" 2>/dev/null || printf '{"items":[]}\n' > "$pods_snapshot"
      probe_rc=0
      py -3.12 - "$deployment_snapshot" "$pods_snapshot" <<'PY' || probe_rc=$?
from pathlib import Path
import json, sys

d=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
pods=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
spec=d.get("spec", {})
status=d.get("status", {})
desired=spec.get("replicas", 1)
ready=status.get("readyReplicas", 0)
available=status.get("availableReplicas", 0)
updated=status.get("updatedReplicas", 0)
observed=status.get("observedGeneration", 0)
generation=d.get("metadata", {}).get("generation", 0)
selector=spec.get("selector", {}).get("matchLabels", {})
matching=[]
for pod in pods.get("items", []):
    labels=pod.get("metadata", {}).get("labels", {})
    if all(labels.get(k)==v for k,v in selector.items()):
        matching.append(pod)

fatal=[]
summary=[]
failure_classes=[]
fatal_wait={
    "ImagePullBackOff", "ErrImagePull", "InvalidImageName",
    "CreateContainerConfigError", "CreateContainerError", "RunContainerError",
}
for pod in matching:
    pod_name=pod.get("metadata", {}).get("name", "")
    for condition in pod.get("status", {}).get("conditions", []):
        if condition.get("type")=="PodScheduled" and condition.get("status")=="False":
            reason=condition.get("reason", "")
            message=condition.get("message", "")
            summary.append(f"{pod_name}:unscheduled:{reason}:{message}")
    for cs in pod.get("status", {}).get("containerStatuses", []):
        name=cs.get("name", "")
        restarts=int(cs.get("restartCount", 0))
        state=cs.get("state", {})
        waiting=state.get("waiting", {})
        terminated=state.get("terminated", {})
        reason=waiting.get("reason") or terminated.get("reason") or ""
        image=cs.get("image", "")
        image_id=cs.get("imageID", "")
        summary.append(f"{pod_name}/{name}:reason={reason}:restarts={restarts}:image={image}:imageID={image_id}")
        if waiting.get("reason") in fatal_wait:
            fatal.append(f"{pod_name}/{name}:{waiting.get('reason')}")
            if waiting.get("reason") in {"ImagePullBackOff", "ErrImagePull", "InvalidImageName"}:
                failure_classes.append("IMAGE_PULL")
            else:
                failure_classes.append("RESOURCE_REQUEST_OR_ADMISSION")
        if waiting.get("reason")=="CrashLoopBackOff" and restarts >= 2:
            fatal.append(f"{pod_name}/{name}:CrashLoopBackOff:{restarts}")
            failure_classes.append("CONTAINER_CRASH")
        if terminated.get("reason") in {"Error", "OOMKilled", "ContainerCannotRun"} and restarts >= 2:
            fatal.append(f"{pod_name}/{name}:{terminated.get('reason')}:{restarts}")
            if terminated.get("reason")=="OOMKilled":
                failure_classes.append("RESOURCE_REQUEST_OR_ADMISSION")
            else:
                failure_classes.append("CONTAINER_CRASH")

ok=(
    desired >= 1
    and ready >= desired
    and available >= desired
    and updated >= desired
    and observed >= generation
)
print(
    f"DEPLOYMENT_STATUS={d.get('metadata',{}).get('namespace')}/"
    f"{d.get('metadata',{}).get('name')} desired={desired} ready={ready} "
    f"available={available} updated={updated} observed={observed} generation={generation}"
)
for line in summary:
    print("POD_STATUS="+line)
if ok:
    raise SystemExit(0)
if fatal:
    print("FATAL_POD_STATES="+",".join(fatal))
    if failure_classes:
        print("CLUSTER_DEPENDENCY_FAILURE_CLASS="+failure_classes[0])
    raise SystemExit(42)
raise SystemExit(1)
PY
      if (( probe_rc == 0 )); then
        echo "DEPLOYMENT_READY=${namespace}/${name}"
        return 0
      elif (( probe_rc == 42 )); then
        fatal_streak=$((fatal_streak + 1))
      else
        fatal_streak=0
      fi
      if (( fatal_streak >= 3 )); then
        capture_namespace "$namespace" "fatal-${name}"
        echo "CLUSTER_DEPENDENCY=${namespace}/${name}"
        echo "CLUSTER_DEPENDENCY_STATUS=FAILED"
        echo "CLUSTER_DEPENDENCY_DIAGNOSTICS_BEGIN"
        kubectl -n "$namespace" get deployments,replicasets,pods,services,endpoints,events -o wide || true
        kubectl -n "$namespace" describe pods || true
        echo "CLUSTER_DEPENDENCY_DIAGNOSTICS_END"
        echo "ERROR: deployment ${namespace}/${name} entered a persistent fatal Pod state." >&2
        return 1
      fi
    fi
    kubectl -n "$namespace" get pods -o wide || true
    sleep 10
  done

  capture_namespace "$namespace" "timeout-${name}"
  echo "CLUSTER_DEPENDENCY=${namespace}/${name}"
  echo "CLUSTER_DEPENDENCY_STATUS=FAILED"
  echo "CLUSTER_DEPENDENCY_FAILURE_CLASS=UNKNOWN"
  echo "CLUSTER_DEPENDENCY_DIAGNOSTICS_BEGIN"
  kubectl -n "$namespace" get deployments,replicasets,pods,services,endpoints,events -o wide || true
  kubectl -n "$namespace" describe pods || true
  echo "CLUSTER_DEPENDENCY_DIAGNOSTICS_END"
  echo "ERROR: deployment ${namespace}/${name} did not become ready." >&2
  return 1
}

wait_crd_established() {
  local crd="$1"
  kubectl wait \
    --for=condition=Established \
    "crd/${crd}" \
    --timeout=10m
}

verify_cert_manager_webhook() {
  local probe="$EVIDENCE_DIR/patched/cert-manager-webhook-probe.yaml"
  cat > "$probe" <<'YAML'
apiVersion: cert-manager.io/v1
kind: Issuer
metadata:
  name: natureprotector-webhook-probe
  namespace: cert-manager
spec:
  selfSigned: {}
YAML

  kubectl apply \
    --dry-run=server \
    -f "$probe" \
    > "$EVIDENCE_DIR/diagnostics/cert-manager-webhook-probe.txt"

  kubectl api-resources --api-group=cert-manager.io \
    > "$EVIDENCE_DIR/diagnostics/cert-manager-api-resources.txt"
}

verify_keda_api() {
  local deadline=$(( $(date +%s) + 1200 ))
  local snapshot="$EVIDENCE_DIR/diagnostics/keda-external-metrics-apiservice.json"

  while (( $(date +%s) < deadline )); do
    if kubectl get apiservice v1beta1.external.metrics.k8s.io -o json \
      > "$snapshot" 2>/dev/null; then
      if py -3.12 - "$snapshot" <<'PY'
from pathlib import Path
import json, sys
data=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
for condition in data.get("status", {}).get("conditions", []):
    if condition.get("type")=="Available" and str(condition.get("status")).lower()=="true":
        raise SystemExit(0)
raise SystemExit(1)
PY
      then
        echo "KEDA_EXTERNAL_METRICS_API_READY"
        return 0
      fi
    fi
    sleep 15
  done

  capture_namespace keda keda-api-timeout
  return 1
}

clean_existing_operator_workloads() {
  echo "OPERATOR_CLEAN_REINSTALL_STARTED"
  capture_all_operator_diagnostics "pre-clean-reinstall" || true

  kubectl -n cert-manager delete deployment \
    cert-manager cert-manager-cainjector cert-manager-webhook \
    --ignore-not-found=true --wait=true --timeout=5m || true
  kubectl -n rabbitmq-system delete deployment \
    rabbitmq-cluster-operator messaging-topology-operator \
    --ignore-not-found=true --wait=true --timeout=5m || true
  kubectl -n keda delete deployment \
    keda-admission keda-metrics-apiserver keda-operator \
    --ignore-not-found=true --wait=true --timeout=5m || true

  for namespace in cert-manager rabbitmq-system keda; do
    if kubectl get namespace "$namespace" >/dev/null 2>&1; then
      kubectl -n "$namespace" delete replicaset --all \
        --ignore-not-found=true --wait=true --timeout=5m || true
      kubectl -n "$namespace" delete pod --all \
        --ignore-not-found=true --wait=true --timeout=5m || true
    fi
  done

  capture_all_operator_diagnostics "post-clean-reinstall" || true
  echo "OPERATOR_CLEAN_REINSTALL_READY"
}

apply_with_retry() {
  local manifest="$1"
  local attempts="${2:-5}"
  local delay="${3:-20}"

  for attempt in $(seq 1 "$attempts"); do
    if kubectl apply \
      --server-side \
      --force-conflicts \
      --field-manager="$FIELD_MANAGER" \
      -f "$manifest"; then
      return 0
    fi
    echo "APPLY_RETRY=${attempt}/${attempts} manifest=$manifest"
    sleep "$delay"
  done
  return 1
}

clean_existing_operator_workloads

while IFS=$'\t' read -r name repository tag asset namespace rollouts; do
  [[ -n "$name" ]] || continue
  rollouts="${rollouts//$'\r'/}"
  manifest="$EVIDENCE_DIR/patched/$asset"

  if [[ "$name" == "messaging-topology-operator" ]]; then
    verify_cert_manager_webhook
  fi

  apply_with_retry "$manifest" 5 20

  if [[ "$name" == "cert-manager" ]]; then
    wait_crd_established certificates.cert-manager.io
    wait_crd_established issuers.cert-manager.io
    wait_crd_established clusterissuers.cert-manager.io

    wait_deployment_ready cert-manager cert-manager "$ROLLOUT_TIMEOUT_SECONDS"
    wait_deployment_ready cert-manager cert-manager-cainjector "$ROLLOUT_TIMEOUT_SECONDS"
    wait_deployment_ready cert-manager cert-manager-webhook "$ROLLOUT_TIMEOUT_SECONDS"

    kubectl -n cert-manager get deployment cert-manager cert-manager-cainjector -o json \
      > "$EVIDENCE_DIR/diagnostics/cert-manager-deployments-final.json"

    py -3.12 - "$EVIDENCE_DIR/diagnostics/cert-manager-deployments-final.json" <<'PY'
from pathlib import Path
import json, sys
data=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
bad=[]
for item in data.get("items", []):
    for container in item.get("spec",{}).get("template",{}).get("spec",{}).get("containers",[]):
        for arg in container.get("args", []):
            if arg=="--leader-election-namespace=kube-system":
                bad.append(f"{item.get('metadata',{}).get('name')}:{container.get('name')}")
if bad:
    raise SystemExit("Forbidden kube-system leader election remains: " + ",".join(bad))
print("CERT_MANAGER_AUTOPILOT_LEADER_ELECTION_CONFIRMED")
PY
    verify_cert_manager_webhook
  else
    IFS=',' read -ra rollout_items <<< "$rollouts"
    for rollout in "${rollout_items[@]}"; do
      [[ -n "$rollout" ]] || continue
      kind="${rollout%%/*}"
      deployment_name="${rollout#*/}"
      [[ "$kind" == "deployment" ]] || {
        echo "ERROR: unsupported rollout kind: $rollout" >&2
        exit 4
      }
      wait_deployment_ready "$namespace" "$deployment_name" "$ROLLOUT_TIMEOUT_SECONDS"
    done
  fi

  if [[ "$name" == "messaging-topology-operator" ]]; then
    local_deadline=$(( $(date +%s) + 600 ))
    while (( $(date +%s) < local_deadline )); do
      if kubectl -n rabbitmq-system get secret webhook-server-cert \
        -o json > "$EVIDENCE_DIR/diagnostics/topology-webhook-secret.json" 2>/dev/null; then
        break
      fi
      sleep 10
    done
    [[ -f "$EVIDENCE_DIR/diagnostics/topology-webhook-secret.json" ]]
  fi

  if [[ "$name" == "keda" ]]; then
    verify_keda_api
  fi

  capture_namespace "$namespace" "ready-${name}"
  echo "DEPENDENCY_READY=$name"
done < "$EVIDENCE_DIR/dependencies.tsv"

py -3.12 - \
  "$LOCK_PATH" \
  "$EVIDENCE_DIR/release-metadata" \
  "$EVIDENCE_DIR/mirror/image-map.json" \
  "$EVIDENCE_DIR/cluster-dependencies.json" <<'PY'
from pathlib import Path
import hashlib, json, sys
lock_path=Path(sys.argv[1])
metadata_root=Path(sys.argv[2])
mapping=json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
records=[]
for path in sorted(metadata_root.glob("*-resolved.json")):
    records.append(json.loads(path.read_text(encoding="utf-8-sig")))
summary={
    "schema_version": 2,
    "project_id": "natureprotector-500518",
    "region": "europe-southwest1",
    "cluster_name": "np-staging",
    "lock_sha256": hashlib.sha256(lock_path.read_bytes()).hexdigest(),
    "dependencies": records,
    "operator_image_mirrors": mapping,
    "cert_manager_autopilot_leader_election_namespace": "cert-manager",
    "external_registry_runtime_dependency": False,
    "status": "passed",
}
Path(sys.argv[4]).write_text(
    json.dumps(summary, indent=2)+"\n",
    encoding="utf-8",
    newline="\n",
)
PY

capture_all_operator_diagnostics final
trap - ERR

echo "OPERATOR_FOUNDATION_PROVED"
echo "CLUSTER_DEPENDENCY=cert-manager"
echo "CLUSTER_DEPENDENCY_STATUS=READY"
trap - ERR INT TERM
