param(
    [string]$OutputRoot = "",
    [string]$ApiBaseUrl = "http://127.0.0.1:5254",
    [string]$AreaCode = "proenca-a-nova",
    [string]$ScenarioCode = "scenario_b",
    [int]$SensorCount = 2,
    [int]$NumberOfCycles = 2,
    [int]$IntervalSeconds = 1,
    [int]$TimeoutSeconds = 180,
    [int]$SettlementTimeoutSeconds = 90,
    [switch]$SkipBuild,
    [switch]$PreserveOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\common\NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$RepoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln')
$ArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts'))
$MatrixOutputBase = [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'acceptance\matrices\multi-replica-runtime'))
$FinalAcceptanceBase = [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'final-acceptance'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $runId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $OutputRoot = Join-Path $MatrixOutputBase $runId
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot $OutputRoot
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$matrixPrefix = $MatrixOutputBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$finalAcceptancePrefix = $FinalAcceptanceBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$isStandaloneRun = -not $OutputRoot.Equals($MatrixOutputBase, [StringComparison]::OrdinalIgnoreCase) -and
    ($OutputRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($matrixPrefix, [StringComparison]::OrdinalIgnoreCase)
$isOrchestratedRun = ($OutputRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($finalAcceptancePrefix, [StringComparison]::OrdinalIgnoreCase)
if (-not $isStandaloneRun -and -not $isOrchestratedRun) {
    throw "OutputRoot must be a run-scoped child of $MatrixOutputBase or an orchestrated final-acceptance component."
}
if ((Test-Path -LiteralPath $OutputRoot) -and -not $PreserveOutput) {
    Get-ChildItem -LiteralPath $OutputRoot -Force | Remove-Item -Recurse -Force
}
$LogsRoot = Join-Path $OutputRoot 'logs'
$QueriesRoot = Join-Path $OutputRoot 'queries'
$ResultsRoot = Join-Path $OutputRoot 'results'
New-Item -ItemType Directory -Force -Path $OutputRoot, $LogsRoot, $QueriesRoot, $ResultsRoot | Out-Null

$StartedProcesses = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
$ApiProcess = $null
$ProcessRows = New-Object System.Collections.Generic.List[object]
$MatrixRows = New-Object System.Collections.Generic.List[object]
$InvariantRows = New-Object System.Collections.Generic.List[object]
$Commands = New-Object System.Collections.Generic.List[string]

function Add-CommandLog {
    param([string]$Command)
    $Commands.Add($Command)
}

function Get-DotEnvValue {
    param(
        [hashtable]$Values,
        [string]$Name,
        [string]$Default
    )
    return Get-NpConfigValue -Values $Values -Name $Name -Fallback $Default -EnvironmentFirst
}

function Start-LoggedProcess {
    param(
        [string]$Name,
        [string]$FileName,
        [string[]]$Arguments,
        [hashtable]$Environment,
        [string]$WorkingDirectory,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command $FileName -ErrorAction Stop).Source
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }
    foreach ($entry in $Environment.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = [string]$entry.Value
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Failed to start $Name."
    }
    $StartedProcesses.Add($process)

    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.EnableRaisingEvents = $true
    Register-ObjectEvent -InputObject $process -EventName Exited -Action {
        try {
            $event.MessageData.StdoutTask.GetAwaiter().GetResult() | Set-Content -LiteralPath $event.MessageData.StdoutPath -Encoding UTF8
            $event.MessageData.StderrTask.GetAwaiter().GetResult() | Set-Content -LiteralPath $event.MessageData.StderrPath -Encoding UTF8
        } catch { }
    } -MessageData ([pscustomobject]@{
        StdoutTask = $stdoutTask
        StderrTask = $stderrTask
        StdoutPath = $StdoutPath
        StderrPath = $StderrPath
    }) | Out-Null

    return $process
}

function Stop-StartedProcesses {
    foreach ($process in @($StartedProcesses)) {
        try {
            if (-not $process.HasExited) {
                $process.Kill($true)
                $process.WaitForExit(15000) | Out-Null
            }
        }
        catch { }
    }
}

function Wait-HttpReady {
    param([string]$Url, [int]$TimeoutSeconds = 60)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 -Uri $Url
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        }
        catch { }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Url."
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = ''
    )
    $headers = @{ Accept = 'application/json' }
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }
    $parameters = @{
        Method = $Method
        Uri = "$ApiBaseUrl$Path"
        Headers = $headers
        TimeoutSec = 60
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ($Body | ConvertTo-Json -Depth 20)
    }
    $response = $null
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        $response = Invoke-WebRequest @parameters
        if ($response.StatusCode -ne 429 -or $attempt -eq 8) {
            break
        }

        $retryAfter = 0
        if ($response.Headers.ContainsKey('Retry-After')) {
            [int]::TryParse([string]$response.Headers['Retry-After'], [ref]$retryAfter) | Out-Null
        }

        $delaySeconds = if ($retryAfter -gt 0) {
            [Math]::Min($retryAfter, 300)
        }
        elseif ($Method -eq 'POST' -and $Path -eq '/api/control/runtime/runs') {
            60
        }
        else {
            10
        }

        Add-CommandLog "HTTP 429 $Method $Path; retry $attempt/7 after ${delaySeconds}s"
        Start-Sleep -Seconds $delaySeconds
    }
    if ($response.StatusCode -ge 400) {
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $safePath = ($Path -replace '[^A-Za-z0-9._-]', '_').Trim('_')
        if ([string]::IsNullOrWhiteSpace($safePath)) {
            $safePath = 'root'
        }
        $errorPath = Join-Path $LogsRoot "http-error-$timestamp-$Method-$safePath.json"
        [pscustomobject]@{
            method = $Method
            path = $Path
            statusCode = [int]$response.StatusCode
            statusDescription = $response.StatusDescription
            body = $response.Content
        } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $errorPath -Encoding UTF8
        throw "HTTP $($response.StatusCode) $Method $Path. Body persisted at $errorPath."
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return $null
    }
    return $response.Content | ConvertFrom-Json
}

