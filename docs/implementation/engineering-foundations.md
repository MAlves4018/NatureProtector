# Engineering foundations

This page is the M02 operational contract for build, tests, coverage, CI, local infrastructure, frontend security triage and observability checks.

## Required local gates

Run from the repository root unless noted otherwise.

The .NET SDK is pinned by `global.json` to feature band `9.0.306` with `rollForward: latestPatch`. Supporting another feature band should be done through an explicit CI matrix, not by loosening local roll-forward.

| Area | Command | Notes |
| --- | --- | --- |
| Backend build | `dotnet build .\NatureProtector.sln --no-restore --nologo -v minimal` | Requires restore first on a clean machine. |
| Backend tests | `dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1` | Local M02 run passed with Docker services available. |
| Backend coverage | `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\generate-coverage-report.ps1` | Generates `coveragereport_core/index.html` and `Summary.txt`. |
| NuGet vulnerability audit | `dotnet list .\NatureProtector.sln package --vulnerable --include-transitive` | Current E2 run reports no vulnerable packages across solution projects. |
| Frontend install | `npm ci` in `webUI/` | Uses `package-lock.json`. |
| Frontend typecheck | `npm run typecheck` in `webUI/` | Runs `tsc --noEmit`. |
| Frontend lint | `npm run lint` in `webUI/` | Runs Biome on the ratcheted frontend scope: UI v2, services, e2e, scripts and config. Stylistic/a11y legacy rewrites are intentionally not enabled. |
| Frontend format check | `npm run format:check` in `webUI/` | Runs Biome formatter check on `biome.jsonc`, `package.json` and `tsconfig.json` only. This is an incremental formatter gate, not a whole-frontend reformat. |
| Frontend tests | `npm test` in `webUI/` | Runs Vitest with jsdom and JUnit output. |
| Frontend coverage | `npm run test:coverage` in `webUI/` | Generates `webUI/coverage/`; output is ignored by git. |
| Frontend build | `npm run build` in `webUI/` | Route-level chunks keep the largest entry below the Vite warning threshold in the M02 run. |
| Frontend e2e | `npm run test:e2e` in `webUI/` | Playwright builds the Vite app, serves `dist/` through `vite preview`, then runs browser checks against the built artifact. Chromium is the default gate; set `NP_PLAYWRIGHT_BROWSER_MATRIX=all` for Chromium, Firefox and WebKit. The UI v2 authenticated spec uses a bounded HTTP fixture to exercise Anonymous/Admin/Sim/Pipeline journeys, failure states and evidence download without depending on a live local identity database. |
| Frontend audit | `npm audit --json` in `webUI/` | M02 leaves the Vite/esbuild advisory as residual risk; see below. |
| Secret scan | `.\scripts\ci\run-secret-scan.ps1 -RepositoryRoot . -IncludeUntracked` | Downloads fixed Gitleaks `8.28.0` when absent, scans Git history, staged changes and a working-tree snapshot, then writes redacted reports under `artifacts/secret-scan/`. |

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
- frontend `npm ci`, typecheck, Biome lint/format checks, Vitest, coverage, production build and Playwright browser checks against `vite preview` on Ubuntu;
- a non-blocking frontend audit artifact.

Pull requests and pushes run the critical Chromium browser gate. The scheduled workflow and manual runs with `browser_matrix=all` install and run Chromium, Firefox and WebKit. Playwright keeps traces and screenshots on failure, and retains video only in CI failure cases.

The workflow does not start Docker services. Infrastructure-dependent validation remains covered by the local gate above until the project has a stable service-container CI contract.

Dedicated security and release-candidate gates run `scripts/ci/run-secret-scan.ps1`. The wrapper uses `.gitleaks.toml`, redacts findings, uploads `artifacts/secret-scan/`, covers relevant Git history through `--all`, staged changes, and tracked plus untracked non-ignored working-tree files. `.gitleaksignore` baselines only known historical local/dev fingerprints and a CSS false positive; it does not contain secret values. `.env` and `.env.example` are not modified by the scanner.

## M02 validation snapshot

Local run on 2026-06-13:

- `dotnet build .\NatureProtector.sln --no-restore --nologo -v minimal`: passed; at the time this reported a known `NU1902` warning for `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0`, which is no longer present after the OpenTelemetry package update.
- `dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1`: passed, 1179 tests.
- `scripts/tests/generate-coverage-report.ps1`: passed; line coverage `82%`, branch coverage `68.1%`, method coverage `89.5%`.
- `npm run typecheck`: passed.
- `npm test`: passed, 5 tests.
- `npm run test:coverage`: passed; frontend baseline line coverage `5.02%`.
- `npm run build`: passed; largest JS chunk `497.24 kB` minified, no large-chunk warning.
- `npm audit --json`: 3 high findings remain through Vite -> esbuild.

Current E2 backend validation on 2026-06-16:

- `dotnet build .\NatureProtector.sln -c Release --no-restore --nologo -v minimal`: passed with two pre-existing CS1587 XML-comment warnings in Postgres migrations.
- `dotnet test .\tests\NatureProtector.Shared.Tests\NatureProtector.Shared.Tests.csproj -c Release --no-restore --no-build --nologo -v minimal`: passed, 18 tests.
- `dotnet test .\tests\NatureProtector.Core.Tests\NatureProtector.Core.Tests.csproj -c Release --no-restore --no-build --nologo -v minimal`: passed, 476 tests.
- `dotnet list .\NatureProtector.sln package --vulnerable --include-transitive`: no vulnerable packages reported.
- `scripts/tests/generate-coverage-report.ps1 -NoRestore -NoBuild`: passed, 1537 non-Docker backend tests; `backend-integral` line coverage `63.5%`, `backend-focused` line coverage `97.1%`.

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

OpenTelemetry runtime wiring now lives in `NatureProtector.Shared.Observability`. `NatureProtector.Shared` remains the contracts/messaging boundary and must not reference `OpenTelemetry*` packages. The beta `OpenTelemetry.Instrumentation.Process` package is intentionally kept inside the runtime observability assembly only; pure contracts must not depend on exporters or instrumentation packages.

The focused observability smoke in `NatureProtector.Shared.Tests` starts the OpenTelemetry hosted service with console export disabled and an OTLP endpoint configured. This is a compatibility/startup smoke, not a live collector or telemetry delivery proof.
