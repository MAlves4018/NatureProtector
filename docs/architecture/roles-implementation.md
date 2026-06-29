---
id: NP-ARCH-ROLES
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: OperationCapabilities.cs, controllers and UI
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Current Roles and Authorisation Implementation

This document supersedes the earlier three-role description.

## Enforcement

The backend registers one policy per capability through `OperationAuthorization.Configure`. `OperationCapabilityAuthorizationHandler` resolves the authenticated user's role claims through `OperationRoleCatalog`. Controllers require the relevant policies. The UI consumes the evaluated profile for navigation but cannot grant itself authority.

## Current roles

- `Pipeline` - pipeline, risk, quality and evidence reading.
- `Sim` - simulation and related evidence.
- `QA` - quality suites and evidence campaigns.
- `Operations` - staging deployment/cloud operations.
- `ReleaseApprover` - production, rollback, destroy approval authority.
- `Admin` - application administration and users/roles, without automatic production/destroy authority.

## Identity persistence

Users, roles and assignments are stored in PostgreSQL under the user/control model. Tokens carry identity and role claims. JWT and Backoffice configuration are validated at startup, with development-only values rejected outside Development.

## UI

The current UI includes Mission Control, Quality Runs, Evidence Explorer, Deployments, Cloud Resources, Approvals and User/Role Administration in addition to simulation, runs, risk and pipeline views.

See [Roles, capabilities and UI journeys](../current-state/roles-capabilities-and-ui.md) and the generated [role-capability matrix](../reference/generated/role-capability-matrix.csv).