function Invoke-PsqlCsv {
    param(
        [string]$Sql,
        [string]$OutputPath
    )
    $sqlPath = Join-Path $QueriesRoot ([System.IO.Path]::GetFileNameWithoutExtension($OutputPath) + '.sql')
    $Sql | Set-Content -LiteralPath $sqlPath -Encoding UTF8
    $result = Get-Content -LiteralPath $sqlPath | docker exec -i np-postgres psql -U $PostgresUser -d $PostgresDb -A -F ',' -q -P footer=off
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed for $sqlPath with exit code $LASTEXITCODE."
    }
    $result | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    return ,@($result | ConvertFrom-Csv)
}

function Reset-Runtime {
    param(
        [string]$Token,
        [string]$EvidencePath
    )
    Add-CommandLog "POST /api/control/runtime/reset"
    $request = @{
        scope = 'runtime-only'
        confirm = 'RESET_RUNTIME_STATE'
        dryRun = $false
        requireExternalStores = $true
        reconcileTerminalOrphans = $true
    }
    try {
        $response = Invoke-Api -Method POST -Path '/api/control/runtime/reset' -Token $Token -Body $request
        $response | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $EvidencePath 'runtime-reset-systemic.json') -Encoding UTF8
        return
    }
    catch {
        Add-CommandLog "POST /api/control/runtime/reset fallback PostgreSQL-only after systemic reset rejection"
    }

    $fallbackRequest = @{
        scope = 'runtime-only'
        confirm = 'RESET_RUNTIME_STATE'
        dryRun = $false
        requireExternalStores = $false
        reconcileTerminalOrphans = $true
    }
    $fallbackResponse = Invoke-Api -Method POST -Path '/api/control/runtime/reset' -Token $Token -Body $fallbackRequest
    $fallbackResponse | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $EvidencePath 'runtime-reset-postgres-only.json') -Encoding UTF8
}

function Wait-OperationCompleted {
    param([string]$OperationId, [string]$Token)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds + $SettlementTimeoutSeconds)
    do {
        $operation = Invoke-Api -Method GET -Path "/api/control/runtime/operations/$OperationId" -Token $Token
        $terminal = [string]$operation.terminalOutcome
        $state = [string]$operation.state
        if ($terminal -eq 'SystemCompleted') {
            return $operation
        }
        if ($terminal -in @('Failed', 'TimedOut', 'Cancelled', 'Orphaned', 'Rejected') -or $state -in @('Failed', 'TimedOut', 'Cancelled', 'Orphaned', 'Rejected')) {
            return $operation
        }
        Start-Sleep -Seconds 2
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Operation $OperationId did not reach SystemCompleted before timeout."
}

