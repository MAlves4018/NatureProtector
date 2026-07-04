---
id: NP-DOC-INDEX
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector Unified Operations Control Plane 2026-06-28
last_verified_at: 2026-06-28
review_triggers: code, workflow, role, environment or evidence changes
---

# NatureProtector Documentation Portal

This portal separates the **current factual state** from history, plans, evidence and generated reference material. When a current document conflicts with an older note, the current document and the referenced code/configuration prevail.

## Start here

- [Current project state](current-state/project-state.md)
- [Architecture and runtime](current-state/architecture-and-runtime.md)
- [Operations Control Plane](current-state/operations-control-plane.md)
- [Roles, capabilities and UI journeys](current-state/roles-capabilities-and-ui.md)
- [Quality, evidence and testing](current-state/quality-evidence-and-testing.md)
- [Cloud and deployment](current-state/cloud-and-deployment.md)
- [Data, risk and scientific boundaries](current-state/data-risk-and-scientific-boundaries.md)
- [Limitations and open gates](current-state/limitations-and-open-gates.md)
- [Architecture diagram portfolio](architecture/diagrams/current/README.md)
- [Complete study compendium](study/NatureProtector-Complete-Study-Compendium.md)

## Documentation modes

| Mode | Purpose | Typical content |
|---|---|---|
| Tutorial | Learn through a guided path | local first run, first simulation, first evidence review |
| How-to | Complete a concrete task | deploy staging, run a quality suite, inspect a failure |
| Reference | Look up facts and contracts | endpoints, roles, events, schemas, operation catalog |
| Explanation | Understand design and trade-offs | architecture, evidence model, scientific boundaries |

## Authority labels

- `CURRENT`: verified against the current repository snapshot.
- `HISTORICAL`: preserved evidence of an earlier state.
- `PLANNED`: intended but not implemented or not proved.
- `GENERATED`: derived from code/configuration and reproducible.
- `EVIDENCE`: execution output or audit material; not automatically a current claim.
- `EXPERIMENTAL`: exploratory and not an authority for production claims.
- `SUPERSEDED`: replaced by another document.

The machine-readable authority map is [documentation-manifest.yml](documentation-manifest.yml).
