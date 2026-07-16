param(
    [string]$OutputRoot = "..\NatureProtector.brain\post-beta\Fixes\ExecutionResults\remediated-integration\reset-recovery-runtime",
    [string]$ApiBaseUrl = "http://127.0.0.1:5254",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\common\NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$RepoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln')
$OutputRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputRoot))
if (-not $OutputRoot.EndsWith('NatureProtector.brain\post-beta\Fixes\ExecutionResults\remediated-integration\reset-recovery-runtime', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clear unexpected output root: $OutputRoot"
}
if (Test-Path -LiteralPath $OutputRoot) {
    Get-ChildItem -LiteralPath $OutputRoot -Force | Remove-Item -Recurse -Force
}

$LogsRoot = Join-Path $OutputRoot 'logs'
$QueriesRoot = Join-Path $OutputRoot 'queries'
$ResultsRoot = Join-Path $OutputRoot 'results'
New-Item -ItemType Directory -Force -Path $OutputRoot, $LogsRoot, $QueriesRoot, $ResultsRoot | Out-Null

$StartedProcesses = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
$MatrixRows = New-Object System.Collections.Generic.List[object]
$StoreRows = New-Object System.Collections.Generic.List[object]
$ResetRows = New-Object System.Collections.Generic.List[object]
$Commands = New-Object System.Collections.Generic.List[string]
$ApiProcesses = New-Object System.Collections.Generic.List[System.Diagnostics.Process]

function Add-CommandLog {
    param([string]$Command)
    $Commands.Add($Command)
    $Command | Add-Content -LiteralPath (Join-Path $LogsRoot 'commands.txt') -Encoding UTF8
}

function Get-DotEnvValue {
    param([hashtable]$Values, [string]$Name, [string]$Default = '')
    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }
    return $Default
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

function Stop-StartedProcess {
    param([System.Diagnostics.Process]$Process)
    if ($null -eq $Process -or $Process.HasExited) { return }
    try { $Process.Kill($true); $Process.WaitForExit(15000) | Out-Null } catch { }
}

function Wait-HttpReady {
    param([string]$Url, [int]$TimeoutSeconds = 90)
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
    $parameters = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $headers; TimeoutSec = 300; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ($Body | ConvertTo-Json -Depth 30)
    }
    Add-CommandLog "$Method $BaseUrl$Path"
    try {
        $response = Invoke-WebRequest @parameters
    }
    catch {
        throw "$Method $BaseUrl$Path failed: $($_.Exception.Message)"
    }
    if ($response.StatusCode -ge 400) {
        $safePath = ($Path -replace '[^A-Za-z0-9._-]', '_').Trim('_')
        $errorPath = Join-Path $LogsRoot ("http-error-{0}-{1}-{2}.json" -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), $Method, $safePath)
        [pscustomobject]@{ method=$Method; baseUrl=$BaseUrl; path=$Path; statusCode=$response.StatusCode; body=$response.Content } |
            ConvertTo-Json -Depth 20 |
            Set-Content -LiteralPath $errorPath -Encoding UTF8
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return [pscustomobject]@{ statusCode = $response.StatusCode; content = $null }
    }
    try { return $response.Content | ConvertFrom-Json } catch { return [pscustomobject]@{ statusCode = $response.StatusCode; content = $response.Content } }
}

function Invoke-PsqlCsv {
    param([string]$Sql, [string]$OutputPath)
    $sqlPath = Join-Path $QueriesRoot ([System.IO.Path]::GetFileNameWithoutExtension($OutputPath) + '.sql')
    $Sql | Set-Content -LiteralPath $sqlPath -Encoding UTF8
    $result = Get-Content -LiteralPath $sqlPath | docker exec -i np-postgres psql -U $PostgresUser -d $PostgresDb -A -F ',' -q -P footer=off
    if ($LASTEXITCODE -ne 0) { throw "psql failed for $sqlPath with exit code $LASTEXITCODE." }
    $result | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    return ,@($result | ConvertFrom-Csv)
}

