param(
    [ValidateSet("Calibration", "B0", "B1", "B2")]
    [string]$Profile = "Calibration",
    [string]$ApiBaseUrl = "http://localhost:5254",
    [string]$AreaCode = "proenca-a-nova",
    [string]$ScenarioCode = "scenario_b",
    [string]$OutputRoot = "artifacts/performance",
    [string]$AuthToken = $env:NP_PERFORMANCE_AUTH_TOKEN,
    [string]$Username = $env:NP_PERFORMANCE_USERNAME,
    [string]$Password = $env:NP_PERFORMANCE_PASSWORD,
    [switch]$UseDevelopmentAdminDefault,
    [string]$CalibrationRunDirectory,
    [int]$SensorCount = 0,
    [int]$NumberOfCycles = 0,
    [int]$IntervalSeconds = 0,
    [int]$Repetitions = 0,
    [int]$TimeoutSeconds = 0,
    [int]$ObservationWaitSeconds = 0,
    [int]$BacklogDrainWaitSeconds = 0,
    [switch]$CollectRuntimeProcessEvidence,
    [switch]$AllowParallelRun,
    [switch]$DryRun
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Add-Type -AssemblyName System.Net.Http

$jsonDepth = 50
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
$runId = "system-$Profile-$timestamp"

if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $resolvedOutputRoot = $OutputRoot
}
else {
    $resolvedOutputRoot = Join-Path $repoRoot $OutputRoot
}

$runDirectory = Join-Path $resolvedOutputRoot $runId
$logsDirectory = Join-Path $runDirectory "logs"
$tracesDirectory = Join-Path $runDirectory "traces"
$metricsDirectory = Join-Path $runDirectory "metrics"
$runsDirectory = Join-Path $runDirectory "runs"
New-Item -ItemType Directory -Force -Path $logsDirectory, $tracesDirectory, $metricsDirectory, $runsDirectory | Out-Null

function Get-ProfileSpec {
    param([string]$Name)

    switch ($Name) {
        "Calibration" {
            return [ordered]@{
                profile = "Calibration"
                sensorCount = 1
                numberOfCycles = 1
                intervalSeconds = 1
                repetitions = 1
                timeoutSeconds = 120
                observationWaitSeconds = 30
                backlogDrainWaitSeconds = 30
                purpose = "Short real-pipeline calibration before selecting B0/B1/B2 volumes."
            }
        }
        "B0" {
            return [ordered]@{
                profile = "B0"
                sensorCount = 2
                numberOfCycles = 2
                intervalSeconds = 1
                repetitions = 2
                timeoutSeconds = 180
                observationWaitSeconds = 45
                backlogDrainWaitSeconds = 60
                purpose = "Repeatable local system smoke after a successful calibration run."
            }
        }
        "B1" {
            return [ordered]@{
                profile = "B1"
                sensorCount = 6
                numberOfCycles = 5
                intervalSeconds = 1
                repetitions = 2
                timeoutSeconds = 300
                observationWaitSeconds = 90
                backlogDrainWaitSeconds = 120
                purpose = "Bounded engineering workload for local comparison."
            }
        }
        "B2" {
            return [ordered]@{
                profile = "B2"
                sensorCount = 6
                numberOfCycles = 10
                intervalSeconds = 1
                repetitions = 1
                timeoutSeconds = 600
                observationWaitSeconds = 180
                backlogDrainWaitSeconds = 180
                purpose = "Bounded deeper local workload; not a stress test or production SLO."
            }
        }
    }
}

