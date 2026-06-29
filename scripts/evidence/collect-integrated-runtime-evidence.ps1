[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineId,

    [string]$RepositoryRoot = "",
    [string]$PythonExecutable = "python",
    [string]$RunId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"),
    [string]$ApiBaseUrl = "http://localhost:5254",
    [switch]$Live,
    [switch]$RequireLive,
    [switch]$ResetRuntime,
    [string]$PostgresDsnEnvironmentVariable = "",
    [switch]$RequireDatabaseTrace
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "../..")).Path
}
else {
    $RepositoryRoot = (Resolve-Path $RepositoryRoot).Path
}

$output = Join-Path $RepositoryRoot "artifacts/report-evidence/$BaselineId/04-runtime/$RunId"
$collector = Join-Path $RepositoryRoot "scripts/evidence/collect-integrated-runtime-evidence.py"
$verifier = Join-Path $RepositoryRoot "scripts/evidence/verify-integrated-runtime-evidence.py"

$arguments = @(
    $collector,
    "--repo", $RepositoryRoot,
    "--baseline-id", $BaselineId,
    "--run-id", $RunId,
    "--output", $output,
    "--api-base-url", $ApiBaseUrl
)
if ($Live -or $RequireLive) { $arguments += "--live" }
if ($RequireLive) { $arguments += "--require-live" }
if ($ResetRuntime) { $arguments += "--reset-runtime" }
if (-not [string]::IsNullOrWhiteSpace($PostgresDsnEnvironmentVariable)) {
    $arguments += @("--postgres-dsn-env", $PostgresDsnEnvironmentVariable)
}

& $PythonExecutable @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Phase 4 collector failed with exit code $LASTEXITCODE."
}

$verifyArguments = @($verifier, $output)
if ($RequireLive) { $verifyArguments += "--require-live" }
if ($RequireDatabaseTrace) { $verifyArguments += "--require-database-trace" }
& $PythonExecutable @verifyArguments
if ($LASTEXITCODE -ne 0) {
    throw "Phase 4 verifier failed with exit code $LASTEXITCODE."
}

Write-Host "PHASE_4_OUTPUT=$output"