function Get-PostgresCounts {
    param([string]$EvidencePath, [string]$Label)
    $out = Join-Path $EvidencePath "postgres-$Label.csv"
    $sql = @"
SELECT 'simulation_runs' AS item, COUNT(*) AS count FROM control.simulation_runs
UNION ALL SELECT 'runtime_orchestrator_executions', COUNT(*) FROM control.runtime_orchestrator_executions
UNION ALL SELECT 'event_inbox', COUNT(*) FROM pipeline.event_inbox
UNION ALL SELECT 'processing_attempts', COUNT(*) FROM pipeline.processing_attempts
UNION ALL SELECT 'rejected_events', COUNT(*) FROM pipeline.rejected_events
UNION ALL SELECT 'quarantined_events', COUNT(*) FROM pipeline.quarantined_events
UNION ALL SELECT 'accepted_reading_log', COUNT(*) FROM projection.accepted_reading_log
UNION ALL SELECT 'risk_assessment_log', COUNT(*) FROM projection.risk_assessment_log
UNION ALL SELECT 'area_risk_snapshot_log', COUNT(*) FROM projection.area_risk_snapshot_log
UNION ALL SELECT 'daily_cell_state', COUNT(*) FROM projection.daily_cell_state
UNION ALL SELECT 'cycle_settlement', COUNT(*) FROM projection.cycle_settlement
UNION ALL SELECT 'cycle_observation', COUNT(*) FROM projection.cycle_observation
UNION ALL SELECT 'cell_cycle_snapshot', COUNT(*) FROM projection.cell_cycle_snapshot
UNION ALL SELECT 'area_cycle_snapshot', COUNT(*) FROM projection.area_cycle_snapshot
UNION ALL SELECT 'cell_operational_state', COUNT(*) FROM projection.cell_operational_state
UNION ALL SELECT 'area_operational_state', COUNT(*) FROM projection.area_operational_state
UNION ALL SELECT 'alert_state', COUNT(*) FROM projection.alert_state;
"@
    return @(Invoke-PsqlCsv -Sql $sql -OutputPath $out)
}

function Get-CountSum {
    param([object[]]$Rows)
    return [int](@($Rows | Measure-Object -Property count -Sum).Sum)
}

function Get-RabbitCounts {
    param([string]$EvidencePath, [string]$Label)
    $uri = "http://localhost:$RabbitManagementPort/api/queues/%2F/np.ingestion.readings"
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${RabbitUser}:${RabbitPassword}"))
    $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers @{ Authorization = "Basic $auth" } -TimeoutSec 10 -SkipHttpErrorCheck
    $path = Join-Path $EvidencePath "rabbitmq-$Label.json"
    $response.Content | Set-Content -LiteralPath $path -Encoding UTF8
    if ($response.StatusCode -ge 400 -or [string]::IsNullOrWhiteSpace($response.Content)) {
        return [pscustomobject]@{ messages = $null; unacknowledged = $null; total = $null }
    }
    $json = $response.Content | ConvertFrom-Json
    return [pscustomobject]@{ messages = [int]$json.messages; unacknowledged = [int]$json.messages_unacknowledged; total = [int]$json.messages + [int]$json.messages_unacknowledged }
}

function Wait-RabbitUnacknowledged {
    param([string]$EvidencePath, [int]$TimeoutSeconds = 30)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $counts = Get-RabbitCounts -EvidencePath $EvidencePath -Label 'unacked-wait'
        if ($counts.unacknowledged -gt 0) { return $counts }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "RabbitMQ did not expose an unacknowledged delivery within $TimeoutSeconds second(s)."
}

function Wait-RabbitQuiescent {
    param([string]$EvidencePath, [int]$TimeoutSeconds = 30)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $counts = Get-RabbitCounts -EvidencePath $EvidencePath -Label 'quiescent-wait'
        if ($counts.unacknowledged -eq 0) { return $counts }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "RabbitMQ still had unacknowledged deliveries after $TimeoutSeconds second(s)."
}

function Invoke-InfluxSql {
    param([string]$Sql, [string]$EvidencePath, [string]$Label)
    $query = [System.Web.HttpUtility]::UrlEncode($Sql)
    $uri = "$InfluxUrl/api/v3/query_sql?db=$InfluxBucket&q=$query&format=json"
    $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers @{ Authorization = "Bearer $InfluxToken" } -TimeoutSec 20 -SkipHttpErrorCheck
    $path = Join-Path $EvidencePath "influx-$Label.json"
    $response.Content | Set-Content -LiteralPath $path -Encoding UTF8
    if ($response.StatusCode -ge 400 -or [string]::IsNullOrWhiteSpace($response.Content)) { return @() }
    return @($response.Content | ConvertFrom-Json)
}

