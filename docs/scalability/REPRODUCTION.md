# Reproduction

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/docker/Start-LocalInfrastructure.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -SkipBuild
python scripts/autoscaling/verify-scaling-experiment.py artifacts/scalability-final/live-autoscaling-normalized-matrix.csv --output artifacts/scalability-final/live-autoscaling-verification-normalized.json
python scripts/autoscaling/analyze-capacity.py artifacts/scalability-final/live-autoscaling-normalized-matrix.csv --output-dir artifacts/scalability-final/live-capacity-analysis-normalized
python scripts/autoscaling/compile-scalability-report.py --evidence-root artifacts/acceptance/matrices/autoscaling-runtime/20260727T082908Z --matrix artifacts/scalability-final/live-autoscaling-normalized-matrix.csv --output-root artifacts/scalability-final
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -Mode FixedReplica -SkipBuild
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -Mode Bottleneck -SkipBuild
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -Mode Autoscaling -SkipBuild
python scripts/autoscaling/compile-scalability-final.py
```

## Temporal S16-R1 protocol

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -Mode TemporalCapacity -SkipBuild
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -Mode TemporalComparison -SkipBuild
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1 -Mode TemporalInflux -SkipBuild
python scripts/autoscaling/compile-scalability-temporal-comparison.py
```

Use `-FixedReplicas`, `-FixedRates`, `-TemporalWorkloads` and repetition parameters only for exploratory or resumed subsets. A report-ready S16-R1 run requires complete raw rows for capacity refinement, the 63 workload/topology/repetition temporal matrix and the Influx enabled/disabled confirmation.
