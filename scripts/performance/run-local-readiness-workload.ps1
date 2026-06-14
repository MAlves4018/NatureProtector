param(
    [string]$ApiBaseUrl = "http://localhost:5254",
    [string]$WebBaseUrl = "http://localhost:5173",
    [string]$AreaCode = "proenca-a-nova",
    [string]$OutputRoot = "docs/evidence/readiness",
    [int]$Repetitions = 5,
    [int]$TimeoutSeconds = 10,
    [switch]$SkipWeb,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

if ($Repetitions -lt 1) {
    throw "Repetitions must be greater than or equal to 1."
}

if ($TimeoutSeconds -lt 1) {
    throw "TimeoutSeconds must be greater than or equal to 1."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"

if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $resolvedOutputRoot = $OutputRoot
}
else {
    $resolvedOutputRoot = Join-Path $repoRoot $OutputRoot
}

$runDirectory = Join-Path $resolvedOutputRoot "local-readiness-$timestamp"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$jsonOptions = @{ Depth = 20 }

function Join-Url {
    param(
        [string]$BaseUrl,
        [string]$Path
    )

    $base = $BaseUrl.TrimEnd("/")
    if ($Path.StartsWith("/")) {
        return "$base$Path"
    }

    return "$base/$Path"
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json @jsonOptions | Set-Content -Path $Path -Encoding UTF8
}

function Get-CommandLineVersion {
    param(
        [string]$Command,
        [string[]]$Arguments = @()
    )

    try {
        $output = & $Command @Arguments 2>$null | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($output)) {
            return "Not available"
        }

        return "$output".Trim()
    }
    catch {
        return "Not available"
    }
}

function Get-PercentileNearestRank {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return $null
    }

    $sorted = @($Values | Sort-Object)
    $rank = [Math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    $rank = [Math]::Max(0, [Math]::Min($rank, $sorted.Count - 1))
    return [Math]::Round([double]$sorted[$rank], 2)
}

function New-Probe {
    param(
        [string]$Surface,
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [int[]]$ExpectedStatusCodes,
        [string]$Purpose
    )

    [pscustomobject]@{
        Surface = $Surface
        Name = $Name
        Method = $Method
        Url = $Url
        ExpectedStatusCodes = $ExpectedStatusCodes
        Purpose = $Purpose
    }
}

