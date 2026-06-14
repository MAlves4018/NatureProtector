# M06 - Delivery capacity, simulation and cutover readiness

Date: 2026-06-14

This document records the local technical readiness pass executed in M06. It is a delivery handoff for the current NatureProtector prototype, not a production cutover decision and not scientific validation of wildfire prediction.

## Scope

M06 measured and documented:

- local clone-to-run readiness using existing setup/runtime scripts;
- local API/web availability and response time probes;
- short simulator workloads for nominal, missing-readings and value-degradation profiles;
- browser journeys for logged-out and local Development Admin states;
- dependency/security findings;
- validation commands for backend and frontend gates.

M06 did not:

- remove or replace the beta UI;
- deploy to production or execute cutover;
- contact stakeholders or collect consent;
- integrate P3 into scoring/runtime;
- change RabbitMQ contracts, event names, API projections, database schema, migrations, scoring, alert semantics, roles or JWT claims;
- perform a stress test, external load test or scientific calibration.

## Evidence pack

Primary mission evidence is under:

```text
NatureProtector.brain/control/M06-DELIVERY-CAPACITY-SIMULATION-AND-CUTOVER-READINESS/
```

Repository evidence added by M06 is under:

```text
docs/evidence/m06-readiness/specs/
docs/evidence/runs/20260614-131245-scenario_b-m06-nominal-scenario-b/
docs/evidence/runs/20260614-131314-scenario_c-m06-missing-readings-scenario-c/
docs/evidence/runs/20260614-131340-scenario_b-m06-noise-scenario-b/
```

Key local observations:

| Area | Evidence | Result | Classification |
| --- | --- | --- | --- |
| Prerequisites | `Test-LocalPrerequisites.ps1` | 0 failures, 0 warnings | Measured locally |
| Infrastructure baseline | `Test-LocalBaseline.ps1 -InfrastructureOnly` | PostgreSQL, RabbitMQ, InfluxDB, Grafana OK | Measured locally |
| Full baseline | `Test-LocalBaseline.ps1 -Full` | One known `401` on an authenticated endpoint; webUI OK | Observed local limitation |
| API/web probes | `run-local-readiness-workload.ps1` | 55/55 expected HTTP statuses | Measured locally |
| Simulations | three M06 run specs | 3 runs completed | Measured locally |
| Browser | in-app browser | logged-out workspace and Admin workspace/UI v2 observed | Observed locally |
| Backend tests | `dotnet test .\NatureProtector.sln --nologo -v minimal -m:1` | 1182/1182 passed | Measured locally |
| Frontend tests | `npm test` | 30/30 passed | Measured locally |
| Frontend coverage | `npm run test:coverage` | `app/ui-v2` line coverage 84.28%; global webUI 31.71% | Measured locally |
| Frontend build | `npm run build` | passed | Measured locally |
| Dependency audit | `npm audit --audit-level=high --json` | 3 high Vite/esbuild-chain findings | Measured locally |
| NuGet audit | `dotnet list package --vulnerable --include-transitive` | OpenTelemetry exporter moderate advisory | Measured locally |

## Local readiness workload

M06 added:

```powershell
.\scripts\performance\run-local-readiness-workload.ps1
```

The script measures local HTTP status and elapsed time for bounded API/web probes. It writes `manifest.json`, `probes.json`, `measurements.csv/json`, `summary.csv/json` and `summary.md`.

Example run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\performance\run-local-readiness-workload.ps1 `
  -ApiBaseUrl http://127.0.0.1:5254 `
  -WebBaseUrl http://127.0.0.1:5173 `
  -Repetitions 5 `
  -TimeoutSeconds 15
```

M06 measured 55 attempts with 55 expected statuses. Selected p95 values:

| Probe | Status | P95 ms | Classification |
| --- | ---: | ---: | --- |
| API health | 200 | 44.83 | Measured locally |
| Areas list | 200 | 15.87 | Measured locally |
| Area detail | 200 | 33.53 | Measured locally |
| Grid cells, `take=25` | 200 | 74.64 | Measured locally |
| Sensor nodes | 200 | 27.72 | Measured locally |
| Active alerts | 200 | 24.18 | Measured locally |
| Scenario auth guard | 401 | 4.73 | Observed access control |
| Operational-state auth guard | 401 | 2.67 | Observed access control |
| Runtime summary auth guard | 401 | 2.60 | Observed access control |
| web root | 200 | 3.56 | Measured locally |
| `/ui-v2` | 200 | 4.80 | Measured locally |

These are local workstation measurements. They are not a load test and do not define production SLOs.

## Simulation evidence

M06 ran three short simulations without reset or cleanup:

| Run label | Scenario | Profile | Run id | Status | Risk assessments | Notes |
| --- | --- | --- | --- | --- | ---: | --- |
| `m06-nominal-scenario-b` | `scenario_b` | `none` | `ceb20860-ed0a-4554-ac43-70d3a6596f70` | Completed | 18 | Nominal short run |
| `m06-missing-readings-scenario-c` | `scenario_c` | `missing-readings` | `93a397e9-87b3-4730-9e58-a44554c70072` | Completed | 14 | Observation coverage gap; not pipeline failure |
| `m06-noise-scenario-b` | `scenario_b` | `noise` | `467ddba8-80f9-4874-9949-1ac5c376d94e` | Completed | 18 | Value-degradation profile |

