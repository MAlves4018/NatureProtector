[CmdletBinding()]
param()

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$upScript = Join-Path $repoRoot 'infra\scripts\up.ps1'

if (-not (Test-Path -LiteralPath $upScript -PathType Leaf)) {
    throw "Docker infrastructure startup script not found at $upScript."
}

Set-Location $repoRoot
Write-Host "Starting NatureProtector local infrastructure with project-scoped docker compose."
& pwsh -NoProfile -ExecutionPolicy Bypass -File $upScript -SkipWorkspacePreparation
exit $LASTEXITCODE
