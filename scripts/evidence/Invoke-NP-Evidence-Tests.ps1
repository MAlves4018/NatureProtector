<#
.SYNOPSIS
Plans or runs repository build/test evidence collection.
.DESCRIPTION
DryRun records restore/build/test commands. Formal runs local non-cloud test commands.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Tests.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Tests.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
01-build-test logs and summaries.
.LIMITATIONS
Does not deploy or purge runtime dependencies.
.SECURITY
Outputs are redacted for secret-like values.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '01-build-test/TESTS-SUMMARY.md' -Content "# Build/Test`n`nMode: $Mode`nNo claims promoted automatically." | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'dotnet-restore' -FilePath 'dotnet' -Arguments @('restore','NatureProtector.sln','--configfile','NuGet.Config') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'dotnet-build' -FilePath 'dotnet' -Arguments @('build','NatureProtector.sln','--no-restore','-m:1') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'dotnet-test' -FilePath 'dotnet' -Arguments @('test','NatureProtector.sln','--no-restore','--no-build','-m:1') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
