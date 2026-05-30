param(
    [string]$ApiBaseUrl = "http://localhost:5254",
    [string]$AreaCode = "proenca-a-nova",
    [string]$OutputRoot = "docs/evidence/runs",
    [int]$SensorCount = 6,
    [int]$NumberOfCycles = 5,
    [int]$IntervalSeconds = 5,
    [int]$Seed = 12345,
    [int]$TimeoutSeconds = 60,
    [switch]$DryRun,
    [switch]$SkipReset,
    [switch]$CollectRuntimeProcessEvidence
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
$runDirectory = Join-Path $OutputRoot "v1-bc-smoke-$timestamp"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$jsonOptions = @{ Depth = 50 }
$diagnostics = @(
    "latest-run-expected-vs-observed",
    "latest-run-np-vs-fwi-kbdi",
    "latest-run-portuguese-context-proxy",
    "latest-run-kbdi-series-context",
    "latest-run-components",
    "latest-run-quality-by-profile",
    "latest-run-degradation-effects",
    "latest-run-cell-context",
    "latest-run-fwi-input-completeness",
    "latest-run-kbdi-input-completeness",
    "latest-run-coverage-freshness",
    "compare-latest-b-vs-c"
)

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json @jsonOptions | Set-Content -Path $Path -Encoding UTF8
}

function Invoke-JsonRequest {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = "$ApiBaseUrl$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json"
    }

    return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body ($Body | ConvertTo-Json @jsonOptions)
}

function Start-ScenarioRun {
    param(
        [string]$ScenarioCode,
        [string[]]$DegradationProfiles
    )

    $body = @{
        areaCode = $AreaCode
        scenarioCode = $ScenarioCode
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        seed = $Seed
        degradationProfile = if ($DegradationProfiles.Count -eq 1) { $DegradationProfiles[0] } else { ($DegradationProfiles -join "+") }
        degradationProfiles = $DegradationProfiles
        collectEvidence = [bool]$CollectRuntimeProcessEvidence
        waitForCompletion = $true
        timeoutSeconds = $TimeoutSeconds
        allowParallelRun = $false
        runLabel = "$ScenarioCode-smoke"
    }

    Invoke-JsonRequest -Method "POST" -Path "/api/control/runtime/runs" -Body $body
}

function Get-RunAudit {
    param([string]$RunId)
    Invoke-JsonRequest -Method "GET" -Path "/api/control/runtime/runs/$RunId/audit"
}

function Assert-SmokeCondition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-ResolvedProfiles {
    param([object]$RunResponse)

    $resolved = $RunResponse.run.runOverrides.resolved
    if ($null -ne $resolved.degradationProfiles -and $resolved.degradationProfiles.Count -gt 0) {
        return @($resolved.degradationProfiles | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    }

    if (-not [string]::IsNullOrWhiteSpace($resolved.degradationProfile)) {
        return @($resolved.degradationProfile -split "[,+;|]" | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    }

    return @()
}

function Assert-IndexComparison {
    param([object]$IndexComparison)

    $incompleteStatuses = @("Missing", "Partial", "NotAvailable")
    Assert-SmokeCondition -Condition ($null -ne $IndexComparison) -Message "NP/FWI/KBDI comparison is missing from runtime summary."
    Assert-SmokeCondition -Condition ($null -ne $IndexComparison.fireWeatherIndex -or -not [string]::IsNullOrWhiteSpace($IndexComparison.fireWeatherCalculationStatus)) -Message "FWI value/status is missing from index comparison."
    Assert-SmokeCondition -Condition ($null -ne $IndexComparison.keetchByramDroughtIndex -or -not [string]::IsNullOrWhiteSpace($IndexComparison.kbdiCalculationStatus)) -Message "KBDI value/status is missing from index comparison."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($IndexComparison.fireWeatherIpmaClass) -or $incompleteStatuses -contains "$($IndexComparison.fireWeatherCalculationStatus)") -Message "FWI IPMA class is missing for a complete FWI value."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($IndexComparison.kbdiDrynessClass) -or $incompleteStatuses -contains "$($IndexComparison.kbdiCalculationStatus)") -Message "KBDI dryness class is missing for a complete KBDI value."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($IndexComparison.portugueseContextRiskProxyClass) -or -not [string]::IsNullOrWhiteSpace($IndexComparison.portugueseContextRiskProxyLabel)) -Message "Portuguese context proxy is missing from index comparison."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($IndexComparison.localFwiPercentileStatus)) -Message "Local FWI percentile status is missing from index comparison."

    if ($incompleteStatuses -contains "$($IndexComparison.fireWeatherCalculationStatus)") {
        Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($IndexComparison.limitations)) -Message "FWI is incomplete but limitations are not exposed."
    }

    if ($incompleteStatuses -contains "$($IndexComparison.kbdiCalculationStatus)") {
        Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($IndexComparison.limitations)) -Message "KBDI is incomplete but limitations are not exposed."
    }
}

