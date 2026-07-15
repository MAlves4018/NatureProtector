param(
    [string]$OutputRoot = "..\NatureProtector.brain\post-beta\Fixes\ExecutionResults\remediated-integration\autoscaling-runtime",
    [string]$ApiBaseUrl = "http://127.0.0.1:5254",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\common\NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$RepoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln')
$OutputRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputRoot))
if (-not $OutputRoot.EndsWith('NatureProtector.brain\post-beta\Fixes\ExecutionResults\remediated-integration\autoscaling-runtime', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clear unexpected output root: $OutputRoot"
}
if (Test-Path -LiteralPath $OutputRoot) {
    Get-ChildItem -LiteralPath $OutputRoot -Force | Remove-Item -Recurse -Force
}

$LogsRoot = Join-Path $OutputRoot 'logs'
$ResultsRoot = Join-Path $OutputRoot 'results'
$QueriesRoot = Join-Path $OutputRoot 'queries'
$ConfigRoot = Join-Path $OutputRoot 'config'
New-Item -ItemType Directory -Force -Path $OutputRoot, $LogsRoot, $ResultsRoot, $QueriesRoot, $ConfigRoot | Out-Null

$StartedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$ReplicaProcesses = [System.Collections.Generic.List[object]]::new()
$MatrixRows = [System.Collections.Generic.List[object]]::new()
$ReplicaTimeline = [System.Collections.Generic.List[object]]::new()
$BacklogTimeline = [System.Collections.Generic.List[object]]::new()
$LatencyRows = [System.Collections.Generic.List[object]]::new()
$CorrectnessRows = [System.Collections.Generic.List[object]]::new()
$Commands = [System.Collections.Generic.List[string]]::new()
$ReplicaSequence = 0

function Add-CommandLog {
    param([string]$Command)
    $Commands.Add($Command)
    $Command | Add-Content -LiteralPath (Join-Path $LogsRoot 'commands.txt') -Encoding UTF8
}

function Load-DotEnv {
    $path = Join-Path $RepoRoot '.env'
    $values = @{}
    if (-not (Test-Path -LiteralPath $path)) { return $values }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -notmatch '^\s*([^#=\s]+)\s*=\s*(.*)\s*$') { continue }
        $values[$Matches[1]] = $Matches[2].Trim().Trim('"')
    }
    return $values
}

function Get-DotEnvValue {
    param([hashtable]$Values, [string]$Name, [string]$Default = '')
    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) { return [string]$Values[$Name] }
    return $Default
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
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$startInfo.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $startInfo.Environment[$key] = [string]$Environment[$key] }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $process.EnableRaisingEvents = $true
    Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            [System.IO.File]::AppendAllText($Event.MessageData, $EventArgs.Data + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
        }
    } -MessageData $StdoutPath | Out-Null
    Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            [System.IO.File]::AppendAllText($Event.MessageData, $EventArgs.Data + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
        }
    } -MessageData $StderrPath | Out-Null
    if (-not $process.Start()) { throw "Failed to start $Name." }
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    $StartedProcesses.Add($process)
    return $process
}

function Stop-ProcessSafe {
    param([System.Diagnostics.Process]$Process)
    if ($null -eq $Process -or $Process.HasExited) { return }
    try {
        $Process.Kill($true)
        $Process.WaitForExit(10000) | Out-Null
    } catch { }
}

function Stop-AllReplicas {
    foreach ($entry in @($ReplicaProcesses)) { Stop-ProcessSafe -Process $entry.Process }
    $ReplicaProcesses.Clear()
}

function Stop-AllStarted {
    Stop-AllReplicas
    foreach ($process in @($StartedProcesses)) { Stop-ProcessSafe -Process $process }
}

function Wait-Http {
    param([string]$Url, [int]$TimeoutSeconds = 60)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 -Uri $Url -SkipHttpErrorCheck
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) { return }
        } catch { }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Url."
}

function Invoke-Api {
    param([string]$Method, [string]$BaseUrl, [string]$Path, [object]$Body = $null, [string]$Token = '')
    $headers = @{ Accept = 'application/json' }
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $headers.Authorization = "Bearer $Token" }
    $parameters = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $headers; TimeoutSec = 240; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ($Body | ConvertTo-Json -Depth 30)
    }
    Add-CommandLog "$Method $BaseUrl$Path"
    $response = Invoke-WebRequest @parameters
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return [pscustomobject]@{ statusCode = $response.StatusCode } }
    try { return $response.Content | ConvertFrom-Json } catch { return [pscustomobject]@{ statusCode = $response.StatusCode; content = $response.Content } }
}

