# Phase 4 — integrated runtime evidence collection

This tooling collects evidence for a complete NatureProtector execution without changing application behaviour, domain contracts, migrations, configuration, `.env`, deployment resources, or scoring semantics.

## Evidence classes

The output keeps three classes separate:

- `STATIC_RUNTIME_CONTRACT`: endpoint, diagnostic, scenario, degradation and persistence-chain declarations extracted from the repository;
- `HISTORICAL_REPOSITORY_EXECUTION`: the preserved scenario B/C execution from May 2026 already committed under `docs/evidence/progress-2026-05-22`;
- `CURRENT_RUNTIME_EXECUTION`: a new owner-environment API execution and optional read-only PostgreSQL trace.

Historical values are never promoted to current execution values.

## Static and historical collection

PowerShell:

```powershell
& .\scripts\evidence\collect-integrated-runtime-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

Git Bash:

```bash
bash scripts/evidence/collect-integrated-runtime-evidence.sh \
  --baseline-id baseline-YYYYMMDDTHHMMSSZ \
  --python /c/Users/Miguel/AppData/Local/Programs/Python/Python313/python.exe
```

This mode does not call the API, Docker, PostgreSQL or cloud resources. It reconstructs the evidence contract and verifies the historical B/C package.

## Current integrated B/C execution

The Backoffice API must be running in `Development`, with PostgreSQL, RabbitMQ, Prevention Host and Simulator Host available. The caller needs a token containing `Sim` or `Admin`.

Use either a ready bearer token:

```powershell
$env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN = "<temporary token>"
```

or login credentials held only in environment variables:

```powershell
$env:NATUREPROTECTOR_RUNTIME_USERNAME = "admin"
$env:NATUREPROTECTOR_RUNTIME_PASSWORD = "<local development password>"
```

Then run:

```powershell
& .\scripts\evidence\collect-integrated-runtime-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe" `
  -Live `
  -RequireLive
```

By default, the collector executes only a reset **dry run**. It changes runtime data only when `-ResetRuntime` is explicitly supplied.

## Event-level PostgreSQL trace

The HTTP audit and timing contracts provide run-level evidence but do not expose every event, inbox, processing attempt, accepted reading, assessment and projection identifier. To close this gap, install psycopg v3 and provide a DSN through a named environment variable:

```powershell
& "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe" -m pip install "psycopg[binary]>=3.2,<4"
$env:NATUREPROTECTOR_POSTGRES_DSN = "Host=localhost;Port=5432;Database=natureprotector;Username=np;Password=<local password>"

& .\scripts\evidence\collect-integrated-runtime-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe" `
  -Live `
  -RequireLive `
  -PostgresDsnEnvironmentVariable "NATUREPROTECTOR_POSTGRES_DSN" `
  -RequireDatabaseTrace
```

The collector opens a read-only transaction, applies a statement timeout and writes no DSN or password to the evidence package.

## Main outputs

```text
04-runtime/<run-id>/
├── static/
├── run-specs/
├── historical/
├── live/
├── database-trace/
├── report-ready/
├── environment.json
├── phase4-summary.json
├── phase4-summary.md
└── SHA256SUMS.txt
```

The report-facing files are:

- `report-ready/integrated-runtime-summary.md`;
- `historical/historical-runs.csv`;
- `historical/historical-comparison.csv`;
- `live/live-runs.csv`, only after a current execution;
- `database-trace/database-trace-summary.csv`, only after a current event-level trace.

## Claim boundaries

- `PublishedAt` is not persisted, so publish-to-end latency remains unsupported.
- API timings are durations between persisted points, not production-capacity claims.
- Historical B/C values remain historical.
- A full event-level chain requires `DATABASE_TRACE_STATUS=PASS`.
