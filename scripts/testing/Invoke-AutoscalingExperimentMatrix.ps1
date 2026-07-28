param(
    [string]$OutputRoot = "",
    [string]$ApiBaseUrl = "http://127.0.0.1:5254",
    [ValidateSet('Autoscaling','FixedReplica','Bottleneck','TemporalCapacity','TemporalComparison','TemporalInflux')]
    [string]$Mode = 'Autoscaling',
    [double[]]$FixedRates = @(),
    [int[]]$FixedReplicas = @(),
    [int]$FixedDefaultRepetitions = 3,
    [int]$FixedFocusedRepetitions = 5,
    [int]$FixedActiveSeconds = 0,
    [int]$BottleneckRepetitions = 3,
    [double]$BottleneckRate = 6.0,
    [ValidateSet('All','Prefetch','Influx')]
    [string]$BottleneckScope = 'All',
    [string]$TemporalWorkloadCatalogPath = "config\autoscaling\temporal-workloads.json",
    [string[]]$TemporalWorkloads = @(),
    [int]$TemporalRepetitions = 3,
    [int]$TemporalFocusedRepetitions = 5,
    [int]$TemporalConstantDurationSeconds = 30,
    [ValidateRange(1,4)]
    [int]$BestFixedReplicas = 3,
    [switch]$SkipBuild,
    [switch]$PreserveOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\common\NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$RepoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln')
$ArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts'))
$MatrixOutputBase = switch ($Mode) {
    'FixedReplica' { [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'scalability-final\fixed-replicas')) }
    'Bottleneck' { [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'scalability-final\bottleneck')) }
    'TemporalCapacity' { [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'scalability-temporal-comparison\capacity-refinement')) }
    'TemporalComparison' { [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'scalability-temporal-comparison\temporal-workloads')) }
    'TemporalInflux' { [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'scalability-temporal-comparison\influx-confirmation')) }
    default { [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'acceptance\matrices\autoscaling-runtime')) }
}
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
$ResourceTimeline = [System.Collections.Generic.List[object]]::new()
$FixedReplicaRows = [System.Collections.Generic.List[object]]::new()
$BottleneckRows = [System.Collections.Generic.List[object]]::new()
$TemporalRows = [System.Collections.Generic.List[object]]::new()
$Commands = [System.Collections.Generic.List[string]]::new()
$ReplicaSequence = 0
$ExperimentEnvironmentOverrides = @{}

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
    $properties = @($json.PSObject.Properties.Name)
    $ready = if ($properties -contains 'messages_ready') { [int]$json.messages_ready } else { 0 }
    $unacked = if ($properties -contains 'messages_unacknowledged') { [int]$json.messages_unacknowledged } else { 0 }
    $total = if ($properties -contains 'messages') { [int]$json.messages } else { $ready + $unacked }
    return [pscustomobject]@{
        messages_ready = $ready
        messages_unacknowledged = $unacked
        messages = $total
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
quarantine AS (
  SELECT COUNT(*) AS quarantined
  FROM pipeline.quarantined_events q
  JOIN pipeline.event_inbox e ON e."Id"=q."InboxEventId"
  WHERE $where
)
SELECT inbox_rows, distinct_events, accepted, settlements, duplicate_settlements,
       cell_snapshots, duplicate_cell_snapshots, area_snapshots, duplicate_area_snapshots,
       quarantined
FROM inbox, attempts, settlements, cell_snapshots, area_snapshots, quarantine;
"@
    $rows = Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $EvidencePath "correctness-$Label.csv")
    $row = @($rows)[0]
    $pass = ([int]$row.inbox_rows -eq [int]$row.distinct_events) -and
        ([int]$row.duplicate_settlements -eq 0) -and
        ([int]$row.duplicate_cell_snapshots -eq 0) -and
        ([int]$row.duplicate_area_snapshots -eq 0) -and
        ([int]$row.quarantined -eq 0)
    return [pscustomobject]@{
        pass = [bool]$pass
        inbox_rows = [int]$row.inbox_rows
        distinct_events = [int]$row.distinct_events
        accepted = [int]$row.accepted
        settlements = [int]$row.settlements
        duplicate_rows = ([int]$row.duplicate_settlements + [int]$row.duplicate_cell_snapshots + [int]$row.duplicate_area_snapshots)
        quarantined = [int]$row.quarantined
    }
}

function Export-RowsCsv {
    param([object[]]$Rows, [string]$OutputPath, [string[]]$Fields)
    if ($Rows.Count -gt 0) {
        $Rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
        return
    }
    ($Fields -join ',') | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}

