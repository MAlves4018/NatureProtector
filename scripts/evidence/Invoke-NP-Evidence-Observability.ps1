<#
.SYNOPSIS
Plans or captures safe observability evidence.
.DESCRIPTION
DryRun records observability checks. Formal captures safe summaries only.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Observability.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Observability.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
08-observability summaries and logs.
.LIMITATIONS
Authenticated observability remains manual/semi-manual unless explicitly authorized.
.SECURITY
Does not print tokens or dashboard credentials.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '08-observability/OBSERVABILITY-SUMMARY.md' -Content "# Observability`n`nMode: $Mode`nAuthenticated observability is not automatically claimed." | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'observability-safe-plan' -FilePath 'powershell' -Arguments @('-NoProfile','-Command','Write-Output observability_safe_summary_planned') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
