[CmdletBinding()]
param(
    [string]$OutputDirectory
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Continue'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$composeFile = Join-Path $repoRoot 'docker-compose.yml'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $OutputDirectory = Join-Path $repoRoot "artifacts\local-runtime\docker-diagnostics\$timestamp"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Export-Command {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [string]$FileName
    )

    $path = Join-Path $OutputDirectory $FileName
    "Command: $Name $($Arguments -join ' ')" | Set-Content -LiteralPath $path -Encoding UTF8
    & $Name @Arguments 2>&1 | Add-Content -LiteralPath $path -Encoding UTF8
    "ExitCode: $LASTEXITCODE" | Add-Content -LiteralPath $path -Encoding UTF8
}

Set-Location $repoRoot
Export-Command 'docker' @('info') 'docker-info.txt'
Export-Command 'docker' @('compose', '--project-directory', $repoRoot, '-f', $composeFile, 'ps') 'compose-ps.txt'
Export-Command 'docker' @('compose', '--project-directory', $repoRoot, '-f', $composeFile, 'config') 'compose-config.txt'
Export-Command 'docker' @('compose', '--project-directory', $repoRoot, '-f', $composeFile, 'logs', '--no-color', '--tail', '200') 'compose-logs-tail.txt'

Write-Host "Docker diagnostics exported: $OutputDirectory"
