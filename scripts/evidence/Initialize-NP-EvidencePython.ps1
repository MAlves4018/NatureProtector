<#+
.SYNOPSIS
Creates the repository-local Python environment used by report/evidence collectors.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$PythonLauncher = 'py',
    [switch]$Force
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$requirements = Join-Path $repo 'scripts/evidence/requirements-report.txt'
$venv = Join-Path $repo '.np_evidence_python_win'
$python = Join-Path $venv 'Scripts/python.exe'
if ($Force -and (Test-Path -LiteralPath $venv)) {
    Remove-Item -LiteralPath $venv -Recurse -Force
}
if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    & $PythonLauncher -3 -m venv $venv
    if ($LASTEXITCODE -ne 0) { throw "Failed to create evidence Python environment." }
}
& $python -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { throw "Failed to update pip in evidence Python environment." }
& $python -m pip install -r $requirements
if ($LASTEXITCODE -ne 0) { throw "Failed to install report/evidence requirements." }
& $python -c "import hcl2, jsonschema, matplotlib, psycopg, pytest, yaml; import sys; print('EVIDENCE_PYTHON_READY=' + sys.executable); print('PYTHON_VERSION=' + sys.version.replace(chr(10), ' ')); print('DEPENDENCIES_READY=pytest,matplotlib,PyYAML,jsonschema,python-hcl2,psycopg')"
if ($LASTEXITCODE -ne 0) { throw "Evidence Python validation failed." }
& $python -m pip freeze
if ($LASTEXITCODE -ne 0) { throw "Failed to inventory evidence Python dependencies." }
Write-Host "Evidence Python ready: $python"
