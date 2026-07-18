# Architecture diagram portfolio

This page is the canonical navigation point for the current architecture diagram portfolio declared by `docs/documentation-manifest.yml`.

## Status

Status: RESTORED_NOT_VISUALLY_VALIDATED.

The manifest declares a generated/current diagram portfolio under `architecture/diagrams/current/*`. After the M3B restore, the canonical rendered PNG/SVG files, source DOT files, sidecar JSON files, contact sheet and diagram register are present in this working tree.

This page is a navigation target for the restored canonical portfolio. Presence in the repository is not evidence that the diagrams have been visually reviewed, fact-checked for report use or externally validated.

## Portfolio navigation

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

## D01-D08 reconciliation

| Overlay diagram | Decision | Existing portfolio mapping | Status | Note |
| --- | --- | --- | --- | --- |
| D01 logical architecture | REUSE_EXISTING | `system-context`, `container-architecture` | RESTORED_NOT_VISUALLY_VALIDATED | Do not copy overlay directly; map to the restored canonical portfolio. |
| D02 runtime risk pipeline | REUSE_EXISTING | `runtime-risk-pipeline` | RESTORED_NOT_VISUALLY_VALIDATED | Prefer the existing portfolio concept over a parallel tree. |
| D03 run/simulation cycle | DOCS_ONLY | `operations-lifecycle`, run-orchestrator docs | RESTORED_NOT_VISUALLY_VALIDATED | Add as a new portfolio item only if later review proves no equivalent exists. |
| D04 messages/retries/quarantine | APPENDIX_ONLY | retry/quarantine implementation diagrams | RESTORED_NOT_VISUALLY_VALIDATED | Keep as technical appendix or supporting docs. |
| D05 persistence/source-of-truth | APPENDIX_ONLY | persistence and data-provenance diagrams | RESTORED_NOT_VISUALLY_VALIDATED | PostgreSQL remains the durable operational source of truth; UI/Grafana/API are not source-of-truth layers. |
| D06 provenance/authority/limits | APPENDIX_ONLY | `data-provenance-authority` | RESTORED_NOT_VISUALLY_VALIDATED | Useful as a guardrail diagram after visual/factual review. |
| D07 cloud/deployment | APPENDIX_ONLY | `deployment-and-promotion` | CONFIGURED_NOT_EXECUTED | Must not be presented as proved staging or production execution. |
| D08 evidence/claims maturity | REUSE_EXISTING | `claims-evidence-maturity` | RESTORED_NOT_VISUALLY_VALIDATED | Strong candidate for report use only after visual review and fact-checking. |

## Restored portfolio assets to verify

The restored portfolio includes, at minimum:

- `contact-sheet.png`
- `diagram-register.csv`
- `render/*.png`
- `render/*.svg`
- `sidecars/*.json`
- `src/*.dot`

Treat these files as restored assets, not as fully reviewed report figures, until a visual smoke check and factual review are recorded.

Do not infer production readiness, scientific validation or official alert authority from these diagrams. They are documentation aids and must remain subordinate to repository code, configuration and explicitly captured evidence.
