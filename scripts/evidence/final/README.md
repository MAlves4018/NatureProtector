# Final integrated evidence execution — Phase 13

This directory is an orchestration layer for the evidence system already present in
`scripts/evidence`. It does not replace the Phase 1–11 collectors and it does not
recalculate report claims independently.

The public entrypoint is:

```powershell
.\scripts\evidence\Invoke-NP-FinalEvidence.ps1 `
  -Mode Plan `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ"
```

Create or repair the repository-local evidence environment before invoking the
orchestrator:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\evidence\Initialize-NP-EvidencePython.ps1
```

The bootstrap installs the pinned requirements from
`scripts/evidence/requirements-report.txt`, validates every required import,
prints the Python version and captures `pip freeze` for provenance.

The orchestrator reuses:

- the Phase 8 report campaign;
- the existing E1–E6 final portfolio;
- the runtime long-run proof;
- the Playwright live-runtime test and the existing capture registrar;
- the Phase 10 evidence intelligence collector, executed last.

Outputs are written to:

```text
artifacts/report-evidence/<baseline-id>/13-final-execution/<run-id>/
```

Execution logs and resume state are deliberately kept outside the immutable phase
package:

```text
artifacts/evidence-orchestration/<baseline-id>/<run-id>/
```

This separation prevents the orchestration process from invalidating the Phase 13
SHA-256 manifest after collection.

## Result semantics

The collector and verifier are fail-closed. A passing final execution requires a
command ledger and every required ledger row must have `PASS`. Failed, blocked,
missing or unknown command states cannot be promoted by a pre-existing summary.
`PASS_WITH_LIMITATIONS` is distinct from `PASS` and cannot satisfy strict live
verification.
