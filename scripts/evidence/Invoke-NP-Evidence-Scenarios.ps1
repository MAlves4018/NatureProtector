<#
.SYNOPSIS
Plans or runs scenario A/B/C evidence.
.DESCRIPTION
DryRun records scenario execution plan. Formal uses local scenario runner only.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Scenarios.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Scenarios.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
06-scenarios summaries and logs.
.LIMITATIONS
Requires local runtime dependencies in Formal mode.
.SECURITY
No secrets are required.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '06-scenarios/SCENARIOS-SUMMARY.md' -Content "# Scenarios`n`nMode: $Mode`nScenarios planned: scenario_a, scenario_b, scenario_c." | Out-Null
foreach ($scenario in @('scenario_a','scenario_b','scenario_c')) {
    $specPath = Join-Path $runRoot "artifacts/$scenario.json"
    $spec = [ordered]@{
        version = "1.0"
        areaCode = "proenca-a-nova"
        scenarioCode = $scenario
        sensorCount = 2
        numberOfCycles = 1
        intervalSeconds = 1
        seed = 20260706
        startTimestamp = [DateTimeOffset]::UtcNow.ToString("o")
        degradationProfile = "none"
        collectEvidence = $false
        waitForCompletion = $true
        timeoutSeconds = 240
        allowParallelRun = $false
        runLabel = "$RunId-$scenario"
    }

    $spec | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $specPath -Encoding UTF8

    Invoke-NPEvidenceCommand -RunRoot $runRoot -Name "scenario-$scenario" -FilePath 'powershell' -Arguments @('-ExecutionPolicy','Bypass','-File','scripts/scenarios/run-scenario.ps1','-SpecPath',$specPath,'-PollIntervalSeconds','1') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
}
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