function Ensure-InfluxDatabase {
    $query = [System.Web.HttpUtility]::UrlEncode("SHOW TABLES")
    $uri = "$InfluxUrl/api/v3/query_sql?db=$InfluxBucket&q=$query&format=json"
    $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers @{ Authorization = "Bearer $InfluxToken" } -TimeoutSec 20 -SkipHttpErrorCheck
    $response.Content | Set-Content -LiteralPath (Join-Path $OutputRoot 'influx-preflight-show-tables.json') -Encoding UTF8
    if ($response.StatusCode -lt 400) { return }

    Add-CommandLog "docker exec np-influxdb influxdb3 create database $InfluxBucket"
    docker exec np-influxdb influxdb3 create database --host http://127.0.0.1:8181 --token $InfluxToken $InfluxBucket |
        Tee-Object -FilePath (Join-Path $LogsRoot 'influx-create-database.log') | Out-Null
}

function Get-InfluxCounts {
    param([string]$EvidencePath, [string]$Label)
    $tables = Invoke-InfluxSql -Sql "SHOW TABLES" -EvidencePath $EvidencePath -Label "$Label-tables"
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($table in @('accepted_readings','risk_assessments','area_risk_snapshots')) {
        $rows = Invoke-InfluxSql -Sql "SELECT COUNT(*) AS count FROM $table" -EvidencePath $EvidencePath -Label "$Label-$table-count"
        $rowArray = @($rows)
        $count = if ($rowArray.Count -gt 0 -and $rowArray[0].PSObject.Properties.Name -contains 'count') { [int]$rowArray[0].count } else { 0 }
        $items.Add([pscustomobject]@{ item = $table; count = $count })
    }
    $schemaCount = @($tables | Where-Object { $_.table_name -in @('accepted_readings','risk_assessments') }).Count
    return [pscustomobject]@{ total = [int](@($items | Measure-Object -Property count -Sum).Sum); schema_tables = $schemaCount; rows = $items }
}

function Start-Api {
    param([int]$Port, [hashtable]$Overrides = @{})
    $env = $BaseEnvironment.Clone()
    $env['ASPNETCORE_URLS'] = "http://127.0.0.1:$Port"
    $env['BackofficeApi__LocalRuntimeProcessLaunchEnabled'] = 'true'
    $env['RuntimeOrchestration__Mode'] = 'LocalProcess'
    $env['RuntimeOrchestration__EvidenceMode'] = 'FileSystem'
    $env['RuntimeOrchestration__EvidenceRoot'] = (Join-Path $OutputRoot 'api-runtime-evidence')
    $env['RuntimeOrchestration__WorkingDirectory'] = $RepoRoot
    $env['RateLimiting__SimulationLaunch__PermitLimit'] = '1000'
    $env['RateLimiting__SimulationLaunch__WindowSeconds'] = '1'
    foreach ($key in $Overrides.Keys) { $env[$key] = $Overrides[$key] }
    $p = Start-LoggedProcess -Name "Backoffice API $Port" -FileName 'dotnet' -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj','--no-launch-profile') -Environment $env -WorkingDirectory $RepoRoot -StdoutPath (Join-Path $LogsRoot "api-$Port.out.log") -StderrPath (Join-Path $LogsRoot "api-$Port.err.log")
    $ApiProcesses.Add($p)
    Wait-HttpReady -Url "http://127.0.0.1:$Port/health/ready" -TimeoutSeconds 90
    return [pscustomobject]@{ Process = $p; BaseUrl = "http://127.0.0.1:$Port" }
}

function Start-Prevention {
    $env = $BaseEnvironment.Clone()
    $env['ASPNETCORE_URLS'] = "http://127.0.0.1:5264"
    $env['Prevention__PipelinePersistenceEnabled'] = 'true'
    $p = Start-LoggedProcess -Name 'Prevention' -FileName 'dotnet' -Arguments @('run','-c','Release','--no-build','--no-restore','--project','src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj','--no-launch-profile') -Environment $env -WorkingDirectory $RepoRoot -StdoutPath (Join-Path $LogsRoot 'prevention.out.log') -StderrPath (Join-Path $LogsRoot 'prevention.err.log')
    Wait-HttpReady -Url "http://127.0.0.1:5264/health/ready" -TimeoutSeconds 90
    return $p
}