function Invoke-PsqlCsv {
    param([string]$Sql, [string]$OutputPath)
    $queryPath = Join-Path $QueriesRoot ([IO.Path]::GetFileNameWithoutExtension($OutputPath) + '.sql')
    $Sql | Set-Content -LiteralPath $queryPath -Encoding UTF8
    $env:PGPASSWORD = $PostgresPassword
    $output = & docker exec -i -e PGPASSWORD=$PostgresPassword np-postgres psql -v ON_ERROR_STOP=1 -U $PostgresUser -d $PostgresDb --csv -c $Sql
    $output | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { throw "psql failed: $Sql" }
    if ([string]::IsNullOrWhiteSpace(($output -join "`n"))) { return @() }
    return @($output | ConvertFrom-Csv)
}

function Get-RabbitQueue {
    param([string]$EvidencePath, [string]$Label)
    $uri = "http://localhost:$RabbitManagementPort/api/queues/%2F/np.ingestion.readings"
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${RabbitUser}:${RabbitPassword}"))
    $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers @{ Authorization = "Basic $auth" } -TimeoutSec 10 -SkipHttpErrorCheck
    $response.Content | Set-Content -LiteralPath (Join-Path $EvidencePath "rabbitmq-$Label.json") -Encoding UTF8
    if ($response.StatusCode -ge 400 -or [string]::IsNullOrWhiteSpace($response.Content)) {
        return [pscustomobject]@{ messages_ready = 0; messages_unacknowledged = 0; messages = 0 }
    }
    $json = $response.Content | ConvertFrom-Json
    return [pscustomobject]@{
        messages_ready = [int]$json.messages_ready
        messages_unacknowledged = [int]$json.messages_unacknowledged
        messages = [int]$json.messages
    }
}

function Get-PostgresWork {
    param([string]$EvidencePath, [string]$Label)
    $sql = @"
SELECT
  (SELECT COALESCE(SUM(CASE WHEN "Status"=0 THEN 1 ELSE 0 END),0) FROM pipeline.event_inbox) AS pending,
  (SELECT COALESCE(SUM(CASE WHEN "Status"=1 THEN 1 ELSE 0 END),0) FROM pipeline.event_inbox) AS processing,
  (SELECT COALESCE(SUM(CASE WHEN "Status"=5 THEN 1 ELSE 0 END),0) FROM pipeline.event_inbox) AS retry_pending,
  (SELECT COUNT(*) FROM projection.cycle_settlement WHERE "Status" NOT IN ('Finalized','Completed','Settled')) AS active_settlements;
"@
    $rows = Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $EvidencePath "postgres-work-$Label.csv")
    $first = @($rows)[0]
    $last = @($rows)[-1]
    return [pscustomobject]@{
        pending = [int]$first.pending
        processing = [int]$first.processing
        retry_pending = [int]$first.retry_pending
        active_settlements = [int]$last.active_settlements
    }
}