function Invoke-ApiJson {
    param(
        [ValidateSet("GET", "POST")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = $null,
        [int[]]$ExpectedStatusCodes = @(200)
    )

    $uri = $ApiBaseUrl.TrimEnd("/") + $Path
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }

    try {
        $parameters = @{
            Method = $Method
            Uri = $uri
            Headers = $headers
            UseBasicParsing = $true
            TimeoutSec = 60
            ErrorAction = "Stop"
        }

        if ($null -ne $Body) {
            $parameters.ContentType = "application/json"
            $parameters.Body = ($Body | ConvertTo-Json -Depth $jsonDepth)
        }

        $response = Invoke-WebRequest @parameters
        $statusCode = [int]$response.StatusCode
        if ($ExpectedStatusCodes -notcontains $statusCode) {
            throw "$Method $uri returned HTTP $statusCode; expected $($ExpectedStatusCodes -join ', ')."
        }

        $content = [string]$response.Content
        $json = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
        return [pscustomobject]@{
            StatusCode = $statusCode
            Json = $json
            Raw = $content
            Uri = $uri
        }
    }
    catch {
        $statusCode = $null
        $content = ""
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = [System.IO.StreamReader]::new($stream)
                    $content = $reader.ReadToEnd()
                }
            }
            catch {
                $content = ""
            }
        }
        elseif ($_.Exception.Message -match '\((\d{3})\)') {
            $statusCode = [int]$Matches[1]
        }

        if ($statusCode -and ($ExpectedStatusCodes -contains $statusCode)) {
            $json = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
            return [pscustomobject]@{
                StatusCode = $statusCode
                Json = $json
                Raw = $content
                Uri = $uri
            }
        }

        throw
    }
}

function Get-AuthToken {
    if (-not [string]::IsNullOrWhiteSpace($AuthToken)) {
        return $AuthToken
    }

    if ($UseDevelopmentAdminDefault) {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            $script:Username = "admin"
        }

        if ([string]::IsNullOrWhiteSpace($Password)) {
            $script:Password = "admin123"
        }
    }

    if ([string]::IsNullOrWhiteSpace($Username) -or [string]::IsNullOrWhiteSpace($Password)) {
        throw "A bearer token or username/password is required. Set NP_PERFORMANCE_AUTH_TOKEN, NP_PERFORMANCE_USERNAME/NP_PERFORMANCE_PASSWORD, or pass -UseDevelopmentAdminDefault in Development only."
    }

    $login = Invoke-ApiJson `
        -Method "POST" `
        -Path "/api/users-roles/login" `
        -Body @{ usernameOrEmail = $Username; password = $Password } `
        -ExpectedStatusCodes @(200)

    if ($null -eq $login.Json -or [string]::IsNullOrWhiteSpace([string]$login.Json.token)) {
        throw "Login succeeded but no bearer token was returned."
    }

    return [string]$login.Json.token
}

function Get-NatureProtectorProcessSnapshot {
    $escapedRoot = [regex]::Escape($repoRoot)
    $rows = @()
    foreach ($process in (Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and
        $_.CommandLine -match $escapedRoot -and
        $_.CommandLine -match "NatureProtector" -and
        ($_.Name -in @("dotnet.exe", "node.exe", "npm.exe", "powershell.exe", "pwsh.exe"))
    })) {
        try {
            $runtimeProcess = Get-Process -Id $process.ProcessId -ErrorAction Stop
            $rows += [pscustomobject]@{
                processId = $process.ProcessId
                name = $process.Name
                workingSetBytes = $runtimeProcess.WorkingSet64
                cpuSeconds = $runtimeProcess.CPU
                threadCount = $runtimeProcess.Threads.Count
                commandLine = $process.CommandLine
            }
        }
        catch {
            $rows += [pscustomobject]@{
                processId = $process.ProcessId
                name = $process.Name
                workingSetBytes = $null
                cpuSeconds = $null
                threadCount = $null
                commandLine = $process.CommandLine
            }
        }
    }

    return @($rows)
}

function Save-DockerStats {
    param([string]$Name)

    $path = Join-Path $metricsDirectory "docker-stats-$Name.jsonl"
    try {
        $output = & docker stats --no-stream --format "{{json .}}" 2>$null
        if ($LASTEXITCODE -eq 0 -and $null -ne $output) {
            $output | Set-Content -Path $path -Encoding UTF8
            return $path
        }
    }
    catch {
    }

    "Docker stats unavailable." | Set-Content -Path $path -Encoding UTF8
    return $path
}

