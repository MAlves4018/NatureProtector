<#
.SYNOPSIS
Plans or captures local API evidence.
.DESCRIPTION
DryRun records API checks. Formal performs local HTTP checks only.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Api.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Api.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
05-api summaries and logs.
.LIMITATIONS
Requires local runtime in Formal mode.
.SECURITY
Does not store bearer tokens; redacts output.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,[string]$ApiBaseUrl='http://127.0.0.1:5254')
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '05-api/API-SUMMARY.md' -Content "# API`n`nMode: $Mode`nApiBaseUrl: $ApiBaseUrl" | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'api-health-ready' -FilePath 'powershell' -Arguments @('-NoProfile','-Command',"Invoke-WebRequest -UseBasicParsing '$ApiBaseUrl/health/ready' | Select-Object StatusCode") -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
