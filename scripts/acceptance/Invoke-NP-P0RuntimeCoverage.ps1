[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [string]$ConfigPath = '',
    [switch]$SkipBuild,
    [switch]$KeepRuntime,
    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Import-Module (Join-Path $PSScriptRoot 'modules\Acceptance.Common.psm1') -Force -ErrorAction Stop
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot 'config\acceptance\p0-runtime-coverage.json'
}
elseif (-not [IO.Path]::IsPathRooted($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot $ConfigPath
}
$ConfigPath = [IO.Path]::GetFullPath($ConfigPath)
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "P0 runtime coverage configuration not found: $ConfigPath"
}
$Config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

$ArtifactsRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $ArtifactsRoot ('p0-runtime-coverage\' + (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
}
elseif (-not [IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot $OutputRoot
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$artifactsPrefix = $ArtifactsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if ($OutputRoot.Equals($ArtifactsRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not ($OutputRoot + [IO.Path]::DirectorySeparatorChar).StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must be a run-scoped child of $ArtifactsRoot"
}
if (Test-Path -LiteralPath $OutputRoot) {
    $existing = @(Get-ChildItem -LiteralPath $OutputRoot -Force -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0 -and -not $Overwrite) {
        throw "OutputRoot exists and is not empty: $OutputRoot. Use -Overwrite for this exact run directory."
    }
    if ($existing.Count -gt 0 -and $Overwrite) {
        $existing | Remove-Item -Recurse -Force
    }
}

$Directories = [ordered]@{
    Logs = Join-Path $OutputRoot 'logs'
    Api = Join-Path $OutputRoot 'api'
    Database = Join-Path $OutputRoot 'database'
    Scenarios = Join-Path $OutputRoot 'scenarios'
    Rbac = Join-Path $OutputRoot 'rbac'
    Diagnostics = Join-Path $OutputRoot 'diagnostics'
    Observability = Join-Path $OutputRoot 'observability'
    Alerts = Join-Path $OutputRoot 'alerts'
    Shutdown = Join-Path $OutputRoot 'shutdown'
}
(@($OutputRoot) + @($Directories.Values)) | ForEach-Object { New-Item -ItemType Directory -Force -Path $_ | Out-Null }

$StartedAtUtc = (Get-Date).ToUniversalTime()
$RunId = $StartedAtUtc.ToString('yyyyMMddTHHmmssZ') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
$Tests = [System.Collections.Generic.List[object]]::new()
$Commands = [System.Collections.Generic.List[object]]::new()
$Blockers = [System.Collections.Generic.List[object]]::new()
$CreatedUserIds = [System.Collections.Generic.List[guid]]::new()
$CreatedRoleIds = [System.Collections.Generic.List[int]]::new()
$RuntimeStarted = $false
$AdminToken = ''
$HarnessException = $null
$JsonDepth = 100

function ConvertTo-P0RedactedText {
    param([AllowNull()][string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $value = $Text
    $value = $value -replace '(?i)(Authorization\s*[:=]\s*Bearer\s+)[A-Za-z0-9._-]+', '${1}<redacted>'
    $value = $value -replace '(?i)("token"\s*:\s*")[^"]+("\s*)', '${1}<redacted>${2}'
    $value = $value -replace '(?i)(password\s*[:=]\s*)[^,\s}]+', '${1}<redacted>'
    return $value
}

function Write-P0Json {
    param([Parameter(Mandatory)][string]$Path, [AllowNull()][object]$Value)
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    $Value | ConvertTo-Json -Depth $JsonDepth | Set-Content -LiteralPath $Path -Encoding utf8
}

function Add-P0Test {
    param(
        [Parameter(Mandatory)][string]$Area,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'WARN')][string]$Status,
        [Parameter(Mandatory)][string]$Detail,
        [string]$Evidence = ''
    )
    $Tests.Add([pscustomobject]@{
        area = $Area
        name = $Name
        status = $Status
        detail = ConvertTo-P0RedactedText $Detail
        evidence = $Evidence
    }) | Out-Null
    if ($Status -eq 'FAIL') {
        $Blockers.Add([pscustomobject]@{
            area = $Area
            name = $Name
            detail = ConvertTo-P0RedactedText $Detail
            evidence = $Evidence
        }) | Out-Null
    }
}

function Assert-P0 {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Area,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$PassDetail,
        [Parameter(Mandatory)][string]$FailDetail,
        [string]$Evidence = ''
    )
    Add-P0Test -Area $Area -Name $Name -Status $(if ($Condition) { 'PASS' } else { 'FAIL' }) -Detail $(if ($Condition) { $PassDetail } else { $FailDetail }) -Evidence $Evidence
    return $Condition
}

function Get-P0DotEnvValues {
    $values = @{}
    $path = Join-Path $RepoRoot '.env'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $values }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#') -or $line -notmatch '=') { continue }
        $parts = $line -split '=', 2
        $values[$parts[0].Trim()] = $parts[1].Trim().Trim('"').Trim("'")
    }
    return $values
}

$DotEnv = Get-P0DotEnvValues
function Get-P0ConfiguredValue {
    param([string]$EnvironmentVariable, [string]$DefaultValue)
    $environmentValue = [Environment]::GetEnvironmentVariable($EnvironmentVariable)
    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) { return $environmentValue }
    if ($DotEnv.ContainsKey($EnvironmentVariable) -and -not [string]::IsNullOrWhiteSpace([string]$DotEnv[$EnvironmentVariable])) {
        return [string]$DotEnv[$EnvironmentVariable]
    }
    return $DefaultValue
}

$RuntimeConfig = $Config.runtime
$ApiRoot = [string]$RuntimeConfig.apiRoot
$ApiBaseUrl = [string]$RuntimeConfig.apiBaseUrl
$PreventionBaseUrl = [string]$RuntimeConfig.preventionBaseUrl
$WebUrl = [string]$RuntimeConfig.webUrl
$AreaCode = [string]$RuntimeConfig.areaCode
$GrafanaHostPort = Get-P0ConfiguredValue -EnvironmentVariable 'NP_ACCEPTANCE_GRAFANA_PORT' -DefaultValue '3300'
$env:GRAFANA_PORT = $GrafanaHostPort
$GrafanaHealthUri = [UriBuilder]([string]$RuntimeConfig.grafanaHealthUrl)
$GrafanaHealthUri.Port = [int]$GrafanaHostPort
$GrafanaHealthUrl = $GrafanaHealthUri.Uri.AbsoluteUri
$AdminUsername = Get-P0ConfiguredValue -EnvironmentVariable ([string]$RuntimeConfig.adminUsernameEnvironmentVariable) -DefaultValue ([string]$RuntimeConfig.defaultAdminUsername)
$AdminPassword = Get-P0ConfiguredValue -EnvironmentVariable ([string]$RuntimeConfig.adminPasswordEnvironmentVariable) -DefaultValue ([string]$RuntimeConfig.defaultAdminPassword)
$RabbitUsername = Get-P0ConfiguredValue -EnvironmentVariable ([string]$RuntimeConfig.rabbitUsernameEnvironmentVariable) -DefaultValue ([string]$RuntimeConfig.defaultRabbitUsername)
$RabbitPassword = Get-P0ConfiguredValue -EnvironmentVariable ([string]$RuntimeConfig.rabbitPasswordEnvironmentVariable) -DefaultValue ([string]$RuntimeConfig.defaultRabbitPassword)
$InfluxToken = Get-P0ConfiguredValue -EnvironmentVariable ([string]$RuntimeConfig.influxTokenEnvironmentVariable) -DefaultValue ''
$InfluxDatabase = Get-P0ConfiguredValue -EnvironmentVariable ([string]$RuntimeConfig.influxDatabaseEnvironmentVariable) -DefaultValue ([string]$RuntimeConfig.defaultInfluxDatabase)

function Invoke-P0LoggedProcess {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 900
    )
    $safe = $Id -replace '[^A-Za-z0-9_.-]', '-'
    $stdout = Join-Path $Directories.Logs "$safe.stdout.log"
    $stderr = Join-Path $Directories.Logs "$safe.stderr.log"
    $combined = Join-Path $Directories.Logs "$safe.log"
    Remove-Item -LiteralPath $stdout, $stderr, $combined -Force -ErrorAction SilentlyContinue
    $started = (Get-Date).ToUniversalTime()
    $commandText = "$Executable " + (($Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ')
    $exitCode = 125
    $timedOut = $false
    try {
        $invocation = New-NpAcceptanceProcessInvocation -Executable $Executable -Arguments $Arguments
        $quotedArguments = @($invocation.Arguments | ForEach-Object { ConvertTo-NpAcceptanceQuotedArgument -Value $_ })
        $process = Start-Process -FilePath $invocation.FilePath -ArgumentList $quotedArguments -WorkingDirectory $RepoRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            try { $process.Kill($true) } catch { }
            try { $process.WaitForExit(5000) | Out-Null } catch { }
            $exitCode = 124
        }
        else {
            $exitCode = [int]$process.ExitCode
        }
    }
    catch {
        $_.Exception.Message | Set-Content -LiteralPath $stderr -Encoding utf8
        $exitCode = 125
    }
    $completed = (Get-Date).ToUniversalTime()
    $stdoutText = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
    $stderrText = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { '' }
    @(
        "> $commandText"
        "exitCode=$exitCode"
        "timedOut=$timedOut"
        ''
        (ConvertTo-P0RedactedText $stdoutText)
        (ConvertTo-P0RedactedText $stderrText)
    ) | Set-Content -LiteralPath $combined -Encoding utf8
    $row = [pscustomobject]@{
        id = $Id
        command = ConvertTo-P0RedactedText $commandText
        exitCode = $exitCode
        timedOut = $timedOut
        durationSeconds = [Math]::Round(($completed - $started).TotalSeconds, 3)
        log = $combined
    }
    $Commands.Add($row) | Out-Null
    return $row
}

