#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C
export CLOUDSDK_CORE_DISABLE_PROMPTS=1
export CLOUDSDK_CORE_PROJECT="natureprotector-500518"

PROJECT_ID="natureprotector-500518"
PROJECT_NUMBER="22505444922"
REGION="europe-southwest1"
STATE_BUCKET="np-tfstate-migkxl-202606"
RELEASE_ARTIFACT="standard-cd-release"
RELEASE_WORKFLOW_FILE="${NP_G81_RELEASE_WORKFLOW_FILE:-gcp-g8-1-release.yml}"
GITHUB_REPOSITORY="${NP_GITHUB_REPOSITORY:-MAlves4018/NatureProtector}"
OWNER_CONFIRMATION="AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H"
AUTHORIZATION="RETIFICAR_CERT_MANAGER_COM_MIRROR_AMD64_LIMPO_E_CONCLUIR_STAGING"

PACKAGE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ARG="${1:-$PWD}"
REPO_TOP="$(cd "$REPO_ARG" && git rev-parse --show-toplevel)"
cd "$REPO_TOP"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
RESULT_PARENT="$(cd "$REPO_TOP/.." && pwd)/NatureProtector-Standard-CD-Result-local/staging-autopilot-final"
RESULT_DIR="$RESULT_PARENT/autopilot-final-$STAMP"
mkdir -p "$RESULT_DIR"/{artifact,audit,backend,deploy/bootstrap,deploy/verified,dns,gcp,git,kubernetes,logs,terraform/platform,terraform/environment}
CHECKPOINT_ROOT="$(cd "$REPO_TOP/.." && pwd)/NatureProtector-Standard-CD-Result-local/staging-resume-checkpoints"
mkdir -p "$CHECKPOINT_ROOT" "$RESULT_DIR/checkpoints"
LAST_CHECKPOINT="00_START"

CLOUD_MUTATION=false
TERRAFORM_APPLY_EXECUTED=false
TERRAFORM_DESTROY_EXECUTED=false
FOUNDATION_PROVED=false
SERVICES_BOOTSTRAPPED=false
EDGE_HTTPS_ACTIVE=false
STAGING_VERIFIED=false
STAGING_URL=""
PACKAGE_CREATED=false

exec > >(tee "$RESULT_DIR/logs/phase3-runner.log") 2>&1

mark_checkpoint() {
  local name="$1"
  LAST_CHECKPOINT="$name"
  printf '%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$CHECKPOINT_ROOT/$name"
  cp "$CHECKPOINT_ROOT/$name" "$RESULT_DIR/checkpoints/$name"
  echo "CHECKPOINT=$name"
}

package_result() {
  local status="${1:-PHASE3_FAILED}"
  if [[ "$PACKAGE_CREATED" == true ]]; then return 0; fi
  PACKAGE_CREATED=true

  cat > "$RESULT_DIR/result.env" <<EOF
RESULT=$status
LAST_CHECKPOINT=$LAST_CHECKPOINT
HEAD_SHA=$(git rev-parse HEAD 2>/dev/null || true)
ACTIVE_PROJECT=$(gcloud config get-value project 2>/dev/null | tr -d '\r' || true)
FOUNDATION_PROVED=$FOUNDATION_PROVED
SERVICES_BOOTSTRAPPED=$SERVICES_BOOTSTRAPPED
EDGE_HTTPS_ACTIVE=$EDGE_HTTPS_ACTIVE
STAGING_VERIFIED=$STAGING_VERIFIED
STAGING_URL=$STAGING_URL
PRODUCTION_AUTHORIZED=false
PRODUCTION_DEPLOYED=false
CLOUD_MUTATION=$CLOUD_MUTATION
TERRAFORM_APPLY_EXECUTED=$TERRAFORM_APPLY_EXECUTED
TERRAFORM_DESTROY_EXECUTED=$TERRAFORM_DESTROY_EXECUTED
GIT_COMMIT_EXECUTED=false
GIT_PUSH_EXECUTED=false
WORKFLOW_EXECUTED=false
EOF

  local zip_path="$RESULT_DIR-review.zip"
  py -3.12 - "$RESULT_DIR" "$zip_path" <<'PY'
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
import hashlib
import sys

root = Path(sys.argv[1])
out = Path(sys.argv[2])

with ZipFile(out, "w", compression=ZIP_DEFLATED) as archive:
    for path in sorted(root.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(root))

digest = hashlib.sha256(out.read_bytes()).hexdigest()
out.with_suffix(out.suffix + ".sha256").write_text(
    f"{digest} *{out.name}\n",
    encoding="utf-8",
)
print(f"REVIEW_ZIP={out}")
print(f"REVIEW_ZIP_SHA256={digest}")
PY
}

collect_failure_state() {
  set +e
  git status --short --branch > "$RESULT_DIR/git/status-at-end.txt" 2>&1

  gcloud deploy delivery-pipelines list \
    --project="$PROJECT_ID" --region="$REGION" --format=json \
    > "$RESULT_DIR/gcp/delivery-pipelines-at-end.json" 2>&1

  gcloud deploy targets list \
    --project="$PROJECT_ID" --region="$REGION" --format=json \
    > "$RESULT_DIR/gcp/delivery-targets-at-end.json" 2>&1

  gcloud run services list \
    --project="$PROJECT_ID" --region="$REGION" --format=json \
    > "$RESULT_DIR/gcp/run-services-at-end.json" 2>&1

  gcloud run jobs list \
    --project="$PROJECT_ID" --region="$REGION" --format=json \
    > "$RESULT_DIR/gcp/run-jobs-at-end.json" 2>&1

  gcloud compute addresses describe np-staging-https \
    --project="$PROJECT_ID" --global --format=json \
    > "$RESULT_DIR/gcp/edge-address-at-end.json" 2>&1

  gcloud compute ssl-certificates describe np-staging \
    --project="$PROJECT_ID" --global --format=json \
    > "$RESULT_DIR/gcp/edge-certificate-at-end.json" 2>&1

  if command -v kubectl >/dev/null 2>&1 && \
     { command -v gke-gcloud-auth-plugin >/dev/null 2>&1 || \
       command -v gke-gcloud-auth-plugin.exe >/dev/null 2>&1; }; then
    for namespace in natureprotector-staging cert-manager rabbitmq-system keda; do
      kubectl -n "$namespace" get deployments,statefulsets,replicasets,pods,services,endpoints,events,leases -o wide \
        > "$RESULT_DIR/kubernetes/${namespace}-at-end.txt" 2>&1 || true
      kubectl -n "$namespace" get deployments,statefulsets,replicasets,pods,services,endpoints,events,leases -o json \
        > "$RESULT_DIR/kubernetes/${namespace}-at-end.json" 2>&1 || true
    done
  fi

  for job in np-postgres-migrations np-postgres-bootstrap natureprotector-simulator np-functional-smoke; do
    gcloud run jobs executions list \
      --job="$job" --project="$PROJECT_ID" --region="$REGION" \
      --limit=5 --format=json \
      > "$RESULT_DIR/gcp/${job}-executions-at-end.json" 2>&1 || true
  done
  set -e
}

on_error() {
  local rc=$?
  local line="${BASH_LINENO[0]:-unknown}"
  trap - ERR INT TERM
  echo "ERROR: Autopilot-aware staging finalization failed at line $line with exit code $rc." >&2
  collect_failure_state
  package_result "CLEAN_AMD64_OPERATOR_REMIRROR_AND_STAGING_COMPLETION_FAILED" || true
  exit "$rc"
}
trap on_error ERR

on_signal() {
  local signal="$1"
  trap - ERR INT TERM
  echo "ERROR: staging finalization interrupted by ${signal}." >&2
  collect_failure_state || true
  package_result "CLEAN_AMD64_OPERATOR_REMIRROR_AND_STAGING_INTERRUPTED" || true
  exit 130
}
trap 'on_signal INT' INT
trap 'on_signal TERM' TERM

for tool in git gcloud terraform gh pwsh py sha256sum cygpath kubectl; do
  command -v "$tool" >/dev/null 2>&1 || {
    echo "ERROR: required tool unavailable: $tool" >&2
    exit 2
  }
done

