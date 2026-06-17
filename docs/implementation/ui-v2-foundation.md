# UI v2 foundation and core capability expansion

This page documents the UI v2 implementation delivered by M03 and expanded by M04/M05, with later browser-auth hardening. It is an implementation note, not a product claim.

UI v2 is an isolated prototype experience mounted at `/ui-v2`. It keeps the existing beta routes intact and does not change RabbitMQ contracts, public API projections, database schema/migrations, alert semantics, scoring, JWT claims or role names.

## Scope implemented

M03 added the first vertical slice:

- demo entry view that states the academic prototype boundary;
- contextual risk read-only view backed by the existing runtime summary API;
- Data Status Strip for origin, freshness, completeness, coverage, eligibility, provenance, continuity and limitations;
- Portuguese and English UI copy controlled client-side;
- contextual help through the help button and F1;
- accessibility-oriented component tests and a basic axe scan;
- route-level isolation so the beta UI remains available.

M04 expands that slice without adding new backend contracts:

- dynamic area selection from `GET /api/control/areas`, with requested/resolved state and no hardcoded area fallback;
- scenario selection from `GET /api/control/areas/{areaCode}/scenarios`;
- run selection and run context from `GET /api/control/simulation-runs`, `GET /api/control/runtime/runs/{runId}`, audit and timing endpoints;
- simulation request review showing requested/resolved configuration when returned by existing runtime run metadata;
- simulation execution UI that calls `POST /api/control/runtime/runs` only when the frontend capability profile allows it;
- integrated browser-functional help content, replacing the M03 repository-docs link that Vite did not serve as an app asset;
- backend authorization tests confirming `Pipeline` can read runtime summary but cannot start a runtime run, while `Sim` can start one.

M05 hardens the UI v2 technical surface without adding backend contracts:

- Pipeline/Observability view backed by existing runtime summary, run audit and run timing contracts;
- explicit `Not instrumented`, `Not confirmed`, `No evidence` and `Not available` states for fields such as queue backlog, broker health, publisher timestamps and per-event latency;
- QA view that separates test definition, execution, result, evidence reference and coverage scope;
- Evidence/Limitations view that lists available evidence and distinguishes supported claims from claims not supported by the artifact;
- proportional Admin view that documents sensitive actions and backend enforcement without exposing destructive reset, P3 run or diagnostic execution controls;
- P3 experimental view that keeps P3 separate from scoring, alert semantics, schema and the main simulator runtime;
- staging/demo readiness checklist that is explicit about browser-visible evidence versus handoff/runtime evidence;
- focused tests for the new adapters, capabilities, claims/absence states and route wiring.

The 2026-06-16 browser hardening adds Playwright coverage against the built UI artifact for Anonymous, Admin, Sim and Pipeline journeys, plus login/session/API failure states, degraded runtime summary states and authenticated evidence download. It also expands accessibility regression coverage for axe, skip link keyboard activation, F1 help dialog focus lifecycle, Escape/focus restore, dark mode, mobile viewport and reduced-motion media settings. A Vitest guardrail rejects browser app `console.*` statements whose messages include sensitive user, token or session terms. These browser tests use an HTTP fixture at the Playwright boundary; they validate UI behavior, token propagation, capability gating, sensitive-console regressions and accessibility regressions, not a live external identity store or WCAG certification.

The UI reads existing backend output through frontend adapters. It does not recalculate risk and does not reinterpret `Blocked` as risk score `0`.

## Runtime path

The route is registered in `webUI/src/app/App.tsx` as a lazy-loaded page:

```text
/ui-v2
```

The UI v2 files live under:

```text
webUI/src/app/ui-v2/
```

The current runtime data sources are existing API endpoints:

