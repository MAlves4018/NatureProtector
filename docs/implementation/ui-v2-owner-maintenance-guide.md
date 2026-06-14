# UI v2 Owner Maintenance Guide

Last updated: 2026-06-14

## Scope

This guide documents the recovered UI v2 maintenance contract after `UI-STRUCTURAL-RECOVERY-002`.

UI v2 remains isolated at `/ui-v2`. The beta routes remain preserved. This pass did not change RabbitMQ contracts, public API projections, database schema, migrations, scoring, alert semantics, roles, JWT claims, P3 runtime integration, reset/rebaseline behavior, production cutover or observability infrastructure.

## Product Surface Matrix

| Profile | Visible UI v2 areas | Hidden by design |
| --- | --- | --- |
| Public / signed out | Product landing, area selector, Data Status, help, login link | Risk score, pipeline, simulation, runs, QA, evidence, P3, admin |
| Pipeline | Overview, risk/data, runs, pipeline, quality/evidence, data status, help | Simulation execution, P3, admin |
| Sim | Overview, risk/data, runs, scenarios, simulation, requested/resolved review, data status, help | Pipeline/quality/evidence internals, P3, admin |
| Admin | All UI v2 surfaces | No destructive reset action is exposed |
| Unknown role | Demo and help only | All operational/technical surfaces |

Backend authorization remains the security boundary. The frontend profile matrix is a product/UX constraint and must not be treated as a replacement for API authorization.

## Current Structure

Primary files:

- `webUI/src/app/ui-v2/UiV2App.tsx`: small shell/provider composition, theme bridge, skip link, header, navigation and page selection.
- `webUI/src/app/ui-v2/state/UiV2Context.tsx`: frontend orchestration and existing API reads/writes.
- `webUI/src/app/ui-v2/navigation/pageRegistry.ts`: task-based page registry derived from capabilities.
- `webUI/src/app/ui-v2/navigation/UiV2Navigation.tsx`: grouped navigation renderer.
- `webUI/src/app/ui-v2/components/`: reusable UI v2 components such as area selection, Data Status, technical details, contextual help and beta parity links.
- `webUI/src/app/ui-v2/pages/`: public, overview, risk/data, runs, simulation, pipeline, quality/evidence, admin and P3 page modules.
- `webUI/src/app/ui-v2/content/`: technical label mapping, help topic registry, beta parity inventory and related content.
- `webUI/src/app/ui-v2/theme/ui-v2.css`: UI v2 light/dark visual system.
- `webUI/src/app/ui-v2/capabilities.ts`: role-to-capability matrix.
- `webUI/src/app/ui-v2/i18n.ts`: PT/EN copy.
- `webUI/src/app/ui-v2/coreContext.ts`: area/scenario/run/simulation read-model adapters.
- `webUI/src/app/ui-v2/outputContext.ts`: contextual risk read model.
- `webUI/src/app/ui-v2/technicalSurfaces.ts`: pipeline, QA, evidence, admin, P3 and readiness read models.
- `webUI/src/app/ui-v2/*.test.ts*`: focused regression coverage.

Known maintenance risk: `UiV2Context.tsx` now carries most frontend orchestration. New features should usually add page/component modules first and only extend the provider when new shared state is required.

## Rules for Safe Changes

- Keep `/ui-v2` isolated until an explicit owner cutover decision exists.
- Do not remove or reroute beta pages as part of UI v2 maintenance.
- Add new public content only if it fits project purpose, limitations, area selection, basic data status or login.
- Do not expose pipeline, QA, evidence, P3, admin or simulation actions to signed-out users.
- Use controlled option sets for simulator degradation profiles; do not reintroduce free-text degradation profile entry.
- Keep P3 framed as experimental and not integrated into scoring, alerts, schema or main runtime.
- Keep candidate weights, thresholds and classifications described as prototype/candidate values, not scientific calibration.

## Validation Contract

For UI v2 changes, run at minimum:

```powershell
cd webUI
npm run typecheck
npm test -- src/app/ui-v2
npm test
npm test
npm test
npm run test:coverage
npm run build
dotnet test tests/NatureProtector.Backoffice.Api.Tests/NatureProtector.Backoffice.Api.Tests.csproj --no-restore
```

When changing visibility/profile behavior, run the full frontend suite at least three times and check stderr for React `act(...)` warnings.

## Latest Recovery Evidence

Stored under:

`NatureProtector.brain/control/UI-STRUCTURAL-RECOVERY-002/`

Key screenshots:

- `baseline/ui-v2-public-light-before.png`
- `baseline/ui-v2-public-dark-before.png`
- `browser-evidence/public-light.png`
- `browser-evidence/public-data-status.png`

Final local validation:

- `npm test` passed three consecutive times, 33 tests each.
- `npm run typecheck` passed.
- `npm test -- src/app/ui-v2` passed, 26 tests.
- `npm run test:coverage` passed; `app/ui-v2` line coverage was `82.56%`.
- `npm run build` passed.
- `dotnet test tests/NatureProtector.Backoffice.Api.Tests/NatureProtector.Backoffice.Api.Tests.csproj --no-restore` passed, 92 tests, with known `NU1902`.

## Remaining Owner Decisions

- Whether UI v2 public copy should be further simplified for non-technical audiences after owner review.
- Whether beta-only capabilities should migrate, be substituted, or remain beta-only after the parity links are reviewed.
- Whether to proceed to observability audit/metrics. This pass did not authorize or implement metrics.
