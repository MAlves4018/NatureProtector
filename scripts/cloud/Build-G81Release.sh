#!/usr/bin/env bash
set -euo pipefail

resolve_release_run_attempt() {
  local release_run_attempt
  if [[ ${GITHUB_RUN_ATTEMPT+x} == x ]]; then
    release_run_attempt="$GITHUB_RUN_ATTEMPT"
  else
    release_run_attempt="1"
  fi

  [[ "$release_run_attempt" =~ ^[1-9][0-9]*$ ]] || {
    echo "Invalid GitHub run attempt" >&2
    return 1
  }

  printf '%s\n' "$release_run_attempt"
}

build_release_tag() {
  local source_sha="${1:?}"
  local run_id="${2:?}"
  local release_run_attempt="${3:?}"

  [[ "$release_run_attempt" =~ ^[1-9][0-9]*$ ]] || {
    echo "Invalid GitHub run attempt" >&2
    return 1
  }

  printf 'git-%s-run-%s-attempt-%s\n' "$source_sha" "$run_id" "$release_run_attempt"
}

main() {
: "${GCP_PLATFORM_PROJECT_ID:?}"
: "${GCP_REGION:?}"
: "${GCP_ARTIFACT_REPOSITORY:?}"
: "${GITHUB_REPOSITORY:?}"
: "${GITHUB_SHA:?}"
: "${GITHUB_RUN_ID:?}"
: "${ENGINEERING_RUN_ID:?}"
: "${SECURITY_RUN_ID:?}"
: "${POLICY_RUN_ID:?}"
: "${COSIGN_CERTIFICATE_IDENTITY:?}"

[[ "$GCP_REGION" == "europe-southwest1" ]] || { echo "Unexpected region" >&2; exit 1; }
[[ "$GITHUB_REPOSITORY" == "MAlves4018/NatureProtector" ]] || { echo "Unexpected repository" >&2; exit 1; }
[[ "$GCP_PLATFORM_PROJECT_ID" != *cn2526* ]] || { echo "CN projects are forbidden" >&2; exit 1; }

out="${G81_RELEASE_DIR:-g81-release}"
mkdir -p "$out"
registry="${GCP_REGION}-docker.pkg.dev/${GCP_PLATFORM_PROJECT_ID}/${GCP_ARTIFACT_REPOSITORY}"
release_run_attempt="$(resolve_release_run_attempt)"
tag="$(build_release_tag "$GITHUB_SHA" "$GITHUB_RUN_ID" "$release_run_attempt")"
identity="$COSIGN_CERTIFICATE_IDENTITY"

declare -A dockerfiles=(
  [backoffice-api]="src/NatureProtector.Backoffice.Api/Dockerfile"
  [prevention]="src/NatureProtector.Prevention.Host/Dockerfile"
  [simulator]="src/NatureProtector.Simulator.Host/Dockerfile"
  [postgres-migrations]="src/NatureProtector.Postgres.Migrations/Dockerfile"
  [postgres-bootstrap]="src/NatureProtector.Postgres.Bootstrap/Dockerfile"
  [frontend]="webUI/Dockerfile"
  [functional-smoke]="infra/gcp/smoke/Dockerfile"
  [rabbitmq]="infra/gcp/rabbitmq/Dockerfile"
  [otel-collector]="infra/gcp/otel/Dockerfile"
  [distributed-probe]="infra/gcp/chain-probe/Dockerfile"
  [cloud-deploy-verifier]="infra/gcp/cloud-deploy-verifier/Dockerfile"
)

printf '{}\n' > "$out/images.json"
for component in backoffice-api prevention simulator postgres-migrations postgres-bootstrap frontend functional-smoke rabbitmq otel-collector distributed-probe cloud-deploy-verifier; do
  image="${registry}/${component}:${tag}"
  docker buildx build . \
    --file "${dockerfiles[$component]}" \
    --platform linux/amd64 \
    --tag "$image" \
    --push \
    --sbom=true \
    --provenance=mode=max

  digest="$(docker buildx imagetools inspect "$image" --format '{{.Manifest.Digest}}')"
  [[ "$digest" =~ ^sha256:[0-9a-f]{64}$ ]] || { echo "Invalid digest for $component" >&2; exit 1; }
  reference="${registry}/${component}@${digest}"

  scan_name="$(gcloud artifacts docker images scan "$reference" --remote --location=europe --format='value(response.scan)' --quiet)"
  gcloud artifacts docker images list-vulnerabilities "$scan_name" --format=json > "$out/${component}-vulnerabilities.json"
  high="$(jq '[.[] | select((.vulnerability.effectiveSeverity // .vulnerability.severity // "") == "HIGH")] | length' "$out/${component}-vulnerabilities.json")"
  critical="$(jq '[.[] | select((.vulnerability.effectiveSeverity // .vulnerability.severity // "") == "CRITICAL")] | length' "$out/${component}-vulnerabilities.json")"
  [[ "$high" == 0 && "$critical" == 0 ]] || { echo "$component has HIGH/CRITICAL vulnerabilities" >&2; exit 1; }

  cosign sign --yes "$reference"
  cosign verify "$reference" \
    --certificate-identity "$identity" \
    --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
    > "$out/${component}-signature-verification.json"

  docker buildx imagetools inspect "$reference" --raw > "$out/${component}-oci-index.json"
  predicate_types="$(
    jq -r '.manifests[]? | select(.annotations["vnd.docker.reference.type"] == "attestation-manifest") | .digest' "$out/${component}-oci-index.json" |
    while read -r attestation_digest; do
      docker buildx imagetools inspect "${registry}/${component}@${attestation_digest}" --raw |
        jq -r '.layers[]?.annotations["in-toto.io/predicate-type"] // empty'
    done
  )"
  grep -q '^https://spdx.dev/Document$' <<<"$predicate_types" || { echo "Missing SPDX SBOM for $component" >&2; exit 1; }
  grep -q '^https://slsa.dev/provenance/' <<<"$predicate_types" || { echo "Missing SLSA provenance for $component" >&2; exit 1; }

  jq --arg name "$component" --arg reference "$reference" --arg digest "$digest" \
    '. + {($name): {reference:$reference,digest:$digest,signature_verified:true,high:0,critical:0,sbom_verified:true,provenance_verified:true}}' \
    "$out/images.json" > "$out/images.tmp"
  mv "$out/images.tmp" "$out/images.json"
done

python3 scripts/cloud/New-G81ReleaseManifest.py \
  --images "$out/images.json" \
  --output "$out/release-manifest.json" \
  --repository "$GITHUB_REPOSITORY" \
  --commit "$GITHUB_SHA" \
  --build-run-id "$GITHUB_RUN_ID" \
  --platform-project "$GCP_PLATFORM_PROJECT_ID" \
  --engineering-run-id "$ENGINEERING_RUN_ID" \
  --security-run-id "$SECURITY_RUN_ID" \
  --policy-run-id "$POLICY_RUN_ID"
python3 scripts/cloud/Test-G81ReleaseManifest.py "$out/release-manifest.json"
sha256sum "$out"/* > "$out/checksums.sha256"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
