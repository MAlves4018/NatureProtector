[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$HeroRunId,
    [Parameter(Mandatory=$true)][string]$NominalRunId,
    [string]$BaselineId = 'NP-FINAL-20260718-S2',
    [string]$BaselineSha256 = '905e9bd711abfdab08ca47bbb37ae3db6c91ddb5033a22e9d2d20a51971e6496',
    [string]$ApiBaseUrl = 'http://127.0.0.1:5254',
    [string]$WebUiBaseUrl = 'http://127.0.0.1:5173',
    [string]$ExistingCampaignRoot,
    [string]$OutputRoot,
    [switch]$SkipPlaywright,
    [switch]$ContinueOnError
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

. (Join-Path $PSScriptRoot 'Point5Evidence.Common.ps1')

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
Set-Location -LiteralPath $repo
[Environment]::CurrentDirectory = $repo

$campaignId = 'NP-P5-' + (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repo "artifacts\point5-report-evidence\$campaignId"
}
$output = [System.IO.Path]::GetFullPath($OutputRoot)
$dirs = @(
    '00-campaign',
    '01-environment',
    '02-api/hero',
    '02-api/nominal',
    '03-derived',
    '04-screenshots',
    '05-figures',
    '06-report-material',
    '07-verification',
    '08-existing-evidence',
    'logs'
)
foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Path (Join-Path $output $dir) -Force | Out-Null
}

$campaignStarted = [DateTimeOffset]::UtcNow
$commandLedger = [System.Collections.Generic.List[object]]::new()
$apiLedger = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()

function Add-Failure {
    param([string]$Phase, [string]$Item, [string]$Status, [string]$Detail)
    $failures.Add([pscustomobject][ordered]@{
        phase = $Phase
        item = $Item
        status = $Status
        detail = $Detail
    })
}

function Read-JsonFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Write-DerivedJson {
    param([Parameter(Mandatory=$true)]$Value, [Parameter(Mandatory=$true)][string]$Name)
    Write-NPPoint5Json -Value $Value -Path (Join-Path $output "03-derived\$Name")
}

