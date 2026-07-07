[CmdletBinding()]
param(
    [switch]$OpenBrowser,
    [switch]$NoBrowser,
    [switch]$SkipDocker,
    [switch]$SkipBootstrap,
    [switch]$ForceRestart,
    [int]$ApiPort = 5254,
    [int]$PreventionPort = 5260,
    [int]$WebPort = 5173
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$launcher = Join-Path $repoRoot 'scripts\dev\start-local-runtime.ps1'

if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Local runtime launcher not found at $launcher."
}

$arguments = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', $launcher,
    '-RunSimulator:$false',
    '-ApiPort', "$ApiPort",
    '-PreventionPort', "$PreventionPort",
    '-WebPort', "$WebPort"
)

if ($OpenBrowser) { $arguments += '-OpenBrowser' }
if ($NoBrowser) { $arguments += '-NoBrowser' }
if ($SkipDocker) { $arguments += '-SkipDocker' }
if ($SkipBootstrap) { $arguments += '-SkipBootstrap' }
if ($ForceRestart) { $arguments += '-ForceRestart' }

Write-Host "Starting persistent local runtime services: Backoffice API, Prevention Host, webUI."
Write-Host "Simulator.Host is not started here; it is launched per run through API/UI."
& pwsh @arguments
$success = $?
$code = $LASTEXITCODE
if (-not $success -and ($null -eq $code -or $code -eq 0)) {
    exit 1
}
if ($code -eq 0) {
    Write-Host "Local runtime start command completed. Persistent service stdout/stderr is redirected to the launcher log directory."
}
exit $code
