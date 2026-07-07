[CmdletBinding()]
param()

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$downScript = Join-Path $repoRoot 'infra\scripts\down.ps1'

if (-not (Test-Path -LiteralPath $downScript -PathType Leaf)) {
    throw "Docker infrastructure stop script not found at $downScript."
}

Set-Location $repoRoot
Write-Host "Stopping NatureProtector local infrastructure with project-scoped docker compose down."
& pwsh -NoProfile -ExecutionPolicy Bypass -File $downScript
exit $LASTEXITCODE
