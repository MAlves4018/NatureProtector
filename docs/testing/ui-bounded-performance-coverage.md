---
id: NP-TEST-UI-BOUNDED-PERFORMANCE
status: CURRENT
owner: Miguel Alves
audience: engineering, QA, delivery
source_of_truth: scripts/acceptance/Invoke-NP-UiPerformanceCoverage.ps1, scripts/acceptance/verify_ui_performance_coverage.py, scripts/acceptance/verify_rate_limit_contract.py, webUI/e2e/live-role-journeys.spec.ts and config/acceptance/ui-performance-coverage.json
last_verified_against: NatureProtector Phase 4 UI and bounded performance implementation
last_verified_at: 2026-07-22
review_triggers: UI route, role, capability, authentication limiter, Playwright, performance workload or evidence-contract changes
---

# UI and bounded performance coverage

This stage closes the UI and local bounded-performance gaps left after the functional P0 campaign. It is selected only by the `Full` final-acceptance profile because it requires the complete frontend toolchain, local runtime, Docker infrastructure and the longer Calibration+B0 workload.

## Canonical commands

Through the final runner:

```powershell
.\scripts\acceptance\Invoke-NP-FinalAcceptance.ps1 `
  -Profile Full `
  -ExecuteControlledValidationP3 `
  -AcknowledgeNonProduction
```

Direct execution for focused diagnosis:

```powershell
.\scripts\acceptance\Invoke-NP-UiPerformanceCoverage.ps1
```

`-SkipBuild` is permitted only when the same unchanged workspace has already completed preparation successfully. `-KeepRuntime` deliberately prevents a passing verdict because shutdown cleanliness would remain unproved.

## Browser coverage

### Fixture suite

The existing `cockpit.spec.ts` suite runs against the deterministic frontend fixture in both desktop and narrow viewports. This proves navigation, capability-driven rendering, responsive behaviour and the stable frontend contract independently of backend availability.

### Live role suite

`live-role-journeys.spec.ts` starts against the official local runtime and uses the real API to create temporary identities for:

```text
Sim
Pipeline
QA
Operations
ReleaseApprover
```

The seeded administrator identity is used only as controlled campaign authority. For every configured role the suite proves:

- successful login through the real UI;
- capabilities returned by `/api/users-roles/me/capabilities`;
- presence of required capabilities and absence of forbidden capabilities;
- successful rendering of an allowed route;
- `Acesso negado` on a route outside that role's authority;
- zero unexpected HTTP 5xx responses;
- zero browser console or page errors;
- logout and cleanup of temporary users.

The public `/demo` surface is also checked without authentication and must not expose protected pipeline navigation.

## Accessibility

The fixture and live journeys use axe. The versioned contract blocks all violations whose impact is `critical`. The detailed violation payload is included in the Playwright failure output so a failure cannot be converted into a warning.

## Sensitive browser artefacts

The live suite enters administrator and temporary-role credentials. Therefore the wrapper sets `NP_UI_SENSITIVE_ACCEPTANCE=1`, which disables Playwright traces and videos for that live run. The fixture suite retains normal failure artefacts because it does not use real credentials. Screenshots remain failure-only; password inputs are masked by the browser.

## Authentication rate limiter

The stage sends invalid login attempts until the real authentication limiter returns `429`. It requires:

- at least one normal `401` before the limit is reached;
- status `429` at the configured boundary;
- `X-RateLimit-Policy: authentication`;
- a positive `Retry-After` value;
- matching ProblemDetails policy metadata;
- `/health/live` and `/health/ready` remaining available after the authentication limit is active.

This confirms both enforcement and the intended health-check bypass.

## Bounded HTTP workload

`run-http-workload.py` executes current API probes and the public `/demo` route with authenticated authority. The stage runs:

- `Calibration`, with at least 24 measured attempts;
- `B0`, with at least 160 measured attempts.

Both must complete successfully and remain within the versioned local p95 threshold. The route formerly named `/ui-v2` is not part of the current application and is no longer probed.

## Bounded full-system workload

`run-system-capacity-workload.ps1` runs `Calibration` first and passes its evidence directory into `B0`. The closed verifier requires, for both profiles:

- terminal status `Completed`;
- the configured number of successful runs;
- zero failed runs;
- zero rejected, quarantined or lost events;
- accepted readings equal expected events;
- risk assessments equal expected events;
- final queue depth equal zero;
- elapsed and backlog-drain p95 within the local thresholds.

These are bounded local acceptance thresholds. They do **not** establish production capacity, production SLO compliance, horizontal scalability or scientific performance claims.

## Output contract

Each direct run writes below:

```text
artifacts/ui-performance-coverage/<UTC-run-id>/
```

The root contains normalized `summary.json`, `acceptance-result.json`, `tests.csv`, `commands.csv`, `blockers.csv`, `SUMMARY.md` and `hashes.sha256`, plus:

```text
ui/fixture/
ui/live/
rate-limit/
performance/http/Calibration/
performance/http/B0/
performance/system/Calibration/
performance/system/B0/
verification/
shutdown/
logs/
```

The native result is exactly one of:

```text
UI_AND_BOUNDED_PERFORMANCE_PASS
UI_AND_BOUNDED_PERFORMANCE_FAIL
UI_AND_BOUNDED_PERFORMANCE_BLOCKED
```

The parent runner independently checks process exit code and delegated result agreement.

## Current proof boundary

Phase 4 implements the live browser, rate-limit and bounded-performance campaign and validates its static and synthetic contracts. The analysis environment used to build this phase does not provide PowerShell, .NET or Docker, so no new live `Full` campaign is claimed. Final delivery proof requires one current `Full` run in the supported local environment, with every selected stage passing in the same campaign.
