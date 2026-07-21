<#
.SYNOPSIS
Prepares deterministic local dependencies for a NatureProtector clone.

.DESCRIPTION
Restores the repository-local .NET dependency graph and installs the webUI
from package-lock.json. The command is intentionally separate from `doctor`:
`doctor` remains read-only, while this script is the canonical mutating setup
step before Docker/bootstrap/runtime startup.

.PARAMETER SkipDotnetRestore
Skips `dotnet restore`. Intended only when an equivalent restore was already
completed for the checkout and SDK.

.PARAMETER SkipFrontendInstall
Skips `npm ci`. Intended only when webUI dependencies were already installed
from the package-lock.json.

.PARAMETER ForceFrontendInstall
Removes webUI/node_modules before `npm ci`. This is an explicit recovery path
for damaged local dependency state.
#>

[CmdletBinding()]
param(
    [switch]$SkipDotnetRestore,
    [switch]$SkipFrontendInstall,
    [switch]$ForceFrontendInstall
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-CommandAvailable {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Hint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found on PATH. $Hint"
    }
}

function Invoke-RequiredCommand {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    Write-Host ""
    Write-Host "== $Name =="
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @(
    'NatureProtector.sln',
    'NuGet.Config',
    'webUI/package.json',
    'webUI/package-lock.json'
)
$webUiRoot = Join-Path $repoRoot 'webUI'
$nodeModulesPath = Join-Path $webUiRoot 'node_modules'
$vitePackagePath = Join-Path $nodeModulesPath 'vite/package.json'

Set-Location $repoRoot

Assert-CommandAvailable -Name 'dotnet' -Hint "Install the SDK selected by global.json, then run '.\scripts\np.ps1 doctor'."
Assert-CommandAvailable -Name 'node' -Hint "Install the Node.js version accepted by webUI/package.json."
Assert-CommandAvailable -Name 'npm' -Hint "Install the npm version accepted by webUI/package.json."

& (Join-Path $repoRoot 'scripts/dotnet/Use-RepoDotnetEnvironment.ps1') -Quiet | Out-Null

if (-not $SkipDotnetRestore) {
    Invoke-RequiredCommand -Name '.NET restore' -Action {
        & dotnet restore (Join-Path $repoRoot 'NatureProtector.sln') `
            --configfile (Join-Path $repoRoot 'NuGet.Config') `
            --disable-parallel `
            --nologo
    }
}
else {
    Write-Host 'Skipping .NET restore by explicit request.'
}

if (-not $SkipFrontendInstall) {
    if ($ForceFrontendInstall -and (Test-Path -LiteralPath $nodeModulesPath)) {
        Write-Host "Removing existing frontend dependency tree: $nodeModulesPath"
        Remove-Item -LiteralPath $nodeModulesPath -Recurse -Force
    }

    Invoke-RequiredCommand -Name 'webUI clean dependency install' -Action {
        Push-Location $webUiRoot
        try {
            & npm ci
        }
        finally {
            Pop-Location
        }
    }
}
else {
    Write-Host 'Skipping webUI dependency installation by explicit request.'
}

if (-not $SkipFrontendInstall -and -not (Test-Path -LiteralPath $vitePackagePath -PathType Leaf)) {
    throw "npm ci completed but the Vite package is missing at $vitePackagePath. Remove webUI/node_modules and rerun with -ForceFrontendInstall."
}

$summary = [ordered]@{
    repository = $repoRoot
    dotnetRestore = if ($SkipDotnetRestore) { 'skipped' } else { 'completed' }
    frontendInstall = if ($SkipFrontendInstall) { 'skipped' } else { 'completed' }
    frontendReady = (Test-Path -LiteralPath $vitePackagePath -PathType Leaf)
}

Write-Host ""
Write-Host 'Local workspace preparation completed.'
$summary.GetEnumerator() | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Key, $_.Value) }