function Login {
    param([string]$BaseUrl)
    $login = Invoke-Api -Method POST -BaseUrl $BaseUrl -Path '/api/users-roles/login' -Body @{ usernameOrEmail = $AdminUsername; password = $AdminPassword }
    return [string]$login.token
}

function Invoke-Reset {
    param([string]$BaseUrl, [string]$Token, [string]$EvidencePath, [string]$Label, [bool]$RequireExternalStores = $true)
    Add-CommandLog "POST $BaseUrl/api/control/runtime/reset label=$Label requireExternalStores=$RequireExternalStores"
    $response = Invoke-Api -Method POST -BaseUrl $BaseUrl -Path '/api/control/runtime/reset' -Token $Token -Body @{
        scope = 'runtime-only'
        confirm = 'RESET_RUNTIME_STATE'
        dryRun = $false
        requireExternalStores = $RequireExternalStores
        reconcileTerminalOrphans = $true
    }
    $response | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $EvidencePath "reset-$Label.json") -Encoding UTF8
    return $response
}

function Start-SmallRun {
    param([string]$BaseUrl, [string]$Token, [string]$Label, [int]$Cycles = 2, [bool]$Wait = $true)
    $body = @{
        areaCode = 'proenca-a-nova'
        scenarioCode = 'scenario_b'
        sensorCount = 2
        numberOfCycles = $Cycles
        intervalSeconds = 1
        seed = 202607141
        degradationProfile = 'none'
        collectEvidence = $true
        waitForCompletion = $Wait
        timeoutSeconds = [Math]::Max(60, $Cycles * 5)
        allowParallelRun = $false
        runLabel = "reset-recovery-$Label"
    }
    Add-CommandLog "POST $BaseUrl/api/control/runtime/runs label=$Label"
    $response = Invoke-Api -Method POST -BaseUrl $BaseUrl -Path '/api/control/runtime/runs' -Token $Token -Body $body
    if ($response.PSObject.Properties.Name -notcontains 'operationId' -or [string]::IsNullOrWhiteSpace([string]$response.operationId)) {
        throw "Runtime run '$Label' was not accepted: status=$($response.status) message=$($response.message)"
    }
    return $response
}

function Wait-Operation {
    param([string]$BaseUrl, [string]$Token, [string]$OperationId, [int]$TimeoutSeconds = 180)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $op = Invoke-Api -Method GET -BaseUrl $BaseUrl -Path "/api/control/runtime/operations/$OperationId" -Token $Token
        if ($op.PSObject.Properties.Name -notcontains 'terminalOutcome') {
            throw "Operation $OperationId lookup did not return an operation record."
        }
        if ([string]$op.terminalOutcome -ne '') { return $op }
        Start-Sleep -Seconds 2
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Operation $OperationId did not become terminal."
}

function New-RuntimeOperationFixture {
    $id = [guid]::NewGuid().ToString()
    $sql = "INSERT INTO control.runtime_orchestrator_executions (execution_id,idempotency_key,provider,state,accepted_at,updated_at,log_correlation,request_id,requested_state,provider_state,run_state,processing_state,is_operational,deadline_at) VALUES ('$id','reset-recovery-$id','fixture','Running',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'reset-recovery-$id',gen_random_uuid(),'Requested','Running','Pending','Pending',true,CURRENT_TIMESTAMP + interval '10 minutes');"
    Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $OutputRoot "insert-runtime-operation-$id.csv") | Out-Null
    return $id
}

function New-InboxFixture {
    param([int]$Status)
    $id = [guid]::NewGuid().ToString()
    $eventId = [guid]::NewGuid().ToString()
    $sql = @"
INSERT INTO pipeline.event_inbox ("Id","EventId","SchemaVersion","CorrelationId","Producer","EventType","AreaId","EventTime","ReceivedAt","PayloadJson","EnvelopeJson","Status","AttemptCount")
SELECT '$id'::uuid,'$eventId'::uuid,'v1','reset-recovery-fixture','fixture','SensorReadingProduced',"Id",CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{}','{}',$Status,0
FROM control.areas WHERE "Code"='proenca-a-nova' LIMIT 1;
"@
    Invoke-PsqlCsv -Sql $sql -OutputPath (Join-Path $OutputRoot "insert-inbox-$id.csv") | Out-Null
    return $id
}

