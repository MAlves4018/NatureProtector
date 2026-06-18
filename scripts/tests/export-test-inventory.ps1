<#
.SYNOPSIS
Exports an automatic NatureProtector test and quality inventory.

.DESCRIPTION
Scans test projects, frontend test scripts, benchmark configuration, coverage
tooling, and mutation tooling. The output is written to artifacts/validation
as JSON, CSV, and Markdown evidence. This script does not execute Git and does
not create or edit .env files.
#>

[CmdletBinding()]
param(
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

function Find-RepositoryRoot {
    $current = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $current) {
        if ((Test-Path -LiteralPath (Join-Path $current.FullName "NatureProtector.sln")) -and
            (Test-Path -LiteralPath (Join-Path $current.FullName "tests"))) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not locate repository root from $PSScriptRoot."
}

function Get-RelativePath {
    param(
        [string]$Root,
        [string]$Path
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($fullRoot.Length).TrimStart('\', '/')
    }

    return $fullPath
}

function Count-Regex {
    param(
        [string]$Text,
        [string]$Pattern
    )

    return ([regex]::Matches($Text, $Pattern)).Count
}

function Get-XmlPackageReferences {
    param([string]$ProjectPath)

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath -Raw
    $references = @()
    foreach ($reference in $projectXml.Project.ItemGroup.PackageReference) {
        if ($reference.Include) {
            $references += [string]$reference.Include
        }
    }

    return $references | Sort-Object -Unique
}

function Get-XmlProjectReferences {
    param([string]$ProjectPath)

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath -Raw
    $references = @()
    foreach ($reference in $projectXml.Project.ItemGroup.ProjectReference) {
        if ($reference.Include) {
            $references += [string]$reference.Include
        }
    }

    return $references | Sort-Object -Unique
}

function Get-TestClassification {
    param(
        [string]$ProjectName,
        [string[]]$Categories,
        [bool]$HasPropertyTests
    )

    $levels = [System.Collections.Generic.List[string]]::new()

    if ($ProjectName -like "*.Core.Tests") {
        $levels.Add("Unit") | Out-Null
        $levels.Add("Domain") | Out-Null
    }
    elseif ($ProjectName -like "*.Prevention.Tests") {
        $levels.Add("Domain") | Out-Null
        $levels.Add("Component") | Out-Null
    }
    elseif ($ProjectName -like "*.Shared.Tests") {
        $levels.Add("Contract") | Out-Null
        $levels.Add("Architecture") | Out-Null
    }
    elseif ($ProjectName -like "*.Backoffice.Api.Tests") {
        $levels.Add("API") | Out-Null
        $levels.Add("Contract") | Out-Null
        $levels.Add("Security") | Out-Null
        $levels.Add("Architecture") | Out-Null
    }
    elseif ($ProjectName -like "*.IntegrationTests") {
        $levels.Add("AdapterIntegration") | Out-Null
        $levels.Add("DistributedIntegration") | Out-Null
        $levels.Add("ProcessLevelIntegration") | Out-Null
    }
    elseif ($ProjectName -like "*.Infrastructure.Influx.Tests") {
        $levels.Add("AdapterIntegration") | Out-Null
        $levels.Add("Component") | Out-Null
    }
    elseif ($ProjectName -like "*.Prevention.Host.Tests" -or $ProjectName -like "*.Simulator.Host.Tests") {
        $levels.Add("Component") | Out-Null
        $levels.Add("AdapterIntegration") | Out-Null
    }
    else {
        $levels.Add("Component") | Out-Null
    }

    if ($Categories -contains "DockerIntegration") {
        $levels.Add("DistributedIntegration") | Out-Null
    }

    if ($HasPropertyTests) {
        $levels.Add("PropertyBased") | Out-Null
    }

    return $levels | Sort-Object -Unique
}

function Get-EnvironmentClassification {
    param(
        [string]$ProjectName,
        [string[]]$Categories,
        [string[]]$Packages
    )

    if ($Categories -contains "DockerIntegration" -or $ProjectName -like "*.IntegrationTests") {
        return "Docker local services: PostgreSQL, RabbitMQ, InfluxDB"
    }

    if ($Packages -contains "Microsoft.AspNetCore.Mvc.Testing") {
        return "In-process ASP.NET Core TestServer; no external service by default"
    }

    if ($ProjectName -like "*.Host.Tests" -or $ProjectName -like "*.Infrastructure.*.Tests") {
        return "In-memory, SQLite, or mocked adapters unless a test opts into Docker"
    }

    return "No external service dependency"
}

function Get-DurationBand {
    param(
        [string]$ProjectName,
        [string[]]$Categories,
        [int]$TestCount
    )

    if ($Categories -contains "DockerIntegration" -or $ProjectName -like "*.IntegrationTests") {
        return "Slow/manual or scheduled"
    }

    if ($TestCount -gt 250) {
        return "Medium"
    }

    return "Fast"
}

function Get-RecommendedFrequency {
    param(
        [string]$DurationBand,
        [string[]]$Levels
    )

    if ($DurationBand -like "Slow*") {
        return "Before release, locally before external verification, and scheduled CI"
    }

    if ($Levels -contains "Security" -or $Levels -contains "Contract") {
        return "Every pull request and before release"
    }

    return "Every local validation and every pull request"
}

function Join-Values {
    param([string[]]$Values)

    $materialized = @($Values | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    if ($materialized.Count -eq 0) {
        return ""
    }

    return ($materialized -join "; ")
}

$repoRoot = Find-RepositoryRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\validation\test-inventory"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDirectory = Join-Path $OutputRoot $runId
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$taxonomy = @(
    [pscustomobject]@{ Name = "Unit"; Description = "Small isolated tests with no external service dependency." },
    [pscustomobject]@{ Name = "Component"; Description = "Single component or host service behavior with fakes, in-memory stores, or SQLite." },
    [pscustomobject]@{ Name = "Domain"; Description = "Domain model and candidate V1 methodology behavior." },
    [pscustomobject]@{ Name = "API"; Description = "Backoffice API routing, responses, auth and OpenAPI semantics." },
    [pscustomobject]@{ Name = "Contract"; Description = "Public serialization, fixture, envelope, OpenAPI, or frontend contract tests." },
    [pscustomobject]@{ Name = "Architecture"; Description = "Dependency and architecture guardrails." },
    [pscustomobject]@{ Name = "PropertyBased"; Description = "FsCheck or equivalent generated-property tests." },
    [pscustomobject]@{ Name = "AdapterIntegration"; Description = "Persistence, broker, HTTP, or telemetry adapter behavior." },
    [pscustomobject]@{ Name = "DistributedIntegration"; Description = "Multiple real services connected through Docker or process boundaries." },
    [pscustomobject]@{ Name = "ProcessLevelIntegration"; Description = "Published process startup/readiness/lifecycle tests." },
    [pscustomobject]@{ Name = "BrowserIntegration"; Description = "Browser-driven frontend checks." },
    [pscustomobject]@{ Name = "FullStackE2E"; Description = "End-to-end flow across frontend, API, runtime and infrastructure." },
    [pscustomobject]@{ Name = "Accessibility"; Description = "Accessibility checks such as axe." },
    [pscustomobject]@{ Name = "Security"; Description = "Security scanners, auth, authorization, traversal, and secret checks." },
    [pscustomobject]@{ Name = "Mutation"; Description = "Stryker.NET mutation testing." },
    [pscustomobject]@{ Name = "Microbenchmark"; Description = "BenchmarkDotNet microbenchmarks." },
    [pscustomobject]@{ Name = "SystemPerformance"; Description = "System workload and capacity measurements." }
)

$inventory = [System.Collections.Generic.List[object]]::new()
$gaps = [System.Collections.Generic.List[string]]::new()

$testProjects = Get-ChildItem -Path (Join-Path $repoRoot "tests") -Recurse -Filter *.csproj |
    Sort-Object FullName

foreach ($project in $testProjects) {
    $projectDir = Split-Path -Parent $project.FullName
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
    $packages = @(Get-XmlPackageReferences $project.FullName)
    $projectReferences = @(Get-XmlProjectReferences $project.FullName)
    $sourceFiles = Get-ChildItem -Path $projectDir -Recurse -File -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

    $factCount = 0
    $theoryCount = 0
    $propertyCount = 0
    $categories = [System.Collections.Generic.List[string]]::new()

    foreach ($file in $sourceFiles) {
        $content = Get-Content -LiteralPath $file.FullName -Raw
        $factCount += Count-Regex $content '\[(?:Xunit\.)?Fact(?:Attribute)?(?:\([^\]]*\))?\]'
        $theoryCount += Count-Regex $content '\[(?:Xunit\.)?Theory(?:Attribute)?(?:\([^\]]*\))?\]'
        $propertyCount += Count-Regex $content '\[(?:FsCheck\.Xunit\.)?Property(?:Attribute)?(?:\([^\]]*\))?\]'

        foreach ($match in [regex]::Matches($content, '\[Trait\("Category",\s*"([^"]+)"\)\]')) {
            $categories.Add($match.Groups[1].Value) | Out-Null
        }
    }

    $explicitCategories = @($categories | Sort-Object -Unique)
    $testCount = $factCount + $theoryCount + $propertyCount
    $levels = @(Get-TestClassification $projectName $explicitCategories ($propertyCount -gt 0))
    $durationBand = Get-DurationBand $projectName $explicitCategories $testCount
    $environment = Get-EnvironmentClassification $projectName $explicitCategories $packages

    if ($explicitCategories.Count -eq 0) {
        $gaps.Add("$projectName has no explicit xUnit Category traits; classification is inferred from project and file paths.") | Out-Null
    }

    if ($testCount -eq 0) {
        $gaps.Add("$projectName has zero statically detected [Fact]/[Theory]/[Property] tests.") | Out-Null
    }

    if ($projectName -like "*.IntegrationTests" -and $explicitCategories -notcontains "DockerIntegration") {
        $gaps.Add("$projectName is an integration project but not all classification came from explicit DockerIntegration traits.") | Out-Null
    }

    $inventory.Add([pscustomobject]@{
        Id = $projectName
        Kind = "BackendTestProject"
        Path = Get-RelativePath $repoRoot $project.FullName
        Taxonomy = $levels
        ExplicitCategories = $explicitCategories
        TestCount = $testCount
        FactCount = $factCount
        TheoryCount = $theoryCount
        PropertyCount = $propertyCount
        SourceFileCount = @($sourceFiles).Count
        ExternalDependency = $environment
        Duration = $durationBand
        Environment = $environment
        RecommendedFrequency = Get-RecommendedFrequency $durationBand $levels
        Packages = $packages
        ProjectReferences = $projectReferences
        Notes = if ($explicitCategories.Count -eq 0) { "Taxonomy inferred automatically; add traits for stronger filtering." } else { "Contains explicit xUnit category metadata." }
    }) | Out-Null
}

$webPackagePath = Join-Path $repoRoot "webUI\package.json"
if (Test-Path -LiteralPath $webPackagePath) {
    $webPackage = Get-Content -LiteralPath $webPackagePath -Raw | ConvertFrom-Json
    $webRoot = Join-Path $repoRoot "webUI"
    $frontendTestFiles = Get-ChildItem -Path $webRoot -Recurse -File -Include *.test.ts,*.test.tsx,*.spec.ts,*.spec.tsx -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(node_modules|dist|coverage|playwright-report|test-results)\\' }
    $e2eFiles = Get-ChildItem -Path (Join-Path $webRoot "e2e") -Recurse -File -Include *.ts,*.tsx -ErrorAction SilentlyContinue
    $scripts = @($webPackage.scripts.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object)
    $frontendLevels = @("Component", "BrowserIntegration")
    if ((Get-ChildItem -Path $webRoot -Recurse -File -Include *.ts,*.tsx -ErrorAction SilentlyContinue | Select-String -Pattern "axe|axe-core" -Quiet)) {
        $frontendLevels += "Accessibility"
    }

    $inventory.Add([pscustomobject]@{
        Id = "webUI"
        Kind = "FrontendTestSurface"
        Path = "webUI\package.json"
        Taxonomy = @($frontendLevels | Sort-Object -Unique)
        ExplicitCategories = @()
        TestCount = @($frontendTestFiles).Count + @($e2eFiles).Count
        FactCount = 0
        TheoryCount = 0
        PropertyCount = 0
        SourceFileCount = @($frontendTestFiles).Count
        ExternalDependency = "Node.js, npm, jsdom, Playwright browser for e2e"
        Duration = "Fast for Vitest; medium/manual for Playwright matrix"
        Environment = "Node 20/22; browser runtime for e2e"
        RecommendedFrequency = "Vitest/typecheck/lint on every PR; Playwright Chromium on PR; full browser matrix scheduled/manual"
        Packages = @($webPackage.devDependencies.PSObject.Properties.Name | Sort-Object)
        ProjectReferences = @()
        Notes = "Inventory derived from package.json scripts and test file discovery. Playwright is classified as BrowserIntegration unless a live API/runtime/full-stack oracle is proven separately."
    }) | Out-Null
}

$benchmarkProject = Join-Path $repoRoot "benchmarks\NatureProtector.Benchmarks\NatureProtector.Benchmarks.csproj"
if (Test-Path -LiteralPath $benchmarkProject) {
    $benchmarkSource = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $benchmarkProject) "Program.cs") -Raw
    $benchmarkCount = Count-Regex $benchmarkSource '\[Benchmark\]'
    $inventory.Add([pscustomobject]@{
        Id = "NatureProtector.Benchmarks"
        Kind = "BenchmarkProject"
        Path = Get-RelativePath $repoRoot $benchmarkProject
        Taxonomy = @("Microbenchmark")
        ExplicitCategories = @()
        TestCount = $benchmarkCount
        FactCount = 0
        TheoryCount = 0
        PropertyCount = 0
        SourceFileCount = 1
        ExternalDependency = "BenchmarkDotNet; no service dependency for current microbenchmarks"
        Duration = "Profile dependent"
        Environment = ".NET Release runtime"
        RecommendedFrequency = "Smoke manually before release; full benchmark manually or scheduled"
        Packages = @(Get-XmlPackageReferences $benchmarkProject)
        ProjectReferences = @(Get-XmlProjectReferences $benchmarkProject)
        Notes = "Benchmark inventory is not scientific validation."
    }) | Out-Null
}

$strykerConfig = Join-Path $repoRoot "stryker-config.json"
$dotnetTools = Join-Path $repoRoot ".config\dotnet-tools.json"
if ((Test-Path -LiteralPath $strykerConfig) -or (Test-Path -LiteralPath $dotnetTools)) {
    $hasStrykerTool = $false
    if (Test-Path -LiteralPath $dotnetTools) {
        $toolsJson = Get-Content -LiteralPath $dotnetTools -Raw | ConvertFrom-Json
        $hasStrykerTool = $null -ne $toolsJson.tools.'dotnet-stryker'
    }

    $inventory.Add([pscustomobject]@{
        Id = "MutationTesting"
        Kind = "QualityTooling"
        Path = if (Test-Path -LiteralPath $strykerConfig) { "stryker-config.json" } else { ".config\dotnet-tools.json" }
        Taxonomy = @("Mutation")
        ExplicitCategories = @()
        TestCount = 0
        FactCount = 0
        TheoryCount = 0
        PropertyCount = 0
        SourceFileCount = 0
        ExternalDependency = "dotnet-stryker tool manifest: $hasStrykerTool"
        Duration = "Manual/scheduled; previous timeout and compile-error reliability must be classified"
        Environment = ".NET test runtime"
        RecommendedFrequency = "Manual before release until stable; scheduled once timeouts are classified"
        Packages = @()
        ProjectReferences = @()
        Notes = "Presence of tooling is not proof of reliable mutation baseline."
    }) | Out-Null
}

$coverageScript = Join-Path $repoRoot "scripts\tests\generate-coverage-report.ps1"
if (Test-Path -LiteralPath $coverageScript) {
    $inventory.Add([pscustomobject]@{
        Id = "BackendCoverage"
        Kind = "QualityTooling"
        Path = Get-RelativePath $repoRoot $coverageScript
        Taxonomy = @("Unit", "Component")
        ExplicitCategories = @()
        TestCount = 0
        FactCount = 0
        TheoryCount = 0
        PropertyCount = 0
        SourceFileCount = 1
        ExternalDependency = "dotnet test, coverlet collector, ReportGenerator"
        Duration = "Medium/slow depending DockerIntegration inclusion"
        Environment = ".NET Release test runtime; Docker optional via -IncludeDockerIntegration"
        RecommendedFrequency = "Local before external verification; scheduled/CI when cost is acceptable"
        Packages = @()
        ProjectReferences = @()
        Notes = "Preserves backend-integral and backend-focused outputs."
    }) | Out-Null
}

if (-not (@($inventory | Where-Object { $_.Taxonomy -contains "PropertyBased" }).Count -gt 0)) {
    $gaps.Add("No property-based tests were detected.") | Out-Null
}

if (-not (Test-Path -LiteralPath $strykerConfig)) {
    $gaps.Add("No stryker-config.json was detected.") | Out-Null
}

$trxFiles = @(Get-ChildItem -Path (Join-Path $repoRoot "artifacts") -Recurse -Filter *.trx -ErrorAction SilentlyContinue)
if ($trxFiles.Count -eq 0) {
    $gaps.Add("No TRX artifacts were found for measured duration enrichment.") | Out-Null
}

$flatRows = foreach ($item in $inventory) {
    [pscustomobject]@{
        Id = $item.Id
        Kind = $item.Kind
        Path = $item.Path
        Taxonomy = Join-Values $item.Taxonomy
        ExplicitCategories = Join-Values $item.ExplicitCategories
        TestCount = $item.TestCount
        FactCount = $item.FactCount
        TheoryCount = $item.TheoryCount
        PropertyCount = $item.PropertyCount
        SourceFileCount = $item.SourceFileCount
        ExternalDependency = $item.ExternalDependency
        Duration = $item.Duration
        Environment = $item.Environment
        RecommendedFrequency = $item.RecommendedFrequency
        Packages = Join-Values $item.Packages
        Notes = $item.Notes
    }
}

$summary = [pscustomobject]@{
    GeneratedAt = (Get-Date).ToString("o")
    RepositoryRoot = $repoRoot
    InventoryCount = $inventory.Count
    BackendProjectCount = @($inventory | Where-Object { $_.Kind -eq "BackendTestProject" }).Count
    TotalDetectedBackendTestAttributes = (@($inventory | Where-Object { $_.Kind -eq "BackendTestProject" } | ForEach-Object { $_.TestCount }) | Measure-Object -Sum).Sum
    TotalDetectedPropertyTests = (@($inventory | Where-Object { $_.Kind -eq "BackendTestProject" } | ForEach-Object { $_.PropertyCount }) | Measure-Object -Sum).Sum
    TrxArtifactCount = $trxFiles.Count
    Gaps = @($gaps | Sort-Object -Unique)
}

$payload = [pscustomobject]@{
    Summary = $summary
    Taxonomy = $taxonomy
    Inventory = @($inventory)
}

$jsonPath = Join-Path $outputDirectory "test-inventory.json"
$csvPath = Join-Path $outputDirectory "test-inventory.csv"
$taxonomyPath = Join-Path $outputDirectory "test-taxonomy.json"
$summaryPath = Join-Path $outputDirectory "summary.md"

$payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$flatRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$taxonomy | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $taxonomyPath -Encoding UTF8

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# Test inventory") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("- Generated at: $($summary.GeneratedAt)") | Out-Null
$markdown.Add("- Backend test projects: $($summary.BackendProjectCount)") | Out-Null
$markdown.Add("- Detected backend test attributes: $($summary.TotalDetectedBackendTestAttributes)") | Out-Null
$markdown.Add("- Detected property-based attributes: $($summary.TotalDetectedPropertyTests)") | Out-Null
$markdown.Add("- TRX artifacts found for future duration enrichment: $($summary.TrxArtifactCount)") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("## Inventory") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Id | Kind | Taxonomy | Test attributes/files | External dependency | Duration | Frequency |") | Out-Null
$markdown.Add("| --- | --- | --- | ---: | --- | --- | --- |") | Out-Null
foreach ($row in $flatRows) {
    $markdown.Add("| $($row.Id) | $($row.Kind) | $($row.Taxonomy) | $($row.TestCount) | $($row.ExternalDependency) | $($row.Duration) | $($row.RecommendedFrequency) |") | Out-Null
}

$markdown.Add("") | Out-Null
$markdown.Add("## Gaps and follow-up checks") | Out-Null
$markdown.Add("") | Out-Null
foreach ($gap in @($summary.Gaps)) {
    $markdown.Add("- $gap") | Out-Null
}

$markdown.Add("") | Out-Null
$markdown.Add("## Classification note") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("This inventory is static evidence from repository files and artifacts. It classifies duration bands and environments conservatively; it does not replace measured TRX timing, coverage reports, mutation reports, or full-stack evidence.") | Out-Null
$markdown | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Test inventory exported to $outputDirectory"
Write-Host "JSON: $jsonPath"
Write-Host "CSV: $csvPath"
Write-Host "Summary: $summaryPath"
