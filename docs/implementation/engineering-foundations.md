# Engineering foundations

This page is the M02 operational contract for build, tests, coverage, CI, local infrastructure, frontend security triage and observability checks.

## Required local gates

Run from the repository root unless noted otherwise.

| Area | Command | Notes |
| --- | --- | --- |
| Backend build | `dotnet build .\NatureProtector.sln --no-restore --nologo -v minimal` | Requires restore first on a clean machine. |
| Backend tests | `dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1` | Local M02 run passed with Docker services available. |
| Backend coverage | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\generate-coverage-report.ps1` | Generates `coveragereport_core/index.html` and `Summary.txt`. |
| Frontend install | `npm ci` in `webUI/` | Uses `package-lock.json`. |
| Frontend typecheck | `npm run typecheck` in `webUI/` | Runs `tsc --noEmit`. |
| Frontend tests | `npm test` in `webUI/` | Runs Vitest with jsdom and JUnit output. |
| Frontend coverage | `npm run test:coverage` in `webUI/` | Generates `webUI/coverage/`; output is ignored by git. |
| Frontend build | `npm run build` in `webUI/` | Route-level chunks keep the largest entry below the Vite warning threshold in the M02 run. |
| Frontend audit | `npm audit --json` in `webUI/` | M02 leaves the Vite/esbuild advisory as residual risk; see below. |

## Local infrastructure gate

Docker-backed validation is intentionally local/manual, not required by the default CI workflow.

```powershell
docker compose --project-directory . -f .\docker-compose.yml up -d
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\influx\Ensure-InfluxDatabase.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\postgres\bootstrap-control-plane.ps1 -SkipBuild
```

M02 validated PostgreSQL, RabbitMQ, InfluxDB and Grafana with this path after `infra/scripts/up.ps1` hung in the local shell. The direct `docker compose` command used the repository compose file and did not change service contracts.

## CI scope

`.github/workflows/engineering-foundations.yml` runs:

- backend restore, build and tests on Windows with `BackofficeApi__ControlPlaneEnabled=false`;
- frontend `npm ci`, typecheck, Vitest, coverage and production build on Ubuntu;
- a non-blocking frontend audit artifact.

The workflow does not start Docker services. Infrastructure-dependent validation remains covered by the local gate above until the project has a stable service-container CI contract.

## M02 validation snapshot

Local run on 2026-06-13:

- `dotnet build .\NatureProtector.sln --no-restore --nologo -v minimal`: passed; one known `NU1902` warning for `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0`.
- `dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1`: passed, 1179 tests.
- `scripts/tests/generate-coverage-report.ps1`: passed; line coverage `82%`, branch coverage `68.1%`, method coverage `89.5%`.
- `npm run typecheck`: passed.
- `npm test`: passed, 5 tests.
- `npm run test:coverage`: passed; frontend baseline line coverage `5.02%`.
- `npm run build`: passed; largest JS chunk `497.24 kB` minified, no large-chunk warning.
- `npm audit --json`: 3 high findings remain through Vite -> esbuild.

## Frontend security triage

M02 updated semver-compatible packages to remove the React Router and Tailwind/Vite plugin advisories:

- `react-router` and `react-router-dom` to `7.17.0`;
- `@tailwindcss/vite` and `tailwindcss` to `4.3.1`;
- `vite` to `6.4.3`.

Residual finding:

- `vite@6.4.3` depends on `esbuild@0.25.12`; `npm audit` flags `esbuild <0.28.1`.
- Vite 6 declares `esbuild` as `^0.25.0`, so forcing `0.28.1` would override outside Vite's declared range.
- `npm audit fix --force` proposes a major/downgrade path and was not applied.

Recommended next step: evaluate a controlled Vite major upgrade when the local Node runtime is upgraded to a version accepted by that Vite line, then rerun frontend typecheck, tests, coverage and build.

## Observability

Backoffice API exposes a minimal `GET /health` endpoint through ASP.NET health checks. The endpoint is intentionally technical and does not change RabbitMQ contracts, domain events, scoring semantics, schemas or alert policy.
