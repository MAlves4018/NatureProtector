# NatureProtector Evidence Harness

Status: `ACTIVE_VERSIONED_HARNESS`. Individual claims remain limited by the latest verified run and its evidence class.

These scripts create run-scoped evidence folders and capture command outputs
without promoting claims automatically.

Dry-run example:

```powershell
$RepoRoot = (Resolve-Path ".").Path
$RdRoot = Join-Path $RepoRoot "docs\RepositorioDocumental"
$EvidenceRoot = Join-Path $RdRoot "11-evidence-validation\evidence-campaigns\EVC-00-automation-harness"

powershell -ExecutionPolicy Bypass -File scripts/evidence/Invoke-NP-Evidence-All.ps1 `
  -RepoRoot $RepoRoot `
  -RdRoot $RdRoot `
  -EvidenceRoot $EvidenceRoot `
  -RunId "YYYYMMDDTHHMMSSZ-DRYRUN" `
  -Mode DryRun `
  -ContinueOnFailure `
  -SkipRuntime `
  -SkipScenarios `
  -SkipUi `
  -SkipObservability `
  -SkipCompression
```

Formal mode requires `-ReadinessRoot` and should be used only for EVC-01 after
SYS readiness is accepted.

The harness never runs cloud/deploy/production/destroy commands and writes run
artifacts only under `EvidenceRoot/RunId`.

## Phase 9 — NP_score exploratory validation

The Phase 9 collector reuses the report-evidence baseline/run structure and
produces retrospective model-validation evidence without claiming calibrated
probability or causal operational effectiveness.

```powershell
& .\scripts\evidence\collect-np-score-validation.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -RequireComplete
```

The `static`, `quality` and `full` profiles of
`run-report-evidence-campaign.ps1` include Phase 9. In `full`, current Phase
4–6 outputs are passed to the collector for conservative scenario-metric
import. Phase 9 always runs before Phase 7, allowing the report-integration
collector to promote only verified analytical tables, figures and claims. See
`docs/report/np-score-validation-evidence-collection.md`.

## Phase 10 — evidence intelligence and governance

The campaign wrappers run Phase 10 after campaign verification. It creates a cross-phase evidence
index, SHA-256 audit, phase coverage scorecard, claim lineage, figure inventory,
gap register and report-ready governance figures. It is read-only with respect
to prior phase outputs.

```powershell
& .\scripts\evidence\collect-evidence-intelligence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Overwrite
```

Manual screenshots should be registered with
`register-evidence-capture.py`. Two baselines can be compared with
`compare-evidence-campaigns.py`; the comparison reports deltas but deliberately
does not label them as improvements or regressions without metric-specific
rules.

## Phase 11 — evidence gap closure and readiness gate

Phase 11 runs after Phase 9 and before Phase 7. It formally admits the existing
historical B/C comparison when reconciliation and provenance checks pass,
separates achieved evidence coverage from closure readiness, and emits
platform-specific runbooks for the remaining current execution gaps.

```powershell
& .\scripts\evidence\collect-evidence-gap-closure.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Overwrite
```

A prepared command never counts as collected evidence. See
`docs/report/phase11-evidence-gap-closure.md`.
