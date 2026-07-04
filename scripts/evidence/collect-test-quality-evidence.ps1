<#+
.SYNOPSIS
Collects current test, coverage and frontend quality evidence for the report.

.DESCRIPTION
Runs the cross-platform Phase 2 collector and then verifies every generated
file and SHA-256 entry. The default run excludes DockerIntegration and
Playwright E2E tests. No Git or cloud commands are executed.
#>

[CmdletBinding()]
param(
    [string]$BaselineId,
    [string]$RunId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"),
    [string]$PythonExecutable = "python",
    [switch]$SkipBackend,
    [switch]$SkipFrontend,
    [switch]$SkipNpmCi,
    [switch]$IncludeE2E,
    [switch]$NoRestore,
    [switch]$NoBuild,
    [int]$TimeoutSeconds = 1800,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

if ([string]::IsNullOrWhiteSpace($BaselineId)) {
    $latestPath = Join-Path $repoRoot "artifacts\report-evidence\LATEST.txt"
    if (Test-Path -LiteralPath $latestPath) {
        $latestValue = (Get-Content -LiteralPath $latestPath -Raw).Trim()
        $BaselineId = Split-Path -Leaf $latestValue
    }
    else {
        $evidenceRoot = Join-Path $repoRoot "artifacts\report-evidence"
        if (Test-Path -LiteralPath $evidenceRoot) {
            $BaselineId = Get-ChildItem -LiteralPath $evidenceRoot -Directory |
                Where-Object { $_.Name -match '^\d{8}T\d{6}Z$' } |
                Sort-Object Name |
                Select-Object -Last 1 -ExpandProperty Name
        }
    }
}

if ([string]::IsNullOrWhiteSpace($BaselineId)) {
    throw "Could not infer the Phase 0 baseline ID. Pass -BaselineId explicitly."
}

$collector = Join-Path $PSScriptRoot "collect-test-quality-evidence.py"
$verifier = Join-Path $PSScriptRoot "verify-test-quality-evidence.py"
$outputRoot = Join-Path $repoRoot "artifacts\report-evidence\$BaselineId\02-tests\$RunId"

$arguments = @(
    $collector,
    "--repo", $repoRoot,
    "--baseline-id", $BaselineId,
    "--run-id", $RunId,
    "--timeout-seconds", $TimeoutSeconds
)
if ($SkipBackend) { $arguments += "--skip-backend" }
if ($SkipFrontend) { $arguments += "--skip-frontend" }
if ($SkipNpmCi) { $arguments += "--skip-npm-ci" }
if ($IncludeE2E) { $arguments += "--include-e2e" }
if ($NoRestore) { $arguments += "--no-restore" }
if ($NoBuild) { $arguments += "--no-build" }
if ($Quiet) { $arguments += "--quiet" }

& $PythonExecutable @arguments
$collectExit = $LASTEXITCODE

$summaryPath = Join-Path $outputRoot "phase2-summary.json"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Collector did not generate $summaryPath"
}

& $PythonExecutable $verifier --evidence-root $outputRoot
$verifyExit = $LASTEXITCODE

if ($collectExit -ne 0) { exit $collectExit }
exit $verifyExit