function Publish-RabbitMessage {
    $uri = "http://localhost:$RabbitManagementPort/api/exchanges/%2F/amq.default/publish"
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${RabbitUser}:${RabbitPassword}"))
    $body = @{ properties = @{}; routing_key = 'np.ingestion.readings'; payload = '{"fixture":"reset-recovery"}'; payload_encoding = 'string' }
    Invoke-WebRequest -UseBasicParsing -Method POST -Uri $uri -Headers @{ Authorization = "Basic $auth" } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 10) -TimeoutSec 10 | Out-Null
}

function Start-UnackedConsumer {
    $helper = Join-Path $OutputRoot 'unacked-helper'
    New-Item -ItemType Directory -Force -Path $helper | Out-Null
    $csproj = Join-Path $helper 'UnackedConsumer.csproj'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><PackageReference Include="RabbitMQ.Client" Version="6.8.1" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $csproj -Encoding UTF8
    @"
using RabbitMQ.Client;
var factory = new ConnectionFactory { HostName = "localhost", Port = $RabbitAmqpPort, UserName = "$RabbitUser", Password = "$RabbitPassword", DispatchConsumersAsync = false };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();
channel.BasicQos(0, 1, false);
var result = channel.BasicGet("np.ingestion.readings", autoAck: false);
if (result is null) { Console.Error.WriteLine("No message available."); return 2; }
Console.WriteLine(result.DeliveryTag);
await Task.Delay(TimeSpan.FromMinutes(5));
return 0;
"@ | Set-Content -LiteralPath (Join-Path $helper 'Program.cs') -Encoding UTF8
    dotnet build $csproj -c Release --nologo | Tee-Object -FilePath (Join-Path $LogsRoot 'build-unacked-helper.log') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Unacked helper build failed." }
    return Start-LoggedProcess -Name 'RabbitMQ unacked consumer' -FileName 'dotnet' -Arguments @('run','-c','Release','--no-build','--project',$csproj) -Environment @{} -WorkingDirectory $helper -StdoutPath (Join-Path $LogsRoot 'unacked-consumer.out.log') -StderrPath (Join-Path $LogsRoot 'unacked-consumer.err.log')
}

function Add-StoreRows {
    param([string]$Case, [object[]]$PgBefore, [object[]]$PgAfter, [object]$RabbitBefore, [object]$RabbitAfter, [object]$InfluxBefore, [object]$InfluxAfter)
    $StoreRows.Add([pscustomobject]@{ case=$Case; store='PostgreSQL'; before=(Get-CountSum $PgBefore); after=(Get-CountSum $PgAfter) })
    $StoreRows.Add([pscustomobject]@{ case=$Case; store='RabbitMQ'; before=$RabbitBefore.total; after=$RabbitAfter.total })
    $StoreRows.Add([pscustomobject]@{ case=$Case; store='InfluxDB'; before=$InfluxBefore.total; after=$InfluxAfter.total; schema_tables_after=$InfluxAfter.schema_tables })
}

