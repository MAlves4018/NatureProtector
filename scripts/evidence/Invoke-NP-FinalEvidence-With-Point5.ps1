[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$HeroRunId,
    [Parameter(Mandatory=$true)][string]$NominalRunId,
    [ValidateSet('Plan','Quick','Full','AnalyzeOnly')][string]$Mode = 'Full',
    [string]$BaselineId = 'NP-FINAL-20260718-S2',
    [string]$BaselineSha256 = '905e9bd711abfdab08ca47bbb37ae3db6c91ddb5033a22e9d2d20a51971e6496',
    [switch]$SkipBaseCampaign,
    [switch]$SkipPlaywright,
    [switch]$ContinueOnError
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$baseScript = Join-Path $repo 'scripts/evidence/Invoke-NP-FinalEvidence.ps1'
$point5Script = Join-Path $repo 'scripts/evidence/Invoke-NP-Point5ReportEvidence.ps1'

if (-not $SkipBaseCampaign) {
    if (-not (Test-Path -LiteralPath $baseScript)) {
        throw "Base evidence script not found: $baseScript"
    }

    & $baseScript `
        -Mode $Mode `
        -BaselineId $BaselineId `
        -ContinueOnError:$ContinueOnError
    $baseExit = $LASTEXITCODE
    if ($baseExit -ne 0 -and -not $ContinueOnError) {
        throw "Base evidence campaign failed with exit code $baseExit."
    }
}

if (-not (Test-Path -LiteralPath $point5Script)) {
    throw "Point 5 evidence script not found: $point5Script"
}

& $point5Script `
    -RepoRoot $repo `
    -HeroRunId $HeroRunId `
    -NominalRunId $NominalRunId `
    -BaselineId $BaselineId `
    -BaselineSha256 $BaselineSha256 `
    -SkipPlaywright:$SkipPlaywright `
    -ContinueOnError:$ContinueOnError

exit $LASTEXITCODE
