<#
.SYNOPSIS
Exports a read-only inventory of generated artifacts and tool outputs.

.DESCRIPTION
The inventory supports artifact hygiene review without deleting or moving
evidence. It does not execute Git commands and does not read secret values.
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputRoot = Join-Path $RepositoryRoot "artifacts\validation\artifact-hygiene\$timestamp"
}

$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$targets = @(
    @{ Path = "artifacts"; Classification = "EvidenceRoot"; Decision = "Preserve"; Notes = "Canonical generated evidence root." },
    @{ Path = "artifacts\coverage"; Classification = "CoverageEvidence"; Decision = "Preserve"; Notes = "Current backend coverage evidence." },
    @{ Path = "artifacts\validation"; Classification = "ValidationEvidence"; Decision = "Preserve"; Notes = "Validation exports, inventories and audits." },
    @{ Path = "artifacts\security"; Classification = "SecurityEvidence"; Decision = "Preserve"; Notes = "Security scan outputs when present." },
    @{ Path = "artifacts\secret-scan"; Classification = "SecurityEvidence"; Decision = "Preserve"; Notes = "Local secret-scan reports." },
    @{ Path = "artifacts\mutation"; Classification = "MutationEvidence"; Decision = "Preserve"; Notes = "Stryker wrapper logs and blocked classification evidence." },
    @{ Path = "artifacts\performance"; Classification = "PerformanceEvidence"; Decision = "Preserve"; Notes = "Benchmark and system workload outputs." },
    @{ Path = "artifacts\release"; Classification = "ReleaseEvidence"; Decision = "Preserve"; Notes = "Release candidate package and validation outputs." },
    @{ Path = "artifacts\recovery"; Classification = "RecoveryEvidence"; Decision = "PreserveIfPresent"; Notes = "Recovery evidence when present." },
    @{ Path = "artifacts\mission-progress"; Classification = "MissionProgress"; Decision = "Preserve"; Notes = "Mission progress snapshots." },
    @{ Path = "artifacts\tests"; Classification = "LargeTestOutput"; Decision = "ReviewLargePreserveByDefault"; Notes = "Large generated test outputs; do not delete without owner decision." },
    @{ Path = "TestResults"; Classification = "LegacyTestOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Legacy test output outside artifacts." },
    @{ Path = "graphify-out"; Classification = "GraphifyOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Knowledge graph output; dirty files expected." },
    @{ Path = "StrykerOutput"; Classification = "MutationToolOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Tool default output outside wrapper artifacts." },
    @{ Path = "BenchmarkDotNet.Artifacts"; Classification = "BenchmarkToolOutput"; Decision = "IgnoredIfPresent"; Notes = "BenchmarkDotNet default output outside artifacts." },
    @{ Path = "coveragereport_backend"; Classification = "LegacyCoverageOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Legacy coverage output outside artifacts." },
    @{ Path = "coveragereport_core"; Classification = "LegacyCoverageOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Legacy coverage output outside artifacts." },
    @{ Path = "playwright-report"; Classification = "FrontendToolOutput"; Decision = "IgnoredIfPresent"; Notes = "Playwright default output outside webUI/artifacts." },
    @{ Path = "test-results"; Classification = "FrontendToolOutput"; Decision = "IgnoredIfPresent"; Notes = "Playwright default test output outside webUI/artifacts." },
    @{ Path = "webUI\playwright-report"; Classification = "FrontendToolOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Frontend Playwright report." },
    @{ Path = "webUI\test-results"; Classification = "FrontendToolOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Frontend Playwright/JUnit output." },
    @{ Path = "webUI\coverage"; Classification = "FrontendCoverageOutput"; Decision = "IgnoredPreserveByDefault"; Notes = "Frontend coverage output." }
)