function Export-EventTrace {
    param(
        [string]$EvidencePath,
        [string]$Label,
        [string]$RunId,
        [string]$RawEventsPath
    )
    $traceRoot = Join-Path $EvidencePath 'event-trace'
    New-Item -ItemType Directory -Force -Path $traceRoot | Out-Null
    if (-not (Test-Path -LiteralPath $RawEventsPath)) {
        throw "Cannot export EventId trace for '$Label': raw events file not found at $RawEventsPath."
    }

    $confirmedRows = @(Import-Csv -LiteralPath $RawEventsPath)
    $confirmedByCycle = @{}
    $confirmed = @($confirmedRows | ForEach-Object {
        $cycle = [string]$_.cycle_index
        if (-not $confirmedByCycle.ContainsKey($cycle)) {
            $confirmedByCycle[$cycle] = [System.Collections.Generic.List[object]]::new()
        }
        $confirmedByCycle[$cycle].Add($_) | Out-Null
        [pscustomobject]@{
            event_id = $_.event_id
            simulation_run_id = $_.simulation_run_id
            cycle_index = $_.cycle_index
            source_stage = 'publisher-confirmed'
            source_table = 'raw.events.csv'
            observed_at_utc = $_.confirmed_utc
            status = ''
        }
    })
    Export-RowsCsv -Rows $confirmed -OutputPath (Join-Path $traceRoot 'confirmed-event-ids.csv') -Fields @('event_id','simulation_run_id','cycle_index','source_stage','source_table','observed_at_utc','status')

    $commonProjection = @('event_id','simulation_run_id','cycle_index','source_stage','source_table','observed_at_utc','status')
    Invoke-PsqlCsv -Sql @"
SELECT
  "EventId" AS event_id,
  "SimulationRunId" AS simulation_run_id,
  NULL::int AS cycle_index,
  'RabbitMQ/inbox' AS source_stage,
  'pipeline.event_inbox' AS source_table,
  "ReceivedAt" AS observed_at_utc,
  "Status"::text AS status
FROM pipeline.event_inbox
WHERE "SimulationRunId"='$RunId'::uuid
ORDER BY "ReceivedAt", "EventId";
"@ -OutputPath (Join-Path $traceRoot 'inbox-event-ids.csv') | Out-Null

    Invoke-PsqlCsv -Sql @"
SELECT
  e."EventId" AS event_id,
  e."SimulationRunId" AS simulation_run_id,
  NULL::int AS cycle_index,
  'processed' AS source_stage,
  'pipeline.processing_attempts' AS source_table,
  COALESCE(p."FinishedAt", p."StartedAt") AS observed_at_utc,
  p."Outcome"::text AS status
FROM pipeline.processing_attempts p
JOIN pipeline.event_inbox e ON e."Id"=p."InboxEventId"
WHERE e."SimulationRunId"='$RunId'::uuid AND p."Outcome"=1
ORDER BY observed_at_utc, e."EventId";
"@ -OutputPath (Join-Path $traceRoot 'processed-event-ids.csv') | Out-Null

    Invoke-PsqlCsv -Sql @"
SELECT
  a."EventId" AS event_id,
  e."SimulationRunId" AS simulation_run_id,
  NULL::int AS cycle_index,
  'persisted' AS source_stage,
  'projection.accepted_reading_log' AS source_table,
  a."PersistedAt" AS observed_at_utc,
  'persisted' AS status
FROM projection.accepted_reading_log a
JOIN pipeline.event_inbox e ON e."EventId"=a."EventId"
WHERE e."SimulationRunId"='$RunId'::uuid
ORDER BY a."PersistedAt", a."EventId";
"@ -OutputPath (Join-Path $traceRoot 'persisted-event-ids.csv') | Out-Null

    Invoke-PsqlCsv -Sql @"
SELECT
  o."EventId" AS event_id,
  o."SimulationRunId" AS simulation_run_id,
  o."CycleIndex" AS cycle_index,
  'projected' AS source_stage,
  'projection.cycle_observation' AS source_table,
  o."CreatedAt" AS observed_at_utc,
  o."Outcome" AS status
FROM projection.cycle_observation o
WHERE o."SimulationRunId"='$RunId'::uuid
UNION ALL
SELECT
  r."SourceEventId" AS event_id,
  r."SimulationRunId" AS simulation_run_id,
  NULL::int AS cycle_index,
  'projected' AS source_stage,
  'projection.risk_assessment_log' AS source_table,
  COALESCE(r."ProjectedAt", r."AssessedAt", r."CreatedAt") AS observed_at_utc,
  r."CalculationStatus" AS status
FROM projection.risk_assessment_log r
WHERE r."SimulationRunId"='$RunId'::uuid
ORDER BY observed_at_utc, event_id;
"@ -OutputPath (Join-Path $traceRoot 'projected-event-ids.csv') | Out-Null

    Invoke-PsqlCsv -Sql @"
SELECT DISTINCT
  s."CycleIndex" AS cycle_index,
  s."SimulationRunId" AS simulation_run_id,
  'projection.cycle_settlement+area_cycle_snapshot' AS source_table,
  COALESCE(s."FinalizedAt", a."SnapshotTimestamp", s."UpdatedAt") AS observed_at_utc,
  s."Status" AS status
FROM projection.cycle_settlement s
JOIN projection.area_cycle_snapshot a
  ON a."SimulationRunId"=s."SimulationRunId"
 AND a."CycleIndex"=s."CycleIndex"
 AND a."AreaId"=s."AreaId"
WHERE s."SimulationRunId"='$RunId'::uuid
ORDER BY s."CycleIndex";
"@ -OutputPath (Join-Path $traceRoot 'final-effect-cycles.csv') | Out-Null
    Invoke-PsqlCsv -Sql @"
SELECT DISTINCT
  o."EventId" AS event_id,
  o."SimulationRunId" AS simulation_run_id,
  o."CycleIndex" AS cycle_index,
  'final-effect' AS source_stage,
  'projection.cycle_observation+cycle_settlement+area_cycle_snapshot' AS source_table,
  COALESCE(s."FinalizedAt", a."SnapshotTimestamp", s."UpdatedAt") AS observed_at_utc,
  s."Status" AS status
FROM projection.cycle_observation o
JOIN projection.cycle_settlement s
  ON s."SimulationRunId"=o."SimulationRunId"
 AND s."CycleIndex"=o."CycleIndex"
 AND s."AreaId"=o."AreaId"
JOIN projection.area_cycle_snapshot a
  ON a."SimulationRunId"=s."SimulationRunId"
 AND a."CycleIndex"=s."CycleIndex"
 AND a."AreaId"=s."AreaId"
WHERE o."SimulationRunId"='$RunId'::uuid
ORDER BY o."CycleIndex", o."EventId";
"@ -OutputPath (Join-Path $traceRoot 'final-effect-event-ids.csv') | Out-Null

    $scriptPath = Join-Path $RepoRoot 'scripts\autoscaling\reconcile-event-trace.py'
    $summaryOutputPath = Join-Path $traceRoot 'reconcile-event-trace.out.json'
    $reconcileOutput = & python $scriptPath --trace-dir $traceRoot
    $reconcileOutput | Set-Content -LiteralPath $summaryOutputPath -Encoding UTF8
    return Get-Content -Raw -LiteralPath (Join-Path $traceRoot 'event-accounting-summary.json') | ConvertFrom-Json
}

function Wait-TemporalEffects {
    param(
        [string]$EvidencePath,
        [string]$Label,
        [string]$RunId,
        [string]$RawEventsPath,
        [int]$ExpectedEffects,
        [int]$TimeoutSeconds = 45
    )
    if ($ExpectedEffects -le 0) { return $null }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastAccounting = $null
    do {
        $lastAccounting = Export-EventTrace -EvidencePath $EvidencePath -Label $Label -RunId $RunId -RawEventsPath $RawEventsPath
        $rabbit = Get-RabbitQueue -EvidencePath $EvidencePath -Label "$Label-effects-wait"
        $pg = Get-PostgresWork -EvidencePath $EvidencePath -Label "$Label-effects-wait"
        $queuesDrained = [int]$rabbit.messages_ready -eq 0 -and [int]$rabbit.messages_unacknowledged -eq 0 -and
            [int]$pg.pending -eq 0 -and [int]$pg.processing -eq 0 -and [int]$pg.retry_pending -eq 0 -and [int]$pg.active_settlements -eq 0
        if ([int]$lastAccounting.final_effect_distinct -ge $ExpectedEffects -and [bool]$lastAccounting.accounting_reconciled -and $queuesDrained) {
            return $lastAccounting
        }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    return $lastAccounting
}

function Get-Latency {
    param([string]$EvidencePath, [string]$Label, [string]$RunId)
    $sql = @"
SELECT
  COUNT(*) AS processed,
  COALESCE(percentile_disc(0.50) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM ("LastProcessedAt" - "ReceivedAt")) * 1000),0) AS p50_ms,
  COALESCE(percentile_disc(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM ("LastProcessedAt" - "ReceivedAt")) * 1000),0) AS p95_ms,
  COALESCE(percentile_disc(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM ("LastProcessedAt" - "ReceivedAt")) * 1000),0) AS p99_ms,
  COALESCE(EXTRACT(EPOCH FROM (MAX("PublishedAt") - MIN("PublishedAt"))),0) AS publish_window_seconds,
  COALESCE(EXTRACT(EPOCH FROM (MAX("LastProcessedAt") - MIN("PublishedAt"))),0) AS processing_window_seconds,
  COALESCE(EXTRACT(EPOCH FROM (MAX("LastProcessedAt") - MIN("ReceivedAt"))),0) AS drain_seconds
