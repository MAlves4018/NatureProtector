---
id: NP-CURRENT-CLOUD
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Cloud and Deployment

## Intended topology

The repository contains a cloud/deployment implementation and configuration for isolated GCP environments, Artifact Registry, GKE/Autopilot, Cloud SQL/PostgreSQL integration, RabbitMQ operators, KEDA, cert-manager, Cloud Deploy and GitHub Actions with Workload Identity Federation.

Status: CONFIGURED_NOT_EXECUTED for static documentation claims unless paired with a captured cloud run, smoke result and evidence bundle from the owner's environment. The checked-in docs must not be read as proof of a deployed staging or production system by themselves.

Staging must be proved before production. Production promotion is based on an immutable release/digest and explicit evidence, not a rebuild of an arbitrary branch.

## Delivery chain

```text
commit/reference
 -> validation and qualification
 -> immutable release package/image
 -> checksums, SBOM and provenance/attestation
 -> staging deployment
 -> smoke and qualification evidence
 -> approval
 -> production promotion
 -> post-deploy observation and rollback readiness
```

## Current factual status

- Infrastructure and workflow implementation: present in the repository.
- Signed-release action pin remediation: present in source.
- Signed release for the final current head: not proved by the supplied executor package alone.
- Staging deployment: not proved in the supplied artifacts.
- Production: not deployed/proved.
- Destroy: authorised in principle by the owner but intentionally disabled in the UI until an immutable destroy-plan workflow and approval chain exist.

## Destroy safety contract

Before any destroy operation the system must identify the environment, project IDs, Terraform state, shared-resource risk, pre-destroy inventory and exact plan hash. Execution must apply the approved plan, verify remaining resources and preserve final evidence/cost information.

## Claim boundary

Repository declarations may be shown as `DeclaredNotObserved`. A live resource, cost or health claim requires an authenticated inventory operation and captured evidence.
