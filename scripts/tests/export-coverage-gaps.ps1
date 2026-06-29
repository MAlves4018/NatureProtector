<#
.SYNOPSIS
Exports zero and low coverage findings from generated ReportGenerator summaries.

.DESCRIPTION
Reads backend-integral and backend-focused Summary.txt files and writes a
machine-readable coverage gap report under artifacts/validation. This script
does not execute tests; run generate-coverage-report.ps1 first when fresh
coverage is required.
#>

[CmdletBinding()]
param(
    [string]$CoverageRoot = ".\artifacts\coverage",
    [string]$OutputRoot = ".\artifacts\validation\coverage-gaps",
    [double]$LowCoverageThreshold = 50.0
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"

function Resolve-UnderRoot {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($Root)
    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $rootFullPath $Path
    }

    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    if (-not ($fullPath.StartsWith($rootFullPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($fullPath, $rootFullPath, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to read or write outside repository root: $fullPath"
    }

    return $fullPath
}

function Parse-CoverageSummary {
    param(
        [string]$Profile,
        [string]$SummaryPath
    )

    $items = [System.Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -LiteralPath $SummaryPath) {
        $match = [regex]::Match($line, '^(?<indent>\s*)(?<name>[A-Za-z0-9_.`<>+]+)\s+(?<coverage>\d+(?:\.\d+)?)%$')
        if (-not $match.Success) {
            continue
        }

        $name = $match.Groups["name"].Value
        $coverage = [double]::Parse($match.Groups["coverage"].Value, [System.Globalization.CultureInfo]::InvariantCulture)
        $kind = if ($match.Groups["indent"].Value.Length -eq 0) { "Assembly" } else { "Class" }

        $items.Add([pscustomobject]@{
            Profile = $Profile
            Kind = $kind
            Name = $name
            LineCoveragePercent = $coverage
        }) | Out-Null
    }

    return $items
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'artifacts')
$coverageRootPath = Resolve-UnderRoot $repoRoot $CoverageRoot
$outputRootPath = Resolve-UnderRoot $repoRoot $OutputRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDirectory = Join-Path $outputRootPath $runId
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$profiles = @(
    [pscustomobject]@{ Name = "backend-integral"; SummaryPath = Join-Path $coverageRootPath "backend-integral\Summary.txt" },
    [pscustomobject]@{ Name = "backend-focused"; SummaryPath = Join-Path $coverageRootPath "backend-focused\Summary.txt" }
)

$items = [System.Collections.Generic.List[object]]::new()
foreach ($profile in $profiles) {
    if (-not (Test-Path -LiteralPath $profile.SummaryPath)) {
        throw "Coverage summary not found for $($profile.Name): $($profile.SummaryPath)"
    }

    foreach ($item in Parse-CoverageSummary $profile.Name $profile.SummaryPath) {
        $items.Add($item) | Out-Null
    }
}

$zeroCoverage = @($items | Where-Object { $_.Kind -eq "Class" -and $_.LineCoveragePercent -eq 0 } | Sort-Object Profile, Name)
$lowCoverage = @($items | Where-Object { $_.Kind -eq "Class" -and $_.LineCoveragePercent -gt 0 -and $_.LineCoveragePercent -lt $LowCoverageThreshold } | Sort-Object Profile, LineCoveragePercent, Name)
$assemblies = @($items | Where-Object { $_.Kind -eq "Assembly" } | Sort-Object Profile, Name)

$payload = [pscustomobject]@{
    GeneratedAt = (Get-Date).ToString("o")
    CoverageRoot = $coverageRootPath
    LowCoverageThreshold = $LowCoverageThreshold
    Assemblies = $assemblies
    ZeroCoverageClasses = $zeroCoverage
    LowCoverageClasses = $lowCoverage
    Notes = @(
        "backend-integral is broad runtime/backend coverage and includes composition roots, migrations, DTOs and glue.",
        "backend-focused is intentionally narrow and must not be presented as global backend coverage.",
        "Zero coverage in generated migrations or DTO-only contracts is not automatically a defect; classify before adding tests."
    )
}

$jsonPath = Join-Path $outputDirectory "coverage-gaps.json"
$zeroCsvPath = Join-Path $outputDirectory "zero-coverage-classes.csv"
$lowCsvPath = Join-Path $outputDirectory "low-coverage-classes.csv"
$summaryPath = Join-Path $outputDirectory "summary.md"

$payload | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$zeroCoverage | Export-Csv -LiteralPath $zeroCsvPath -NoTypeInformation -Encoding UTF8
$lowCoverage | Export-Csv -LiteralPath $lowCsvPath -NoTypeInformation -Encoding UTF8

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# Coverage gaps") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("- Generated at: $($payload.GeneratedAt)") | Out-Null
$markdown.Add("- Low coverage threshold: $LowCoverageThreshold%") | Out-Null
$markdown.Add("- Zero-coverage classes: $($zeroCoverage.Count)") | Out-Null
$markdown.Add("- Low non-zero coverage classes: $($lowCoverage.Count)") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("## Assemblies") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Profile | Assembly | Line coverage |") | Out-Null
$markdown.Add("| --- | --- | ---: |") | Out-Null
foreach ($assembly in $assemblies) {
    $markdown.Add("| $($assembly.Profile) | $($assembly.Name) | $($assembly.LineCoveragePercent)% |") | Out-Null
}

$markdown.Add("") | Out-Null
$markdown.Add("## Zero-coverage classes") | Out-Null
$markdown.Add("") | Out-Null
if ($zeroCoverage.Count -eq 0) {
    $markdown.Add("No zero-coverage classes were parsed.") | Out-Null
}
else {
    $markdown.Add("| Profile | Class |") | Out-Null
    $markdown.Add("| --- | --- |") | Out-Null
    foreach ($item in $zeroCoverage) {
        $markdown.Add("| $($item.Profile) | $($item.Name) |") | Out-Null
    }
}

$markdown.Add("") | Out-Null
$markdown.Add("## Low non-zero coverage classes") | Out-Null
$markdown.Add("") | Out-Null
if ($lowCoverage.Count -eq 0) {
    $markdown.Add("No low non-zero coverage classes were parsed.") | Out-Null
}
else {
    $markdown.Add("| Profile | Class | Line coverage |") | Out-Null
    $markdown.Add("| --- | --- | ---: |") | Out-Null
    foreach ($item in $lowCoverage) {
        $markdown.Add("| $($item.Profile) | $($item.Name) | $($item.LineCoveragePercent)% |") | Out-Null
    }
}

$markdown.Add("") | Out-Null
$markdown.Add("## Interpretation guardrails") | Out-Null
$markdown.Add("") | Out-Null
foreach ($note in $payload.Notes) {
    $markdown.Add("- $note") | Out-Null
}

$markdown | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Coverage gap report exported to $outputDirectory"
Write-Host "JSON: $jsonPath"
Write-Host "Zero CSV: $zeroCsvPath"
Write-Host "Low CSV: $lowCsvPath"
Write-Host "Summary: $summaryPath"