ensure_gke_auth_plugin() {
  local plugin=""
  local sdk_root=""
  local sdk_root_posix=""

  plugin="$(command -v gke-gcloud-auth-plugin 2>/dev/null || true)"
  if [[ -z "$plugin" ]]; then
    plugin="$(command -v gke-gcloud-auth-plugin.exe 2>/dev/null || true)"
  fi

  if [[ -z "$plugin" ]]; then
    echo "GKE_AUTH_PLUGIN_STATUS=INSTALLING"
    gcloud components install gke-gcloud-auth-plugin --quiet
  fi

  plugin="$(command -v gke-gcloud-auth-plugin 2>/dev/null || true)"
  if [[ -z "$plugin" ]]; then
    plugin="$(command -v gke-gcloud-auth-plugin.exe 2>/dev/null || true)"
  fi

  sdk_root="$(gcloud info --format='value(installation.sdk_root)' 2>/dev/null | tr -d '\r\n')"
  if [[ -n "$sdk_root" ]]; then
    sdk_root_posix="$(cygpath -u "$sdk_root" 2>/dev/null || printf '%s' "$sdk_root")"
  fi

  if [[ -z "$plugin" && -n "$sdk_root_posix" && -d "$sdk_root_posix" ]]; then
    plugin="$(
      find "$sdk_root_posix" -type f \
        \( -iname 'gke-gcloud-auth-plugin.exe' -o -iname 'gke-gcloud-auth-plugin' \) \
        -print -quit
    )"
  fi

  if [[ -z "$plugin" || ! -f "$plugin" ]]; then
    echo "ERROR: gke-gcloud-auth-plugin was not found after installation." >&2
    echo "GCLOUD_SDK_ROOT=$sdk_root" >&2
    exit 2
  fi

  export PATH="$(dirname "$plugin"):$PATH"
  export USE_GKE_GCLOUD_AUTH_PLUGIN=True
  export GKE_GCLOUD_AUTH_PLUGIN_PATH="$plugin"

  "$plugin" --version | tee "$RESULT_DIR/logs/gke-gcloud-auth-plugin-version.txt"

  echo "GKE_AUTH_PLUGIN_STATUS=READY"
  echo "GKE_AUTH_PLUGIN_PATH=$plugin"
}

ensure_gke_auth_plugin

if [[ "${NP_CONFIRM_STAGING_RESUME:-}" != "$AUTHORIZATION" ]]; then
  echo "ERROR: exact NP_CONFIRM_STAGING_RESUME authorization is required." >&2
  exit 2
fi

BRANCH="$(git branch --show-current)"
HEAD_SHA="$(git rev-parse HEAD)"
EXPECTED_HEAD="${NP_EXPECTED_HEAD:-$HEAD_SHA}"
REMOTE_SHA="$(git ls-remote origin refs/heads/master | awk '{print $1}')"
ACTIVE_PROJECT="$(gcloud config get-value project 2>/dev/null | tr -d '\r')"
ACTIVE_ACCOUNT="$(gcloud auth list --filter=status:ACTIVE --format='value(account)' | head -n1 | tr -d '\r')"
LIVE_PROJECT_NUMBER="$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)' | tr -d '\r')"

{
  echo "REPO_TOP=$REPO_TOP"
  echo "BRANCH=$BRANCH"
  echo "HEAD_SHA=$HEAD_SHA"
  echo "EXPECTED_HEAD=$EXPECTED_HEAD"
  echo "REMOTE_SHA=$REMOTE_SHA"
  echo "RELEASE_WORKFLOW_FILE=$RELEASE_WORKFLOW_FILE"
  echo "GITHUB_REPOSITORY=$GITHUB_REPOSITORY"
  echo "ACTIVE_PROJECT=$ACTIVE_PROJECT"
  echo "ACTIVE_ACCOUNT=$ACTIVE_ACCOUNT"
  echo "PROJECT_NUMBER=$LIVE_PROJECT_NUMBER"
} | tee "$RESULT_DIR/git/preflight.txt"

[[ "$BRANCH" == "master" ]]
if [[ "$EXPECTED_HEAD" != "$HEAD_SHA" ]]; then
  echo "ERROR: NP_EXPECTED_HEAD does not match the current HEAD." >&2
  echo "EXPECTED_HEAD=$EXPECTED_HEAD" >&2
  echo "HEAD_SHA=$HEAD_SHA" >&2
  exit 2
fi
[[ "$REMOTE_SHA" == "$HEAD_SHA" ]]
[[ "$ACTIVE_PROJECT" == "$PROJECT_ID" ]]
[[ -n "$ACTIVE_ACCOUNT" ]]
[[ "$LIVE_PROJECT_NUMBER" == "$PROJECT_NUMBER" ]]

git status --porcelain > "$RESULT_DIR/git/status-porcelain-before.txt"
if [[ -s "$RESULT_DIR/git/status-porcelain-before.txt" ]]; then
  py -3.12 - "$RESULT_DIR/git/status-porcelain-before.txt" <<'PY'
from pathlib import Path
import sys

allowed = {
    "infra/gcp/kubernetes/g8-1/operator-lock.json",
    "infra/gcp/terraform/g8-1-platform/cloud_deploy.tf",
    "infra/gcp/terraform/g8-1-platform/evidence.tf",
    "infra/gcp/terraform/g8-1-platform/terraform.staging.tfvars",
    "infra/gcp/terraform/g8-1-platform/terraform.tfvars.example",
    "infra/gcp/terraform/g8-1-platform/variables.tf",
    "scripts/cloud/Deploy-G81Staging-Autopilot.ps1",
    "scripts/cloud/Test-EnvironmentRemediationStatic.py",
    "scripts/cloud/Test-StandardPlatformConfiguration.py",
    "scripts/cloud/complete-staging-after-autopilot-remediation.sh",
    "scripts/cloud/install-g81-cluster-dependencies-autopilot.sh",
    "scripts/np.ps1",
}

unexpected = []
for line in Path(sys.argv[1]).read_text(encoding="utf-8-sig").splitlines():
    if not line.strip():
        continue
    path = line[3:]
    if " -> " in path:
        path = path.split(" -> ", 1)[1]
    path = path.replace("\\", "/")
    if path not in allowed:
        unexpected.append(path)

if unexpected:
    raise SystemExit(
        "Unexpected local repository changes before staging deployment: "
        + ", ".join(sorted(unexpected))
    )

print("GIT_DIRTY_CANONICAL_CHANGES_ACCEPTED")
PY
fi

git status --short --branch > "$RESULT_DIR/git/status-before.txt"
git log -1 --format=fuller > "$RESULT_DIR/git/head-before.txt"

PYTHON_EXE_WIN="$(py -3.12 -c 'import sys; print(sys.executable)' | tr -d '\r\n')"
PYTHON_DIR_POSIX="$(dirname "$(cygpath -u "$PYTHON_EXE_WIN")")"
export PATH="$PYTHON_DIR_POSIX:$PATH"
export USE_GKE_GCLOUD_AUTH_PLUGIN=True
py -3.12 --version | tee "$RESULT_DIR/logs/python-version.txt"

pwsh ./scripts/cloud/New-CanonicalTerraformBackendFiles.ps1 \
  -Environment staging \
  -StateBucket "$STATE_BUCKET" \
  -OutputDirectory "$(cygpath -w "$RESULT_DIR/backend")" \
  > "$RESULT_DIR/backend/backend-generation.json"

PLATFORM_WS="$RESULT_DIR/terraform/platform/workspace"
ENVIRONMENT_WS="$RESULT_DIR/terraform/environment/workspace"
mkdir -p "$PLATFORM_WS" "$ENVIRONMENT_WS"

cp -R infra/gcp/terraform/g8-1-platform/. "$PLATFORM_WS/"
cp -R infra/gcp/terraform/g8-1-environment/. "$ENVIRONMENT_WS/"
rm -rf "$PLATFORM_WS/.terraform" "$ENVIRONMENT_WS/.terraform"

PLATFORM_WS_MIXED="$(cygpath -m "$PLATFORM_WS")"
ENVIRONMENT_WS_MIXED="$(cygpath -m "$ENVIRONMENT_WS")"
PLATFORM_BACKEND_MIXED="$(cygpath -m "$RESULT_DIR/backend/platform.gcs.tfbackend")"
ENVIRONMENT_BACKEND_MIXED="$(cygpath -m "$RESULT_DIR/backend/staging.gcs.tfbackend")"

terraform -chdir="$PLATFORM_WS_MIXED" init \
  -input=false -reconfigure \
  -backend-config="$PLATFORM_BACKEND_MIXED" \
  > "$RESULT_DIR/logs/platform-init.log" 2>&1

terraform -chdir="$PLATFORM_WS_MIXED" validate \
  > "$RESULT_DIR/logs/platform-validate.log" 2>&1

terraform -chdir="$PLATFORM_WS_MIXED" state list \
  > "$RESULT_DIR/terraform/platform/state.txt"

gcloud deploy delivery-pipelines list \
  --project="$PROJECT_ID" --region="$REGION" --format=json \
  > "$RESULT_DIR/gcp/delivery-pipelines.json"

gcloud deploy targets list \
  --project="$PROJECT_ID" --region="$REGION" --format=json \
  > "$RESULT_DIR/gcp/delivery-targets.json"

py -3.12 - \
  "$RESULT_DIR/terraform/platform/state.txt" \
  "$RESULT_DIR/gcp/delivery-pipelines.json" \
  "$RESULT_DIR/gcp/delivery-targets.json" \
  "$RESULT_DIR/audit/foundation.json" <<'PY'
from pathlib import Path
import json
import sys

state = [
    item.strip()
    for item in Path(sys.argv[1]).read_text().splitlines()
    if item.strip()
]
pipelines_raw = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
targets_raw = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
output = Path(sys.argv[4])

