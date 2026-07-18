<#
.SYNOPSIS
Runs every test suite in the project: .NET backend, Python (unittest), frontend
(vitest), workspace script checks, and CI audit/quality gates.

.DESCRIPTION
Executes all test categories found in the repository and writes one log file
per suite under webUI/testSuiteResults/ plus a _summary.json.

By default Docker integration tests are skipped because they require
live PostgreSQL / RabbitMQ / InfluxDB containers.

Each category is run sequentially.  The script exits with a non‑zero code if
*any* test suite fails.

Log files ALWAYS contain the full command output, even on failure — including
stderr and exception details.
#>

param(
    [switch]$IncludeDockerIntegration,
    [switch]$NoRestore,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$resultsRoot = Join-Path $repoRoot "webUI" "testSuiteResults"
$python      = if ($IsWindows) { "python" } else { "python3" }

# ---- helpers ---------------------------------------------------------------
$passed   = [System.Collections.Generic.List[hashtable]]::new()
$failed   = [System.Collections.Generic.List[hashtable]]::new()

function Sanitize-Name($Name) {
    $n = $Name -replace '[\s:;\\/*?"<>|]+', '-'
    $n = $n.Trim('-')
    if ($n -eq '') { $n = 'unnamed' }
    $n.ToLowerInvariant()
}

function Invoke-TestSuite {
    <#
    .SYNOPSIS
    Runs a script block as a test suite, captures ALL output (stdout, stderr,
    errors, warnings, etc.), writes it to a log file, and records pass/fail.
    #>
    param(
        [string]$Name,
        [scriptblock]$Script
    )

    $slug = Sanitize-Name $Name
    $logFile = Join-Path $resultsRoot "$slug.log"
    $statusFile = Join-Path $resultsRoot "$slug.json"
    $output = $null
    $caught = $null

    Write-Host ""
    Write-Host "---------------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  $Name" -ForegroundColor Cyan
    Write-Host "---------------------------------------------------------------" -ForegroundColor Cyan

    # Run the script block, capturing ALL streams (including terminating errors).
    # We wrap in a try/catch so that any throw inside $Script is caught here
    # and $output is preserved.
    try {
        $output = & $Script *>&1
    } catch {
        $caught = $_
        # $output was already assigned with everything streamed before the throw
    }

    # Build the full output text — command output first, then any exception detail
    $parts = [System.Collections.Generic.List[string]]::new()
    if ($output) {
        $text = ($output | Out-String -Stream) -join "`r`n"
        if ($text) { $parts.Add($text) }
    }
    if ($caught) {
        $parts.Add("--- EXCEPTION ---")
        $parts.Add($caught.Exception.ToString())
        if ($caught.ErrorDetails) { $parts.Add("ErrorDetails: $($caught.ErrorDetails)") }
        if ($caught.ScriptStackTrace) { $parts.Add("Stack: $($caught.ScriptStackTrace)") }
    }
    $outputText = $parts -join "`r`n"

    # Print to console
    if ($outputText) { Write-Host $outputText }

    # ALWAYS write the log — success or failure, partial or full
    if ($outputText) {
        $outputText | Out-File -LiteralPath $logFile -Encoding utf8
    } else {
        "(no output)" | Out-File -LiteralPath $logFile -Encoding utf8
    }

    # Determine status
    $exitCode = if ($caught) { 1 } elseif ($LASTEXITCODE) { $LASTEXITCODE } else { 0 }
    if ($exitCode -ne 0) {
        $failed.Add(@{ Name = $Name; Slug = $slug; Status = "failed"; ExitCode = $exitCode })
        Write-Host "  [FAIL] $Name (exit $exitCode)" -ForegroundColor Red
    } else {
        $passed.Add(@{ Name = $Name; Slug = $slug; Status = "passed"; ExitCode = 0 })
        Write-Host "  [PASS] $Name" -ForegroundColor Green
    }

    $status = @{
        Name      = $Name
        Slug      = $slug
        Status    = if ($exitCode -ne 0) { "failed" } else { "passed" }
        ExitCode  = $exitCode
        Timestamp = (Get-Date -Format "o")
        LogFile   = $logFile
    }
    $status | ConvertTo-Json -Compress | Out-File -LiteralPath $statusFile -Encoding utf8
}

# ---- ensure results directory ----------------------------------------------
if (Test-Path $resultsRoot) { Remove-Item -LiteralPath $resultsRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

# ============================================================================
# 1.  .NET backend tests
# ============================================================================
Push-Location $repoRoot
try {
    $useEnvScript = Join-Path $repoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1"
    if (Test-Path $useEnvScript) { & $useEnvScript -Quiet | Out-Null }

    dotnet tool restore | Out-Null

    Invoke-TestSuite -Name "Backend-dotnet-build" -Script {
        $a = @("build", ".\NatureProtector.sln", "-c", "Release", "--nologo", "-v", "minimal")
        if ($NoRestore) { $a += "--no-restore" }
        dotnet @a 2>&1
    }

    Invoke-TestSuite -Name "Backend-unit-tests" -Script {
        $a = @("test", ".\NatureProtector.sln", "-c", "Release", "--nologo", "-v", "minimal", "-m:1")
        if ($NoRestore) { $a += "--no-restore" }
        if ($NoBuild)   { $a += "--no-build" }
        if (-not $IncludeDockerIntegration) { $a += "--filter"; $a += "Category!=DockerIntegration" }
        dotnet @a 2>&1
    }

    Invoke-TestSuite -Name "Backend-nuget-audit" -Script {
        & (Join-Path $repoRoot "scripts\ci\check-dotnet-audit.ps1") 2>&1
    }

} finally { Pop-Location }

# ============================================================================
# 2.  Python tests (unittest discover)
# ============================================================================
Push-Location $repoRoot
try {
    Invoke-TestSuite -Name "Python-tests-evidence" -Script {
        & $python -m unittest discover -s tests/evidence -t . -v 2>&1
    }

    Invoke-TestSuite -Name "Python-tests-operations" -Script {
        & $python -m unittest discover -s tests/operations -t . -v 2>&1
    }

    Invoke-TestSuite -Name "Python-tests-cloud" -Script {
        & $python -m unittest discover -s tests/cloud -t . -v 2>&1
    }

    Invoke-TestSuite -Name "Python-tools-tests" -Script {
        $allPassed = $true
        Get-ChildItem "$repoRoot\tools" -Filter "tests" -Directory | ForEach-Object {
            $toolDir = $_.Parent.FullName
            Write-Host "   -> $($_.Parent.Name)/tests"
            & $python -m unittest discover -s $_.FullName -t $toolDir -v 2>&1
            if ($LASTEXITCODE -ne 0) { $allPassed = $false }
        }
        if (-not $allPassed) { throw "Some tool tests failed" }
    }

} finally { Pop-Location }

# ============================================================================
# 3.  Frontend checks and tests
# ============================================================================
Push-Location (Join-Path $repoRoot "webUI")
try {
    Invoke-TestSuite -Name "Frontend-typecheck" -Script {
        npm run typecheck 2>&1
    }

    Invoke-TestSuite -Name "Frontend-lint" -Script {
        npm run lint 2>&1
    }

    Invoke-TestSuite -Name "Frontend-format-check" -Script {
        npm run format:check 2>&1
    }

    Invoke-TestSuite -Name "Frontend-vitest" -Script {
        npm test 2>&1
    }

    Invoke-TestSuite -Name "Frontend-audit-script" -Script {
        npm run test:audit-script 2>&1
    }

    Invoke-TestSuite -Name "Frontend-npm-audit" -Script {
        npm run audit:ci 2>&1
    }

    Invoke-TestSuite -Name "Frontend-build" -Script {
        npm run build 2>&1
    }

} finally { Pop-Location }

# ============================================================================
# 4.  PowerShell workspace script checks
# ============================================================================
Push-Location $repoRoot
try {
    Invoke-TestSuite -Name "Workspace-script-checks" -Script {
        & (Join-Path $repoRoot "scripts\tests\test-workspace-script.ps1") 2>&1
    }

} finally { Pop-Location }

# ============================================================================
# Summary
# ============================================================================
Write-Host ""
Write-Host "---------------------------------------------------------------" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "---------------------------------------------------------------" -ForegroundColor Cyan
foreach ($entry in $passed) { Write-Host "  [PASS] $($entry.Name)" -ForegroundColor Green }
foreach ($entry in $failed) { Write-Host "  [FAIL] $($entry.Name) (exit $($entry.ExitCode))" -ForegroundColor Red }
Write-Host ""

$overallStatus = if ($failed.Count -eq 0) { "passed" } else { "failed" }

$summary = @{
    Timestamp = (Get-Date -Format "o")
    Overall   = $overallStatus
    Passed    = $passed.Count
    Failed    = $failed.Count
    Results   = @(($passed + $failed) | Sort-Object Name)
}
$summary | ConvertTo-Json -Depth 3 | Out-File (Join-Path $resultsRoot "_summary.json") -Encoding utf8

if ($failed.Count -eq 0) {
    Write-Host "  All test suites passed!" -ForegroundColor Green
} else {
    Write-Host "  $($failed.Count) test suite(s) failed." -ForegroundColor Red
    Write-Host "  Check individual .log files in: $resultsRoot" -ForegroundColor Yellow
}

Write-Host "Results written to: $resultsRoot" -ForegroundColor Gray

if ($failed.Count -gt 0) { exit 1 } else { exit 0 }
