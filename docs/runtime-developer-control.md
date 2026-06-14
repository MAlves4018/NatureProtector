# Runtime Developer Control

The developer runtime console is available at:

```text
/dev/runtime
```

It is a local development surface for:

- fixed read-only diagnostics;
- starting `Simulator.Host` runs through Development-only API endpoints;
- dry-run and confirmed runtime-state reset;
- freshness/carry-forward visibility based on persisted projections.

The frontend never sends free-form SQL. Risk and alerts are read from persisted state and are not recalculated in the browser.

## UI v2 technical surfaces

M05 adds technical read surfaces under:

```text
/ui-v2
```

Those surfaces reuse existing runtime contracts for Pipeline/Observability, QA, Evidence, proportional Admin context, P3 experimental context and staging/demo readiness. They do not replace `/dev/runtime` and do not expose runtime reset, runtime diagnostic execution or P3 run execution as controls.

Missing runtime instrumentation is shown explicitly. In particular, UI v2 does not infer broker health from lack of errors and does not invent RabbitMQ backlog, publisher timestamps or full per-event latency.

## Local Launcher

Use one command to start the local runtime services:

```powershell
.\scripts\dev\start-local-runtime.ps1 -OpenBrowser
```

Useful options:

```powershell
.\scripts\dev\start-local-runtime.ps1 -SkipBootstrap -OpenBrowser
.\scripts\dev\start-local-runtime.ps1 -SkipDocker -NoBrowser
.\scripts\dev\start-local-runtime.ps1 -SkipBootstrap -ForceRestart -OpenBrowser
```

Logs are written under:

```text
docs/evidence/dev-runtime/<timestamp>-local-runtime/
```

## Safety

Runtime reset is Development-only, blocks active runs and requires exact confirmation:

```text
RESET_RUNTIME_STATE
```

It clears only runtime tables in `control`, `pipeline` and `projection`. It does not clear areas, sensors, scenarios, configuration versions, datasets, user roles or Docker volumes.

Before using a confirmed reset for a clean demo, run the reset endpoint with `dryRun=true` and preserve the before/after counts. For external-reproduction preparation, prefer:

1. inspect current runs and runtime counts;
2. dry-run reset through an authenticated `Sim` or `Admin` identity;
3. execute the confirmed reset only when a clean runtime state has been explicitly chosen;
4. create a short rebaseline run with a clear `runLabel`;
5. validate the selected `run id` through summary, audit and timings endpoints.

Do not use Docker volume deletion as the normal rebaseline path.

## Run Evidence

Runs started from `/dev/runtime` with `collectEvidence=true` write an evidence bundle under:

```text
docs/evidence/dev-runtime/<yyyyMMdd-HHmmss>-<runLabel>/
```

The bundle includes the request and response JSON, runtime summaries before/after, fixed diagnostic outputs, simulator stdout/stderr logs when captured, `summary.md`, and `post-run-report.md`. Diagnostics are read-only and use persisted runtime data; they do not recalculate risk or alert state.

## Scenario Diagnostics

The console includes:

- `Scenario definition details`, to inspect `control.scenario_definitions` parameters and simulator options.
- `Compare latest B vs C`, to compare the latest persisted `scenario_b` and `scenario_c` runs for the selected area.

`scenario_c` is intended for degraded or operational comparison. Running it with `degradationProfile=none` is allowed but shown with a warning because it may behave like a clean scenario. The current technical degradation profile is `missing-readings`, which deterministically omits a subset of published readings without changing scoring, alert policy, RabbitMQ topology or event contracts.

## Local readiness workload

M06 adds a small local HTTP readiness workload:

```powershell
.\scripts\performance\run-local-readiness-workload.ps1
```

It measures local API/web status codes and elapsed time for bounded probes, then writes `manifest.json`, `probes.json`, `measurements.csv/json`, `summary.csv/json` and `summary.md`.

The script does not run a load test, stress test, broker-depth test or end-to-end event-latency test. Treat its HTTP timings as measured local evidence only. Broker backlog, publisher timestamps and full per-event latency remain not instrumented until a separate runtime observability change adds those signals.