FROM pipeline.event_inbox
WHERE "SimulationRunId"='$RunId'::uuid AND "LastProcessedAt" IS NOT NULL;
"@
    $rows = Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $EvidencePath "latency-$Label.csv")
    $row = @($rows)[0]
    return [pscustomobject]@{
        processed = [int]$row.processed
        p50_ms = [double]$row.p50_ms
        p95_ms = [double]$row.p95_ms
        p99_ms = [double]$row.p99_ms
        publish_window_seconds = [double]$row.publish_window_seconds
        processing_window_seconds = [double]$row.processing_window_seconds
        drain_seconds = [double]$row.drain_seconds
        actual_publish_rate = if ([double]$row.publish_window_seconds -gt 0) { [Math]::Round([int]$row.processed / [double]$row.publish_window_seconds, 3) } else { [int]$row.processed }
        completed_throughput = if ([double]$row.processing_window_seconds -gt 0) { [Math]::Round([int]$row.processed / [double]$row.processing_window_seconds, 3) } else { [int]$row.processed }
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
    $env['PreventionHost__ProcessingLeaseTimeoutSeconds'] = '180'
    $env['PreventionHost__RetryPollingIntervalSeconds'] = '1'
    foreach ($key in $script:ExperimentEnvironmentOverrides.Keys) {
        $env[$key] = [string]$script:ExperimentEnvironmentOverrides[$key]
    }
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
        $properties = @($response.PSObject.Properties.Name)
        $status = if ($properties -contains 'status') { [string]$response.status } else { '' }
        if ($status -eq 'Completed') { return $response }
        Start-Sleep -Seconds 5
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    $properties = @($response.PSObject.Properties.Name)
    $status = if ($properties -contains 'status') { [string]$response.status } else { 'missing-status' }
    $message = if ($properties -contains 'message') { [string]$response.message } else { ($response | ConvertTo-Json -Depth 20 -Compress) }
    throw "Required reset '$Label' did not complete: status=$status message=$message"
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

function Add-ResourceSample {
    param([string]$Experiment, [DateTimeOffset]$Timestamp)
    foreach ($entry in @($ReplicaProcesses)) {
        if ($entry.Process.HasExited) { continue }
        try {
            $process = Get-Process -Id $entry.Process.Id -ErrorAction Stop
            $ResourceTimeline.Add([pscustomobject]@{
                timestamp_utc = $Timestamp.UtcDateTime.ToString('o')
                experiment = $Experiment
                replica_id = $entry.Id
                pid = $entry.Process.Id
                cpu_seconds = [Math]::Round([double]$process.CPU, 6)
                working_set_bytes = [int64]$process.WorkingSet64
                private_memory_bytes = [int64]$process.PrivateMemorySize64
                threads = [int]$process.Threads.Count
                handles = [int]$process.HandleCount
            }) | Out-Null
        } catch { }
    }
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
    $zeroTerminalSamples = 0
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
        Add-ResourceSample -Experiment $Experiment -Timestamp $now
        $ReplicaTimeline.Add([pscustomobject]@{ timestamp_utc=$now.UtcDateTime.ToString('o'); experiment=$Experiment; desired_replicas=$desired; active_replicas=$activeReplicas; min_replicas=$MinReplicas; max_replicas=$MaxReplicas; reason="work=$work target=$TargetBacklogPerReplica" })
        $BacklogTimeline.Add([pscustomobject]@{ timestamp_utc=$now.UtcDateTime.ToString('o'); experiment=$Experiment; rabbit_ready=$rabbit.messages_ready; rabbit_unacknowledged=$rabbit.messages_unacknowledged; rabbit_total=$rabbit.messages; pending=$pg.pending; processing=$pg.processing; retry_pending=$pg.retry_pending; active_settlements=$pg.active_settlements; total_work=$work })
        $operation = Get-Operation -Token $Token -OperationId $OperationId
        $operationProperties = @($operation.PSObject.Properties.Name)
        $terminalOutcome = if ($operationProperties -contains 'terminalOutcome') { [string]$operation.terminalOutcome } else { '' }
        if ($work -eq 0 -and $terminalOutcome -ne '') {
            $zeroTerminalSamples++
            if ($zeroTerminalSamples -ge 3 -and $null -eq $drainedAt) {
                $drainedAt = $now
                $postDrainDeadline = $now.AddSeconds(6)
            }
        } else {
            $zeroTerminalSamples = 0
            if ($null -ne $drainedAt) {
                $drainedAt = $null
                $postDrainDeadline = $null
            }
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
    $finalReplicas = [int]@($replicas)[-1].active_replicas
    $peakBacklog = [int](@($backlogs | Measure-Object -Property total_work -Maximum).Maximum)
    $finalBacklog = [int]@($backlogs)[-1].total_work
    $scaleUpOk = (-not $ExpectScaleUp) -or ($observedMax -gt $observedMin)
    $scaleDownOk = (-not $ExpectScaleDown) -or ($finalReplicas -eq $MinReplicas)
    $longOk = (-not $ExpectLongerThan60) -or ($scaler.time_to_drain -gt 60)
    $pass = $correctness.pass -and $finalBacklog -eq 0 -and $scaleUpOk -and $scaleDownOk -and $longOk
    $LatencyRows.Add([pscustomobject]@{ experiment=$Experiment; operation_id=$operationId; simulation_run_id=$runId; processed=$latency.processed; processing_p95_ms=[Math]::Round($latency.p95_ms,3); time_to_drain=$scaler.time_to_drain })
    $CorrectnessRows.Add([pscustomobject]@{ experiment=$Experiment; simulation_run_id=$runId; inbox_rows=$correctness.inbox_rows; distinct_events=$correctness.distinct_events; accepted=$correctness.accepted; settlements=$correctness.settlements; duplicate_rows=$correctness.duplicate_rows; quarantined=$correctness.quarantined; correctness_pass=$correctness.pass })
    $MatrixRows.Add([pscustomobject]@{
        experiment=$Experiment; operation_id=$operationId; simulation_run_id=$runId; publisher_rate=([Math]::Round($SensorCount / [Math]::Max(1,$IntervalSeconds),3));
        min_replicas=$MinReplicas; max_replicas=$MaxReplicas; observed_min_replicas=$observedMin; observed_max_replicas=$observedMax; peak_backlog=$peakBacklog;
        final_replicas=$finalReplicas;
        time_to_scale_up=$scaler.time_to_scale_up; time_to_drain=$scaler.time_to_drain; processing_p95_ms=[Math]::Round($latency.p95_ms,3);
        replicas=$observedMax; processed=$latency.processed; processed_rate=([Math]::Round($latency.processed / [Math]::Max(1,$scaler.time_to_drain),3));
        p95_ms=[Math]::Round($latency.p95_ms,3); backlog_end=$finalBacklog;
        correctness_pass=$correctness.pass; result=($(if($pass){'PASS'}else{'FAIL'})); evidence_path=$caseRoot
    })
    Stop-AllReplicas
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'after' | Out-Null
}

function Get-RateSpec {
    param([double]$Rate, [int]$ActiveSeconds = 0)
    if ($ActiveSeconds -le 0) {
        if ($Rate -eq 0.5) { return [pscustomobject]@{ SensorCount = 1; IntervalSeconds = 2; Cycles = 6; ActualRate = 0.5 } }
        if ($Rate -eq 1.0) { return [pscustomobject]@{ SensorCount = 1; IntervalSeconds = 1; Cycles = 6; ActualRate = 1.0 } }
        if ($Rate -eq 1.5) { return [pscustomobject]@{ SensorCount = 3; IntervalSeconds = 2; Cycles = 6; ActualRate = 1.5 } }
        if ($Rate -eq 3.0) { return [pscustomobject]@{ SensorCount = 3; IntervalSeconds = 1; Cycles = 6; ActualRate = 3.0 } }
        if ($Rate -eq 6.0) { return [pscustomobject]@{ SensorCount = 6; IntervalSeconds = 1; Cycles = 6; ActualRate = 6.0 } }
    }
    foreach ($interval in @(1,2,4,5,10,20)) {
        $sensorCount = [int][Math]::Round($Rate * $interval)
        if ($sensorCount -lt 1 -or $sensorCount -gt 12) { continue }
        $actualRate = $sensorCount / $interval
        if ([Math]::Abs($actualRate - $Rate) -lt 0.000001) {
            $cycles = [Math]::Max(1, [int][Math]::Round($ActiveSeconds / $interval))
            return [pscustomobject]@{ SensorCount = $sensorCount; IntervalSeconds = $interval; Cycles = $cycles; ActualRate = $actualRate }
        }
    }
    throw "Unsupported fixed-replica offered rate: $Rate"
}

function Get-ResourceSummary {
    param([string]$Experiment)
    $samples = @($ResourceTimeline | Where-Object experiment -eq $Experiment)
    if ($samples.Count -eq 0) {
        return [pscustomobject]@{ cpu_avg=0; cpu_peak=0; memory_avg_mb=0; memory_peak_mb=0; samples=0 }
    }
    $byReplica = $samples | Group-Object replica_id
    $cpuRates = [System.Collections.Generic.List[double]]::new()
    foreach ($group in $byReplica) {
        $ordered = @($group.Group | Sort-Object timestamp_utc)
        if ($ordered.Count -lt 2) { continue }
        $first = $ordered[0]
        $last = $ordered[-1]
        $elapsed = ([DateTimeOffset]::Parse([string]$last.timestamp_utc) - [DateTimeOffset]::Parse([string]$first.timestamp_utc)).TotalSeconds
        if ($elapsed -gt 0) {
            $cpuRates.Add(([double]$last.cpu_seconds - [double]$first.cpu_seconds) / $elapsed) | Out-Null
        }
    }
    $memoryValues = @($samples | ForEach-Object { [double]$_.working_set_bytes / 1MB })
    return [pscustomobject]@{
        cpu_avg = if ($cpuRates.Count -gt 0) { [Math]::Round((($cpuRates | Measure-Object -Average).Average), 6) } else { 0 }
        cpu_peak = if ($cpuRates.Count -gt 0) { [Math]::Round((($cpuRates | Measure-Object -Maximum).Maximum), 6) } else { 0 }
        memory_avg_mb = [Math]::Round((($memoryValues | Measure-Object -Average).Average), 3)
        memory_peak_mb = [Math]::Round((($memoryValues | Measure-Object -Maximum).Maximum), 3)
        samples = $samples.Count
    }
}

function Get-ReplicaSeconds {
    param([string]$Experiment)
    $samples = @($ReplicaTimeline | Where-Object experiment -eq $Experiment | Sort-Object timestamp_utc)
    if ($samples.Count -lt 2) { return 0 }
    $sum = 0.0
    for ($i = 0; $i -lt ($samples.Count - 1); $i++) {
        $start = [DateTimeOffset]::Parse([string]$samples[$i].timestamp_utc)
        $end = [DateTimeOffset]::Parse([string]$samples[$i + 1].timestamp_utc)
        $sum += [Math]::Max(0, ($end - $start).TotalSeconds) * [int]$samples[$i].active_replicas
    }
    return [Math]::Round($sum, 3)
}

function Get-ResourceSeconds {
    param([string]$Experiment)
    $samples = @($ResourceTimeline | Where-Object experiment -eq $Experiment | Sort-Object replica_id,timestamp_utc)
    if ($samples.Count -lt 2) {
        return [pscustomobject]@{ cpu_seconds=0; memory_mb_seconds=0 }
    }
    $cpu = 0.0
    $memory = 0.0
    foreach ($group in ($samples | Group-Object replica_id)) {
        $ordered = @($group.Group | Sort-Object timestamp_utc)
        if ($ordered.Count -lt 2) { continue }
        $first = $ordered[0]
        $last = $ordered[-1]
        $cpu += [Math]::Max(0, [double]$last.cpu_seconds - [double]$first.cpu_seconds)
        for ($i = 0; $i -lt ($ordered.Count - 1); $i++) {
            $start = [DateTimeOffset]::Parse([string]$ordered[$i].timestamp_utc)
            $end = [DateTimeOffset]::Parse([string]$ordered[$i + 1].timestamp_utc)
            $memory += [Math]::Max(0, ($end - $start).TotalSeconds) * ([double]$ordered[$i].working_set_bytes / 1MB)
        }
    }
    return [pscustomobject]@{
        cpu_seconds = [Math]::Round($cpu, 6)
        memory_mb_seconds = [Math]::Round($memory, 3)
    }
}

function New-TemporalConstantWorkloadFile {
    param([string]$Path, [string]$WorkloadId, [double]$Rate, [int]$DurationSeconds, [int]$Seed)
    $catalog = [ordered]@{
        schemaVersion = 1
        workloads = @(
            [ordered]@{
                id = $WorkloadId
                description = "Generated constant-rate capacity point"
                warmUpSeconds = 3
                cooldownSeconds = 3
                drainTimeoutSeconds = 180
                seed = $Seed
                segments = @(
                    [ordered]@{
                        id = "steady"
                        kind = "constant"
                        durationSeconds = $DurationSeconds
                        requestedRate = $Rate
                    }
                )
            }
        )
    }
    $catalog | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Start-TemporalLoadProcess {
    param(
        [string]$Experiment,
        [string]$WorkloadPath,
        [string]$WorkloadId,
        [string]$Topology,
        [int]$Repeat,
        [string]$EvidencePath,
        [int]$Seed,
        [int]$TimeoutSeconds = 900
    )
    $env = $BaseEnvironment.Clone()
    $env['Simulator__ControlPlaneEnabled'] = 'true'
    $env['Simulator__ControlPlaneAreaCode'] = 'proenca-a-nova'
    $env['Simulator__ControlPlaneScenarioCode'] = 'scenario_b'
    $env['TemporalLoad__Enabled'] = 'true'
    $env['TemporalLoad__WorkloadPath'] = [System.IO.Path]::GetFullPath($WorkloadPath)
    $env['TemporalLoad__WorkloadId'] = $WorkloadId
    $env['TemporalLoad__OutputRoot'] = (Join-Path $EvidencePath 'raw')
    $env['TemporalLoad__RunLabel'] = $Experiment
    $env['TemporalLoad__Topology'] = $Topology
    $env['TemporalLoad__Repetition'] = [string]$Repeat
    $env['TemporalLoad__Seed'] = [string]$Seed
    $env['TemporalLoad__PublisherTimeoutSeconds'] = [string]$TimeoutSeconds
    foreach ($key in $script:ExperimentEnvironmentOverrides.Keys) {
        $env[$key] = [string]$script:ExperimentEnvironmentOverrides[$key]
    }
    return Start-LoggedProcess -Name "Temporal load $Experiment" -FileName 'dotnet' -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Simulator.Host\NatureProtector.Simulator.Host.csproj','--no-launch-profile') -Environment $env -WorkingDirectory $RepoRoot -StdoutPath (Join-Path $EvidencePath 'temporal-load.out.log') -StderrPath (Join-Path $EvidencePath 'temporal-load.err.log')
}

function Get-TemporalRunArtifacts {
    param([string]$EvidencePath)
    $identityPath = Get-ChildItem -LiteralPath (Join-Path $EvidencePath 'raw') -Recurse -Filter identity.json | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($null -eq $identityPath) { throw "Temporal identity.json was not produced under $EvidencePath." }
    $summaryPath = Join-Path $identityPath.Directory.FullName 'summary.json'
    if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Temporal summary.json was not produced next to $($identityPath.FullName)." }
    return [pscustomobject]@{
        identity = Get-Content -Raw -LiteralPath $identityPath.FullName | ConvertFrom-Json
        summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
        root = $identityPath.Directory.FullName
    }
}

function Invoke-TemporalScalerLoop {
    param(
        [string]$Experiment,
        [System.Diagnostics.Process]$GeneratorProcess,
        [string]$EvidencePath,
        [int]$MinReplicas,
        [int]$MaxReplicas,
        [int]$TargetBacklogPerReplica,
        [int]$TimeoutSeconds
    )
    $started = [DateTimeOffset]::UtcNow
    $lastWorkAt = $started
    $firstScaleUpAt = $null
    $firstScaleDownAt = $null
    $drainedAt = $null
    $zeroAfterExit = 0
    $scaleDecisions = 0
    Set-ReplicaCount -Experiment $Experiment -Desired $MinReplicas
    do {
        $now = [DateTimeOffset]::UtcNow
        $rabbit = Get-RabbitQueue -EvidencePath $EvidencePath -Label "temporal-sample-$($now.ToUnixTimeMilliseconds())"
        $pg = Get-PostgresWork -EvidencePath $EvidencePath -Label "temporal-sample-$($now.ToUnixTimeMilliseconds())"
        $activeReplicas = @($ReplicaProcesses | Where-Object { -not $_.Process.HasExited }).Count
        $work = [int]$rabbit.messages + [int]$pg.pending + [int]$pg.processing + [int]$pg.retry_pending + [int]$pg.active_settlements
        if ($work -gt 0) { $lastWorkAt = $now }
        $desired = $activeReplicas
        if ($work -gt ($TargetBacklogPerReplica * [Math]::Max(1, $activeReplicas))) {
            $desired = [Math]::Min($MaxReplicas, [Math]::Max($activeReplicas + 1, [int][Math]::Ceiling($work / [Math]::Max(1, $TargetBacklogPerReplica))))
        } elseif ($GeneratorProcess.HasExited -and $work -eq 0 -and ($now - $lastWorkAt).TotalSeconds -ge 3) {
            $desired = $MinReplicas
        }
        if ($desired -gt $activeReplicas -and $null -eq $firstScaleUpAt) { $firstScaleUpAt = $now }
        if ($desired -lt $activeReplicas -and $null -eq $firstScaleDownAt) { $firstScaleDownAt = $now }
        if ($desired -ne $activeReplicas) {
            $scaleDecisions++
            Set-ReplicaCount -Experiment $Experiment -Desired $desired
        }
        $activeReplicas = @($ReplicaProcesses | Where-Object { -not $_.Process.HasExited }).Count
        Add-ResourceSample -Experiment $Experiment -Timestamp $now
        $ReplicaTimeline.Add([pscustomobject]@{ timestamp_utc=$now.UtcDateTime.ToString('o'); experiment=$Experiment; desired_replicas=$desired; active_replicas=$activeReplicas; min_replicas=$MinReplicas; max_replicas=$MaxReplicas; reason="temporal work=$work target=$TargetBacklogPerReplica generatorExited=$($GeneratorProcess.HasExited)" })
        $BacklogTimeline.Add([pscustomobject]@{ timestamp_utc=$now.UtcDateTime.ToString('o'); experiment=$Experiment; rabbit_ready=$rabbit.messages_ready; rabbit_unacknowledged=$rabbit.messages_unacknowledged; rabbit_total=$rabbit.messages; pending=$pg.pending; processing=$pg.processing; retry_pending=$pg.retry_pending; active_settlements=$pg.active_settlements; total_work=$work })
        if ($GeneratorProcess.HasExited -and $work -eq 0) {
            $zeroAfterExit++
            if ($zeroAfterExit -ge 3 -and $null -eq $drainedAt) { $drainedAt = $now }
        } else {
            $zeroAfterExit = 0
        }
        Start-Sleep -Seconds 1
    } while (([DateTimeOffset]::UtcNow - $started).TotalSeconds -lt $TimeoutSeconds -and $null -eq $drainedAt)
    if (-not $GeneratorProcess.HasExited) {
        Stop-ProcessSafe -Process $GeneratorProcess
        throw "Temporal load generator '$Experiment' timed out after $TimeoutSeconds seconds."
    }
    if ($GeneratorProcess.ExitCode -ne 0) {
        throw "Temporal load generator '$Experiment' exited with code $($GeneratorProcess.ExitCode)."
    }
    if ($null -eq $drainedAt) { $drainedAt = [DateTimeOffset]::UtcNow }
    return [pscustomobject]@{
        time_to_scale_up = if ($null -eq $firstScaleUpAt) { $null } else { [Math]::Round(($firstScaleUpAt - $started).TotalSeconds, 3) }
        time_to_scale_down = if ($null -eq $firstScaleDownAt) { $null } else { [Math]::Round(($firstScaleDownAt - $started).TotalSeconds, 3) }
        time_to_drain = [Math]::Round(($drainedAt - $started).TotalSeconds, 3)
        scale_decisions = $scaleDecisions
    }
}

function Invoke-TemporalExperiment {
    param(
        [string]$Experiment,
        [string]$Token,
        [string]$WorkloadPath,
        [string]$WorkloadId,
        [string]$Topology,
        [int]$Repeat,
        [int]$ReplicaCount,
        [int]$MinReplicas,
        [int]$MaxReplicas,
        [int]$TargetBacklogPerReplica,
        [bool]$InfluxEnabled = $true,
        [int]$TimeoutSeconds = 900,
        [nullable[double]]$CapacityRate = $null
    )
    $caseRoot = Join-Path $ResultsRoot $Experiment
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    Stop-AllReplicas
    $Token = Login
    $script:ExperimentEnvironmentOverrides = @{
        InfluxDb__Enabled = if ($InfluxEnabled) { 'true' } else { 'false' }
    }
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'before' | Out-Null
    Set-ReplicaCount -Experiment $Experiment -Desired $ReplicaCount
    $generator = Start-TemporalLoadProcess -Experiment $Experiment -WorkloadPath $WorkloadPath -WorkloadId $WorkloadId -Topology $Topology -Repeat $Repeat -EvidencePath $caseRoot -Seed (202607270 + $Repeat + ($ReplicaCount * 1000)) -TimeoutSeconds $TimeoutSeconds
    $scaler = Invoke-TemporalScalerLoop -Experiment $Experiment -GeneratorProcess $generator -EvidencePath $caseRoot -MinReplicas $MinReplicas -MaxReplicas $MaxReplicas -TargetBacklogPerReplica $TargetBacklogPerReplica -TimeoutSeconds $TimeoutSeconds
    $artifacts = Get-TemporalRunArtifacts -EvidencePath $caseRoot
    $runId = [string]$artifacts.identity.simulationRunId
    $rawEventsPath = Join-Path $artifacts.root 'events.csv'
    $accounting = Wait-TemporalEffects -EvidencePath $caseRoot -Label $Experiment -RunId $runId -RawEventsPath $rawEventsPath -ExpectedEffects ([int]$artifacts.summary.publisherConfirmedCount)
    if ($null -eq $accounting) {
        $accounting = Export-EventTrace -EvidencePath $caseRoot -Label $Experiment -RunId $runId -RawEventsPath $rawEventsPath
    }
    $latency = Get-Latency -EvidencePath $caseRoot -Label $Experiment -RunId $runId
    $correctness = Get-Correctness -EvidencePath $caseRoot -Label $Experiment -RunId $runId
    $resources = Get-ResourceSummary -Experiment $Experiment
    $resourceSeconds = Get-ResourceSeconds -Experiment $Experiment
    $replicaSeconds = Get-ReplicaSeconds -Experiment $Experiment
    $cpuSeconds = if ([double]$resourceSeconds.cpu_seconds -gt 0) { [double]$resourceSeconds.cpu_seconds } else { [Math]::Round(([double]$resources.cpu_avg * [double]$replicaSeconds), 6) }
    $replicas = @($ReplicaTimeline | Where-Object experiment -eq $Experiment)
    $backlogs = @($BacklogTimeline | Where-Object experiment -eq $Experiment)
    $peakBacklog = if ($backlogs.Count -gt 0) { [int](($backlogs | Measure-Object -Property total_work -Maximum).Maximum) } else { 0 }
    $finalBacklog = if ($backlogs.Count -gt 0) { [int]@($backlogs)[-1].total_work } else { 0 }
    $observedMax = if ($replicas.Count -gt 0) { [int](($replicas | Measure-Object -Property active_replicas -Maximum).Maximum) } else { $ReplicaCount }
    $requestedRate = [double]$artifacts.summary.requestedRateEventsPerSecond
    $actualRate = [double]$artifacts.summary.actualPublishRateEventsPerSecond
    $confirmedRate = if ($artifacts.summary.publishWindowSeconds -gt 0) { [Math]::Round([int]$artifacts.summary.publisherConfirmedCount / [double]$artifacts.summary.publishWindowSeconds, 6) } else { [int]$artifacts.summary.publisherConfirmedCount }
    $eventLoss = [int]$accounting.event_loss
    $unexpectedDuplicateEffects = [int]$accounting.unexpected_duplicate_effects
    $accountingReconciled = [bool]$accounting.accounting_reconciled
    $duplicateRows = [int]$correctness.duplicate_rows + $unexpectedDuplicateEffects
    $correctnessPass = [bool]$correctness.pass -and $accountingReconciled
    $stable = $correctnessPass -and $finalBacklog -eq 0 -and $latency.completed_throughput -ge ($confirmedRate * 0.95)
    $row = [pscustomobject]@{
        experiment=$Experiment; workload_id=$WorkloadId; topology=$Topology; repeat=$Repeat; replica_count=$ReplicaCount; min_replicas=$MinReplicas; max_replicas=$MaxReplicas; observed_max_replicas=$observedMax;
        requested_rate=$(if($null -ne $CapacityRate){[double]$CapacityRate}else{$requestedRate}); actual_publish_rate=[Math]::Round($actualRate,6); confirmed_rate=$confirmedRate; rate_percent_error=[Math]::Round([double]$artifacts.summary.ratePercentError,6);
        scheduled_events=[int]$artifacts.summary.scheduledEventCount; published_events=[int]$artifacts.summary.actualPublishedCount; confirmed_events=[int]$artifacts.summary.publisherConfirmedCount;
        simulation_run_id=$runId; completed_throughput=$latency.completed_throughput; peak_throughput=$latency.completed_throughput; p50_ms=[Math]::Round($latency.p50_ms,3); p95_ms=[Math]::Round($latency.p95_ms,3); p99_ms=[Math]::Round($latency.p99_ms,3);
        peak_backlog=$peakBacklog; final_backlog=$finalBacklog; drain_seconds=$scaler.time_to_drain; scale_up_seconds=$scaler.time_to_scale_up; scale_down_seconds=$scaler.time_to_scale_down; scale_decisions=$scaler.scale_decisions;
        cpu_avg=$resources.cpu_avg; cpu_peak=$resources.cpu_peak; memory_avg_mb=$resources.memory_avg_mb; memory_peak_mb=$resources.memory_peak_mb; resource_samples=$resources.samples; cpu_seconds=$cpuSeconds; memory_mb_seconds=$resourceSeconds.memory_mb_seconds; replica_seconds=$replicaSeconds;
        event_loss=$eventLoss; missing_event_ids=[int]$accounting.missing_event_ids; duplicate_rows=$duplicateRows; unexpected_duplicate_effects=$unexpectedDuplicateEffects; quarantined=$correctness.quarantined; accounting_reconciled=$accountingReconciled; correctness_pass=$correctnessPass; stable=$stable; influx_enabled=$InfluxEnabled; evidence_path=$caseRoot
    }
    $TemporalRows.Add($row) | Out-Null
    $partialPath = Join-Path $OutputRoot 'TEMPORAL_RAW_RESULTS.partial.csv'
    if (Test-Path -LiteralPath $partialPath) {
        $row | Export-Csv -LiteralPath $partialPath -NoTypeInformation -Encoding UTF8 -Append
    } else {
        $row | Export-Csv -LiteralPath $partialPath -NoTypeInformation -Encoding UTF8
    }
    @($ReplicaTimeline | Where-Object experiment -eq $Experiment) | Export-Csv -LiteralPath (Join-Path $caseRoot 'replica-timeline.csv') -NoTypeInformation -Encoding UTF8
    @($BacklogTimeline | Where-Object experiment -eq $Experiment) | Export-Csv -LiteralPath (Join-Path $caseRoot 'queue-timeline.csv') -NoTypeInformation -Encoding UTF8
    @($ResourceTimeline | Where-Object experiment -eq $Experiment) | Export-Csv -LiteralPath (Join-Path $caseRoot 'process-resources.csv') -NoTypeInformation -Encoding UTF8
    $correctness | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $caseRoot 'accounting.json') -Encoding UTF8
    Stop-AllReplicas
    $Token = Login
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'after' | Out-Null
    return $row
}

function Write-TemporalOutputs {
    param([string]$FileName)
    $TemporalRows | Export-Csv -LiteralPath (Join-Path $OutputRoot $FileName) -NoTypeInformation -Encoding UTF8
    $ReplicaTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'REPLICA_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $BacklogTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'BACKLOG_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $ResourceTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'RESOURCE_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $Commands | Set-Content -LiteralPath (Join-Path $LogsRoot 'commands.txt') -Encoding UTF8
    [ordered]@{
        schemaVersion=1; component=$Mode; status='PASS'; rowCount=$TemporalRows.Count; outputRoot=$OutputRoot; completedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'acceptance-result.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/')
    } | Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding UTF8
}

function Invoke-FixedReplicaExperiment {
    param(
        [string]$Experiment,
        [string]$Token,
        [int]$ReplicaCount,
        [double]$OfferedRate,
        [int]$Repeat,
        [int]$Prefetch = 1,
        [bool]$InfluxEnabled = $true,
        [int]$ActiveSeconds = 0
    )
    $caseRoot = Join-Path $ResultsRoot $Experiment
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    Stop-AllReplicas
    $script:ExperimentEnvironmentOverrides = @{
        PreventionHost__ConsumerPrefetchCount = [string]$Prefetch
        InfluxDb__Enabled = if ($InfluxEnabled) { 'true' } else { 'false' }
    }
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'before' | Out-Null
    Set-ReplicaCount -Experiment $Experiment -Desired $ReplicaCount
    $spec = Get-RateSpec -Rate $OfferedRate -ActiveSeconds $ActiveSeconds
    $run = Start-Run -Token $Token -Label $Experiment -SensorCount $spec.SensorCount -Cycles $spec.Cycles -IntervalSeconds $spec.IntervalSeconds -Seed (202607270 + $Repeat + ($ReplicaCount * 1000)) -EvidencePath $caseRoot
    $scaler = Invoke-ScalerLoop -Experiment $Experiment -Token $Token -OperationId ([string]$run.operationId) -EvidencePath $caseRoot -MinReplicas $ReplicaCount -MaxReplicas $ReplicaCount -TargetBacklogPerReplica 999999 -TimeoutSeconds 180
    $operation = Get-Operation -Token $Token -OperationId ([string]$run.operationId)
    $runId = [string]$operation.simulationRunId
    $latency = Get-Latency -EvidencePath $caseRoot -Label $Experiment -RunId $runId
    $correctness = Get-Correctness -EvidencePath $caseRoot -Label $Experiment -RunId $runId
    $resources = Get-ResourceSummary -Experiment $Experiment
    $backlogs = @($BacklogTimeline | Where-Object experiment -eq $Experiment)
    $peakBacklog = [int](@($backlogs | Measure-Object -Property total_work -Maximum).Maximum)
    $finalBacklog = [int]@($backlogs)[-1].total_work
    $stable = $correctness.pass -and $finalBacklog -eq 0 -and $latency.completed_throughput -ge ($OfferedRate * 0.95)
    $row = [pscustomobject]@{
        experiment=$Experiment; replica_count=$ReplicaCount; offered_rate=$OfferedRate; actual_offered_rate=$spec.ActualRate; repeat=$Repeat; prefetch=$Prefetch; influx_enabled=$InfluxEnabled;
        sensor_count=$spec.SensorCount; cycles=$spec.Cycles; interval_seconds=$spec.IntervalSeconds; active_seconds=($spec.Cycles * $spec.IntervalSeconds);
        operation_id=[string]$run.operationId; simulation_run_id=$runId; expected_events=($spec.SensorCount * $spec.Cycles);
        processed=$latency.processed; actual_publish_rate=$latency.actual_publish_rate; completed_throughput=$latency.completed_throughput;
        p50_ms=[Math]::Round($latency.p50_ms,3); p95_ms=[Math]::Round($latency.p95_ms,3); p99_ms=[Math]::Round($latency.p99_ms,3);
        peak_backlog=$peakBacklog; final_backlog=$finalBacklog; drain_seconds=$scaler.time_to_drain;
        cpu_avg=$resources.cpu_avg; cpu_peak=$resources.cpu_peak; memory_avg_mb=$resources.memory_avg_mb; memory_peak_mb=$resources.memory_peak_mb; resource_samples=$resources.samples;
        duplicate_rows=$correctness.duplicate_rows; quarantined=$correctness.quarantined; correctness_pass=$correctness.pass; stable=$stable; evidence_path=$caseRoot
    }
    $FixedReplicaRows.Add($row) | Out-Null
    $partialPath = Join-Path $OutputRoot 'FIXED_REPLICA_RAW_RESULTS.partial.csv'
    if (Test-Path -LiteralPath $partialPath) {
        $row | Export-Csv -LiteralPath $partialPath -NoTypeInformation -Encoding UTF8 -Append
    } else {
        $row | Export-Csv -LiteralPath $partialPath -NoTypeInformation -Encoding UTF8
    }
    $CorrectnessRows.Add([pscustomobject]@{ experiment=$Experiment; simulation_run_id=$runId; inbox_rows=$correctness.inbox_rows; distinct_events=$correctness.distinct_events; accepted=$correctness.accepted; settlements=$correctness.settlements; duplicate_rows=$correctness.duplicate_rows; quarantined=$correctness.quarantined; correctness_pass=$correctness.pass }) | Out-Null
    Stop-AllReplicas
    Invoke-RequiredReset -Token $Token -EvidencePath $caseRoot -Label 'after' | Out-Null
    return $row
}

function Write-FixedReplicaOutputs {
    $FixedReplicaRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'FIXED_REPLICA_RAW_RESULTS.csv') -NoTypeInformation -Encoding UTF8
    $ReplicaTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'REPLICA_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $BacklogTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'BACKLOG_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $ResourceTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'RESOURCE_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $CorrectnessRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'CORRECTNESS_RESULTS.csv') -NoTypeInformation -Encoding UTF8
    $Commands | Set-Content -LiteralPath (Join-Path $LogsRoot 'commands.txt') -Encoding UTF8
    $stableByReplica = @($FixedReplicaRows | Where-Object stable -eq $true | Group-Object replica_count | ForEach-Object {
        $rate = (($_.Group | Measure-Object -Property offered_rate -Maximum).Maximum)
        [pscustomobject]@{ replica_count=[int]$_.Name; stable_capacity=$rate }
    })
    $baseCandidate = @($stableByReplica | Where-Object replica_count -eq 1)
    $base = if ($baseCandidate.Count -gt 0) { [double]$baseCandidate[0].stable_capacity } elseif ($stableByReplica.Count -gt 0) { [double]@($stableByReplica | Sort-Object replica_count)[0].stable_capacity } else { 0 }
    $summaryRows = foreach ($row in ($stableByReplica | Sort-Object replica_count)) {
        $previous = @($stableByReplica | Where-Object replica_count -eq ([int]$row.replica_count - 1))
        [pscustomobject]@{
            replica_count=$row.replica_count
            stable_capacity=$row.stable_capacity
            speedup=if ($base -gt 0) { [Math]::Round($row.stable_capacity / $base, 6) } else { 0 }
            efficiency=if ($base -gt 0) { [Math]::Round(($row.stable_capacity / $base) / $row.replica_count, 6) } else { 0 }
            marginal_gain=if ($previous.Count -gt 0) { [Math]::Round($row.stable_capacity - [double]$previous[0].stable_capacity, 6) } else { 0 }
        }
    }
    $summaryRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'FIXED_REPLICA_SUMMARY.csv') -NoTypeInformation -Encoding UTF8
    $expectedReplicas = if ($FixedReplicas.Count -gt 0) { $FixedReplicas } else { @(1,2,3,4) }
    $allReplicas = $expectedReplicas | ForEach-Object { $r = $_; @($stableByReplica | Where-Object replica_count -eq $r).Count -gt 0 }
    $allCorrection = @($FixedReplicaRows | Where-Object { -not $_.correctness_pass -or $_.final_backlog -ne 0 -or $_.duplicate_rows -ne 0 -or $_.quarantined -ne 0 }).Count -eq 0
    $status = if (($allReplicas -notcontains $false) -and $allCorrection) { 'FIXED_REPLICA_REPETITION_PROTOCOL_COMPLETE' } else { 'BLOCKED' }
    [ordered]@{
        schemaVersion=1; component='fixed-replica-scalability'; status=if($status -eq 'FIXED_REPLICA_REPETITION_PROTOCOL_COMPLETE'){'PASS'}else{'FAIL'}; nativeStatus=$status;
        rowCount=$FixedReplicaRows.Count; outputRoot=$OutputRoot; completedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'acceptance-result.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/')
    } | Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding UTF8
    if ($status -ne 'FIXED_REPLICA_REPETITION_PROTOCOL_COMPLETE') { exit 1 }
}

