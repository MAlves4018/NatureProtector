<#
.SYNOPSIS
Runs backend tests with coverage and generates integral and focused reports.

.DESCRIPTION
The script runs `dotnet test` in Release, writes TRX and coverage output under a
script-owned artifact directory, and generates two ReportGenerator reports:

- backend-integral: broad backend/runtime coverage. It intentionally keeps
  Program.cs, workers, hosted/background services and composition roots in scope.
- backend-focused: narrow coverage for risk rules, classifiers, eligibility,
  mappings and critical runtime/transport contracts. This report is not global
  backend coverage.

DockerIntegration tests are excluded by default because they require live
PostgreSQL/RabbitMQ/InfluxDB infrastructure. Use -IncludeDockerIntegration to
include them in the same run.
#>

param(
    [string]$Solution = ".\NatureProtector.sln",
    [string]$RunSettings = ".\coverage.runsettings",
    [string]$OutputRoot = ".\artifacts\coverage",
    [switch]$IncludeDockerIntegration,
    [switch]$NoRestore,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

function Resolve-PathForWrite
{
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    $combined = if ([System.IO.Path]::IsPathRooted($TargetPath))
    {
        $TargetPath
    }
    else
    {
        Join-Path $baseFullPath $TargetPath
    }

    $targetFullPath = [System.IO.Path]::GetFullPath($combined)
    if (-not ($targetFullPath.StartsWith($baseFullPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($targetFullPath, $baseFullPath, [System.StringComparison]::OrdinalIgnoreCase)))
    {
        throw "Refusing to write coverage output outside the repository: $targetFullPath"
    }

    return $targetFullPath
}

function Invoke-ReportGenerator
{
    param(
        [string]$Reports,
        [string]$TargetDirectory,
        [string]$AssemblyFilters,
        [string]$ClassFilters,
        [string]$FileFilters
    )

    $arguments = @(
        "-reports:$Reports",
        "-targetdir:$TargetDirectory",
        "-reporttypes:Html;TextSummary",
        "-assemblyfilters:$AssemblyFilters",
        "-filefilters:$FileFilters"
    )

    if (-not [string]::IsNullOrWhiteSpace($ClassFilters))
    {
        $arguments += "-classfilters:$ClassFilters"
    }

    dotnet tool run reportgenerator -- @arguments
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }
}

function Write-Summary
{
    param(
        [string]$Label,
        [string]$ReportDirectory
    )

    $summaryPath = Join-Path $ReportDirectory "Summary.txt"
    Write-Host ""
    Write-Host "Coverage report: $Label"
    Write-Host "Directory: $ReportDirectory"

    if (Test-Path $summaryPath)
    {
        Get-Content $summaryPath
    }
    else
    {
        Write-Warning "Summary.txt was not generated for $Label."
    }
}

function Assert-CoverageSummaryContainsAssemblies
{
    param(
        [string]$Label,
        [string]$ReportDirectory,
        [string[]]$ExpectedAssemblies
    )

    $summaryPath = Join-Path $ReportDirectory "Summary.txt"
    if (-not (Test-Path $summaryPath))
    {
        throw "Coverage summary was not generated for $Label."
    }

    $summary = Get-Content -Raw $summaryPath
    foreach ($assembly in $ExpectedAssemblies)
    {
        $pattern = "(?m)^$([regex]::Escape($assembly))\s+"
        if ($summary -notmatch $pattern)
        {
            throw "Coverage report '$Label' is missing expected assembly '$assembly'. Ensure a non-Docker test loads the assembly or adjust the coverage scope explicitly."
        }
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
& (Join-Path $repoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1") -Quiet | Out-Null

Push-Location $repoRoot
try
{
    dotnet tool restore | Out-Null

    $outputRootPath = Resolve-PathForWrite -BasePath $repoRoot -TargetPath $OutputRoot
    if (Test-Path $outputRootPath)
    {
        Remove-Item -LiteralPath $outputRootPath -Recurse -Force
    }

    $resultsDirectory = Join-Path $outputRootPath "test-results"
    $integralDirectory = Join-Path $outputRootPath "backend-integral"
    $focusedDirectory = Join-Path $outputRootPath "backend-focused"
    New-Item -ItemType Directory -Force -Path $resultsDirectory, $integralDirectory, $focusedDirectory | Out-Null

    $testArguments = @(
        $Solution,
        "-c", "Release",
        "--nologo",
        "-v", "minimal",
        "-m:1",
        "--logger", "trx;LogFilePrefix=coverage",
        "--results-directory", $resultsDirectory,
        "--collect:XPlat Code Coverage",
        "--settings", $RunSettings
    )

    if ($NoRestore)
    {
        $testArguments += "--no-restore"
    }

    if ($NoBuild)
    {
        $testArguments += "--no-build"
    }

    if (-not $IncludeDockerIntegration)
    {
        $testArguments += "--filter"
        $testArguments += "Category!=DockerIntegration"
    }

    dotnet test @testArguments
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    $coverageReports = @(Get-ChildItem -Path $resultsDirectory -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue)
    if ($coverageReports.Count -eq 0)
    {
        throw "No coverage.cobertura.xml files were generated under $resultsDirectory."
    }

    $uniqueCoverageReports = @(
        $coverageReports |
            ForEach-Object {
                [pscustomobject]@{
                    Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
                    Path = $_.FullName
                }
            } |
            Group-Object Hash |
            ForEach-Object {
                $_.Group | Sort-Object Path | Select-Object -First 1
            } |
            Sort-Object Path
    )

    if ($uniqueCoverageReports.Count -lt $coverageReports.Count)
    {
        Write-Host "Deduplicated $($coverageReports.Count) Cobertura files to $($uniqueCoverageReports.Count) unique reports."
    }

    $reports = ($uniqueCoverageReports.Path | Sort-Object) -join ";"
    $generatedFileFilters = @(
        "-**\bin\**",
        "-**\obj\**",
        "-**\*.g.cs",
        "-**\*.Designer.cs"
    ) -join ";"

    $integralAssemblyFilters = @(
        "+NatureProtector.Core",
        "+NatureProtector.Shared",
        "+NatureProtector.Prevention",
        "+NatureProtector.Prevention.Host",
        "+NatureProtector.Simulator.Host",
        "+NatureProtector.Backoffice.Api",
        "+NatureProtector.Infrastructure.Postgres",
        "+NatureProtector.Infrastructure.Influx",
        "+NatureProtector.Postgres.Bootstrap",
        "+NatureProtector.Shared.Observability",
        "-*.Tests"
    ) -join ";"

    $focusedAssemblyFilters = @(
        "+NatureProtector.Core",
        "+NatureProtector.Shared",
        "+NatureProtector.Prevention",
        "+NatureProtector.Backoffice.Api",
        "-*.Tests"
    ) -join ";"

    $focusedClassFilters = @(
        "+NatureProtector.Core.Risk.*",
        "+NatureProtector.Core.Primitives.RiskLevel",
        "+NatureProtector.Core.Primitives.Severity",
        "+NatureProtector.Prevention.Readings.*",
        "+NatureProtector.Prevention.Risk.*",
        "+NatureProtector.Shared.Configuration.RabbitMqOptions",
        "+NatureProtector.Shared.Contracts.Readings.*",
        "+NatureProtector.Shared.Messaging.*",
        "+NatureProtector.Backoffice.Api.ControlPlane.Contracts.*"
    ) -join ";"

    Invoke-ReportGenerator `
        -Reports $reports `
        -TargetDirectory $integralDirectory `
        -AssemblyFilters $integralAssemblyFilters `
        -ClassFilters "" `
        -FileFilters $generatedFileFilters

    Invoke-ReportGenerator `
        -Reports $reports `
        -TargetDirectory $focusedDirectory `
        -AssemblyFilters $focusedAssemblyFilters `
        -ClassFilters $focusedClassFilters `
        -FileFilters $generatedFileFilters

    Assert-CoverageSummaryContainsAssemblies `
        -Label "backend-integral" `
        -ReportDirectory $integralDirectory `
        -ExpectedAssemblies @(
            "NatureProtector.Backoffice.Api",
            "NatureProtector.Core",
            "NatureProtector.Infrastructure.Influx",
            "NatureProtector.Infrastructure.Postgres",
            "NatureProtector.Postgres.Bootstrap",
            "NatureProtector.Prevention",
            "NatureProtector.Prevention.Host",
            "NatureProtector.Shared",
            "NatureProtector.Shared.Observability",
            "NatureProtector.Simulator.Host"
        )

    Assert-CoverageSummaryContainsAssemblies `
        -Label "backend-focused" `
        -ReportDirectory $focusedDirectory `
        -ExpectedAssemblies @(
            "NatureProtector.Backoffice.Api",
            "NatureProtector.Core",
            "NatureProtector.Prevention",
            "NatureProtector.Shared"
        )

    Write-Summary -Label "backend-integral" -ReportDirectory $integralDirectory
    Write-Summary -Label "backend-focused" -ReportDirectory $focusedDirectory

    Write-Host ""
    Write-Host "Coverage artifacts generated at: $outputRootPath"
    if (-not $IncludeDockerIntegration)
    {
        Write-Host "DockerIntegration tests were excluded. Re-run with -IncludeDockerIntegration to include them."
    }
}
finally
{
    Pop-Location
}
