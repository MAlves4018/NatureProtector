param(
    [string]$Solution = ".\NatureProtector.sln",
    [string]$RunSettings = ".\coverage.runsettings",
    [string]$TargetDir = "coveragereport_core"
)

$ErrorActionPreference = "Stop"

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
  --collect:"XPlat Code Coverage" `
  --settings $RunSettings

$reports = (Get-ChildItem -Recurse -Filter "coverage.cobertura.xml").FullName -join ";"

if ([string]::IsNullOrWhiteSpace($reports))
{
    throw "No coverage.cobertura.xml files were generated."
}

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