function Get-RunInvariants {
    param(
        [string]$RunId,
        [string]$ScenarioEvidenceRoot
    )
    $output = Join-Path $ScenarioEvidenceRoot "postgres-$RunId.csv"
    $sql = @"
WITH run AS (
  SELECT "Id", "NumberOfCycles" FROM control.simulation_runs WHERE "Id" = '$RunId'::uuid
),
settlement AS (
  SELECT COUNT(*) AS settlements,
         COUNT(DISTINCT "CycleIndex") AS settlement_cycles,
         COALESCE(SUM(jsonb_array_length("ExpectedSensorIdsJson"::jsonb)),0) AS expected_memberships,
         COUNT(DISTINCT "ExpectedSensorIdsJson") AS membership_versions
  FROM projection.cycle_settlement WHERE "SimulationRunId" = '$RunId'::uuid
),
cell AS (
  SELECT COUNT(*) AS cell_snapshots,
         COALESCE(SUM("ExpectedCount"),0) AS expected_cell_memberships
  FROM projection.cell_cycle_snapshot WHERE "SimulationRunId" = '$RunId'::uuid
),
area AS (
  SELECT COUNT(*) AS area_snapshots,
         COALESCE(SUM("ExpectedCount"),0) AS expected_area_memberships,
         COALESCE(SUM(CASE WHEN "AlertOutcome" <> 'None' THEN 1 ELSE 0 END),0) AS alerts
  FROM projection.area_cycle_snapshot WHERE "SimulationRunId" = '$RunId'::uuid
),
inbox AS (
  SELECT COUNT(*) FILTER (WHERE "Status" = 6) AS quarantined,
         COUNT(*) - COUNT(DISTINCT "EventId") AS duplicate_rows
  FROM pipeline.event_inbox WHERE "SimulationRunId" = '$RunId'::uuid
),
attempts AS (
  SELECT COUNT(*) FILTER (WHERE pa."Outcome" = 3) AS retry_scheduled,
         COALESCE(MAX(pa."AttemptNumber"),0) AS max_attempt_number
  FROM pipeline.processing_attempts pa
  JOIN pipeline.event_inbox ei ON ei."Id" = pa."InboxEventId"
  WHERE ei."SimulationRunId" = '$RunId'::uuid
),
accepted AS (
  SELECT COUNT(*) AS accepted FROM pipeline.event_inbox WHERE "SimulationRunId" = '$RunId'::uuid AND "Status" = 2
)
SELECT
  '$RunId' AS simulation_run_id,
  (SELECT "NumberOfCycles" FROM run) AS cycles,
  settlement.settlements,
  area.area_snapshots,
  cell.cell_snapshots,
  area.alerts,
  inbox.quarantined,
  inbox.duplicate_rows,
  attempts.retry_scheduled,
  attempts.max_attempt_number,
  accepted.accepted,
  area.expected_area_memberships AS expected,
  settlement.expected_memberships,
  settlement.membership_versions
FROM settlement, cell, area, inbox, attempts, accepted;
"@
    $rows = Invoke-PsqlCsv -Sql $sql -OutputPath $output
    if ($rows.Count -ne 1) {
        throw "Invariant query for $RunId returned $($rows.Count) rows."
    }
    return $rows[0]
}

function Get-ActiveSensorCells {
    $output = Join-Path $OutputRoot 'active-sensor-cells.csv'
    $sql = @"
SELECT s."Name", s."GridCellId"
FROM control.sensor_nodes s
JOIN control.areas a ON a."Id" = s."AreaId"
WHERE a."Code" = '$AreaCode' AND s."IsActive" = TRUE
ORDER BY s."Name";
"@
    return @(Invoke-PsqlCsv -Sql $sql -OutputPath $output)
}

function Test-DistinctCellSeed {
    param(
        [object[]]$SensorRows,
        [int]$Seed,
        [int]$Count
    )
    if ($Count -le 1) {
        return $true
    }
    $shuffled = [System.Collections.ArrayList]::new()
    foreach ($sensor in $SensorRows) {
        [void]$shuffled.Add($sensor)
    }
    $random = [Random]::new($Seed)
    for ($index = $shuffled.Count - 1; $index -gt 0; $index--) {
        $swapIndex = $random.Next($index + 1)
        $temporary = $shuffled[$index]
        $shuffled[$index] = $shuffled[$swapIndex]
        $shuffled[$swapIndex] = $temporary
    }
    $selected = @($shuffled | Select-Object -First $Count)
    return @($selected.GridCellId | Select-Object -Unique).Count -eq $Count
}

