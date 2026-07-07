# Cloud Bootstrap Preflight

Status: `CONFIGURED_NOT_EXECUTED`.

This page documents the safe bootstrap boundary for the freeze candidate. It
does not authorize project creation, billing changes, API enablement, Terraform
apply, deploy, image push, or resource deletion.

## Safe Commands

These commands are read-only or local/static checks:

```powershell
.\scripts\np.ps1 cloud doctor -Environment staging
terraform fmt -check -recursive infra/gcp/terraform
terraform -chdir=infra/gcp/terraform/g8-1-state-bootstrap validate
terraform -chdir=infra/gcp/terraform/g8-1-platform validate
terraform -chdir=infra/gcp/terraform/g8-1-environment validate
```

`cloud doctor` checks local tool availability and records evidence. It must not
create or mutate GCP resources.

## Bootstrap Inputs

The expected bootstrap contract is represented by:

- `infra/gcp/contracts/g10-2-bootstrap-input.example.json`
- `deploy/environments/common.json`
- `deploy/environments/staging.json`
- `deploy/environments/production.json`

Known static values:

- primary region: `europe-southwest1`;
- current single project in checked-in staging config: `natureprotector-500518`;
- example future isolated projects: `np-platform-migkxl-202606`,
  `np-staging-migkxl-202606`, `np-production-migkxl-202606`;
- Terraform state bucket example: `np-tfstate-migkxl-202606`;
- evidence bucket example: `np-evidence-migkxl-202606`.

Billing account IDs and secret payloads must never be committed.

## Requires Separate Approval

The following are blocked until a later phase with explicit approval:

- `terraform apply`;
- `terraform destroy`;
- `gcloud projects create`;
- `gcloud billing projects link`;
- `gcloud services enable`;
- Secret Manager writes;
- Artifact Registry writes;
- Cloud Deploy release creation/promotion;
- image push;
- staging or production teardown.

Production remains locked until staging has passed cloud smoke and the owner
approves production separately.