$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    apiBaseUrl = $ApiBaseUrl
    areaCode = $AreaCode
    sensorCount = $SensorCount
    numberOfCycles = $NumberOfCycles
    intervalSeconds = $IntervalSeconds
    seed = $Seed
    dryRun = [bool]$DryRun
    collectRuntimeProcessEvidence = [bool]$CollectRuntimeProcessEvidence
    diagnostics = $diagnostics
    outputDirectory = (Resolve-Path $runDirectory).Path
}
Write-JsonFile -Path (Join-Path $runDirectory "run-spec.resolved.json") -Value $manifest

if ($DryRun) {
    @"
# V1 B/C smoke dry run

The script resolved its run specification and output directory.
No HTTP calls were made because -DryRun was used.

Required runtime:
- Backoffice API running at $ApiBaseUrl
- PostgreSQL/RabbitMQ/Prevention/Simulator dependencies available
- Development environment enabled for runtime orchestration
"@ | Set-Content -Path (Join-Path $runDirectory "summary.md") -Encoding UTF8

    @"
# V1 B/C smoke limitations

Dry-run mode only validates script parameters, output directory creation and resolved evidence manifest.
No API calls, reset, simulation runs, diagnostics or runtime assertions were executed.
"@ | Set-Content -Path (Join-Path $runDirectory "limitations.md") -Encoding UTF8

    Write-Host "Dry run complete. Evidence directory: $runDirectory"
    exit 0
}

try {
    $summaryBefore = Invoke-JsonRequest -Method "GET" -Path "/api/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30"
    Write-JsonFile -Path (Join-Path $runDirectory "runtime-summary-before.json") -Value $summaryBefore

    if (-not $SkipReset) {
        $resetBody = @{
            scope = "runtime-only"
            confirm = "RESET_RUNTIME_STATE"
            dryRun = $false
        }
        $reset = Invoke-JsonRequest -Method "POST" -Path "/api/control/runtime/reset" -Body $resetBody
        Write-JsonFile -Path (Join-Path $runDirectory "reset.json") -Value $reset
    }

    $runB = Start-ScenarioRun -ScenarioCode "scenario_b" -DegradationProfiles @("none")
    Write-JsonFile -Path (Join-Path $runDirectory "run-b.json") -Value $runB
    Assert-SmokeCondition -Condition ($null -ne $runB.run -and $runB.run.status -eq "Completed") -Message "scenario_b did not complete."

    $auditB = Get-RunAudit -RunId $runB.run.id
    Write-JsonFile -Path (Join-Path $runDirectory "audit-b.json") -Value $auditB

    $runC = Start-ScenarioRun -ScenarioCode "scenario_c" -DegradationProfiles @("missing-readings")
    Write-JsonFile -Path (Join-Path $runDirectory "run-c.json") -Value $runC
    Assert-SmokeCondition -Condition ($null -ne $runC.run -and $runC.run.status -eq "Completed") -Message "scenario_c did not complete."

    $auditC = Get-RunAudit -RunId $runC.run.id
    Write-JsonFile -Path (Join-Path $runDirectory "audit-c.json") -Value $auditC

    $profilesC = Get-ResolvedProfiles -RunResponse $runC
    Assert-SmokeCondition -Condition ($runB.run.id -ne $runC.run.id) -Message "scenario_b and scenario_c produced the same SimulationRunId."
    Assert-SmokeCondition -Condition ($profilesC -contains "missing-readings") -Message "scenario_c resolved profiles do not include missing-readings."
    Assert-SmokeCondition -Condition ($auditB.riskAssessments -gt 0) -Message "scenario_b produced no risk assessments."
    Assert-SmokeCondition -Condition ($auditC.riskAssessments -gt 0) -Message "scenario_c produced no risk assessments."
    Assert-SmokeCondition -Condition ($auditB.rejected -eq 0 -and $auditB.quarantined -eq 0) -Message "scenario_b has rejected or quarantined events."
    Assert-SmokeCondition -Condition ($auditC.rejected -eq 0 -and $auditC.quarantined -eq 0) -Message "scenario_c has rejected or quarantined events."
    Assert-SmokeCondition -Condition ($auditC.missingEvents -gt 0) -Message "scenario_c missing-readings did not produce missing events."

    $diagnosticResults = @{}
    foreach ($diagnostic in $diagnostics) {
        $result = Invoke-JsonRequest -Method "POST" -Path "/api/control/runtime/diagnostics/$diagnostic" -Body @{
            areaCode = $AreaCode
            recentMinutes = 30
            scenarioCode = "scenario_b"
        }
        $diagnosticResults[$diagnostic] = $result
        Write-JsonFile -Path (Join-Path $runDirectory "$diagnostic.json") -Value $result
    }

    $summaryAfter = Invoke-JsonRequest -Method "GET" -Path "/api/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30"
    Write-JsonFile -Path (Join-Path $runDirectory "runtime-summary.json") -Value $summaryAfter
    Write-JsonFile -Path (Join-Path $runDirectory "diagnostics.json") -Value $diagnosticResults

    Assert-SmokeCondition -Condition ($null -ne $summaryAfter.scoreComponents -and $null -ne $summaryAfter.scoreComponents.npScore) -Message "NatureProtector score is missing from runtime summary."
    Assert-IndexComparison -IndexComparison $summaryAfter.indexComparison
    Assert-SmokeCondition -Condition ($null -ne $summaryAfter.areaOperationalState) -Message "Area operational state is missing from runtime summary."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($summaryAfter.areaOperationalState.coverageStatus)) -Message "Coverage status is missing from runtime summary."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($summaryAfter.areaOperationalState.freshnessStatus)) -Message "Freshness status is missing from runtime summary."
    Assert-SmokeCondition -Condition (-not [string]::IsNullOrWhiteSpace($summaryAfter.areaOperationalState.carryForwardStatus)) -Message "Carry-forward status is missing from runtime summary."
    Assert-SmokeCondition -Condition ($null -ne $diagnosticResults["latest-run-np-vs-fwi-kbdi"]) -Message "latest-run-np-vs-fwi-kbdi diagnostic was not exported."
    Assert-SmokeCondition -Condition ($null -ne $diagnosticResults["latest-run-portuguese-context-proxy"]) -Message "latest-run-portuguese-context-proxy diagnostic was not exported."
    Assert-SmokeCondition -Condition ($null -ne $diagnosticResults["latest-run-kbdi-series-context"]) -Message "latest-run-kbdi-series-context diagnostic was not exported."
    Assert-SmokeCondition -Condition ($null -ne $diagnosticResults["latest-run-cell-context"]) -Message "latest-run-cell-context diagnostic was not exported."
    Assert-SmokeCondition -Condition ($null -ne $diagnosticResults["latest-run-degradation-effects"]) -Message "latest-run-degradation-effects diagnostic was not exported."

    Copy-Item -Path (Join-Path $runDirectory "latest-run-np-vs-fwi-kbdi.json") -Destination (Join-Path $runDirectory "np-vs-fwi-kbdi.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "latest-run-components.json") -Destination (Join-Path $runDirectory "components.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "latest-run-cell-context.json") -Destination (Join-Path $runDirectory "daily-cell-state.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "latest-run-portuguese-context-proxy.json") -Destination (Join-Path $runDirectory "portuguese-context-proxy.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "latest-run-kbdi-series-context.json") -Destination (Join-Path $runDirectory "kbdi-series-context.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "latest-run-degradation-effects.json") -Destination (Join-Path $runDirectory "degradation-effects.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "compare-latest-b-vs-c.json") -Destination (Join-Path $runDirectory "b-vs-c.json") -Force
    Copy-Item -Path (Join-Path $runDirectory "compare-latest-b-vs-c.json") -Destination (Join-Path $runDirectory "compare-b-vs-c.json") -Force

    @"