def unwrap(value, key):
    if isinstance(value, list):
        return value
    if isinstance(value, dict):
        candidate = value.get(key, [])
        if isinstance(candidate, list):
            return candidate
    raise TypeError(f"Unsupported gcloud JSON shape for {key}: {type(value)}")

pipelines = unwrap(pipelines_raw, "deliveryPipelines")
targets = unwrap(targets_raw, "targets")

managed = [item for item in state if not item.startswith("data.")]
data = [item for item in state if item.startswith("data.")]

pipeline_names = sorted(
    item["name"].rsplit("/", 1)[-1]
    for item in pipelines
)
target_names = sorted(
    item["name"].rsplit("/", 1)[-1]
    for item in targets
)

expected_pipelines = sorted([
    "natureprotector-api",
    "natureprotector-frontend",
    "natureprotector-prevention",
])
expected_targets = sorted([
    "np-gke-staging",
    "np-run-staging",
])

errors = []
if len(managed) != 53:
    errors.append(f"managed resources: expected 53, got {len(managed)}")
if len(data) != 3:
    errors.append(f"data resources: expected 3, got {len(data)}")
if pipeline_names != expected_pipelines:
    errors.append(f"pipelines: {pipeline_names}")
if target_names != expected_targets:
    errors.append(f"targets: {target_names}")

expected_pool = (
    "projects/natureprotector-500518/locations/"
    "europe-southwest1/workerPools/np-staging-deploy"
)
expected_sa = (
    "np-deploy-staging@natureprotector-500518."
    "iam.gserviceaccount.com"
)
for item in targets:
    target_id = item.get("targetId") or item.get("name", "").rsplit("/", 1)[-1]
    configs = item.get("executionConfigs", [])
    if len(configs) != 1:
        errors.append(f"{target_id}: executionConfigs={len(configs)}")
        continue
    config = configs[0]
    private = config.get("privatePool") or {}
    if config.get("workerPool") != expected_pool:
        errors.append(f"{target_id}: top-level workerPool mismatch")
    if private.get("workerPool") != expected_pool:
        errors.append(f"{target_id}: privatePool.workerPool mismatch")
    if config.get("serviceAccount") != expected_sa:
        errors.append(f"{target_id}: serviceAccount mismatch")
    if private.get("serviceAccount") != expected_sa:
        errors.append(f"{target_id}: privatePool.serviceAccount mismatch")
    if sorted(config.get("usages", [])) != ["DEPLOY", "RENDER", "VERIFY"]:
        errors.append(f"{target_id}: usages mismatch")

result = {
    "status": "PASS" if not errors else "FAIL",
    "managed_resources": len(managed),
    "data_resources": len(data),
    "pipelines": pipeline_names,
    "targets": target_names,
    "errors": errors,
}
output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Foundation verification failed")
PY

export TF_VAR_owner_creation_confirmation="$OWNER_CONFIRMATION"

FOUNDATION_PROVED=true
mark_checkpoint "01_FOUNDATION_CONFIRMED"
echo "DELIVERY_PIPELINE_FOUNDATION_PROVED"

terraform -chdir="$ENVIRONMENT_WS_MIXED" init \
  -input=false -reconfigure \
  -backend-config="$ENVIRONMENT_BACKEND_MIXED" \
  > "$RESULT_DIR/logs/environment-init.log" 2>&1

terraform -chdir="$ENVIRONMENT_WS_MIXED" validate \
  > "$RESULT_DIR/logs/environment-validate.log" 2>&1

terraform -chdir="$ENVIRONMENT_WS_MIXED" state list \
  > "$RESULT_DIR/terraform/environment/state-before-edge.txt"

terraform -chdir="$ENVIRONMENT_WS_MIXED" output -json \
  > "$RESULT_DIR/terraform/environment/output.json"

RELEASE_RESOLUTION_DIR="$RESULT_DIR/artifact/release-resolution"
mkdir -p "$RELEASE_RESOLUTION_DIR"
gh api \
  "repos/$GITHUB_REPOSITORY/actions/workflows/$RELEASE_WORKFLOW_FILE/runs" \
  --method GET \
  -f branch="$BRANCH" \
  -f head_sha="$HEAD_SHA" \
  -f status=success \
  -f per_page=20 \
  > "$RELEASE_RESOLUTION_DIR/candidate-runs.json"

py -3.12 - \
  "$RELEASE_RESOLUTION_DIR/candidate-runs.json" \
  "$HEAD_SHA" \
  "$RELEASE_RESOLUTION_DIR/resolved-release.env" <<'PY'
from pathlib import Path
import json
import sys

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
expected_head = sys.argv[2]
output = Path(sys.argv[3])
runs = [
    item for item in payload.get("workflow_runs", [])
    if item.get("head_sha") == expected_head
    and item.get("conclusion") == "success"
    and item.get("status") == "completed"
]
if not runs:
    raise SystemExit("SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED")

runs.sort(key=lambda item: item.get("created_at", ""), reverse=True)
run = runs[0]
database_id = run.get("id") or run.get("databaseId")
if not database_id:
    raise SystemExit("Resolved release run has no id")

output.write_text(f"RELEASE_RUN_ID={database_id}\n", encoding="utf-8")
print(f"SIGNED_RELEASE_RUN_RESOLVED={database_id}")
PY

# shellcheck disable=SC1091
source "$RELEASE_RESOLUTION_DIR/resolved-release.env"

gh api "repos/$GITHUB_REPOSITORY/actions/runs/$RELEASE_RUN_ID" \
  > "$RESULT_DIR/artifact/release-run.json"

gh api "repos/$GITHUB_REPOSITORY/actions/runs/$RELEASE_RUN_ID/artifacts" \
  > "$RESULT_DIR/artifact/release-artifacts.json"

py -3.12 - \
  "$RESULT_DIR/artifact/release-run.json" \
  "$RESULT_DIR/artifact/release-artifacts.json" \
  "$HEAD_SHA" \
  "$RELEASE_ARTIFACT" \
  "$RELEASE_RESOLUTION_DIR/resolved-artifact.env" <<'PY'
from pathlib import Path
import json
import sys

run = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
artifacts = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
expected_head = sys.argv[3]
expected_name = sys.argv[4]
output = Path(sys.argv[5])

if run.get("conclusion") != "success":
    raise SystemExit(f"Release run conclusion: {run.get('conclusion')}")
if run.get("head_sha") != expected_head:
    raise SystemExit("SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED")

matches = [
    item for item in artifacts.get("artifacts", [])
    if item.get("name") == expected_name
]
if len(matches) != 1:
    raise SystemExit("SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED")

artifact = matches[0]
if artifact.get("expired"):
    raise SystemExit("SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED")
digest = artifact.get("digest", "")
if not digest.startswith("sha256:"):
    raise SystemExit("Release artifact digest missing")

output.write_text(
    f"RELEASE_ARTIFACT_DIGEST={digest}\n",
    encoding="utf-8",
)

print("SIGNED_RELEASE_ARTIFACT_CONFIRMED")
PY

RELEASE_DOWNLOAD="$RESULT_DIR/artifact/download"
mkdir -p "$RELEASE_DOWNLOAD"
gh run download "$RELEASE_RUN_ID" \
  --repo "$GITHUB_REPOSITORY" \
  --name "$RELEASE_ARTIFACT" \
  --dir "$RELEASE_DOWNLOAD"

MANIFEST_PATH="$(find "$RELEASE_DOWNLOAD" -type f -name release-manifest.json -print -quit)"
CHECKSUM_PATH="$(find "$RELEASE_DOWNLOAD" -type f -name checksums.sha256 -print -quit)"
[[ -n "$MANIFEST_PATH" && -f "$MANIFEST_PATH" ]]
[[ -n "$CHECKSUM_PATH" && -f "$CHECKSUM_PATH" ]]

py -3.12 scripts/cloud/Test-G81ReleaseManifest.py "$MANIFEST_PATH"

gh attestation verify "$MANIFEST_PATH" \
  --repo "$GITHUB_REPOSITORY" \
  --signer-workflow "$GITHUB_REPOSITORY/.github/workflows/$RELEASE_WORKFLOW_FILE" \
  --source-digest "$HEAD_SHA" \
  --source-ref "refs/heads/$BRANCH" \
  > "$RESULT_DIR/artifact/manifest-attestation-verification.txt"

py -3.12 - \
  "$RELEASE_DOWNLOAD" \
  "$MANIFEST_PATH" \
  "$CHECKSUM_PATH" \
  "$HEAD_SHA" \
  "$RELEASE_RUN_ID" \
  "$RESULT_DIR/artifact/release-validation.json" <<'PY'
from pathlib import Path
import hashlib
import json
import sys

root = Path(sys.argv[1])
manifest_path = Path(sys.argv[2])
checksums_path = Path(sys.argv[3])
expected_head = sys.argv[4]
expected_run_id = sys.argv[5]
output = Path(sys.argv[6])

manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
errors = []
verified = []

if manifest.get("source_commit") != expected_head:
    errors.append("SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED")
