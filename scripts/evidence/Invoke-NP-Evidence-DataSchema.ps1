<#
.SYNOPSIS
Plans or captures database schema evidence.
.DESCRIPTION
DryRun records schema commands. Formal uses local read-only migration/schema commands only.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-DataSchema.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-DataSchema.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
03-data-schema summaries and logs.
.LIMITATIONS
Does not open connection strings or print secrets.
.SECURITY
No DSN is printed; outputs are redacted.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '03-data-schema/DATA-SCHEMA-SUMMARY.md' -Content "# Data Schema`n`nMode: $Mode`nFormal mode should use read-only schema and migration inspection." | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'migration-files-inventory' -FilePath 'powershell' -Arguments @('-NoProfile','-Command','Get-ChildItem src/NatureProtector.Infrastructure.Postgres/Migrations -File | Select-Object Name,Length') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
