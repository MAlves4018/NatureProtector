<#
.SYNOPSIS
Orchestrates NatureProtector testmaxing gates without redefining test authority.

.DESCRIPTION
This harness delegates to the repository's existing coverage, frontend,
functional-validation, Docker-integration, route and mutation commands. It writes
run state, a command ledger and mode-level verdicts so focused coverage cannot be
reported as global coverage by accident.
#>

[CmdletBinding()]
param(
    [ValidateSet("Baseline", "Backend", "Frontend", "Functional", "Integration", "Routes", "Mutation", "Reliability", "Full", "Resume")]
    [string]$Mode = "Resume",
    [string]$OutputRoot = ".\artifacts\testmaxing",
    [string]$BackendCoverageRoot = ".\artifacts\coverage\testmaxing-backend",
    [string]$FunctionalRunRoot = "",
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$PlanOnly,
    [switch]$NoMutationRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NpRepositoryRoot {
    $candidate = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    if (-not (Test-Path -LiteralPath (Join-Path $candidate "NatureProtector.sln"))) {
        throw "Unable to resolve NatureProtector repository root from $PSScriptRoot."
    }

    return $candidate
}

function Resolve-OutputPath {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $Root $Path))
}

function ConvertTo-CommandText {
    param([string]$FileName, [string[]]$Arguments)

    $escaped = @($Arguments | ForEach-Object {
        if ($_ -match '\s|"') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    })

    return ($FileName + " " + ($escaped -join " ")).Trim()
}

function New-LedgerRow {
    param(
        [string]$RunId,
        [string]$Mode,
        [string]$Step,
        [string]$Command,
        [string]$Status,
        [int]$ExitCode,
        [datetime]$StartedAtUtc,
        [datetime]$FinishedAtUtc,
        [string]$LogPath,
        [string]$Notes
    )

    [pscustomobject]@{
        run_id = $RunId
        mode = $Mode
        step = $Step
        command = $Command
        status = $Status
        exit_code = $ExitCode
        started_at_utc = $StartedAtUtc.ToString("o")
        finished_at_utc = $FinishedAtUtc.ToString("o")
        duration_seconds = [Math]::Round(($FinishedAtUtc - $StartedAtUtc).TotalSeconds, 3)
        log_path = $LogPath
        notes = $Notes
    }
}