if str(manifest.get("build_run_id")) != expected_run_id:
    errors.append("manifest build_run_id mismatch")

by_name = {}
for candidate in root.rglob("*"):
    if candidate.is_file():
        by_name.setdefault(candidate.name, []).append(candidate)

for raw in checksums_path.read_text(encoding="utf-8-sig").splitlines():
    line = raw.strip()
    if not line:
        continue
    digest, raw_name = line.split(maxsplit=1)
    basename = Path(raw_name.lstrip("*").replace("\\", "/")).name
    matches = by_name.get(basename, [])
    if len(matches) != 1:
        errors.append(f"checksum target {basename}: matches={len(matches)}")
        continue
    actual = hashlib.sha256(matches[0].read_bytes()).hexdigest()
    if actual.lower() != digest.lower():
        errors.append(f"checksum mismatch: {basename}")
    else:
        verified.append(basename)

for image_name, image in manifest.get("images", {}).items():
    reference = image.get("reference", "")
    if "@sha256:" not in reference:
        errors.append(f"image is not digest-pinned: {image_name}")
    if image.get("signature_verified") is not True:
        errors.append(f"image signature not verified: {image_name}")
    if image.get("sbom_verified") is not True:
        errors.append(f"image sbom not verified: {image_name}")
    if image.get("provenance_verified") is not True:
        errors.append(f"image provenance not verified: {image_name}")
    if image.get("critical") != 0 or image.get("high") != 0:
        errors.append(f"image vulnerability gate failed: {image_name}")

for required_image in ("postgres-migrations", "postgres-bootstrap"):
    image = manifest.get("images", {}).get(required_image)
    if not image:
        errors.append(f"required runtime job image missing: {required_image}")
        continue
    reference = image.get("reference", "")
    if f"/{required_image}@" not in reference or "@sha256:" not in reference:
        errors.append(f"runtime job image is not release-owned: {required_image}")

result = {
    "status": "PASS" if not errors else "FAIL",
    "source_commit": manifest.get("source_commit"),
    "build_run_id": manifest.get("build_run_id"),
    "image_count": len(manifest.get("images", {})),
    "checksums_verified": sorted(verified),
    "errors": errors,
}
output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    if "SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED" in errors:
        raise SystemExit("SIGNED_RELEASE_FOR_CURRENT_HEAD_REQUIRED")
    raise SystemExit("Release validation failed")
PY

cp "$MANIFEST_PATH" "$RESULT_DIR/artifact/release-manifest.json"
cp "$CHECKSUM_PATH" "$RESULT_DIR/artifact/release-checksums.sha256"
MANIFEST_PATH="$RESULT_DIR/artifact/release-manifest.json"
mark_checkpoint "02_RELEASE_VALIDATED"

for secret in \
  np-staging-rabbitmq-tls-certificate \
  np-staging-rabbitmq-tls-private-key \
  np-staging-rabbitmq-ca-certificate \
  np-staging-cloud-sql-server-ca \
  np-staging-postgres-app-password \
  np-staging-postgres-migration-password \
  np-staging-bootstrap-admin-password \
  np-staging-jwt-signing-key \
  np-staging-rabbitmq-app-username \
  np-staging-rabbitmq-app-password; do
  gcloud secrets versions list "$secret" \
    --project="$PROJECT_ID" \
    --filter='state=ENABLED' \
    --format=json \
    > "$RESULT_DIR/gcp/${secret}-versions.json"
done

py -3.12 - \
  "$RESULT_DIR/terraform/environment/output.json" \
  "$RESULT_DIR/gcp" \
  "$RESULT_DIR/artifact/resolved-runtime.env" <<'PY'
from pathlib import Path
import json
import shlex
import sys

outputs = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
gcp_dir = Path(sys.argv[2])
out = Path(sys.argv[3])
value = lambda key: outputs[key]["value"]

secret_names = [
    "np-staging-rabbitmq-tls-certificate",
    "np-staging-rabbitmq-tls-private-key",
    "np-staging-rabbitmq-ca-certificate",
    "np-staging-cloud-sql-server-ca",
    "np-staging-postgres-app-password",
    "np-staging-postgres-migration-password",
    "np-staging-bootstrap-admin-password",
    "np-staging-jwt-signing-key",
    "np-staging-rabbitmq-app-username",
    "np-staging-rabbitmq-app-password",
]
for name in secret_names:
    raw = json.loads(
        (gcp_dir / f"{name}-versions.json").read_text(
            encoding="utf-8-sig"
        )
    )
    versions = sorted(
        int(item["name"].rsplit("/", 1)[-1])
        for item in raw
        if item.get("state") == "ENABLED"
    )
    if versions != [1]:
        raise SystemExit(
            f"{name}: expected enabled version [1], got {versions}"
        )

accounts = value("runtime_service_accounts")
secret_ids = value("secret_ids")
versions = value("generated_secret_versions")

def secret_name(key):
    return secret_ids[key].rsplit("/", 1)[-1]

values = {
    "CLUSTER_NAME": value("cluster_id").rsplit("/", 1)[-1],
    "RUNTIME_NETWORK": value("network_name"),
    "RUNTIME_SUBNETWORK": value("subnetwork_name"),
    "CLOUD_SQL_PRIVATE_IP": value("cloud_sql_private_ip"),
    "RABBITMQ_HOST": value("rabbitmq_private_dns_name"),
    "RABBITMQ_TLS_SERVER_NAME": value("rabbitmq_private_dns_name"),
    "OTEL_ENDPOINT": f"http://{value('otel_private_dns_name')}:4317",
    "SIMULATOR_SERVICE_ACCOUNT": accounts["simulator"],
    "MIGRATION_SERVICE_ACCOUNT": accounts["migrations"],
    "BOOTSTRAP_SERVICE_ACCOUNT": accounts["bootstrap"],
    "SMOKE_SERVICE_ACCOUNT": accounts["smoke"],
    "POSTGRES_APP_PASSWORD_SECRET": secret_name("postgres-app-password"),
    "POSTGRES_APP_PASSWORD_VERSION": versions["postgres-app-password"],
    "POSTGRES_MIGRATION_PASSWORD_SECRET": secret_name("postgres-migration-password"),
    "POSTGRES_MIGRATION_PASSWORD_VERSION": versions["postgres-migration-password"],
    "BOOTSTRAP_ADMIN_PASSWORD_SECRET": secret_name("bootstrap-admin-password"),
    "BOOTSTRAP_ADMIN_PASSWORD_VERSION": versions["bootstrap-admin-password"],
    "RABBITMQ_USERNAME_SECRET": secret_name("rabbitmq-app-username"),
    "RABBITMQ_USERNAME_VERSION": versions["rabbitmq-app-username"],
    "RABBITMQ_PASSWORD_SECRET": secret_name("rabbitmq-app-password"),
    "RABBITMQ_PASSWORD_VERSION": versions["rabbitmq-app-password"],
    "RABBITMQ_CA_SECRET": secret_name("rabbitmq-ca-certificate"),
    "RABBITMQ_CA_VERSION": "1",
    "CLOUD_SQL_CA_SECRET": secret_name("cloud-sql-server-ca"),
    "CLOUD_SQL_CA_VERSION": "1",
}

out.write_text(
    "\n".join(
        f"{key}={shlex.quote(str(item))}"
        for key, item in values.items()
    ) + "\n",
    encoding="utf-8",
    newline="\n",
)
print("RUNTIME_INPUTS_RESOLVED")
PY

# shellcheck disable=SC1090
source "$RESULT_DIR/artifact/resolved-runtime.env"
mark_checkpoint "03_RUNTIME_INPUTS_RESOLVED"

rm -f \
  "$CHECKPOINT_ROOT/04_GKE_CREDENTIALS_READY" \
  "$CHECKPOINT_ROOT/04_GKE_AUTH_AND_API_READY"

gcloud container clusters get-credentials "$CLUSTER_NAME" \
  --project="$PROJECT_ID" --region="$REGION" --dns-endpoint --quiet

kubectl config current-context \
  > "$RESULT_DIR/kubernetes/current-context.txt"

kubectl get namespace default \
  --request-timeout=60s \
  -o json \
  > "$RESULT_DIR/kubernetes/default-namespace.json"

kubectl api-resources \
  --request-timeout=60s \
  --api-group="" \
  -o name \
  > "$RESULT_DIR/kubernetes/core-api-resources.txt"

CAN_I_GET_PODS="$(
  kubectl auth can-i get pods \
    --namespace natureprotector-staging \
    --request-timeout=60s |
  tr -d '\r\n'
)"
echo "KUBECTL_CAN_I_GET_PODS=$CAN_I_GET_PODS"
[[ "$CAN_I_GET_PODS" == "yes" ]]

kubectl get namespace natureprotector-staging \
  --request-timeout=60s \
  -o json \
  > "$RESULT_DIR/kubernetes/staging-namespace-before-deploy.json" 2>/dev/null || true

mark_checkpoint "04_GKE_AUTH_AND_API_READY"