```text
GET  /api/control/areas
GET  /api/control/areas/{areaCode}/scenarios
GET  /api/control/runtime/summary?areaCode={areaCode}&recentMinutes=30
GET  /api/control/simulation-runs?areaCode={areaCode}&take=20
GET  /api/control/runtime/runs/{runId}
GET  /api/control/runtime/runs/{runId}/audit
GET  /api/control/runtime/runs/{runId}/timings
GET  /api/control/runtime/observability/health
GET  /api/control/runtime/observability/rabbitmq
GET  /api/control/runtime/observability/evidence
GET  /api/control/runtime/observability/evidence/{evidenceId}
GET  /api/dev/controlled-validation/p3
POST /api/control/runtime/runs
```

`GET /api/dev/controlled-validation/p3` is only queried by UI v2 when the current profile has `Sim` or `Admin`; otherwise the P3 surface reports availability as not confirmed for that profile/session.

No new backend endpoint was added for M03, M04, M05 or the 2026-06-16 browser-auth hardening.

## Output-context adapters

`outputContext.ts` builds a `UiV2RiskReadModel` from `RuntimeSummaryResponse`.

The adapter:

- prefers existing score component values when present;
- falls back to existing area operational projection values only for display context;
- hides the score when state is `blocked`, `no-data`, `loading`, `error` or `access-denied`;
- deduplicates repeated limitations from summary, score component and index comparison projections;
- exposes degraded states instead of hiding uncertainty.

`coreContext.ts` adds presentation models for M04:

- area requested/resolved status;
- selected scenario availability;
- selected run lifecycle, audit and timing context;
- simulation requested/resolved review.

`technicalSurfaces.ts` adds M05 presentation models for:

- Pipeline/Observability fields, each with source, timestamp, scope, state and limitation;
- QA suites and recorded execution metadata;
- evidence items and claim support boundaries;
- proportional administrative actions and sensitive-action availability;
- P3 experimental context;
- staging/demo readiness items.

These states are presentation states only. Scientific calibration, alert policy and eligibility semantics remain backend/domain concerns.

## Capabilities and authorization

`capabilities.ts` defines provisional client-side capabilities:

- `demo.read`
- `area.read`
- `risk.read`
- `pipeline.read`
- `run.read`
- `scenario.read`
- `simulation.read`
- `simulation.execute`
- `qa.read`
- `evidence.read`
- `limitations.read`
- `admin.read`
- `admin.execute`
- `p3.read`
- `data_context.read`
- `help.read`

Unsigned visitors and `Pipeline` get read-oriented UI capability. Existing `Admin` and `Sim` roles get `simulation.execute` in the frontend. Existing `Admin` also gets the proportional administration surface. M05 does not create new roles or claims.

This is frontend UX only. It is not a security boundary. The real write boundary remains the existing backend authorization on `POST /api/control/runtime/runs`, which allows `Sim,Admin` and denies `Pipeline`. Other sensitive backend actions such as runtime reset, runtime diagnostic execution and P3 run remain backend-protected and are not exposed as executable controls by M05.

## Boundaries

UI v2 must keep these boundaries visible:

- NatureProtector is an academic prototype, not an operational civil-protection system.
- Risk output is calculated pipeline output, not a direct observation.
- It is not an official alert and does not replace authorities.
- Candidate parameters and thresholds are not scientifically calibrated final values.
- FWI/KBDI/proxy context is contextual evidence, not official equivalence.
- Missing or blocked data must stay visible as degraded state, not be converted into low risk.

## Validation snapshot

Browser hardening validation on 2026-06-16:

- `npm test -- src/app/services/api.test.ts src/app/ui-v2/outputContext.test.ts`: passed, 9 tests.
- `npm run typecheck`: passed.
- `npm test`: passed, 43 frontend tests after the sensitive-console guardrail addition.
- `npm run test:coverage`: passed, 43 frontend tests after D7 lint fixes; all frontend line coverage `34.25%`, `app/ui-v2` line coverage `83.33%`.
- `npm run test:e2e`: passed, 18 Playwright tests against `npm run build` + `vite preview`.

The Playwright specs cover UI v2 role/capability behavior, error surfaces and selected accessibility regressions with bounded API fixtures. Backend authorization and JWT behavior remain covered by Backoffice API tests.

