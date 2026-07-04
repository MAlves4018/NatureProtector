---
id: NP-CURRENT-ROLES
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Roles, Capabilities and UI Journeys

## Authority model

Roles are mapped to capabilities on the server. The frontend requests the evaluated profile and uses it for navigation and affordances; server policies remain authoritative.

## Roles

| Role | Main purpose | Important limits |
|---|---|---|
| Pipeline | Read pipeline, quality and evidence | No simulation or cloud mutation |
| Sim | Execute simulations and inspect related evidence | No engineering deployment authority |
| QA | Execute quality suites and evidence campaigns | No cloud mutation |
| Operations | Read cloud/deployment state and operate staging | No automatic production authority |
| ReleaseApprover | Review/approve production, rollback and destroy gates | Does not imply user administration |
| Admin | Manage users/roles and application administration | Does not automatically receive production or destroy authority |

A user may hold more than one role, but confirmation and approval steps remain explicit.

## UI task surfaces

- Public overview and data context.
- Mission Control and release-readiness narrative.
- Risk, pipeline, runs and simulation.
- Quality Runs.
- Evidence Explorer and comparisons.
- Deployments.
- Cloud Resources.
- Approvals.
- User and Role Administration.
- Experimental/P3 surfaces where authorised.

## Separation of powers

The deliberate choice `Admin != production deploy/destroy` avoids coupling identity administration with infrastructure authority. In a one-person academic project the same person may possess multiple roles, but the system still records the distinct decision stages.

## Current limitations

Capability evaluation is implemented. Remote execution remains dependent on configured GitHub/cloud identities and callbacks. Some UI operations are visible as blocked because the repository intentionally lacks a qualified authoritative workflow for them.
