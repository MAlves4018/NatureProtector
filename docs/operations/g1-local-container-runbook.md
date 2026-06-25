# G1 local container runbook

## Purpose

Validate production-shaped images locally without provisioning Google Cloud. This is not a production deployment and does not replace the canonical local developer launcher.

## Prerequisites

- Docker Engine with Compose v2;
- .NET SDK selected by `global.json`;
- Node/npm compatible with `webUI/package.json`;
- the existing local `.env`, created by the owner according to the setup documentation.

The G1 scripts do not create or modify `.env` or `.env.example`. The evidence script generates a process-local random JWT signing key when `NP_G1_JWT_SIGNING_KEY` is absent; the value is not written to the repository or evidence. An explicit key may be supplied in the current shell when a stable local token is required:

```powershell
$env:NP_G1_JWT_SIGNING_KEY = '<at least 32 UTF-8 bytes>'
```

`NP_G1_BOOTSTRAP_ADMIN_PASSWORD` is optional and has no fixed fallback in the G1 Compose overlay.

The G1 overlay expects PostgreSQL migration/admin credentials and runtime application credentials to be distinct:

- `POSTGRES_USER` / `POSTGRES_PASSWORD` are used by the PostgreSQL container and the `postgres-migrations` job;
- `POSTGRES_APP_USER` / `POSTGRES_APP_PASSWORD` are used by `postgres-bootstrap`, `backoffice-api`, `prevention` and `simulator` after the migrations job grants least-privilege access.

## Compose model

Always combine both files:

```powershell
docker compose -f docker-compose.yml -f docker-compose.g1.yml config
```

The base file owns PostgreSQL, RabbitMQ, InfluxDB and Grafana. The G1 overlay adds:

- `postgres-bootstrap`, one-shot and profile-gated;
- `backoffice-api`, HTTP 8080 in-container;
- `prevention`, continuous worker with HTTP probes;
- `simulator`, one-shot and profile-gated;
- `frontend`, immutable Vite build served by unprivileged Nginx.

## Recommended evidence run

```powershell
pwsh ./scripts/containers/Test-G1ContainerReadiness.ps1
```

Artifacts are written under `artifacts/g1-container-readiness/<timestamp>/` with logs, TRX, health checks, result status and SHA-256 checksums.

## Manual sequence

```powershell
$compose = @('-f','docker-compose.yml','-f','docker-compose.g1.yml')
docker compose @compose --profile bootstrap --profile simulator build postgres-migrations postgres-bootstrap backoffice-api prevention simulator frontend
docker compose @compose up -d postgres rabbitmq influxdb
docker compose @compose --profile bootstrap run --rm postgres-migrations
docker compose @compose --profile bootstrap run --rm postgres-bootstrap
docker compose @compose up -d backoffice-api prevention frontend
```

Probe endpoints:

```text
http://localhost:5254/health/live
http://localhost:5254/health/ready
http://localhost:5260/health/live
http://localhost:5260/health/ready
http://localhost:5173/healthz
```

Run a finite simulator smoke:

```powershell
docker compose @compose --profile simulator run --rm `
  -e Simulator__NumberOfCycles=2 `
  -e Simulator__IntervalSeconds=1 `
  -e Simulator__RunOverrides__SensorCount=1 `
  simulator
```

## Read-only filesystem

Application containers use a read-only root filesystem in Compose. `/tmp` is a temporary filesystem. Runtime orchestration and filesystem evidence are disabled in the API container, so the API does not depend on source files or writable repository paths.

## Known limits

- The bootstrap is not the migration runner decided in G0; migration execution remains a later phase.
- Image build, startup and smoke must be proven in an owner environment with Docker and package access.
- Backend restore/build/tests require the SDK selected by `global.json`; before cloud release, move the pinned .NET toolchain to the current supported patch and rerun the complete evidence suite.
- The API startup administrative bootstrap is retained only for local compatibility; do not treat it as the staging migration/bootstrap design.
- The local compose file uses development infrastructure credentials supplied by the owner and must not be treated as a cloud secret model. Resolved Compose configuration is validated with `--quiet` to avoid copying secrets into evidence.
- The fixed Prevention probe port is intended for the single-instance G1 smoke. Multi-instance evidence must use a separate override without a fixed host port.
