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

It clears only runtime tables in `control`, `pipeline` and `projection`. It does not clear areas, sensors, scenarios, configuration versions or datasets.

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