function Get-Correctness {
    param([string]$EvidencePath, [string]$Label, [string]$RunId)
    $where = if ([string]::IsNullOrWhiteSpace($RunId)) { "TRUE" } else { "`"SimulationRunId`"='$RunId'::uuid" }
    $sql = @"
WITH inbox AS (
  SELECT COUNT(*) AS inbox_rows, COUNT(DISTINCT "EventId") AS distinct_events
  FROM pipeline.event_inbox WHERE $where
),
attempts AS (
  SELECT COUNT(*) FILTER (WHERE p."Outcome"=1) AS accepted
  FROM pipeline.processing_attempts p JOIN pipeline.event_inbox e ON e."Id"=p."InboxEventId"
  WHERE $where
),
settlements AS (
  SELECT COUNT(*) AS settlements, COUNT(*) - COUNT(DISTINCT "SimulationRunId"::text || ':' || "CycleIndex"::text) AS duplicate_settlements
  FROM projection.cycle_settlement WHERE "SimulationRunId"='$RunId'::uuid
),
cell_snapshots AS (
  SELECT COUNT(*) AS cell_snapshots, COUNT(*) - COUNT(DISTINCT "SimulationRunId"::text || ':' || "CycleIndex"::text || ':' || "GridCellId"::text) AS duplicate_cell_snapshots
  FROM projection.cell_cycle_snapshot WHERE "SimulationRunId"='$RunId'::uuid
),
area_snapshots AS (
  SELECT COUNT(*) AS area_snapshots, COUNT(*) - COUNT(DISTINCT "SimulationRunId"::text || ':' || "CycleIndex"::text || ':' || "AreaId"::text) AS duplicate_area_snapshots
  FROM projection.area_cycle_snapshot WHERE "SimulationRunId"='$RunId'::uuid
),
alerts AS (
  SELECT COUNT(*) AS alerts, COUNT(*) - COUNT(DISTINCT "AreaId"::text || ':' || "AlertCode" || ':' || "Status") AS duplicate_alerts
  FROM projection.alert_state
),
quarantine AS (
  SELECT COUNT(*) AS quarantined FROM pipeline.quarantined_events
)
SELECT inbox_rows, distinct_events, accepted, settlements, duplicate_settlements,
       cell_snapshots, duplicate_cell_snapshots, area_snapshots, duplicate_area_snapshots,
       alerts, duplicate_alerts, quarantined
FROM inbox, attempts, settlements, cell_snapshots, area_snapshots, alerts, quarantine;
"@
    $rows = Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $EvidencePath "correctness-$Label.csv")
    $row = @($rows)[0]
    $pass = ([int]$row.inbox_rows -eq [int]$row.distinct_events) -and
        ([int]$row.duplicate_settlements -eq 0) -and
        ([int]$row.duplicate_cell_snapshots -eq 0) -and
        ([int]$row.duplicate_area_snapshots -eq 0) -and
        ([int]$row.duplicate_alerts -eq 0) -and
        ([int]$row.quarantined -eq 0)
    return [pscustomobject]@{
        pass = [bool]$pass
        inbox_rows = [int]$row.inbox_rows
        distinct_events = [int]$row.distinct_events
        accepted = [int]$row.accepted
        settlements = [int]$row.settlements
        duplicate_rows = ([int]$row.duplicate_settlements + [int]$row.duplicate_cell_snapshots + [int]$row.duplicate_area_snapshots + [int]$row.duplicate_alerts)
        quarantined = [int]$row.quarantined
    }
}

function Get-Latency {
    param([string]$EvidencePath, [string]$Label, [string]$RunId)
    $sql = @"
SELECT
  COUNT(*) AS processed,
  COALESCE(percentile_disc(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM ("LastProcessedAt" - "ReceivedAt")) * 1000),0) AS p95_ms,
  COALESCE(EXTRACT(EPOCH FROM (MAX("LastProcessedAt") - MIN("ReceivedAt"))),0) AS drain_seconds
FROM pipeline.event_inbox
WHERE "SimulationRunId"='$RunId'::uuid AND "LastProcessedAt" IS NOT NULL;
"@
    $rows = Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $EvidencePath "latency-$Label.csv")
    $row = @($rows)[0]
    return [pscustomobject]@{
        processed = [int]$row.processed
        p95_ms = [double]$row.p95_ms
        drain_seconds = [double]$row.drain_seconds
    }
}

function Start-Api {
    $env = $BaseEnvironment.Clone()
    $env['ASPNETCORE_URLS'] = $ApiBaseUrl
    $env['RuntimeOrchestration__Mode'] = 'LocalProcess'
    $env['RuntimeOrchestration__EvidenceMode'] = 'FileSystem'
    $env['RuntimeOrchestration__EvidenceRoot'] = (Join-Path $OutputRoot 'api-runtime-evidence')
    $env['RuntimeOrchestration__WorkingDirectory'] = $RepoRoot
    $env['RateLimiting__SimulationLaunch__PermitLimit'] = '1000'
    $env['RateLimiting__SimulationLaunch__WindowSeconds'] = '1'
    $process = Start-LoggedProcess -Name 'Backoffice API' -FileName 'dotnet' -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj','--no-launch-profile') -Environment $env -WorkingDirectory $RepoRoot -StdoutPath (Join-Path $LogsRoot 'api.out.log') -StderrPath (Join-Path $LogsRoot 'api.err.log')
    Wait-Http "$ApiBaseUrl/swagger/index.html" 90
    return $process
}

function Start-Replica {
    param([string]$Experiment)
    $script:ReplicaSequence++
    $id = "autoscaling-$Experiment-$script:ReplicaSequence"
    $env = $BaseEnvironment.Clone()
    $env['ASPNETCORE_URLS'] = "http://127.0.0.1:$([int]5270 + $script:ReplicaSequence)"
    $env['Replica__InstanceId'] = $id
    $env['ControlledValidation__ProcessingFaults__AllowedRunLabelPrefixes__0'] = 'autoscaling'
    $env['PreventionHost__ProcessingLeaseTimeoutSeconds'] = '15'
    $env['PreventionHost__RetryPollingIntervalSeconds'] = '1'
    $stdout = Join-Path $LogsRoot "prevention-$id.out.log"
    $stderr = Join-Path $LogsRoot "prevention-$id.err.log"
    $process = Start-LoggedProcess -Name "Prevention $id" -FileName 'dotnet' -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj','--no-launch-profile') -Environment $env -WorkingDirectory $RepoRoot -StdoutPath $stdout -StderrPath $stderr
    $entry = [pscustomobject]@{ Id = $id; Process = $process; StartedAtUtc = [DateTimeOffset]::UtcNow; Experiment = $Experiment }
    $ReplicaProcesses.Add($entry)
    return $entry
}

function Set-ReplicaCount {
    param([string]$Experiment, [int]$Desired)
    for ($i = $ReplicaProcesses.Count - 1; $i -ge 0; $i--) {
        if ($ReplicaProcesses[$i].Process.HasExited) {
            $ReplicaProcesses.RemoveAt($i)
        }
    }
    while ($ReplicaProcesses.Count -lt $Desired) { Start-Replica -Experiment $Experiment | Out-Null }
    while ($ReplicaProcesses.Count -gt $Desired) {
        $entry = $ReplicaProcesses[$ReplicaProcesses.Count - 1]
        Stop-ProcessSafe -Process $entry.Process
        $ReplicaProcesses.RemoveAt($ReplicaProcesses.Count - 1)
    }
}

function Login {
    $login = Invoke-Api -Method POST -BaseUrl $ApiBaseUrl -Path '/api/users-roles/login' -Body @{ usernameOrEmail = $AdminUsername; password = $AdminPassword }
    return [string]$login.token
}

function Invoke-Reset {
    param([string]$Token, [string]$EvidencePath, [string]$Label)
    $response = Invoke-Api -Method POST -BaseUrl $ApiBaseUrl -Path '/api/control/runtime/reset' -Token $Token -Body @{
        scope = 'runtime-only'
        confirm = 'RESET_RUNTIME_STATE'
        dryRun = $false
        requireExternalStores = $true
        reconcileTerminalOrphans = $true
    }
    $response | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $EvidencePath "reset-$Label.json") -Encoding UTF8
    return $response
}

function Invoke-RequiredReset {
    param([string]$Token, [string]$EvidencePath, [string]$Label, [int]$TimeoutSeconds = 360)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $response = Invoke-Reset -Token $Token -EvidencePath $EvidencePath -Label $Label
        if ($response.status -eq 'Completed') { return $response }
        Start-Sleep -Seconds 5
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Required reset '$Label' did not complete: status=$($response.status) message=$($response.message)"
}

function Start-Run {
    param(
        [string]$Token,
        [string]$Label,
        [int]$SensorCount,
        [int]$Cycles,
        [int]$IntervalSeconds,
        [int]$Seed,
        [string]$DegradationProfile = 'none',
        [string[]]$DegradationProfiles = @('none'),
        [string]$EvidencePath = ''
    )
    $body = @{
        areaCode = 'proenca-a-nova'
        scenarioCode = 'scenario_b'
        sensorCount = $SensorCount
        numberOfCycles = $Cycles
        intervalSeconds = $IntervalSeconds
        seed = $Seed
        degradationProfile = $DegradationProfile
        degradationProfiles = $DegradationProfiles
        collectEvidence = $true
        waitForCompletion = $false
        timeoutSeconds = [Math]::Max(180, ($Cycles * $IntervalSeconds) + 120)
        allowParallelRun = $false
        runLabel = "autoscaling-$Label"
    }
    $run = $null
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        $run = Invoke-Api -Method POST -BaseUrl $ApiBaseUrl -Path '/api/control/runtime/runs' -Token $Token -Body $body
        if ($EvidencePath -ne '') {
            $run | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $EvidencePath "run-start-attempt-$attempt.json") -Encoding UTF8
        }
        if ($run.PSObject.Properties.Name -contains 'operationId' -and -not [string]::IsNullOrWhiteSpace([string]$run.operationId)) { break }
        Start-Sleep -Seconds 5
    }
    if ($run.PSObject.Properties.Name -notcontains 'operationId' -or [string]::IsNullOrWhiteSpace([string]$run.operationId)) {
        $details = $run | ConvertTo-Json -Depth 20 -Compress
        throw "Runtime run '$Label' was not accepted after retries: $details"
    }
    return $run
}

function Get-Operation {
    param([string]$Token, [string]$OperationId)
    return Invoke-Api -Method GET -BaseUrl $ApiBaseUrl -Path "/api/control/runtime/operations/$OperationId" -Token $Token
}

function Invoke-ScalerLoop {
    param(
        [string]$Experiment,
        [string]$Token,
        [string]$OperationId,
        [string]$EvidencePath,
        [int]$MinReplicas,
        [int]$MaxReplicas,
        [int]$TargetBacklogPerReplica,
        [int]$TimeoutSeconds,
        [switch]$HoldScaleDownForPendingWork,
        [switch]$CrashOneReplica
    )
    $started = [DateTimeOffset]::UtcNow
    $lastWorkAt = $started
    $firstScaleUpAt = $null
    $drainedAt = $null
    $postDrainDeadline = $null
    $crashed = $false
    $desired = $MinReplicas
    Set-ReplicaCount -Experiment $Experiment -Desired $desired
    do {
        $now = [DateTimeOffset]::UtcNow
        $rabbit = Get-RabbitQueue -EvidencePath $EvidencePath -Label "sample-$($now.ToUnixTimeMilliseconds())"
        $pg = Get-PostgresWork -EvidencePath $EvidencePath -Label "sample-$($now.ToUnixTimeMilliseconds())"
        $activeReplicas = @($ReplicaProcesses | Where-Object { -not $_.Process.HasExited }).Count
        $work = [int]$rabbit.messages + [int]$pg.pending + [int]$pg.processing + [int]$pg.retry_pending + [int]$pg.active_settlements
        if ($work -gt 0) { $lastWorkAt = $now }
        $desired = $activeReplicas
        if ($work -gt ($TargetBacklogPerReplica * [Math]::Max(1, $activeReplicas))) {
            $desired = [Math]::Min($MaxReplicas, [Math]::Max($activeReplicas + 1, [int][Math]::Ceiling($work / [Math]::Max(1, $TargetBacklogPerReplica))))
        } elseif ($work -eq 0 -and ($now - $lastWorkAt).TotalSeconds -ge 3) {
            $desired = $MinReplicas
        } elseif ($HoldScaleDownForPendingWork -and $work -gt 0) {
            $desired = [Math]::Max($activeReplicas, [Math]::Min($MaxReplicas, 2))
        }
        if ($desired -gt $activeReplicas -and $null -eq $firstScaleUpAt) { $firstScaleUpAt = $now }
        if ($desired -ne $activeReplicas) { Set-ReplicaCount -Experiment $Experiment -Desired $desired }
        if ($CrashOneReplica -and -not $crashed -and @($ReplicaProcesses).Count -gt 1 -and ($now - $started).TotalSeconds -gt 6) {
            Stop-ProcessSafe -Process $ReplicaProcesses[0].Process
            $ReplicaProcesses.RemoveAt(0)
            $crashed = $true
        }
        $activeReplicas = @($ReplicaProcesses | Where-Object { -not $_.Process.HasExited }).Count
        $ReplicaTimeline.Add([pscustomobject]@{ timestamp_utc=$now.UtcDateTime.ToString('o'); experiment=$Experiment; desired_replicas=$desired; active_replicas=$activeReplicas; min_replicas=$MinReplicas; max_replicas=$MaxReplicas; reason="work=$work target=$TargetBacklogPerReplica" })
        $BacklogTimeline.Add([pscustomobject]@{ timestamp_utc=$now.UtcDateTime.ToString('o'); experiment=$Experiment; rabbit_ready=$rabbit.messages_ready; rabbit_unacknowledged=$rabbit.messages_unacknowledged; rabbit_total=$rabbit.messages; pending=$pg.pending; processing=$pg.processing; retry_pending=$pg.retry_pending; active_settlements=$pg.active_settlements; total_work=$work })
        $operation = Get-Operation -Token $Token -OperationId $OperationId
        if ($work -eq 0 -and [string]$operation.terminalOutcome -ne '' -and $null -eq $drainedAt) {
            $drainedAt = $now
            $postDrainDeadline = $now.AddSeconds(6)
        }
        Start-Sleep -Seconds 1
    } while (([DateTimeOffset]::UtcNow - $started).TotalSeconds -lt $TimeoutSeconds -and ($null -eq $drainedAt -or [DateTimeOffset]::UtcNow -lt $postDrainDeadline))
    if ($null -eq $drainedAt) { $drainedAt = [DateTimeOffset]::UtcNow }
    return [pscustomobject]@{
        time_to_scale_up = if ($null -eq $firstScaleUpAt) { $null } else { [Math]::Round(($firstScaleUpAt - $started).TotalSeconds, 3) }
        time_to_drain = [Math]::Round(($drainedAt - $started).TotalSeconds, 3)
        crashed = $crashed
    }
}

function Invoke-Experiment {
    param(
        [string]$Experiment,
        [string]$Token,
        [int]$SensorCount,
        [int]$Cycles,
        [int]$IntervalSeconds,
        [int]$MinReplicas,
        [int]$MaxReplicas,
        [int]$TargetBacklogPerReplica,
        [int]$TimeoutSeconds,
        [string]$DegradationProfile = 'none',
        [string[]]$DegradationProfiles = @('none'),
        [switch]$HoldScaleDownForPendingWork,
        [switch]$CrashOneReplica,
        [switch]$ExpectScaleUp,
        [switch]$ExpectScaleDown,
        [switch]$ExpectLongerThan60
    )
    $caseRoot = Join-Path $ResultsRoot $Experiment
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    Stop-AllReplicas
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'before' | Out-Null
    Set-ReplicaCount -Experiment $Experiment -Desired $MinReplicas
    $run = Start-Run -Token $Token -Label $Experiment -SensorCount $SensorCount -Cycles $Cycles -IntervalSeconds $IntervalSeconds -Seed (202607140 + [int]($Experiment.Substring(1))) -DegradationProfile $DegradationProfile -DegradationProfiles $DegradationProfiles -EvidencePath $caseRoot
    $operationId = [string]$run.operationId
    $scaler = Invoke-ScalerLoop -Experiment $Experiment -Token $Token -OperationId $operationId -EvidencePath $caseRoot -MinReplicas $MinReplicas -MaxReplicas $MaxReplicas -TargetBacklogPerReplica $TargetBacklogPerReplica -TimeoutSeconds $TimeoutSeconds -HoldScaleDownForPendingWork:$HoldScaleDownForPendingWork -CrashOneReplica:$CrashOneReplica
    $operation = Get-Operation -Token $Token -OperationId $operationId
    $runId = [string]$operation.simulationRunId
    $latency = Get-Latency -EvidencePath $caseRoot -Label $Experiment -RunId $runId
    $correctness = Get-Correctness -EvidencePath $caseRoot -Label $Experiment -RunId $runId
    $replicas = @($ReplicaTimeline | Where-Object experiment -eq $Experiment)
    $backlogs = @($BacklogTimeline | Where-Object experiment -eq $Experiment)
    $observedMin = [int](@($replicas | Measure-Object -Property active_replicas -Minimum).Minimum)
    $observedMax = [int](@($replicas | Measure-Object -Property active_replicas -Maximum).Maximum)
    $peakBacklog = [int](@($backlogs | Measure-Object -Property total_work -Maximum).Maximum)
    $finalBacklog = [int]@($backlogs)[-1].total_work
    $scaleUpOk = (-not $ExpectScaleUp) -or ($observedMax -gt $observedMin)
    $scaleDownOk = (-not $ExpectScaleDown) -or (@($replicas)[-1].active_replicas -eq $MinReplicas)
    $longOk = (-not $ExpectLongerThan60) -or ($scaler.time_to_drain -gt 60)
    $pass = $correctness.pass -and $finalBacklog -eq 0 -and $scaleUpOk -and $scaleDownOk -and $longOk
    $LatencyRows.Add([pscustomobject]@{ experiment=$Experiment; operation_id=$operationId; simulation_run_id=$runId; processed=$latency.processed; processing_p95_ms=[Math]::Round($latency.p95_ms,3); time_to_drain=$scaler.time_to_drain })
    $CorrectnessRows.Add([pscustomobject]@{ experiment=$Experiment; simulation_run_id=$runId; inbox_rows=$correctness.inbox_rows; distinct_events=$correctness.distinct_events; accepted=$correctness.accepted; settlements=$correctness.settlements; duplicate_rows=$correctness.duplicate_rows; quarantined=$correctness.quarantined; correctness_pass=$correctness.pass })
    $MatrixRows.Add([pscustomobject]@{
        experiment=$Experiment; operation_id=$operationId; simulation_run_id=$runId; publisher_rate=([Math]::Round($SensorCount / [Math]::Max(1,$IntervalSeconds),3));
        min_replicas=$MinReplicas; max_replicas=$MaxReplicas; observed_min_replicas=$observedMin; observed_max_replicas=$observedMax; peak_backlog=$peakBacklog;
        time_to_scale_up=$scaler.time_to_scale_up; time_to_drain=$scaler.time_to_drain; processing_p95_ms=[Math]::Round($latency.p95_ms,3);
        correctness_pass=$correctness.pass; result=($(if($pass){'PASS'}else{'FAIL'})); evidence_path=$caseRoot
    })
    Stop-AllReplicas
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'after' | Out-Null
}

$dotEnv = Load-DotEnv
$PostgresDb = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_DB' -Default 'natureprotector'
$PostgresUser = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_USER' -Default 'np'
$PostgresPassword = Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_PASSWORD' -Default 'np_dev_pass'
$PostgresPort = [int](Get-DotEnvValue -Values $dotEnv -Name 'POSTGRES_PORT' -Default '5433')
$RabbitUser = Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_USER' -Default 'np'
$RabbitPassword = Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_PASS' -Default 'np_dev_pass'
$RabbitAmqpPort = [int](Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_AMQP_PORT' -Default '5672')
$RabbitManagementPort = [int](Get-DotEnvValue -Values $dotEnv -Name 'RABBITMQ_MANAGEMENT_PORT' -Default '15672')
$InfluxUrl = "http://localhost:$(Get-DotEnvValue -Values $dotEnv -Name 'INFLUXDB_PORT' -Default '8181')"
$InfluxToken = Get-DotEnvValue -Values $dotEnv -Name 'INFLUXDB_TOKEN' -Default ''
$InfluxBucket = Get-DotEnvValue -Values $dotEnv -Name 'INFLUXDB_BUCKET' -Default (Get-DotEnvValue -Values $dotEnv -Name 'INFLUXDB_DATABASE' -Default 'np_telemetry')
$AdminUsername = Get-DotEnvValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_USERNAME' -Default 'admin'
$AdminPassword = Get-DotEnvValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_PASSWORD' -Default 'admin123'

$BaseEnvironment = @{
    ASPNETCORE_ENVIRONMENT='Development'; DOTNET_ENVIRONMENT='Development';
    POSTGRES_HOST='localhost'; POSTGRES_PORT=[string]$PostgresPort; POSTGRES_DB=$PostgresDb; POSTGRES_USER=$PostgresUser; POSTGRES_PASSWORD=$PostgresPassword;
    RabbitMq__HostName='localhost'; RabbitMq__Port=[string]$RabbitAmqpPort; RabbitMq__UserName=$RabbitUser; RabbitMq__Password=$RabbitPassword;
    RabbitMq__ManagementScheme='http'; RabbitMq__ManagementPort=[string]$RabbitManagementPort; RabbitMq__ManagementAllowInsecureHttp='true';
    InfluxDb__Enabled='true'; InfluxDb__Url=$InfluxUrl; InfluxDb__Token=$InfluxToken; InfluxDb__Bucket=$InfluxBucket
}

$config = [ordered]@{ apiBaseUrl=$ApiBaseUrl; postgresDb=$PostgresDb; rabbitAmqpPort=$RabbitAmqpPort; rabbitManagementPort=$RabbitManagementPort; influxUrl=$InfluxUrl; influxBucket=$InfluxBucket; skipBuild=[bool]$SkipBuild }
$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'configuration.json') -Encoding UTF8

try {
    Add-CommandLog "scripts/postgres/bootstrap-control-plane.ps1 -SkipBuild:$SkipBuild"
    $bootstrapArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts\postgres\bootstrap-control-plane.ps1')
    if ($SkipBuild) { $bootstrapArgs += '-SkipBuild' }
    & pwsh @bootstrapArgs | Tee-Object -FilePath (Join-Path $LogsRoot 'bootstrap-control-plane.log') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Bootstrap failed with exit code $LASTEXITCODE." }
    if (-not $SkipBuild) {
        dotnet build src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj -c Release --no-restore | Tee-Object -FilePath (Join-Path $LogsRoot 'build-api.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "API build failed." }
        dotnet build src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj -c Release --no-restore | Tee-Object -FilePath (Join-Path $LogsRoot 'build-prevention.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Prevention build failed." }
    }

    $api = Start-Api
    $token = Login
    $activeSensors = Invoke-PsqlCsv -Sql 'SELECT COUNT(*) AS count FROM control.sensor_nodes s JOIN control.areas a ON a."Id"=s."AreaId" WHERE a."Code"=''proenca-a-nova'' AND s."IsActive";' -OutputPath (Join-Path $OutputRoot 'active-sensors.csv')
    $sensorLimit = [int]@($activeSensors)[0].count
    $baselineSensors = [Math]::Min(12, [Math]::Max(2, $sensorLimit))
    Set-ReplicaCount -Experiment 'capacity-baseline' -Desired 1
    Invoke-RequiredReset -Token $token -EvidencePath $OutputRoot -Label 'capacity-before' | Out-Null
    $baselineRun = Start-Run -Token $token -Label 'capacity-baseline' -SensorCount $baselineSensors -Cycles 8 -IntervalSeconds 1 -Seed 202607148
    $baselineScaler = Invoke-ScalerLoop -Experiment 'capacity-baseline' -Token $token -OperationId ([string]$baselineRun.operationId) -EvidencePath $OutputRoot -MinReplicas 1 -MaxReplicas 1 -TargetBacklogPerReplica 999999 -TimeoutSeconds 120
    $baselineOperation = Get-Operation -Token $token -OperationId ([string]$baselineRun.operationId)
    $baselineLatency = Get-Latency -EvidencePath $OutputRoot -Label 'capacity-baseline' -RunId ([string]$baselineOperation.simulationRunId)
    $capacityPerReplica = if ($baselineScaler.time_to_drain -gt 0) { [Math]::Max(1, [Math]::Round($baselineLatency.processed / $baselineScaler.time_to_drain, 3)) } else { 1 }
    $targetBacklog = [Math]::Max(2, [int][Math]::Floor($capacityPerReplica * 3))
    $capacity = [ordered]@{ measuredAtUtc=[DateTimeOffset]::UtcNow.UtcDateTime.ToString('o'); activeSensors=$sensorLimit; baselineSensors=$baselineSensors; processed=$baselineLatency.processed; timeToDrainSeconds=$baselineScaler.time_to_drain; processingP95Ms=[Math]::Round($baselineLatency.p95_ms,3); measuredCapacityPerReplicaPerSecond=$capacityPerReplica; targetBacklogPerReplica=$targetBacklog; thresholdBasis='floor(measuredCapacityPerReplicaPerSecond * 3s), minimum 2' }
    $capacity | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'CAPACITY_BASELINE.json') -Encoding UTF8
    $capacity | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $ConfigRoot 'local-scaler-config.json') -Encoding UTF8
    Invoke-RequiredReset -Token $token -EvidencePath $OutputRoot -Label 'capacity-after' | Out-Null
    Stop-AllReplicas

    Invoke-Experiment -Experiment 'S1' -Token $token -SensorCount $baselineSensors -Cycles 4 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 1 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 90
    Invoke-Experiment -Experiment 'S2' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 4)) -Cycles 6 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 120 -ExpectScaleUp
    Invoke-Experiment -Experiment 'S3' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 3)) -Cycles 12 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 160 -ExpectScaleUp
    Invoke-Experiment -Experiment 'S4' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 3)) -Cycles 5 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 140 -ExpectScaleUp -ExpectScaleDown
    Invoke-Experiment -Experiment 'S5' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 3)) -Cycles 6 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 140 -DegradationProfile 'retry-transient' -DegradationProfiles @('retry-transient') -ExpectScaleUp -HoldScaleDownForPendingWork
    Invoke-Experiment -Experiment 'S6' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 4)) -Cycles 8 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 150 -ExpectScaleUp -CrashOneReplica
    Invoke-Experiment -Experiment 'S7' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 3)) -Cycles 6 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 140 -DegradationProfile 'none' -DegradationProfiles @('duplicate-deliveries','out-of-order') -ExpectScaleUp
    Invoke-Experiment -Experiment 'S8' -Token $token -SensorCount ([Math]::Min($sensorLimit, $baselineSensors * 4)) -Cycles 75 -IntervalSeconds 1 -MinReplicas 1 -MaxReplicas 4 -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 240 -ExpectScaleUp -ExpectScaleDown -ExpectLongerThan60

    $MatrixRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'AUTOSCALING_MATRIX.csv') -NoTypeInformation -Encoding UTF8
    $ReplicaTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'REPLICA_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $BacklogTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'BACKLOG_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $LatencyRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'LATENCY_RESULTS.csv') -NoTypeInformation -Encoding UTF8
    $CorrectnessRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'CORRECTNESS_RESULTS.csv') -NoTypeInformation -Encoding UTF8
    $Commands | Set-Content -LiteralPath (Join-Path $LogsRoot 'commands.txt') -Encoding UTF8
    $allPass = @($MatrixRows | Where-Object { $_.result -eq 'PASS' }).Count -eq 8
    $replicaVariation = @($MatrixRows | Where-Object { $_.observed_max_replicas -gt $_.observed_min_replicas }).Count
    $status = if ($allPass -and $replicaVariation -ge 4) { 'AUTOSCALING_REALTIME_OBSERVABILITY_PROVED' } else { 'BLOCKED' }
    $summary = [System.Collections.Generic.List[string]]::new()
    $summary.Add('# Autoscaling Runtime Experiment Matrix')
    $summary.Add('')
    $summary.Add("Status: $status")
    $summary.Add("EvidenceRoot: $OutputRoot")
    $summary.Add("MeasuredCapacityPerReplicaPerSecond: $capacityPerReplica")
    $summary.Add("TargetBacklogPerReplica: $targetBacklog")
    $summary.Add('')
    $summary.Add('## Matrix')
    foreach ($row in $MatrixRows) {
        $summary.Add("- $($row.experiment) result=$($row.result) replicas=$($row.observed_min_replicas)-$($row.observed_max_replicas) peak_backlog=$($row.peak_backlog) drain_s=$($row.time_to_drain) p95_ms=$($row.processing_p95_ms)")
    }
    $summary | Set-Content -LiteralPath (Join-Path $OutputRoot 'AUTOSCALING_RESULTS.md') -Encoding UTF8
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/')
    } | Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding UTF8
    if ($status -ne 'AUTOSCALING_REALTIME_OBSERVABILITY_PROVED') { exit 1 }
}
finally {
    Stop-AllStarted
}
