param(
    [Parameter(Mandatory = $true)] [string]$BaselineId,
    [string]$PythonExecutable = "python",
    [string]$RunId,
    [string]$Output,
    [string]$ApiBaseUrl = "http://localhost:5254",
    [switch]$ExecuteP3,
    [switch]$AcknowledgeNonProduction,
    [string]$P3RunLabel,
    [int]$TimeoutSeconds = 300,
    [string]$AuditDirectory,
    [switch]$RequireP3,
    [switch]$RequireAudit
)
$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$argsList = @('--repo', $repoRoot, '--baseline-id', $BaselineId, '--api-base-url', $ApiBaseUrl, '--timeout-seconds', $TimeoutSeconds)
if ($RunId) { $argsList += @('--run-id', $RunId) }
if ($Output) { $argsList += @('--output', $Output) }
if ($ExecuteP3) { $argsList += '--execute-p3' }
if ($AcknowledgeNonProduction) { $argsList += '--acknowledge-non-production' }
if ($P3RunLabel) { $argsList += @('--p3-run-label', $P3RunLabel) }
if ($AuditDirectory) { $argsList += @('--audit-directory', $AuditDirectory) }
if ($RequireP3) { $argsList += '--require-p3' }
if ($RequireAudit) { $argsList += '--require-audit' }
& $PythonExecutable (Join-Path $scriptDir 'collect-reliability-evidence.py') @argsList
if ($LASTEXITCODE -ne 0) { throw "Phase 6 collector failed with exit code $LASTEXITCODE." }
if ($Output) { $evidenceRoot = $Output }
elseif ($RunId) { $evidenceRoot = Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/06-reliability/$RunId" }
else { $evidenceRoot = (Get-Content -Raw (Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/06-reliability/LATEST.txt")).Trim() }
$verifyArgs = @($evidenceRoot)
if ($RequireP3) { $verifyArgs += '--require-p3' }
if ($RequireAudit) { $verifyArgs += '--require-audit' }
& $PythonExecutable (Join-Path $scriptDir 'verify-reliability-evidence.py') @verifyArgs
if ($LASTEXITCODE -ne 0) { throw "Phase 6 verification failed with exit code $LASTEXITCODE." }
