<#
.SYNOPSIS
Configura um ambiente local de .NET/NuGet isolado dentro do repositório.

.DESCRIPTION
Este script prepara diretórios locais para `APPDATA`, `DOTNET_CLI_HOME` e
`NUGET_PACKAGES`, evitando poluir o perfil global da máquina e tornando os
comandos mais reproduzíveis entre membros da equipa.

.NOTES
- Deve ser executado antes de builds, testes ou restores quando se pretende
  manter o estado do .NET dentro do repositório.
- Não altera o sistema fora da árvore do projeto.
#>

[CmdletBinding()]
param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$paths = @{
    APPDATA = Join-Path $repoRoot ".config\AppData\Roaming"
    DOTNET_CLI_HOME = Join-Path $repoRoot ".config\dotnet"
    NUGET_PACKAGES = if ([string]::IsNullOrWhiteSpace($env:NP_NUGET_PACKAGES)) {
        Join-Path $repoRoot ".nuget\packages"
    }
    else {
        [IO.Path]::GetFullPath($env:NP_NUGET_PACKAGES)
    }
}

# Garante que os diretórios existem antes de atualizar as variáveis de ambiente.
foreach ($path in $paths.Values) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

$env:APPDATA = $paths.APPDATA
$env:DOTNET_CLI_HOME = $paths.DOTNET_CLI_HOME
$env:NUGET_PACKAGES = $paths.NUGET_PACKAGES

if (-not $Quiet) {
    Write-Host "Configured repo-local dotnet/NuGet environment."
    Write-Host "  APPDATA: $($env:APPDATA)"
    Write-Host "  DOTNET_CLI_HOME: $($env:DOTNET_CLI_HOME)"
    Write-Host "  NUGET_PACKAGES: $($env:NUGET_PACKAGES)"
}
