[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$EvidenceRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Point5Evidence.Common.ps1')

$root = (Resolve-Path -LiteralPath $EvidenceRoot).Path
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param([string]$Name, [bool]$Passed, [string]$Detail)
    $checks.Add([pscustomobject][ordered]@{
        name = $Name
        status = $(if ($Passed) { 'PASS' } else { 'FAIL' })
        detail = $Detail
    })
}

$required = @(
    'manifest.json',
    '01-environment/environment.json',
    '02-api/hero/runtime-run.json',
    '02-api/hero/operation.json',
    '02-api/hero/audit.json',
    '02-api/hero/timings.json',
    '02-api/nominal/runtime-run.json',
    '02-api/nominal/operation.json',
    '02-api/nominal/audit.json',
    '03-derived/hero-accounting.json',
    '03-derived/hero-quality-eligibility.json',
    '03-derived/hero-risk-and-indices.json',
    '03-derived/hero-metrics.json',
    '03-derived/hero-assessments.json',
    '03-derived/hero-snapshots.json',
    '03-derived/hero-eligibility.json',
    '03-derived/hero-evidence-association.json',
    '03-derived/nominal-vs-hero-comparison.json',
    'command-ledger.csv',
    'evidence-index.csv',
    'failures.csv',
    'limitations.md',
    '06-report-material/coverage-matrix.json',
    'SHA256SUMS.txt'
)
foreach ($relative in $required) {
    Add-Check -Name "file:$relative" -Passed (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf) -Detail $relative
}

Add-Check -Name 'hashes' -Passed (Test-NPPoint5Hashes -Root $root) -Detail 'All SHA256SUMS entries must resolve and match.'

$heroRun = Get-Content -LiteralPath (Join-Path $root '02-api/hero/runtime-run.json') -Raw | ConvertFrom-Json
$nominalRun = Get-Content -LiteralPath (Join-Path $root '02-api/nominal/runtime-run.json') -Raw | ConvertFrom-Json
$heroAccounting = Get-Content -LiteralPath (Join-Path $root '03-derived/hero-accounting.json') -Raw | ConvertFrom-Json
$evidenceAssociation = Get-Content -LiteralPath (Join-Path $root '03-derived/hero-evidence-association.json') -Raw | ConvertFrom-Json
$environment = Get-Content -LiteralPath (Join-Path $root '01-environment/environment.json') -Raw | ConvertFrom-Json

$heroProfiles = @(Get-NPPoint5ResolvedProfiles -Run $heroRun)
$nominalProfiles = @(Get-NPPoint5ResolvedProfiles -Run $nominalRun)
Add-Check -Name 'hero-profile' -Passed ($heroProfiles -contains 'missing-readings') -Detail ($heroProfiles -join ', ')
Add-Check -Name 'nominal-profile' -Passed ($nominalProfiles -contains 'none') -Detail ($nominalProfiles -join ', ')
Add-Check -Name 'settled' -Passed ([bool]$heroAccounting.settled) -Detail "settled=$($heroAccounting.settled)"
Add-Check -Name 'accounting-invariants' -Passed (@($heroAccounting.invariants | Where-Object status -ne 'PASS').Count -eq 0) -Detail 'All declared accounting invariants must pass.'
Add-Check -Name 'evidence-wording' -Passed ($evidenceAssociation.PSObject.Properties.Name -contains 'directOperationAssociation' -and $evidenceAssociation.PSObject.Properties.Name -contains 'catalogAvailable') -Detail 'Direct structural association and catalog availability must be separate fields.'
Add-Check -Name 'npm-detection' -Passed ($environment.tools.npm.available -eq $true -and [int]$environment.tools.npm.exitCode -eq 0) -Detail "available=$($environment.tools.npm.available); exitCode=$($environment.tools.npm.exitCode); output=$($environment.tools.npm.output)"

$capturePath = Join-Path $root '04-screenshots/capture-register.json'
if (Test-Path -LiteralPath $capturePath) {
    $captures = @(Get-Content -LiteralPath $capturePath -Raw | ConvertFrom-Json)
    $requiredCaptureIds = @(
        'hero-configuration',
        'hero-identity',
        'hero-summary',
        'hero-scientific-metrics',
        'hero-accounting',
        'hero-quality',
        'hero-evidence',
        'hero-query-quality',
        'hero-vs-nominal-comparison',
        'hero-evidence-catalog',
        'hero-observability'
    )
    foreach ($captureId in $requiredCaptureIds) {
        $record = @($captures | Where-Object captureId -eq $captureId)
        Add-Check -Name "capture:$captureId" -Passed ($record.Count -eq 1) -Detail "matches=$($record.Count)"
    }

    foreach ($capture in $captures) {
        $imagePath = Join-Path (Join-Path $root '04-screenshots') ([string]$capture.file)
        $valid = (
            -not [string]::IsNullOrWhiteSpace([string]$capture.simulationRunId) -and
            [string]$capture.baselineSha256 -match '^[0-9a-f]{64}$' -and
            (Test-Path -LiteralPath $imagePath -PathType Leaf) -and
            ((Get-NPPoint5Sha256 -Path $imagePath) -eq [string]$capture.sha256)
        )
        Add-Check -Name "capture-metadata:$($capture.captureId)" -Passed $valid -Detail "run=$($capture.simulationRunId); resolution=$($capture.resolution)"
    }
} else {
    Add-Check -Name 'capture-register' -Passed $false -Detail '04-screenshots/capture-register.json is missing.'
}

$failed = @($checks | Where-Object status -eq 'FAIL')
$summary = [ordered]@{
    schemaVersion = 1
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    evidenceRoot = $root
    status = $(if ($failed.Count -eq 0) { 'PASS' } else { 'FAIL' })
    checked = $checks.Count
    failed = $failed.Count
    checks = @($checks)
}
$verificationRoot = Join-Path $root '07-verification'
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
Write-NPPoint5Json -Value $summary -Path (Join-Path $verificationRoot 'verification-summary.json')
Write-NPPoint5Csv -Rows @($checks) -Columns @('name','status','detail') -Path (Join-Path $verificationRoot 'verification-checks.csv')
Write-NPPoint5Hashes -Root $root

Write-Host "POINT5_VERIFICATION_STATUS=$($summary.status)" -ForegroundColor $(if ($summary.status -eq 'PASS') { 'Green' } else { 'Red' })
Write-Host "POINT5_VERIFICATION_CHECKED=$($summary.checked)"
Write-Host "POINT5_VERIFICATION_FAILED=$($summary.failed)"
if ($summary.status -eq 'PASS') { exit 0 }
exit 1