$apiProbes = @(
    (New-Probe -Surface "api" -Name "api-health" -Method "GET" -Url (Join-Url $ApiBaseUrl "/health") -ExpectedStatusCodes @(200) -Purpose "Measured API health endpoint availability."),
    (New-Probe -Surface "api" -Name "areas-list" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas") -ExpectedStatusCodes @(200) -Purpose "Measured anonymous control-plane area list read path."),
    (New-Probe -Surface "api" -Name "area-detail" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas/$AreaCode") -ExpectedStatusCodes @(200) -Purpose "Measured anonymous area detail read path."),
    (New-Probe -Surface "api" -Name "area-grid-cells" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas/$AreaCode/grid-cells?take=25") -ExpectedStatusCodes @(200) -Purpose "Measured anonymous grid-cell read path with bounded result size."),
    (New-Probe -Surface "api" -Name "area-sensor-nodes" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas/$AreaCode/sensor-nodes") -ExpectedStatusCodes @(200) -Purpose "Measured anonymous sensor-node read path."),
    (New-Probe -Surface "api" -Name "area-alerts-active" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas/$AreaCode/alerts/active") -ExpectedStatusCodes @(200) -Purpose "Measured anonymous active-alert read path."),
    (New-Probe -Surface "api" -Name "area-scenarios-auth-guard" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas/$AreaCode/scenarios") -ExpectedStatusCodes @(401, 403) -Purpose "Observed unauthenticated access is blocked for scenario catalogue."),
    (New-Probe -Surface "api" -Name "area-operational-state-auth-guard" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/areas/$AreaCode/operational-state") -ExpectedStatusCodes @(401, 403) -Purpose "Observed unauthenticated access is blocked for operational state."),
    (New-Probe -Surface "api" -Name "runtime-summary-auth-guard" -Method "GET" -Url (Join-Url $ApiBaseUrl "/api/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30") -ExpectedStatusCodes @(401, 403) -Purpose "Observed unauthenticated access is blocked for runtime summary.")
)

$webProbes = @()
if (-not $SkipWeb) {
    $webProbes = @(
        (New-Probe -Surface "web" -Name "web-root" -Method "GET" -Url (Join-Url $WebBaseUrl "/") -ExpectedStatusCodes @(200) -Purpose "Measured webUI root availability."),
        (New-Probe -Surface "web" -Name "web-ui-v2" -Method "GET" -Url (Join-Url $WebBaseUrl "/ui-v2") -ExpectedStatusCodes @(200) -Purpose "Measured UI v2 route availability through the frontend dev server.")
    )
}

$probes = @($apiProbes + $webProbes)

$environment = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot = $repoRoot
    apiBaseUrl = $ApiBaseUrl
    webBaseUrl = if ($SkipWeb) { "Skipped" } else { $WebBaseUrl }
    areaCode = $AreaCode
    repetitions = $Repetitions
    timeoutSeconds = $TimeoutSeconds
    dryRun = [bool]$DryRun
    scope = "Measured local HTTP availability and response elapsed time only; not a load test, stress test, broker-depth test, end-to-end event-latency test, or external validation."
    dotnet = Get-CommandLineVersion -Command "dotnet" -Arguments @("--version")
    node = Get-CommandLineVersion -Command "node" -Arguments @("--version")
    npm = Get-CommandLineVersion -Command "npm" -Arguments @("--version")
    dockerClient = Get-CommandLineVersion -Command "docker" -Arguments @("version", "--format", "{{.Client.Version}}")
    machineName = $env:COMPUTERNAME
    osVersion = [System.Environment]::OSVersion.VersionString
}

Write-JsonFile -Path (Join-Path $runDirectory "manifest.json") -Value $environment
Write-JsonFile -Path (Join-Path $runDirectory "probes.json") -Value $probes

if ($DryRun) {
    @(
        "# Local readiness workload dry run",
        "",
        "- GeneratedAtUtc: $($environment.generatedAtUtc)",
        "- OutputDirectory: $runDirectory",
        "- Probes configured: $($probes.Count)",
        "- HTTP calls executed: 0",
        "",
        "Dry-run mode validates parameters, output directory creation and probe selection only."
    ) | Set-Content -Path (Join-Path $runDirectory "summary.md") -Encoding UTF8

    Write-Host "Dry run complete. Evidence directory: $runDirectory"
    exit 0
}

$clientHandler = [System.Net.Http.HttpClientHandler]::new()
$client = [System.Net.Http.HttpClient]::new($clientHandler)
$client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)

$measurements = @()

try {
    for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
        foreach ($probe in $probes) {
            $statusCode = $null
            $byteCount = $null
            $errorKind = ""
            $errorMessage = ""
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

            try {
                $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($probe.Method), $probe.Url)
                $request.Headers.UserAgent.ParseAdd("NatureProtector-LocalReadiness/1.0")

                $response = $client.SendAsync($request).GetAwaiter().GetResult()
                $statusCode = [int]$response.StatusCode
                $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                $byteCount = $bytes.Length

                $response.Dispose()
                $request.Dispose()
            }
            catch {
                $errorKind = $_.Exception.GetType().Name
                $errorMessage = $_.Exception.Message
            }
            finally {
                $stopwatch.Stop()
            }

            $expected = $false
            if ($null -ne $statusCode) {
                $expected = $probe.ExpectedStatusCodes -contains [int]$statusCode
            }

            $measurements += [pscustomobject]@{
                generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
                iteration = $iteration
                surface = $probe.Surface
                name = $probe.Name
                method = $probe.Method
                url = $probe.Url
                expectedStatusCodes = ($probe.ExpectedStatusCodes -join "|")
                statusCode = $statusCode
                expectedStatusObserved = $expected
                elapsedMs = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
                byteCount = $byteCount
                errorKind = $errorKind
                errorMessage = $errorMessage
                purpose = $probe.Purpose
            }
        }
    }
}
finally {
    $client.Dispose()
    $clientHandler.Dispose()
}

$measurementRows = @($measurements)
$measurementRows | Export-Csv -Path (Join-Path $runDirectory "measurements.csv") -NoTypeInformation -Encoding UTF8
Write-JsonFile -Path (Join-Path $runDirectory "measurements.json") -Value $measurementRows

$summaries = @()
foreach ($group in ($measurementRows | Group-Object surface, name)) {
    $rows = @($group.Group)
    $elapsedValues = @($rows | Where-Object { $_.expectedStatusObserved } | ForEach-Object { [double]$_.elapsedMs })
    $statusCodes = @($rows | ForEach-Object { if ($null -eq $_.statusCode) { "none" } else { "$($_.statusCode)" } } | Sort-Object -Unique)

    $summaries += [pscustomobject]@{
        surface = $rows[0].surface
        name = $rows[0].name
        attempts = $rows.Count
        expectedStatusCount = @($rows | Where-Object { $_.expectedStatusObserved }).Count
        unexpectedStatusOrErrorCount = @($rows | Where-Object { -not $_.expectedStatusObserved }).Count
        observedStatusCodes = ($statusCodes -join "|")
        minElapsedMs = if ($elapsedValues.Count -gt 0) { [Math]::Round(($elapsedValues | Measure-Object -Minimum).Minimum, 2) } else { $null }
        avgElapsedMs = if ($elapsedValues.Count -gt 0) { [Math]::Round(($elapsedValues | Measure-Object -Average).Average, 2) } else { $null }
        p50ElapsedMs = Get-PercentileNearestRank -Values $elapsedValues -Percentile 50
        p95ElapsedMs = Get-PercentileNearestRank -Values $elapsedValues -Percentile 95
        maxElapsedMs = if ($elapsedValues.Count -gt 0) { [Math]::Round(($elapsedValues | Measure-Object -Maximum).Maximum, 2) } else { $null }
        purpose = $rows[0].purpose
    }
}

$summaries | Export-Csv -Path (Join-Path $runDirectory "summary.csv") -NoTypeInformation -Encoding UTF8
Write-JsonFile -Path (Join-Path $runDirectory "summary.json") -Value $summaries

$totalAttempts = $measurementRows.Count
$expectedAttempts = @($measurementRows | Where-Object { $_.expectedStatusObserved }).Count
$unexpectedAttempts = $totalAttempts - $expectedAttempts

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Local readiness workload summary")
$markdown.Add("")
$markdown.Add("- GeneratedAtUtc: $($environment.generatedAtUtc)")
$markdown.Add("- OutputDirectory: $runDirectory")
$markdown.Add("- ApiBaseUrl: $ApiBaseUrl")
$markdown.Add("- WebBaseUrl: $(if ($SkipWeb) { 'Skipped' } else { $WebBaseUrl })")
$markdown.Add("- AreaCode: $AreaCode")
$markdown.Add("- Repetitions: $Repetitions")
$markdown.Add("- Probes: $($probes.Count)")
$markdown.Add("- Attempts: $totalAttempts")
$markdown.Add("- Expected status attempts: $expectedAttempts")
$markdown.Add("- Unexpected status or error attempts: $unexpectedAttempts")
$markdown.Add("")
$markdown.Add("## Probe summary")
$markdown.Add("")
$markdown.Add("| Surface | Probe | Expected/attempts | Status codes | Avg ms | P95 ms | Purpose |")
$markdown.Add("| --- | --- | ---: | --- | ---: | ---: | --- |")

foreach ($summary in ($summaries | Sort-Object surface, name)) {
    $markdown.Add("| $($summary.surface) | $($summary.name) | $($summary.expectedStatusCount)/$($summary.attempts) | $($summary.observedStatusCodes) | $($summary.avgElapsedMs) | $($summary.p95ElapsedMs) | $($summary.purpose) |")
}

$markdown.Add("")
$markdown.Add("## Classification")
$markdown.Add("")
$markdown.Add("- HTTP status and elapsed time in this file are Measured locally.")
$markdown.Add("- API/web availability from these probes is Observed for this workstation/run only.")
$markdown.Add("- Capacity conclusions derived from these values are Estimated unless supported by a separate load test.")
$markdown.Add("- Broker queue depth, per-event end-to-end latency and production readiness are Not instrumented by this script.")
$markdown.Add("- External stakeholder validation and scientific calibration are Not validated by this script.")

$markdown | Set-Content -Path (Join-Path $runDirectory "summary.md") -Encoding UTF8

if ($unexpectedAttempts -gt 0) {
    Write-Host "Readiness workload completed with unexpected status or error attempts. Evidence directory: $runDirectory"
    exit 1
}

Write-Host "Readiness workload complete. Evidence directory: $runDirectory"