gcloud sql instances describe np-staging-postgres   --project="$PROJECT_ID"   --format=json   > "$RESULT_DIR/gcp/cloud-sql-preflight.json"

gcloud builds worker-pools describe np-staging-deploy   --project="$PROJECT_ID"   --region="$REGION"   --format=json   > "$RESULT_DIR/gcp/cloud-build-worker-pool-preflight.json"

py -3.12 -   "$RESULT_DIR/gcp/cloud-sql-preflight.json"   "$RESULT_DIR/gcp/cloud-build-worker-pool-preflight.json"   "$CLOUD_SQL_PRIVATE_IP"   "$RESULT_DIR/audit/runtime-foundation-preflight.json" <<'PY'
from pathlib import Path
import json, sys
sql=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
pool=json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
expected_ip=sys.argv[3]
out=Path(sys.argv[4])
sql_ips=[item.get("ipAddress") for item in sql.get("ipAddresses", [])]
checks={
    "cloud_sql_runnable": sql.get("state")=="RUNNABLE",
    "cloud_sql_private_ip": expected_ip in sql_ips,
    "worker_pool_running": pool.get("state")=="RUNNING",
}
errors=[name for name, passed in checks.items() if not passed]
result={"status":"PASS" if not errors else "FAIL","checks":checks,"errors":errors}
out.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Runtime foundation preflight failed")
PY

OPERATOR_EVIDENCE="$RESULT_DIR/deploy/operator-foundation"
CLOUD_MUTATION=true
mark_checkpoint "04A_OPERATOR_FOUNDATION_STARTED"
bash "$PACKAGE_DIR/install-g81-cluster-dependencies-autopilot.sh" \
  "$PROJECT_ID" \
  "$REGION" \
  "$CLUSTER_NAME" \
  "$REPO_TOP/infra/gcp/kubernetes/g8-1/operator-lock.json" \
  "$OPERATOR_EVIDENCE"

export NP_G81_OPERATORS_READY=true
export NP_G81_OPERATOR_EVIDENCE
NP_G81_OPERATOR_EVIDENCE="$(cygpath -w "$OPERATOR_EVIDENCE")"
mark_checkpoint "04B_AUTOPILOT_OPERATOR_FOUNDATION_READY"

RELEASE_NAME="git-${HEAD_SHA:0:12}-r${RELEASE_RUN_ID}-s3"
echo "RELEASE_NAME=$RELEASE_NAME"
printf '%s\n' "$RELEASE_NAME" > "$RESULT_DIR/checkpoints/RELEASE_NAME"
DIRECT_FRONTEND_ORIGIN="https://natureprotector-frontend-${PROJECT_NUMBER}.${REGION}.run.app"

run_deployment() {
  local mode="$1"
  local origin="$2"
  local evidence_posix="$3"
  local edge_confirmation=""

  if [[ "$mode" == "services-only-bootstrap" ]]; then
    edge_confirmation="BOOTSTRAP_SERVICES_BEFORE_EDGE"
  fi

  mkdir -p "$evidence_posix"

  local manifest_win
  local evidence_win
  manifest_win="$(cygpath -w "$MANIFEST_PATH")"
  evidence_win="$(cygpath -w "$evidence_posix")"

  local args=(
    -NoProfile
    -File "$(cygpath -w "$PACKAGE_DIR/Deploy-G81Staging-Autopilot.ps1")"
    -ManifestPath "$manifest_win"
    -PlatformProjectId "$PROJECT_ID"
    -StagingProjectId "$PROJECT_ID"
    -Region "$REGION"
    -ClusterName "$CLUSTER_NAME"
    -ReleaseName "$RELEASE_NAME"
    -RuntimeNetwork "$RUNTIME_NETWORK"
    -RuntimeSubnetwork "$RUNTIME_SUBNETWORK"
    -CloudSqlPrivateIp "$CLOUD_SQL_PRIVATE_IP"
    -RabbitMqHost "$RABBITMQ_HOST"
    -RabbitMqTlsServerName "$RABBITMQ_TLS_SERVER_NAME"
    -OtelEndpoint "$OTEL_ENDPOINT"
    -SimulatorServiceAccount "$SIMULATOR_SERVICE_ACCOUNT"
    -MigrationServiceAccount "$MIGRATION_SERVICE_ACCOUNT"
    -BootstrapServiceAccount "$BOOTSTRAP_SERVICE_ACCOUNT"
    -SmokeServiceAccount "$SMOKE_SERVICE_ACCOUNT"
    -FrontendOrigin "$origin"
    -BootstrapAdminUsername "admin"
    -PostgresAppPasswordSecret "$POSTGRES_APP_PASSWORD_SECRET"
    -PostgresAppPasswordVersion "$POSTGRES_APP_PASSWORD_VERSION"
    -PostgresMigrationPasswordSecret "$POSTGRES_MIGRATION_PASSWORD_SECRET"
    -PostgresMigrationPasswordVersion "$POSTGRES_MIGRATION_PASSWORD_VERSION"
    -BootstrapAdminPasswordSecret "$BOOTSTRAP_ADMIN_PASSWORD_SECRET"
    -BootstrapAdminPasswordVersion "$BOOTSTRAP_ADMIN_PASSWORD_VERSION"
    -RabbitMqUsernameSecret "$RABBITMQ_USERNAME_SECRET"
    -RabbitMqUsernameVersion "$RABBITMQ_USERNAME_VERSION"
    -RabbitMqPasswordSecret "$RABBITMQ_PASSWORD_SECRET"
    -RabbitMqPasswordVersion "$RABBITMQ_PASSWORD_VERSION"
    -RabbitMqCaSecret "$RABBITMQ_CA_SECRET"
    -RabbitMqCaVersion "$RABBITMQ_CA_VERSION"
    -CloudSqlCaSecret "$CLOUD_SQL_CA_SECRET"
    -CloudSqlCaVersion "$CLOUD_SQL_CA_VERSION"
    -EvidenceDirectory "$evidence_win"
    -DeploymentMode "$mode"
  )

  if [[ -n "$edge_confirmation" ]]; then
    args+=(-EdgeBootstrapConfirmation "$edge_confirmation")
  fi

  pwsh "${args[@]}"
}

CLOUD_MUTATION=true
if [[ -f "$CHECKPOINT_ROOT/08_SERVICES_READY" ]]; then
  echo "RESUME: services checkpoint exists; validating live resources without repeating jobs."
else
  run_deployment \
    "services-only-bootstrap" \
    "$DIRECT_FRONTEND_ORIGIN" \
    "$RESULT_DIR/deploy/bootstrap" \
    > >(tee "$RESULT_DIR/logs/services-bootstrap.log") 2>&1
  mark_checkpoint "05_BOOTSTRAP_DEPLOYMENT_COMPLETED"
fi

gcloud run services describe natureprotector-api \
  --project="$PROJECT_ID" --region="$REGION" --format=json \
  > "$RESULT_DIR/gcp/natureprotector-api.json"

gcloud run services describe natureprotector-frontend \
  --project="$PROJECT_ID" --region="$REGION" --format=json \
  > "$RESULT_DIR/gcp/natureprotector-frontend.json"

gcloud container clusters get-credentials "$CLUSTER_NAME" \
  --project="$PROJECT_ID" --region="$REGION" --dns-endpoint --quiet

kubectl -n natureprotector-staging rollout status deployment/natureprotector-prevention --timeout=20m
kubectl -n natureprotector-staging rollout status deployment/natureprotector-otel --timeout=20m

py -3.12 - <<'PYWAIT'
import json
import subprocess
import time

deadline = time.time() + 1200
last = None
while time.time() < deadline:
    completed = subprocess.run(
        [
            "kubectl", "-n", "natureprotector-staging",
            "get", "rabbitmqcluster", "natureprotector-rabbitmq",
            "-o", "json",
        ],
        capture_output=True,
        text=True,
    )
    if completed.returncode == 0:
        data = json.loads(completed.stdout)
        last = data.get("status", {})
        for condition in last.get("conditions", []):
            if (
                "Ready" in str(condition.get("type", ""))
                and str(condition.get("status", "")).lower() == "true"
            ):
                print("RABBITMQ_READY_CONDITION_CONFIRMED")
                raise SystemExit(0)
        if (
            last.get("readyReplicas", 0) >= 1
            and last.get("readyReplicas") == last.get("replicas")
        ):
            print("RABBITMQ_READY_REPLICAS_CONFIRMED")
            raise SystemExit(0)
    time.sleep(10)
raise SystemExit(f"RabbitMQ did not become ready: {last}")
PYWAIT

kubectl -n natureprotector-staging get deployment natureprotector-prevention -o json \
  > "$RESULT_DIR/kubernetes/prevention.json"
kubectl -n natureprotector-staging get deployment natureprotector-otel -o json \
  > "$RESULT_DIR/kubernetes/otel.json"
kubectl -n natureprotector-staging get rabbitmqcluster natureprotector-rabbitmq -o json \
  > "$RESULT_DIR/kubernetes/rabbitmq.json"