function Get-QueueTotals {
    param(
        [object]$RabbitMqMetrics,
        [string[]]$QueueNames = @("np.ingestion.readings")
    )

    $ready = 0
    $unacknowledged = 0
    $total = 0
    $consumers = 0
    $measuredQueues = 0

    if ($null -ne $RabbitMqMetrics -and $null -ne $RabbitMqMetrics.queues) {
        foreach ($queue in @($RabbitMqMetrics.queues)) {
            $queueName = [string]$queue.queueName
            if ($QueueNames.Count -gt 0 -and $QueueNames -notcontains $queueName) {
                continue
            }

            if ("$($queue.collectionStatus)" -eq "Measured") {
                $measuredQueues++
            }

            if ($null -ne $queue.messagesReady) { $ready += [int]$queue.messagesReady }
            if ($null -ne $queue.messagesUnacknowledged) { $unacknowledged += [int]$queue.messagesUnacknowledged }
            if ($null -ne $queue.messagesTotal) { $total += [int]$queue.messagesTotal }
            if ($null -ne $queue.consumers) { $consumers += [int]$queue.consumers }
        }
    }

    return [pscustomobject]@{
        ready = $ready
        unacknowledged = $unacknowledged
        total = $total
        consumers = $consumers
        measuredQueues = $measuredQueues
    }
}

function Test-RunEvidenceComplete {
    param(
        [object]$Audit,
        [object]$Timings,
        [int]$ExpectedEvents
    )

    if ($null -eq $Audit -or $null -eq $Timings) {
        return $false
    }

    $acceptedReadings = if ($null -ne $Audit.acceptedReadings) { [int]$Audit.acceptedReadings } else { 0 }
    $riskAssessments = if ($null -ne $Audit.riskAssessments) { [int]$Audit.riskAssessments } else { 0 }
    $attemptCount = if ($null -ne $Timings.attempts -and $null -ne $Timings.attempts.attemptCount) { [int]$Timings.attempts.attemptCount } else { 0 }

    return $acceptedReadings -ge $ExpectedEvents -and
        $riskAssessments -ge $ExpectedEvents -and
        $attemptCount -ge $ExpectedEvents
}

