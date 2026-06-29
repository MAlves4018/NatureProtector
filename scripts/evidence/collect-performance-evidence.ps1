[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineId,

    [string]$PythonExecutable = "python",
    [string]$RunId,
    [string]$Output,
    [string]$ApiBaseUrl = "http://localhost:5254",
    [switch]$RunHttp,
    [ValidateSet("Calibration", "B0", "B1", "B2")]
    [string]$HttpProfile = "Calibration",
    [switch]$IncludeWeb,
    [switch]$RunMicrobenchmarks,
    [ValidateSet("B0", "B1", "B2")]
    [string]$BenchmarkProfile = "B0",
    [string]$BenchmarkRunDirectory,
    [string]$HttpRunDirectory,
    [string]$SystemRunDirectory,
    [switch]$RequireHttp,
    [switch]$RequireMicrobenchmarks,
    [switch]$RequireSystem
)

$ErrorActionPreference = "Stop"
$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepositoryRoot = (Resolve-Path (Join-Path $ScriptDirectory "../..")).Path
$Collector = Join-Path $ScriptDirectory "collect-performance-evidence.py"
$Verifier = Join-Path $ScriptDirectory "verify-performance-evidence.py"

$collectorArguments = @(
    $Collector,
    "--repo", $RepositoryRoot,
    "--baseline-id", $BaselineId,
    "--api-base-url", $ApiBaseUrl,
    "--http-profile", $HttpProfile,
    "--benchmark-profile", $BenchmarkProfile
)
if ($RunId) { $collectorArguments += @("--run-id", $RunId) }
if ($Output) { $collectorArguments += @("--output", $Output) }
if ($RunHttp) { $collectorArguments += "--run-http" }
if ($IncludeWeb) { $collectorArguments += "--include-web" }
if ($RunMicrobenchmarks) { $collectorArguments += "--run-microbenchmarks" }
if ($BenchmarkRunDirectory) { $collectorArguments += @("--benchmark-run-directory", $BenchmarkRunDirectory) }
if ($HttpRunDirectory) { $collectorArguments += @("--http-run-directory", $HttpRunDirectory) }
if ($SystemRunDirectory) { $collectorArguments += @("--system-run-directory", $SystemRunDirectory) }
if ($RequireHttp) { $collectorArguments += "--require-http" }
if ($RequireMicrobenchmarks) { $collectorArguments += "--require-microbenchmarks" }
if ($RequireSystem) { $collectorArguments += "--require-system" }

& $PythonExecutable @collectorArguments
if ($LASTEXITCODE -ne 0) {
    throw "Phase 5 collector failed with exit code $LASTEXITCODE."
}

if ($Output) {
    $EvidenceRoot = (Resolve-Path $Output).Path
}
elseif ($RunId) {
    $EvidenceRoot = Join-Path $RepositoryRoot "artifacts/report-evidence/$BaselineId/05-performance/$RunId"
}
else {
    $LatestFile = Join-Path $RepositoryRoot "artifacts/report-evidence/$BaselineId/05-performance/LATEST.txt"
    $EvidenceRoot = (Get-Content -Raw $LatestFile).Trim()
}

$verifierArguments = @($Verifier, $EvidenceRoot)
if ($RequireHttp) { $verifierArguments += "--require-http" }
if ($RequireMicrobenchmarks) { $verifierArguments += "--require-microbenchmarks" }
if ($RequireSystem) { $verifierArguments += "--require-system" }

& $PythonExecutable @verifierArguments
if ($LASTEXITCODE -ne 0) {
    throw "Phase 5 evidence verification failed with exit code $LASTEXITCODE."
}