kubectl -n natureprotector-staging get pods,services -o wide \
  > "$RESULT_DIR/kubernetes/workloads-after-bootstrap.txt"

for pipeline in natureprotector-api natureprotector-frontend natureprotector-prevention; do
  gcloud deploy rollouts list \
    --project="$PROJECT_ID" --region="$REGION" \
    --delivery-pipeline="$pipeline" --release="$RELEASE_NAME" \
    --sort-by=~createTime --limit=1 --format=json \
    > "$RESULT_DIR/gcp/${pipeline}-latest-rollout.json"
done

BOOTSTRAP_SUMMARY="$RESULT_DIR/deploy/bootstrap/staging-deployment-summary.json"
if [[ ! -f "$BOOTSTRAP_SUMMARY" ]]; then
  printf '{}\n' > "$BOOTSTRAP_SUMMARY"
fi

py -3.12 - \
  "$BOOTSTRAP_SUMMARY" \
  "$RESULT_DIR/gcp/natureprotector-api.json" \
  "$RESULT_DIR/gcp/natureprotector-frontend.json" \
  "$RESULT_DIR/kubernetes/prevention.json" \
  "$RESULT_DIR/kubernetes/otel.json" \
  "$RESULT_DIR/kubernetes/rabbitmq.json" \
  "$RESULT_DIR/gcp/natureprotector-api-latest-rollout.json" \
  "$RESULT_DIR/gcp/natureprotector-frontend-latest-rollout.json" \
  "$RESULT_DIR/gcp/natureprotector-prevention-latest-rollout.json" \
  "$HEAD_SHA" \
  "$RESULT_DIR/audit/services-bootstrap.json" <<'PY'
from pathlib import Path
import json
import sys

summary = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
api = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
frontend = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
prevention = json.loads(Path(sys.argv[4]).read_text(encoding="utf-8-sig"))
otel = json.loads(Path(sys.argv[5]).read_text(encoding="utf-8-sig"))
rabbit = json.loads(Path(sys.argv[6]).read_text(encoding="utf-8-sig"))
rollout_paths = [Path(sys.argv[7]), Path(sys.argv[8]), Path(sys.argv[9])]
expected_head = sys.argv[10]
output = Path(sys.argv[11])

def cloud_run_ready(service):
    status = service.get("status", {})
    if status.get("latestReadyRevisionName"):
        return True
    for condition in status.get("conditions", []):
        if condition.get("type") == "Ready":
            return (
                condition.get("status") in ("True", True)
                or condition.get("state") == "CONDITION_SUCCEEDED"
            )
    return False

def deployment_ready(resource):
    status = resource.get("status", {})
    return (
        status.get("availableReplicas", 0) >= 1
        and status.get("readyReplicas", 0) >= 1
    )

def rabbit_ready(resource):
    for condition in resource.get("status", {}).get("conditions", []):
        if (
            "Ready" in str(condition.get("type", ""))
            and condition.get("status") in ("True", True)
        ):
            return True
    status = resource.get("status", {})
    return (
        status.get("readyReplicas", 0) >= 1
        and status.get("readyReplicas") == status.get("replicas")
    )

live_rollout_states = []
for path in rollout_paths:
    raw = json.loads(path.read_text(encoding="utf-8-sig"))
    items = raw if isinstance(raw, list) else raw.get("rollouts", [])
    live_rollout_states.append(items[0].get("state") if items else None)
summary_present = bool(summary)
checks = {
    "summary_mode": (not summary_present) or summary.get("deployment_mode") == "services-only-bootstrap",
    "summary_source_commit": (not summary_present) or summary.get("source_commit")
        == expected_head,
    "live_rollouts_succeeded": live_rollout_states == ["SUCCEEDED", "SUCCEEDED", "SUCCEEDED"],
    "summary_edge_pending": (not summary_present) or summary.get("edge_bootstrap_pending") is True,
    "api_ready": cloud_run_ready(api),
    "frontend_ready": cloud_run_ready(frontend),
    "prevention_ready": deployment_ready(prevention),
    "otel_ready": deployment_ready(otel),
    "rabbitmq_ready": rabbit_ready(rabbit),
}
errors = [name for name, passed in checks.items() if not passed]

result = {
    "status": "PASS" if not errors else "FAIL",
    "checks": checks,
    "errors": errors,
}
output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Services bootstrap verification failed")
PY

SERVICES_BOOTSTRAPPED=true
mark_checkpoint "08_SERVICES_READY"
echo "SERVICES_ONLY_BOOTSTRAP_SUCCEEDED"

BOOTSTRAP_TFVARS="$RESULT_DIR/terraform/environment/edge-address-bootstrap.tfvars"
FINAL_EDGE_TFVARS="$RESULT_DIR/terraform/environment/edge-active.tfvars"
CANONICAL_ENV_TFVARS="$ENVIRONMENT_WS/terraform.staging.tfvars"

py -3.12 - \
  "$CANONICAL_ENV_TFVARS" \
  "$BOOTSTRAP_TFVARS" \
  "bootstrap.invalid" <<'PY'
from pathlib import Path
import sys

source = Path(sys.argv[1]).read_text(encoding="utf-8")
output = Path(sys.argv[2])
domain = sys.argv[3]

updated = source.replace(
    "create_edge                   = false",
    "create_edge                   = true",
)
updated = updated.replace(
    "managed_certificate_domains      = []",
    f'managed_certificate_domains      = ["{domain}"]',
)
if updated == source:
    raise SystemExit("Unable to create edge tfvars override")
if "create_edge                   = true" not in updated:
    raise SystemExit("create_edge was not enabled")
output.write_text(updated, encoding="utf-8", newline="\n")
PY

ADDRESS_STATE="google_compute_global_address.https[0]"
ADDRESS_EXISTS=false
if gcloud compute addresses describe np-staging-https \
  --project="$PROJECT_ID" --global --format=json \
  > "$RESULT_DIR/gcp/edge-address-before.json" 2>/dev/null; then
  ADDRESS_EXISTS=true
fi

if [[ "$ADDRESS_EXISTS" == "false" ]]; then
  if terraform -chdir="$ENVIRONMENT_WS_MIXED" plan \
    -input=false -lock=false -refresh=true -detailed-exitcode \
    -target="$ADDRESS_STATE" \
    -var-file="$(cygpath -m "$BOOTSTRAP_TFVARS")" \
    -out="$(cygpath -m "$RESULT_DIR/terraform/environment/address-bootstrap.tfplan")" \
    > "$RESULT_DIR/logs/address-bootstrap-plan.log" 2>&1; then
    ADDRESS_PLAN_RC=0
  else
    ADDRESS_PLAN_RC=$?
  fi

  cat "$RESULT_DIR/logs/address-bootstrap-plan.log"
  [[ "$ADDRESS_PLAN_RC" -eq 2 ]]

  terraform -chdir="$ENVIRONMENT_WS_MIXED" show -json \
    "$(cygpath -m "$RESULT_DIR/terraform/environment/address-bootstrap.tfplan")" \
    > "$RESULT_DIR/terraform/environment/address-bootstrap-plan.json"

  py -3.12 - \
    "$RESULT_DIR/terraform/environment/address-bootstrap-plan.json" <<'PY'
import json
import sys

plan = json.load(open(sys.argv[1], encoding="utf-8-sig"))
changes = [
    item for item in plan.get("resource_changes", [])
    if item.get("change", {}).get("actions") != ["no-op"]
]
expected = "google_compute_global_address.https[0]"
if len(changes) != 1:
    raise SystemExit(f"Expected one address change, got {len(changes)}")
if changes[0].get("address") != expected:
    raise SystemExit(f"Unexpected address change: {changes[0].get('address')}")
if changes[0].get("change", {}).get("actions") != ["create"]:
    raise SystemExit("Address bootstrap action is not create")
print("EDGE_ADDRESS_BOOTSTRAP_PLAN_CONFIRMED")
PY

  TERRAFORM_APPLY_EXECUTED=true
  terraform -chdir="$ENVIRONMENT_WS_MIXED" apply \
    -input=false \
    "$(cygpath -m "$RESULT_DIR/terraform/environment/address-bootstrap.tfplan")" \
    > >(tee "$RESULT_DIR/logs/address-bootstrap-apply.log") 2>&1
fi

