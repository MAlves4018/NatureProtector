param(
    [ValidateSet("B0", "B1", "B2")]
    [string]$Profile = "B0",
    [string]$Filter = "*",
    [string]$OutputRoot = "artifacts/performance",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$benchmarkProject = Join-Path $repoRoot "benchmarks\NatureProtector.Benchmarks\NatureProtector.Benchmarks.csproj"
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
    outputDirectory = $runDirectory
    project = $benchmarkProject
    scope = "BenchmarkDotNet microbenchmarks for candidate scoring, temporal classification, territorial mappings and event serialization. Results are engineering measurements, not scientific validation."
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $runDirectory "run-manifest.json") -Encoding UTF8

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Benchmark run complete. Output: $runDirectory"
