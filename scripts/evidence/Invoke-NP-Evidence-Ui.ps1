<#
.SYNOPSIS
Plans or runs frontend/UI evidence.
.DESCRIPTION
DryRun records typecheck/test/build/E2E commands. Formal runs local WebUI commands.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Ui.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Ui.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
07-ui summaries and logs.
.LIMITATIONS
Manual visual judgement remains outside automated evidence.
.SECURITY
No credentials are required for dry-run/unit commands.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
$webRoot=Join-Path $RepoRoot 'webUI'
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '07-ui/UI-SUMMARY.md' -Content "# UI`n`nMode: $Mode`nManual screenshot selection remains manual." | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'npm-typecheck' -FilePath 'npm' -Arguments @('run','typecheck') -WorkingDirectory $webRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'npm-test' -FilePath 'npm' -Arguments @('test') -WorkingDirectory $webRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'npm-build' -FilePath 'npm' -Arguments @('run','build') -WorkingDirectory $webRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'npm-e2e' -FilePath 'npm' -Arguments @('run','test:e2e') -WorkingDirectory $webRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