# V1 B/C smoke summary

- GeneratedAtUtc: $((Get-Date).ToUniversalTime().ToString("o"))
- Scenario B run: $($runB.run.id)
- Scenario C run: $($runC.run.id)
- Scenario B status: $($runB.run.status)
- Scenario C status: $($runC.run.status)
- Scenario B accepted/risk/missing/rejected/quarantined: $($auditB.acceptedReadings)/$($auditB.riskAssessments)/$($auditB.missingEvents)/$($auditB.rejected)/$($auditB.quarantined)
- Scenario C accepted/risk/missing/rejected/quarantined: $($auditC.acceptedReadings)/$($auditC.riskAssessments)/$($auditC.missingEvents)/$($auditC.rejected)/$($auditC.quarantined)
- NP vs FWI/KBDI diagnostic: np-vs-fwi-kbdi.json
- Portuguese context proxy diagnostic: portuguese-context-proxy.json
- KBDI series context diagnostic: kbdi-series-context.json
- Components diagnostic: components.json
- Daily cell state diagnostic: daily-cell-state.json
- Degradation effects diagnostic: degradation-effects.json
- Compare B vs C diagnostic: compare-b-vs-c.json
- Coverage/freshness diagnostic: latest-run-coverage-freshness.json

This smoke reads persisted API/runtime data and does not recalculate risk in the script.
"@ | Set-Content -Path (Join-Path $runDirectory "summary.md") -Encoding UTF8

    "" | Set-Content -Path (Join-Path $runDirectory "limitations.md") -Encoding UTF8
    Write-Host "Smoke complete. Evidence directory: $runDirectory"
}
catch {
    $message = $_.Exception.Message
    @"
# V1 B/C smoke limitations

Smoke failed before producing complete runtime evidence.

- Error: $message
- ApiBaseUrl: $ApiBaseUrl
- OutputDirectory: $runDirectory

Check that Backoffice.Api is running in Development and that PostgreSQL/RabbitMQ/Prevention/Simulator services are available.
"@ | Set-Content -Path (Join-Path $runDirectory "limitations.md") -Encoding UTF8

    Write-Host "Smoke failed. See limitations.md in $runDirectory"
    exit 1
}