function Resolve-DistinctCellSeed {
    param(
        [int]$BaseSeed,
        [bool]$Sequential,
        [object[]]$SensorRows
    )
    for ($candidate = $BaseSeed; $candidate -lt ($BaseSeed + 1000); $candidate++) {
        $currentOk = Test-DistinctCellSeed -SensorRows $SensorRows -Seed $candidate -Count $SensorCount
        $nextOk = (-not $Sequential) -or (Test-DistinctCellSeed -SensorRows $SensorRows -Seed ($candidate + 1) -Count $SensorCount)
        if ($currentOk -and $nextOk) {
            return $candidate
        }
    }
    throw "Could not resolve distinct-cell seed from base $BaseSeed for SensorCount=$SensorCount."
}

function Start-PreventionReplicas {
    param(
        [int]$Replicas,
        [string]$ScenarioEvidenceRoot,
        [hashtable]$BaseEnvironment
    )
    $instances = @()
    for ($index = 1; $index -le $Replicas; $index++) {
        $port = 5260 + $index + ($Replicas * 10)
        $instanceId = "prevention-r$Replicas-i$index-$([guid]::NewGuid().ToString('N').Substring(0,8))"
        $environment = $BaseEnvironment.Clone()
        $environment['ASPNETCORE_URLS'] = "http://127.0.0.1:$port"
        $environment['OTEL_RESOURCE_ATTRIBUTES'] = "service.instance.id=$instanceId,np.prevention.instance_id=$instanceId"
        $environment['Prevention__PipelinePersistenceEnabled'] = 'true'
        $environment['PreventionHost__RetryDelaySeconds__0'] = '0'
        $environment['PreventionHost__RetryDelaySeconds__1'] = '0'
        $environment['PreventionHost__RetryPollingIntervalSeconds'] = '1'
        $environment['ControlledValidation__ProcessingFaults__Enabled'] = 'true'
        $environment['ControlledValidation__ProcessingFaults__EnableBuiltInP3Cases'] = 'true'
        $environment['ControlledValidation__ProcessingFaults__AllowedRunLabelPrefixes__0'] = 'multi-replica-runtime'
        $stdout = Join-Path $ScenarioEvidenceRoot "prevention-$index.out.log"
        $stderr = Join-Path $ScenarioEvidenceRoot "prevention-$index.err.log"
        $process = Start-LoggedProcess `
            -Name "Prevention $instanceId" `
            -FileName 'dotnet' `
            -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj','--no-launch-profile') `
            -Environment $environment `
            -WorkingDirectory $RepoRoot `
            -StdoutPath $stdout `
            -StderrPath $stderr
        Wait-HttpReady -Url "http://127.0.0.1:$port/health/ready" -TimeoutSeconds 60
        $instances += [pscustomobject]@{
            InstanceId = $instanceId
            ProcessId = $process.Id
            Port = $port
            Stdout = $stdout
            Stderr = $stderr
        }
        $ProcessRows.Add([pscustomobject]@{
            replicas = $Replicas
            instance_id = $instanceId
            process_id = $process.Id
            port = $port
            stdout = $stdout
            stderr = $stderr
        })
    }
    return $instances
}

function Invoke-MatrixRun {
    param(
        [int]$Replicas,
        [string]$Scenario,
        [string]$Token,
        [string]$ScenarioEvidenceRoot,
        [int]$Seed,
        [string]$DegradationProfile = 'none',
        [switch]$NoReset
    )
    if (-not $NoReset) {
        Reset-Runtime -Token $Token -EvidencePath $ScenarioEvidenceRoot
    }
    $body = @{
        areaCode = $AreaCode
        scenarioCode = $ScenarioCode
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        seed = $Seed
        degradationProfile = $DegradationProfile
        collectEvidence = $true
        waitForCompletion = $true
        timeoutSeconds = $TimeoutSeconds
        allowParallelRun = $false
        runLabel = "multi-replica-$Replicas-$Scenario-$Seed"
    }
    Add-CommandLog "POST /api/control/runtime/runs replicas=$Replicas scenario=$Scenario"
    $start = Invoke-Api -Method POST -Path '/api/control/runtime/runs' -Token $Token -Body $body
    $start | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $ScenarioEvidenceRoot "start-$Seed.json") -Encoding UTF8
    $operationId = [string]$start.operationId
    if ([string]::IsNullOrWhiteSpace($operationId)) {
        throw "Run start for $Scenario did not return operationId."
    }
    $operation = Wait-OperationCompleted -OperationId $operationId -Token $Token
    $operation | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $ScenarioEvidenceRoot "operation-$Seed.json") -Encoding UTF8
    $runId = [string]$operation.simulationRunId
    if ([string]::IsNullOrWhiteSpace($runId)) {
        throw "Operation $operationId did not expose simulationRunId."
    }
    $run = Invoke-Api -Method GET -Path "/api/control/runtime/runs/$runId" -Token $Token
    $audit = Invoke-Api -Method GET -Path "/api/control/runtime/runs/$runId/audit" -Token $Token
    $timings = Invoke-Api -Method GET -Path "/api/control/runtime/runs/$runId/timings" -Token $Token
    $run | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $ScenarioEvidenceRoot "run-$Seed.json") -Encoding UTF8
    $audit | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $ScenarioEvidenceRoot "audit-$Seed.json") -Encoding UTF8
    $timings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $ScenarioEvidenceRoot "timings-$Seed.json") -Encoding UTF8
    return [pscustomobject]@{
        Operation = $operation
        RunId = $runId
        Audit = $audit
        Invariants = Get-RunInvariants -RunId $runId -ScenarioEvidenceRoot $ScenarioEvidenceRoot
    }
}

function Add-MatrixRow {
    param(
        [int]$Replicas,
        [string]$Scenario,
        [object[]]$Runs,
        [object[]]$Instances,
        [string]$EvidencePath
    )
    $operationIds = @($Runs | ForEach-Object { $_.Operation.operationId }) -join ';'
    $runIds = @($Runs | ForEach-Object { $_.RunId }) -join ';'
    $expected = @($Runs | ForEach-Object { $_.Invariants.expected }) -join ';'
    $accepted = @($Runs | ForEach-Object { $_.Invariants.accepted }) -join ';'
    $settlements = @($Runs | ForEach-Object { $_.Invariants.settlements }) -join ';'
    $cellSnapshots = @($Runs | ForEach-Object { $_.Invariants.cell_snapshots }) -join ';'
    $areaSnapshots = @($Runs | ForEach-Object { $_.Invariants.area_snapshots }) -join ';'
    $alerts = @($Runs | ForEach-Object { $_.Invariants.alerts }) -join ';'
    $quarantined = @($Runs | ForEach-Object { $_.Invariants.quarantined }) -join ';'
    $duplicates = @($Runs | ForEach-Object { $_.Invariants.duplicate_rows }) -join ';'
    $providerInstances = @($Instances | ForEach-Object { "$($_.InstanceId):$($_.ProcessId)" }) -join ';'
    $failures = New-Object System.Collections.Generic.List[string]

    foreach ($run in $Runs) {
        $inv = $run.Invariants
        $cycles = [int]$inv.cycles
        if ([int]$inv.settlements -ne $cycles) { $failures.Add("settlements!=cycles") }
        if ([int]$inv.area_snapshots -ne $cycles) { $failures.Add("area_snapshots!=cycles") }
        if ([int]$inv.cell_snapshots -ne [int]$inv.expected_memberships) { $failures.Add("cell_snapshots!=expected_memberships") }
        if ([int]$inv.quarantined -ne 0) { $failures.Add("quarantined!=0") }
        if ([int]$inv.duplicate_rows -ne 0) { $failures.Add("duplicate_rows!=0") }
        if ([int]$inv.membership_versions -gt $cycles) { $failures.Add("membership_not_frozen") }
    }
    if ($Scenario -eq 'nominal') {
        foreach ($run in $Runs) {
            if ([int]$run.Invariants.expected -ne [int]$run.Invariants.accepted) {
                $failures.Add("nominal_expected!=accepted")
            }
        }
    }
    if ($Scenario -eq 'redelivery_retry') {
        foreach ($run in $Runs) {
            if ([int]$run.Invariants.retry_scheduled -lt 1 -or [int]$run.Invariants.max_attempt_number -lt 2) {
                $failures.Add("redelivery_retry_not_exercised")
            }
        }
    }

    $result = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL:' + ($failures -join '|') }
    $MatrixRows.Add([pscustomobject]@{
        replicas = $Replicas
        scenario = $Scenario
        operation_id = $operationIds
        simulation_run_id = $runIds
        expected = $expected
        accepted = $accepted
        settlements = $settlements
        cell_snapshots = $cellSnapshots
        area_snapshots = $areaSnapshots
        alerts = $alerts
        quarantined = $quarantined
        duplicate_rows = $duplicates
        provider_instances = $providerInstances
        result = $result
        evidence_path = $EvidencePath
    })
    foreach ($run in $Runs) {
        $InvariantRows.Add($run.Invariants)
    }
}

try {
    Push-Location $RepoRoot
    $dotEnv = Read-NpDotEnv -Path (Join-Path $RepoRoot '.env')
    $PostgresHost = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_HOST' -Default 'localhost'
    $PostgresPort = [int](Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_PORT' -Default '5433')
    $PostgresDb = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_DB' -Default 'natureprotector'
    $PostgresUser = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_USER' -Default 'np'
    $PostgresPassword = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_PASSWORD' -Default 'np_dev_pass'
    $RabbitPort = [int](Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_AMQP_PORT' -Default '5672')
    $RabbitUser = Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_USER' -Default 'np'
    $RabbitPassword = Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_PASS' -Default 'np_dev_pass'
    $InfluxPort = [int](Get-DotEnvValue -Values $dotEnv -Name 'INFLUXDB_PORT' -Default '8181')
    $AdminUsername = Get-DotEnvValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_USERNAME' -Default 'admin'
    $AdminPassword = Get-DotEnvValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_PASSWORD' -Default 'admin123'

    if (-not (Test-NpTcpEndpoint -HostName $PostgresHost -Port $PostgresPort -TimeoutMilliseconds 3000)) { throw "PostgreSQL not reachable." }
    if (-not (Test-NpTcpEndpoint -HostName 'localhost' -Port $RabbitPort -TimeoutMilliseconds 3000)) { throw "RabbitMQ not reachable." }

    $config = [ordered]@{
        apiBaseUrl = $ApiBaseUrl
        areaCode = $AreaCode
        scenarioCode = $ScenarioCode
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        timeoutSeconds = $TimeoutSeconds
        settlementTimeoutSeconds = $SettlementTimeoutSeconds
        postgresHost = $PostgresHost
        postgresPort = $PostgresPort
        postgresDb = $PostgresDb
        rabbitPort = $RabbitPort
    }
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'configuration.json') -Encoding UTF8

    Add-CommandLog "scripts/postgres/bootstrap-control-plane.ps1 -SkipBuild:$SkipBuild"
    $bootstrapArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts\postgres\bootstrap-control-plane.ps1')
    if ($SkipBuild) { $bootstrapArgs += '-SkipBuild' }
    & pwsh @bootstrapArgs | Tee-Object -FilePath (Join-Path $LogsRoot 'bootstrap-control-plane.log') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Bootstrap failed with exit code $LASTEXITCODE." }

    if (-not $SkipBuild) {
        Add-CommandLog "dotnet build runtime projects"
        dotnet build src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj -c Release --no-restore | Tee-Object -FilePath (Join-Path $LogsRoot 'build-api.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "API build failed." }
        dotnet build src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj -c Release --no-restore | Tee-Object -FilePath (Join-Path $LogsRoot 'build-prevention.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Prevention build failed." }
        dotnet build src\NatureProtector.Simulator.Host\NatureProtector.Simulator.Host.csproj -c Release --no-restore | Tee-Object -FilePath (Join-Path $LogsRoot 'build-simulator.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Simulator build failed." }
    }

    $baseEnvironment = @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        POSTGRES_HOST = $PostgresHost
        POSTGRES_PORT = [string]$PostgresPort
        POSTGRES_DB = $PostgresDb
        POSTGRES_USER = $PostgresUser
        POSTGRES_PASSWORD = $PostgresPassword
        RabbitMq__HostName = 'localhost'
        RabbitMq__Port = [string]$RabbitPort
        RabbitMq__UserName = $RabbitUser
        RabbitMq__Password = $RabbitPassword
        RabbitMq__ManagementScheme = 'http'
        RabbitMq__ManagementPort = '15672'
        RabbitMq__ManagementAllowInsecureHttp = 'true'
        InfluxDb__Url = "http://localhost:$InfluxPort"
        InfluxDb__Token = (Get-DotEnvValue -Values $dotEnv -Name 'INFLUXDB_TOKEN' -Default '')
        NP_BOOTSTRAP_ADMIN_USERNAME = $AdminUsername
        NP_BOOTSTRAP_ADMIN_PASSWORD = $AdminPassword
    }

    try {
        Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 -Uri "$ApiBaseUrl/health/ready" | Out-Null
    }
    catch {
        $apiEnvironment = $baseEnvironment.Clone()
        $apiEnvironment['ASPNETCORE_URLS'] = $ApiBaseUrl
        $apiEnvironment['BackofficeApi__LocalRuntimeProcessLaunchEnabled'] = 'true'
        $apiEnvironment['RuntimeOrchestration__Mode'] = 'LocalProcess'
        $apiEnvironment['RuntimeOrchestration__EvidenceMode'] = 'FileSystem'
        $apiEnvironment['RuntimeOrchestration__EvidenceRoot'] = (Join-Path $OutputRoot 'api-runtime-evidence')
        $apiEnvironment['RuntimeOrchestration__WorkingDirectory'] = $RepoRoot
        $apiEnvironment['RateLimiting__SimulationLaunch__PermitLimit'] = '1000'
        $apiEnvironment['RateLimiting__SimulationLaunch__WindowSeconds'] = '1'
        $api = Start-LoggedProcess `
            -Name 'Backoffice API' `
            -FileName 'dotnet' `
            -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj','--no-launch-profile') `
            -Environment $apiEnvironment `
            -WorkingDirectory $RepoRoot `
            -StdoutPath (Join-Path $LogsRoot 'api.out.log') `
            -StderrPath (Join-Path $LogsRoot 'api.err.log')
        [void]$StartedProcesses.Remove($api)
        $ApiProcess = $api
        $ProcessRows.Add([pscustomobject]@{ replicas = 0; instance_id = 'backoffice-api'; process_id = $api.Id; port = ([uri]$ApiBaseUrl).Port; stdout = (Join-Path $LogsRoot 'api.out.log'); stderr = (Join-Path $LogsRoot 'api.err.log') })
        Wait-HttpReady -Url "$ApiBaseUrl/health/ready" -TimeoutSeconds 90
    }

    $login = Invoke-Api -Method POST -Path '/api/users-roles/login' -Body @{ usernameOrEmail = $AdminUsername; password = $AdminPassword }
    $token = [string]$login.token
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Login returned no token." }

    $scenarioDefinitions = @(
        @{ name = 'nominal'; profile = 'none'; sequential = $false },
        @{ name = 'duplicate_deliveries'; profile = 'duplicate'; sequential = $false },
        @{ name = 'out_of_order'; profile = 'out-of-order'; sequential = $false },
        @{ name = 'redelivery_retry'; profile = 'retry-transient'; sequential = $false },
        @{ name = 'concurrent_completion'; profile = 'none'; sequential = $false },
        @{ name = 'sequential_runs_isolation'; profile = 'none'; sequential = $true }
    )
    $sensorCellRows = Get-ActiveSensorCells

    foreach ($replicas in 1,2,3) {
        foreach ($scenario in $scenarioDefinitions) {
            $scenarioRoot = Join-Path $ResultsRoot "$replicas-$($scenario.name)"
            New-Item -ItemType Directory -Force -Path $scenarioRoot | Out-Null
            $instances = Start-PreventionReplicas -Replicas $replicas -ScenarioEvidenceRoot $scenarioRoot -BaseEnvironment $baseEnvironment
            try {
                $scenarioIndex = [Array]::IndexOf($scenarioDefinitions, $scenario)
                $baseSeed = 2026071400 + ($replicas * 100) + ($scenarioIndex * 10)
                $seed = Resolve-DistinctCellSeed -BaseSeed $baseSeed -Sequential ([bool]$scenario.sequential) -SensorRows $sensorCellRows
                if ($scenario.name -eq 'redelivery_retry') {
                    Start-Sleep -Milliseconds 500
                    if ($instances.Count -gt 0) {
                        $victim = $StartedProcesses | Where-Object { $_.Id -eq $instances[0].ProcessId } | Select-Object -First 1
                        if ($victim -and -not $victim.HasExited) {
                            $victim.Kill($true)
                            $victim.WaitForExit(15000) | Out-Null
                        }
                    }
                    $instances = Start-PreventionReplicas -Replicas $replicas -ScenarioEvidenceRoot $scenarioRoot -BaseEnvironment $baseEnvironment
                }
                if ($scenario.sequential) {
                    Reset-Runtime -Token $token -EvidencePath $scenarioRoot
                    $first = Invoke-MatrixRun -Replicas $replicas -Scenario $scenario.name -Token $token -ScenarioEvidenceRoot $scenarioRoot -Seed $seed -DegradationProfile $scenario.profile -NoReset
                    $second = Invoke-MatrixRun -Replicas $replicas -Scenario $scenario.name -Token $token -ScenarioEvidenceRoot $scenarioRoot -Seed ($seed + 1) -DegradationProfile $scenario.profile -NoReset
                    Add-MatrixRow -Replicas $replicas -Scenario $scenario.name -Runs @($first, $second) -Instances $instances -EvidencePath $scenarioRoot
                }
                else {
                    $run = Invoke-MatrixRun -Replicas $replicas -Scenario $scenario.name -Token $token -ScenarioEvidenceRoot $scenarioRoot -Seed $seed -DegradationProfile $scenario.profile
                    Add-MatrixRow -Replicas $replicas -Scenario $scenario.name -Runs @($run) -Instances $instances -EvidencePath $scenarioRoot
                }
            }
            catch {
                $MatrixRows.Add([pscustomobject]@{
                    replicas = $replicas
                    scenario = $scenario.name
                    operation_id = ''
                    simulation_run_id = ''
                    expected = ''
                    accepted = ''
                    settlements = ''
                    cell_snapshots = ''
                    area_snapshots = ''
                    alerts = ''
                    quarantined = ''
                    duplicate_rows = ''
                    provider_instances = @($instances | ForEach-Object { "$($_.InstanceId):$($_.ProcessId)" }) -join ';'
                    result = "ERROR:$($_.Exception.Message)"
                    evidence_path = $scenarioRoot
                })
            }
            finally {
                Stop-StartedProcesses
                $StartedProcesses.Clear()
            }
        }
    }
}
finally {
    Stop-StartedProcesses
    if ($null -ne $ApiProcess) {
        try {
            if (-not $ApiProcess.HasExited) {
                $ApiProcess.Kill($true)
                $ApiProcess.WaitForExit(15000) | Out-Null
            }
        }
        catch { }
    }
    if ((Get-Location).Path -eq $RepoRoot) { Pop-Location }
    $MatrixRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'MULTI_REPLICA_MATRIX.csv')
    $ProcessRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'PROCESS_INSTANCES.csv')
    $InvariantRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'POSTGRES_INVARIANTS.csv')
    $Commands | Set-Content -LiteralPath (Join-Path $OutputRoot 'commands.txt') -Encoding UTF8

    $pass = ($MatrixRows.Count -eq 18) -and (@($MatrixRows | Where-Object { $_.result -ne 'PASS' }).Count -eq 0)
    $summary = @(
        '# Multi-Replica Runtime Temporal Matrix',
        '',
        "Status: $(if ($pass) { 'MULTI_REPLICA_TEMPORAL_CORRECTNESS_PASS' } else { 'BLOCKED' })",
        "Rows: $($MatrixRows.Count)",
        "EvidenceRoot: $OutputRoot",
        '',
        '## Matrix',
        ''
    )
    foreach ($row in $MatrixRows) {
        $summary += "- replicas=$($row.replicas) scenario=$($row.scenario) result=$($row.result) operation=$($row.operation_id) run=$($row.simulation_run_id)"
    }
    $summary | Set-Content -LiteralPath (Join-Path $OutputRoot 'MULTI_REPLICA_RESULTS.md') -Encoding UTF8

    [ordered]@{
        schemaVersion = 1
        component = 'multi-replica'
        status = if ($pass) { 'PASS' } else { 'FAIL' }
        nativeStatus = if ($pass) { 'MULTI_REPLICA_TEMPORAL_CORRECTNESS_PASS' } else { 'BLOCKED' }
        rowCount = $MatrixRows.Count
        outputRoot = $OutputRoot
        completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'acceptance-result.json') -Encoding UTF8

    $shaPath = Join-Path $OutputRoot 'SHA256SUMS.txt'
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File |
        Where-Object { $_.FullName -ne $shaPath } |
        Sort-Object FullName |
        ForEach-Object {
            $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
            "$($hash.Hash.ToLowerInvariant())  $($_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/'))"
        } | Set-Content -LiteralPath $shaPath -Encoding UTF8
}

if (($MatrixRows.Count -ne 18) -or (@($MatrixRows | Where-Object { $_.result -ne 'PASS' }).Count -gt 0)) {
    exit 1
}
exit 0
