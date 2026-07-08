# Cloud Safety Gates

Cloud readiness uses explicit gates. Advancing one gate does not imply approval
for the next gate.

## Gates

- `CLOUD_PREFLIGHT_READY`: static/read-only checks and inventories are complete.
- `CLOUD_PLAN_READY`: a reviewed plan exists and no apply/deploy has run.
- `CLOUD_BOOTSTRAP_APPROVED`: owner approves the exact bootstrap command set.
- `CLOUD_DEPLOY_APPROVED`: owner approves the exact deploy command set.
- `CLOUD_SMOKE_PASS`: deployed staging URL passed cloud smoke.
- `CLOUD_PRODUCTION_APPROVED`: production is approved only after staging smoke.

## Hard Blocks

The following commands require separate explicit approval:

- `terraform apply`;
- `terraform destroy`;
- `docker push`;
- `gcloud projects create`;
- `gcloud billing projects link`;
- `gcloud services enable`;
- `gcloud deploy releases create`;
- `gcloud deploy releases promote`;
- `kubectl apply`;
- any `Remove-*`, teardown or cleanup of cloud resources.

## Secret Rules

Secret payloads do not go to git, manifests, logs or evidence. Use Secret
Manager references, versions and checksums where needed. Local development
defaults such as `admin123` are not cloud credentials.
