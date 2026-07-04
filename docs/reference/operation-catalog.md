---
id: NP-REF-OPS
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: OperationCatalog.cs
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# Reference: Engineering Operation Catalog

The generated catalog in [generated/operation-catalog.csv](generated/operation-catalog.csv) is derived from `OperationCatalog.cs`.

An `implemented` operation has a defined dispatcher path; it is not automatically runtime-proved. Entries whose availability begins with `blocked-` are deliberately unavailable until the documented authority or input contract exists.
