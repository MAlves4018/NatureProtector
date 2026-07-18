<#
.SYNOPSIS
Runs a local push-CI cycle and optionally reports results to the backoffice.
.DESCRIPTION
1. Gets commit SHA, generates deterministic operation ID.
2. Runs all test suites or uses existing results.
3. Determines overall status.
4. If NP_OPERATIONS_CALLBACK_URL is set, POSTs callback.
5. Prints summary.
.PARAMETER ReportOnly
Skip running tests; use existing results from webUI/testSuiteResults/.
.PARAMETER NoRestore
Pass -NoRestore to run-all-tests.ps1.
.PARAMETER NoBuild
Pass -NoBuild to run-all-tests.ps1.
.PARAMETER CallbackOnly
Only POST callback; skip test execution.
.EXAMPLE
.\scripts\ci\run-local-push-ci.ps1 -NoRestore -NoBuild
.EXAMPLE
.\scripts\ci\run-local-push-ci.ps1 -ReportOnly
#>

param(
    [switch]$ReportOnly,
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$CallbackOnly
)

$ErrorActionPreference = "Stop"
$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$resultsRoot = Join-Path $repoRoot "webUI" "testSuiteResults"
$python      = if ($IsWindows) { "python" } else { "python3" }

function Get-CommitSha {
    $sha = & git -C $repoRoot rev-parse HEAD 2>$null
    if (-not $sha) { return "0000000000000000000000000000000000000000" }
    $sha.Trim()
}

function New-DeterministicUuid($Sha) {
    $result = & $python -c "import uuid; print(uuid.uuid5(uuid.NAMESPACE_DNS, 'push-ci-$Sha'))" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "UUID generation failed: $result" }
    $result.Trim()
}

function Read-Summary {
    $path = Join-Path $resultsRoot "_summary.json"
    if (-not (Test-Path $path)) { return $null }
    Get-Content -Path $path -Raw -Encoding utf8 | ConvertFrom-Json
}

function Invoke-Callback($OperationId, $Status, $DetailJson) {
    $url = [Environment]::GetEnvironmentVariable("NP_OPERATIONS_CALLBACK_URL")
    $secret = [Environment]::GetEnvironmentVariable("NP_OPERATIONS_CALLBACK_SECRET")
    if ([string]::IsNullOrEmpty($url)) {
        Write-Host "  NP_OPERATIONS_CALLBACK_URL not set -- skipping callback" -ForegroundColor Yellow
        return
    }
    $body = @{ operationId = $OperationId; status = $Status; detail = $DetailJson; artifacts = @() } |
        ConvertTo-Json -Depth 5 -Compress
    Write-Host "  POST $url" -ForegroundColor Gray
    try {
        $headers = @{}
        if (-not [string]::IsNullOrEmpty($secret)) {
            $secretBytes = [Text.Encoding]::UTF8.GetBytes($secret)
            $bodyBytes   = [Text.Encoding]::UTF8.GetBytes($body)
            $hmac = [Security.Cryptography.HMACSHA256]::new($secretBytes)
            $hashStr = [BitConverter]::ToString($hmac.ComputeHash($bodyBytes)).Replace("-","").ToLowerInvariant()
            $headers["X-Operations-Callback-Signature-256"] = $hashStr
        }
        Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType "application/json" -Headers $headers | Out-Null
        Write-Host "  Callback accepted." -ForegroundColor Green
    } catch { Write-Host "  Callback failed: $($_.Exception.Message)" -ForegroundColor Red }
}

# ---- main -------------------------------------------------------------------
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "  Local Push CI" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan

$sha = Get-CommitSha
$operationId = New-DeterministicUuid -Sha $sha
Write-Host "  Commit SHA : $sha" -ForegroundColor Gray
Write-Host "  OperationId: $operationId" -ForegroundColor Gray

if ($CallbackOnly) {
    Write-Host "  CallbackOnly mode -- skipping test execution" -ForegroundColor Yellow
} elseif (-not $ReportOnly) {
    Write-Host ""; Write-Host "--- Running all test suites ---" -ForegroundColor Cyan
    $args = @()
    if ($NoRestore) { $args += "-NoRestore" }
    if ($NoBuild)   { $args += "-NoBuild" }
    & (Join-Path $repoRoot "scripts\tests\run-all-tests.ps1") @args 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Host "  Some suites failed -- continuing" -ForegroundColor Yellow }
} else {
    Write-Host "  ReportOnly mode -- using existing results" -ForegroundColor Yellow
}

Write-Host ""; Write-Host "--- Consolidating results ---" -ForegroundColor Cyan
$summary = Read-Summary
if (-not $summary) {
    Write-Host "  No _summary.json found at $resultsRoot" -ForegroundColor Red
    Write-Host "  Run run-all-tests.ps1 first or omit -ReportOnly" -ForegroundColor Yellow
    exit 1
}

$overallStatus = if ($summary.Overall -eq "passed") { "Succeeded" } else { "Failed" }
$detailJson = if ($summary.Results) { $summary.Results | ConvertTo-Json -Depth 5 -Compress } else { "{}" }

Write-Host "  Overall: $overallStatus" -ForegroundColor $(if ($overallStatus -eq 'Succeeded') { 'Green' } else { 'Red' })
Write-Host "  Passed: $($summary.Passed) | Failed: $($summary.Failed)" -ForegroundColor Gray

foreach ($r in ($summary.Results | Sort-Object Name)) {
    $color = if ($r.Status -eq 'passed') { 'Green' } else { 'Red' }
    Write-Host "    [$($r.Status.ToUpper())] $($r.Name)" -ForegroundColor $color
}

Write-Host ""; Write-Host "--- Callback ---" -ForegroundColor Cyan
Invoke-Callback -OperationId $operationId -Status $overallStatus -DetailJson $detailJson

Write-Host ""; Write-Host "Done." -ForegroundColor Gray
if ($overallStatus -eq "Failed") { exit 1 }
