param(
    [ValidateSet("B0", "B1", "B2")]
    [string]$Profile = "B0",
    [string]$Filter = "*",
    [string]$OutputRoot = "artifacts/performance",
    [switch]$NoBuild,
    [int]$TimeoutSeconds = 0,
    [string]$SummarizeOnlyDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$benchmarkProject = Join-Path $repoRoot "benchmarks\NatureProtector.Benchmarks\NatureProtector.Benchmarks.csproj"
function Write-BenchmarkSummary {
    param(
        [string]$Directory,
        [string]$ProfileName,
        [string]$BenchmarkFilter,
        [string]$Status,
        [int]$ExitCode,
        [bool]$TimedOut
    )

    $reports = @(Get-ChildItem -LiteralPath $Directory -Recurse -Filter "*-report-brief.json" -ErrorAction SilentlyContinue)
    $rows = [System.Collections.Generic.List[object]]::new()

    foreach ($report in $reports) {
        try {
            $payload = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
            foreach ($benchmark in @($payload.Benchmarks)) {
                $gen0PerThousandOperations = if ($benchmark.Memory.TotalOperations -gt 0) {
                    [double]$benchmark.Memory.Gen0Collections * 1000 / [double]$benchmark.Memory.TotalOperations
                }
                else {
                    $null
                }

                $rows.Add([pscustomobject]@{
                    report = $report.FullName.Substring($Directory.Length).TrimStart("\", "/").Replace("\", "/")
                    type = $benchmark.Type
                    method = $benchmark.Method
                    parameters = $benchmark.Parameters
                    meanNanoseconds = [double]$benchmark.Statistics.Mean
                    standardErrorNanoseconds = [double]$benchmark.Statistics.StandardError
                    standardDeviationNanoseconds = [double]$benchmark.Statistics.StandardDeviation
                    confidenceIntervalMargin = [string]$benchmark.Statistics.ConfidenceInterval.Margin
                    gen0CollectionsPer1000Operations = $gen0PerThousandOperations
                    gen1Collections = [double]$benchmark.Memory.Gen1Collections
                    gen2Collections = [double]$benchmark.Memory.Gen2Collections
                    allocatedBytesPerOperation = [double]$benchmark.Memory.BytesAllocatedPerOperation
                }) | Out-Null
            }
        }
        catch {
            $rows.Add([pscustomobject]@{
                report = $report.FullName.Substring($Directory.Length).TrimStart("\", "/").Replace("\", "/")
                type = "<parse-error>"
                method = $_.Exception.Message
                parameters = ""
                meanNanoseconds = $null
                standardErrorNanoseconds = $null
                standardDeviationNanoseconds = $null
                confidenceIntervalMargin = ""
                gen0CollectionsPer1000Operations = $null
                gen1Collections = $null
                gen2Collections = $null
                allocatedBytesPerOperation = $null
            }) | Out-Null
        }
    }

    if ($ExitCode -eq 0 -and $reports.Count -gt 0 -and $rows.Count -gt 0 -and $Status -ne "ready") {
        $Status = "ready"
    }

    $summary = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        profile = $ProfileName
        filter = $BenchmarkFilter
        status = $Status
        exitCode = $ExitCode
        timedOut = $TimedOut
        reportCount = $reports.Count
        benchmarkCount = $rows.Count
        scope = "BenchmarkDotNet microbenchmark summary. B0 is a smoke profile; B1/B2 provide deeper local engineering measurements when bounded by operator-selected filters/timeouts. These results are not scientific validation."
        benchmarks = @($rows)
    }

    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Directory "summary.json") -Encoding UTF8

    $benchmarkLines = if ($rows.Count -eq 0) {
        "- No BenchmarkDotNet brief reports were produced."
    }
    else {
        $rows | ForEach-Object {
            "- $($_.type).$($_.method) $($_.parameters): mean(ns)=$($_.meanNanoseconds), stdErr(ns)=$($_.standardErrorNanoseconds), stdDev(ns)=$($_.standardDeviationNanoseconds), error=$($_.confidenceIntervalMargin), gen0/1k ops=$($_.gen0CollectionsPer1000Operations), allocated(bytes/op)=$($_.allocatedBytesPerOperation)"
        } | Out-String
    }

    @"
# Benchmark summary

Generated: $($summary.generatedAtUtc)

Profile: $ProfileName
Filter: $BenchmarkFilter
Status: $Status
Timed out: $TimedOut
Reports: $($reports.Count)
Benchmarks: $($rows.Count)

## Scope

$($summary.scope)

## Results

$benchmarkLines
"@ | Set-Content -LiteralPath (Join-Path $Directory "summary.md") -Encoding UTF8
}

if (-not [string]::IsNullOrWhiteSpace($SummarizeOnlyDirectory)) {
    $summaryDirectory = (Resolve-Path $SummarizeOnlyDirectory).Path
    $manifestPath = Join-Path $summaryDirectory "run-manifest.json"
    $summaryProfile = $Profile
    $summaryFilter = $Filter

    if (Test-Path -LiteralPath $manifestPath) {
        $existingManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $summaryProfile = $existingManifest.profile
        $summaryFilter = $existingManifest.filter
    }

    Write-BenchmarkSummary `
        -Directory $summaryDirectory `
        -ProfileName $summaryProfile `
        -BenchmarkFilter $summaryFilter `
        -Status "ready" `
        -ExitCode 0 `
        -TimedOut $false

    Write-Host "Benchmark summary regenerated. Output: $summaryDirectory"
    exit 0
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $repoRoot (Join-Path $OutputRoot "benchmarks-$Profile-$timestamp")

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$arguments = @(
    "run",
    "--project", $benchmarkProject,
    "-c", "Release"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$arguments += @(
    "--",
    "--profile", $Profile,
    "--filter", $Filter,
    "--artifacts", $runDirectory
)

$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    profile = $Profile
    filter = $Filter
    timeoutSeconds = $TimeoutSeconds
    outputDirectory = $runDirectory
    project = $benchmarkProject
    scope = "BenchmarkDotNet microbenchmarks for candidate scoring, temporal classification, territorial mappings and event serialization. Results are engineering measurements, not scientific validation."
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $runDirectory "run-manifest.json") -Encoding UTF8

$exitCode = 0
$timedOut = $false

if ($TimeoutSeconds -gt 0) {
    $stdoutPath = Join-Path $runDirectory "benchmark.stdout.log"
    $stderrPath = Join-Path $runDirectory "benchmark.stderr.log"
    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $arguments `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru

    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $timedOut = $true
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000) | Out-Null
        $exitCode = 124
    }
    else {
        $exitCode = $process.ExitCode
    }
}
else {
    & dotnet @arguments
    $exitCode = $LASTEXITCODE
}

$status = if ($exitCode -eq 0) {
    "ready"
}
elseif ($timedOut) {
    "timeout"
}
else {
    "failed"
}

Write-BenchmarkSummary `
    -Directory $runDirectory `
    -ProfileName $Profile `
    -BenchmarkFilter $Filter `
    -Status $status `
    -ExitCode $exitCode `
    -TimedOut $timedOut

if ($exitCode -ne 0) {
    exit $exitCode
}

Write-Host "Benchmark run complete. Output: $runDirectory"
