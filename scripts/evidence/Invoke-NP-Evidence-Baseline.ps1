<#
.SYNOPSIS
Collects baseline git and toolchain evidence.
.DESCRIPTION
In DryRun, writes planned baseline commands. In Formal, executes safe read-only baseline commands.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after command failures.
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Baseline.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Invoke-NP-Evidence-Baseline.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal -ContinueOnFailure
.OUTPUTS
00-baseline files and command logs.
.LIMITATIONS
Read-only local commands only.
.SECURITY
No secrets are required; output is redacted before writing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$EvidenceRoot,
    [Parameter(Mandatory=$true)][string]$RunId,
    [ValidateSet('DryRun','Formal')][string]$Mode='DryRun',
    [switch]$ContinueOnFailure,
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot = Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '00-baseline/BASELINE-SUMMARY.md' -Content "# Baseline`n`nMode: $Mode`nRepoRoot: $RepoRoot`nTimestampUtc: $((Get-Date).ToUniversalTime().ToString('o'))" | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'git-status' -FilePath 'git' -Arguments @('status','--short','--branch') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'git-head' -FilePath 'git' -Arguments @('rev-parse','HEAD') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'dotnet-info' -FilePath 'dotnet' -Arguments @('--info') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Invoke-NPEvidenceCommand -RunRoot $runRoot -Name 'node-version' -FilePath 'node' -Arguments @('--version') -WorkingDirectory $RepoRoot -Mode $Mode -ContinueOnFailure:$ContinueOnFailure | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