function Invoke-P0Api {
    param(
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [AllowNull()][object]$Body = $null,
        [string]$Token = '',
        [int[]]$ExpectedStatus = @(200),
        [string]$EvidenceName = ''
    )
    $uri = if ($Path.StartsWith('http', [StringComparison]::OrdinalIgnoreCase)) { $Path } else { "$ApiBaseUrl$Path" }
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $headers.Authorization = "Bearer $Token" }
    $evidenceRunHeader = 'X-NP-Evidence-Run-Id'
    $evidenceRunPartition = $RunId
    if ($Method -eq 'POST' -and $Path -eq '/control/runtime/runs' -and -not [string]::IsNullOrWhiteSpace($EvidenceName)) {
        $suffix = [IO.Path]::GetFileNameWithoutExtension($EvidenceName) -replace '[^A-Za-z0-9_.-]', '-'
        $evidenceRunPartition = "$RunId-$suffix"
        if ($evidenceRunPartition.Length -gt 128) {
            $evidenceRunPartition = $evidenceRunPartition.Substring(0, 128)
        }
    }
    $headers[$evidenceRunHeader] = $evidenceRunPartition
    $parameters = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        TimeoutSec = 120
        SkipHttpErrorCheck = $true
        ErrorAction = 'Stop'
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth $JsonDepth -Compress
    }
    $response = $null
    $status = 0
    $raw = ''
    $maxAttempts = 8
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $response = Invoke-WebRequest @parameters
        $status = [int]$response.StatusCode
        $raw = [string]$response.Content
        if ($status -ne 429 -or $attempt -ge $maxAttempts) {
            break
        }

        $retryAfterHeader = ''
        if ($response.Headers.ContainsKey('Retry-After')) {
            $retryAfterHeader = [string]($response.Headers['Retry-After'] | Select-Object -First 1)
        }
        [int]$parsedDelay = 0
        [DateTimeOffset]$retryAfterDate = [DateTimeOffset]::MinValue
        $delaySeconds = 10
        if ([int]::TryParse($retryAfterHeader, [ref]$parsedDelay)) {
            $delaySeconds = $parsedDelay
        }
        elseif ([DateTimeOffset]::TryParse($retryAfterHeader, [ref]$retryAfterDate)) {
            $delaySeconds = [Math]::Ceiling(($retryAfterDate.ToUniversalTime() - (Get-Date).ToUniversalTime()).TotalSeconds)
        }
        if ($Method -eq 'POST' -and $Path -eq '/control/runtime/runs') {
            $delaySeconds = [Math]::Max($delaySeconds, 60)
        }
        $delaySeconds = [Math]::Min(300, [Math]::Max(1, [int]$delaySeconds))
        $Commands.Add([pscustomobject]@{
            id = 'http-429-retry'
            command = ConvertTo-P0RedactedText "$Method $uri attempt=$attempt retryAfter=$retryAfterHeader"
            exitCode = 429
            timedOut = $false
            durationSeconds = $delaySeconds
            log = ''
        }) | Out-Null
        Start-Sleep -Seconds $delaySeconds
    }
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        try { $json = $raw | ConvertFrom-Json } catch { }
    }
    $evidencePath = ''
    if (-not [string]::IsNullOrWhiteSpace($EvidenceName)) {
        $evidencePath = Join-Path $Directories.Api $EvidenceName
        (ConvertTo-P0RedactedText $raw) | Set-Content -LiteralPath $evidencePath -Encoding utf8
    }
    if ($ExpectedStatus -notcontains $status) {
        throw "$Method $uri returned HTTP $status; expected $($ExpectedStatus -join ','). Body=$(ConvertTo-P0RedactedText $raw)"
    }
    return [pscustomobject]@{ StatusCode = $status; Json = $json; Raw = $raw; Evidence = $evidencePath; Uri = $uri }
}

