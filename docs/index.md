---
id: NP-DOC-INDEX
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: repository code and configuration
last_verified_against: NatureProtector repository snapshot 2026-07-22
last_verified_at: 2026-07-22
review_triggers: code, workflow, role, environment or evidence changes
---

# NatureProtector Documentation Portal

This portal separates the **current factual state** from history, plans, evidence and generated reference material. When a current document conflicts with an older note, the current document and the referenced code/configuration prevail.

## Start here

- [Final delivery status](current-state/final-delivery-status.md)
- [Functional capability catalog](reference/functional-capability-catalog.md)
- [Operations Control Plane](reference/operation-catalog.md)
- [Roles, capabilities and UI journeys](current-state/roles-capabilities-and-ui.md)
- [Quality, evidence and testing](testing/validation-gates.md)
- [Cloud and deployment](runtime-developer-control.md)
- [Data, risk and scientific boundaries](reference/scenario-acceptance-invariants.md)
- [Limitations and open gates](current-state/final-delivery-status.md)
- [Architecture diagram portfolio](architecture/diagrams/current/README.md)
- [Complete study compendium](study/NatureProtector-Complete-Study-Compendium.md)
- [Functional capability catalog](reference/functional-capability-catalog.md)
- [Functional traceability matrix](reference/functional-traceability-matrix.csv)
- [Scenario acceptance invariants](reference/scenario-acceptance-invariants.md)
- [Current API endpoint catalog](reference/generated/api-endpoint-catalog.csv)
- [Current UI route/capability matrix](reference/generated/ui-route-capability-matrix.csv)
- [Phase 1 functional audit](current-state/phase-1-functional-audit-2026-07-22.md)
- [Final acceptance runner](testing/final-acceptance-runner.md)
- [Phase 2 acceptance orchestration](current-state/phase-2-acceptance-orchestration-2026-07-22.md)

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