function Get-InventoryRow {
    param(
        [hashtable]$Target
    )

    $relativePath = $Target.Path
    $absolutePath = Join-Path $resolvedRepositoryRoot $relativePath
    $exists = Test-Path -LiteralPath $absolutePath

    $fileCount = 0
    $sizeBytes = 0
    $lastWrite = $null
    $largeFileCount = 0
    $secretNameCandidateCount = 0

    if ($exists) {
        $item = Get-Item -LiteralPath $absolutePath
        if ($item.PSIsContainer) {
            $files = @(Get-ChildItem -LiteralPath $absolutePath -Recurse -File -ErrorAction SilentlyContinue)
        }
        else {
            $files = @($item)
        }

        $fileCount = $files.Count
        $sizeBytes = ($files | Measure-Object Length -Sum).Sum
        $lastWrite = $files | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty LastWriteTime
        $largeFileCount = @($files | Where-Object { $_.Length -ge 100MB }).Count
        $secretNameCandidateCount = @(
            $files | Where-Object {
                $_.Name -match "secret|token|password|credential|key|\.env"
            }
        ).Count
    }

    [pscustomobject]@{
        path = $relativePath
        exists = $exists
        classification = $Target.Classification
        decision = $Target.Decision
        files = $fileCount
        sizeMB = [math]::Round(($sizeBytes / 1MB), 2)
        largeFiles100MBOrMore = $largeFileCount
        secretNameCandidates = $secretNameCandidateCount
        lastWrite = if ($lastWrite) { $lastWrite.ToString("o") } else { $null }
        notes = $Target.Notes
    }
}

$rows = foreach ($target in $targets) {
    Get-InventoryRow -Target $target
}

$summary = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("o")
    repositoryRoot = $resolvedRepositoryRoot
    outputRoot = $OutputRoot
    targetCount = $rows.Count
    existingTargetCount = @($rows | Where-Object { $_.exists }).Count
    totalFiles = ($rows | Measure-Object files -Sum).Sum
    summedTargetSizeMB = [math]::Round((($rows | Measure-Object sizeMB -Sum).Sum), 2)
    largeTargets = @($rows | Where-Object { $_.sizeMB -ge 500 } | Select-Object path, sizeMB, files, classification, decision)
    secretNameCandidateTargets = @($rows | Where-Object { $_.secretNameCandidates -gt 0 } | Select-Object path, secretNameCandidates, classification, decision)
    notes = @(
        "This inventory is read-only and does not delete, move or rewrite evidence.",
        "Large generated outputs are classified for owner review instead of automatic cleanup.",
        "Nested targets overlap, so summedTargetSizeMB is a review signal and not deduplicated disk usage.",
        "secretNameCandidates are filename signals only; secret values are not read."
    )
}

$inventoryJson = Join-Path $OutputRoot "inventory.json"
$inventoryCsv = Join-Path $OutputRoot "inventory.csv"
$summaryJson = Join-Path $OutputRoot "summary.json"
$summaryMd = Join-Path $OutputRoot "summary.md"

ConvertTo-Json -InputObject $rows -Depth 5 | Set-Content -LiteralPath $inventoryJson -Encoding UTF8
$rows | Export-Csv -LiteralPath $inventoryCsv -NoTypeInformation -Encoding UTF8
ConvertTo-Json -InputObject $summary -Depth 6 | Set-Content -LiteralPath $summaryJson -Encoding UTF8

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# Artifact hygiene inventory") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("Generated at: $($summary.generatedAt)") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Metric | Value |") | Out-Null
$markdown.Add("| --- | ---: |") | Out-Null
$markdown.Add("| Targets | $($summary.targetCount) |") | Out-Null
$markdown.Add("| Existing targets | $($summary.existingTargetCount) |") | Out-Null
$markdown.Add("| Total files counted | $($summary.totalFiles) |") | Out-Null
$markdown.Add("| Summed target size MB | $($summary.summedTargetSizeMB) |") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("## Inventory") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Path | Exists | Classification | Decision | Files | Size MB | Notes |") | Out-Null
$markdown.Add("| --- | --- | --- | --- | ---: | ---: | --- |") | Out-Null
foreach ($row in ($rows | Sort-Object path)) {
    $markdown.Add("| $($row.path) | $($row.exists) | $($row.classification) | $($row.decision) | $($row.files) | $($row.sizeMB) | $($row.notes) |") | Out-Null
}
$markdown.Add("") | Out-Null
$markdown.Add("## Notes") | Out-Null
$markdown.Add("") | Out-Null
foreach ($note in $summary.notes) {
    $markdown.Add("- $note") | Out-Null
}

Set-Content -LiteralPath $summaryMd -Value $markdown -Encoding UTF8

Write-Host "Artifact hygiene inventory exported to $OutputRoot"
Write-Host "Existing targets: $($summary.existingTargetCount)/$($summary.targetCount)"
Write-Host "Summed target size MB: $($summary.summedTargetSizeMB)"
