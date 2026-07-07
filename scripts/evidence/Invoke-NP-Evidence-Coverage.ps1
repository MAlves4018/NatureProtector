<#
.SYNOPSIS
Plans or runs coverage evidence collection.
.DESCRIPTION
DryRun records coverage intent. Formal runs available coverage command when supported.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Coverage.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Coverage.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
02-coverage summaries and logs.
.LIMITATIONS
Falls back to configured-not-executed when coverage tooling is absent.
.SECURITY
No secrets required.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '02-coverage/COVERAGE-SUMMARY.md' -Content "# Coverage`n`nMode: $Mode`nCoverage command is planned; formal collection may require repository tooling." | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'dotnet-test-coverage-plan' -FilePath 'dotnet' -Arguments @('test','NatureProtector.sln','--collect','XPlat Code Coverage') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
