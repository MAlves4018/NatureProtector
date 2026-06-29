[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineId,

    [string]$PythonExecutable = "python",
    [string]$RunId,
    [string]$Output,
    [string]$Dsn,
    [switch]$RequireLive
)

$ErrorActionPreference = "Stop"
$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepositoryRoot = (Resolve-Path (Join-Path $ScriptDirectory "../..")).Path
$Collector = Join-Path $ScriptDirectory "collect-database-architecture-evidence.py"
$Verifier = Join-Path $ScriptDirectory "verify-database-architecture-evidence.py"

$collectorArguments = @(
    $Collector,
    "--repo", $RepositoryRoot,
    "--baseline-id", $BaselineId
)
if ($RunId) { $collectorArguments += @("--run-id", $RunId) }
if ($Output) { $collectorArguments += @("--output", $Output) }
if ($Dsn) { $collectorArguments += @("--dsn", $Dsn) }
if ($RequireLive) { $collectorArguments += "--require-live" }

& $PythonExecutable @collectorArguments
if ($LASTEXITCODE -ne 0) {
    throw "Phase 3 collector failed with exit code $LASTEXITCODE."
}

if ($Output) {
    $EvidenceRoot = (Resolve-Path $Output).Path
}
elseif ($RunId) {
    $EvidenceRoot = Join-Path $RepositoryRoot "artifacts/report-evidence/$BaselineId/03-database/$RunId"
}
else {
    $LatestFile = Join-Path $RepositoryRoot "artifacts/report-evidence/$BaselineId/03-database/LATEST.txt"
    $EvidenceRoot = (Get-Content -Raw $LatestFile).Trim()
}

$verifierArguments = @($Verifier, $EvidenceRoot)
if ($RequireLive) { $verifierArguments += "--require-live" }
& $PythonExecutable @verifierArguments
if ($LASTEXITCODE -ne 0) {
    throw "Phase 3 evidence verification failed with exit code $LASTEXITCODE."
}
