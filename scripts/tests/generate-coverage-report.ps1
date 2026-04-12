<#
.SYNOPSIS
Executa os testes com cobertura e gera um relatório consolidado.

.DESCRIPTION
O script corre `dotnet test` com recolha de cobertura Cobertura, agrega todos os
relatórios encontrados e produz uma saída HTML e um resumo textual através de
`reportgenerator`.

.NOTES
- Exclui ficheiros de arranque e boilerplate para dar mais foco à lógica.
- Assume que `reportgenerator` está disponível no ambiente.
#>

param(
    [string]$Solution = ".\NatureProtector.sln",
    [string]$RunSettings = ".\coverage.runsettings",
    [string]$TargetDir = "coveragereport_core"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
& (Join-Path $repoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1") -Quiet | Out-Null

$assemblyFilters = @(
    "+NatureProtector.Core",
    "+NatureProtector.Prevention",
    "+NatureProtector.Shared",
    "+NatureProtector.Simulator.Host",
    "+NatureProtector.Prevention.Host",
    "+NatureProtector.Infrastructure.Influx",
    "+NatureProtector.Backoffice.Api",
    "-*.Tests"
) -join ";"

# Estes filtros removem pontos de entrada e infraestrutura de baixo sinal para
# que a cobertura se concentre no código com mais lógica.
$fileFilters = @(
    "-**\Program.cs",
    "-**\Program.Partial.cs",
    "-**\*Worker*.cs",
    "-**\*HostedService*.cs",
    "-**\*BackgroundService*.cs",
    "-**\*ServiceCollectionExtensions*.cs",
    "-**\*AssemblyInfo.cs",
    "-**\bin\**",
    "-**\obj\**",
    "-**\*.g.cs",
    "-**\*.Designer.cs"
) -join ";"

Get-ChildItem -Recurse -Directory -Filter TestResults | Remove-Item -Recurse -Force
Remove-Item -Recurse -Force $TargetDir -ErrorAction SilentlyContinue

dotnet test $Solution `
  --nologo `
  -v minimal `
  -m:1 `
  --collect:"XPlat Code Coverage" `
  --settings $RunSettings

$reports = (Get-ChildItem -Recurse -Filter "coverage.cobertura.xml").FullName -join ";"

if ([string]::IsNullOrWhiteSpace($reports))
{
    throw "No coverage.cobertura.xml files were generated."
}

# A geração do relatório consolidado acontece só depois de todos os ficheiros de
# cobertura terem sido recolhidos.
reportgenerator `
  -reports:"$reports" `
  -targetdir:"$TargetDir" `
  -reporttypes:"Html;TextSummary" `
  -assemblyfilters:"$assemblyFilters" `
  -filefilters:"$fileFilters"

$summaryPath = Join-Path $TargetDir "Summary.txt"

if (Test-Path $summaryPath)
{
    Get-Content $summaryPath
}
else
{
    Write-Warning "Summary.txt was not generated, but the HTML report may still be available."
}

Write-Host ""
Write-Host "Coverage report generated at: $((Resolve-Path (Join-Path $TargetDir 'index.html')).Path)"