EDGE_IP="$(
  gcloud compute addresses describe np-staging-https \
    --project="$PROJECT_ID" --global \
    --format='value(address)' |
  tr -d '\r\n'
)"
[[ "$EDGE_IP" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]

EDGE_HOST="$(
  py -3.12 - "$EDGE_IP" "$RESULT_DIR/dns/dynamic-host-selection.json" <<'PYDNS'
from pathlib import Path
import json
import socket
import sys
import urllib.parse
import urllib.request

ip = sys.argv[1]
output = Path(sys.argv[2])
dash = ip.replace(".", "-")
candidates = [f"{dash}.sslip.io", f"{dash}.nip.io"]
resolvers = {
    "google": "https://dns.google/resolve",
    "cloudflare": "https://cloudflare-dns.com/dns-query",
}
observations = {}
selected = None
allowed_cas = {"pki.goog", "letsencrypt.org"}

def resolve_json(endpoint, name, record_type):
    query = urllib.parse.urlencode({"name": name, "type": record_type})
    request = urllib.request.Request(
        f"{endpoint}?{query}",
        headers={
            "Accept": "application/dns-json",
            "User-Agent": "NatureProtector-deployment-proof/1",
        },
    )
    with urllib.request.urlopen(request, timeout=20) as response:
        return json.load(response)

def answers(payload, numeric_type):
    return [
        answer.get("data")
        for answer in payload.get("Answer", [])
        if answer.get("type") == numeric_type
    ]

def parse_caa(values):
    issuers = []
    for value in values:
        parts = str(value).strip().split(maxsplit=2)
        if len(parts) != 3:
            continue
        tag = parts[1].strip('"').lower()
        ca = parts[2].strip().strip('"').split(";", 1)[0].strip().lower()
        if tag == "issue":
            issuers.append(ca)
    return issuers

for host in candidates:
    base_domain = host.split(".", 1)[1]
    item = {
        "local_a": None,
        "public": {},
        "accepted": False,
        "reasons": [],
    }

    try:
        item["local_a"] = socket.gethostbyname(host)
    except OSError as exc:
        item["local_error"] = str(exc)

    public_a_sets = []
    public_aaaa = []
    caa_values = []

    for resolver_name, endpoint in resolvers.items():
        resolver_item = {}
        for record_type, numeric_type in [("A", 1), ("AAAA", 28)]:
            try:
                payload = resolve_json(endpoint, host, record_type)
                values = answers(payload, numeric_type)
                resolver_item[record_type] = values
                if record_type == "A":
                    public_a_sets.append(values)
                else:
                    public_aaaa.extend(values)
            except Exception as exc:
                resolver_item[f"{record_type}_error"] = str(exc)

        resolver_item["CAA"] = []
        for caa_name in [host, base_domain]:
            try:
                payload = resolve_json(endpoint, caa_name, "CAA")
                values = answers(payload, 257)
                resolver_item["CAA"].append(
                    {"name": caa_name, "values": values}
                )
                caa_values.extend(values)
            except Exception as exc:
                resolver_item["CAA"].append(
                    {"name": caa_name, "error": str(exc)}
                )

        item["public"][resolver_name] = resolver_item

    nonempty_a_sets = [values for values in public_a_sets if values]
    if item.get("local_a") != ip:
        item["reasons"].append("local A record does not match the edge IP")
    if not nonempty_a_sets:
        item["reasons"].append("no public resolver returned an A record")
    elif any(set(values) != {ip} for values in nonempty_a_sets):
        item["reasons"].append(
            "a public resolver returned an A record other than the edge IP"
        )
    if public_aaaa:
        item["reasons"].append(
            f"unexpected AAAA records are present: {sorted(set(public_aaaa))}"
        )

    issuers = parse_caa(caa_values)
    item["caa_issue_authorities"] = sorted(set(issuers))
    if issuers and not any(issuer in allowed_cas for issuer in issuers):
        item["reasons"].append(
            "CAA records do not allow pki.goog or letsencrypt.org"
        )

    item["accepted"] = not item["reasons"]
    observations[host] = item
    if item["accepted"]:
        selected = host
        break

result = {
    "expected_ip": ip,
    "selected_host": selected,
    "allowed_certificate_authorities": sorted(allowed_cas),
    "observations": observations,
}
output.write_text(
    json.dumps(result, indent=2) + "\n",
    encoding="utf-8",
)
if not selected:
    raise SystemExit(
        "Neither sslip.io nor nip.io passed A, AAAA and CAA validation"
    )
print(selected)
PYDNS
)"
STAGING_URL="https://$EDGE_HOST"
echo "EDGE_IP=$EDGE_IP"
echo "EDGE_HOST=$EDGE_HOST"
echo "STAGING_URL=$STAGING_URL"
printf '%s\n' "$STAGING_URL" > "$CHECKPOINT_ROOT/STAGING_URL"
mark_checkpoint "09_EDGE_ADDRESS_READY"

py -3.12 - \
  "$CANONICAL_ENV_TFVARS" \
  "$FINAL_EDGE_TFVARS" \
  "$EDGE_HOST" <<'PY'
from pathlib import Path
import sys

source = Path(sys.argv[1]).read_text(encoding="utf-8")
output = Path(sys.argv[2])
domain = sys.argv[3]

updated = source.replace(
    "create_edge                   = false",
    "create_edge                   = true",
)
updated = updated.replace(
    "managed_certificate_domains      = []",
    f'managed_certificate_domains      = ["{domain}"]',
)
if updated == source:
    raise SystemExit("Unable to create final edge tfvars")
output.write_text(updated, encoding="utf-8", newline="\n")
PY

terraform -chdir="$ENVIRONMENT_WS_MIXED" state list \
  > "$RESULT_DIR/terraform/environment/state-before-full-edge.txt"

if ! grep -Fxq "$ADDRESS_STATE" \
  "$RESULT_DIR/terraform/environment/state-before-full-edge.txt"; then
  terraform -chdir="$ENVIRONMENT_WS_MIXED" import \
    -input=false \
    -var-file="$(cygpath -m "$FINAL_EDGE_TFVARS")" \
    "$ADDRESS_STATE" \
    "projects/$PROJECT_ID/global/addresses/np-staging-https" \
    > >(tee "$RESULT_DIR/logs/address-import.log") 2>&1
fi

cp "$RESULT_DIR/dns/dynamic-host-selection.json" "$RESULT_DIR/dns/resolution.json"

if terraform -chdir="$ENVIRONMENT_WS_MIXED" plan \
  -input=false -lock=false -refresh=true -detailed-exitcode \
  -var-file="$(cygpath -m "$FINAL_EDGE_TFVARS")" \
  -out="$(cygpath -m "$RESULT_DIR/terraform/environment/full-edge.tfplan")" \
  > "$RESULT_DIR/logs/full-edge-plan.log" 2>&1; then
  EDGE_PLAN_RC=0
else
  EDGE_PLAN_RC=$?
fi

cat "$RESULT_DIR/logs/full-edge-plan.log"
[[ "$EDGE_PLAN_RC" -eq 0 || "$EDGE_PLAN_RC" -eq 2 ]]

terraform -chdir="$ENVIRONMENT_WS_MIXED" show -json \
  "$(cygpath -m "$RESULT_DIR/terraform/environment/full-edge.tfplan")" \
  > "$RESULT_DIR/terraform/environment/full-edge-plan.json"

py -3.12 - \
  "$RESULT_DIR/terraform/environment/state-before-full-edge.txt" \
  "$RESULT_DIR/terraform/environment/full-edge-plan.json" \
  "$RESULT_DIR/audit/full-edge-plan.json" <<'PY'
from pathlib import Path
import json
import sys

state = {
    item.strip()
    for item in Path(sys.argv[1]).read_text().splitlines()
    if item.strip()
}
plan = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
output = Path(sys.argv[3])

expected = {
    "google_compute_security_policy.edge[0]",
    "google_compute_security_policy_rule.login_rate_limit[0]",
    "google_compute_security_policy_rule.simulation_rate_limit[0]",
    "google_compute_security_policy_rule.api_rate_limit[0]",
    "google_compute_security_policy_rule.owasp_sqli[0]",
    "google_compute_security_policy_rule.owasp_xss[0]",
    "google_compute_security_policy_rule.default[0]",
    "google_compute_region_network_endpoint_group.api[0]",
    "google_compute_region_network_endpoint_group.frontend[0]",
    "google_compute_backend_service.api[0]",
    "google_compute_backend_service.frontend[0]",
    "google_compute_url_map.https[0]",
    "google_compute_managed_ssl_certificate.https[0]",
    "google_compute_global_address.https[0]",
    "google_compute_target_https_proxy.https[0]",
    "google_compute_global_forwarding_rule.https[0]",
}
missing_before = expected - state
changes = [
    item for item in plan.get("resource_changes", [])
    if item.get("change", {}).get("actions") != ["no-op"]
]
actions = {
    item["address"]: item.get("change", {}).get("actions")
    for item in changes
}
actual = set(actions)
errors = []

if actual != missing_before:
    errors.append(
        f"edge address mismatch: "
        f"missing={sorted(missing_before - actual)} "
        f"unexpected={sorted(actual - missing_before)}"
    )
for address, action in actions.items():
    if action != ["create"]:
        errors.append(f"{address}: expected create, got {action}")

result = {
    "status": "PASS" if not errors else "FAIL",
    "expected_edge_resources": len(expected),
    "already_in_state": len(expected & state),
    "planned_creates": len(actions),
    "actions": actions,
    "errors": errors,
}
output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Full edge plan audit failed")
PY

if [[ "$EDGE_PLAN_RC" -eq 2 ]]; then
  TERRAFORM_APPLY_EXECUTED=true
  terraform -chdir="$ENVIRONMENT_WS_MIXED" apply \
    -input=false \
    "$(cygpath -m "$RESULT_DIR/terraform/environment/full-edge.tfplan")" \
    > >(tee "$RESULT_DIR/logs/full-edge-apply.log") 2>&1
