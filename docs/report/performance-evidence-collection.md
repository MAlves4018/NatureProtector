# Phase 5 — performance evidence collection

Phase 5 separates three measurement layers that must not be conflated:

1. **Microbenchmarks** — isolated scoring, temporal classification, territorial mapping and event serialization through BenchmarkDotNet.
2. **HTTP read paths** — read-only local request latency and observed request rate with explicit warm-up, concurrency and p50/p95/p99.
3. **Integrated pipeline workload** — bounded simulation campaigns, persisted audit/timings, queue drain and reconciliation through the existing system-capacity script.

The tooling does not promote static declarations, historical prose or a smoke run into a current performance result.

## Static collection

PowerShell:

```powershell
& .\scripts\evidence\collect-performance-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python"
```

Git Bash:

```bash
bash scripts/evidence/collect-performance-evidence.sh \
  baseline-YYYYMMDDTHHMMSSZ \
  --python /c/Users/Miguel/AppData/Local/Programs/Python/Python313/python.exe
```

This inventories profiles, benchmark cases, probes, telemetry instruments, known measurement limitations and report chart specifications. It makes one short `/health` availability probe but does not execute a workload unless requested.

## Current microbenchmarks

Use B1 for the first reportable local comparison. B0 uses BenchmarkDotNet `Job.Dry` and is only a harness smoke check.

```powershell
& .\scripts\evidence\collect-performance-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python" `
  -RunMicrobenchmarks `
  -BenchmarkProfile B1 `
  -RequireMicrobenchmarks
```

Preserve the raw BenchmarkDotNet JSON, stdout/stderr and the Phase 5 hash manifest.

## Current read-only HTTP workload

Start the API and, optionally, the frontend. Then execute a calibration before B0/B1:

```powershell
& .\scripts\evidence\collect-performance-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python" `
  -RunHttp `
  -HttpProfile Calibration `
  -IncludeWeb `
  -RequireHttp
```

For the report baseline:

```powershell
& .\scripts\evidence\collect-performance-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python" `
  -RunHttp `
  -HttpProfile B1 `
  -IncludeWeb `
  -RequireHttp
```

The portable runner is also directly callable:

```powershell
& "python" `
  .\scripts\performance\run-http-workload.py `
  --profile B1 `
  --include-web
```

Only GET requests are issued. Request durations are not event end-to-end latency.

## Integrated system workload

First run calibration with the existing runtime script:

```powershell
$env:NP_PERFORMANCE_AUTH_TOKEN = "<temporary Sim or Admin token>"

& .\scripts\performance\run-system-capacity-workload.ps1 `
  -Profile Calibration `
  -CollectRuntimeProcessEvidence
```

Then run the bounded B1 campaign. For report-grade percentiles, override the default two repetitions with at least ten completed runs:

```powershell
& .\scripts\performance\run-system-capacity-workload.ps1 `
  -Profile B1 `
  -CalibrationRunDirectory "<calibration output directory>" `
  -Repetitions 10 `
  -CollectRuntimeProcessEvidence
```

Ingest the completed directory into the Phase 5 package:

```powershell
& .\scripts\evidence\collect-performance-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python" `
  -SystemRunDirectory "<B1 output directory>" `
  -RequireSystem
```

## Report-ready aggregation

After collecting one or more raw workload directories, generate bounded report
tables with the deterministic aggregator:

```powershell
& "python" `
  .\scripts\performance\aggregate-runtime-metrics.py `
  --output-root "<external dossier directory>" `
  --benchmark-dir "<BenchmarkDotNet output directory>" `
  --http-run-dir "<HTTP workload output directory>" `
  --system-run-dir "<system workload output directory>"
```

The aggregator writes canonical CSV tables, method notes and SVG figures for
supported samples. Unsupported metrics are not imputed: publish-to-receive
latency remains `UNSUPPORTED` until a persisted `PublishedAt` or equivalent
stage timestamp exists, and processing throughput is not calculated from a run
request duration that includes deliberate generation intervals or drain waits.

## Required report presentation

Every result must include:

- baseline and Phase 5 run ID;
- machine and toolchain;
- profile, warm-up, repetitions and concurrency;
- sample count;
- p50, p95, p99 and maximum where sample count permits;
- success/error counts;
- raw artifact location and hash;
- explicit claim boundary.

Do not claim:

- production capacity or production SLO compliance;
- scalability from one workstation;
- publish-to-projection latency while `PublishedAt` is not persisted;
- distributed throughput from BenchmarkDotNet;
- meaningful p95/p99 from one or two integrated runs.
