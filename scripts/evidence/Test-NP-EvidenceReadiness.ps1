<#
.SYNOPSIS
Checks whether formal evidence collection is allowed.
.DESCRIPTION
DryRun records readiness checks. Formal blocks unless ReadinessRoot exists and contains readiness handover.
.PARAMETER EvidenceRoot Root directory for evidence runs.
.PARAMETER RunId Run identifier under EvidenceRoot.
.PARAMETER Mode DryRun or Formal.
.PARAMETER ContinueOnFailure Continue after failures.
.EXAMPLE
powershell -File .\Test-NP-EvidenceReadiness.ps1 -EvidenceRoot C:\evidence -RunId run -Mode DryRun
.EXAMPLE
powershell -File .\Test-NP-EvidenceReadiness.ps1 -EvidenceRoot C:\evidence -RunId run -Mode Formal -ReadinessRoot C:\sys03
.OUTPUTS
10-summaries/READINESS-GATE.md.
.LIMITATIONS
Does not infer readiness from old runs.
.SECURITY
No secrets are read.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceRoot,[Parameter(Mandatory=$true)][string]$RunId,[ValidateSet('DryRun','Formal')][string]$Mode='DryRun',[switch]$ContinueOnFailure,[string]$ReadinessRoot)
. (Join-Path $PSScriptRoot 'Write-NP-EvidenceArtifact.ps1') -EvidenceRoot $EvidenceRoot -RunId $RunId -Mode $Mode -ContinueOnFailure:$ContinueOnFailure
$runRoot=Initialize-NPEvidenceRun -EvidenceRoot $EvidenceRoot -RunId $RunId
$status='DRY_RUN_READY_CHECK_ONLY'
$message='DryRun does not require readiness.'
if ($Mode -eq 'Formal') {
    if ([string]::IsNullOrWhiteSpace($ReadinessRoot) -or -not (Test-Path -LiteralPath $ReadinessRoot)) {
        $status='BLOCKED_MISSING_READINESS'
        $message='Formal mode requires an existing ReadinessRoot.'
        Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '10-summaries/READINESS-GATE.md' -Content "# Readiness Gate`n`nStatus: $status`n$message" | Out-Null
        if (-not $ContinueOnFailure) { throw $message }
    } else {
        $status='READINESS_ROOT_PRESENT'
        $message="ReadinessRoot exists: $ReadinessRoot"
    }
}
Write-NPEvidenceFile -RunRoot $runRoot -RelativePath '10-summaries/READINESS-GATE.md' -Content "# Readiness Gate`n`nStatus: $status`n$message" | Out-Null
Write-NPEvidenceManifest -RunRoot $runRoot | Out-Null
Write-NPEvidenceHashes -RunRoot $runRoot | Out-Null
