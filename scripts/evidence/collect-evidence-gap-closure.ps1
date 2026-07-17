[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselineId,
    [string]$RunId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"),
    [string]$PythonExecutable = "python",
    [switch]$Overwrite
)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$output = Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/11-evidence-gap-closure/$RunId"
$argsList = @(
    (Join-Path $PSScriptRoot "collect-evidence-gap-closure.py"),
    "--repo", $repoRoot,
    "--baseline-id", $BaselineId,
    "--run-id", $RunId,
    "--output", $output
)
if ($Overwrite) { $argsList += "--overwrite" }
& $PythonExecutable @argsList
if ($LASTEXITCODE -ne 0) { throw "Phase 11 collector failed with exit code $LASTEXITCODE." }
& $PythonExecutable (Join-Path $PSScriptRoot "verify-evidence-gap-closure.py") $output
if ($LASTEXITCODE -ne 0) { throw "Phase 11 verifier failed with exit code $LASTEXITCODE." }
Write-Host "PHASE_11_OUTPUT=$output"