function Wait-P0Http {
    param([string]$Uri, [int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 5
            if ([int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 400) { return $true }
        }
        catch { }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Invoke-P0StartRuntime {
    param([int]$TimeoutSeconds = 420)
    $id = 'np-start'
    $safe = $id
    $stdout = Join-Path $Directories.Logs "$safe.stdout.log"
    $stderr = Join-Path $Directories.Logs "$safe.stderr.log"
    $combined = Join-Path $Directories.Logs "$safe.log"
    Remove-Item -LiteralPath $stdout, $stderr, $combined -Force -ErrorAction SilentlyContinue
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'start', '-NoBrowser', '-ForceRestart')
    $commandText = 'pwsh ' + (($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ')
    $started = (Get-Date).ToUniversalTime()
    $process = $null
    $exitCode = 1
    $timedOut = $false
    $note = ''
    try {
        $resolved = Get-Command 'pwsh' -ErrorAction Stop
        $process = Start-Process -FilePath $resolved.Source -ArgumentList $arguments -WorkingDirectory $RepoRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            $apiReady = Wait-P0Http -Uri "$ApiRoot/health" -TimeoutSeconds 1
            $preventionReady = Wait-P0Http -Uri "$PreventionBaseUrl/health/live" -TimeoutSeconds 1
            $webReady = Wait-P0Http -Uri $WebUrl -TimeoutSeconds 1
            if ($apiReady -and $preventionReady -and $webReady) {
                $exitCode = 0
                if (-not $process.HasExited) {
                    $note = "Runtime became healthy while the np start wrapper was still active; wrapper PID $($process.Id) was stopped after health proof."
                    try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
                }
                break
            }
            if ($process.HasExited -and [int]$process.ExitCode -ne 0) {
                $exitCode = [int]$process.ExitCode
                $note = "np start exited before all runtime endpoints became healthy."
                break
            }
            Start-Sleep -Seconds 2
        } while ((Get-Date) -lt $deadline)
        if ($exitCode -ne 0 -and $null -ne $process -and -not $process.HasExited) {
            $timedOut = $true
            $exitCode = 124
            $note = "np start did not produce all healthy endpoints within $TimeoutSeconds seconds."
            try { $process.Kill($true) } catch { try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { } }
        }
    }
    catch {
        $note = $_.Exception.Message
        $exitCode = 125
        if ($null -ne $process -and -not $process.HasExited) {
            try { $process.Kill($true) } catch { try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { } }
        }
    }
    finally {
        if ($null -ne $process) { try { $process.Dispose() } catch { } }
    }
    $completed = (Get-Date).ToUniversalTime()
    $stdoutText = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
    $stderrText = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { '' }
    @(
        "> $commandText"
        "exitCode=$exitCode"
        "timedOut=$timedOut"
        "durationSeconds=$([Math]::Round(($completed - $started).TotalSeconds, 3))"
        $note
        ''
        (ConvertTo-P0RedactedText $stdoutText)
        (ConvertTo-P0RedactedText $stderrText)
    ) | Set-Content -LiteralPath $combined -Encoding utf8
    $row = [pscustomobject]@{
        id = $id
        command = ConvertTo-P0RedactedText $commandText
        exitCode = $exitCode
        timedOut = $timedOut
        durationSeconds = [Math]::Round(($completed - $started).TotalSeconds, 3)
        log = $combined
    }
    $Commands.Add($row) | Out-Null
    return $row
}

function Invoke-P0SqlJson {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Sql)
    $container = [string]$RuntimeConfig.postgresContainer
    $user = [string]$RuntimeConfig.postgresUser
    $database = [string]$RuntimeConfig.postgresDatabase
    $result = Invoke-P0LoggedProcess -Id "sql-$Name" -Executable 'docker' -Arguments @('exec', '-i', $container, 'psql', '-U', $user, '-d', $database, '-t', '-A', '-v', 'ON_ERROR_STOP=1', '-c', $Sql) -TimeoutSeconds 120
    if ($result.exitCode -ne 0) { throw "SQL query '$Name' failed. See $($result.log)" }
    $stdoutPath = Join-Path $Directories.Logs ("sql-$Name.stdout.log")
    $raw = if (Test-Path -LiteralPath $stdoutPath) { (Get-Content -LiteralPath $stdoutPath -Raw).Trim() } else { '' }
    if ([string]::IsNullOrWhiteSpace($raw)) { $raw = '[]' }
    $value = $raw | ConvertFrom-Json
    $path = Join-Path $Directories.Database "$Name.json"
    Write-P0Json -Path $path -Value $value
    return @($value)
}

function Get-P0PublishEvents {
    param([AllowNull()][string]$LogDirectory, [string]$RunIdValue)
    if ([string]::IsNullOrWhiteSpace($LogDirectory)) { return @() }
    $resolved = if ([IO.Path]::IsPathRooted($LogDirectory)) { $LogDirectory } else { Join-Path $RepoRoot $LogDirectory }
    if (-not (Test-Path -LiteralPath $resolved -PathType Container)) { return @() }
    $events = [System.Collections.Generic.List[object]]::new()
    $pattern = 'Published\s+[^|]+\|\s+EventId=(?<event>[0-9a-fA-F-]{36})\s+\|\s+CorrelationId=(?<correlation>[^|]+)\s+\|\s+SensorId=(?<sensor>[0-9a-fA-F-]{36})'
    foreach ($file in Get-ChildItem -LiteralPath $resolved -Recurse -File -ErrorAction SilentlyContinue) {
        foreach ($line in Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue) {
            $match = [regex]::Match([string]$line, $pattern)
            if (-not $match.Success) { continue }
            $correlation = $match.Groups['correlation'].Value.Trim()
            $cycle = -1
            $compactRun = $RunIdValue.Replace('-', '')
            $cycleMatch = [regex]::Match($correlation, "^$compactRun-(?<cycle>\d{4})-")
            if ($cycleMatch.Success) { $cycle = [int]$cycleMatch.Groups['cycle'].Value }
            $events.Add([pscustomobject]@{
                eventId = $match.Groups['event'].Value
                correlationId = $correlation
                sensorId = $match.Groups['sensor'].Value
                cycleIndex = $cycle
                sourceFile = $file.FullName
            }) | Out-Null
        }
    }
    return @($events)
}

function Wait-P0OperationSettled {
    param([guid]$OperationId, [int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = $null
    do {
        $last = (Invoke-P0Api -Method GET -Path "/control/runtime/operations/$OperationId" -Token $AdminToken -ExpectedStatus @(200)).Json
        $accounting = $last.accounting
        if ($null -ne $accounting -and [bool]$accounting.settled -and
            [int]$accounting.pendingInbox -eq 0 -and [int]$accounting.processingInbox -eq 0 -and [int]$accounting.retryPendingInbox -eq 0 -and
            ([string]$last.state -in @('SystemCompleted', 'Completed', 'Succeeded'))) {
            return $last
        }
        if ([string]$last.state -in @('Failed', 'Rejected', 'Cancelled')) { return $last }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $last
}

function Wait-P0AuditStable {
    param([guid]$RunIdValue, [int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = $null
    $previousSignature = ''
    $stablePolls = 0
    do {
        $last = (Invoke-P0Api -Method GET -Path "/control/runtime/runs/$RunIdValue/audit" -Token $AdminToken -ExpectedStatus @(200)).Json
        $signature = "$($last.expectedEvents)/$($last.acceptedReadings)/$($last.riskAssessments)/$($last.missingEvents)/$($last.rejected)/$($last.quarantined)/$($last.retryAttempts)"
        if ($signature -eq $previousSignature) { $stablePolls++ } else { $stablePolls = 0; $previousSignature = $signature }
        if ($stablePolls -ge 1 -and $null -ne $last.expectedEvents) { return $last }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $last
}

function Start-P0RunCase {
    param(
        [Parameter(Mandatory)][string]$CaseId,
        [Parameter(Mandatory)][string]$PrimaryProfile,
        [Parameter(Mandatory)][string[]]$Profiles,
        [Parameter(Mandatory)][int]$Seed,
        [string]$RepeatOf = '',
        [switch]$Supplemental,
        [string]$ScenarioCode = '',
        [int]$SensorCount = 0,
        [int]$NumberOfCycles = 0,
        [int]$IntervalSeconds = 0
    )
    if ([string]::IsNullOrWhiteSpace($ScenarioCode)) { $ScenarioCode = [string]$Config.scenarioMatrix.scenarioCode }
    if ($SensorCount -le 0) { $SensorCount = [int]$RuntimeConfig.sensorCount }
    if ($NumberOfCycles -le 0) { $NumberOfCycles = [int]$RuntimeConfig.numberOfCycles }
    if ($IntervalSeconds -le 0) { $IntervalSeconds = [int]$RuntimeConfig.intervalSeconds }
    $runLabel = "p0-$RunId-$CaseId"
    $body = [ordered]@{
        areaCode = $AreaCode
        scenarioCode = $ScenarioCode
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        seed = $Seed
        degradationProfile = if ($Profiles.Count -eq 1) { $Profiles[0] } else { $Profiles -join '+' }
        degradationProfiles = $Profiles
        collectEvidence = $true
        waitForCompletion = $true
        timeoutSeconds = [int]$RuntimeConfig.runTimeoutSeconds
        allowParallelRun = $false
        runLabel = $runLabel
    }
    $caseDir = Join-Path $Directories.Scenarios ($CaseId -replace '[^A-Za-z0-9_.-]', '-')
    New-Item -ItemType Directory -Force -Path $caseDir | Out-Null
    $start = Invoke-P0Api -Method POST -Path '/control/runtime/runs' -Body $body -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "$CaseId-start.json"
    $response = $start.Json
    if ($null -eq $response.run -or [string]$response.run.status -ne 'Completed') {
        throw "Run case '$CaseId' did not complete. responseStatus=$($response.status); runStatus=$($response.run.status)"
    }
    $runGuid = [guid]$response.run.id
    $operationGuid = [guid]$response.operationId
    $operation = Wait-P0OperationSettled -OperationId $operationGuid -TimeoutSeconds ([int]$RuntimeConfig.settlementTimeoutSeconds)
    $audit = Wait-P0AuditStable -RunIdValue $runGuid -TimeoutSeconds ([int]$RuntimeConfig.settlementTimeoutSeconds)
    $timings = (Invoke-P0Api -Method GET -Path "/control/runtime/runs/$runGuid/timings" -Token $AdminToken -ExpectedStatus @(200)).Json
    $operationByRun = (Invoke-P0Api -Method GET -Path "/control/runtime/runs/$runGuid/operation" -Token $AdminToken -ExpectedStatus @(200)).Json
    $operationByRequest = (Invoke-P0Api -Method GET -Path "/control/runtime/operations/by-request/$($response.requestId)" -Token $AdminToken -ExpectedStatus @(200)).Json
    $runDetail = (Invoke-P0Api -Method GET -Path "/control/runtime/runs/$runGuid" -Token $AdminToken -ExpectedStatus @(200)).Json
    $latest = (Invoke-P0Api -Method GET -Path "/control/runtime/runs/latest?areaCode=$AreaCode" -Token $AdminToken -ExpectedStatus @(200)).Json

    $readingsSql = @"
SELECT COALESCE(json_agg(row_to_json(x)), '[]'::json)::text FROM (
  SELECT "EventId" AS "eventId", "SensorId" AS "sensorId", "MetricType" AS "metricType",
         "Value" AS "value", "EventTime" AS "eventTime", "IngestTime" AS "ingestTime",
         EXTRACT(EPOCH FROM ("IngestTime" - "EventTime")) AS "ingestDelaySeconds",
         "CorrelationId" AS "correlationId", ("PayloadJson"::jsonb->>'cycleIndex')::int AS "cycleIndex",
         "OperationalState" AS "operationalState"
  FROM projection.accepted_reading_log
  WHERE "PayloadJson"::jsonb->>'simulationRunId' = '$runGuid'
  ORDER BY "PersistedAt", "EventTime", "SensorId"
) x;
"@
    $inboxSql = @"
SELECT COALESCE(json_agg(row_to_json(x)), '[]'::json)::text FROM (
  SELECT "EventId" AS "eventId", "SimulationRunId" AS "simulationRunId", "CorrelationId" AS "correlationId",
         CASE "Status"
           WHEN 0 THEN 'Pending' WHEN 1 THEN 'Processing' WHEN 2 THEN 'Processed'
           WHEN 3 THEN 'Failed' WHEN 4 THEN 'Rejected' WHEN 5 THEN 'RetryPending'
           WHEN 6 THEN 'Quarantined' ELSE 'Unknown'
         END AS "status", "AttemptCount" AS "attemptCount", "PublishedAt" AS "publishedAt",
         "ReceivedAt" AS "receivedAt", "PersistedAt" AS "persistedAt", "IngestTime" AS "ingestTime"
  FROM pipeline.event_inbox WHERE "SimulationRunId" = '$runGuid'
  ORDER BY "ReceivedAt", "PersistedAt", "EventId"
) x;
"@
    $attemptSql = @"
SELECT COALESCE(json_agg(row_to_json(x)), '[]'::json)::text FROM (
  SELECT i."EventId" AS "eventId", a."AttemptNumber" AS "attemptNumber", a."Stage" AS "stage",
         CASE a."Outcome"
           WHEN 0 THEN 'Started' WHEN 1 THEN 'Succeeded' WHEN 2 THEN 'Failed'
           WHEN 3 THEN 'RetryScheduled' WHEN 4 THEN 'Quarantined' ELSE 'Unknown'
         END AS "outcome", a."ErrorCode" AS "errorCode", a."StartedAt" AS "startedAt", a."FinishedAt" AS "finishedAt"
  FROM pipeline.processing_attempts a JOIN pipeline.event_inbox i ON i."Id" = a."InboxEventId"
  WHERE i."SimulationRunId" = '$runGuid'
  ORDER BY i."EventId", a."AttemptNumber"
) x;
"@
    $cycleSql = @"
SELECT COALESCE(json_agg(row_to_json(x)), '[]'::json)::text FROM (
  SELECT "EventId" AS "eventId", "SensorId" AS "sensorId", "GridCellId" AS "gridCellId",
         "CycleIndex" AS "cycleIndex", "MetricOrigin" AS "metricOrigin", "Outcome" AS "outcome",
         "RiskScore" AS "riskScore", "RiskLevel" AS "riskLevel", "EventTime" AS "eventTime"
  FROM projection.cycle_observation WHERE "SimulationRunId" = '$runGuid'
  ORDER BY "CycleIndex", "CreatedAt", "SensorId"
) x;
"@
    $readings = Invoke-P0SqlJson -Name "$CaseId-readings" -Sql $readingsSql
    $inbox = Invoke-P0SqlJson -Name "$CaseId-inbox" -Sql $inboxSql
    $attempts = Invoke-P0SqlJson -Name "$CaseId-attempts" -Sql $attemptSql
    $cycleObservations = Invoke-P0SqlJson -Name "$CaseId-cycle-observations" -Sql $cycleSql
    $resolvedProfiles = @()
    $runOverrides = $response.run.runOverrides
    $resolvedOverrides = if ($null -ne $runOverrides) { $runOverrides.resolved } else { $null }
    if ($null -ne $resolvedOverrides -and $null -ne $resolvedOverrides.degradationProfiles -and @($resolvedOverrides.degradationProfiles).Count -gt 0) {
        $resolvedProfiles = @($resolvedOverrides.degradationProfiles | ForEach-Object { [string]$_ })
    }
    elseif ($null -ne $resolvedOverrides -and -not [string]::IsNullOrWhiteSpace([string]$resolvedOverrides.degradationProfile)) {
        $resolvedProfiles = @(([string]$resolvedOverrides.degradationProfile) -split '[,+;|]' | ForEach-Object { $_.Trim() })
    }
    $publishEvents = Get-P0PublishEvents -LogDirectory ([string]$response.logDirectory) -RunIdValue ([string]$runGuid)
    $case = [ordered]@{
        caseId = $CaseId
        primaryProfile = $PrimaryProfile
        profiles = $Profiles
        repeatOf = $RepeatOf
        supplemental = [bool]$Supplemental
        seed = $Seed
        scenarioCode = $ScenarioCode
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        expectedEvents = $SensorCount * $NumberOfCycles
        requestId = $response.requestId
        operationId = $response.operationId
        runId = $runGuid
        runStatus = $response.run.status
        resolvedProfiles = $resolvedProfiles
        logDirectory = $response.logDirectory
        evidenceDirectory = $response.evidenceDirectory
        operation = $operation
        operationByRun = $operationByRun
        operationByRequest = $operationByRequest
        run = $runDetail
        latestRunAtCollection = $latest
        audit = $audit
        timings = $timings
        readings = $readings
        inbox = $inbox
        attempts = $attempts
        cycleObservations = $cycleObservations
        publishEvents = $publishEvents
    }
    Write-P0Json -Path (Join-Path $caseDir 'case-evidence.json') -Value $case
    Assert-P0 -Condition ($null -ne $operation -and [bool]$operation.accounting.settled) -Area 'scenario-matrix' -Name "$CaseId accounting settled" -PassDetail 'Operation accounting settled.' -FailDetail "Operation accounting did not settle: $($operation | ConvertTo-Json -Depth 8 -Compress)" -Evidence (Join-Path $caseDir 'case-evidence.json') | Out-Null
    Assert-P0 -Condition ([string]$operationByRun.operationId -eq [string]$response.operationId -and [string]$operationByRequest.operationId -eq [string]$response.operationId) -Area 'scenario-matrix' -Name "$CaseId correlation lookup" -PassDetail 'Run and request lookups resolve the same operation.' -FailDetail 'Run/request/operation correlation diverged.' -Evidence (Join-Path $caseDir 'case-evidence.json') | Out-Null
    return $case
}

function Invoke-P0RoleCoverage {
    $rolesResponse = Invoke-P0Api -Method GET -Path '/users-roles/roles' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'rbac-roles.json'
    $roleRows = @($rolesResponse.Json)
    $roleByName = @{}
    foreach ($role in $roleRows) { $roleByName[[string]$role.name] = $role }

    $tempRoleName = "P0Temp-$($RunId.Substring($RunId.Length - 8))"
    $createRole = Invoke-P0Api -Method POST -Path '/users-roles/roles' -Body @{ name = $tempRoleName } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'rbac-temp-role-create.json'
    $tempRoleId = [int]$createRole.Json.id
    $CreatedRoleIds.Add($tempRoleId) | Out-Null
    $getRole = Invoke-P0Api -Method GET -Path "/users-roles/roles/$tempRoleId" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'rbac-temp-role-get.json'
    $renamed = "$tempRoleName-Renamed"
    $updateRole = Invoke-P0Api -Method PUT -Path "/users-roles/roles/$tempRoleId" -Body @{ name = $renamed } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'rbac-temp-role-update.json'
    Assert-P0 -Condition ([string]$getRole.Json.name -eq $tempRoleName -and [string]$updateRole.Json.name -eq $renamed) -Area 'rbac' -Name 'temporary role lifecycle create/read/update' -PassDetail 'Temporary role was created, read and renamed.' -FailDetail 'Temporary role lifecycle returned inconsistent names.' -Evidence $createRole.Evidence | Out-Null

    $roleIndex = 0
    foreach ($spec in @($Config.rbac.roles)) {
        $roleIndex++
        $roleName = [string]$spec.role
        if (-not $roleByName.ContainsKey($roleName)) {
            Add-P0Test -Area 'rbac' -Name "$roleName seeded role" -Status 'FAIL' -Detail "Seeded role '$roleName' was not returned by /roles." -Evidence $rolesResponse.Evidence
            continue
        }
        $roleId = [int]$roleByName[$roleName].id
        $slug = ($roleName.ToLowerInvariant() -replace '[^a-z0-9]', '-')
        $username = "$($Config.rbac.temporaryUsernamePrefix)-$slug-$($RunId.Substring($RunId.Length - 8))"
        $email = "$username@$($Config.rbac.temporaryEmailDomain)"
        $password = [string]$Config.rbac.temporaryPassword
        $createUser = Invoke-P0Api -Method POST -Path '/users-roles/users' -Body @{
            username = $username
            password = $password
            email = $email
            organization = [string]$Config.rbac.temporaryOrganization
            roles = @()
        } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-user-create.json"
        $userId = [guid]$createUser.Json.id
        $CreatedUserIds.Add($userId) | Out-Null
        $getUser = Invoke-P0Api -Method GET -Path "/users-roles/users/$userId" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-user-get.json"
        $updatedEmail = "updated-$email"
        $updateUser = Invoke-P0Api -Method PUT -Path "/users-roles/users/$userId" -Body @{
            username = $username
            password = $password
            email = $updatedEmail
            organization = "$($Config.rbac.temporaryOrganization) Updated"
            roles = @()
        } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-user-update.json"
        Assert-P0 -Condition ([string]$getUser.Json.username -eq $username -and [string]$updateUser.Json.email -eq $updatedEmail) -Area 'rbac' -Name "$roleName user lifecycle create/read/update" -PassDetail 'Temporary user was created, read and updated.' -FailDetail 'Temporary user lifecycle returned inconsistent identity fields.' -Evidence $updateUser.Evidence | Out-Null

        Invoke-P0Api -Method PUT -Path "/users-roles/users/$userId/roles/$roleId" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-role-add.json" | Out-Null
        Invoke-P0Api -Method GET -Path "/users-roles/users/$userId/roles/$roleId" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-role-check.json" | Out-Null
        $userRoles = Invoke-P0Api -Method GET -Path "/users-roles/users/$userId/roles" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-user-roles.json"
        $roleUsers = Invoke-P0Api -Method GET -Path "/users-roles/roles/$roleId/users" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-role-users.json"
        $membershipVisible = @($userRoles.Json | Where-Object { [int]$_.id -eq $roleId }).Count -eq 1 -and @($roleUsers.Json | Where-Object { [guid]$_.id -eq $userId }).Count -eq 1
        Assert-P0 -Condition $membershipVisible -Area 'rbac' -Name "$roleName membership visibility" -PassDetail 'Membership is visible from both user and role projections.' -FailDetail 'User-role membership was not visible from both API directions.' -Evidence $userRoles.Evidence | Out-Null

        $login = Invoke-P0Api -Method POST -Path '/users-roles/login' -Body @{ usernameOrEmail = $updatedEmail; password = $password } -ExpectedStatus @(200) -EvidenceName "rbac-$slug-login.json"
        $token = [string]$login.Json.token
        $caps = Invoke-P0Api -Method GET -Path '/users-roles/me/capabilities' -Token $token -ExpectedStatus @(200) -EvidenceName "rbac-$slug-capabilities.json"
        $actualCapabilities = @($caps.Json.capabilities | ForEach-Object { [string]$_ })
        $missingRequired = @($spec.requiredCapabilities | Where-Object { $actualCapabilities -notcontains [string]$_ })
        $presentForbidden = @($spec.forbiddenCapabilities | Where-Object { $actualCapabilities -contains [string]$_ })
        Assert-P0 -Condition ($missingRequired.Count -eq 0 -and $presentForbidden.Count -eq 0) -Area 'rbac' -Name "$roleName capability profile" -PassDetail "Required capabilities present and forbidden capabilities absent. count=$($actualCapabilities.Count)" -FailDetail "missing=$($missingRequired -join ','); forbiddenPresent=$($presentForbidden -join ',')" -Evidence $caps.Evidence | Out-Null

        $allowedBody = $null
        if ([string]$spec.allowedProbe.method -eq 'POST') {
            $allowedBody = @{ areaCode = $AreaCode; scenarioCode = 'scenario_b'; sensorCount = 1; numberOfCycles = 1; intervalSeconds = 1; waitForCompletion = $false; timeoutSeconds = 30; allowParallelRun = $false; runLabel = "rbac-allowed-$slug" }
        }
        $allowed = Invoke-P0Api -Method ([string]$spec.allowedProbe.method) -Path ([string]$spec.allowedProbe.path) -Body $allowedBody -Token $token -ExpectedStatus @([int]$spec.allowedProbe.expectedStatus) -EvidenceName "rbac-$slug-allowed.json"
        Add-P0Test -Area 'rbac' -Name "$roleName allowed probe" -Status 'PASS' -Detail "$($spec.allowedProbe.method) $($spec.allowedProbe.path) returned HTTP $($allowed.StatusCode)." -Evidence $allowed.Evidence

        $deniedBody = $null
        if ([string]$spec.deniedProbe.method -eq 'POST') {
            $deniedBody = @{ areaCode = $AreaCode; scenarioCode = 'scenario_b'; sensorCount = 1; numberOfCycles = 1; intervalSeconds = 1; waitForCompletion = $false; timeoutSeconds = 30; allowParallelRun = $false; runLabel = "rbac-denied-$slug" }
        }
        $denied = Invoke-P0Api -Method ([string]$spec.deniedProbe.method) -Path ([string]$spec.deniedProbe.path) -Body $deniedBody -Token $token -ExpectedStatus @([int]$spec.deniedProbe.expectedStatus) -EvidenceName "rbac-$slug-denied.json"
        Add-P0Test -Area 'rbac' -Name "$roleName denied probe" -Status 'PASS' -Detail "$($spec.deniedProbe.method) $($spec.deniedProbe.path) returned HTTP $($denied.StatusCode)." -Evidence $denied.Evidence
        Invoke-P0Api -Method POST -Path '/users-roles/logout' -Token $token -ExpectedStatus @(204) -EvidenceName "rbac-$slug-logout.json" | Out-Null
        Invoke-P0Api -Method DELETE -Path "/users-roles/users/$userId/roles/$roleId" -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-role-remove.json" | Out-Null
        $rolelessLogin = Invoke-P0Api -Method POST -Path '/users-roles/login' -Body @{ usernameOrEmail = $username; password = $password } -ExpectedStatus @(200) -EvidenceName "rbac-$slug-roleless-login.json"
        $rolelessToken = [string]$rolelessLogin.Json.token
        $rolelessCapabilities = Invoke-P0Api -Method GET -Path '/users-roles/me/capabilities' -Token $rolelessToken -ExpectedStatus @(200) -EvidenceName "rbac-$slug-roleless-capabilities.json"
        $remainingProtected = @($spec.requiredCapabilities | Where-Object { @($rolelessCapabilities.Json.capabilities) -contains [string]$_ })
        Assert-P0 -Condition ($remainingProtected.Count -eq 0) -Area 'rbac' -Name "$roleName removal changes fresh authority" -PassDetail 'A fresh token after role removal no longer contains the role capabilities.' -FailDetail "Capabilities still present after role removal: $($remainingProtected -join ',')." -Evidence $rolelessCapabilities.Evidence | Out-Null
        Invoke-P0Api -Method POST -Path '/users-roles/logout' -Token $rolelessToken -ExpectedStatus @(204) -EvidenceName "rbac-$slug-roleless-logout.json" | Out-Null
        Invoke-P0Api -Method DELETE -Path "/users-roles/users/$userId" -Token $AdminToken -ExpectedStatus @(204) -EvidenceName "rbac-$slug-user-delete.json" | Out-Null
        $CreatedUserIds.Remove($userId) | Out-Null
    }

    Invoke-P0Api -Method DELETE -Path "/users-roles/roles/$tempRoleId" -Token $AdminToken -ExpectedStatus @(204) -EvidenceName 'rbac-temp-role-delete.json' | Out-Null
    $CreatedRoleIds.Remove($tempRoleId) | Out-Null
    Add-P0Test -Area 'rbac' -Name 'temporary role lifecycle delete' -Status 'PASS' -Detail 'Temporary role was removed.' -Evidence (Join-Path $Directories.Api 'rbac-temp-role-delete.json')
}

function Invoke-P0DiagnosticCoverage {
    $catalog = Invoke-P0Api -Method GET -Path '/control/runtime/diagnostics' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'diagnostic-catalog.json'
    $runtimeIds = @($catalog.Json.diagnostics | ForEach-Object { [string]$_.id } | Sort-Object)
    $catalogPath = Join-Path $RepoRoot ([string]$Config.diagnostics.generatedCatalog)
    $expectedIds = @(Import-Csv -LiteralPath $catalogPath | ForEach-Object { [string]$_.diagnostic_id } | Sort-Object)
    $catalogMatches = ($runtimeIds.Count -eq $expectedIds.Count -and -not (Compare-Object $runtimeIds $expectedIds))
    Assert-P0 -Condition $catalogMatches -Area 'diagnostics' -Name 'runtime diagnostic catalog exact match' -PassDetail "Runtime exposed exactly $($runtimeIds.Count) versioned diagnostics." -FailDetail "Runtime=$($runtimeIds -join ','); expected=$($expectedIds -join ',')" -Evidence $catalog.Evidence | Out-Null

    $semantic = @{}
    foreach ($property in $Config.diagnostics.semanticRequirements.PSObject.Properties) { $semantic[$property.Name] = $property.Value }
    foreach ($diagnosticId in $runtimeIds) {
        $safe = $diagnosticId -replace '[^A-Za-z0-9_.-]', '-'
        $result = Invoke-P0Api -Method POST -Path "/control/runtime/diagnostics/$diagnosticId" -Body @{ areaCode = $AreaCode; recentMinutes = 120; scenarioCode = 'scenario_b' } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName "diagnostic-$safe.json"
        $payload = $result.Json
        $shapeOk = [string]$payload.id -eq $diagnosticId -and $null -ne $payload.columns -and $null -ne $payload.rows -and $null -ne $payload.limitations
        Assert-P0 -Condition $shapeOk -Area 'diagnostics' -Name "$diagnosticId contract" -PassDetail "columns=$(@($payload.columns).Count); rows=$(@($payload.rows).Count); limitations=$(@($payload.limitations).Count)" -FailDetail 'Diagnostic result shape or id is invalid.' -Evidence $result.Evidence | Out-Null
        if ($semantic.ContainsKey($diagnosticId)) {
            $requirement = $semantic[$diagnosticId]
            if ($requirement.PSObject.Properties.Name -contains 'minimumRows') {
                Assert-P0 -Condition (@($payload.rows).Count -ge [int]$requirement.minimumRows) -Area 'diagnostics' -Name "$diagnosticId semantic rows" -PassDetail "rows=$(@($payload.rows).Count)" -FailDetail "Expected at least $($requirement.minimumRows) rows; got $(@($payload.rows).Count)." -Evidence $result.Evidence | Out-Null
            }
            if ($requirement.PSObject.Properties.Name -contains 'requiredMetrics') {
                $metrics = @($payload.rows | ForEach-Object { [string]$_.metric })
                $missingMetrics = @($requirement.requiredMetrics | Where-Object { $metrics -notcontains [string]$_ })
                Assert-P0 -Condition ($missingMetrics.Count -eq 0) -Area 'diagnostics' -Name "$diagnosticId required metrics" -PassDetail 'All required metrics are present.' -FailDetail "Missing metrics: $($missingMetrics -join ',')." -Evidence $result.Evidence | Out-Null
            }
            if ($requirement.PSObject.Properties.Name -contains 'requiredScenarios') {
                $scenarios = @($payload.rows | ForEach-Object { [string]$_.scenario } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
                $missingScenarios = @($requirement.requiredScenarios | Where-Object { $scenarios -notcontains [string]$_ })
                Assert-P0 -Condition ($missingScenarios.Count -eq 0) -Area 'diagnostics' -Name "$diagnosticId required scenarios" -PassDetail "Scenarios present: $($scenarios -join ',')." -FailDetail "Missing scenarios: $($missingScenarios -join ','); present=$($scenarios -join ',')." -Evidence $result.Evidence | Out-Null
            }
        }
    }
}

function Invoke-P0ObservabilityCoverage {
    param([Parameter(Mandatory)][guid]$SimulationRunId)
    $health = Invoke-P0Api -Method GET -Path '/control/runtime/observability/health' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'observability-health.json'
    $components = @($health.Json.components)
    foreach ($required in @($Config.observability.requiredOperationalComponents)) {
        $matches = @($components | Where-Object { [string]$_.component -eq [string]$required })
        $allowedStatuses = @($Config.observability.allowedOperationalStatuses | ForEach-Object { [string]$_ })
        $componentStatus = if ($matches.Count -eq 1) { [string]$matches[0].status } else { '' }
        Assert-P0 -Condition ($matches.Count -eq 1 -and $allowedStatuses -contains $componentStatus) -Area 'observability' -Name "$required operational component" -PassDetail "status=$componentStatus; source=$($matches[0].source)" -FailDetail "Missing or non-operational component '$required' (status='$componentStatus'; allowed=$($allowedStatuses -join ','))." -Evidence $health.Evidence | Out-Null
    }
    $rabbit = Invoke-P0Api -Method GET -Path '/control/runtime/observability/rabbitmq' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'observability-rabbitmq.json'
    Assert-P0 -Condition ([string]$rabbit.Json.collectionStatus -eq 'Measured' -and @($rabbit.Json.queues).Count -gt 0) -Area 'observability' -Name 'RabbitMQ metrics collection' -PassDetail "queues=$(@($rabbit.Json.queues).Count)" -FailDetail "collectionStatus=$($rabbit.Json.collectionStatus); queues=$(@($rabbit.Json.queues).Count)" -Evidence $rabbit.Evidence | Out-Null

    $basicBytes = [Text.Encoding]::ASCII.GetBytes("${RabbitUsername}:${RabbitPassword}")
    $basicHeaders = @{ Authorization = 'Basic ' + [Convert]::ToBase64String($basicBytes) }
    foreach ($endpoint in @('overview', 'queues', 'bindings')) {
        $response = Invoke-WebRequest -Uri "$($RuntimeConfig.rabbitManagementUrl)/api/$endpoint" -Headers $basicHeaders -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 30
        $path = Join-Path $Directories.Observability "rabbit-$endpoint.json"
        ([string]$response.Content) | Set-Content -LiteralPath $path -Encoding utf8
        Assert-P0 -Condition ([int]$response.StatusCode -eq 200) -Area 'observability' -Name "RabbitMQ management $endpoint" -PassDetail 'HTTP 200.' -FailDetail "HTTP $([int]$response.StatusCode)." -Evidence $path | Out-Null
    }

    if ([string]::IsNullOrWhiteSpace($InfluxToken)) {
        throw 'INFLUXDB_TOKEN is required for the authenticated InfluxDB health probe.'
    }
    $influxHeaders = @{ Authorization = "Bearer $InfluxToken" }
    $influx = Invoke-WebRequest -Uri ([string]$RuntimeConfig.influxHealthUrl) -Headers $influxHeaders -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 30
    $influxPath = Join-Path $Directories.Observability 'influx-health.json'
    ([string]$influx.Content) | Set-Content -LiteralPath $influxPath -Encoding utf8
    $influxJson = $null
    try { $influxJson = ([string]$influx.Content) | ConvertFrom-Json } catch { }
    $influxStatus = if ($null -ne $influxJson -and $null -ne $influxJson.PSObject.Properties['status']) { [string]$influxJson.status } else { '' }
    $influxHealthy = [int]$influx.StatusCode -eq 200 -and ([string]::IsNullOrWhiteSpace($influxStatus) -or $influxStatus -eq 'pass')
    Assert-P0 -Condition $influxHealthy -Area 'observability' -Name 'InfluxDB authenticated health' -PassDetail "InfluxDB authenticated /health returned HTTP 200; status=$influxStatus." -FailDetail "HTTP=$([int]$influx.StatusCode); status=$influxStatus" -Evidence $influxPath | Out-Null

    if ([string]::IsNullOrWhiteSpace($InfluxDatabase)) {
        throw 'INFLUXDB_DATABASE is required for the run-scoped InfluxDB query.'
    }
    $influxBaseUrl = ([string]$RuntimeConfig.influxHealthUrl) -replace '/health$', ''
    $influxSql = "SELECT * FROM accepted_readings WHERE simulation_run_id = '$SimulationRunId' ORDER BY time DESC LIMIT 100"
    $influxQueryUrl = "$influxBaseUrl/api/v3/query_sql?db=$([uri]::EscapeDataString($InfluxDatabase))&q=$([uri]::EscapeDataString($influxSql))&format=json"
    $influxQuery = Invoke-WebRequest -Uri $influxQueryUrl -Headers $influxHeaders -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 60
    $influxQueryPath = Join-Path $Directories.Observability 'influx-run-query.json'
    ([string]$influxQuery.Content) | Set-Content -LiteralPath $influxQueryPath -Encoding utf8
    $influxRows = @()
    try { $influxRows = @(([string]$influxQuery.Content) | ConvertFrom-Json) } catch { }
    Assert-P0 -Condition ([int]$influxQuery.StatusCode -eq 200 -and $influxRows.Count -gt 0) -Area 'observability' -Name 'InfluxDB run-scoped accepted reading series' -PassDetail "simulationRunId=$SimulationRunId; database=$InfluxDatabase; rows=$($influxRows.Count)" -FailDetail "HTTP=$([int]$influxQuery.StatusCode); database=$InfluxDatabase; rows=$($influxRows.Count)" -Evidence $influxQueryPath | Out-Null

    $grafana = Invoke-WebRequest -Uri $GrafanaHealthUrl -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 30
    $grafanaPath = Join-Path $Directories.Observability 'grafana-health.json'
    ([string]$grafana.Content) | Set-Content -LiteralPath $grafanaPath -Encoding utf8
    $grafanaJson = $null
    try { $grafanaJson = ([string]$grafana.Content) | ConvertFrom-Json } catch { }
    Assert-P0 -Condition ([int]$grafana.StatusCode -eq 200 -and [string]$grafanaJson.database -eq 'ok') -Area 'observability' -Name 'Grafana health' -PassDetail "database=$($grafanaJson.database); version=$($grafanaJson.version)" -FailDetail "HTTP=$([int]$grafana.StatusCode); database=$($grafanaJson.database)" -Evidence $grafanaPath | Out-Null

    $evidence = Invoke-P0Api -Method GET -Path '/control/runtime/observability/evidence' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'observability-evidence-catalog.json'
    $items = @($evidence.Json.items)
    Assert-P0 -Condition ($items.Count -ge [int]$Config.observability.minimumEvidenceItems) -Area 'evidence' -Name 'runtime evidence catalog populated' -PassDetail "items=$($items.Count)" -FailDetail "Expected at least $($Config.observability.minimumEvidenceItems) items; got $($items.Count)." -Evidence $evidence.Evidence | Out-Null
    $downloadable = @($items | Where-Object { [bool]$_.contentAvailable -or [bool]$_.downloadAvailable } | Select-Object -First 1)
    if ([bool]$Config.observability.downloadFirstHttpEvidence) {
        if ($downloadable.Count -eq 0) {
            Add-P0Test -Area 'evidence' -Name 'runtime evidence HTTP download' -Status 'FAIL' -Detail 'No HTTP-downloadable evidence item was exposed.' -Evidence $evidence.Evidence
        }
        else {
            $item = $downloadable[0]
            $headers = @{ Authorization = "Bearer $AdminToken" }
            $downloadPath = Join-Path $Directories.Observability ('evidence-' + ([string]$item.evidenceId -replace '[^A-Za-z0-9_.-]', '-') + '.bin')
            $download = Invoke-WebRequest -Uri "$ApiBaseUrl/control/runtime/observability/evidence/$($item.evidenceId)" -Headers $headers -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 60 -OutFile $downloadPath -PassThru
            Assert-P0 -Condition ([int]$download.StatusCode -eq 200 -and (Get-Item -LiteralPath $downloadPath).Length -gt 0) -Area 'evidence' -Name 'runtime evidence HTTP download' -PassDetail "evidenceId=$($item.evidenceId); bytes=$((Get-Item -LiteralPath $downloadPath).Length)" -FailDetail "HTTP=$([int]$download.StatusCode); bytes=$((Get-Item -LiteralPath $downloadPath).Length)" -Evidence $downloadPath | Out-Null
        }
    }
}

function Invoke-P0AlertCoverage {
    $spec = $Config.alerts
    $case = Start-P0RunCase -CaseId 'alert-high-risk' -PrimaryProfile 'none' -Profiles @('none') -Seed ([int]$spec.seed) -ScenarioCode ([string]$spec.scenarioCode) -SensorCount ([int]$spec.sensorCount) -NumberOfCycles ([int]$spec.numberOfCycles) -IntervalSeconds ([int]$spec.intervalSeconds)
    Write-P0Json -Path (Join-Path $Directories.Alerts 'alert-run.json') -Value $case
    $activeDiagnostic = Invoke-P0Api -Method POST -Path '/control/runtime/diagnostics/active-alerts' -Body @{ areaCode = $AreaCode; recentMinutes = 120; scenarioCode = [string]$spec.scenarioCode } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'alert-active-diagnostic.json'
    $transitionDiagnostic = Invoke-P0Api -Method POST -Path '/control/runtime/diagnostics/recent-alert-transitions' -Body @{ areaCode = $AreaCode; recentMinutes = 120; scenarioCode = [string]$spec.scenarioCode } -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'alert-transitions-diagnostic.json'
    $publicActive = Invoke-P0Api -Method GET -Path "/control/areas/$AreaCode/alerts/active" -ExpectedStatus @(200) -EvidenceName 'alert-active-public.json'
    $expectedCode = [string]$spec.expectedAlertCode
    $transitionRows = @($transitionDiagnostic.Json.rows | Where-Object { [string]$_.alertCode -eq $expectedCode })
    $transitionStatusValues = @($transitionRows | ForEach-Object { if ($_.PSObject.Properties['status']) { [string]$_.status } else { '<missing-status>' } })
    Assert-P0 -Condition ($transitionRows.Count -gt 0) -Area 'alerts' -Name 'high-risk alert transition observed' -PassDetail "transitions=$($transitionRows.Count); statuses=$($transitionStatusValues -join ',')" -FailDetail "No '$expectedCode' transition was persisted after the high-risk run." -Evidence $transitionDiagnostic.Evidence | Out-Null
    $invalidStatuses = @($transitionRows | Where-Object { @($spec.allowedStatuses) -notcontains [string]$_.status })
    $invalidStatusValues = @($invalidStatuses | ForEach-Object { if ($_.PSObject.Properties['status']) { [string]$_.status } else { '<missing-status>' } })
    Assert-P0 -Condition ($invalidStatuses.Count -eq 0) -Area 'alerts' -Name 'alert transition statuses valid' -PassDetail 'All transition statuses are allowed.' -FailDetail "Invalid statuses: $($invalidStatusValues -join ',')." -Evidence $transitionDiagnostic.Evidence | Out-Null
    $diagnosticActiveIds = @($activeDiagnostic.Json.rows | ForEach-Object { [string]$_.id } | Sort-Object)
    $publicActiveIds = @($publicActive.Json | ForEach-Object { [string]$_.id } | Sort-Object)
    Assert-P0 -Condition (-not (Compare-Object $diagnosticActiveIds $publicActiveIds)) -Area 'alerts' -Name 'active alert API consistency' -PassDetail "activeCount=$($publicActiveIds.Count)" -FailDetail "Diagnostic IDs=$($diagnosticActiveIds -join ','); public IDs=$($publicActiveIds -join ',')." -Evidence $publicActive.Evidence | Out-Null
    $duplicateOpenCodes = @($activeDiagnostic.Json.rows | Group-Object alertCode | Where-Object { $_.Count -gt 1 })
    $duplicateOpenCodeNames = @($duplicateOpenCodes | ForEach-Object { [string]$_.Name })
    Assert-P0 -Condition ($duplicateOpenCodes.Count -eq 0) -Area 'alerts' -Name 'no duplicate open alert code' -PassDetail 'No duplicate active alert code exists.' -FailDetail "Duplicates: $($duplicateOpenCodeNames -join ',')." -Evidence $activeDiagnostic.Evidence | Out-Null
    return $case
}

function Get-P0RuntimeProcesses {
    $patterns = @($Config.shutdown.processNamePatterns | ForEach-Object { [string]$_ })
    if ($IsWindows -and $null -ne (Get-Command Get-CimInstance -ErrorAction SilentlyContinue)) {
        return @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
            $commandLine = [string]$_.CommandLine
            $name = [string]$_.Name
            @($patterns | Where-Object { $commandLine -like "*$_*" -or $name -like "*$_*" }).Count -gt 0
        } | Select-Object ProcessId, ParentProcessId, Name, CommandLine)
    }
    return @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $name = [string]$_.ProcessName
        @($patterns | Where-Object { $name -like "*$_*" }).Count -gt 0
    } | Select-Object Id, ProcessName, Path)
}

function Get-P0ProcessDescription {
    param([AllowNull()][object]$Process)
    if ($null -eq $Process) { return '' }
    foreach ($propertyName in @('CommandLine', 'ProcessName', 'Name', 'Path')) {
        $property = $Process.PSObject.Properties[$propertyName]
        if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            return [string]$property.Value
        }
    }
    return [string]$Process
}

function Invoke-P0CleanupEntities {
    foreach ($userId in @($CreatedUserIds)) {
        try { Invoke-P0Api -Method DELETE -Path "/users-roles/users/$userId" -Token $AdminToken -ExpectedStatus @(204, 404) | Out-Null } catch { }
    }
    foreach ($roleId in @($CreatedRoleIds)) {
        try { Invoke-P0Api -Method DELETE -Path "/users-roles/roles/$roleId" -Token $AdminToken -ExpectedStatus @(204, 404) | Out-Null } catch { }
    }
}

try {
    Write-P0Json -Path (Join-Path $OutputRoot 'run-spec.json') -Value ([ordered]@{
        schemaVersion = 1
        runId = $RunId
        startedAtUtc = $StartedAtUtc.ToString('o')
        configPath = $ConfigPath
        outputRoot = $OutputRoot
        skipBuild = [bool]$SkipBuild
        keepRuntime = [bool]$KeepRuntime
        grafanaHostPort = $GrafanaHostPort
        grafanaHealthUrl = $GrafanaHealthUrl
    })

    $preExistingProcesses = @(Get-P0RuntimeProcesses)
    Write-P0Json -Path (Join-Path $Directories.Shutdown 'processes-before-start.json') -Value $preExistingProcesses
    if ($preExistingProcesses.Count -gt 0) {
        throw "Tracked NatureProtector runtime processes are already running. Refusing to overlap acceptance execution. See processes-before-start.json."
    }
    Add-P0Test -Area 'runtime' -Name 'exclusive local runtime precondition' -Status 'PASS' -Detail 'No tracked NatureProtector runtime process existed before startup.' -Evidence (Join-Path $Directories.Shutdown 'processes-before-start.json')

    if (-not $SkipBuild) {
        $prepare = Invoke-P0LoggedProcess -Id 'np-prepare-local' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'prepare-local') -TimeoutSeconds 1800
        if ($prepare.exitCode -ne 0) { throw "np prepare-local failed. See $($prepare.log)" }
        Add-P0Test -Area 'runtime' -Name 'prepare local runtime' -Status 'PASS' -Detail 'np prepare-local completed.' -Evidence $prepare.log
    }
    else {
        Add-P0Test -Area 'runtime' -Name 'prepare local runtime' -Status 'WARN' -Detail '-SkipBuild selected; existing unchanged build outputs are required.'
    }
    $clean = Invoke-P0LoggedProcess -Id 'np-clean-local' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'clean-local') -TimeoutSeconds 600
    if ($clean.exitCode -ne 0) { throw "np clean-local failed. See $($clean.log)" }
    Add-P0Test -Area 'runtime' -Name 'clean deterministic local state' -Status 'PASS' -Detail 'Project-scoped containers, networks and volumes were removed before the campaign.' -Evidence $clean.log

    $up = Invoke-P0LoggedProcess -Id 'np-up' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'up') -TimeoutSeconds 900
    if ($up.exitCode -ne 0) { throw "np up failed. See $($up.log)" }
    $RuntimeStarted = $true
    $start = Invoke-P0StartRuntime -TimeoutSeconds ([int]$RuntimeConfig.serviceStartTimeoutSeconds)
    if ($start.exitCode -ne 0) { throw "np start failed to produce all healthy runtime endpoints. See $($start.log)" }

    $apiReady = Wait-P0Http -Uri "$ApiRoot/health" -TimeoutSeconds ([int]$RuntimeConfig.serviceStartTimeoutSeconds)
    $preventionReady = Wait-P0Http -Uri "$PreventionBaseUrl/health/live" -TimeoutSeconds ([int]$RuntimeConfig.serviceStartTimeoutSeconds)
    $webReady = Wait-P0Http -Uri $WebUrl -TimeoutSeconds ([int]$RuntimeConfig.serviceStartTimeoutSeconds)
    Assert-P0 -Condition $apiReady -Area 'runtime' -Name 'Backoffice API ready' -PassDetail "$ApiRoot/health reachable." -FailDetail 'Backoffice API did not become ready.' | Out-Null
    Assert-P0 -Condition $preventionReady -Area 'runtime' -Name 'Prevention Host live' -PassDetail "$PreventionBaseUrl/health/live reachable." -FailDetail 'Prevention Host did not become live.' | Out-Null
    Assert-P0 -Condition $webReady -Area 'runtime' -Name 'webUI reachable' -PassDetail "$WebUrl reachable." -FailDetail 'webUI did not become reachable.' | Out-Null
    if (-not ($apiReady -and $preventionReady -and $webReady)) { throw 'Required local runtime surfaces are unavailable.' }

    $login = Invoke-P0Api -Method POST -Path '/users-roles/login' -Body @{ usernameOrEmail = $AdminUsername; password = $AdminPassword } -ExpectedStatus @(200) -EvidenceName 'admin-login.json'
    $AdminToken = [string]$login.Json.token
    if ([string]::IsNullOrWhiteSpace($AdminToken)) { throw 'Admin login returned no token.' }
    $invalidLogin = Invoke-P0Api -Method POST -Path '/users-roles/login' -Body @{ usernameOrEmail = $AdminUsername; password = "$AdminPassword-invalid" } -ExpectedStatus @(401) -EvidenceName 'admin-invalid-login.json'
    Add-P0Test -Area 'rbac' -Name 'invalid credentials rejected' -Status 'PASS' -Detail 'Invalid administrator credentials returned HTTP 401.' -Evidence $invalidLogin.Evidence
    $anonymousProtected = Invoke-P0Api -Method GET -Path '/users-roles/users' -ExpectedStatus @(401) -EvidenceName 'anonymous-protected-endpoint.json'
    Add-P0Test -Area 'rbac' -Name 'anonymous protected endpoint rejected' -Status 'PASS' -Detail 'A protected administration endpoint returned HTTP 401 without a bearer token.' -Evidence $anonymousProtected.Evidence
    Add-P0Test -Area 'auth' -Name 'invalid credentials rejected' -Status 'PASS' -Detail "Invalid credentials returned HTTP $($invalidLogin.StatusCode)." -Evidence $invalidLogin.Evidence
    $adminIdentity = Invoke-P0Api -Method GET -Path '/users-roles/me' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'admin-identity.json'
    Assert-P0 -Condition ([string]$adminIdentity.Json.username -eq $AdminUsername -or [string]$adminIdentity.Json.email -eq $AdminUsername) -Area 'auth' -Name 'admin authenticated identity' -PassDetail 'Authenticated identity matches the configured administrator.' -FailDetail "Unexpected identity username=$($adminIdentity.Json.username); email=$($adminIdentity.Json.email)." -Evidence $adminIdentity.Evidence | Out-Null
    $adminCaps = Invoke-P0Api -Method GET -Path '/users-roles/me/capabilities' -Token $AdminToken -ExpectedStatus @(200) -EvidenceName 'admin-capabilities.json'
    $requiredAdminCaps = @('run.read', 'simulation.execute', 'users.manage', 'roles.manage', 'quality.read', 'evidence.read')
    $missingAdminCaps = @($requiredAdminCaps | Where-Object { @($adminCaps.Json.capabilities) -notcontains $_ })
    Assert-P0 -Condition ($missingAdminCaps.Count -eq 0) -Area 'auth' -Name 'admin capabilities' -PassDetail 'Admin has all capabilities required by the P0 harness.' -FailDetail "Missing admin capabilities: $($missingAdminCaps -join ',')." -Evidence $adminCaps.Evidence | Out-Null

    $matrixRuns = [System.Collections.Generic.List[object]]::new()
    $seed = [int]$Config.scenarioMatrix.seed
    foreach ($profile in @($Config.scenarioMatrix.profiles)) {
        $profileName = [string]$profile
        $caseId = $profileName -replace '[^A-Za-z0-9_.-]', '-'
        $matrixRuns.Add((Start-P0RunCase -CaseId $caseId -PrimaryProfile $profileName -Profiles @($profileName) -Seed $seed)) | Out-Null
    }
    foreach ($repeatProfile in @($Config.scenarioMatrix.repeatProfiles)) {
        $profileName = [string]$repeatProfile
        $originalCaseId = $profileName -replace '[^A-Za-z0-9_.-]', '-'
        $repeatCaseId = "$originalCaseId-repeat"
        $matrixRuns.Add((Start-P0RunCase -CaseId $repeatCaseId -PrimaryProfile $profileName -Profiles @($profileName) -Seed $seed -RepeatOf $originalCaseId)) | Out-Null
    }
    foreach ($supplement in @($Config.scenarioMatrix.supplementalRuns)) {
        $matrixRuns.Add((Start-P0RunCase -CaseId ([string]$supplement.id) -PrimaryProfile ([string]$supplement.primaryProfile) -Profiles @($supplement.profiles | ForEach-Object { [string]$_ }) -Seed $seed -Supplemental)) | Out-Null
    }
    $matrixInputPath = Join-Path $Directories.Scenarios 'scenario-matrix-input.json'
    Write-P0Json -Path $matrixInputPath -Value ([ordered]@{ schemaVersion = 1; generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o'); runs = @($matrixRuns) })
    $matrixVerify = Invoke-P0LoggedProcess -Id 'verify-scenario-profile-matrix' -Executable 'python' -Arguments @(
        (Join-Path $RepoRoot 'scripts\acceptance\verify_scenario_profile_matrix.py'),
        '--config', $ConfigPath,
        '--input', $matrixInputPath,
        '--output-dir', $Directories.Scenarios
    ) -TimeoutSeconds 300
    Assert-P0 -Condition ($matrixVerify.exitCode -eq 0) -Area 'scenario-matrix' -Name 'all degradation profile invariants' -PassDetail 'All versioned profile invariants passed.' -FailDetail "Scenario profile verifier failed with exit $($matrixVerify.exitCode)." -Evidence (Join-Path $Directories.Scenarios 'scenario-matrix-result.json') | Out-Null

    Invoke-P0RoleCoverage
    $diagnosticPrep = $Config.diagnostics.prepareScenarioC
    $diagnosticScenarioCase = Start-P0RunCase -CaseId ([string]$diagnosticPrep.caseId) -PrimaryProfile ([string]$diagnosticPrep.primaryProfile) -Profiles @($diagnosticPrep.profiles | ForEach-Object { [string]$_ }) -Seed ([int]$diagnosticPrep.seed) -ScenarioCode ([string]$diagnosticPrep.scenarioCode) -SensorCount ([int]$diagnosticPrep.sensorCount) -NumberOfCycles ([int]$diagnosticPrep.numberOfCycles) -IntervalSeconds ([int]$diagnosticPrep.intervalSeconds) -Supplemental
    Write-P0Json -Path (Join-Path $Directories.Diagnostics 'scenario-c-prerequisite-run.json') -Value $diagnosticScenarioCase
    Invoke-P0DiagnosticCoverage
    $alertCase = Invoke-P0AlertCoverage
    Invoke-P0ObservabilityCoverage -SimulationRunId ([guid]$alertCase.runId)

    $simulatorBeforeStop = @(Get-P0RuntimeProcesses | Where-Object { (Get-P0ProcessDescription -Process $_) -like '*NatureProtector.Simulator.Host*' })
    Write-P0Json -Path (Join-Path $Directories.Shutdown 'processes-before-stop.json') -Value @(Get-P0RuntimeProcesses)
    Assert-P0 -Condition ($simulatorBeforeStop.Count -eq 0) -Area 'shutdown' -Name 'Simulator.Host self-terminated after runs' -PassDetail 'No Simulator.Host process remains before global stop.' -FailDetail "Simulator.Host processes remaining=$($simulatorBeforeStop.Count)." -Evidence (Join-Path $Directories.Shutdown 'processes-before-stop.json') | Out-Null
}
catch {
    $HarnessException = $_
    Add-P0Test -Area 'harness' -Name 'P0 runtime coverage exception' -Status 'FAIL' -Detail $_.Exception.Message -Evidence (Join-Path $Directories.Logs 'harness-exception.txt')
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $Directories.Logs 'harness-exception.txt') -Encoding utf8
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($AdminToken)) {
        Invoke-P0CleanupEntities
        try {
            Invoke-P0Api -Method POST -Path '/users-roles/logout' -Token $AdminToken -ExpectedStatus @(204) -EvidenceName 'admin-logout.json' | Out-Null
            Add-P0Test -Area 'auth' -Name 'admin logout endpoint' -Status 'PASS' -Detail 'Admin logout returned HTTP 204.' -Evidence (Join-Path $Directories.Api 'admin-logout.json')
        }
        catch {
            Add-P0Test -Area 'auth' -Name 'admin logout endpoint' -Status 'FAIL' -Detail $_.Exception.Message -Evidence (Join-Path $Directories.Api 'admin-logout.json')
        }
    }
    if (-not $KeepRuntime) {
        if ($RuntimeStarted) {
            $stop = Invoke-P0LoggedProcess -Id 'np-stop' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'stop') -TimeoutSeconds 300
            Add-P0Test -Area 'shutdown' -Name 'np stop' -Status $(if ($stop.exitCode -eq 0) { 'PASS' } else { 'FAIL' }) -Detail "exitCode=$($stop.exitCode)" -Evidence $stop.log
        }
        $down = Invoke-P0LoggedProcess -Id 'np-down' -Executable 'pwsh' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $RepoRoot 'scripts\np.ps1'), 'down') -TimeoutSeconds 300
        Add-P0Test -Area 'shutdown' -Name 'np down' -Status $(if ($down.exitCode -eq 0) { 'PASS' } else { 'FAIL' }) -Detail "exitCode=$($down.exitCode)" -Evidence $down.log
    }
    elseif ($KeepRuntime) {
        Add-P0Test -Area 'shutdown' -Name 'np stop/down' -Status 'FAIL' -Detail '-KeepRuntime selected; a P0 acceptance run cannot pass without shutdown verification.'
    }

    $processesAfter = @(Get-P0RuntimeProcesses)
    Write-P0Json -Path (Join-Path $Directories.Shutdown 'processes-after-stop.json') -Value $processesAfter
    if (-not $KeepRuntime) {
        Assert-P0 -Condition ($processesAfter.Count -eq 0) -Area 'shutdown' -Name 'no tracked runtime processes after stop' -PassDetail 'No tracked API, Prevention, Simulator or Vite process remains.' -FailDetail "Tracked processes remaining=$($processesAfter.Count)." -Evidence (Join-Path $Directories.Shutdown 'processes-after-stop.json') | Out-Null
        $containerResult = Invoke-P0LoggedProcess -Id 'docker-containers-after-down' -Executable 'docker' -Arguments @('ps', '--filter', "name=$($Config.shutdown.containerNamePrefix)", '--format', '{{json .}}') -TimeoutSeconds 60
        $containerStdout = Join-Path $Directories.Logs 'docker-containers-after-down.stdout.log'
        $runningContainerLines = @(if (Test-Path -LiteralPath $containerStdout) { Get-Content -LiteralPath $containerStdout | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } })
        Assert-P0 -Condition ($containerResult.exitCode -eq 0 -and $runningContainerLines.Count -eq 0) -Area 'shutdown' -Name 'no running project containers after down' -PassDetail 'No running np-* container remains.' -FailDetail "exit=$($containerResult.exitCode); running=$($runningContainerLines.Count)" -Evidence $containerResult.log | Out-Null
    }

    $Tests | Export-Csv -LiteralPath (Join-Path $OutputRoot 'tests.csv') -NoTypeInformation -Encoding utf8
    $Commands | Export-Csv -LiteralPath (Join-Path $OutputRoot 'commands.csv') -NoTypeInformation -Encoding utf8
    if ($Blockers.Count -eq 0) {
        @([pscustomobject]@{ area = 'none'; name = 'none'; detail = 'No blockers recorded.'; evidence = '' }) | Export-Csv -LiteralPath (Join-Path $OutputRoot 'blockers.csv') -NoTypeInformation -Encoding utf8
    }
    else {
        $Blockers | Export-Csv -LiteralPath (Join-Path $OutputRoot 'blockers.csv') -NoTypeInformation -Encoding utf8
    }

    $failed = @($Tests | Where-Object { $_.status -eq 'FAIL' })
    $status = if ($null -ne $HarnessException) { 'FAIL' } elseif ($failed.Count -gt 0) { 'FAIL' } else { 'PASS' }
    $native = if ($status -eq 'PASS') { 'P0_RUNTIME_FUNCTIONAL_COVERAGE_PASS' } else { 'P0_RUNTIME_FUNCTIONAL_COVERAGE_FAIL' }
    $completedAt = (Get-Date).ToUniversalTime()
    $result = [ordered]@{
        schemaVersion = 1
        status = $status
        nativeStatus = $native
        runId = $RunId
        startedAtUtc = $StartedAtUtc.ToString('o')
        completedAtUtc = $completedAt.ToString('o')
        durationSeconds = [Math]::Round(($completedAt - $StartedAtUtc).TotalSeconds, 3)
        tests = [ordered]@{ total = $Tests.Count; passed = @($Tests | Where-Object { $_.status -eq 'PASS' }).Count; failed = $failed.Count; warnings = @($Tests | Where-Object { $_.status -eq 'WARN' }).Count }
        blockers = $Blockers.Count
        outputRoot = $OutputRoot
    }
    Write-P0Json -Path (Join-Path $OutputRoot 'acceptance-result.json') -Value $result
    Write-P0Json -Path (Join-Path $OutputRoot 'summary.json') -Value ([ordered]@{ result = $result; tests = @($Tests); blockers = @($Blockers) })
    @(
        '# P0 runtime functional coverage'
        ''
        "- Status: **$status**"
        "- Native status: ``$native``"
        "- Tests: $($Tests.Count)"
        "- Passed: $(@($Tests | Where-Object { $_.status -eq 'PASS' }).Count)"
        "- Failed: $($failed.Count)"
        "- Warnings: $(@($Tests | Where-Object { $_.status -eq 'WARN' }).Count)"
        "- Output: ``$OutputRoot``"
        ''
        '## Failed checks'
        ''
        $(if ($failed.Count -eq 0) { '- None.' } else { @($failed | ForEach-Object { "- [$($_.area)] $($_.name): $($_.detail)" }) })
    ) | Set-Content -LiteralPath (Join-Path $OutputRoot 'SUMMARY.md') -Encoding utf8

    $manifestPath = Join-Path $OutputRoot 'evidence-manifest.csv'
    $manifestRows = @(Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.FullName -ne $manifestPath -and $_.Name -ne 'hashes.sha256' } | Sort-Object FullName | ForEach-Object {
        $manifestHash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        [pscustomobject]@{
            relativePath = [IO.Path]::GetRelativePath($OutputRoot, $_.FullName).Replace('\', '/')
            sizeBytes = $_.Length
            sha256 = $manifestHash.Hash.ToLowerInvariant()
        }
    })
    $manifestRows | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding utf8

    $hashPath = Join-Path $OutputRoot 'hashes.sha256'
    Get-ChildItem -LiteralPath $OutputRoot -Recurse -File | Where-Object { $_.FullName -ne $hashPath } | Sort-Object FullName | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        $relative = [IO.Path]::GetRelativePath($OutputRoot, $_.FullName).Replace('\', '/')
        "$($hash.Hash.ToLowerInvariant())  $relative"
    } | Set-Content -LiteralPath $hashPath -Encoding utf8

    Write-Host ($result | ConvertTo-Json -Depth 10)
    if ($status -eq 'PASS') { exit 0 }
    exit 1
}
