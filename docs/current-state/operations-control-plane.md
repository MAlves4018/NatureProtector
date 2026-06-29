---
id: NP-CURRENT-OPS
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Operations Control Plane

## Purpose

The Operations Control Plane turns the Web UI into an auditable engineering console without turning the application server into an unrestricted shell.

```text
UI request
 -> server-side capability check
 -> closed operation catalog
 -> confirmation and/or approval
 -> operation record and timeline
 -> authorised dispatcher
 -> GitHub Actions / deployment / cloud runner
 -> callback, artifacts and hashes
 -> evidence shown in the UI
```

## Security invariants

- The browser cannot provide arbitrary shell, Terraform or `gcloud` commands.
- Provider credentials and callback secrets remain server/runner side.
- A UI button is not an authorisation boundary; policies are enforced in the backend.
- High-risk operations use exact confirmation phrases.
- Production and destroy require separate approval roles and immutable plan/release evidence.
- A succeeded job without hashed artifacts is not automatically classified as fully proved.

## Categories

### Quality

Closed suites include frontend, backend, architecture, security, Playwright, accessibility, mutation, Terraform/cloud static checks and the aggregated quality profile.

### Evidence

Profiles include static, quality, full plan and full execution. Full execution requires confirmation and approval.

### Deployment

Staging plan, deploy and rollback are represented. Production promotion exists as a controlled operation; standalone production plan and rollback remain blocked where no authoritative workflow exists.

### Cloud

Open/close staging are represented. Inventory, costs, smoke and destroy remain blocked until their input contracts, immutable plans and authoritative workflows are qualified.

## Evidence level

An operation records provider, provider reference, workflow, plan hash, timeline, artifacts, approvals and limitations. The system distinguishes `IMPLEMENTED_NOT_PROVED`, `NOT_PROVED` and proof backed by reported hashed artifacts.
