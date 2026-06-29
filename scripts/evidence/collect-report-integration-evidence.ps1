param(
    [Parameter(Mandatory=$true)][string]$BaselineId,
    [string]$RepositoryRoot = (Resolve-Path "$PSScriptRoot\..\.."),
    [string]$RunId,
    [string]$PythonExecutable = "python"
)
$ErrorActionPreference = "Stop"
$collector = Join-Path $RepositoryRoot "scripts/evidence/collect-report-integration-evidence.py"
$verifier = Join-Path $RepositoryRoot "scripts/evidence/verify-report-integration-evidence.py"
$args = @($collector, "--repo", $RepositoryRoot, "--baseline-id", $BaselineId)
if ($RunId) { $args += @("--run-id", $RunId) }
& $PythonExecutable @args
if ($LASTEXITCODE -ne 0) { throw "Phase 7 collector failed: $LASTEXITCODE" }
$verifyArgs = @($verifier, "--repo", $RepositoryRoot, "--baseline-id", $BaselineId)
if ($RunId) { $verifyArgs += @("--run-id", $RunId) }
& $PythonExecutable @verifyArgs
if ($LASTEXITCODE -ne 0) { throw "Phase 7 verification failed: $LASTEXITCODE" }
