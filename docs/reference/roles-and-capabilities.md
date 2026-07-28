---
id: NP-REF-ROLES
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: OperationCapabilities.cs and OperationRoleCatalog
last_verified_against: NatureProtector repository snapshot 2026-07-22
last_verified_at: 2026-07-22
review_triggers: code, workflow, role, environment or evidence changes
---

# Reference: Roles and Capabilities

The generated matrix in [generated/role-capability-matrix.csv](generated/role-capability-matrix.csv) is derived from `OperationCapabilities.cs`. The backend policy names use the lower-case capability strings shown in that file.

Important rule: frontend capability fallback supports navigation resilience but cannot grant backend authority.
