[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselineId,
    [string]$RunId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"),
    [string]$PythonExecutable = "python",
    [string]$ConfigPath = "config/evidence/np-score-validation.json",
    [string[]]$RuntimeEvidenceRoot = @(),
    [int]$BootstrapIterations = 0,
    [switch]$Overwrite,
    [switch]$RequireComplete
)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$output = Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/09-np-score-validation/$RunId"
$collector = Join-Path $PSScriptRoot "collect-np-score-validation.py"
$verifier = Join-Path $PSScriptRoot "verify-np-score-validation.py"
$argsList = @(
    $collector, "--repo", $repoRoot, "--baseline-id", $BaselineId,
    "--run-id", $RunId, "--config", $ConfigPath, "--output", $output
)
foreach ($root in $RuntimeEvidenceRoot) { $argsList += @("--runtime-evidence-root", $root) }
if ($BootstrapIterations -gt 0) { $argsList += @("--bootstrap-iterations", $BootstrapIterations) }
if ($Overwrite) { $argsList += "--overwrite" }
& $PythonExecutable @argsList
if ($LASTEXITCODE -ne 0) { throw "Phase 9 collector failed with exit code $LASTEXITCODE." }
$verifyArgs = @($verifier, $output)
if ($RequireComplete) { $verifyArgs += "--require-complete" }
& $PythonExecutable @verifyArgs
if ($LASTEXITCODE -ne 0) { throw "Phase 9 verifier failed with exit code $LASTEXITCODE." }
$latest = Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/09-np-score-validation/LATEST.txt"
$RunId | Set-Content -LiteralPath $latest -Encoding UTF8
Write-Host "PHASE_9_OUTPUT=$output"