function Invoke-Case {
    param([string]$Name, [scriptblock]$Arrange, [scriptblock]$Act, [scriptblock]$Assert, [switch]$SeedRun)
    $caseRoot = Join-Path $ResultsRoot $Name
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    $operationId = ''
    $runId = ''
    $reset = $null
    $newRunStatus = ''
    try {
        if ($SeedRun) {
            $run = Start-SmallRun -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -Label "$Name-seed"
            $operationId = [string]$run.operationId
            $op = Wait-Operation -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -OperationId $operationId
            $runId = [string]$op.simulationRunId
            Wait-RabbitQuiescent -EvidencePath $caseRoot -TimeoutSeconds 60 | Out-Null
        }
        if ($Arrange) { & $Arrange $caseRoot }
        $pgBefore = Get-PostgresCounts -EvidencePath $caseRoot -Label 'before'
        $rabbitBefore = Get-RabbitCounts -EvidencePath $caseRoot -Label 'before'
        $influxBefore = Get-InfluxCounts -EvidencePath $caseRoot -Label 'before'
        $reset = & $Act $caseRoot
        $pgAfter = Get-PostgresCounts -EvidencePath $caseRoot -Label 'after'
        $rabbitAfter = Get-RabbitCounts -EvidencePath $caseRoot -Label 'after'
        $influxAfter = Get-InfluxCounts -EvidencePath $caseRoot -Label 'after'
        $ok = & $Assert $reset $pgBefore $pgAfter $rabbitBefore $rabbitAfter $influxBefore $influxAfter
        Add-StoreRows -Case $Name -PgBefore $pgBefore -PgAfter $pgAfter -RabbitBefore $rabbitBefore -RabbitAfter $rabbitAfter -InfluxBefore $influxBefore -InfluxAfter $influxAfter
        $resetId = if ($reset.resetId) { [string]$reset.resetId } else { '' }
        $status = if ($reset.status) { [string]$reset.status } else { '' }
        $ResetRows.Add([pscustomobject]@{ case=$Name; reset_id=$resetId; status=$status; message=([string]$reset.message); evidence_path=$caseRoot })
        $MatrixRows.Add([pscustomobject]@{
            case=$Name; reset_id=$resetId; operation_id=$operationId; simulation_run_id=$runId;
            postgres_before=(Get-CountSum $pgBefore); postgres_after=(Get-CountSum $pgAfter);
            rabbitmq_before=$rabbitBefore.total; rabbitmq_after=$rabbitAfter.total;
            influx_before=$influxBefore.total; influx_after=$influxAfter.total;
            reset_status=$status; new_run_status=$newRunStatus; result=($(if($ok){'PASS'}else{'FAIL'})); evidence_path=$caseRoot
        })
    } catch {
        $MatrixRows.Add([pscustomobject]@{ case=$Name; reset_id=''; operation_id=$operationId; simulation_run_id=$runId; postgres_before=''; postgres_after=''; rabbitmq_before=''; rabbitmq_after=''; influx_before=''; influx_after=''; reset_status='ERROR'; new_run_status=$newRunStatus; result="ERROR:$($_.Exception.Message)"; evidence_path=$caseRoot })
    }
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

$config = [ordered]@{ apiBaseUrl=$ApiBaseUrl; postgresDb=$PostgresDb; rabbitAmqpPort=$RabbitAmqpPort; rabbitManagementPort=$RabbitManagementPort; influxUrl=$InfluxUrl; influxBucket=$InfluxBucket; skipBuild=[bool]$SkipBuild }
$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'configuration.json') -Encoding UTF8

Ensure-InfluxDatabase

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

$BaseEnvironment = @{
    ASPNETCORE_ENVIRONMENT='Development'; DOTNET_ENVIRONMENT='Development';
    POSTGRES_HOST='localhost'; POSTGRES_PORT=[string]$PostgresPort; POSTGRES_DB=$PostgresDb; POSTGRES_USER=$PostgresUser; POSTGRES_PASSWORD=$PostgresPassword;
    RabbitMq__HostName='localhost'; RabbitMq__Port=[string]$RabbitAmqpPort; RabbitMq__UserName=$RabbitUser; RabbitMq__Password=$RabbitPassword;
    RabbitMq__ManagementScheme='http'; RabbitMq__ManagementPort=[string]$RabbitManagementPort; RabbitMq__ManagementAllowInsecureHttp='true';
    InfluxDb__Enabled='true'; InfluxDb__Url=$InfluxUrl; InfluxDb__Token=$InfluxToken; InfluxDb__Bucket=$InfluxBucket;
    NP_BOOTSTRAP_ADMIN_USERNAME=$AdminUsername; NP_BOOTSTRAP_ADMIN_PASSWORD=$AdminPassword
}

