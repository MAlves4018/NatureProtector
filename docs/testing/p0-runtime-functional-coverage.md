---
id: NP-TEST-P0-RUNTIME-COVERAGE
status: CURRENT
owner: Miguel Alves
audience: engineering, QA, delivery
source_of_truth: scripts/acceptance/Invoke-NP-P0RuntimeCoverage.ps1, scripts/acceptance/verify_scenario_profile_matrix.py and config/acceptance/p0-runtime-coverage.json
last_verified_against: NatureProtector Phase 3 P0 runtime coverage implementation
last_verified_at: 2026-07-22
review_triggers: simulator profile, runtime API, role, diagnostic, alert, observability, evidence or shutdown changes
---

# P0 runtime functional coverage

This harness closes the highest-priority runtime gaps left by the nominal B/C campaign. It starts from a clean project-scoped local environment, creates only temporary identities, executes current APIs and persisted-data checks, and produces a closed machine-readable verdict.

It is selected automatically by the `Functional` and `Full` final-acceptance profiles after the existing B/C stage and before controlled P3 and reset/recovery.

## Canonical command

Direct execution:

```powershell
.\scripts\acceptance\Invoke-NP-P0RuntimeCoverage.ps1
```

Through the final runner:

```powershell
.\scripts\acceptance\Invoke-NP-FinalAcceptance.ps1 -Profile Functional
```

`-SkipBuild` is available only when the unchanged workspace has already been prepared successfully. `-KeepRuntime` is a debugging aid and deliberately causes the acceptance result to fail because shutdown cleanliness was not proved.

## Preconditions and destructive scope

The harness requires `pwsh`, Python, .NET, Node/npm and Docker. It uses the local development credentials resolved from environment variables or `.env` and refuses an output location outside a run-scoped child of `artifacts`.

Before execution it:

1. refuses to overlap tracked NatureProtector runtime processes;
2. runs `np.ps1 prepare-local` unless `-SkipBuild` is selected;
3. runs `np.ps1 clean-local`, which removes only the project Compose containers, networks and volumes;
4. starts the local infrastructure and application surfaces;
5. checks API, Prevention Host and webUI availability.

The campaign therefore resets local runtime data. It does not prune unrelated Docker resources.

## Runtime coverage

### Authentication and RBAC

The campaign proves:

- valid administrator login and authenticated identity;
- invalid credentials return `401`;
- administrator capabilities needed by the harness;
- create/read/update/delete lifecycle for a temporary role;
- create/read/update/delete lifecycle for a temporary user;
- membership visibility from both user and role endpoints;
- a real login for each seeded role in the versioned matrix;
- required capabilities present and forbidden capabilities absent;
- one endpoint allowed by that role and one endpoint denied with `403`;
- removal of the role followed by a fresh login no longer grants the removed capabilities;
- cleanup of every temporary user and role, including best-effort cleanup after failure.

The seeded roles covered are `Sim`, `Pipeline`, `QA`, `Operations` and `ReleaseApprover`. Admin is used only as the controlled campaign authority.

### Twelve degradation profiles

The matrix executes every profile currently declared by `SimulationDegradationProfiles.cs` with a fixed seed:

```text
none
missing-readings
noise
bias
drift
stuck-value
outlier
clipping/range
lag/delay
duplicate
out-of-order
retry-transient
```

For each run, the harness captures request, operation and run correlation, audit, timings, accepted readings, inbox rows, processing attempts, cycle observations and the actual Simulator publishing order from the current run logs.

`verify_scenario_profile_matrix.py` compares persisted values with a `none` baseline and applies profile-specific invariants. Examples include deterministic omission repetition, signed bias, drift slope, flatline behaviour, material outliers, observed clipping saturation, persisted lag, duplicate delivery with idempotent acceptance, reversed delivery order and transient retry followed by success.

`clipping/range` has a supplemental `outlier + clipping/range` run so the campaign fails when caps are configured but no saturation is ever observed.

### Diagnostics

The harness obtains the diagnostic catalogue from the running API and compares it exactly with the generated repository catalogue. It then executes every returned diagnostic ID and validates the response contract.

Before the diagnostic sweep it creates a current controlled `scenario_c` run. This prevents `compare-latest-b-vs-c` from passing using historical data or returning only one side of the comparison. The semantic check requires both `scenario_b` and `scenario_c` rows.

### Alerts

A bounded high-risk `scenario_b` run is executed after deterministic cleanup. The campaign verifies:

- at least one `area-risk-high` transition;
- only allowed `Open` or `Resolved` states;
- agreement between the public active-alert API and the prepared diagnostic;
- no duplicate active alert code.

### Observability and evidence

The campaign validates:

- the operational health component set for PostgreSQL, RabbitMQ, InfluxDB and Grafana;
- explicit allowlisted operational statuses rather than merely rejecting `Unhealthy`;
- RabbitMQ queue metrics from the application API;
- RabbitMQ Management `overview`, `queues` and `bindings` endpoints;
- authenticated direct InfluxDB health and a run-scoped `accepted_readings` query;
- direct Grafana database health response;
- a populated runtime evidence catalogue;
- an allowlisted evidence item downloaded through the authenticated HTTP endpoint.

This phase proves service health, run-scoped accepted-reading persistence and evidence access. It does not replace the separate bounded performance gate or broader dashboard/retention analysis.

### Shutdown cleanliness

After all runs, no `NatureProtector.Simulator.Host` process may remain. The finalizer then calls project-scoped `np stop` and `np down`, checks tracked runtime processes and confirms that no running `np-*` container remains.

A selected shutdown check that cannot run is a failure, not a warning or partial pass.

## Output contract

Each run writes below:

```text
artifacts/p0-runtime-coverage/<UTC-run-id>/
```

The root contains:

```text
run-spec.json
acceptance-result.json
summary.json
SUMMARY.md
tests.csv
commands.csv
blockers.csv
evidence-manifest.csv
hashes.sha256
api/
database/
scenarios/
rbac/
diagnostics/
alerts/
observability/
shutdown/
logs/
```

The native result is exactly one of:

```text
P0_RUNTIME_FUNCTIONAL_COVERAGE_PASS
P0_RUNTIME_FUNCTIONAL_COVERAGE_FAIL
```

The parent final-acceptance runner additionally checks that process exit code and delegated result agree.

## Current proof boundary

The harness and its static/synthetic contract tests are implemented in Phase 3. The analysis environment used for this phase did not contain PowerShell, .NET or Docker, so no new live campaign is claimed here. A current `Functional` or `Full` run on the supported Windows/Docker environment remains required before final delivery evidence can be classified as `PROVED`.