function Invoke-TestMaxingCommand {
    param(
        [string]$RunId,
        [string]$Mode,
        [string]$Step,
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$LogPath,
        [System.Collections.Generic.List[object]]$Ledger
    )

    $commandText = ConvertTo-CommandText -FileName $FileName -Arguments $Arguments
    $startedAt = (Get-Date).ToUniversalTime()
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null

    if ($PlanOnly) {
        "PLAN_ONLY: $commandText" | Set-Content -LiteralPath $LogPath -Encoding UTF8
        $finishedAt = (Get-Date).ToUniversalTime()
        $Ledger.Add((New-LedgerRow -RunId $RunId -Mode $Mode -Step $Step -Command $commandText -Status "PLANNED" -ExitCode 0 -StartedAtUtc $startedAt -FinishedAtUtc $finishedAt -LogPath $LogPath -Notes "PlanOnly; command not executed.")) | Out-Null
        return 0
    }

    Push-Location $WorkingDirectory
    try {
        $output = & $FileName @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $output | Out-String | Set-Content -LiteralPath $LogPath -Encoding UTF8
    $finishedAt = (Get-Date).ToUniversalTime()
    $status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
    $Ledger.Add((New-LedgerRow -RunId $RunId -Mode $Mode -Step $Step -Command $commandText -Status $status -ExitCode $exitCode -StartedAtUtc $startedAt -FinishedAtUtc $finishedAt -LogPath $LogPath -Notes "")) | Out-Null
    return $exitCode
}

function Get-BackendCoverageSummary {
    param([string]$RepoRoot)

    $summaryPath = Join-Path $RepoRoot "artifacts\coverage\megazord-b2-backend\backend-integral\Summary.txt"
    if (-not (Test-Path -LiteralPath $summaryPath)) {
        return $null
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw
    $line = [regex]::Match($summary, 'Line coverage:\s+(?<value>[0-9.]+)%')
    $branch = [regex]::Match($summary, 'Branch coverage:\s+(?<value>[0-9.]+)%')
    $method = [regex]::Match($summary, 'Method coverage:\s+(?<value>[0-9.]+)%')

    return [pscustomobject]@{
        source = $summaryPath
        line_percent = if ($line.Success) { [decimal]$line.Groups["value"].Value } else { $null }
        branch_percent = if ($branch.Success) { [decimal]$branch.Groups["value"].Value } else { $null }
        method_percent = if ($method.Success) { [decimal]$method.Groups["value"].Value } else { $null }
        scope = "backend-integral"
        focused_is_global = $false
    }
}

function Get-FrontendCoverageSummary {
    param([string]$RepoRoot)

    $summaryPath = Join-Path $RepoRoot "webUI\coverage\coverage-summary.json"
    if (-not (Test-Path -LiteralPath $summaryPath)) {
        return $null
    }

    $json = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    return [pscustomobject]@{
        source = $summaryPath
        line_percent = [decimal]$json.total.lines.pct
        branch_percent = [decimal]$json.total.branches.pct
        method_percent = [decimal]$json.total.functions.pct
        scope = "frontend"
        focused_is_global = $false
    }
}

function Write-State {
    param(
        [string]$Path,
        [object]$State
    )

    $State | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding UTF8
}

$repoRoot = Resolve-NpRepositoryRoot
$outputRootPath = Resolve-OutputPath -Root $repoRoot -Path $OutputRoot
$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$runRoot = Join-Path $outputRootPath $runId
$logsRoot = Join-Path $runRoot "logs"
New-Item -ItemType Directory -Force -Path $runRoot, $logsRoot | Out-Null

$statePath = Join-Path $outputRootPath "TESTMAXING_STATE.json"
$ledgerPath = Join-Path $outputRootPath "TESTMAXING_ITERATION_LEDGER.csv"
$ledger = [System.Collections.Generic.List[object]]::new()
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$branch = (& git -C $repoRoot branch --show-current).Trim()
$startedAt = (Get-Date).ToUniversalTime()
$overallStatus = "PASS"
$notes = [System.Collections.Generic.List[string]]::new()

if ($Mode -eq "Resume") {
    if (Test-Path -LiteralPath $statePath) {
        $notes.Add("Existing state loaded from $statePath.") | Out-Null
    }
    else {
        $notes.Add("No previous TESTMAXING_STATE.json existed; initialized a resumable state file.") | Out-Null
    }
}

if ($Mode -eq "Baseline" -or $Mode -eq "Full" -or $Mode -eq "Resume") {
    $backendSummary = Get-BackendCoverageSummary -RepoRoot $repoRoot
    $frontendSummary = Get-FrontendCoverageSummary -RepoRoot $repoRoot
    if ($null -eq $backendSummary) {
        $notes.Add("Backend integral coverage summary not found under artifacts/coverage/megazord-b2-backend.") | Out-Null
    }
    if ($null -eq $frontendSummary) {
        $notes.Add("Frontend coverage summary not found under webUI/coverage.") | Out-Null
    }

    @($backendSummary, $frontendSummary) |
        Where-Object { $null -ne $_ } |
        Export-Csv -LiteralPath (Join-Path $runRoot "coverage-baseline-snapshot.csv") -NoTypeInformation -Encoding UTF8
}

if ($Mode -eq "Backend" -or $Mode -eq "Full") {
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\tests\generate-coverage-report.ps1", "-OutputRoot", $BackendCoverageRoot)
    if ($NoRestore) { $arguments += "-NoRestore" }
    if ($NoBuild) { $arguments += "-NoBuild" }
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "backend-coverage-integral" -FileName "pwsh" -Arguments $arguments -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "backend-coverage.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($Mode -eq "Frontend" -or $Mode -eq "Full") {
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "frontend-coverage" -FileName "npm" -Arguments @("--prefix", "webUI", "run", "test:coverage", "--", "--run") -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "frontend-coverage.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($Mode -eq "Routes" -or $Mode -eq "Full") {
    $filter = "FullyQualifiedName~AuthorizationMatrixTests|FullyQualifiedName~ProgramSmokeTests|FullyQualifiedName~ControlPlaneApiTests|FullyQualifiedName~OpenApiSemanticTests"
    $arguments = @("test", "tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj", "-c", "Release", "--logger", "trx;LogFileName=testmaxing-routes.trx", "--filter", $filter)
    if ($NoRestore) { $arguments += "--no-restore" }
    if ($NoBuild) { $arguments += "--no-build" }
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "route-contract-tests" -FileName "dotnet" -Arguments $arguments -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "routes.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($Mode -eq "Functional" -or $Mode -eq "Full") {
    $functionalRoot = if ([string]::IsNullOrWhiteSpace($FunctionalRunRoot)) { Join-Path $runRoot "functional-smoke" } else { Resolve-OutputPath -Root $repoRoot -Path $FunctionalRunRoot }
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\validation\Invoke-LocalFunctionalValidation.ps1", "-Smoke", "-RunRoot", $functionalRoot)
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "functional-smoke" -FileName "pwsh" -Arguments $arguments -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "functional-smoke.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($Mode -eq "Integration" -or $Mode -eq "Full") {
    $arguments = @("test", "tests\NatureProtector.IntegrationTests\NatureProtector.IntegrationTests.csproj", "-c", "Release", "--logger", "trx;LogFileName=testmaxing-integration.trx", "--filter", "Category=DockerIntegration")
    if ($NoRestore) { $arguments += "--no-restore" }
    if ($NoBuild) { $arguments += "--no-build" }
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "docker-integration-tests" -FileName "dotnet" -Arguments $arguments -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "integration.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($Mode -eq "Reliability" -or $Mode -eq "Full") {
    $arguments = @("scripts\reliability\run-controlled-validation-p3.py", "--help")
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "reliability-harness-smoke" -FileName "python" -Arguments $arguments -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "reliability.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($Mode -eq "Mutation" -or $Mode -eq "Full") {
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\tests\run-mutation.ps1", "-Profile", "Smoke", "-OutputRoot", ".\artifacts\mutation\testmaxing")
    if ($NoMutationRun -or $PlanOnly) { $arguments += "-NoRun" }
    $exitCode = Invoke-TestMaxingCommand -RunId $runId -Mode $Mode -Step "mutation-smoke" -FileName "pwsh" -Arguments $arguments -WorkingDirectory $repoRoot -LogPath (Join-Path $logsRoot "mutation.log") -Ledger $ledger
    if ($exitCode -ne 0) { $overallStatus = "FAIL" }
}

if ($ledger.Count -eq 0) {
    $ledgerStartedAt = (Get-Date).ToUniversalTime()
    $ledgerFinishedAt = (Get-Date).ToUniversalTime()
    $ledger.Add((New-LedgerRow `
        -RunId $runId `
        -Mode $Mode `
        -Step "state-refresh" `
        -Command "no external command" `
        -Status "PASS" `
        -ExitCode 0 `
        -StartedAtUtc $ledgerStartedAt `
        -FinishedAtUtc $ledgerFinishedAt `
        -LogPath "" `
        -Notes "State-only mode; no coverage, functional or mutation command executed.")) | Out-Null
}

$ledger | Export-Csv -LiteralPath $ledgerPath -NoTypeInformation -Encoding UTF8

$finishedAt = (Get-Date).ToUniversalTime()
$state = [ordered]@{
    runId = $runId
    mode = $Mode
    branch = $branch
    head = $head
    startedAtUtc = $startedAt.ToString("o")
    finishedAtUtc = $finishedAt.ToString("o")
    status = $overallStatus
    planOnly = [bool]$PlanOnly
    outputRoot = $outputRootPath
    runRoot = $runRoot
    ledger = $ledgerPath
    backendFocusedIsGlobal = $false
    globalCoverageRequires = "backend-integral plus frontend total coverage; backend-focused is diagnostic only"
    notes = $notes
}
Write-State -Path $statePath -State $state

if ($overallStatus -ne "PASS") {
    throw "Testmaxing mode '$Mode' failed. See $ledgerPath and $logsRoot."
}

Write-Host "TESTMAXING_STATUS=$overallStatus"
Write-Host "TESTMAXING_MODE=$Mode"
Write-Host "TESTMAXING_RUN_ID=$runId"
Write-Host "TESTMAXING_STATE=$statePath"
Write-Host "TESTMAXING_LEDGER=$ledgerPath"
