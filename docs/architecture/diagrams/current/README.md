---
id: NP-DIAGRAM-PORTFOLIO
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: Graphviz sources and sidecars
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---
# Current Diagram Portfolio

- [NatureProtector system context](render/system-context.svg) - [sidecar](sidecars/system-context.json)
- [Current logical/container architecture](render/container-architecture.svg) - [sidecar](sidecars/container-architecture.json)
- [Runtime reading, quality and risk pipeline](render/runtime-risk-pipeline.svg) - [sidecar](sidecars/runtime-risk-pipeline.json)
- [Auditable engineering operation lifecycle](render/operations-lifecycle.svg) - [sidecar](sidecars/operations-lifecycle.json)
- [Quality execution and evidence promotion](render/quality-evidence-flow.svg) - [sidecar](sidecars/quality-evidence-flow.json)
- [Data provenance and authority boundaries](render/data-provenance-authority.svg) - [sidecar](sidecars/data-provenance-authority.json)
- [Security and trust boundaries](render/security-trust-boundaries.svg) - [sidecar](sidecars/security-trust-boundaries.json)
- [Immutable release, staging and production promotion](render/deployment-and-promotion.svg) - [sidecar](sidecars/deployment-and-promotion.json)
- [Roles, capabilities and UI journeys](render/roles-ui-journeys.svg) - [sidecar](sidecars/roles-ui-journeys.json)
- [Claims, evidence and maturity ladder](render/claims-evidence-maturity.svg) - [sidecar](sidecars/claims-evidence-maturity.json)

## Rules

Each promoted diagram has a source file, SVG and PNG render, presentation variant and JSON sidecar. The sidecar states authority, evidence level, limitations and accessible descriptions. Report and presentation consumers should reference these promoted renders rather than old screenshots labelled as current.
