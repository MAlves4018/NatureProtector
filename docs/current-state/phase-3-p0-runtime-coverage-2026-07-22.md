---
id: NP-EVIDENCE-PHASE-3-P0-RUNTIME
status: EVIDENCE
owner: Miguel Alves
audience: engineering, QA, delivery
source_of_truth: Phase 3 repository snapshot
last_verified_against: NatureProtector Phase 2 acceptance orchestration
last_verified_at: 2026-07-22
---

# Phase 3 — P0 runtime functional coverage

## Objective

Implement executable checks for the highest-priority functional gaps identified in the Phase 1 catalogue and integrate them into the Phase 2 final-acceptance runner without duplicating the existing B/C, P3 or reset/recovery harnesses.

## Implemented

- Added `scripts/acceptance/Invoke-NP-P0RuntimeCoverage.ps1`.
- Added the side-effect-free verifier `scripts/acceptance/verify_scenario_profile_matrix.py`.
- Added `config/acceptance/p0-runtime-coverage.json` as the versioned matrix of profiles, thresholds, roles, diagnostic semantics, alert expectations, observability requirements and shutdown rules.
- Added the `p0-runtime-coverage` stage to `Functional` and `Full` after B/C and before P3/reset-recovery.
- Implemented current-run evidence collection from the runtime API, PostgreSQL and Simulator logs.
- Implemented profile-specific checks for all 12 supported degradation profiles.
- Added a deterministic repeat for `missing-readings` and a saturation-producing supplemental run for `clipping/range`.
- Implemented real temporary user/role lifecycle and capability-driven allow/deny probes for five seeded non-admin roles.
- Implemented exact execution of all 28 runtime diagnostics, including current B and C prerequisites.
- Implemented high-risk alert transition and duplicate-active-alert checks.
- Implemented application and direct health checks for PostgreSQL, RabbitMQ, InfluxDB and Grafana, an authenticated run-scoped InfluxDB query, plus evidence catalogue/download validation.
- Implemented fail-closed shutdown verification and project-scoped cleanup.
- Added normalized results, CSV checks, an evidence manifest and SHA-256 hashes.

## Defects prevented during implementation

Static review found and corrected several harness defects before delivery:

- the cycle observation table name was initially plural instead of the actual singular table;
- the audit convergence loop initially referenced fields that belong to operation accounting, not the audit DTO;
- PostgreSQL enum values required explicit string mapping for retry assertions;
- P3-style duplicate/out-of-order proof required actual publisher log order, not only final counters;
- the diagnostic B/C comparison would lose scenario C after deterministic cleanup;
- observability checks initially allowed any status except two bad values instead of a closed allowlist;
- `-KeepRuntime` initially allowed a warning even though shutdown was unproved;
- output hashes initially lacked a dedicated evidence manifest.

## Validation completed in this environment

- Python verifier compilation: passed.
- Synthetic scenario matrix: passed.
- Negative synthetic cases for missing profiles, missing duplicate delivery and missing clipping saturation: failed closed as expected.
- Acceptance contract tests: 28/28 passed.
- Documentation validation: 212 Markdown files, 0 errors.
- Reference catalogues: 91 role/capability rows, 30 operations, 75 API endpoints, 25 UI routes and 28 runtime diagnostics.
- Configuration audit: 291/291 passed.
- Control-plane audit: 77/77 passed.
- Frontend audit: 50/50 passed.
- Workflow audit: 166/166 passed.
- Operations audit: 107/107 passed.
- Script audit: 364/364 passed.
- Final repository audit: 70/70 passed.
- Final delivery audit: 9/9 passed.
- Final-acceptance configuration contract: selected only in `Functional` and `Full`.
- Exact profile catalogue: 12/12.

## Proof boundary

This phase implements the runtime campaign but does not claim a new live execution. The analysis environment has Python but no `pwsh`, .NET SDK or Docker daemon. Final proof requires execution of:

```powershell
.\scripts\acceptance\Invoke-NP-FinalAcceptance.ps1 -Profile Functional
```

on the supported local environment, followed by correction and repetition until every selected stage passes in one current campaign.
