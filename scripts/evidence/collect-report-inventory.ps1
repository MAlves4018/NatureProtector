<#
.SYNOPSIS
Collects the Phase 1 static repository inventory used by the report evidence workflow.

.DESCRIPTION
The wrapper does not run tests, Docker, databases, cloud operations or benchmarks.
It invokes the dependency-free Python collector and writes JSON, CSV, Markdown and
SHA-256 evidence under artifacts/report-evidence/<baseline>/01-inventory.
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$BaselineId,
    [string]$OutputRoot,
    [string]$PythonExecutable
)

$ErrorActionPreference = "Stop"

function Find-RepositoryRoot {
    $current = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $current) {
        if ((Test-Path -LiteralPath (Join-Path $current.FullName "NatureProtector.sln")) -and
            (Test-Path -LiteralPath (Join-Path $current.FullName "src"))) {
            return $current.FullName
        }
        $current = $current.Parent
    }
    throw "Could not locate NatureProtector repository root from $PSScriptRoot."
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Find-RepositoryRoot
}
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path

if ([string]::IsNullOrWhiteSpace($BaselineId)) {
    $latestPath = Join-Path $RepositoryRoot "artifacts\report-evidence\LATEST.txt"
    if (-not (Test-Path -LiteralPath $latestPath)) {
        throw "BaselineId is required because $latestPath does not exist."
    }
    $latestValue = (Get-Content -LiteralPath $latestPath -Raw).Trim()
    $BaselineId = Split-Path -Leaf $latestValue
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepositoryRoot "artifacts\report-evidence\$BaselineId\01-inventory"
}

$collector = Join-Path $PSScriptRoot "collect-report-inventory.py"
if (-not (Test-Path -LiteralPath $collector)) {
    throw "Collector not found: $collector"
}

$pythonCommand = $null
$pythonPrefixArguments = @()
if (-not [string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $pythonCommand = $PythonExecutable
}
elseif (Get-Command py -ErrorAction SilentlyContinue) {
    $pythonCommand = "py"
    $pythonPrefixArguments = @("-3")
}
elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
    $pythonCommand = "python3"
}
elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $pythonCommand = "python"
}
else {
    throw "Python 3 was not found. Pass -PythonExecutable with its full path."
}

$versionOutput = & $pythonCommand @pythonPrefixArguments -c "import sys; print('.'.join(map(str, sys.version_info[:3])))"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to execute Python: $pythonCommand"
}
$version = [version]$versionOutput.Trim()
if ($version -lt [version]"3.10") {
    throw "Python 3.10 or later is required; found $version."
}

& $pythonCommand @pythonPrefixArguments $collector `
    --repo $RepositoryRoot `
    --baseline-id $BaselineId `
    --output $OutputRoot

if ($LASTEXITCODE -ne 0) {
    throw "Phase 1 inventory collector failed with exit code $LASTEXITCODE."
}

$verifier = Join-Path $PSScriptRoot "verify-report-inventory.py"
& $pythonCommand @pythonPrefixArguments $verifier --inventory-root $OutputRoot
if ($LASTEXITCODE -ne 0) {
    throw "Phase 1 inventory verification failed with exit code $LASTEXITCODE."
}
