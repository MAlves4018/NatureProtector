[CmdletBinding()]
param(
    [switch]$OpenBrowser,
    [switch]$NoBrowser,
    [switch]$SkipDocker,
    [switch]$SkipBootstrap,
    [switch]$ForceRestart,
    [int]$ApiPort = 0,
    [int]$PreventionPort = 0,
    [int]$WebPort = 0
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$launcher = Join-Path $repoRoot 'scripts\dev\start-local-runtime.ps1'

if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Local runtime launcher not found at $launcher."
}

$launcherParameters = @{
    RunSimulator = $false
    ApiPort = $ApiPort
    PreventionPort = $PreventionPort
    WebPort = $WebPort
    OpenBrowser = [bool]$OpenBrowser
    NoBrowser = [bool]$NoBrowser
    SkipDocker = [bool]$SkipDocker
    SkipBootstrap = [bool]$SkipBootstrap
    ForceRestart = [bool]$ForceRestart
}

Write-Host "Starting persistent local runtime services: Backoffice API, Prevention Host, webUI."
Write-Host "Simulator.Host is not started here; it is launched per run through API/UI."
& $launcher @launcherParameters
if (-not $?) {
    exit 1
}

Write-Host "Local runtime start command completed. Persistent service stdout/stderr is redirected to the launcher log directory."
exit 0
