[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaselineId,
    [string]$RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'),
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$PythonExecutable = 'python',
    [switch]$RequireReady,
    [switch]$Overwrite
)
$ErrorActionPreference = 'Stop'
$output = Join-Path $RepoRoot "artifacts\report-evidence\$BaselineId\10-evidence-intelligence\$RunId"
$collector = Join-Path $PSScriptRoot 'collect-evidence-intelligence.py'
$verifier = Join-Path $PSScriptRoot 'verify-evidence-intelligence.py'
$args = @($collector, '--repo', $RepoRoot, '--baseline-id', $BaselineId, '--run-id', $RunId, '--output', $output)
if ($Overwrite) { $args += '--overwrite' }
& $PythonExecutable @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$verifyArgs = @($verifier, $output)
if ($RequireReady) { $verifyArgs += '--require-ready' }
& $PythonExecutable @verifyArgs
exit $LASTEXITCODE