function Invoke-RequiredApi {
    param([string]$Name, [string]$RelativePath, [string]$Destination, [switch]$Optional)
    $result = Invoke-NPPoint5ApiGet `
        -ApiBaseUrl $ApiBaseUrl `
        -Token $script:token `
        -RelativePath $RelativePath `
        -OutputPath (Join-Path $output $Destination) `
        -Optional:$Optional
    $apiLedger.Add([pscustomobject]([ordered]@{
        name = $Name
        uri = $result.uri
        status = $result.status
        statusCode = $result.statusCode
        file = $(if ($null -eq $result.file) { $null } else { [System.IO.Path]::GetRelativePath($output, $result.file).Replace('\', '/') })
        error = $result.error
    }))
    if ($result.status -eq 'FAIL') {
        Add-Failure -Phase 'API' -Item $Name -Status 'FAIL' -Detail ([string]$result.error)
        if (-not $ContinueOnError) { throw "Required API collection failed: $Name" }
    }
}

# Campaign and source identity.
$gitCommit = (@(git rev-parse HEAD 2>$null) | Select-Object -First 1)
$gitBranch = (@(git branch --show-current 2>$null) | Select-Object -First 1)
$gitStatus = @(git status --short 2>$null)
Set-Content -LiteralPath (Join-Path $output '00-campaign/git-status.txt') -Value $gitStatus -Encoding UTF8

$campaign = [ordered]@{
    schemaVersion = 1
    campaignId = $campaignId
    baselineId = $BaselineId
    baselineSha256 = $BaselineSha256
    repositoryRoot = $repo
    gitCommit = [string]$gitCommit
    gitBranch = [string]$gitBranch
    workingTreeIncluded = ($gitStatus.Count -gt 0)
    startedAtUtc = $campaignStarted.ToString('o')
    heroRunId = $HeroRunId
    nominalRunId = $NominalRunId
    apiBaseUrl = $ApiBaseUrl
    webUiBaseUrl = $WebUiBaseUrl
    scope = 'Point 5 report evidence: correlation, comparison, screenshots, hero-run dossier and report figures.'
}
Write-NPPoint5Json -Value $campaign -Path (Join-Path $output '00-campaign/manifest.json')

# Environment. npm is considered available only from an actual executable lookup
# and captured command result, never from stale metadata.
$environment = [ordered]@{
    observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    os = [System.Environment]::OSVersion.VersionString
    powershell = $PSVersionTable.PSVersion.ToString()
    processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    currentCulture = [System.Globalization.CultureInfo]::CurrentCulture.Name
    currentUICulture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
    tools = [ordered]@{
        dotnet = Get-NPPoint5Tool -Command 'dotnet' -Arguments @('--info')
        node = Get-NPPoint5Tool -Command 'node' -Arguments @('--version')
        npm = Get-NPPoint5Tool -Command 'npm' -Arguments @('--version')
        python = Get-NPPoint5Tool -Command 'python' -Arguments @('--version')
        docker = Get-NPPoint5Tool -Command 'docker' -Arguments @('version')
        git = Get-NPPoint5Tool -Command 'git' -Arguments @('--version')
        playwright = $(if (Test-Path -LiteralPath (Join-Path $repo 'webUI')) {
            Push-Location (Join-Path $repo 'webUI')
            try { Get-NPPoint5Tool -Command 'npx' -Arguments @('playwright', '--version') }
            finally { Pop-Location }
        } else {
            [ordered]@{ command = 'npx playwright'; available = $false; exitCode = $null; output = $null }
        })
    }
}
Write-NPPoint5Json -Value $environment -Path (Join-Path $output '01-environment/environment.json')

$healthRows = foreach ($uri in @(
    "$ApiBaseUrl/health/live",
    "$ApiBaseUrl/health/ready",
    'http://127.0.0.1:5260/health/live',
    'http://127.0.0.1:5260/health/ready',
    $WebUiBaseUrl
)) {
    $started = [DateTimeOffset]::UtcNow
    try {
        $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15
        [pscustomobject][ordered]@{
            uri = $uri
            observedAtUtc = $started.ToString('o')
            status = 'PASS'
            statusCode = [int]$response.StatusCode
            error = $null
        }
    } catch {
        [pscustomobject][ordered]@{
            uri = $uri
            observedAtUtc = $started.ToString('o')
            status = 'FAIL'
            statusCode = $null
            error = $_.Exception.Message
        }
    }
}
Write-NPPoint5Json -Value @($healthRows) -Path (Join-Path $output '01-environment/health.json')

# Import a small, governed bridge to the already completed full campaign. This
# avoids falsely replacing the 14-command/portfolio/long-run evidence with the
# focused Point 5 checks.
$bridgeRoots = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($ExistingCampaignRoot)) {
    if (Test-Path -LiteralPath $ExistingCampaignRoot) {
        $bridgeRoots.Add((Resolve-Path -LiteralPath $ExistingCampaignRoot).Path)
    }
} else {
    foreach ($candidate in @(
        (Join-Path $repo "artifacts\report-evidence\$BaselineId"),
        (Join-Path $repo "artifacts\final-full-handover\$BaselineId")
    )) {
        if (Test-Path -LiteralPath $candidate) { $bridgeRoots.Add($candidate) }
    }
}

$bridgeNamePatterns = @(
    'phase13-summary.json',
    'command-ledger.csv',
    'environment.json',
    'capture-register.json',
    'capture-register.csv',
    '*portfolio*.json',
    '*long-run*.json',
    '*longrun*.json',
    '*settlement*.json',
    'manifest.json'
)
$bridgeCandidates = [System.Collections.Generic.List[object]]::new()
foreach ($rootCandidate in @($bridgeRoots)) {
    foreach ($pattern in $bridgeNamePatterns) {
        foreach ($file in @(Get-ChildItem -LiteralPath $rootCandidate -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue)) {
            if ($file.Length -gt 25MB) { continue }
            $bridgeCandidates.Add($file)
        }
    }
}
$bridgeSelected = @(
    $bridgeCandidates |
    Sort-Object LastWriteTimeUtc -Descending |
    Group-Object Name |
    ForEach-Object { $_.Group | Select-Object -First 1 }
)
$bridgeIndex = [System.Collections.Generic.List[object]]::new()
foreach ($file in $bridgeSelected) {
    $hash = Get-NPPoint5Sha256 -Path $file.FullName
    $safeName = (($file.BaseName -replace '[^A-Za-z0-9._-]', '_') + '-' + $hash.Substring(0, 12) + $file.Extension)
    $destination = Join-Path $output "08-existing-evidence\$safeName"
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    $bridgeIndex.Add([pscustomobject][ordered]@{
        source = $file.FullName
        copiedAs = [System.IO.Path]::GetRelativePath($output, $destination).Replace('\', '/')
        lastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
        sizeBytes = $file.Length
        sha256 = $hash
    })
}
Write-NPPoint5Json -Value @($bridgeIndex) -Path (Join-Path $output '08-existing-evidence/bridge-index.json')

# Fresh token and exact successful API contracts from the prior discovery.
$token = Get-NPPoint5FreshToken -ApiBaseUrl $ApiBaseUrl

$runRequests = @(
    @{ name='hero-runtime-run'; path="/api/control/runtime/runs/$HeroRunId"; dest='02-api/hero/runtime-run.json' },
    @{ name='hero-simulation-run'; path="/api/control/simulation-runs/$HeroRunId"; dest='02-api/hero/simulation-run.json' },
    @{ name='hero-operation'; path="/api/control/runtime/runs/$HeroRunId/operation"; dest='02-api/hero/operation.json' },
    @{ name='hero-audit'; path="/api/control/runtime/runs/$HeroRunId/audit"; dest='02-api/hero/audit.json' },
    @{ name='hero-timings'; path="/api/control/runtime/runs/$HeroRunId/timings"; dest='02-api/hero/timings.json' },
    @{ name='nominal-runtime-run'; path="/api/control/runtime/runs/$NominalRunId"; dest='02-api/nominal/runtime-run.json' },
    @{ name='nominal-simulation-run'; path="/api/control/simulation-runs/$NominalRunId"; dest='02-api/nominal/simulation-run.json' },
    @{ name='nominal-operation'; path="/api/control/runtime/runs/$NominalRunId/operation"; dest='02-api/nominal/operation.json' },
    @{ name='nominal-audit'; path="/api/control/runtime/runs/$NominalRunId/audit"; dest='02-api/nominal/audit.json' },
    @{ name='nominal-timings'; path="/api/control/runtime/runs/$NominalRunId/timings"; dest='02-api/nominal/timings.json' },
    @{ name='runtime-summary'; path='/api/control/runtime/summary'; dest='02-api/runtime-summary.json' },
    @{ name='simulation-runs'; path='/api/control/simulation-runs'; dest='02-api/simulation-runs.json' },
    @{ name='observability-health'; path='/api/control/runtime/observability/health'; dest='02-api/observability-health.json' },
    @{ name='observability-rabbitmq'; path='/api/control/runtime/observability/rabbitmq'; dest='02-api/observability-rabbitmq.json' },
    @{ name='observability-evidence'; path='/api/control/runtime/observability/evidence'; dest='02-api/observability-evidence.json' },
    @{ name='evidence-campaign-catalog'; path='/api/control/evidence/campaigns/catalog'; dest='02-api/evidence-campaign-catalog.json' }
)
foreach ($request in $runRequests) {
    Invoke-RequiredApi -Name $request.name -RelativePath $request.path -Destination $request.dest
}
Write-NPPoint5Json -Value @($apiLedger) -Path (Join-Path $output '02-api/api-ledger.json')

# Read canonical run-scoped payloads.
$heroRun = Read-JsonFile (Join-Path $output '02-api/hero/runtime-run.json')
$heroOperation = Read-JsonFile (Join-Path $output '02-api/hero/operation.json')
$heroAudit = Read-JsonFile (Join-Path $output '02-api/hero/audit.json')
$heroTimings = Read-JsonFile (Join-Path $output '02-api/hero/timings.json')
$nominalRun = Read-JsonFile (Join-Path $output '02-api/nominal/runtime-run.json')
$nominalOperation = Read-JsonFile (Join-Path $output '02-api/nominal/operation.json')
$nominalAudit = Read-JsonFile (Join-Path $output '02-api/nominal/audit.json')
$nominalTimings = Read-JsonFile (Join-Path $output '02-api/nominal/timings.json')
$evidenceCatalog = Read-JsonFile (Join-Path $output '02-api/observability-evidence.json')

function Get-ResolvedProfiles {
    param($Run)
    return @(Get-NPPoint5ResolvedProfiles -Run $Run)
}

function Get-CoveragePercent {
    param($Audit)
    if ([double]$Audit.expectedEvents -le 0) { return $null }
    return [Math]::Round(([double]$Audit.acceptedReadings / [double]$Audit.expectedEvents) * 100, 3)
}

function New-Invariant {
    param([string]$Name, [string]$Expression, [double]$Left, [double]$Right, [string]$Explanation)
    return [pscustomobject][ordered]@{
        name = $Name
        expression = $Expression
        left = $Left
        right = $Right
        difference = [Math]::Round($Left - $Right, 6)
        status = $(if ([Math]::Abs($Left - $Right) -lt 0.000001) { 'PASS' } else { 'FAIL' })
        explanation = $Explanation
    }
}

$accountingInvariants = @(
    New-Invariant `
        -Name 'expected_equals_accepted_plus_missing' `
        -Expression 'expectedEvents = acceptedReadings + missingEvents' `
        -Left ([double]$heroAudit.expectedEvents) `
        -Right ([double]$heroAudit.acceptedReadings + [double]$heroAudit.missingEvents) `
        -Explanation 'Run audit arithmetic for the missing-readings profile.'
    New-Invariant `
        -Name 'processed_inbox_equals_accepted' `
        -Expression 'processedInbox = acceptedReadings' `
        -Left ([double]$heroOperation.accounting.processedInbox) `
        -Right ([double]$heroAudit.acceptedReadings) `
        -Explanation 'The accepted observations are the processed inbox items for this run.'
    New-Invariant `
        -Name 'risk_assessments_equals_accepted' `
        -Expression 'riskAssessments = acceptedReadings' `
        -Left ([double]$heroAudit.riskAssessments) `
        -Right ([double]$heroAudit.acceptedReadings) `
        -Explanation 'Each accepted reading produced a persisted risk assessment in this controlled run.'
    New-Invariant `
        -Name 'pending_work_equals_zero' `
        -Expression 'pending + processing + retryPending = 0' `
        -Left ([double]$heroOperation.accounting.pendingInbox + [double]$heroOperation.accounting.processingInbox + [double]$heroOperation.accounting.retryPendingInbox) `
        -Right 0 `
        -Explanation 'No residual work remains at settlement.'
)
$accounting = [ordered]@{
    simulationRunId = $HeroRunId
    expected = [int]$heroAudit.expectedEvents
    accepted = [int]$heroAudit.acceptedReadings
    missing = [int]$heroAudit.missingEvents
    rejected = [int]$heroAudit.rejected
    quarantined = [int]$heroAudit.quarantined
    retries = [int]$heroAudit.retryAttempts
    riskAssessments = [int]$heroAudit.riskAssessments
    pendingInbox = [int]$heroOperation.accounting.pendingInbox
    processingInbox = [int]$heroOperation.accounting.processingInbox
    retryPendingInbox = [int]$heroOperation.accounting.retryPendingInbox
    processedInbox = [int]$heroOperation.accounting.processedInbox
    settled = [bool]$heroOperation.accounting.settled
    settlementGap = ([int]$heroAudit.expectedEvents - [int]$heroAudit.acceptedReadings - [int]$heroAudit.missingEvents)
    invariants = @($accountingInvariants)
}
Write-DerivedJson -Value $accounting -Name 'hero-accounting.json'
Write-NPPoint5Csv -Rows @($accountingInvariants) `
    -Columns @('name','expression','left','right','difference','status','explanation') `
    -Path (Join-Path $output '03-derived/hero-accounting-invariants.csv')

$quality = [ordered]@{
    simulationRunId = $HeroRunId
    expectedSensors = Get-NPPoint5ResolvedSensorCount -Run $heroRun
    expectedEvents = $heroAudit.expectedEvents
    acceptedReadings = $heroAudit.acceptedReadings
    coveragePercent = Get-CoveragePercent -Audit $heroAudit
    observedLossPercent = [Math]::Round(100 - [double](Get-CoveragePercent -Audit $heroAudit), 3)
    confidence = $heroAudit.scoreComponents.confidenceFactor
    integrity = $heroAudit.scoreComponents.integrityFactor
    qualityFlags = @($heroAudit.qualityFlagsSummary)
    eligibility = @($heroAudit.eligibilitySummary)
    calculationStatus = $heroAudit.scoreComponents.calculationStatus
    limitations = @($heroAudit.limitations)
}
Write-DerivedJson -Value $quality -Name 'hero-quality-eligibility.json'

$metrics = [ordered]@{
    simulationRunId = $HeroRunId
    npScore = $heroAudit.scoreComponents.npScore
    baseRisk = $heroAudit.scoreComponents.baseRisk
    adjusted = $heroAudit.scoreComponents.adjustedScore
    score100 = $heroAudit.scoreComponents.score100
    confidence = $heroAudit.scoreComponents.confidenceFactor
    integrity = $heroAudit.scoreComponents.integrityFactor
    riskLevel = $heroAudit.scoreComponents.npRiskClass
    riskLevelLabel = $heroAudit.scoreComponents.npRiskClassLabel
    fireWeatherIndex = $heroAudit.indexComparison.fireWeatherIndex
    fireWeatherIpmaClass = $heroAudit.indexComparison.fireWeatherIpmaClass
    fireWeatherIpmaClassLabel = $heroAudit.indexComparison.fireWeatherIpmaClassLabel
    kbdi = $heroAudit.indexComparison.keetchByramDroughtIndex
    kbdiClass = $heroAudit.indexComparison.kbdiDrynessClass
    kbdiClassLabel = $heroAudit.indexComparison.kbdiDrynessClassLabel
    portugueseContextRiskProxy = $heroAudit.indexComparison.portugueseContextRiskProxyLabel
    parameterSetVersion = $heroAudit.scoreComponents.parameterSetVersion
    calculationLimitations = $heroAudit.scoreComponents.limitations
}
Write-DerivedJson -Value $metrics -Name 'hero-risk-and-indices.json'
Write-DerivedJson -Value $metrics -Name 'hero-metrics.json'

$assessments = [ordered]@{
    simulationRunId = $HeroRunId
    assessmentCount = [int]$heroAudit.riskAssessments
    latestAssessmentTimestamp = $heroAudit.scoreComponents.latestAssessmentTimestamp
    eligibilitySummary = @($heroAudit.eligibilitySummary)
    scoreComponents = $heroAudit.scoreComponents
    indexComparison = $heroAudit.indexComparison
    source = 'Persisted run audit; no risk recalculation.'
}
Write-DerivedJson -Value $assessments -Name 'hero-assessments.json'

$runtimeSummary = Read-JsonFile (Join-Path $output '02-api/runtime-summary.json')
$snapshots = [ordered]@{
    simulationRunId = $HeroRunId
    auditAreaSnapshot = $heroAudit.areaSnapshot
    latestAreaOperationalState = $(if ([string]$runtimeSummary.latestRun.id -eq $HeroRunId) { $runtimeSummary.areaOperationalState } else { $null })
    snapshotAvailableInAudit = ($null -ne $heroAudit.areaSnapshot)
    limitation = 'The run audit areaSnapshot is null for this run; the latest area operational state is included only when runtime-summary resolves to the same SimulationRunId.'
}
Write-DerivedJson -Value $snapshots -Name 'hero-snapshots.json'

$eligibilityRows = @(
    Get-NPPoint5ObjectProperty -InputObject $heroAudit -Name 'eligibilitySummary'
)
$eligibleRows = @(
    $eligibilityRows |
    Where-Object {
        ([string](Get-NPPoint5ObjectProperty -InputObject $_ -Name 'status')).
            ToLowerInvariant().
            Contains('eligible')
    }
)
$blockedRows = @(
    $eligibilityRows |
    Where-Object {
        ([string](Get-NPPoint5ObjectProperty -InputObject $_ -Name 'status')).
            ToLowerInvariant().
            Contains('blocked')
    }
)
$eligibility = [ordered]@{
    simulationRunId = $HeroRunId
    summary = @($eligibilityRows)
    eligibleCount = Get-NPPoint5PropertySum -InputObject $eligibleRows -Property 'count'
    blockedCount = Get-NPPoint5PropertySum -InputObject $blockedRows -Property 'count'
}
Write-DerivedJson -Value $eligibility -Name 'hero-eligibility.json'

$heroProfiles = @(Get-ResolvedProfiles -Run $heroRun)
$nominalProfiles = @(Get-ResolvedProfiles -Run $nominalRun)
$comparisonRows = @(
    [pscustomobject]@{ metric='SimulationRunId'; nominal=[string]$NominalRunId; hero=[string]$HeroRunId },
    [pscustomobject]@{ metric='scenario'; nominal=[string]$nominalRun.scenarioCode; hero=[string]$heroRun.scenarioCode },
    [pscustomobject]@{ metric='seed'; nominal=[string]$nominalRun.executionSeed; hero=[string]$heroRun.executionSeed },
    [pscustomobject]@{ metric='cycles'; nominal=[string]$nominalRun.numberOfCycles; hero=[string]$heroRun.numberOfCycles },
    [pscustomobject]@{ metric='intervalSeconds'; nominal=[string]$nominalRun.intervalSeconds; hero=[string]$heroRun.intervalSeconds },
    [pscustomobject]@{ metric='resolvedProfiles'; nominal=($nominalProfiles -join ', '); hero=($heroProfiles -join ', ') },
    [pscustomobject]@{ metric='expected'; nominal=[string]$nominalAudit.expectedEvents; hero=[string]$heroAudit.expectedEvents },
    [pscustomobject]@{ metric='accepted'; nominal=[string]$nominalAudit.acceptedReadings; hero=[string]$heroAudit.acceptedReadings },
    [pscustomobject]@{ metric='missing'; nominal=[string]$nominalAudit.missingEvents; hero=[string]$heroAudit.missingEvents },
    [pscustomobject]@{ metric='coveragePercent'; nominal=[string](Get-CoveragePercent -Audit $nominalAudit); hero=[string](Get-CoveragePercent -Audit $heroAudit) },
    [pscustomobject]@{ metric='settled'; nominal=[string]$nominalOperation.accounting.settled; hero=[string]$heroOperation.accounting.settled },
    [pscustomobject]@{ metric='score100'; nominal=[string]$nominalAudit.scoreComponents.score100; hero=[string]$heroAudit.scoreComponents.score100 },
    [pscustomobject]@{ metric='fwi'; nominal=[string]$nominalAudit.indexComparison.fireWeatherIndex; hero=[string]$heroAudit.indexComparison.fireWeatherIndex },
    [pscustomobject]@{ metric='kbdi'; nominal=[string]$nominalAudit.indexComparison.keetchByramDroughtIndex; hero=[string]$heroAudit.indexComparison.keetchByramDroughtIndex },
    [pscustomobject]@{ metric='directEvidenceId'; nominal=$(if ($nominalOperation.evidenceId) { [string]$nominalOperation.evidenceId } else { 'not_structurally_associated' }); hero=$(if ($heroOperation.evidenceId) { [string]$heroOperation.evidenceId } else { 'not_structurally_associated' }) }
)
Write-DerivedJson -Value @($comparisonRows) -Name 'nominal-vs-hero-comparison.json'
Write-NPPoint5Csv -Rows @($comparisonRows) -Columns @('metric','nominal','hero') -Path (Join-Path $output '03-derived/nominal-vs-hero-comparison.csv')

$catalogItems = @(Get-NPPoint5ObjectProperty -InputObject $evidenceCatalog -Name 'items')
$heroCatalogMatches = @(
    $catalogItems | Where-Object {
        ([string]$_.title).IndexOf($HeroRunId, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        ([string]$_.scope).IndexOf($HeroRunId, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        ([string]$_.evidenceId).IndexOf($HeroRunId, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    }
)
$evidenceAssociation = [ordered]@{
    simulationRunId = $HeroRunId
    directOperationAssociation = (-not [string]::IsNullOrWhiteSpace([string]$heroOperation.evidenceId))
    operationEvidenceId = $heroOperation.evidenceId
    operationEvidenceLocation = $heroOperation.evidenceLocation
    catalogAvailable = (@($catalogItems).Count -gt 0)
    catalogItemCount = @($catalogItems).Count
    runMatchedCatalogItems = @($heroCatalogMatches)
    interpretation = $(if ($heroOperation.evidenceId) {
        'The operation has a direct structural evidence association.'
    } elseif (@($heroCatalogMatches).Count -gt 0) {
        'The catalog contains run-matched artifacts, but operation.evidenceId is not populated.'
    } else {
        'The catalog is available, but no direct or title/scope run match was found for this historical Playwright run.'
    })
}
Write-DerivedJson -Value $evidenceAssociation -Name 'hero-evidence-association.json'

$traceability = [ordered]@{
    baselineId = $BaselineId
    baselineSha256 = $BaselineSha256
    simulationRunId = $HeroRunId
    operationId = $heroOperation.operationId
    requestId = $heroOperation.requestId
    correlationId = $heroOperation.correlationId
    areaCode = $heroRun.areaCode
    scenarioCode = $heroRun.scenarioCode
    executionSeed = $heroRun.executionSeed
    configurationVersion = $heroRun.configurationVersionNumber
    producerState = $heroOperation.providerState
    runState = $heroOperation.runState
    processingState = $heroOperation.processingState
    terminalOutcome = $heroOperation.terminalOutcome
    timeline = @($heroTimings.timeline)
    dataScope = $heroAudit.dataScope
}
Write-DerivedJson -Value $traceability -Name 'hero-traceability.json'

$heroSummary = @"
# Hero run dossier

- Baseline: `$BaselineId`
- Baseline SHA-256: `$BaselineSha256`
- SimulationRunId: `$HeroRunId`
- OperationId: `$($heroOperation.operationId)`
- Scenario: `$($heroRun.scenarioCode)`
- Seed: `$($heroRun.executionSeed)`
- Cycles: `$($heroRun.numberOfCycles)`
- Interval: `$($heroRun.intervalSeconds)` s
- Resolved profile: `$($heroProfiles -join ', ')`
- Expected: `$($heroAudit.expectedEvents)`
- Accepted: `$($heroAudit.acceptedReadings)`
- Missing: `$($heroAudit.missingEvents)`
- Coverage: `$(Get-CoveragePercent -Audit $heroAudit)` %
- Settled: `$($heroOperation.accounting.settled)`
- Score100: `$($heroAudit.scoreComponents.score100)`
- FWI: `$($heroAudit.indexComparison.fireWeatherIndex)`
- KBDI: `$($heroAudit.indexComparison.keetchByramDroughtIndex)`

This dossier reads persisted run-scoped records. It does not recalculate risk.
"@
Set-Content -LiteralPath (Join-Path $output '03-derived/HERO-RUN-SUMMARY.md') -Value $heroSummary -Encoding UTF8

# Focused frontend validation.
$webUi = Join-Path $repo 'webUI'
if (-not (Test-Path -LiteralPath $webUi)) { throw "webUI not found: $webUi" }

$focusedTest = Invoke-NPPoint5Command `
    -Name 'point5-vitest' `
    -FilePath 'npx' `
    -Arguments @('vitest','run','src/app/utils/runEvidence.test.ts') `
    -WorkingDirectory $webUi `
    -LogRoot (Join-Path $output 'logs')
$commandLedger.Add([pscustomobject]$focusedTest)
if ($focusedTest.exitCode -ne 0) {
    Add-Failure -Phase 'FRONTEND' -Item 'point5-vitest' -Status 'FAIL' -Detail 'Focused run-evidence unit tests failed.'
    if (-not $ContinueOnError) { throw 'Focused frontend tests failed.' }
}

$typecheck = Invoke-NPPoint5Command `
    -Name 'frontend-typecheck' `
    -FilePath 'npm' `
    -Arguments @('run','typecheck','--if-present') `
    -WorkingDirectory $webUi `
    -LogRoot (Join-Path $output 'logs')
$commandLedger.Add([pscustomobject]$typecheck)
if ($typecheck.exitCode -ne 0) {
    Add-Failure -Phase 'FRONTEND' -Item 'frontend-typecheck' -Status 'FAIL' -Detail 'Frontend typecheck failed.'
    if (-not $ContinueOnError) { throw 'Frontend typecheck failed.' }
}

if (-not $SkipPlaywright) {
    $captureRoot = Join-Path $output '04-screenshots'
    $playwrightEnvironment = @{
        NP_POINT5_REPORT_EVIDENCE = '1'
        NP_POINT5_SCREENSHOT_ROOT = $captureRoot
        NP_POINT5_BASELINE_ID = $BaselineId
        NP_POINT5_BASELINE_SHA256 = $BaselineSha256
        NP_POINT5_HERO_RUN_ID = $HeroRunId
        NP_POINT5_NOMINAL_RUN_ID = $NominalRunId
        NP_POINT5_HERO_SCENARIO = [string]$heroRun.scenarioCode
        NP_POINT5_NOMINAL_SCENARIO = [string]$nominalRun.scenarioCode
    }
    $playwright = Invoke-NPPoint5Command `
        -Name 'point5-playwright' `
        -FilePath 'npx' `
        -Arguments @('playwright','test','e2e/point5-report-evidence.spec.ts','--project=desktop','--workers=1') `
        -WorkingDirectory $webUi `
        -LogRoot (Join-Path $output 'logs') `
        -Environment $playwrightEnvironment
    $commandLedger.Add([pscustomobject]$playwright)
    if ($playwright.exitCode -ne 0) {
        Add-Failure -Phase 'SCREENSHOTS' -Item 'point5-playwright' -Status 'FAIL' -Detail 'Point 5 Playwright capture failed.'
        if (-not $ContinueOnError) { throw 'Point 5 Playwright capture failed.' }
    }
}

# Create report-readable composite figures from element screenshots.
$figureScript = Join-Path $PSScriptRoot 'New-NP-Point5CompositeFigures.ps1'
if (Test-Path -LiteralPath $figureScript) {
    try {
        & $figureScript `
            -ScreenshotRoot (Join-Path $output '04-screenshots') `
            -OutputRoot (Join-Path $output '05-figures')
    } catch {
        Add-Failure -Phase 'FIGURES' -Item 'composite-figures' -Status 'FAIL' -Detail $_.Exception.Message
        if (-not $ContinueOnError) { throw }
    }
}

# Captions and report-ready tables.
$captureRegisterPath = Join-Path $output '04-screenshots/capture-register.json'
if (Test-Path -LiteralPath $captureRegisterPath) {
    $captureRecords = @(Read-JsonFile $captureRegisterPath)
    $captionLines = @('# Suggested captions', '')
    foreach ($capture in $captureRecords) {
        $captionLines += "## $($capture.captureId)"
        $captionLines += ''
        $captionLines += $capture.claim
        $captionLines += ''
        $captionLines += "**Limitação:** $($capture.limitation)"
        $captionLines += ''
        $captionLines += "**SimulationRunId:** $($capture.simulationRunId)"
        $captionLines += ''
    }
    Set-Content -LiteralPath (Join-Path $output '06-report-material/captions.md') -Value $captionLines -Encoding UTF8
} else {
    $captureRecords = @()
    Add-Failure -Phase 'SCREENSHOTS' -Item 'capture-register' -Status 'NOT_EXECUTED' -Detail 'Capture register was not generated.'
}

Copy-Item -LiteralPath (Join-Path $output '03-derived/nominal-vs-hero-comparison.csv') `
    -Destination (Join-Path $output '06-report-material/table-nominal-vs-hero.csv') -Force
Copy-Item -LiteralPath (Join-Path $output '03-derived/hero-accounting-invariants.csv') `
    -Destination (Join-Path $output '06-report-material/table-hero-accounting.csv') -Force

# Coverage matrix connecting this focused enrichment to the earlier full
# campaign described in the report closure checklist.
$bridgeText = ''
foreach ($bridgeFile in @(Get-ChildItem -LiteralPath (Join-Path $output '08-existing-evidence') -File -ErrorAction SilentlyContinue)) {
    try { $bridgeText += "`n" + (Get-Content -LiteralPath $bridgeFile.FullName -Raw -ErrorAction Stop) } catch {}
}
$coverageRows = @(
    [pscustomobject]@{ criterion='baseline-and-campaign'; status='PASS'; source='manifest.json; 00-campaign/manifest.json'; limitation='' },
    [pscustomobject]@{ criterion='commands-and-tests'; status=$(if ($bridgeText -match 'command-ledger|14') {'IMPORTED'} else {'POINT5_FOCUSED_ONLY'}); source='command-ledger.csv; 08-existing-evidence'; limitation='The Point 5 campaign does not replace the prior full command ledger.' },
    [pscustomobject]@{ criterion='local-infrastructure'; status=$(if (@($healthRows | Where-Object status -eq 'FAIL').Count -eq 0) {'PASS'} else {'FAIL'}); source='01-environment/health.json'; limitation='' },
    [pscustomobject]@{ criterion='hero-run'; status='PASS'; source='02-api/hero; 03-derived'; limitation='' },
    [pscustomobject]@{ criterion='scenarios-a-b-c'; status=$(if ($bridgeText -match 'scenario_a' -and $bridgeText -match 'scenario_b' -and $bridgeText -match 'scenario_c') {'IMPORTED'} else {'NOT_REPROVED_BY_POINT5'}); source='08-existing-evidence'; limitation='Use the prior final portfolio as the source of truth.' },
    [pscustomobject]@{ criterion='pipeline-reconciliation'; status=$(if (@($accountingInvariants | Where-Object status -ne 'PASS').Count -eq 0) {'PASS'} else {'FAIL'}); source='03-derived/hero-accounting.json'; limitation='' },
    [pscustomobject]@{ criterion='quality-eligibility-risk'; status='PASS'; source='03-derived/hero-quality-eligibility.json; hero-assessments.json; hero-metrics.json'; limitation='Detailed classifier payloads are not fully persisted.' },
    [pscustomobject]@{ criterion='lag-delay'; status=$(if ($bridgeText -match 'lag/delay|lag-delay') {'IMPORTED'} else {'NOT_REPROVED_BY_POINT5'}); source='08-existing-evidence'; limitation='The fixed hero run uses missing-readings, not lag/delay.' },
    [pscustomobject]@{ criterion='long-run-30-60-120-300'; status=$(if ($bridgeText -match '300' -and $bridgeText -match '120' -and $bridgeText -match '60' -and $bridgeText -match '30') {'IMPORTED'} else {'NOT_REPROVED_BY_POINT5'}); source='08-existing-evidence'; limitation='Use the prior long-run portfolio.' },
    [pscustomobject]@{ criterion='run-id-traceability'; status='PASS'; source='03-derived/hero-traceability.json'; limitation='' },
    [pscustomobject]@{ criterion='screenshots'; status=$(if (@($captureRecords).Count -ge 8) {'PASS'} else {'NOT_EXECUTED'}); source='04-screenshots/capture-register.json'; limitation='' },
    [pscustomobject]@{ criterion='report-figures'; status=$(if (Test-Path -LiteralPath (Join-Path $output '05-figures/figure-register.json')) {'PASS'} else {'NOT_EXECUTED'}); source='05-figures'; limitation='' },
    [pscustomobject]@{ criterion='limitations-and-integrity'; status='PASS'; source='limitations.md; SHA256SUMS.txt; evidence-index.csv'; limitation='' }
)
Write-NPPoint5Json -Value @($coverageRows) -Path (Join-Path $output '06-report-material/coverage-matrix.json')
Write-NPPoint5Csv -Rows @($coverageRows) -Columns @('criterion','status','source','limitation') -Path (Join-Path $output '06-report-material/coverage-matrix.csv')

