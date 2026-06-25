#!/usr/bin/env bash
set -euo pipefail

: "${G81_IMAGES_EVIDENCE:?}"
: "${GCP_PLATFORM_PROJECT_ID:?}"
: "${GCP_REGION:?}"
: "${GCP_ARTIFACT_REPOSITORY:?}"
: "${GITHUB_REPOSITORY:?}"
: "${GITHUB_SHA:?}"
: "${GITHUB_RUN_ID:?}"
: "${ENGINEERING_RUN_ID:?}"
: "${SECURITY_RUN_ID:?}"
: "${POLICY_RUN_ID:?}"

[[ "$GCP_PLATFORM_PROJECT_ID" == "np-platform-migkxl-20260624" ]] || { echo "Unexpected platform project" >&2; exit 1; }
[[ "$GCP_REGION" == "europe-southwest1" ]] || { echo "Unexpected region" >&2; exit 1; }
[[ "$GCP_ARTIFACT_REPOSITORY" == "natureprotector" ]] || { echo "Unexpected Artifact Registry repository" >&2; exit 1; }
[[ "$GITHUB_REPOSITORY" == "MAlves4018/NatureProtector" ]] || { echo "Unexpected repository" >&2; exit 1; }
[[ "$GITHUB_SHA" =~ ^[0-9a-f]{40}$ ]] || { echo "Unexpected source SHA" >&2; exit 1; }

out="${G81_RELEASE_DIR:-g81-release}"
mkdir -p "$out/signatures"

registry="${GCP_REGION}-docker.pkg.dev/${GCP_PLATFORM_PROJECT_ID}/${GCP_ARTIFACT_REPOSITORY}"
identity="https://github.com/${GITHUB_REPOSITORY}/.github/workflows/gcp-g8-1-release.yml@refs/heads/master"
issuer="https://token.actions.githubusercontent.com"

required='["backoffice-api","prevention","simulator","postgres-migrations","postgres-bootstrap","frontend","functional-smoke","rabbitmq","otel-collector","distributed-probe","cloud-deploy-verifier"]'

jq -e --argjson required "$required" '
  .images as $images
  | ($images | type) == "object"
  and (($images | keys | sort) == ($required | sort))
  and ([ $images[] | .reference ] | unique | length == 11)
  and all($images[]; (.digest | test("^sha256:[0-9a-f]{64}$")))
  and all($images[]; (.reference | startswith("'"$registry"'/")))
  and all($images[]; (.reference | contains("@sha256:")))
  and all($images[]; . as $image | ($image.reference | endswith($image.digest)))
  and all($images[]; (.high == 0 and .critical == 0 and .sbom_verified == true and .provenance_verified == true))
' "$G81_IMAGES_EVIDENCE" >/dev/null

printf '{"images":{}}\n' > "$out/signature-results.json"

for component in backoffice-api prevention simulator postgres-migrations postgres-bootstrap frontend functional-smoke rabbitmq otel-collector distributed-probe cloud-deploy-verifier; do
  reference="$(jq -r --arg name "$component" '.images[$name].reference' "$G81_IMAGES_EVIDENCE")"
  digest="$(jq -r --arg name "$component" '.images[$name].digest' "$G81_IMAGES_EVIDENCE")"
  [[ "$reference" == "${registry}/${component}@${digest}" ]] || { echo "Unexpected reference for $component: $reference" >&2; exit 1; }

  gcloud artifacts docker images describe "$reference" --format=json > "$out/${component}-artifact-describe.json"

  scan_name="$(gcloud artifacts docker images scan "$reference" --remote --project="$GCP_PLATFORM_PROJECT_ID" --location=europe --format='value(response.scan)' --quiet)"
  gcloud artifacts docker images list-vulnerabilities "$scan_name" --project="$GCP_PLATFORM_PROJECT_ID" --location=europe --format=json > "$out/${component}-vulnerabilities.json"
  high="$(jq '[.[] | select((.vulnerability.effectiveSeverity // .vulnerability.severity // "") == "HIGH")] | length' "$out/${component}-vulnerabilities.json")"
  critical="$(jq '[.[] | select((.vulnerability.effectiveSeverity // .vulnerability.severity // "") == "CRITICAL")] | length' "$out/${component}-vulnerabilities.json")"
  [[ "$high" == 0 && "$critical" == 0 ]] || { echo "$component has HIGH/CRITICAL vulnerabilities" >&2; exit 1; }

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

  cosign sign --yes "$reference"
  cosign verify "$reference" \
    --certificate-identity "$identity" \
    --certificate-oidc-issuer "$issuer" \
    --output json > "$out/signatures/${component}-cosign-verify.json"

  jq --arg name "$component" \
     --arg reference "$reference" \
     --arg digest "$digest" \
     --arg identity "$identity" \
     --arg issuer "$issuer" \
     --arg verification_path "signatures/${component}-cosign-verify.json" \
     '.images[$name] = {
        reference: $reference,
        digest: $digest,
        signature_exists: true,
        signature_verified: true,
        certificate_identity: $identity,
        certificate_oidc_issuer: $issuer,
        verification_path: $verification_path
      }' "$out/signature-results.json" > "$out/signature-results.tmp"
  mv "$out/signature-results.tmp" "$out/signature-results.json"
done

python3 scripts/cloud/New-G81SignedReleaseManifest.py \
  --images-evidence "$G81_IMAGES_EVIDENCE" \
  --signature-results "$out/signature-results.json" \
  --output "$out/release-manifest.json" \
  --repository "$GITHUB_REPOSITORY" \
  --source-commit "$GITHUB_SHA" \
  --build-run-id "$GITHUB_RUN_ID" \
  --platform-project "$GCP_PLATFORM_PROJECT_ID" \
  --engineering-run-id "$ENGINEERING_RUN_ID" \
  --security-run-id "$SECURITY_RUN_ID" \
  --policy-run-id "$POLICY_RUN_ID" \
  --expected-identity "$identity"

python3 scripts/cloud/Test-G81ReleaseManifest.py "$out/release-manifest.json"
find "$out" -type f ! -name checksums.sha256 ! -name checksums.sha256.tmp -print0 |
  sort -z |
  xargs -0 sha256sum > "$out/checksums.sha256.tmp"
mv "$out/checksums.sha256.tmp" "$out/checksums.sha256"
