param(
    [string]$RepositoryRoot = ".",
    [string]$OutputRoot = "artifacts/observability/telemetry-catalog"
)

$ErrorActionPreference = "Stop"

function Get-CardinalityClass {
    param([string]$TagName)

    switch -Regex ($TagName) {
        "EventId|CorrelationId|InboxEventId|SimulationRunId|SensorId|SensorName" { return "HighIdentifier" }
        "AreaId|ScenarioId" { return "DomainIdentifier" }
        "AttemptNumber|ConfigurationVersion" { return "BoundedNumericOrVersion" }
        "ErrorCode|RejectionCode|QuarantineCode|RetryKind|Outcome|MetricType|RiskLevel|Severity|Stage|Measurement|Operation|Host" { return "LowEnumOrCategory" }
        "HasAcceptedReadings|HasRiskAssessments|HasAreaRiskSnapshots" { return "Boolean" }
        default { return "Review" }
    }
}

$repoRoot = (Resolve-Path $RepositoryRoot).Path
$sourcePath = Join-Path $repoRoot "src\NatureProtector.Shared.Observability\Observability\HostTelemetry.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "HostTelemetry.cs not found at $sourcePath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $timestamp)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$source = Get-Content -LiteralPath $sourcePath -Raw

$tagRows = foreach ($match in [regex]::Matches($source, 'public const string (?<name>\w+) = "(?<value>[^"]+)";')) {
    $name = $match.Groups["name"].Value
    $value = $match.Groups["value"].Value

    [pscustomobject]@{
        name = $name
        value = $value
        cardinality = Get-CardinalityClass -TagName $name
    }
}

$serviceRows = [System.Collections.Generic.List[object]]::new()
$metricRows = [System.Collections.Generic.List[object]]::new()

$classBlocks = [regex]::Matches(
    $source,
    'public static class (?<className>\w+Telemetry)\s*\{(?<body>.*?)(?=public static class|\z)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

foreach ($classBlock in $classBlocks) {
    $className = $classBlock.Groups["className"].Value
    $body = $classBlock.Groups["body"].Value
    $serviceMatch = [regex]::Match($body, 'public const string ServiceName = "(?<service>[^"]+)";')
    if (-not $serviceMatch.Success) {
        continue
    }

    $serviceName = $serviceMatch.Groups["service"].Value
    $hasActivitySource = $body -match 'ActivitySource\s*=\s*new\(ServiceName\)'
    $hasMeter = $body -match 'Meter\s*=\s*new\(ServiceName\)'

    $serviceRows.Add([pscustomobject]@{
        className = $className
        serviceName = $serviceName
        activitySource = [bool]$hasActivitySource
        meter = [bool]$hasMeter
    }) | Out-Null

    foreach ($metricMatch in [regex]::Matches($body, 'Meter\.Create(?<instrument>Counter|Histogram)<[^>]+>\("(?<name>[^"]+)"(?:,\s*unit:\s*"(?<unit>[^"]+)")?')) {
        $metricRows.Add([pscustomobject]@{
            serviceName = $serviceName
            className = $className
            instrument = $metricMatch.Groups["instrument"].Value
            metricName = $metricMatch.Groups["name"].Value
            unit = $metricMatch.Groups["unit"].Value
        }) | Out-Null
    }
}

$catalog = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    source = "src/NatureProtector.Shared.Observability/Observability/HostTelemetry.cs"
    services = @($serviceRows)
    metrics = @($metricRows)
    tags = @($tagRows)
    limitations = @(
        "Static catalog derived from code, not a remote collector scrape.",
        "HighIdentifier tags should be used carefully in dashboards and metric dimensions.",
        "Cross-service trace correlation is not proved by this catalog."
    )
}

$catalog | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runDirectory "telemetry-catalog.json") -Encoding UTF8
$metricRows | Export-Csv -LiteralPath (Join-Path $runDirectory "metrics.csv") -NoTypeInformation -Encoding UTF8
$tagRows | Export-Csv -LiteralPath (Join-Path $runDirectory "tags.csv") -NoTypeInformation -Encoding UTF8

$highCardinalityLines = $tagRows |
    Where-Object { $_.cardinality -in @("HighIdentifier", "DomainIdentifier") } |
    ForEach-Object { "- ``$($_.value)`` -> $($_.cardinality)" } |
    Out-String

$summary = @"
# Telemetry catalog

Generated: $($catalog.generatedAtUtc)

Services: $($serviceRows.Count)
Metrics: $($metricRows.Count)
Tags: $($tagRows.Count)

## Scope

Static catalog derived from `HostTelemetry.cs`. This is useful for review, dashboard design and cardinality control. It is not proof of remote collector delivery or cross-service correlation.

## High-cardinality tags

$highCardinalityLines
"@

$summary | Set-Content -LiteralPath (Join-Path $runDirectory "summary.md") -Encoding UTF8

Write-Host "Telemetry catalog exported to $runDirectory"
Write-Host "Services: $($serviceRows.Count)"
Write-Host "Metrics: $($metricRows.Count)"
Write-Host "Tags: $($tagRows.Count)"