try {
    $PrimaryApi = Start-Api -Port ([uri]$ApiBaseUrl).Port
    $PrimaryToken = Login -BaseUrl $PrimaryApi.BaseUrl
    $script:Prevention = Start-Prevention

    $initialCleanup = Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $OutputRoot -Label 'initial-cleanup'
    if ($initialCleanup.status -ne 'Completed') {
        throw "Initial cleanup did not complete: status=$($initialCleanup.status) message=$($initialCleanup.message)"
    }

    Invoke-Case -Name '01-reset-nominal-three-stores' -SeedRun -Act { param($p) Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'nominal' } -Assert {
        param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Completed' -and (Get-CountSum $pga) -eq 0 -and $rba.total -eq 0 -and $ifa.total -eq 0 -and $ifa.schema_tables -ge 2
    }

    Invoke-Case -Name '02-reset-rejects-active-run' -Act {
        param($p)
        $run = Start-SmallRun -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -Label 'active-run' -Cycles 20 -Wait $false
        $script:LastActiveOperation = [string]$run.operationId
        $res = Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'active-run'
        Wait-Operation -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -OperationId $script:LastActiveOperation -TimeoutSeconds 180 | Out-Null
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'active-run-cleanup' | Out-Null
        $res
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Rejected' -and $reset.message -like '*active*' }

    Invoke-Case -Name '03-reset-rejects-nonterminal-operation' -Arrange { param($p) $script:FixtureOperation = New-RuntimeOperationFixture } -Act {
        param($p)
        $res = Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'nonterminal-operation'
        Invoke-PsqlCsv -Sql "DELETE FROM control.runtime_orchestrator_executions WHERE execution_id='$script:FixtureOperation'::uuid;" -OutputPath (Join-Path $p 'cleanup-operation.csv') | Out-Null
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'nonterminal-operation-cleanup' | Out-Null
        $res
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Rejected' -and $reset.message -like '*active operations*' }

    Invoke-Case -Name '04-reset-rejects-active-inbox' -Arrange { param($p) $script:InboxFixture = New-InboxFixture -Status 0 } -Act {
        param($p)
        $res = Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'active-inbox'
        Invoke-PsqlCsv -Sql "DELETE FROM pipeline.event_inbox WHERE ""Id""='$script:InboxFixture'::uuid;" -OutputPath (Join-Path $p 'cleanup-inbox.csv') | Out-Null
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'active-inbox-cleanup' | Out-Null
        $res
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Rejected' -and $reset.message -like '*pending/processing/retry inbox*' }

    Invoke-Case -Name '05-reset-rejects-rabbitmq-unacknowledged' -Arrange {
        param($p)
        Stop-StartedProcess -Process $script:Prevention
        Publish-RabbitMessage
        $script:UnackedProcess = Start-UnackedConsumer
        Wait-RabbitUnacknowledged -EvidencePath $p | Out-Null
    } -Act {
        param($p)
        $res = Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'rabbit-unacked'
        Stop-StartedProcess -Process $script:UnackedProcess
        Wait-RabbitQuiescent -EvidencePath $p | Out-Null
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'rabbit-unacked-cleanup' | Out-Null
        $script:Prevention = Start-Prevention
        $res
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -in @('Rejected','Failed') -and (Get-CountSum $pga) -eq (Get-CountSum $pgb) -and $rbb.unacknowledged -gt 0 }

    Invoke-Case -Name '06-reset-after-elevated-data-volume' -Act {
        param($p)
        for ($i=0; $i -lt 5; $i++) {
            $volumeRun = Start-SmallRun -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -Label "volume-$i" -Cycles 3
            Wait-Operation -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -OperationId ([string]$volumeRun.operationId) | Out-Null
        }
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'volume'
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Completed' -and (Get-CountSum $pga) -eq 0 -and $ifa.total -eq 0 }

    Invoke-Case -Name '07-reset-concurrent-with-admission-two-api' -Act {
        param($p)
        $SecondaryApi = Start-Api -Port 5255
        $SecondaryToken = Login -BaseUrl $SecondaryApi.BaseUrl
        $job = Start-Job -ScriptBlock {
            param($base,$token)
            $body = @{ areaCode='proenca-a-nova'; scenarioCode='scenario_b'; sensorCount=2; numberOfCycles=2; intervalSeconds=1; seed=42; waitForCompletion=$false; timeoutSeconds=60; allowParallelRun=$false; runLabel='reset-concurrent-admission' }
            Invoke-RestMethod -Method POST -Uri "$base/api/control/runtime/runs" -Headers @{Authorization="Bearer $token"} -ContentType 'application/json' -Body ($body|ConvertTo-Json)
        } -ArgumentList $SecondaryApi.BaseUrl,$SecondaryToken
        $res = Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'concurrent-admission'
        $jobResult = Receive-Job -Job $job -Wait -AutoRemoveJob -ErrorAction SilentlyContinue
        $jobResult | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $p 'concurrent-admission-result.json') -Encoding UTF8
        if ($jobResult -and $jobResult.PSObject.Properties.Name -contains 'operationId' -and -not [string]::IsNullOrWhiteSpace([string]$jobResult.operationId)) {
            Wait-Operation -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -OperationId ([string]$jobResult.operationId) | Out-Null
        }
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'concurrent-admission-cleanup' | Out-Null
        $res
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -in @('Completed','Rejected') }

    Invoke-Case -Name '08-rabbitmq-failure-preserves-postgres' -SeedRun -Act {
        param($p)
        $BadApi = Start-Api -Port 5256 -Overrides @{ RabbitMq__ManagementPort='15999' }
        $BadToken = Login -BaseUrl $BadApi.BaseUrl
        Invoke-Reset -BaseUrl $BadApi.BaseUrl -Token $BadToken -EvidencePath $p -Label 'rabbit-failure'
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Rejected' -and (Get-CountSum $pga) -eq (Get-CountSum $pgb) }

    Wait-RabbitQuiescent -EvidencePath $OutputRoot | Out-Null
    Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $OutputRoot -Label 'after-rabbit-failure-cleanup' | Out-Null

    Invoke-Case -Name '09-influxdb-failure-preserves-postgres' -SeedRun -Act {
        param($p)
        $BadApi = Start-Api -Port 5257 -Overrides @{ InfluxDb__Url='http://localhost:8199' }
        $BadToken = Login -BaseUrl $BadApi.BaseUrl
        Invoke-Reset -BaseUrl $BadApi.BaseUrl -Token $BadToken -EvidencePath $p -Label 'influx-failure'
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $reset.status -eq 'Rejected' -and (Get-CountSum $pga) -eq (Get-CountSum $pgb) }

    Wait-RabbitQuiescent -EvidencePath $OutputRoot | Out-Null
    Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $OutputRoot -Label 'after-influx-failure-cleanup' | Out-Null

    Invoke-Case -Name '10-new-run-after-complete-reset' -Act {
        param($p)
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'pre-new-run' | Out-Null
        $run = Start-SmallRun -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -Label 'after-reset' -Cycles 2
        $op = Wait-Operation -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -OperationId ([string]$run.operationId)
        $script:NewRunTerminal = [string]$op.terminalOutcome
        $run | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $p 'new-run-start.json') -Encoding UTF8
        $op | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $p 'new-run-operation.json') -Encoding UTF8
        Wait-RabbitQuiescent -EvidencePath $p -TimeoutSeconds 60 | Out-Null
        Invoke-Reset -BaseUrl $PrimaryApi.BaseUrl -Token $PrimaryToken -EvidencePath $p -Label 'post-new-run-cleanup'
    } -Assert { param($reset,$pgb,$pga,$rbb,$rba,$ifb,$ifa) $script:NewRunTerminal -eq 'SystemCompleted' -and $reset.status -eq 'Completed' }
}
finally {
    foreach ($process in @($StartedProcesses)) { Stop-StartedProcess -Process $process }
}

$MatrixRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'RESET_RECOVERY_MATRIX.csv')
$StoreRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'STORE_COUNTS_BEFORE_AFTER.csv')
$ResetRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'RESET_OPERATIONS.csv')
$Commands | Set-Content -LiteralPath (Join-Path $LogsRoot 'commands.txt') -Encoding UTF8

$pass = @($MatrixRows | Where-Object { $_.result -eq 'PASS' }).Count -eq 10
$status = if ($pass) { 'SYSTEM_RESET_AND_RECOVERY_PASS' } else { 'BLOCKED' }
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# System Reset Recovery Runtime Matrix')
$lines.Add('')
$lines.Add("Status: $status")
$lines.Add("Rows: $($MatrixRows.Count)")
$lines.Add("EvidenceRoot: $OutputRoot")
$lines.Add('')
$lines.Add('## Matrix')
foreach ($row in $MatrixRows) {
    $lines.Add("- case=$($row.case) result=$($row.result) reset=$($row.reset_status) resetId=$($row.reset_id)")
}
$lines | Set-Content -LiteralPath (Join-Path $OutputRoot 'RESET_RECOVERY_RESULTS.md') -Encoding UTF8

Get-ChildItem -LiteralPath $OutputRoot -Recurse -File |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName |
    ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), $_.FullName.Substring($OutputRoot.Length + 1).Replace('\','/')
    } | Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding UTF8

if (-not $pass) { exit 1 }