M05 local validation on 2026-06-14:

- `npm run typecheck`: passed.
- `npm test -- src/app/ui-v2 src/app/services/api.test.ts`: passed, 27 tests.
- `npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts`: passed.
  - all frontend line coverage: `30.72%`;
  - `app/ui-v2` line coverage: `84.12%`.
- `npm run build`: passed.
- `npm test`: passed, 30 tests.
- `dotnet test tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore --nologo -v minimal -m:1`: passed, 91 tests, with the then-known `NU1902` warning.
- `dotnet test NatureProtector.sln --no-restore --nologo -v minimal -m:1`: passed, 1182 tests, with the then-known `NU1902` warning.
- Security checks:
  - `npm audit --json` returned 3 high findings in the existing Vite/esbuild chain; no forced or major dependency fix was applied.
  - `dotnet list NatureProtector.sln package --vulnerable --include-transitive` returned the then-known moderate OpenTelemetry advisory; E2 validation on 2026-06-16 reports no vulnerable NuGet packages.
  - targeted UI v2 scan found only test fixture strings and technical labels, not new secrets.

M04 local validation on 2026-06-14:

- `npm run typecheck`: passed.
- `npm test -- src/app/ui-v2 src/app/services/api.test.ts`: passed, 20 tests.
- `npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts`: passed.
  - all frontend line coverage: `26.06%`;
  - `app/ui-v2` line coverage: `81.28%`.
- `npm run build`: passed.
- `dotnet test tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore --nologo -v minimal -m:1`: passed, 91 tests, with the then-known `NU1902` warning.
- Browser smoke against local Vite and Backoffice API:
  - login with existing admin credentials;
  - open `/ui-v2`;
  - select area from the real area catalog;
  - confirm requested/resolved area state;
  - open integrated help and confirm it has no local docs link;
  - open simulation view as Admin;
  - submit a minimal runtime request (`1` sensor, `1` cycle, `1` second interval, no wait);
  - observe `POST /api/control/runtime/runs` returning HTTP 200 and runtime status visible in the UI.

M03 local validation on 2026-06-14:

- `npm run typecheck`: passed.
- `npm test`: passed, 15 tests.
- `npm run test:coverage`: passed.
  - all frontend line coverage: `13.67%`;
  - `app/ui-v2` line coverage: `86.48%`.
- `npm run build`: passed.
- `dotnet test .\tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore --nologo -v minimal -m:1`: passed, 88 tests, with the then-known `NU1902` warning.
- Browser smoke against local Vite and Backoffice API confirmed login, `/ui-v2`, read-only risk, Data Status Strip, help drawer and PT/EN toggle.

## Known limitations

- The route is isolated and is not a replacement for the beta UI.
- Client-side capabilities do not enforce backend authorization.
- The M04 browser smoke submitted a minimal runtime request but did not wait for full simulator completion.
- The M04 browser smoke run remains local state and may affect latest-run ordering until a safe reset/rebaseline is explicitly chosen.
- The global frontend coverage remains low because most legacy views are still uncovered.
- The existing Vite/esbuild npm audit finding from M02 remains unresolved.
- Runtime backlog, broker health, publisher timestamps and full per-event latency are not instrumented through the current UI v2 contracts.
- The M05 Admin view is intentionally read-oriented; destructive reset and P3 execution are not exposed.
- The 2026-06-16 Playwright matrix uses an HTTP fixture; it is not evidence of a deployed multi-user environment, external validation or accessibility certification.
- P3 integration, final alert policy, beta removal, cutover, new backend contracts and scientific calibration remain out of scope.

## Next mission boundary

M05 deliberately does not implement P3 integration, final alert policy, beta removal, cutover, new backend contracts, destructive administration or new scientific calibration. Any future UI mission should first review whether UI v2 stays a parallel prototype surface or starts a controlled migration path for specific beta workflows.
