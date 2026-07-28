---
id: NP-EVIDENCE-PHASE-4-UI-PERFORMANCE
status: EVIDENCE
owner: Miguel Alves
audience: engineering, QA, delivery
source_of_truth: Phase 4 repository snapshot
last_verified_against: NatureProtector Phase 3 P0 runtime coverage
last_verified_at: 2026-07-22
---

# Phase 4 — UI and bounded performance coverage

## Objective

Close the remaining UI, accessibility, rate-limiting and local bounded-performance gaps and integrate them as a fail-closed stage of the canonical `Full` acceptance profile.

## Implemented

- Added `scripts/acceptance/Invoke-NP-UiPerformanceCoverage.ps1`.
- Added the side-effect-free closed verifier `scripts/acceptance/verify_ui_performance_coverage.py`.
- Added the live rate-limit verifier `scripts/acceptance/verify_rate_limit_contract.py`.
- Added `config/acceptance/ui-performance-coverage.json` as the versioned role, accessibility, limiter and performance contract.
- Added `webUI/e2e/live-role-journeys.spec.ts` with real temporary identities for `Sim`, `Pipeline`, `QA`, `Operations` and `ReleaseApprover`, plus administrator and public journeys.
- Added fixture browser execution at desktop and narrow viewports.
- Added critical axe checks to live role journeys.
- Added real authentication rate-limit proof and health endpoint bypass checks.
- Added bounded HTTP `Calibration` and `B0` workloads.
- Added bounded full-system `Calibration` and `B0` workloads with exact event accounting and final queue drainage.
- Replaced the removed `/ui-v2` performance probe with the current public route `/demo`.
- Disabled traces and videos for the credential-bearing live Playwright campaign.
- Integrated `ui-performance-coverage` only into the `Full` profile.

## Fail-closed behaviour demonstrated synthetically

The verifier accepts complete synthetic fixture/live, rate-limit, HTTP and system evidence and rejects a B0 result whose final queue depth is non-zero. The contract also requires current evidence paths and exact Calibration+B0 profile pairs.

## Proof boundary

This phase does not claim a new live execution. The construction environment has Python and Node, but not PowerShell, .NET or Docker. Frontend dependencies could not be installed in this environment, so the Playwright suite could not be compiled or executed here. Static repository audits, Python compilation and synthetic contract tests are the available proof for this snapshot. A current `Full` run remains mandatory before final delivery.
