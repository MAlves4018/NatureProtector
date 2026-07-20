[CmdletBinding()]
param(
    [string]$SpecPath = "scripts/scenarios/examples/scenario-b-default.json",
    [string]$PostgresContainer = "np-postgres",
    [string]$PostgresUser = "np",
    [string]$Database = "natureprotector",
    [int]$PollIntervalSeconds = 3,
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Resolve-RepositoryRoot {
    param(
        [string[]]$StartPaths
    )

    foreach ($startPath in $StartPaths) {
        if ([string]::IsNullOrWhiteSpace($startPath)) {
            continue
        }

        $resolvedStart = $null
        try {
            $resolvedStart = Resolve-Path -LiteralPath $startPath -ErrorAction Stop
        }
        catch {
            continue
        }

        $current = Get-Item -LiteralPath $resolvedStart.Path
        if (-not $current.PSIsContainer) {
            $current = $current.Directory
        }

        while ($null -ne $current) {
            $solutionPath = Join-Path $current.FullName "NatureProtector.sln"
            if (Test-Path -LiteralPath $solutionPath) {
                return $current.FullName
            }

            $current = $current.Parent
        }
    }

    throw "Could not resolve repository root (NatureProtector.sln)."
}

function Escape-SqlLiteral {
    param([string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return $Value.Replace("'", "''")
}

function Invoke-PsqlQuery {
    param([string]$Sql)

    $result = $Sql | docker exec -i $PostgresContainer psql `
        -U $PostgresUser `
        -d $Database `
        -X `
        -v ON_ERROR_STOP=1 `
        -P pager=off `
        -t `
        -A 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL query failed: $($result | Out-String)"
    }

    return ($result | Out-String).Trim()
}

function Invoke-PsqlScalarInt {
    param([string]$Sql)

    $value = Invoke-PsqlQuery -Sql $Sql
    if ([string]::IsNullOrWhiteSpace($value)) {
        return 0
    }

    return [int]$value
}

function Convert-RunStatusValueToName {
    param([object]$StatusValue)

    if ($null -eq $StatusValue) {
        return $null
    }

    if ($StatusValue -is [string]) {
        $raw = $StatusValue.Trim()
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return $null
        }

        switch ($raw.ToLowerInvariant()) {
            "defined" { return "Defined" }
            "ready" { return "Ready" }
            "running" { return "Running" }
            "completed" { return "Completed" }
            "failed" { return "Failed" }
            "cancelled" { return "Cancelled" }
            "0" { return "Defined" }
            "1" { return "Ready" }
            "2" { return "Running" }
            "3" { return "Completed" }
            "4" { return "Failed" }
            "5" { return "Cancelled" }
            default { return "Unknown" }
        }
    }

    if ($StatusValue -is [int] -or $StatusValue -is [long] -or $StatusValue -is [short]) {
        $statusInt = [int]$StatusValue
        switch ($statusInt) {
            0 { return "Defined" }
            1 { return "Ready" }
            2 { return "Running" }
            3 { return "Completed" }
            4 { return "Failed" }
            5 { return "Cancelled" }
            default { return "Unknown" }
        }
    }

    return "Unknown"
}

function Is-TerminalRunStatus {
    param([string]$StatusName)

    return $StatusName -in @("Completed", "Failed", "Cancelled")
}

function Get-RunRecord {
    param(
        [string]$AreaCode,
        [string]$ScenarioCode,
        [datetime]$CreatedAfterUtc,
        [string]$OrchestratorCorrelationId
    )

    $areaCodeLiteral = Escape-SqlLiteral -Value $AreaCode
    $scenarioCodeLiteral = Escape-SqlLiteral -Value $ScenarioCode
    $createdAfterLiteral = $CreatedAfterUtc.ToString("o")

    $correlationPredicate = ""
    if (-not [string]::IsNullOrWhiteSpace($OrchestratorCorrelationId)) {
        $correlationLiteral = Escape-SqlLiteral -Value $OrchestratorCorrelationId
        $correlationPredicate = " and coalesce(r.""MetadataJson"", '') like '%$correlationLiteral%'"
    }

    $sql = @"
with candidate as (
    select
        r."Id",
        r."Status" as "StatusValue",
        r."CreatedAt",
        r."StartedAt",
        r."EndedAt",
        r."ScenarioCode",
        r."ScenarioName",
        r."NumberOfCycles",
        r."IntervalSeconds",
        r."ExecutionSeed",
        r."MetadataJson",
        a."Code" as "AreaCode"
    from control.simulation_runs r
    inner join control.areas a on a."Id" = r."AreaId"
    where a."Code" = '$areaCodeLiteral'
      and r."ScenarioCode" = '$scenarioCodeLiteral'
      and r."CreatedAt" >= '$createdAfterLiteral'::timestamptz
      $correlationPredicate
    order by r."CreatedAt" desc
    limit 1
)
select row_to_json(candidate)
from candidate;
"@

    $json = Invoke-PsqlQuery -Sql $sql
    if ([string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    $record = $json | ConvertFrom-Json
    $statusName = Convert-RunStatusValueToName -StatusValue $record.StatusValue
    if ($null -ne $statusName) {
        Add-Member -InputObject $record -MemberType NoteProperty -Name StatusName -Value $statusName -Force
    }

    return $record
}

function Get-BestRunRecord {
    param(
        [string]$AreaCode,
        [string]$ScenarioCode,
        [datetime]$CreatedAfterUtc,
        [string]$OrchestratorCorrelationId
    )

    $recordByCorrelation = Get-RunRecord `
        -AreaCode $AreaCode `
        -ScenarioCode $ScenarioCode `
        -CreatedAfterUtc $CreatedAfterUtc `
        -OrchestratorCorrelationId $OrchestratorCorrelationId

    if ($null -ne $recordByCorrelation) {
        return $recordByCorrelation
    }

    return Get-RunRecord `
        -AreaCode $AreaCode `
        -ScenarioCode $ScenarioCode `
        -CreatedAfterUtc $CreatedAfterUtc `
        -OrchestratorCorrelationId ""
}

function Get-RequiredValue {
    param(
        [object]$Object,
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "Missing required property '$Name' in run spec."
    }

    return $property.Value
}

function Get-OptionalValue {
    param(
        [object]$Object,
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Assert-NotBlank {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "'$Name' must be a non-empty string."
    }
}

function Assert-PositiveIfDefined {
    param(
        [string]$Name,
        [object]$Value
    )

    if ($null -eq $Value) {
        return
    }

    if (-not ($Value -is [int] -or $Value -is [long])) {
        throw "'$Name' must be an integer when defined."
    }

    if ([int64]$Value -le 0) {
        throw "'$Name' must be greater than zero when defined."
    }
}

function New-SafeLabel {
    param([string]$RawLabel)

    $safe = $RawLabel -replace '[^A-Za-z0-9._-]', '-'
    if ([string]::IsNullOrWhiteSpace($safe)) {
        return "run"
    }

    return $safe
}

$repoRoot = Resolve-RepositoryRoot -StartPaths @(
    (Get-Location).Path,
    $PSScriptRoot
)

$resolvedSpecPath = $SpecPath
if (-not [System.IO.Path]::IsPathRooted($resolvedSpecPath)) {
    $resolvedSpecPath = Join-Path $repoRoot $resolvedSpecPath
}

$resolvedSpecPath = (Resolve-Path -LiteralPath $resolvedSpecPath -ErrorAction Stop).Path

$rawSpec = Get-Content -LiteralPath $resolvedSpecPath -Raw -Encoding UTF8
$spec = $rawSpec | ConvertFrom-Json

$version = [string](Get-RequiredValue -Object $spec -Name "version")
$areaCode = [string](Get-RequiredValue -Object $spec -Name "areaCode")
$scenarioCode = [string](Get-RequiredValue -Object $spec -Name "scenarioCode")

$sensorCount = Get-OptionalValue -Object $spec -Name "sensorCount"
$numberOfCycles = Get-OptionalValue -Object $spec -Name "numberOfCycles"
$intervalSeconds = Get-OptionalValue -Object $spec -Name "intervalSeconds"
$seed = Get-OptionalValue -Object $spec -Name "seed"
$startTimestamp = Get-OptionalValue -Object $spec -Name "startTimestamp"
$degradationProfile = Get-OptionalValue -Object $spec -Name "degradationProfile"
$collectEvidence = Get-OptionalValue -Object $spec -Name "collectEvidence"
$waitForCompletion = Get-OptionalValue -Object $spec -Name "waitForCompletion"
$timeoutSeconds = Get-OptionalValue -Object $spec -Name "timeoutSeconds"
$allowParallelRun = Get-OptionalValue -Object $spec -Name "allowParallelRun"
$runLabel = Get-OptionalValue -Object $spec -Name "runLabel"

if ($version -ne "1.0") {
    throw "Unsupported run spec version '$version'. Expected '1.0'."
}

Assert-NotBlank -Name "areaCode" -Value $areaCode
Assert-NotBlank -Name "scenarioCode" -Value $scenarioCode

Assert-PositiveIfDefined -Name "sensorCount" -Value $sensorCount
Assert-PositiveIfDefined -Name "numberOfCycles" -Value $numberOfCycles
Assert-PositiveIfDefined -Name "intervalSeconds" -Value $intervalSeconds
Assert-PositiveIfDefined -Name "timeoutSeconds" -Value $timeoutSeconds

if ($null -ne $seed -and -not ($seed -is [int] -or $seed -is [long])) {
    throw "'seed' must be an integer when defined."
}

if ($null -ne $startTimestamp -and $startTimestamp -is [datetimeoffset]) {
    $startTimestamp = $startTimestamp.ToUniversalTime().ToString("o")
}
elseif ($null -ne $startTimestamp -and $startTimestamp -is [datetime]) {
    $startTimestamp = ([datetime]$startTimestamp).ToUniversalTime().ToString("o")
}
elseif ($null -ne $startTimestamp) {
    $parsedStartTimestamp = [datetimeoffset]::MinValue
    if (-not [datetimeoffset]::TryParse([string]$startTimestamp, [ref]$parsedStartTimestamp)) {
        throw "'startTimestamp' must be a valid ISO-8601 timestamp when defined."
    }

    $startTimestamp = $parsedStartTimestamp.ToUniversalTime().ToString("o")
}

if ($null -eq $collectEvidence) { $collectEvidence = $true }
if ($null -eq $waitForCompletion) { $waitForCompletion = $true }
if ($null -eq $timeoutSeconds) { $timeoutSeconds = 900 }
if ($null -eq $allowParallelRun) { $allowParallelRun = $false }
if ($null -eq $degradationProfile) { $degradationProfile = "none" }
if ([string]::IsNullOrWhiteSpace($runLabel)) { $runLabel = $scenarioCode }

$runLabel = New-SafeLabel -RawLabel $runLabel

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot "docs/evidence/runs"
}
else {
    if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path $repoRoot $OutputRoot }
}
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
$runOutputDir = Join-Path $resolvedOutputRoot "$timestamp-$scenarioCode-$runLabel"
New-Item -ItemType Directory -Path $runOutputDir -Force | Out-Null

$stdoutPath = Join-Path $runOutputDir "simulator-host.stdout.log"
$stderrPath = Join-Path $runOutputDir "simulator-host.stderr.log"
$combinedLogPath = Join-Path $runOutputDir "simulator-host.log"
$resolvedSpecOutputPath = Join-Path $runOutputDir "run-spec.resolved.json"
$summaryPath = Join-Path $runOutputDir "summary.md"

$orchestratorCorrelationId = [Guid]::NewGuid().ToString("D")
$startedAtUtc = [datetime]::UtcNow.AddSeconds(-5)

Write-Host "Repository root: $repoRoot"
Write-Host "Run output dir: $runOutputDir"
Write-Host "OrchestratorCorrelationId: $orchestratorCorrelationId"

$dockerCheck = & docker ps --format "{{.Names}}" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Docker is not accessible: $($dockerCheck | Out-String)"
}

$pgProbe = Invoke-PsqlQuery -Sql "select 1;"
if (-not ($pgProbe -match "1")) {
    throw "PostgreSQL probe did not return the expected result."
}

$areaCodeLiteral = Escape-SqlLiteral -Value $areaCode
$scenarioCodeLiteral = Escape-SqlLiteral -Value $scenarioCode

$areaCount = Invoke-PsqlScalarInt -Sql @"
select count(*)
from control.areas
where "Code" = '$areaCodeLiteral';
"@

if ($areaCount -le 0) {
    throw "Area '$areaCode' was not found in control.areas."
}

$scenarioCount = Invoke-PsqlScalarInt -Sql @"
select count(*)
from control.scenario_definitions s
inner join control.areas a on a."Id" = s."AreaId"
where a."Code" = '$areaCodeLiteral'
  and s."Code" = '$scenarioCodeLiteral';
"@

if ($scenarioCount -le 0) {
    throw "Scenario '$scenarioCode' for area '$areaCode' was not found in control.scenario_definitions."
}

if (-not [bool]$allowParallelRun) {
    $activeRunsCount = Invoke-PsqlScalarInt -Sql @"
select count(*)
from control.simulation_runs
where "EndedAt" is null;
"@

    if ($activeRunsCount -gt 0) {
        throw "Parallel runs are blocked by default. Found $activeRunsCount active run(s) without EndedAt."
    }
}

$resolvedSpec = [ordered]@{
    version = $version
    areaCode = $areaCode
    scenarioCode = $scenarioCode
    runLabel = $runLabel
    collectEvidence = [bool]$collectEvidence
    waitForCompletion = [bool]$waitForCompletion
    timeoutSeconds = [int]$timeoutSeconds
    allowParallelRun = [bool]$allowParallelRun
    requested = [ordered]@{
        sensorCount = $sensorCount
        numberOfCycles = $numberOfCycles
        intervalSeconds = $intervalSeconds
        seed = $seed
        startTimestamp = $startTimestamp
        degradationProfile = $degradationProfile
    }
    orchestrator = [ordered]@{
        correlationId = $orchestratorCorrelationId
        startedAtUtc = [datetime]::UtcNow.ToString("o")
        repoRoot = $repoRoot
        specPath = $resolvedSpecPath
    }
    confirmation = [ordered]@{
        sensorCount = [ordered]@{
            status = "requested_not_confirmed_pending_host_support"
            observedValue = $null
        }
        numberOfCycles = [ordered]@{
            status = "requested_not_confirmed"
            observedValue = $null
        }
        intervalSeconds = [ordered]@{
            status = "requested_not_confirmed"
            observedValue = $null
        }
        seed = [ordered]@{
            status = "requested_not_confirmed"
            observedValue = $null
        }
        degradationProfile = [ordered]@{
            status = "requested_not_confirmed_pending_host_support"
            observedValue = $null
        }
    }
}

$resolvedSpec | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedSpecOutputPath -Encoding UTF8

$envBackup = @{}
function Set-TemporaryEnvVar {
    param(
        [string]$Name,
        [string]$Value
    )

    if (-not $envBackup.ContainsKey($Name)) {
        if (Test-Path "Env:$Name") {
            $envBackup[$Name] = (Get-Item "Env:$Name").Value
        }
        else {
            $envBackup[$Name] = $null
        }
    }

    Set-Item -Path "Env:$Name" -Value $Value
}

function Set-TemporaryEnvVarIfDefined {
    param(
        [string]$Name,
        [object]$Value
    )

    if ($null -eq $Value) {
        return
    }

    Set-TemporaryEnvVar -Name $Name -Value ([string]$Value)
}

function Restore-TemporaryEnvVars {
    foreach ($entry in $envBackup.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path "Env:$($entry.Key)" -Value $entry.Value
        }
    }
}

$runRecord = $null
$finalStatus = "NotStarted"
$runId = $null
$hostExitCode = $null
$limitations = @(
    "Run status uses PostgreSQL (control.simulation_runs) as source of truth.",
    "Overrides are considered confirmed when observed in SimulationRun fields and/or SimulationRun.MetadataJson."
)

try {
    Set-TemporaryEnvVar -Name "Simulator__ControlPlaneEnabled" -Value "true"
    Set-TemporaryEnvVar -Name "Simulator__ControlPlaneAreaCode" -Value $areaCode
    Set-TemporaryEnvVar -Name "Simulator__ControlPlaneScenarioCode" -Value $scenarioCode
    Set-TemporaryEnvVarIfDefined -Name "Simulator__RunOverrides__SensorCount" -Value $sensorCount
    Set-TemporaryEnvVarIfDefined -Name "Simulator__RunOverrides__NumberOfCycles" -Value $numberOfCycles
    Set-TemporaryEnvVarIfDefined -Name "Simulator__RunOverrides__IntervalSeconds" -Value $intervalSeconds
    Set-TemporaryEnvVarIfDefined -Name "Simulator__RunOverrides__Seed" -Value $seed
    Set-TemporaryEnvVarIfDefined -Name "Simulator__StartTimestamp" -Value $startTimestamp
    Set-TemporaryEnvVarIfDefined -Name "Simulator__RunOverrides__DegradationProfile" -Value $degradationProfile
    Set-TemporaryEnvVar -Name "Simulator__RunOverrides__OrchestratorCorrelationId" -Value "$orchestratorCorrelationId"

    Write-Host "Starting Simulator.Host..."
    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--project", "src/NatureProtector.Simulator.Host") `
        -WorkingDirectory $repoRoot `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $deadlineUtc = [datetime]::UtcNow.AddSeconds([int]$timeoutSeconds)
    $terminalStatuses = @("Completed", "Failed", "Cancelled")

    while ($true) {
        $latestRunRecord = Get-BestRunRecord `
            -AreaCode $areaCode `
            -ScenarioCode $scenarioCode `
            -CreatedAfterUtc $startedAtUtc `
            -OrchestratorCorrelationId $orchestratorCorrelationId
        if ($null -ne $latestRunRecord) {
            $runRecord = $latestRunRecord
        }

        if ($null -ne $runRecord) {
            $runId = $runRecord.Id
            if (Is-TerminalRunStatus -StatusName $runRecord.StatusName) {
                $finalStatus = $runRecord.StatusName
                break
            }
        }

        if (-not [bool]$waitForCompletion) {
            if ($process.HasExited) {
                $hostExitCode = $process.ExitCode
                if ($finalStatus -eq "NotStarted") {
                    $finalStatus = "StillRunning"
                }
            }
            else {
                $finalStatus = "StillRunning"
            }

            break
        }

        if ([datetime]::UtcNow -gt $deadlineUtc) {
            # DB is the source of truth: before timing out, perform a final DB read.
            $finalRecordByCorrelation = Get-RunRecord `
                -AreaCode $areaCode `
                -ScenarioCode $scenarioCode `
                -CreatedAfterUtc $startedAtUtc `
                -OrchestratorCorrelationId $orchestratorCorrelationId

            if ($null -ne $finalRecordByCorrelation) {
                $runRecord = $finalRecordByCorrelation
                $runId = $runRecord.Id
            }
            else {
                $finalFallbackRecord = Get-RunRecord `
                    -AreaCode $areaCode `
                    -ScenarioCode $scenarioCode `
                    -CreatedAfterUtc $startedAtUtc `
                    -OrchestratorCorrelationId ""

                if ($null -ne $finalFallbackRecord) {
                    $runRecord = $finalFallbackRecord
                    $runId = $runRecord.Id
                }
            }

            if ($null -ne $runRecord -and (Is-TerminalRunStatus -StatusName $runRecord.StatusName)) {
                $finalStatus = $runRecord.StatusName
            }
            else {
                $finalStatus = "TimedOut"
            }

            break
        }

        if ($process.HasExited) {
            $hostExitCode = $process.ExitCode
            $finalRunRecord = Get-BestRunRecord `
                -AreaCode $areaCode `
                -ScenarioCode $scenarioCode `
                -CreatedAfterUtc $startedAtUtc `
                -OrchestratorCorrelationId $orchestratorCorrelationId
            if ($null -ne $finalRunRecord) {
                $runRecord = $finalRunRecord
                $runId = $runRecord.Id
            }

            if ($null -ne $runRecord -and (Is-TerminalRunStatus -StatusName $runRecord.StatusName)) {
                $finalStatus = $runRecord.StatusName
            }
            else {
                $finalStatus = "HostFailedBeforeRun"
            }
            break
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }

    if (($null -ne $runRecord) -and (Is-TerminalRunStatus -StatusName $runRecord.StatusName) -and -not $process.HasExited) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }
        catch {
            # best effort: DB is still source of truth
        }
    }
    elseif ($finalStatus -eq "TimedOut" -and -not $process.HasExited) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }
        catch {
            # best effort; status remains TimedOut
        }
    }

    if ($null -eq $hostExitCode -and $process.HasExited) {
        $hostExitCode = $process.ExitCode
    }
}
finally {
    Restore-TemporaryEnvVars
}

$stdoutContent = ""
$stderrContent = ""
if (Test-Path -LiteralPath $stdoutPath) {
    $stdoutContent = Get-Content -LiteralPath $stdoutPath -Raw -Encoding UTF8
}
if (Test-Path -LiteralPath $stderrPath) {
    $stderrContent = Get-Content -LiteralPath $stderrPath -Raw -Encoding UTF8
}

@(
    "=== STDOUT ==="
    $stdoutContent
    ""
    "=== STDERR ==="
    $stderrContent
) | Set-Content -LiteralPath $combinedLogPath -Encoding UTF8

if ($null -ne $runRecord) {
    if ($null -ne $numberOfCycles) {
        $resolvedSpec.confirmation.numberOfCycles.observedValue = $runRecord.NumberOfCycles
        $resolvedSpec.confirmation.numberOfCycles.status = if ([int64]$runRecord.NumberOfCycles -eq [int64]$numberOfCycles) { "observed_match" } else { "requested_not_confirmed_or_not_applied" }
    }

    if ($null -ne $intervalSeconds) {
        $resolvedSpec.confirmation.intervalSeconds.observedValue = $runRecord.IntervalSeconds
        $resolvedSpec.confirmation.intervalSeconds.status = if ([int64]$runRecord.IntervalSeconds -eq [int64]$intervalSeconds) { "observed_match" } else { "requested_not_confirmed_or_not_applied" }
    }

    if ($null -ne $seed) {
        $resolvedSpec.confirmation.seed.observedValue = $runRecord.ExecutionSeed
        $resolvedSpec.confirmation.seed.status = if ($null -ne $runRecord.ExecutionSeed -and [int64]$runRecord.ExecutionSeed -eq [int64]$seed) { "observed_match" } else { "requested_not_confirmed_or_not_applied" }
    }

    if ($null -ne $sensorCount) {
        $metadataSensorCount = $null
        if (-not [string]::IsNullOrWhiteSpace($runRecord.MetadataJson)) {
            try {
                $metadata = $runRecord.MetadataJson | ConvertFrom-Json
                if ($null -ne $metadata.run_overrides -and $null -ne $metadata.run_overrides.resolved -and $null -ne $metadata.run_overrides.resolved.sensor_count) {
                    $metadataSensorCount = $metadata.run_overrides.resolved.sensor_count
                }
                elseif ($null -ne $metadata.sensor_count) {
                    $metadataSensorCount = $metadata.sensor_count
                }
            }
            catch {
                $metadataSensorCount = $null
            }
        }

        $resolvedSpec.confirmation.sensorCount.observedValue = $metadataSensorCount
        if ($null -ne $metadataSensorCount -and [int64]$metadataSensorCount -eq [int64]$sensorCount) {
            $resolvedSpec.confirmation.sensorCount.status = "observed_match"
        }
        elseif ($null -ne $metadataSensorCount) {
            $resolvedSpec.confirmation.sensorCount.status = "requested_not_confirmed_or_not_applied"
        }
    }
}

if ($finalStatus -eq "HostFailedBeforeRun") {
    $failureReason = $null
    foreach ($line in (($stderrContent + "`n" + $stdoutContent) -split "`r?`n")) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed -match "(?i)(exception|error|fail(ed|ure))") {
            $failureReason = $trimmed
            break
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($failureReason)) {
        $limitations += "HostFailedBeforeRun reason: $failureReason"
    }
}

$resolvedSpec.orchestrator.completedAtUtc = [datetime]::UtcNow.ToString("o")
$resolvedSpec.orchestrator.outputDir = $runOutputDir
$resolvedSpec.orchestrator.runId = $runId
$resolvedSpec.orchestrator.finalStatus = $finalStatus
$resolvedSpec.orchestrator.hostExitCode = $hostExitCode

$evidenceResult = "not_requested"
if ([bool]$collectEvidence) {
    $evidenceScriptPath = Join-Path $repoRoot "scripts/evidence/collect-v1-runtime-evidence.ps1"
    if (Test-Path -LiteralPath $evidenceScriptPath) {
        $evidenceLog = Join-Path $runOutputDir "evidence-collector.log"
        try {
            & $evidenceScriptPath -OutputDir $runOutputDir *>&1 | Tee-Object -FilePath $evidenceLog | Out-Null
            $evidenceResult = "requested_completed"
        }
        catch {
            $evidenceResult = "requested_failed"
            $limitations += "Evidence collection failed: $($_.Exception.Message)"
        }
    }
    else {
        $evidenceResult = "requested_script_not_found"
        $limitations += "Evidence script not found at scripts/evidence/collect-v1-runtime-evidence.ps1."
    }
}

$resolvedSpec.orchestrator.evidence = $evidenceResult
$resolvedSpec | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedSpecOutputPath -Encoding UTF8

$branch = ""
$commit = ""
try { $branch = (& git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim() } catch { $branch = "unknown" }
try { $commit = (& git -C $repoRoot rev-parse HEAD).Trim() } catch { $commit = "unknown" }

$summaryLines = @(
    "# Scenario Run Summary",
    "",
    "- branch: $branch",
    "- commit: $commit",
    "- runLabel: $runLabel",
    "- areaCode: $areaCode",
    "- scenarioCode: $scenarioCode",
    "- simulationRunId: $runId",
    "- finalStatus: $finalStatus",
    "- hostExitCode: $hostExitCode",
    "- outputDir: $runOutputDir",
    "- collectEvidence: $collectEvidence",
    "- evidenceResult: $evidenceResult",
    "",
    "## Requested Parameters",
    "",
    "- sensorCount: $sensorCount",
    "- numberOfCycles: $numberOfCycles",
    "- intervalSeconds: $intervalSeconds",
    "- seed: $seed",
    "- startTimestamp: $startTimestamp",
    "- degradationProfile: $degradationProfile",
    "- waitForCompletion: $waitForCompletion",
    "- timeoutSeconds: $timeoutSeconds",
    "- allowParallelRun: $allowParallelRun",
    "",
    "## Resolved/Observed",
    "",
    "- numberOfCycles: $($resolvedSpec.confirmation.numberOfCycles.observedValue) [$($resolvedSpec.confirmation.numberOfCycles.status)]",
    "- intervalSeconds: $($resolvedSpec.confirmation.intervalSeconds.observedValue) [$($resolvedSpec.confirmation.intervalSeconds.status)]",
    "- seed: $($resolvedSpec.confirmation.seed.observedValue) [$($resolvedSpec.confirmation.seed.status)]",
    "- sensorCount: $($resolvedSpec.confirmation.sensorCount.observedValue) [$($resolvedSpec.confirmation.sensorCount.status)]",
    "",
    "## Limitations",
    ""
)

foreach ($limitation in $limitations) {
    $summaryLines += "- $limitation"
}

$summaryLines += @(
    "",
    "## Next Steps",
    "",
    "- If status and metadata are correct, proceed with operational hardening (timeouts/retries/reporting).",
    "- Keep run-spec and metadata fields stable for future Backoffice/API orchestration reuse."
)

$summaryLines | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Run orchestration output written to:"
Write-Host "  $runOutputDir"
Write-Host "Summary:"
Write-Host "  $summaryPath"
Write-Host "Resolved spec:"
Write-Host "  $resolvedSpecOutputPath"
