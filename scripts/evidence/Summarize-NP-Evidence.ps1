<#
.SYNOPSIS
Summarizes an evidence run.
.DESCRIPTION
Builds manifest, hashes, ledger defaults, and a summary from captured logs.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Summarize-NP-Evidence.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Summarize-NP-Evidence.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal
.OUTPUTS
10-summaries, MANIFEST.csv, SHA256SUMS.txt.
.LIMITATIONS
Does not promote claims.
.SECURITY
Summaries are redacted before writing.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
Write-NPEvidenceLedgerDefaults -RunRoot $runRoot
$exitRows = Get-ChildItem -LiteralPath (Join-Path $runRoot 'logs') -Filter '*.exit-code.txt' -File -ErrorAction SilentlyContinue | ForEach-Object {
    [pscustomobject]@{ command=$_.BaseName.Replace('.exit-code',''); exit_code=(Get-Content -LiteralPath $_.FullName -Raw).Trim() }
}
$exitRows | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath (Join-Path $runRoot '10-summaries/EXIT-CODES.csv') -Encoding UTF8
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '10-summaries/SUMMARY.md' -Content "# Evidence Summary`n`nMode: $Mode`nCommands captured: $(@($exitRows).Count)`nNo claims promoted automatically." | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null