Resolved profiles were confirmed from `control.simulation_runs.MetadataJson`. The `missing-readings` run produced fewer risk assessments than the nominal run. This is an observation-gap effect and must not be described as rejected/quarantined processing failure.

## Capacity interpretation

Measured:

- local API/web probes completed within the recorded p95 values above;
- three short simulator runs completed with 6 sensors, 3 cycles and 1-second intervals;
- backend and frontend automated gates passed on the local machine.

Estimated:

- the local baseline is suitable for a controlled technical demo using small runs similar to the M06 specs or the existing 6-sensor smoke profiles;
- the local machine can present the API/webUI and process short simulator runs for demonstration.

Not instrumented:

- broker queue depth as an API/UI metric;
- per-event publisher timestamp;
- full end-to-end event latency from simulator publish through UI projection;
- sustained throughput, saturation point, concurrency ceiling and production SLOs.

Not validated:

- production deployment;
- external users/stakeholders;
- scientific calibration;
- civil-protection/official alerting use.

## UI and profile journeys

Observed browser journeys:

- logged-out user can open the app, select `proenca-a-nova`, enter the workspace and see public data plus explicit `Not available` states for protected runtime data;
- Development Admin can sign in locally, open the workspace, see the latest M06 run and runtime summary, and open `/ui-v2`;
- `/ui-v2` shows the academic/non-operational boundary, technical pipeline, QA, evidence, Admin and P3 experimental surfaces.

Not available:

- real browser journeys for separate Pipeline and Sim identities. The local database contained only the existing Admin user during M06. Backend authorization tests still cover read/write role behavior, but this is not the same as a real multi-login browser matrix.

## Dependency and security readiness

Open findings:

- `npm audit --audit-level=high --json`: 3 high findings through `@vitejs/plugin-react`, `vite` and `esbuild`. The available npm fix path reports semver-major changes; M06 did not apply `npm audit fix --force`.
- `dotnet list package --vulnerable --include-transitive`: `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0` moderate advisory.
- `.env` and `.env.example` contain non-empty development secret values. M06 recorded only redacted classifications and did not print values.

Minimum before any external sharing or stronger delivery claim:

- rotate/remove real-looking tracked dev secrets or replace them with documented placeholders;
- complete a controlled dependency-hardening pass;
- decide whether a remote CI/service-container gate is required for infrastructure.

## Cutover readiness

Local technical demo readiness: conditional go.

Conditions:

- use the local Development baseline only;
- explicitly state academic prototype/non-operational status;
- use known M06 or smoke runs;
- avoid production/civil-protection claims;
- preserve beta until a separate human cutover decision.

Production or external cutover readiness: no-go.

Reasons:

- no production deployment validation;
- dependency findings remain open;
- broker backlog and full end-to-end latency are not instrumented;
- Pipeline/Sim browser identities were not available for real journey validation;
- stakeholder consent/feedback readiness was not validated;
- scientific calibration remains out of scope.

## Rollback and preservation

M06 did not execute a Git branch, commit, tag, reset, restore, clean, pull, push or checkout.

Data preservation decisions:

- no volumes were deleted;
- no database reset was executed;
- the M04 smoke run was preserved;
- M06 added new run evidence and database rows for the three short simulations.

If a clean demo is required later, use an explicitly authorized reset/rebaseline path. Do not silently delete volumes or runtime evidence.

## Pre-external verification readiness supplement

On 2026-06-14, a focused pre-external-readiness pass closed the minimum technical blockers that were directly blocking independent local reproduction preparation. This supplement does not reopen M06 and does not claim external verification has been completed.

Changes validated locally:

- `Test-LocalBaseline.ps1 -Full` now uses public `/health` for Backoffice API readiness and treats unauthenticated `GET /api/control/configurations/active` returning `401` as an expected authentication guard. The protected endpoint remains protected by `Sim`, `Pipeline` or `Admin`.
- `scripts/setup/Ensure-LocalDemoIdentities.ps1` prepares local `Pipeline` and `Sim` identities through the existing Admin user-plane API and existing roles. It requires passwords from parameters or environment variables and does not store secrets in the repository.
- Direct API journeys were validated: `Pipeline` can log in and read runtime summary but receives `403` on runtime start; `Sim` can log in, read scenarios and start a minimal local run.
- Reset/rebaseline guidance now prefers authenticated runtime reset dry-run and explicit confirmed runtime reset over Docker volume deletion. No confirmed reset was executed in this pass.
- Coverage was rerun. Backend aggregate remained `82%` line / `68.1%` branch. UI v2 was brought back above the M06 ratchet with `84.45%` line coverage.

Known side effects:

- The `Sim` journey validation created a local minimal run `53f5ab53-c39f-4274-9665-934140a98291`.
- Local `pipeline.local` and `sim.local` users were created in the current PostgreSQL database for reproduction preparation.
- App/runtime logs and validation evidence were generated locally.

Residual boundaries:

- This is still local technical readiness, not production cutover, not stakeholder validation and not scientific calibration.
- Dependency/security findings from M06 remain open.
- Secrets hygiene for `.env`/`.env.example` remains a separate external-sharing blocker unless explicitly resolved before publication.
