# NatureProtector Evidence Harness

Status: `CONFIGURED_NOT_EXECUTED` until a Formal EVC-01 run is executed.

These scripts create run-scoped evidence folders and capture command outputs
without promoting claims automatically.

Dry-run example:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/evidence/Invoke-NP-Evidence-All.ps1 `
  -RepoRoot "C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector" `
  -RdRoot "C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\RepositorioDocumental" `
  -EvidenceRoot "C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\RepositorioDocumental\11-evidence-validation\evidence-campaigns\EVC-00-automation-harness" `
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
