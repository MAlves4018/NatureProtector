# Google Cloud production delivery

This directory contains the single canonical cloud architecture selected by G9. No GCP resource is created by repository checkout or by the default local workflow.

## Active structure

- `terraform/g8-1-state-bootstrap` — remote Terraform state foundation;
- `terraform/g8-1-platform` — Artifact Registry, WIF, Cloud Deploy and evidence controls;
- `terraform/g8-1-environment` — reusable staging/production environment root;
- `kubernetes/g8-1` — RabbitMQ quorum, Prevention, KEDA, OTel and network policies;
- `cloud-deploy/g8-1` — API, frontend and Prevention delivery definitions;
- `contracts/g8-1-*` — immutable release manifest;
- `contracts/g8-2-*` — qualification, evidence, review and authorization contracts;
- `production/g8-1-*` — production architecture policies;
- `qualification/g8-2-*` — runtime qualification policy.

## Environment topology

```text
platform project
  -> registry, WIF, Cloud Deploy, release/evidence control plane
staging project
  -> protected edge, Cloud Run, GKE, RabbitMQ, Cloud SQL and observability
  -> first bounded deployment may use the explicit non-production qualification profile
production project
  -> equivalent isolated production data plane
```

All three projects are new NatureProtector projects created only after G10 and owner integration. They may be linked to the owner-approved academic billing account, but no NatureProtector workload is deployed into the CN project.

## Guardrails

- no service-account keys;
- no Owner or Editor roles;
- no secret payloads in Git, manifests or evidence;
- images are promoted by digest;
- staging and production use the same release manifest;
- Cloud Armor and application rate limiting protect the edge;
- Cloud SQL uses HA/PITR and separate migration/runtime identities;
- production authorization never deploys production;
- resource creation is opt-in and disabled in example variables.

See `docs/implementation/cloud/g8-1-cloud-production-architecture-cd-hardening.md`, `docs/implementation/cloud/g8-2-qualification-evidence-integrity.md` and `docs/implementation/cloud/g9-repository-convergence.md`.

## Local/cloud parity contract

- PostgreSQL uses the same `POSTGRES_*` contract locally and in cloud; cloud sets `POSTGRES_REQUIRE_EXPLICIT=true`.
- The first cloud qualification profile explicitly selects `InfluxDb__Enabled=false`, activating the existing NoOp adapter. Local InfluxDB remains available.
- Runtime evidence stays filesystem-backed only in Development/Evidence. Cloud qualification relies on the signed G8.2 evidence pipeline until a GCS runtime sink is separately approved.
- `scripts/cloud/Test-LocalCloudConfigurationContract.py` blocks drift between host settings and deployable manifests.

## G10.2 executable preflight

Before any project or Terraform creation, use the schema-controlled G10.2 input
and the read-only preflight described in
`docs/operations/g10-2-preflight-bootstrap-runbook.md`.

The preflight never executes `terraform apply`, never enables APIs and never
creates a data plane. `Invoke-G102ProjectBootstrap.ps1` is a separate,
owner-confirmed step limited to empty isolated projects and billing links.

## G10.3 owner bootstrap safety

After the G10.2 read-only preflight, use the G10.3 controls to:

- prove the empty projects and billing links with a read-only inventory;
- create schema-controlled Cloud Billing alerts only after exact confirmation;
- generate state/platform Terraform inputs with every creation flag disabled;
- keep the state foundation, delivery control plane and data plane uncreated until
  their plans and costs are reviewed.

Runbook: `docs/operations/g10-3-owner-bootstrap-runbook.md`.

