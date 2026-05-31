<#
.SYNOPSIS
Executa o bootstrap do control plane PostgreSQL do projeto.

.DESCRIPTION
Este script valida a solução, confirma que o PostgreSQL local está acessível,
prepara o ambiente .NET do repositório e arranca o projeto
`NatureProtector.Postgres.Bootstrap`.

.PARAMETER SkipBuild
Salta o `dotnet build` da solution quando o bootstrap já está compilado ou
quando se pretende iterar mais rapidamente.

.NOTES
- Requer um PostgreSQL acessível em `localhost:5432`, salvo configuração
  diferente resolvida pelo `.env`.
- Falha cedo quando a solution ou o projeto de bootstrap não existem.
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$bootstrapDllPath = Join-Path $repoRoot "src\NatureProtector.Postgres.Bootstrap\bin\Debug\net9.0\NatureProtector.Postgres.Bootstrap.dll"

if (-not (Test-Path (Join-Path $repoRoot "src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj"))) {
    throw "Bootstrap project not found under $repoRoot"
}

if (-not (Test-Path (Join-Path $repoRoot "NatureProtector.sln"))) {
    throw "Solution not found under $repoRoot"
}

$postgresReachable = Test-NetConnection -ComputerName "localhost" -Port 5433 -InformationLevel Quiet

if (-not $postgresReachable) {
    throw "PostgreSQL não está acessível em localhost:5433. Levanta primeiro o serviço 'postgres' no Docker Compose ou ajusta o .env para um endpoint válido."
}

Push-Location $repoRoot
try {
    & (Join-Path $repoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1") -Quiet | Out-Null

    if (-not $SkipBuild) {
        # Compila a solution para garantir que o projeto de bootstrap e as
        # dependências estão alinhados com o estado atual do repositório.
        dotnet build .\NatureProtector.sln --nologo -v minimal -m:1 --configfile NuGet.Config

        if ($LASTEXITCODE -ne 0) {
            if (-not (Test-Path $bootstrapDllPath)) {
                throw "dotnet build .\\NatureProtector.sln --nologo -v minimal -m:1 --configfile NuGet.Config falhou com exit code $LASTEXITCODE."
            }

            Write-Warning "O build da solution falhou nesta execução, mas o bootstrap já estava compilado. O script vai continuar com --no-build."
        }
    }

    # O bootstrap é executado sem restore/build porque essas fases já foram
    # tratadas acima, o que torna o fluxo mais previsível.
    dotnet run --project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj --no-build --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run --project .\\src\\NatureProtector.Postgres.Bootstrap\\NatureProtector.Postgres.Bootstrap.csproj --no-build --no-restore falhou com exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