function Wait-RunEvidence {
    param(
        [string]$SimulationRunId,
        [string]$Token,
        [int]$ExpectedEvents,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $audit = $null
    $timings = $null
    $lastError = ""

    do {
        try {
            $audit = (Invoke-ApiJson -Method "GET" -Path "/api/control/runtime/runs/$SimulationRunId/audit" -Token $Token).Json
            $timings = (Invoke-ApiJson -Method "GET" -Path "/api/control/runtime/runs/$SimulationRunId/timings" -Token $Token).Json
            if (Test-RunEvidenceComplete -Audit $audit -Timings $timings -ExpectedEvents $ExpectedEvents) {
                return [pscustomobject]@{
                    audit = $audit
                    timings = $timings
                    complete = $true
                    lastError = ""
                }
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    return [pscustomobject]@{
        audit = $audit
        timings = $timings
        complete = $false
        lastError = $lastError
    }
}

function Wait-QueueDrain {
    param(
        [string]$Token,
        [int]$TimeoutSeconds
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $rabbit = $null
    $totals = $null
    $lastError = ""

    do {
        try {
            $rabbit = (Invoke-ApiJson -Method "GET" -Path "/api/control/runtime/observability/rabbitmq" -Token $Token).Json
            $totals = Get-QueueTotals -RabbitMqMetrics $rabbit
            if ($totals.total -eq 0) {
                $stopwatch.Stop()
                return [pscustomobject]@{
                    rabbit = $rabbit
                    totals = $totals
                    drained = $true
                    elapsedMs = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
                    lastError = ""
                }
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    $stopwatch.Stop()
    if ($null -eq $totals) {
        $totals = [pscustomobject]@{
            ready = 0
            unacknowledged = 0
            total = 0
            consumers = 0
            measuredQueues = 0
        }
    }

    return [pscustomobject]@{
        rabbit = $rabbit
        totals = $totals
        drained = $false
        elapsedMs = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
        lastError = $lastError
    }
}

function Save-ApiSnapshot {
    param(
        [string]$Name,
        [string]$Token
    )

    $snapshot = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        summary = $null
        rabbitmq = $null
        processes = Get-NatureProtectorProcessSnapshot
        dockerStatsPath = Save-DockerStats -Name $Name
        limitations = @()
    }

    try {
        $snapshot.summary = (Invoke-ApiJson -Method "GET" -Path "/api/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30" -Token $Token).Json
    }
    catch {
        $snapshot.limitations += "Runtime summary unavailable: $($_.Exception.Message)"
    }

    try {
        $snapshot.rabbitmq = (Invoke-ApiJson -Method "GET" -Path "/api/control/runtime/observability/rabbitmq" -Token $Token).Json
    }
    catch {
        $snapshot.limitations += "RabbitMQ observability unavailable: $($_.Exception.Message)"
    }

    $path = Join-Path $metricsDirectory "$Name.json"
    Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path $path -Value $snapshot
    return $snapshot
}

$profileSpec = Get-ProfileSpec -Name $Profile
if ($SensorCount -gt 0) { $profileSpec.sensorCount = $SensorCount }
if ($NumberOfCycles -gt 0) { $profileSpec.numberOfCycles = $NumberOfCycles }
if ($IntervalSeconds -gt 0) { $profileSpec.intervalSeconds = $IntervalSeconds }
if ($Repetitions -gt 0) { $profileSpec.repetitions = $Repetitions }
if ($TimeoutSeconds -gt 0) { $profileSpec.timeoutSeconds = $TimeoutSeconds }
if ($ObservationWaitSeconds -gt 0) { $profileSpec.observationWaitSeconds = $ObservationWaitSeconds }
if ($BacklogDrainWaitSeconds -gt 0) { $profileSpec.backlogDrainWaitSeconds = $BacklogDrainWaitSeconds }

$effectiveObservationWaitSeconds = [int]$profileSpec.observationWaitSeconds
$effectiveBacklogDrainWaitSeconds = [int]$profileSpec.backlogDrainWaitSeconds

$calibrationEvidence = [ordered]@{
    required = $Profile -ne "Calibration"
    path = $CalibrationRunDirectory
    present = $false
    summaryPath = $null
}

if ($Profile -ne "Calibration" -and -not [string]::IsNullOrWhiteSpace($CalibrationRunDirectory)) {
    $summaryPath = Join-Path $CalibrationRunDirectory "summary.json"
    $calibrationEvidence.summaryPath = $summaryPath
    $calibrationEvidence.present = Test-Path -LiteralPath $summaryPath
}

$expectedEventsPerRun = [int]$profileSpec.sensorCount * [int]$profileSpec.numberOfCycles
$workload = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    runId = $runId
    profile = $Profile
    apiBaseUrl = $ApiBaseUrl
    areaCode = $AreaCode
    scenarioCode = $ScenarioCode
    sensorCount = [int]$profileSpec.sensorCount
    numberOfCycles = [int]$profileSpec.numberOfCycles
    intervalSeconds = [int]$profileSpec.intervalSeconds
    repetitions = [int]$profileSpec.repetitions
    timeoutSeconds = [int]$profileSpec.timeoutSeconds
    observationWaitSeconds = $effectiveObservationWaitSeconds
    backlogDrainWaitSeconds = $effectiveBacklogDrainWaitSeconds
    expectedEventsPerRun = $expectedEventsPerRun
    rabbitMqQueueDepthFilter = @("np.ingestion.readings")
    collectRuntimeProcessEvidence = [bool]$CollectRuntimeProcessEvidence
    allowParallelRun = [bool]$AllowParallelRun
    purpose = $profileSpec.purpose
    calibrationEvidence = $calibrationEvidence
    classification = "Local reproducible capacity baseline input; not production readiness, not stress testing, not scientific validation."
}

$environment = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot = $repoRoot
    machineName = $env:COMPUTERNAME
    osVersion = [System.Environment]::OSVersion.VersionString
    dotnet = Get-NpCommandLineVersion -Command "dotnet" -Arguments @("--version")
    node = Get-NpCommandLineVersion -Command "node" -Arguments @("--version")
    npm = Get-NpCommandLineVersion -Command "npm" -Arguments @("--version")
    dockerClient = Get-NpCommandLineVersion -Command "docker" -Arguments @("version", "--format", "{{.Client.Version}}")
    dockerServer = Get-NpCommandLineVersion -Command "docker" -Arguments @("version", "--format", "{{.Server.Version}}")
    authentication = if (-not [string]::IsNullOrWhiteSpace($AuthToken)) { "bearer-token" } elseif ($UseDevelopmentAdminDefault) { "development-admin-default" } else { "username-password" }
    secretsPrinted = $false
}

Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "environment.json") -Value $environment
Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "workload.json") -Value $workload

if ($DryRun) {
    $summary = [ordered]@{
        status = "DryRun"
        runId = $runId
        outputDirectory = $runDirectory
        httpCallsExecuted = 0
        processStarts = 0
        expectedEventsPerRun = $expectedEventsPerRun
        workload = $workload
        limitations = @("Dry-run only validates parameters, artifact layout and workload profile resolution.")
    }
    Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "summary.json") -Value $summary
    Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "run-failures.json") -Value @()
    @(
        "# System capacity workload dry run",
        "",
        "- RunId: $runId",
        "- Profile: $Profile",
        "- OutputDirectory: $runDirectory",
        "- Expected events per run: $expectedEventsPerRun",
        "- HTTP calls executed: 0",
        "",
        "Dry-run mode validates parameters and artifact layout only."
    ) | Set-Content -Path (Join-Path $runDirectory "summary.md") -Encoding UTF8
    Write-Host "Dry run complete. Output: $runDirectory"
    exit 0
}

if ($Profile -ne "Calibration" -and -not $calibrationEvidence.present) {
    throw "Profile $Profile requires a previous calibration summary. Run -Profile Calibration first and pass -CalibrationRunDirectory <path>."
}

$token = Get-AuthToken
$measurements = @()
$runFailures = @()

$snapshotBefore = Save-ApiSnapshot -Name "before" -Token $token

for ($iteration = 1; $iteration -le [int]$profileSpec.repetitions; $iteration++) {
    $iterationLabel = "{0}-r{1:00}" -f $runId, $iteration
    $request = @{
        areaCode = $AreaCode
        scenarioCode = $ScenarioCode
        sensorCount = [int]$profileSpec.sensorCount
        numberOfCycles = [int]$profileSpec.numberOfCycles
        intervalSeconds = [int]$profileSpec.intervalSeconds
        seed = 7300 + $iteration
        degradationProfile = "none"
        degradationProfiles = @("none")
        collectEvidence = [bool]$CollectRuntimeProcessEvidence
        waitForCompletion = $true
        timeoutSeconds = [int]$profileSpec.timeoutSeconds
        allowParallelRun = [bool]$AllowParallelRun
        runLabel = $iterationLabel
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "Unknown"
    $runIdFromApi = $null
    $audit = $null
    $timings = $null
    $rabbitAfter = $null
    $queueDrain = $null
    $errorMessage = ""

    try {
        $runResponse = Invoke-ApiJson -Method "POST" -Path "/api/control/runtime/runs" -Body $request -Token $token -ExpectedStatusCodes @(200)
        Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runsDirectory "$iterationLabel-response.json") -Value $runResponse.Json
        $status = [string]$runResponse.Json.status
        if ($null -ne $runResponse.Json.run) {
            $runIdFromApi = [string]$runResponse.Json.run.id
            $status = [string]$runResponse.Json.run.status
        }

        if (-not [string]::IsNullOrWhiteSpace($runIdFromApi)) {
            $evidence = Wait-RunEvidence `
                -SimulationRunId $runIdFromApi `
                -Token $token `
                -ExpectedEvents $expectedEventsPerRun `
                -TimeoutSeconds $effectiveObservationWaitSeconds
            $audit = $evidence.audit
            $timings = $evidence.timings
            Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runsDirectory "$iterationLabel-audit.json") -Value $audit
            Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runsDirectory "$iterationLabel-timings.json") -Value $timings
            if (-not $evidence.complete) {
                if ([string]::IsNullOrWhiteSpace($evidence.lastError)) {
                    $errorMessage = "Timed out waiting $effectiveObservationWaitSeconds seconds for persisted run evidence to reach $expectedEventsPerRun expected event(s)."
                }
                else {
                    $errorMessage = "Timed out waiting $effectiveObservationWaitSeconds seconds for persisted run evidence. Last error: $($evidence.lastError)"
                }
            }
        }

        $queueDrain = Wait-QueueDrain -Token $token -TimeoutSeconds $effectiveBacklogDrainWaitSeconds
        $rabbitAfter = $queueDrain.rabbit
        if (-not $queueDrain.drained) {
            if ([string]::IsNullOrWhiteSpace($queueDrain.lastError)) {
                $errorMessage = "Timed out waiting $effectiveBacklogDrainWaitSeconds seconds for np.ingestion.readings to drain."
            }
            else {
                $errorMessage = "Timed out waiting $effectiveBacklogDrainWaitSeconds seconds for np.ingestion.readings to drain. Last error: $($queueDrain.lastError)"
            }
        }
    }
    catch {
        $errorMessage = $_.Exception.Message
        $runFailures += [pscustomobject]@{
            iteration = $iteration
            runLabel = $iterationLabel
            error = $errorMessage
        }
    }
    finally {
        $stopwatch.Stop()
    }

    if (-not [string]::IsNullOrWhiteSpace($errorMessage) -and
        -not ($runFailures | Where-Object { $_.iteration -eq $iteration })) {
        $runFailures += [pscustomobject]@{
            iteration = $iteration
            runLabel = $iterationLabel
            error = $errorMessage
        }
    }

    $queueTotals = if ($null -ne $queueDrain -and $null -ne $queueDrain.totals) {
        $queueDrain.totals
    }
    else {
        Get-QueueTotals -RabbitMqMetrics $rabbitAfter
    }
    $acceptedReadings = if ($null -ne $audit -and $null -ne $audit.acceptedReadings) { [int]$audit.acceptedReadings } else { $null }
    $riskAssessments = if ($null -ne $audit -and $null -ne $audit.riskAssessments) { [int]$audit.riskAssessments } else { $null }
    $missingEvents = if ($null -ne $audit -and $null -ne $audit.missingEvents) { [int]$audit.missingEvents } else { $null }
    $rejected = if ($null -ne $audit -and $null -ne $audit.rejected) { [int]$audit.rejected } else { $null }
    $quarantined = if ($null -ne $audit -and $null -ne $audit.quarantined) { [int]$audit.quarantined } else { $null }
    $lostEvents = if ($null -ne $acceptedReadings -and $null -ne $missingEvents) { $expectedEventsPerRun - $acceptedReadings - $missingEvents } else { $null }

    $measurements += [pscustomobject]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        profile = $Profile
        iteration = $iteration
        runLabel = $iterationLabel
        simulationRunId = $runIdFromApi
        status = $status
        elapsedMs = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
        expectedEvents = $expectedEventsPerRun
        acceptedReadings = $acceptedReadings
        riskAssessments = $riskAssessments
        missingEvents = $missingEvents
        rejected = $rejected
        quarantined = $quarantined
        lostEvents = $lostEvents
        timeToFirstInboxMs = if ($null -ne $timings) { $timings.timeToFirstInboxMs } else { $null }
        timeToFirstProcessingAttemptMs = if ($null -ne $timings) { $timings.timeToFirstProcessingAttemptMs } else { $null }
        timeToFirstRiskAssessmentMs = if ($null -ne $timings) { $timings.timeToFirstRiskAssessmentMs } else { $null }
        attemptCount = if ($null -ne $timings -and $null -ne $timings.attempts) { $timings.attempts.attemptCount } else { $null }
        successfulAttempts = if ($null -ne $timings -and $null -ne $timings.attempts) { $timings.attempts.successfulAttempts } else { $null }
        failedAttempts = if ($null -ne $timings -and $null -ne $timings.attempts) { $timings.attempts.failedAttempts } else { $null }
        quarantinedAttempts = if ($null -ne $timings -and $null -ne $timings.attempts) { $timings.attempts.quarantinedAttempts } else { $null }
        backlogDrained = if ($null -ne $queueDrain) { [bool]$queueDrain.drained } else { $null }
        backlogDrainTimeMs = if ($null -ne $queueDrain) { $queueDrain.elapsedMs } else { $null }
        queueReadyAfter = $queueTotals.ready
        queueUnacknowledgedAfter = $queueTotals.unacknowledged
        queueTotalAfter = $queueTotals.total
        queueConsumersAfter = $queueTotals.consumers
        error = $errorMessage
    }

    Save-ApiSnapshot -Name ("after-r{0:00}" -f $iteration) -Token $token | Out-Null
}

