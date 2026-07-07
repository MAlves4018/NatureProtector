[CmdletBinding()]
param()

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$composeFile = Join-Path $repoRoot 'docker-compose.yml'

if (-not (Test-Path -LiteralPath $composeFile -PathType Leaf)) {
    throw "docker-compose.yml not found at $composeFile."
}

Set-Location $repoRoot

Write-Host "Cleaning NatureProtector local Docker state only."
Write-Host "Scope: docker compose project directory '$repoRoot' with '$composeFile'."
Write-Host "Operation: docker compose down -v --remove-orphans."
Write-Host "No global docker prune is executed."

& docker compose --project-directory $repoRoot -f $composeFile down -v --remove-orphans
if ($LASTEXITCODE -ne 0) {
    throw "Project-scoped docker compose down -v --remove-orphans failed with exit code $LASTEXITCODE."
}

Write-Host "NatureProtector local Docker state cleaned. Project containers, networks and compose volumes were removed when present."
