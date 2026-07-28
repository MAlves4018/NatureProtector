---
id: NP-EVIDENCE-PHASE-2-ACCEPTANCE
status: EVIDENCE
owner: Miguel Alves
audience: engineering, QA, delivery
source_of_truth: Phase 2 repository snapshot
last_verified_against: NatureProtector Phase 1 Functional Catalogue
last_verified_at: 2026-07-22
---

# Phase 2 — Acceptance orchestration consolidation

## Objective

Create one safe and deterministic orchestration surface around the repository's existing quality, functional, negative, recovery, temporal, autoscaling and performance harnesses.

This phase changes validation tooling, acceptance configuration, documentation and one frontend route-registry inconsistency. It does not change domain calculations, persistence schemas, runtime APIs or simulation behaviour.

## Implemented

- Added `scripts/acceptance/Invoke-NP-FinalAcceptance.ps1`.
- Added the shared module `scripts/acceptance/modules/Acceptance.Common.psm1`.
- Added `config/acceptance/final-acceptance.json` with closed profiles, stages, prerequisites and timeouts.
- Standardized campaign states and exit codes.
- Integrated the existing B/C functional harness and a guarded P3 acceptance wrapper that executes query pack 11 against the exact generated `runLabel`.
- Integrated reset/recovery, multi-replica, autoscaling and bounded performance harnesses.
- Moved the three advanced matrix defaults from an external `NatureProtector.brain` path into run-scoped repository artifacts.
- Added path guards that refuse matrix output outside `artifacts`.
- Added machine-readable `acceptance-result.json` to the functional, P3 and advanced matrix harnesses; the parent runner rejects status/exit-code disagreement.
- Added a non-mutating `--check` mode to the generated reference catalogue tool.
- Registered `/quality` in `UI_PAGE_REGISTRY` with `quality.read` and added regression coverage.

## Acceptance profiles

- `Static`: current generated catalogues and maintained static audits.
- `Smoke`: static gates, quick workspace validation and functional smoke.
- `Functional`: static gates, full test profile, B/C, P3 and reset/recovery.
- `Full`: functional plus multi-replica, autoscaling and performance smoke.

## Safety rules

- P3 requires explicit execution and non-production acknowledgement switches, an environment token, `psql` and PostgreSQL connectivity from `.env` or an explicit override. It passes only after exact-run reconciliation of all expected executable and blocked cases.
- Output is constrained to `artifacts`.
- Existing output is not silently deleted.
- Missing prerequisites are classified as `BLOCKED_PREREQUISITE`, not `PASS` or a generic failure.
- Runner start failures and timeouts are classified as `HARNESS_ERROR`.
- Plan mode does not create an execution claim.

## Remaining implementation backlog

Phase 2 consolidates existing coverage; it does not yet add all missing end-to-end assertions identified in Phase 1. The next phase must expand runtime coverage for every degradation profile, RBAC mutation flows, diagnostics, alerts and further endpoint-level journeys.

## Validation boundary

The current analysis environment has Python but not PowerShell, .NET or Docker. Static contracts and all Python-maintained audits can be executed here; Windows/.NET/Docker runtime stages remain to be executed on the project's supported local environment before delivery.