fi

terraform -chdir="$ENVIRONMENT_WS_MIXED" state list \
  > "$RESULT_DIR/terraform/environment/state-after-edge.txt"

terraform -chdir="$ENVIRONMENT_WS_MIXED" output -json \
  > "$RESULT_DIR/terraform/environment/output-after-edge.json"

py -3.12 - \
  "$RESULT_DIR/terraform/environment/state-after-edge.txt" \
  "$RESULT_DIR/terraform/environment/output-after-edge.json" \
  "$EDGE_IP" \
  "$RESULT_DIR/audit/edge-state.json" <<'PY'
from pathlib import Path
import json
import sys

state = [
    item.strip()
    for item in Path(sys.argv[1]).read_text().splitlines()
    if item.strip()
]
outputs = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
expected_ip = sys.argv[3]
output = Path(sys.argv[4])

managed = [item for item in state if not item.startswith("data.")]
data = [item for item in state if item.startswith("data.")]
edge_ip = outputs["edge_ip"]["value"]

errors = []
if len(managed) != 101:
    errors.append(f"expected 101 managed resources, got {len(managed)}")
if len(data) != 1:
    errors.append(f"expected 1 data resource, got {len(data)}")
if edge_ip != expected_ip:
    errors.append(f"edge output IP mismatch: {edge_ip}")

result = {
    "status": "PASS" if not errors else "FAIL",
    "managed_resources": len(managed),
    "data_resources": len(data),
    "edge_ip": edge_ip,
    "errors": errors,
}
output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Edge state verification failed")
PY

mark_checkpoint "10_EDGE_RESOURCES_READY"

CERT_DEADLINE=$(( $(date +%s) + 5400 ))
CERT_STATUS=""
while (( $(date +%s) < CERT_DEADLINE )); do
  if gcloud compute ssl-certificates describe np-staging \
    --project="$PROJECT_ID" --global --format=json \
    > "$RESULT_DIR/gcp/edge-certificate-latest.json" 2>/dev/null; then
    CERT_STATUS="$(
      py -3.12 - "$RESULT_DIR/gcp/edge-certificate-latest.json" <<'PY'
import json
import sys
data = json.load(open(sys.argv[1], encoding="utf-8-sig"))
print(data.get("managed", {}).get("status", ""))
PY
    )"
    echo "CERTIFICATE_STATUS=$CERT_STATUS"

    if [[ "$CERT_STATUS" == "ACTIVE" ]]; then
      cp "$RESULT_DIR/gcp/edge-certificate-latest.json" \
        "$RESULT_DIR/gcp/edge-certificate-active.json"
      break
    fi

    if [[ "$CERT_STATUS" == FAILED* && "$CERT_STATUS" != "FAILED_RETRYING_NOT_VISIBLE" ]]; then
      echo "ERROR: managed certificate entered terminal failure state $CERT_STATUS" >&2
      exit 1
    fi
  fi
  sleep 30
done

[[ "$CERT_STATUS" == "ACTIVE" ]]
EDGE_HTTPS_ACTIVE=true
mark_checkpoint "11_CERTIFICATE_ACTIVE"
echo "EDGE_HTTPS_ACTIVE"

py -3.12 - \
  "$STAGING_URL" \
  "$RESULT_DIR/audit/https-probes-before-verified.json" <<'PY'
from pathlib import Path
import json
import ssl
import sys
import time
import urllib.request

origin = sys.argv[1].rstrip("/")
output = Path(sys.argv[2])
paths = ["/", "/healthz"]
results = {}
deadline = time.time() + 900
context = ssl.create_default_context()

while time.time() < deadline:
    all_ok = True
    results = {}
    for path in paths:
        url = origin + path
        try:
            request = urllib.request.Request(
                url,
                headers={"User-Agent": "NatureProtector-deployment-proof/1"},
            )
            with urllib.request.urlopen(
                request,
                timeout=30,
                context=context,
            ) as response:
                code = response.getcode()
                body = response.read(512).decode(
                    "utf-8",
                    errors="replace",
                )
            results[path] = {
                "status": code,
                "body_prefix": body[:200],
            }
            if code < 200 or code >= 400:
                all_ok = False
        except Exception as exc:
            results[path] = {"error": str(exc)}
            all_ok = False
    if all_ok:
        break
    time.sleep(15)

status = "PASS" if all(
    isinstance(item, dict)
    and 200 <= item.get("status", 0) < 400
    for item in results.values()
) else "FAIL"

payload = {
    "origin": origin,
    "status": status,
    "probes": results,
}
output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(json.dumps(payload, indent=2))
if status != "PASS":
    raise SystemExit("HTTPS probes did not pass")
PY

run_deployment \
  "verified" \
  "$STAGING_URL" \
  "$RESULT_DIR/deploy/verified" \
  > >(tee "$RESULT_DIR/logs/verified-deployment.log") 2>&1

py -3.12 - \
  "$RESULT_DIR/deploy/verified/staging-deployment-summary.json" \
  "$HEAD_SHA" \
  "$STAGING_URL" \
  "$RESULT_DIR/audit/final-staging-verification.json" <<'PY'
from pathlib import Path
import json
import sys

summary = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
expected_head = sys.argv[2]
origin = sys.argv[3]
output = Path(sys.argv[4])

rollouts = summary.get("rollouts", [])
checks = {
    "environment": summary.get("environment") == "staging",
    "deployment_mode": summary.get("deployment_mode") == "verified",
    "source_commit": summary.get("source_commit") == expected_head,
    "three_rollouts": len(rollouts) == 3,
    "rollouts_succeeded":
        all(item.get("state") == "SUCCEEDED" for item in rollouts),
    "functional_smoke":
        summary.get("functional_smoke_passed") is True,
    "edge_not_pending":
        summary.get("edge_bootstrap_pending") is False,
    "staging_verified":
        summary.get("staging_verified") is True,
}
errors = [name for name, passed in checks.items() if not passed]

result = {
    "status": "PASS" if not errors else "FAIL",
    "staging_url": origin,
    "release_name": summary.get("release_name"),
    "checks": checks,
    "errors": errors,
}
output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
if errors:
    raise SystemExit("Final staging verification failed")
PY

if terraform -chdir="$ENVIRONMENT_WS_MIXED" plan \
  -input=false -lock=false -refresh=true -detailed-exitcode \
  -var-file="$(cygpath -m "$FINAL_EDGE_TFVARS")" \
  -out="$(cygpath -m "$RESULT_DIR/terraform/environment/final-no-change.tfplan")" \
  > "$RESULT_DIR/logs/environment-final-no-change.log" 2>&1; then
  FINAL_PLAN_RC=0
else
  FINAL_PLAN_RC=$?
fi

cat "$RESULT_DIR/logs/environment-final-no-change.log"
if [[ "$FINAL_PLAN_RC" -eq 0 ]]; then
  echo "FINAL_TERRAFORM_NO_CHANGE_CONFIRMED"
elif [[ "$FINAL_PLAN_RC" -eq 2 ]]; then
  terraform -chdir="$ENVIRONMENT_WS_MIXED" show -json \
    "$(cygpath -m "$RESULT_DIR/terraform/environment/final-no-change.tfplan")" \
    > "$RESULT_DIR/terraform/environment/final-drift-plan.json"
  echo "FINAL_TERRAFORM_DRIFT_RECORDED_NON_BLOCKING"
else
  echo "ERROR: final Terraform plan failed with exit code $FINAL_PLAN_RC" >&2
  exit "$FINAL_PLAN_RC"
fi

STAGING_VERIFIED=true
mark_checkpoint "13_STAGING_DEPLOYMENT_PROVED"

cat > "$RESULT_DIR/staging-access.txt" <<EOF
NatureProtector staging
URL: $STAGING_URL
Source commit: $HEAD_SHA
Release: $RELEASE_NAME
Status: STAGING_DEPLOYMENT_PROVED
EOF

cat > "$RESULT_DIR/final-summary.txt" <<EOF
DELIVERY_PIPELINE_FOUNDATION_PROVED
SERVICES_ONLY_BOOTSTRAP_SUCCEEDED
API_SERVICE_READY
FRONTEND_SERVICE_READY
PREVENTION_READY
RABBITMQ_READY
OTEL_READY
EDGE_HTTPS_ACTIVE
CERTIFICATE_ACTIVE
FUNCTIONAL_SMOKE_PASSED
STAGING_VERIFIED=true
STAGING_DEPLOYMENT_PROVED
STAGING_URL=$STAGING_URL
PRODUCTION_AUTHORIZED=false
PRODUCTION_DEPLOYED=false
EOF

package_result "STAGING_DEPLOYMENT_PROVED_AFTER_CLEAN_AMD64_OPERATOR_REMIRROR"
trap - ERR INT TERM

cat "$RESULT_DIR/final-summary.txt"
cat "$RESULT_DIR/result.env"
