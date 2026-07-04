# Phase 3 — Shared PowerShell tooling

## Status

`PHASE_3_SHARED_POWERSHELL_TOOLING_IMPLEMENTED`

This phase consolidates repeated repository-automation primitives without changing the public parameters or orchestration responsibility of the existing entrypoint scripts.

## Authority

The shared authority is:

```text
scripts/common/NatureProtector.Tooling.psd1
scripts/common/NatureProtector.Tooling.psm1
```

The module targets PowerShell 7 and exports fourteen explicitly prefixed functions:

- `Find-NpRepositoryRoot`
- `Read-NpDotEnv`
- `Get-NpConfigValue`
- `Get-NpRelativePath`
- `Get-NpPathUnderRoot`
- `Invoke-NpExternalCommand`
- `Test-NpTcpEndpoint`
- `Resolve-NpValidationPython`
- `Write-NpJsonFile`
- `Get-NpAbsolutePath`
- `Assert-NpPathExists`
- `Get-NpFreeTcpPort`
- `Get-NpCommandLineVersion`
- `Get-NpPercentileNearestRank`

The `Np` noun prefix prevents collisions with generic shell functions and makes shared calls distinguishable from script-local helpers.

## Migrated scope

Twenty-four scripts now import the module. The migration covers:

- local setup and baseline checks;
- InfluxDB local provisioning;
- workspace and local runtime launchers;
- secret-scan path handling;
- test inventory and mutation tooling;
- documentation generation path checks;
- G10.2/G10.3 evidence and validation-Python helpers;
- readiness and capacity evidence serialization;
- temporary TCP port allocation.

Entrypoint parameter blocks, default values, cloud confirmation tokens and workflow-facing paths remain unchanged.

## Behavior preservation

Variation that was previously hidden in copied functions is now explicit at each call site:

- repository discovery supplies its required sentinel paths;
- `.env` readers choose `Both`, `BothTrim`, `Double` or `None` quote handling;
- configuration lookups explicitly request environment-first precedence where it existed;
- Influx external-command calls preserve throw-on-start behavior;
- evidence writers preserve JSON depth and WhatIf bypass behavior;
- path helpers keep the two previous outside-root semantics as separate functions.

This avoids replacing several subtly different implementations with one ambiguous default.

## Guardrails

The machine-readable migration contract is:

```text
tools/script-audit/migration-contract.json
```

The static validator is:

```text
tools/script-audit/validate.py
```

It verifies:

- module and runtime-test presence;
- exact exported-function inventory;
- one resolvable import per migrated consumer;
- removal of the migrated local definitions;
- preservation of intentionally local exceptions;
- resolution of every `*-Np*` call to an exported function;
- conservative delimiter balance for the changed PowerShell files;
- the remaining duplicate function-name inventory.

The validator is integrated into `scripts/np.ps1 validate` as `script-tooling-authority`.

A runtime contract test is available at:

```text
scripts/tests/test-common-tooling.ps1
```

It is also integrated into `scripts/np.ps1 validate` as `powershell-tooling-runtime`. The test performs no cloud mutation and uses only temporary local files, a temporary TCP listener and the current PowerShell process.

## Duplication result

Measured over all `.ps1` and `.psm1` files:

| Metric | Before | After |
|---|---:|---:|
| Function definitions | 317 | 283 |
| Duplicate-name groups | 30 | 16 |
| Definitions participating in duplicate-name groups | 89 | 36 |
| Exact duplicate function groups | 15 | 1 |
| Definitions participating in exact duplicates | 32 | 2 |

The single remaining exact duplicate is `Add-Result` in two local diagnostic scripts. It depends on script-local result state and output conventions, so moving it into the side-effect-free module would increase coupling rather than reduce it.

## Intentional exclusions

The following were not centralized in this phase:

- `Test-TcpPort` in `workspace.ps1`, because it tests whether a port is free, not whether an endpoint is reachable;
- cloud-specific `Invoke-GcloudJson` variants, because their return and failure contracts differ;
- the two richer G10.2 validation-Python resolvers, because they perform runtime discovery, platform enforcement and evidence writing;
- release backup/restore helpers, which are candidates for a later release-tooling consolidation;
- test-reporting helpers that mutate script-local result collections;
- domain and workflow orchestration functions.

## Safety boundaries

This phase does not change:

- application production code;
- migrations, contracts, scoring, alerts or roles;
- Terraform, Kubernetes or cloud resources;
- `.env` or `.env.example`;
- workflow inputs or confirmation phrases;
- package versions;
- Git state.

## Required runtime gate

A PowerShell 7 environment must run:

```powershell
pwsh -NoProfile -File scripts/tests/test-common-tooling.ps1
pwsh -NoProfile -File scripts/np.ps1 validate
```

The static and Python-backed validation can run without PowerShell:

```bash
python tools/script-audit/validate.py --repo .
python -m unittest discover -s tools/script-audit/tests -p "test_*.py" -v
```
