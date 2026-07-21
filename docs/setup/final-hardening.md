# Final hardening local authority

This document describes the reproducible local hardening entry points. It does not replace the existing setup, runtime, coverage, functional, release or CI authorities. It records how to run them with a resumable ledger and without reusing stale evidence as a PASS.

## Canonical entry points

- `scripts/np.ps1` remains the clone-to-run authority for local setup and runtime.
- `scripts/validation/Invoke-LocalFunctionalValidation.ps1` remains the local functional validation harness.
- `scripts/hardening/Invoke-NP-FinalHardening.ps1` is the thin hardening orchestrator. It writes fingerprints, command ledgers and gate results, then delegates to existing authorities.
- `scripts/release/Invoke-FinalRepositoryFreeze.ps1` is the pre-freeze wrapper. It supports `Plan` and `Verify` in this mission. `Execute` is intentionally blocked until after merge and explicit owner confirmation.

## Clean-room validation

`Invoke-LocalFunctionalValidation.ps1 -CleanRoom` now creates a real Git clone before it mutates local setup files or starts Docker/runtime services.

Example:

```powershell
$root = "C:\temp\np-functional-clean-room"
pwsh -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\validation\Invoke-LocalFunctionalValidation.ps1 `
  -CleanRoom `
  -Smoke `
  -RunRoot $root
```

The clean-room path:

1. resolves the current source `HEAD`;
2. runs `git clone --no-local` into a short temporary path by default, to avoid Windows path-length failures while keeping evidence under `RunRoot`;
3. checks out the exact source `HEAD`;
4. verifies `git status --porcelain` is empty;
5. records an explicit short NuGet package cache path through `NP_NUGET_PACKAGES`;
6. runs the local setup sequence through `scripts/np.ps1`.

The setup sequence includes `prepare-local`, so dependencies are restored from declared lockfiles before the runtime smoke starts.

## Hardening orchestration

Use an external output root when collecting durable evidence:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\hardening\Invoke-NP-FinalHardening.ps1 `
  -Mode VerifyOnly `
  -OutputRoot "C:\temp\np-final-hardening"
```

The orchestrator produces:

- `REPRODUCIBILITY_FINGERPRINT.json`;
- `PHASE_STATE.json`;
- `GATE_RESULTS.csv`;
- `COMMAND_LEDGER.csv`;
- `SHA256SUMS.txt`.

Modes that are declared but not implemented as delegated authorities cannot pass in `-Enforce` mode. This prevents placeholder PASS states.

## Freeze rehearsal

Plan:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\release\Invoke-FinalRepositoryFreeze.ps1 `
  -Mode Plan
```

Verify:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\release\Invoke-FinalRepositoryFreeze.ps1 `
  -Mode Verify
```

`Execute` is blocked by policy in this mission. It is reserved for after the PR is merged and the owner explicitly authorizes final tagging/release actions.

## Protected local file

`docs/report/LaTeXReport_template.zip` is local-only evidence input. It must remain untracked and must not be included in hardening archives, source archives, commits or release packages.