function Write-BottleneckOutputs {
    $FixedReplicaRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'BOTTLENECK_AB_RAW_RESULTS.csv') -NoTypeInformation -Encoding UTF8
    $ReplicaTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'REPLICA_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $BacklogTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'BACKLOG_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $ResourceTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'RESOURCE_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
    $CorrectnessRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'CORRECTNESS_RESULTS.csv') -NoTypeInformation -Encoding UTF8
    $summary = @($FixedReplicaRows | Group-Object prefetch,influx_enabled | ForEach-Object {
        $rows = @($_.Group)
        [pscustomobject]@{
            variant=$_.Name
            repetitions=$rows.Count
            mean_throughput=[Math]::Round((($rows | Measure-Object -Property completed_throughput -Average).Average), 6)
            mean_p95_ms=[Math]::Round((($rows | Measure-Object -Property p95_ms -Average).Average), 3)
            mean_peak_backlog=[Math]::Round((($rows | Measure-Object -Property peak_backlog -Average).Average), 3)
            mean_cpu=[Math]::Round((($rows | Measure-Object -Property cpu_avg -Average).Average), 6)
            mean_memory_mb=[Math]::Round((($rows | Measure-Object -Property memory_avg_mb -Average).Average), 3)
            correction_pass=(@($rows | Where-Object { -not $_.correctness_pass -or $_.final_backlog -ne 0 -or $_.duplicate_rows -ne 0 -or $_.quarantined -ne 0 }).Count -eq 0)
        }
    })
    $summary | Export-Csv -LiteralPath (Join-Path $OutputRoot 'BOTTLENECK_AB_SUMMARY.csv') -NoTypeInformation -Encoding UTF8
    $best = @($summary | Sort-Object mean_throughput -Descending)[0]
    $status = if ($summary.Count -ge 5 -and @($summary | Where-Object correction_pass -eq $false).Count -eq 0) { 'BOTTLENECK_ISOLATION_COMPLETE' } else { 'BLOCKED' }
    [ordered]@{
        schemaVersion=1; component='bottleneck-isolation'; status=if($status -eq 'BOTTLENECK_ISOLATION_COMPLETE'){'PASS'}else{'FAIL'}; nativeStatus=$status;
        bestVariant=$best.variant; bestMeanThroughput=$best.mean_throughput; rowCount=$FixedReplicaRows.Count; outputRoot=$OutputRoot; completedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'acceptance-result.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/')
    } | Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding UTF8
    if ($status -ne 'BOTTLENECK_ISOLATION_COMPLETE') { exit 1 }
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
        dotnet build src\NatureProtector.Simulator.Host\NatureProtector.Simulator.Host.csproj -c Release --no-restore | Tee-Object -FilePath (Join-Path $LogsRoot 'build-simulator.log') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Simulator build failed." }
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

    if ($Mode -eq 'FixedReplica') {
        $requestedRates = if ($FixedRates.Count -gt 0) { $FixedRates } else { @(0.5, 1.0, 1.5, 3.0, 6.0) }
        $rateSupportRows = [System.Collections.Generic.List[object]]::new()
        $supportedRates = [System.Collections.Generic.List[double]]::new()
        foreach ($rate in $requestedRates) {
            try {
                $spec = Get-RateSpec -Rate $rate -ActiveSeconds $FixedActiveSeconds
                $supported = $spec.SensorCount -le $sensorLimit
                if ($supported) { $supportedRates.Add($rate) | Out-Null }
                $rateSupportRows.Add([pscustomobject]@{
                    requested_rate=$rate; supported=$supported; reason=if($supported){''}else{"requires $($spec.SensorCount) sensors but only $sensorLimit active sensor(s) exist"};
                    sensor_count=$spec.SensorCount; interval_seconds=$spec.IntervalSeconds; cycles=$spec.Cycles; actual_rate=$spec.ActualRate
                }) | Out-Null
            } catch {
                $rateSupportRows.Add([pscustomobject]@{ requested_rate=$rate; supported=$false; reason=$_.Exception.Message; sensor_count=''; interval_seconds=''; cycles=''; actual_rate='' }) | Out-Null
            }
        }
        $rateSupportRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'UNSUPPORTED_FIXED_RATES.csv') -NoTypeInformation -Encoding UTF8
        $rates = @($supportedRates)
        if ($rates.Count -eq 0) { throw "No requested fixed rates are representable with $sensorLimit active sensor(s)." }
        $replicasToRun = if ($FixedReplicas.Count -gt 0) { $FixedReplicas } else { @(1,2,3,4) }
        foreach ($replicaCount in $replicasToRun) {
            foreach ($rate in $rates) {
                $repetitions = if ($rate -in @(1.0, 1.5)) { $FixedFocusedRepetitions } else { $FixedDefaultRepetitions }
                for ($repeat = 1; $repeat -le $repetitions; $repeat++) {
                    $label = ('F{0}-R{1}-P{2}' -f $replicaCount, ([string]$rate).Replace('.','p'), $repeat)
                    Invoke-FixedReplicaExperiment -Experiment $label -Token $token -ReplicaCount $replicaCount -OfferedRate $rate -Repeat $repeat -ActiveSeconds $FixedActiveSeconds | Out-Null
                }
            }
        }
        Write-FixedReplicaOutputs
        exit 0
    }

    if ($Mode -eq 'Bottleneck') {
        if ($BottleneckScope -in @('All','Prefetch')) {
            foreach ($prefetch in @(1,2,4,8)) {
                for ($repeat = 1; $repeat -le $BottleneckRepetitions; $repeat++) {
                    Invoke-FixedReplicaExperiment -Experiment "H1-prefetch-$prefetch-r$repeat" -Token $token -ReplicaCount 1 -OfferedRate $BottleneckRate -Repeat $repeat -Prefetch $prefetch -InfluxEnabled $true -ActiveSeconds $FixedActiveSeconds | Out-Null
                }
            }
        }
        if ($BottleneckScope -in @('All','Influx')) {
            foreach ($enabled in @($true,$false)) {
                for ($repeat = 1; $repeat -le $BottleneckRepetitions; $repeat++) {
                    $name = if ($enabled) { 'enabled' } else { 'disabled' }
                    Invoke-FixedReplicaExperiment -Experiment "H2-influx-$name-r$repeat" -Token $token -ReplicaCount 1 -OfferedRate $BottleneckRate -Repeat $repeat -Prefetch 1 -InfluxEnabled $enabled -ActiveSeconds $FixedActiveSeconds | Out-Null
                }
            }
        }
        Write-BottleneckOutputs
        exit 0
    }

    if ($Mode -eq 'TemporalCapacity') {
        $requestedRates = if ($FixedRates.Count -gt 0) {
            $FixedRates
        } else {
            @(0.60,0.70,0.75,0.80,0.85,0.90,0.95,1.00,1.25,1.50,1.60,1.70,1.75,1.80,1.90,2.00,2.25,2.40,2.50,2.60,2.70,2.75,2.90,3.00,3.10,3.25,3.50,3.75,4.00)
        }
        $replicasToRun = if ($FixedReplicas.Count -gt 0) { $FixedReplicas } else { @(1,2,3,4) }
        foreach ($replicaCount in $replicasToRun) {
            foreach ($rate in $requestedRates) {
                if (($replicaCount -eq 1 -and $rate -gt 1.00) -or
                    ($replicaCount -eq 2 -and ($rate -lt 1.25 -or $rate -gt 2.00)) -or
                    ($replicaCount -eq 3 -and ($rate -lt 2.00 -or $rate -gt 3.00)) -or
                    ($replicaCount -eq 4 -and ($rate -lt 2.50 -or $rate -gt 4.00))) {
                    continue
                }
                $repetitions = if ($rate -in @(0.80,0.90,1.00,1.50,1.75,2.00,2.50,2.75,3.00,3.50,4.00)) { $TemporalFocusedRepetitions } else { $TemporalRepetitions }
                for ($repeat = 1; $repeat -le $repetitions; $repeat++) {
                    $labelRate = ([string]$rate).Replace('.','p')
                    $label = "TC-R$replicaCount-P$labelRate-r$repeat"
                    $workloadPath = Join-Path $ConfigRoot "$label.workload.json"
                    $workloadId = "capacity-$labelRate"
                    New-TemporalConstantWorkloadFile -Path $workloadPath -WorkloadId $workloadId -Rate $rate -DurationSeconds $TemporalConstantDurationSeconds -Seed (202607270 + $repeat + ($replicaCount * 1000))
                    Invoke-TemporalExperiment -Experiment $label -Token $token -WorkloadPath $workloadPath -WorkloadId $workloadId -Topology "fixed-$replicaCount" -Repeat $repeat -ReplicaCount $replicaCount -MinReplicas $replicaCount -MaxReplicas $replicaCount -TargetBacklogPerReplica 999999 -TimeoutSeconds ([Math]::Max(240, $TemporalConstantDurationSeconds + 180)) -CapacityRate $rate | Out-Null
                }
            }
        }
        Write-TemporalOutputs -FileName 'TEMPORAL_CAPACITY_RAW_RESULTS.csv'
        exit 0
    }

    if ($Mode -eq 'TemporalComparison') {
        $catalog = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $TemporalWorkloadCatalogPath))
        $selectedWorkloads = if ($TemporalWorkloads.Count -gt 0) {
            $TemporalWorkloads
        } else {
            @('W1-low-constant','W2-near-knee-constant','W3-sustained-overload','W4-short-spike','W5-step-load','W6-ramp-up','W7-rise-hold-fall')
        }
        $topologies = @(
            [pscustomobject]@{ Name='fixed-one'; ReplicaCount=1; Min=1; Max=1 },
            [pscustomobject]@{ Name='best-fixed'; ReplicaCount=$BestFixedReplicas; Min=$BestFixedReplicas; Max=$BestFixedReplicas },
            [pscustomobject]@{ Name='autoscaling'; ReplicaCount=1; Min=1; Max=4 }
        )
        foreach ($workloadId in $selectedWorkloads) {
            foreach ($topology in $topologies) {
                for ($repeat = 1; $repeat -le $TemporalRepetitions; $repeat++) {
                    $label = "$($workloadId)-$($topology.Name)-r$repeat"
                    Invoke-TemporalExperiment -Experiment $label -Token $token -WorkloadPath $catalog -WorkloadId $workloadId -Topology $topology.Name -Repeat $repeat -ReplicaCount $topology.ReplicaCount -MinReplicas $topology.Min -MaxReplicas $topology.Max -TargetBacklogPerReplica $targetBacklog -TimeoutSeconds 420 | Out-Null
                }
            }
        }
        Write-TemporalOutputs -FileName 'TEMPORAL_WORKLOAD_RAW_RESULTS.csv'
        exit 0
    }

    if ($Mode -eq 'TemporalInflux') {
        $catalog = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $TemporalWorkloadCatalogPath))
        foreach ($enabled in @($true,$false)) {
            $variant = if ($enabled) { 'enabled' } else { 'disabled' }
            for ($repeat = 1; $repeat -le $BottleneckRepetitions; $repeat++) {
                $label = "TI-influx-$variant-r$repeat"
                Invoke-TemporalExperiment -Experiment $label -Token $token -WorkloadPath $catalog -WorkloadId 'W3-sustained-overload' -Topology "influx-$variant" -Repeat $repeat -ReplicaCount 1 -MinReplicas 1 -MaxReplicas 1 -TargetBacklogPerReplica 999999 -InfluxEnabled $enabled -TimeoutSeconds 420 | Out-Null
            }
        }
        Write-TemporalOutputs -FileName 'INFLUX_CONFIRMATION_RAW_RESULTS.csv'
        exit 0
    }

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
    $ResourceTimeline | Export-Csv -LiteralPath (Join-Path $OutputRoot 'RESOURCE_TIMELINE.csv') -NoTypeInformation -Encoding UTF8
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
    [ordered]@{
        schemaVersion = 1
        component = 'autoscaling'
        status = if ($status -eq 'AUTOSCALING_REALTIME_OBSERVABILITY_PROVED') { 'PASS' } else { 'FAIL' }
        nativeStatus = $status
        rowCount = $MatrixRows.Count
        outputRoot = $OutputRoot
        completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'acceptance-result.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/')
    } | Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding UTF8
    if ($status -ne 'AUTOSCALING_REALTIME_OBSERVABILITY_PROVED') { exit 1 }
    exit 0
}
finally {
    Stop-AllStarted
}