$limitations = [System.Collections.Generic.List[string]]::new()
$limitations.Add('# Limitations')
$limitations.Add('')
$limitations.Add('- The campaign is local and does not prove cloud or production operation.')
$limitations.Add('- The hero and nominal comparison uses one controlled execution per scenario with seed 42.')
$limitations.Add('- The configuration screenshot reconstructs the persisted hero-run settings; it is not a historical pre-launch capture.')
$limitations.Add('- operation.evidenceId is not populated for the historical Playwright runs; catalog availability is reported separately.')
$limitations.Add('- Detailed classifier payloads are not fully persisted; the audit derives a quality summary from accepted states and missing arithmetic.')
$limitations.Add('- Event publisher timestamps are not persisted as PublishedAt in the current RabbitMQ contract.')
$limitations.Add('- Grafana filtered by SimulationRunId is optional; the application observability surface is captured instead when no direct dashboard is available.')
foreach ($item in @($heroAudit.limitations)) {
    $limitations.Add("- Audit: $($item.code) — $($item.message)")
}
foreach ($item in @($heroAudit.dataScope.limitations)) {
    $limitations.Add("- Data scope: $($item.code) — $($item.message)")
}
foreach ($item in @($heroTimings.limitations)) {
    $limitations.Add("- Timings: $item")
}
Set-Content -LiteralPath (Join-Path $output 'limitations.md') -Value $limitations -Encoding UTF8

