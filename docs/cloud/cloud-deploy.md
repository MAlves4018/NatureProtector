# Cloud Deploy Preflight

Status: `CONFIGURED_NOT_EXECUTED`.

The local freeze candidate passed local validation, but no cloud deploy has
been executed. Cloud deployment remains a future, separately approved operation.

## Expected Future Entrypoints

The intended operator-facing shape is:

```powershell
.\scripts\np.ps1 cloud doctor -Environment staging
.\scripts\np.ps1 cloud plan -Environment staging
.\scripts\np.ps1 cloud bootstrap -Environment staging
.\scripts\np.ps1 cloud deploy -Environment staging
.\scripts\np.ps1 cloud smoke -Environment staging
.\scripts\np.ps1 cloud evidence -Environment staging
```

Current repo reality:

- `cloud doctor` exists as a safe local preflight.
- `cloud plan` is guarded and points to the existing `staging plan` path.
- `staging plan` is intended to run isolated Terraform planning with no apply,
  no refresh and no retained binary plan.
- `cloud up`, `staging open`, `staging deploy`, `staging close`, production
  promotion and teardown are mutating or potentially mutating paths and require
  explicit approval before use.

## Build And Release Surface

`scripts/cloud/Build-G81Release.sh` is the release authority for future cloud
images. It builds and pushes by digest to Artifact Registry and performs
signature/SBOM/provenance checks. It must not be run in preflight unless the
owner explicitly authorizes image push.

Expected release image components:

- `backoffice-api`;
- `prevention`;
- `simulator`;
- `postgres-migrations`;
- `postgres-bootstrap`;
- `frontend`;
- `functional-smoke`;
- `rabbitmq`;
- `otel-collector`;
- `distributed-probe`;
- `cloud-deploy-verifier`.

## Deployment Safety

Staging must pass before production is considered. Production commands are
blocked by configuration and require a separate written approval after staging
cloud smoke passes.
