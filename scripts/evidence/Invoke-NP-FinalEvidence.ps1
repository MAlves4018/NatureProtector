<#
.SYNOPSIS
Orchestrates the existing NatureProtector evidence phases and writes Phase 13.

.DESCRIPTION
This command does not create a parallel evidence system. It reuses the existing
Phase 1-11 collectors, the E1-E6 portfolio, the runtime long-run proof and the
Playwright live-runtime capture. Phase 10 is refreshed last so that it indexes
Phase 13 as well.
#>
[CmdletBinding()]
param(
    [ValidateSet('Plan','Quick','Full','AnalyzeOnly')]
    [string]$Mode = 'Plan',

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,

    [Parameter(Mandatory = $true)]
    [string]$BaselineId,

    [string]$RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'),
    [string]$ApiBaseUrl = 'http://localhost:5254',
    [string]$PythonExecutable = 'python',
    [string]$PowerShellExecutable = 'pwsh',
    [string]$ConfigPath = 'config/evidence/final-execution.json',
    [switch]$Resume,
    [switch]$ContinueOnError,
    [switch]$UseExistingRuntime,
    [switch]$SkipInfrastructure,
    [switch]$KeepServicesRunning,
    [switch]$SkipLongRun,
    [switch]$SkipScreenshots,
    [switch]$SkipFinalPortfolio,
    [switch]$AllowReviewedCommands,
    [switch]$AcknowledgeNonProduction,
    [switch]$RequireLive,
    [switch]$IncludeE2E,
    [int]$BootstrapIterations = 500,
    [int]$RuntimeTimeoutSeconds = 240,
    [string]$InputPhase8Root,
    [string]$InputPortfolioRoot,
    [string]$InputLongRunRoot,
    [string]$InputScreenshotsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedRepo = (Resolve-Path -LiteralPath $RepoRoot).Path

# Normalise authentication aliases used by independent evidence phases.
if ($env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN) {
    if (-not $env:NP_RELIABILITY_AUTH_TOKEN) {
        $env:NP_RELIABILITY_AUTH_TOKEN = $env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN
    }
    if (-not $env:NP_PERFORMANCE_AUTH_TOKEN) {
        $env:NP_PERFORMANCE_AUTH_TOKEN = $env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN
    }
}

$env:NP_EVIDENCE_RUN_ID = $RunId

$defaultEvidencePython = Join-Path $resolvedRepo '.np_evidence_python_win/Scripts/python.exe'
if ($PythonExecutable -eq 'python' -and (Test-Path -LiteralPath $defaultEvidencePython -PathType Leaf)) {
    $PythonExecutable = $defaultEvidencePython
}

if ($Mode -in @('Quick','Full')) {
    & $PythonExecutable -c "import matplotlib" 2>$null
    if ($LASTEXITCODE -ne 0) {
        $setup = Join-Path $resolvedRepo 'scripts/evidence/Initialize-NP-EvidencePython.ps1'
        throw "Evidence Python lacks matplotlib. Run: pwsh -NoProfile -ExecutionPolicy Bypass -File `"$setup`" -RepoRoot `"$resolvedRepo`""
    }
}
$runner = Join-Path $resolvedRepo 'scripts/evidence/final/run_final_evidence.py'
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Phase 13 runner not found: $runner"
}

$modeMap = @{
    Plan = 'plan'
    Quick = 'quick'
    Full = 'full'
    AnalyzeOnly = 'analyze'
}

$arguments = @(
    $runner,
    '--repo', $resolvedRepo,
    '--mode', $modeMap[$Mode],
    '--baseline-id', $BaselineId,
    '--run-id', $RunId,
    '--api-base-url', $ApiBaseUrl,
    '--python', $PythonExecutable,
    '--pwsh', $PowerShellExecutable,
    '--config', $ConfigPath,
    '--bootstrap-iterations', [string]$BootstrapIterations,
    '--runtime-timeout-seconds', [string]$RuntimeTimeoutSeconds
)

if ($Resume) { $arguments += '--resume' }
if ($ContinueOnError) { $arguments += '--continue-on-error' }
if ($UseExistingRuntime) { $arguments += '--use-existing-runtime' }
if ($SkipInfrastructure) { $arguments += '--skip-infrastructure' }
if ($KeepServicesRunning) { $arguments += '--keep-services-running' }
if ($SkipLongRun) { $arguments += '--skip-long-run' }
if ($SkipScreenshots) { $arguments += '--skip-screenshots' }
if ($SkipFinalPortfolio) { $arguments += '--skip-final-portfolio' }
if ($AllowReviewedCommands) { $arguments += '--allow-reviewed-commands' }
if ($AcknowledgeNonProduction) { $arguments += '--acknowledge-non-production' }
if ($RequireLive) { $arguments += '--require-live' }
if ($IncludeE2E) { $arguments += '--include-e2e' }
if ($InputPhase8Root) { $arguments += @('--input-phase8-root', $InputPhase8Root) }
if ($InputPortfolioRoot) { $arguments += @('--input-portfolio-root', $InputPortfolioRoot) }
if ($InputLongRunRoot) { $arguments += @('--input-long-run-root', $InputLongRunRoot) }
if ($InputScreenshotsRoot) { $arguments += @('--input-screenshots-root', $InputScreenshotsRoot) }

& $PythonExecutable @arguments
exit $LASTEXITCODE