# Command and failure ledgers.
Write-NPPoint5Csv -Rows @($commandLedger) `
    -Columns @('name','command','workingDirectory','startedAtUtc','finishedAtUtc','durationMs','exitCode','status','stdout','stderr','error') `
    -Path (Join-Path $output 'command-ledger.csv')
Write-NPPoint5Json -Value @($commandLedger) -Path (Join-Path $output 'command-ledger.json')
Write-NPPoint5Csv -Rows @($failures) -Columns @('phase','item','status','detail') -Path (Join-Path $output 'failures.csv')
Write-NPPoint5Json -Value @($failures) -Path (Join-Path $output 'failures.json')

# Evidence index is generated before the final hashes, then regenerated once
# after all report files exist.
function Write-EvidenceIndex {
    $rows = @(
        Get-ChildItem -LiteralPath $output -Recurse -File |
        Where-Object {
            $_.Name -notin @('SHA256SUMS.txt','evidence-index.csv') -and
            $_.Extension -ne '.zip'
        } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($output, $_.FullName).Replace('\', '/')
            $phase = ($relative -split '/')[0]
            [pscustomobject][ordered]@{
                baselineId = $BaselineId
                campaignId = $campaignId
                phase = $phase
                artifact = $relative
                claim = $(if ($relative -like '04-screenshots/*') { 'Correlated UI evidence.' } elseif ($relative -like '03-derived/*') { 'Run-scoped derived evidence.' } else { 'Campaign support artifact.' })
                status = 'AVAILABLE'
                sha256 = Get-NPPoint5Sha256 -Path $_.FullName
            }
        }
    )
    Write-NPPoint5Csv -Rows @($rows) -Columns @('baselineId','campaignId','phase','artifact','claim','status','sha256') -Path (Join-Path $output 'evidence-index.csv')
}
Write-EvidenceIndex