$snapshotAfter = Save-ApiSnapshot -Name "after" -Token $token

$measurements | Export-Csv -Path (Join-Path $runDirectory "measurements.csv") -NoTypeInformation -Encoding UTF8
Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "measurements.json") -Value $measurements
Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "run-failures.json") -Value $runFailures

$elapsedValues = @($measurements | ForEach-Object { [double]$_.elapsedMs })
$drainValues = @($measurements | Where-Object { $null -ne $_.backlogDrainTimeMs } | ForEach-Object { [double]$_.backlogDrainTimeMs })
$queueAfterValues = @($measurements | ForEach-Object { [double]$_.queueTotalAfter })
$successRows = @($measurements | Where-Object { $_.status -eq "Completed" -and [string]::IsNullOrWhiteSpace($_.error) })
$failureRows = @($measurements | Where-Object { $_.status -ne "Completed" -or -not [string]::IsNullOrWhiteSpace($_.error) })
$finalQueue = Get-QueueTotals -RabbitMqMetrics $snapshotAfter.rabbitmq

$summary = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    status = if ($failureRows.Count -eq 0) { "Completed" } else { "CompletedWithFailures" }
    runId = $runId
    outputDirectory = $runDirectory
    profile = $Profile
    repetitions = [int]$profileSpec.repetitions
    successfulRuns = $successRows.Count
    failedRuns = $failureRows.Count
    expectedEventsTotal = $expectedEventsPerRun * [int]$profileSpec.repetitions
    acceptedReadingsTotal = @($measurements | Where-Object { $null -ne $_.acceptedReadings } | Measure-Object -Property acceptedReadings -Sum).Sum
    riskAssessmentsTotal = @($measurements | Where-Object { $null -ne $_.riskAssessments } | Measure-Object -Property riskAssessments -Sum).Sum
    rejectedTotal = @($measurements | Where-Object { $null -ne $_.rejected } | Measure-Object -Property rejected -Sum).Sum
    quarantinedTotal = @($measurements | Where-Object { $null -ne $_.quarantined } | Measure-Object -Property quarantined -Sum).Sum
    lostEventsTotal = @($measurements | Where-Object { $null -ne $_.lostEvents } | Measure-Object -Property lostEvents -Sum).Sum
    elapsedMs = [ordered]@{
        min = if ($elapsedValues.Count -gt 0) { [Math]::Round(($elapsedValues | Measure-Object -Minimum).Minimum, 2) } else { $null }
        p50 = Get-NpPercentileNearestRank -Values $elapsedValues -Percentile 50
        p95 = Get-NpPercentileNearestRank -Values $elapsedValues -Percentile 95
        p99 = Get-NpPercentileNearestRank -Values $elapsedValues -Percentile 99
        max = if ($elapsedValues.Count -gt 0) { [Math]::Round(($elapsedValues | Measure-Object -Maximum).Maximum, 2) } else { $null }
    }
    backlogDrainMs = [ordered]@{
        min = if ($drainValues.Count -gt 0) { [Math]::Round(($drainValues | Measure-Object -Minimum).Minimum, 2) } else { $null }
        p50 = Get-NpPercentileNearestRank -Values $drainValues -Percentile 50
        p95 = Get-NpPercentileNearestRank -Values $drainValues -Percentile 95
        p99 = Get-NpPercentileNearestRank -Values $drainValues -Percentile 99
        max = if ($drainValues.Count -gt 0) { [Math]::Round(($drainValues | Measure-Object -Maximum).Maximum, 2) } else { $null }
    }
    queueTotalAfter = [ordered]@{
        maxObservedAfterRun = if ($queueAfterValues.Count -gt 0) { [Math]::Round(($queueAfterValues | Measure-Object -Maximum).Maximum, 2) } else { $null }
        final = $finalQueue.total
        finalReady = $finalQueue.ready
        finalUnacknowledged = $finalQueue.unacknowledged
        finalConsumers = $finalQueue.consumers
    }
    limitations = @(
        "This workload uses existing runtime API endpoints and persisted audit/timing projections.",
        "PublishedAt is not persisted in the RabbitMQ envelope, so full publish-to-end latency is not claimed.",
        "p50/p95/p99 are calculated across completed run request durations, not per-event latency.",
        "Queue totals in summary are filtered to np.ingestion.readings; full RabbitMQ queue snapshots are retained under metrics/.",
        "Backlog drain time measures how long np.ingestion.readings took to reach zero after each run request.",
        "CPU, memory, threads and container stats are opportunistic local snapshots.",
        "This is a local reproducible capacity baseline, not production readiness or scientific validation."
    )
}

