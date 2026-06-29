# Unified Operations Control Plane

## Purpose

The control plane turns the UI into an auditable engineering console without turning the Backoffice API into a shell, `gcloud` or Terraform terminal.

```text
UI
  → server-side capability policy
  → closed operation catalog
  → atomic operation store
  → confirmation / approval gate
  → safe dispatcher
  → existing GitHub Actions / cloud authority
  → callback with status and hashed artifact references
  → evidence and timeline in the UI
```

The existing deployment workflows remain the source of truth. The new wrappers delegate to them and do not duplicate deployment logic.

## Surfaces

| Surface | Purpose |
| --- | --- |
| Mission Control | Lifecycle, recent operations and derived release readiness |
| Quality Runs | Closed suites and execution history |
| Evidence Explorer | Campaigns, artifacts, hashes and comparison |
| Deployments | Staging and production operation catalog |
| Cloud Resources | Declared environment inventory and bounded actions |
| Approvals | Separate approval decisions for high-risk operations |
| Users & Roles | Users, roles and separation-of-duties narrative |

## Operation record

Every operation preserves the requester, role/capability snapshot, commit/reference, environment, sanitized inputs, exact confirmation, steps, approvals, provider reference, artifacts, evidence level and limitations.

The file store is intentionally outside the repository by default and uses atomic replacement. It can later be replaced by PostgreSQL through `IOperationStore` without changing the API contract.

## Evidence semantics

- `DEMONSTRATION_ONLY`: local Development simulation; no remote work occurred.
- `IMPLEMENTED_NOT_PROVED`: dispatch or implementation exists, but no qualifying execution proof was ingested.
- `SUCCEEDED_WITHOUT_VERIFIABLE_ARTIFACT_PROOF`: a provider reported success but artifact references/hashes were incomplete.
- `PROVED_BY_HASHED_REPORTED_ARTIFACTS`: the callback reported at least one referenced artifact and every artifact had a valid SHA-256.

A callback is provider-reported evidence, not independent attestation. Signed release attestations and staging qualification remain separate gates.

## Environment inventory

The Cloud Resources page reads `deploy/environments/*.json` and related repository paths. It is always labelled `DeclaredNotObserved`. Live health, costs, drift and resource existence require a dedicated dispatched inventory operation and are not inferred from configuration files.