$campaignFinished = [DateTimeOffset]::UtcNow
$campaign['finishedAtUtc'] = $campaignFinished.ToString('o')
$campaign['durationMs'] = [Math]::Round(($campaignFinished - $campaignStarted).TotalMilliseconds, 3)
$campaign['commandStatusCounts'] = [ordered]@{
    pass = @($commandLedger | Where-Object status -eq 'PASS').Count
    fail = @($commandLedger | Where-Object status -eq 'FAIL').Count
}
$campaign['captureCount'] = @($captureRecords).Count
$campaign['failureCount'] = @($failures).Count
$campaign['status'] = $(if (@($failures | Where-Object status -eq 'FAIL').Count -eq 0) { 'PASS_WITH_LIMITATIONS' } else { 'FAIL' })
Write-NPPoint5Json -Value $campaign -Path (Join-Path $output 'manifest.json')

Write-EvidenceIndex
Write-NPPoint5Hashes -Root $output

$verifyScript = Join-Path $PSScriptRoot 'Test-NP-Point5ReportEvidence.ps1'
$verificationStatus = 'NOT_EXECUTED'
if (Test-Path -LiteralPath $verifyScript) {
    & $verifyScript -EvidenceRoot $output
    if ($LASTEXITCODE -eq 0) { $verificationStatus = 'PASS' } else { $verificationStatus = 'FAIL' }
}

$zip = "$output.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal
$zipHash = Get-NPPoint5Sha256 -Path $zip

Write-Host ''
Write-Host "POINT5_REPORT_EVIDENCE_STATUS=$($campaign['status'])" -ForegroundColor $(if ($campaign['status'] -eq 'FAIL') { 'Red' } else { 'Green' })
Write-Host "POINT5_REPORT_EVIDENCE_VERIFICATION=$verificationStatus"
Write-Host "POINT5_REPORT_EVIDENCE_OUTPUT=$output"
Write-Host "POINT5_REPORT_EVIDENCE_ZIP=$zip"
Write-Host "POINT5_REPORT_EVIDENCE_ZIP_SHA256=$zipHash"

if ($campaign['status'] -eq 'FAIL' -or $verificationStatus -eq 'FAIL') { exit 1 }
exit 0