Write-NpJsonFile -Depth 50 -NullWhenEmpty -Path (Join-Path $runDirectory "summary.json") -Value $summary

$summaryLines = New-Object System.Collections.Generic.List[string]
$summaryLines.Add("# System capacity workload summary")
$summaryLines.Add("")
$summaryLines.Add("- GeneratedAtUtc: $($summary.generatedAtUtc)")
$summaryLines.Add("- RunId: $runId")
$summaryLines.Add("- Profile: $Profile")
$summaryLines.Add("- OutputDirectory: $runDirectory")
$summaryLines.Add("- Successful runs: $($summary.successfulRuns)/$($summary.repetitions)")
$summaryLines.Add("- Expected events total: $($summary.expectedEventsTotal)")
$summaryLines.Add("- Accepted readings total: $($summary.acceptedReadingsTotal)")
$summaryLines.Add("- Risk assessments total: $($summary.riskAssessmentsTotal)")
$summaryLines.Add("- Rejected total: $($summary.rejectedTotal)")
$summaryLines.Add("- Quarantined total: $($summary.quarantinedTotal)")
$summaryLines.Add("- Lost events total: $($summary.lostEventsTotal)")
$summaryLines.Add("- Elapsed p50/p95/p99/max ms: $($summary.elapsedMs.p50)/$($summary.elapsedMs.p95)/$($summary.elapsedMs.p99)/$($summary.elapsedMs.max)")
$summaryLines.Add("- Backlog drain p50/p95/p99/max ms: $($summary.backlogDrainMs.p50)/$($summary.backlogDrainMs.p95)/$($summary.backlogDrainMs.p99)/$($summary.backlogDrainMs.max)")
$summaryLines.Add("- Queue total final: $($summary.queueTotalAfter.final)")
$summaryLines.Add("")
$summaryLines.Add("## Classification")
$summaryLines.Add("")
$summaryLines.Add("This is a local reproducible capacity baseline. It is not production readiness, stress testing, external validation or scientific calibration.")
$summaryLines.Add("")
$summaryLines.Add("## Limitations")
$summaryLines.Add("")
foreach ($limitation in $summary.limitations) {
    $summaryLines.Add("- $limitation")
}

if ($runFailures.Count -gt 0) {
    $summaryLines.Add("")
    $summaryLines.Add("## Failures")
    $summaryLines.Add("")
    foreach ($failure in $runFailures) {
        $summaryLines.Add("- Iteration $($failure.iteration): $($failure.error)")
    }
}

$summaryLines | Set-Content -Path (Join-Path $runDirectory "summary.md") -Encoding UTF8

if ($failureRows.Count -gt 0) {
    Write-Host "System capacity workload completed with failures. Output: $runDirectory"
    exit 1
}

Write-Host "System capacity workload complete. Output: $runDirectory"
