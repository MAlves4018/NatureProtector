[CmdletBinding()]
param(
    [switch]$Smoke,
    [switch]$Full,
    [switch]$Evidence,
    [switch]$Ui,
    [switch]$CleanRoom,
    [string]$CleanCloneRoot,
    [string]$RunRoot,
    [string]$ApiRoot = "http://127.0.0.1:5254",
    [string]$ApiBaseUrl = "http://127.0.0.1:5254/api",
    [string]$PreventionBaseUrl = "http://127.0.0.1:5260",
    [string]$WebUrl = "http://127.0.0.1:5173",
    [string]$AreaCode = "proenca-a-nova",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword = "admin123",
    [string]$RabbitUser = "np",
    [string]$RabbitPassword = "np_dev_pass",
    [int]$SensorCount = 6,
    [int]$NumberOfCycles = 5,
    [int]$IntervalSeconds = 5,
    [int]$TimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$SourceRepoRoot = $RepoRoot
$DefaultEvidenceRoot = Join-Path $RepoRoot "artifacts\functional-validation"
if ([string]::IsNullOrWhiteSpace($RunRoot)) {
    $RunRoot = Join-Path $DefaultEvidenceRoot ((Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))
}

$RunRoot = [IO.Path]::GetFullPath($RunRoot)
$LogsDir = Join-Path $RunRoot "logs"
$ExportsDir = Join-Path $RunRoot "exports"
$ScreenshotsDir = Join-Path $RunRoot "screenshots"
$DockerConfigDir = Join-Path $RunRoot "docker-config"
New-Item -ItemType Directory -Force -Path $RunRoot, $LogsDir, $ExportsDir, $ScreenshotsDir, $DockerConfigDir | Out-Null

if ($CleanRoom) {
    $cloneParent = if ([string]::IsNullOrWhiteSpace($CleanCloneRoot)) {
        Join-Path ([IO.Path]::GetTempPath()) (Join-Path "np-clean-clone" ([guid]::NewGuid().ToString("N")))
    }
    else {
        [IO.Path]::GetFullPath($CleanCloneRoot)
    }
    $cloneRepo = Join-Path $cloneParent "NatureProtector"
    if (Test-Path -LiteralPath $cloneRepo) {
        throw "Clean clone target already exists: $cloneRepo"
    }
    New-Item -ItemType Directory -Force -Path $cloneParent | Out-Null
    $currentHead = (& git -C $SourceRepoRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($currentHead)) {
        throw "Unable to resolve source repository HEAD for clean-room validation."
    }
    & git clone --no-local $SourceRepoRoot $cloneRepo 2>&1 | Set-Content -LiteralPath (Join-Path $LogsDir "clean-clone.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "git clone failed for clean-room validation. See clean-clone.log." }
    & git -C $cloneRepo checkout --detach $currentHead 2>&1 | Add-Content -LiteralPath (Join-Path $LogsDir "clean-clone.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "git checkout failed for clean-room validation. See clean-clone.log." }
    $cloneStatus = (& git -C $cloneRepo status --porcelain=v1)
    $cloneStatus | Set-Content -LiteralPath (Join-Path $LogsDir "clean-clone-status.txt") -Encoding utf8
    if (-not [string]::IsNullOrWhiteSpace(($cloneStatus -join ""))) {
        throw "Clean clone is not clean. See clean-clone-status.txt."
    }
    $RepoRoot = (Resolve-Path $cloneRepo).Path
}
$PreviousDockerConfig = $env:DOCKER_CONFIG
$DockerConfigWasIsolated = $false
if ($env:NP_PHASE3_ISOLATE_DOCKER_CONFIG -eq "1") {
    $env:DOCKER_CONFIG = $DockerConfigDir
    $DockerConfigWasIsolated = $true
}

$JsonDepth = 80
$CommandRows = New-Object System.Collections.Generic.List[object]
$TestRows = New-Object System.Collections.Generic.List[object]
$Blockers = New-Object System.Collections.Generic.List[object]
$RfxRows = New-Object System.Collections.Generic.List[object]
$EvidenceRows = New-Object System.Collections.Generic.List[object]
$ChangedRows = New-Object System.Collections.Generic.List[object]
$RunStartedAtUtc = (Get-Date).ToUniversalTime()
$Verdict = "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_PASS"
$AuthToken = $null
$RunB = $null
$RunC = $null
$AuditB = $null
$AuditC = $null
$TimingB = $null
$TimingC = $null
$SummaryAfterB = $null
$SummaryAfterC = $null

function ConvertTo-RedactedText {
    param([AllowNull()][string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $value = $Text
    $value = $value -replace '(?i)(Authorization\s*[:=]\s*Bearer\s+)[A-Za-z0-9._-]+', '${1}<redacted>'
    $value = $value -replace '(?i)("token"\s*:\s*")[^"]+(")', '${1}<redacted>${2}'
    $value = $value -replace '(?i)(password\s*[:=]\s*)[^,\s}]+', '${1}<redacted>'
    $value = $value -replace [regex]::Escape($AdminPassword), "<redacted-password>"
    $value = $value -replace [regex]::Escape($RabbitPassword), "<redacted-password>"
    return $value
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [AllowNull()][object]$Value
    )
    $Value | ConvertTo-Json -Depth $JsonDepth | Set-Content -LiteralPath $Path -Encoding utf8
    Add-Evidence -Path $Path -Kind "json" -Status "created"
}

function Add-Evidence {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Kind,
        [Parameter(Mandatory)][string]$Status
    )
    $full = [IO.Path]::GetFullPath($Path)
    if (-not ($EvidenceRows | Where-Object { $_.path -eq $full })) {
        $EvidenceRows.Add([pscustomobject]@{
            path = $full
            kind = $Kind
            status = $Status
            sizeBytes = if (Test-Path -LiteralPath $full -PathType Leaf) { (Get-Item -LiteralPath $full).Length } else { $null }
        }) | Out-Null
    }
}

function Add-Test {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail,
        [string]$EvidencePath = ""
    )
    $TestRows.Add([pscustomobject]@{
        name = $Name
        status = $Status
        detail = ConvertTo-RedactedText $Detail
        evidence = $EvidencePath
    }) | Out-Null
}

function Add-Blocker {
    param(
        [string]$Severity,
        [string]$Area,
        [string]$Command,
        [string]$RootCause,
        [string]$EvidencePath,
        [string]$NextStep
    )
    $script:Verdict = if ($Area -eq "environment") {
        "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_BLOCKED"
    }
    else {
        "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_FAILED"
    }
    $Blockers.Add([pscustomobject]@{
        severity = $Severity
        area = $Area
        command = $Command
        rootCause = ConvertTo-RedactedText $RootCause
        evidence = $EvidencePath
        nextStep = $NextStep
    }) | Out-Null
}

function Add-Rfx {
    param(
        [string]$Severity,
        [string]$Title,
        [string]$EvidencePath,
        [string]$RecommendedPhase4Fix
    )
    $RfxRows.Add([pscustomobject]@{
        severity = $Severity
        title = $Title
        evidence = $EvidencePath
        recommendedPhase4Fix = $RecommendedPhase4Fix
    }) | Out-Null
}

function Save-TextFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [AllowEmptyString()][string[]]$Lines,
        [string]$Kind = "markdown"
    )
    $Lines | Set-Content -LiteralPath $Path -Encoding utf8
    Add-Evidence -Path $Path -Kind $Kind -Status "created"
}

function Join-ProcessArguments {
    param([string[]]$Arguments)

    return (($Arguments | ForEach-Object {
        if ($null -eq $_) { '""' }
        elseif ($_ -match '[\s"]') { '"' + ($_.Replace('"', '\"')) + '"' }
        else { $_ }
    }) -join ' ')
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 900
    )
    $safeName = $Name -replace '[^a-zA-Z0-9_.-]', '-'
    $logPath = Join-Path $LogsDir "$safeName.log"
    $commandText = "$Executable $($Arguments -join ' ')"
    $started = Get-Date
    $output = New-Object System.Collections.Generic.List[string]
    $exitCode = 1
    try {
        $processInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processInfo.FileName = $Executable
        $argumentListProperty = [System.Diagnostics.ProcessStartInfo].GetProperty("ArgumentList")
        if ($argumentListProperty) {
            foreach ($argument in $Arguments) { [void]$processInfo.ArgumentList.Add($argument) }
        }
        else {
            $processInfo.Arguments = Join-ProcessArguments -Arguments $Arguments
        }
        $processInfo.WorkingDirectory = $RepoRoot
        $processInfo.RedirectStandardOutput = $true
        $processInfo.RedirectStandardError = $true
        $processInfo.UseShellExecute = $false
        $processInfo.CreateNoWindow = $true
        $process = [System.Diagnostics.Process]::Start($processInfo)
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill($true) } catch {}
            try { $process.WaitForExit(5000) | Out-Null } catch {}
            $stdout = $process.StandardOutput.ReadToEnd()
            $stderr = $process.StandardError.ReadToEnd()
            $output.Add("Command timed out after $TimeoutSeconds seconds.") | Out-Null
            if (-not [string]::IsNullOrWhiteSpace($stdout)) { $output.Add($stdout) | Out-Null }
            if (-not [string]::IsNullOrWhiteSpace($stderr)) { $output.Add($stderr) | Out-Null }
            $exitCode = 1
            throw "Command timed out after $TimeoutSeconds seconds."
        }

        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        if (-not [string]::IsNullOrWhiteSpace($stdout)) { $output.Add($stdout) | Out-Null }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) { $output.Add($stderr) | Out-Null }
        $exitCode = [int]$process.ExitCode
    }
    catch {
        $output.Add($_.Exception.Message) | Out-Null
        $exitCode = 1
    }

    $duration = [Math]::Round(((Get-Date) - $started).TotalSeconds, 3)
    $redacted = @($output | ForEach-Object { ConvertTo-RedactedText ([string]$_) })
    @(
        "> $commandText"
        "exitCode=$exitCode"
        "durationSeconds=$duration"
        ""
        $redacted
    ) | Set-Content -LiteralPath $logPath -Encoding utf8
    Add-Evidence -Path $logPath -Kind "log" -Status "created"

    $CommandRows.Add([pscustomobject]@{
        command = $commandText
        exitCode = $exitCode
        status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
        durationSeconds = $duration
        log = $logPath
    }) | Out-Null

    return [pscustomobject]@{
        ExitCode = $exitCode
        LogPath = $logPath
        Output = ($redacted -join [Environment]::NewLine)
    }
}

function Test-Phase3RuntimeReady {
    foreach ($uri in @("$ApiRoot/health", "$PreventionBaseUrl/health/live", $WebUrl)) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 5 -ErrorAction Stop
            if ([int]$response.StatusCode -lt 200 -or [int]$response.StatusCode -ge 400) {
                return $false
            }
        }
        catch {
            return $false
        }
    }

    return $true
}

function Invoke-StartRuntimeCommand {
    $name = "np-start"
    $logPath = Join-Path $LogsDir "$name.log"
    $stdout = Join-Path $LogsDir "$name.stdout.tmp.log"
    $stderr = Join-Path $LogsDir "$name.stderr.tmp.log"
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\np.ps1"), "start", "-NoBrowser")
    $commandText = "pwsh $($arguments -join ' ')"
    $started = Get-Date
    $exitCode = 1
    $note = ""

    try {
        $process = Start-Process -FilePath "pwsh" `
            -ArgumentList $arguments `
            -WorkingDirectory $RepoRoot `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr `
            -WindowStyle Hidden `
            -PassThru

        $deadline = (Get-Date).AddSeconds(300)
        do {
            if ($process.HasExited) {
                $exitCode = [int]$process.ExitCode
                break
            }

            if (Test-Phase3RuntimeReady) {
                $exitCode = 0
                $note = "Runtime endpoints became healthy while np start wrapper was still running; stopped wrapper process PID $($process.Id) after health proof."
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 1
                break
            }

            Start-Sleep -Seconds 2
        } while ((Get-Date) -lt $deadline)

        if (-not $process.HasExited -and $exitCode -ne 0) {
            $note = "np start did not exit and runtime endpoints did not become healthy within timeout."
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        $note = $_.Exception.Message
        $exitCode = 1
    }

    $duration = [Math]::Round(((Get-Date) - $started).TotalSeconds, 3)
    $output = New-Object System.Collections.Generic.List[string]
    if ($note) { $output.Add($note) | Out-Null }
    if (Test-Path -LiteralPath $stdout) { $output.Add((Get-Content -Raw -LiteralPath $stdout)) | Out-Null }
    if (Test-Path -LiteralPath $stderr) { $output.Add((Get-Content -Raw -LiteralPath $stderr)) | Out-Null }
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue

    $redacted = @($output | ForEach-Object { ConvertTo-RedactedText ([string]$_) })
    @(
        "> $commandText"
        "exitCode=$exitCode"
        "durationSeconds=$duration"
        ""
        $redacted
    ) | Set-Content -LiteralPath $logPath -Encoding utf8
    Add-Evidence -Path $logPath -Kind "log" -Status "created"

    $CommandRows.Add([pscustomobject]@{
        command = $commandText
        exitCode = $exitCode
        status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
        durationSeconds = $duration
        log = $logPath
    }) | Out-Null

    return [pscustomobject]@{
        ExitCode = $exitCode
        LogPath = $logPath
        Output = ($redacted -join [Environment]::NewLine)
    }
}

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory)][ValidateSet("GET", "POST")][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [AllowNull()][object]$Body = $null,
        [AllowNull()][string]$Token = $null,
        [int[]]$ExpectedStatusCodes = @(200),
        [string]$EvidenceName = ""
    )
    $uri = if ($Path.StartsWith("http", [StringComparison]::OrdinalIgnoreCase)) {
        $Path
    }
    else {
        $ApiBaseUrl.TrimEnd("/") + "/" + $Path.TrimStart("/")
    }
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }
    $parameters = @{
        Method = $Method
        Uri = $uri
        Headers = $headers
        TimeoutSec = 90
        UseBasicParsing = $true
        ErrorAction = "Stop"
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = ($Body | ConvertTo-Json -Depth $JsonDepth)
    }

    try {
        $response = Invoke-WebRequest @parameters
        $statusCode = [int]$response.StatusCode
        $content = [string]$response.Content
        if ($ExpectedStatusCodes -notcontains $statusCode) {
            throw "$Method $uri returned HTTP $statusCode; expected $($ExpectedStatusCodes -join ',')."
        }
        $json = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
        if ($EvidenceName) {
            $pathOut = Join-Path $ExportsDir $EvidenceName
            (ConvertTo-RedactedText $content) | Set-Content -LiteralPath $pathOut -Encoding utf8
            Add-Evidence -Path $pathOut -Kind "json" -Status "created"
        }
        return [pscustomobject]@{
            StatusCode = $statusCode
            Json = $json
            Raw = $content
            Uri = $uri
        }
    }
    catch {
        $message = $_.Exception.Message
        if ($_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $message = "$message :: $($_.ErrorDetails.Message)"
        }
        throw (ConvertTo-RedactedText "$Method $uri failed: $message")
    }
}

function Test-HttpStatus {
    param(
        [string]$Name,
        [string]$Uri,
        [bool]$Required = $true
    )
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -TimeoutSec 10 -ErrorAction Stop
        Add-Test -Name $Name -Status "PASS" -Detail "HTTP $([int]$response.StatusCode)"
        return $true
    }
    catch {
        $detail = $_.Exception.Message
        Add-Test -Name $Name -Status ($(if ($Required) { "FAIL" } else { "WARN" })) -Detail $detail
        if ($Required) {
            Add-Blocker -Severity "BLOCKER" -Area "runtime" -Command "GET $Uri" -RootCause $detail -EvidencePath "" -NextStep "Inspect local runtime logs and rerun np health."
        }
        return $false
    }
}

function Get-ResolvedProfiles {
    param([AllowNull()][object]$RunResponse)
    if ($null -eq $RunResponse -or $null -eq $RunResponse.run -or $null -eq $RunResponse.run.runOverrides) { return @() }
    $resolved = $RunResponse.run.runOverrides.resolved
    if ($null -ne $resolved -and $null -ne $resolved.degradationProfiles -and @($resolved.degradationProfiles).Count -gt 0) {
        return @($resolved.degradationProfiles | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    }
    if ($null -ne $resolved -and -not [string]::IsNullOrWhiteSpace([string]$resolved.degradationProfile)) {
        return @(([string]$resolved.degradationProfile) -split "[,+;|]" | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    }
    return @()
}

function Start-RuntimeScenario {
    param(
        [string]$ScenarioCode,
        [string[]]$DegradationProfiles,
        [string]$RunLabel,
        [int]$Seed
    )
    $body = [ordered]@{
        areaCode = $AreaCode
        scenarioCode = $ScenarioCode
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        seed = $Seed
        degradationProfile = if ($DegradationProfiles.Count -eq 1) { $DegradationProfiles[0] } else { ($DegradationProfiles -join "+") }
        degradationProfiles = $DegradationProfiles
        collectEvidence = [bool]$Evidence
        waitForCompletion = $true
        timeoutSeconds = $TimeoutSeconds
        allowParallelRun = $false
        runLabel = $RunLabel
    }
    return (Invoke-ApiJson -Method "POST" -Path "/control/runtime/runs" -Body $body -Token $AuthToken -EvidenceName "$RunLabel.start-response.json").Json
}

function Wait-RuntimeRunTerminal {
    param(
        [string]$RunId,
        [int]$WaitSeconds = 120
    )
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $last = $null
    do {
        $last = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/runs/$RunId" -Token $AuthToken).Json
        if (@("Completed", "Failed", "Rejected", "Cancelled") -contains [string]$last.status) {
            return $last
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $last
}

function Get-JsonIntValue {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Value -or
        $null -eq $Value.PSObject.Properties[$Name] -or
        $null -eq $Value.$Name) {
        return $null
    }

    return [int]$Value.$Name
}

function Get-StartedProcessingAttemptCount {
    param([AllowNull()][object]$Timing)
    if ($null -eq $Timing -or
        $null -eq $Timing.PSObject.Properties["stages"] -or
        $null -eq $Timing.stages) {
        return 0
    }

    $count = 0
    foreach ($stage in @($Timing.stages)) {
        if ([string]$stage.outcome -eq "Started" -and
            $null -ne $stage.PSObject.Properties["count"] -and
            $null -ne $stage.count) {
            $count += [int]$stage.count
        }
    }

    return $count
}

function Wait-RuntimeRunAuditConverged {
    param(
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][ValidateSet("clean", "missing-readings")][string]$Expectation,
        [int]$WaitSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $lastAudit = $null
    $lastTiming = $null
    $lastSignature = $null
    $stablePolls = 0
    $reason = "Audit did not converge before timeout."

    do {
        $lastAudit = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/runs/$RunId/audit" -Token $AuthToken).Json
        $lastTiming = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/runs/$RunId/timings" -Token $AuthToken).Json

        $expected = Get-JsonIntValue -Value $lastAudit -Name "expectedEvents"
        $accepted = Get-JsonIntValue -Value $lastAudit -Name "acceptedReadings"
        $missing = Get-JsonIntValue -Value $lastAudit -Name "missingEvents"
        $risk = Get-JsonIntValue -Value $lastAudit -Name "riskAssessments"
        $started = Get-StartedProcessingAttemptCount -Timing $lastTiming
        $signature = "$expected/$accepted/$missing/$risk/$started"
        if ($signature -eq $lastSignature) {
            $stablePolls++
        }
        else {
            $stablePolls = 0
            $lastSignature = $signature
        }

        if ($Expectation -eq "clean") {
            if ($null -ne $expected -and
                $expected -gt 0 -and
                $accepted -eq $expected -and
                $risk -eq $accepted -and
                $missing -eq 0 -and
                $started -eq 0) {
                return [pscustomobject]@{
                    Converged = $true
                    Audit = $lastAudit
                    Timing = $lastTiming
                    Reason = "Clean audit converged: accepted=$accepted risk=$risk expected=$expected missing=$missing."
                }
            }

            $reason = "Expected clean audit accepted==expected, risk==accepted, missing==0 and no started processing attempts; latest=$signature."
        }
        else {
            if ($null -ne $expected -and
                $expected -gt 0 -and
                $accepted -gt 0 -and
                $accepted -lt $expected -and
                $risk -eq $accepted -and
                $missing -gt 0 -and
                $started -eq 0 -and
                $stablePolls -ge 1) {
                return [pscustomobject]@{
                    Converged = $true
                    Audit = $lastAudit
                    Timing = $lastTiming
                    Reason = "Missing-readings audit converged: accepted=$accepted risk=$risk expected=$expected missing=$missing."
                }
            }

            $reason = "Expected degraded audit accepted<expected, risk==accepted, missing>0, no started processing attempts and stable counts; latest=$signature."
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    return [pscustomobject]@{
        Converged = $false
        Audit = $lastAudit
        Timing = $lastTiming
        Reason = $reason
    }
}

function Export-PostgresCsv {
    param(
        [string]$Name,
        [string]$Sql
    )
    $pathOut = Join-Path $ExportsDir $Name
    $args = @("exec", "-i", "np-postgres", "psql", "-U", "np", "-d", "natureprotector", "--csv")
    $output = $Sql | docker @args 2>&1
    $code = $LASTEXITCODE
    $output | Set-Content -LiteralPath $pathOut -Encoding utf8
    Add-Evidence -Path $pathOut -Kind "csv" -Status ($(if ($code -eq 0) { "created" } else { "failed" }))
    if ($code -ne 0) {
        Add-Blocker -Severity "HIGH" -Area "db" -Command "docker $($args -join ' ') < sql" -RootCause ($output -join "`n") -EvidencePath $pathOut -NextStep "Verify np-postgres container and table/schema names."
    }
    return [pscustomobject]@{ Path = $pathOut; ExitCode = $code; Rows = [Math]::Max(0, (@($output).Count - 1)) }
}

function Invoke-RabbitApi {
    param(
        [string]$Path,
        [string]$OutFile
    )
    $bytes = [Text.Encoding]::ASCII.GetBytes("${RabbitUser}:${RabbitPassword}")
    $headers = @{ Authorization = "Basic $([Convert]::ToBase64String($bytes))" }
    $uri = "http://127.0.0.1:15672/api/$Path"
    $outPath = Join-Path $ExportsDir $OutFile
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers $headers -TimeoutSec 20 -ErrorAction Stop
        ([string]$response.Content) | Set-Content -LiteralPath $outPath -Encoding utf8
        Add-Evidence -Path $outPath -Kind "json" -Status "created"
        Add-Test -Name "RabbitMQ management $Path" -Status "PASS" -Detail "HTTP $([int]$response.StatusCode)" -EvidencePath $outPath
        return $true
    }
    catch {
        $_.Exception.Message | Set-Content -LiteralPath $outPath -Encoding utf8
        Add-Evidence -Path $outPath -Kind "json" -Status "failed"
        Add-Test -Name "RabbitMQ management $Path" -Status "FAIL" -Detail $_.Exception.Message -EvidencePath $outPath
        Add-Blocker -Severity "HIGH" -Area "rabbitmq" -Command "GET $uri" -RootCause $_.Exception.Message -EvidencePath $outPath -NextStep "Inspect RabbitMQ container and management port."
        return $false
    }
}

function New-ComparisonRow {
    param(
        [string]$Name,
        [object]$RunResponse,
        [object]$Audit,
        [object]$Timings,
        [object]$Summary
    )
    $run = $RunResponse.run
    $area = if ($null -ne $Summary) { $Summary.areaOperationalState } else { $null }
    $score = if ($null -ne $Audit) { $Audit.scoreComponents } else { $null }
    return [pscustomobject]@{
        scenario = $Name
        runId = if ($null -ne $run) { $run.id } else { $null }
        scenarioCode = if ($null -ne $run) { $run.scenarioCode } else { $null }
        runLabel = if ($null -ne $RunResponse.requested) { $RunResponse.requested.orchestratorCorrelationId } else { $null }
        status = if ($null -ne $run) { $run.status } else { $null }
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        seed = if ($null -ne $run) { $run.executionSeed } else { $null }
        degradationProfiles = (Get-ResolvedProfiles -RunResponse $RunResponse) -join "+"
        expectedEvents = if ($null -ne $Audit) { $Audit.expectedEvents } else { $null }
        acceptedReadings = if ($null -ne $Audit) { $Audit.acceptedReadings } else { $null }
        missingEvents = if ($null -ne $Audit) { $Audit.missingEvents } else { $null }
        rejected = if ($null -ne $Audit) { $Audit.rejected } else { $null }
        quarantined = if ($null -ne $Audit) { $Audit.quarantined } else { $null }
        retryAttempts = if ($null -ne $Audit) { $Audit.retryAttempts } else { $null }
        riskAssessments = if ($null -ne $Audit) { $Audit.riskAssessments } else { $null }
        aggregateRiskScore = if ($null -ne $Audit -and $null -ne $Audit.areaSnapshot) { $Audit.areaSnapshot.aggregateRiskScore } else { $null }
        aggregateRiskLevel = if ($null -ne $Audit -and $null -ne $Audit.areaSnapshot) { $Audit.areaSnapshot.aggregateRiskLevel } else { $null }
        npScore = if ($null -ne $score) { $score.npScore } else { $null }
        confidenceFactor = if ($null -ne $score) { $score.confidenceFactor } else { $null }
        integrityFactor = if ($null -ne $score) { $score.integrityFactor } else { $null }
        alertState = if ($null -ne $area) { $area.alertState } else { $null }
        coverageStatus = if ($null -ne $area) { $area.coverageStatus } else { $null }
        freshnessStatus = if ($null -ne $area) { $area.freshnessStatus } else { $null }
        carryForwardStatus = if ($null -ne $area) { $area.carryForwardStatus } else { $null }
        attemptCount = if ($null -ne $Timings -and $null -ne $Timings.attempts) { $Timings.attempts.attemptCount } else { $null }
        successfulAttempts = if ($null -ne $Timings -and $null -ne $Timings.attempts) { $Timings.attempts.successfulAttempts } else { $null }
        failedAttempts = if ($null -ne $Timings -and $null -ne $Timings.attempts) { $Timings.attempts.failedAttempts } else { $null }
    }
}

function Invoke-UiSmoke {
    $scriptPath = Join-Path $RunRoot "ui-smoke.mjs"
    $resultPath = Join-Path $ExportsDir "ui-smoke-result.json"
    $screenshotLogin = Join-Path $ScreenshotsDir "ui-login.png"
    $screenshotDashboard = Join-Path $ScreenshotsDir "ui-dashboard.png"
    $screenshotRuns = Join-Path $ScreenshotsDir "ui-runs.png"
    $webUiPackageJson = (Join-Path $RepoRoot "webUI\package.json").Replace('\', '/')
    $script = @"
import { createRequire } from 'module';
import path from 'path';
const require = createRequire('$webUiPackageJson');
const { chromium } = require('playwright');
const result = { status: 'started', pages: [], errors: [] };
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 950 } });
  await page.goto('$WebUrl/login', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('body').waitFor({ timeout: 10000 });
  await page.screenshot({ path: '$($screenshotLogin.Replace('\', '/'))', fullPage: true });
  await page.fill('#usernameOrEmail', process.env.NP_PHASE3_UI_USERNAME || 'admin');
  await page.fill('#password', process.env.NP_PHASE3_UI_PASSWORD || '');
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForTimeout(2000);
  result.pages.push({ name: 'login', url: page.url(), title: await page.title() });
  await page.goto('$WebUrl/dashboard', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('body').waitFor({ timeout: 10000 });
  await page.waitForLoadState('networkidle', { timeout: 5000 }).catch(() => {});
  await page.screenshot({ path: '$($screenshotDashboard.Replace('\', '/'))', fullPage: true });
  result.pages.push({ name: 'dashboard', url: page.url(), textSample: (await page.locator('body').innerText()).slice(0, 500) });
  await page.goto('$WebUrl/runs', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('body').waitFor({ timeout: 10000 });
  await page.waitForLoadState('networkidle', { timeout: 5000 }).catch(() => {});
  await page.screenshot({ path: '$($screenshotRuns.Replace('\', '/'))', fullPage: true });
  result.pages.push({ name: 'runs', url: page.url(), textSample: (await page.locator('body').innerText()).slice(0, 500) });
  result.status = 'passed';
} catch (error) {
  result.status = 'failed';
  result.errors.push(String(error && error.stack ? error.stack : error));
} finally {
  await browser.close();
}
await import('fs/promises').then(fs => fs.writeFile('$($resultPath.Replace('\', '/'))', JSON.stringify(result, null, 2)));
if (result.status !== 'passed') process.exit(1);
"@
    $script | Set-Content -LiteralPath $scriptPath -Encoding utf8
    Add-Evidence -Path $scriptPath -Kind "script" -Status "created"
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) {
        Add-Test -Name "UI Playwright smoke" -Status "BLOCKED_BY_ENVIRONMENT" -Detail "node executable not found"
        Add-Blocker -Severity "MEDIUM" -Area "environment" -Command "node ui-smoke.mjs" -RootCause "node executable not found" -EvidencePath "" -NextStep "Install/enable Node.js and rerun UI smoke."
        return
    }
    $previousUiUser = $env:NP_PHASE3_UI_USERNAME
    $previousUiPassword = $env:NP_PHASE3_UI_PASSWORD
    try {
        $env:NP_PHASE3_UI_USERNAME = $AdminUsername
        $env:NP_PHASE3_UI_PASSWORD = $AdminPassword
        $result = Invoke-LoggedCommand -Name "ui-playwright-smoke" -Executable $node.Source -Arguments @($scriptPath) -TimeoutSeconds 120
    }
    finally {
        if ($null -eq $previousUiUser) { Remove-Item Env:NP_PHASE3_UI_USERNAME -ErrorAction SilentlyContinue } else { $env:NP_PHASE3_UI_USERNAME = $previousUiUser }
        if ($null -eq $previousUiPassword) { Remove-Item Env:NP_PHASE3_UI_PASSWORD -ErrorAction SilentlyContinue } else { $env:NP_PHASE3_UI_PASSWORD = $previousUiPassword }
    }
    Add-Evidence -Path $resultPath -Kind "json" -Status ($(if ($result.ExitCode -eq 0) { "created" } else { "failed" }))
    foreach ($shot in @($screenshotLogin, $screenshotDashboard, $screenshotRuns)) {
        if (Test-Path -LiteralPath $shot) { Add-Evidence -Path $shot -Kind "screenshot" -Status "created" }
    }
    if ($result.ExitCode -eq 0) {
        Add-Test -Name "UI Playwright smoke" -Status "PASS" -Detail "Login, dashboard and runs pages loaded." -EvidencePath $resultPath
    }
    else {
        Add-Test -Name "UI Playwright smoke" -Status "FAIL" -Detail "Playwright smoke failed." -EvidencePath $result.LogPath
        Add-Rfx -Severity "MEDIUM" -Title "UI smoke did not complete locally" -EvidencePath $result.LogPath -RecommendedPhase4Fix "Classify UI route/login failure after reviewing Playwright result and browser console output."
    }
}

function Get-SimulatorProcessSnapshot {
    try {
        return @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object {
            $_.CommandLine -and $_.CommandLine -match "NatureProtector.Simulator.Host"
        } | Select-Object ProcessId, Name, CommandLine)
    }
    catch {
        $pathOut = Join-Path $LogsDir "simulator-process-snapshot-limitation.txt"
        "Win32_Process query unavailable: $($_.Exception.Message)" | Set-Content -LiteralPath $pathOut -Encoding utf8
        Add-Evidence -Path $pathOut -Kind "log" -Status "created"
        Add-Test -Name "Simulator process snapshot" -Status "BLOCKED_BY_ENVIRONMENT" -Detail $_.Exception.Message -EvidencePath $pathOut
        return @()
    }
}

function Complete-Reports {
    $completedAt = (Get-Date).ToUniversalTime()
    $durationSeconds = [Math]::Round(($completedAt - $RunStartedAtUtc).TotalSeconds, 3)

    $CommandRows | Export-Csv -LiteralPath (Join-Path $RunRoot "COMMANDS-RUN.md.csv") -NoTypeInformation -Encoding utf8
    Add-Evidence -Path (Join-Path $RunRoot "COMMANDS-RUN.md.csv") -Kind "csv" -Status "created"

    $commandsMd = New-Object System.Collections.Generic.List[string]
    $commandsMd.Add("# Commands Run")
    $commandsMd.Add("")
    foreach ($row in $CommandRows) {
        $commandsMd.Add("- ``$($row.command)`` -> $($row.status) exit=$($row.exitCode) log=$($row.log)")
    }
    Save-TextFile -Path (Join-Path $RunRoot "COMMANDS-RUN.md") -Lines $commandsMd

    if ($TestRows.Count -eq 0) { Add-Test -Name "phase3-harness" -Status "FAIL" -Detail "No tests were recorded." }
    $TestRows | Export-Csv -LiteralPath (Join-Path $RunRoot "TESTS-RUN.csv") -NoTypeInformation -Encoding utf8
    Add-Evidence -Path (Join-Path $RunRoot "TESTS-RUN.csv") -Kind "csv" -Status "created"

    if ($Blockers.Count -eq 0) {
        $Blockers.Add([pscustomobject]@{ severity="INFO"; area="none"; command=""; rootCause="No blockers recorded."; evidence=""; nextStep="" }) | Out-Null
    }
    $Blockers | Export-Csv -LiteralPath (Join-Path $RunRoot "BLOCKERS.csv") -NoTypeInformation -Encoding utf8
    Add-Evidence -Path (Join-Path $RunRoot "BLOCKERS.csv") -Kind "csv" -Status "created"

    if ($RfxRows.Count -eq 0) {
        $RfxRows.Add([pscustomobject]@{ severity="INFO"; title="No Phase 4 RFX candidates recorded by this harness."; evidence=""; recommendedPhase4Fix="" }) | Out-Null
    }
    $RfxRows | Export-Csv -LiteralPath (Join-Path $RunRoot "RFX-CANDIDATES.csv") -NoTypeInformation -Encoding utf8
    Add-Evidence -Path (Join-Path $RunRoot "RFX-CANDIDATES.csv") -Kind "csv" -Status "created"

    $ChangedRows.Add([pscustomobject]@{
        path = Join-Path $RepoRoot "scripts\validation\Invoke-LocalFunctionalValidation.ps1"
        changeType = "created_or_updated"
        reason = "Phase 3 local functional validation harness"
    }) | Out-Null
    $ChangedRows | Export-Csv -LiteralPath (Join-Path $RunRoot "FILES-CHANGED.csv") -NoTypeInformation -Encoding utf8
    Add-Evidence -Path (Join-Path $RunRoot "FILES-CHANGED.csv") -Kind "csv" -Status "created"

    $handover = @(
        "# Phase 3 Handover",
        "",
        "- Verdict: $Verdict",
        "- RunRoot: $RunRoot",
        "- StartedAtUtc: $($RunStartedAtUtc.ToString("o"))",
        "- CompletedAtUtc: $($completedAt.ToString("o"))",
        "- DurationSeconds: $durationSeconds",
        "- Scope: local functional validation only; no cloud, no commits, no global Docker prune.",
        "- Entrypoint under validation: scripts\\np.ps1",
        "- Functional harness: scripts\\validation\\Invoke-LocalFunctionalValidation.ps1"
    )
    Save-TextFile -Path (Join-Path $RunRoot "PHASE-3-HANDOVER.md") -Lines $handover

    $bcMdPath = Join-Path $RunRoot "BC-COMPARISON.md"
    $bcJsonPath = Join-Path $RunRoot "BC-COMPARISON.json"
    $bcCsvPath = Join-Path $RunRoot "BC-COMPARISON.csv"
    if (-not (Test-Path -LiteralPath $bcMdPath -PathType Leaf)) {
        Save-TextFile -Path $bcMdPath -Lines @(
            "# B/C Comparison",
            "",
            "- Status: not produced.",
            "- Reason: validation stopped before both scenario_b and scenario_c completed.",
            "- See BLOCKERS.csv and TESTS-RUN.csv."
        )
    }
    if (-not (Test-Path -LiteralPath $bcJsonPath -PathType Leaf)) {
        Write-JsonFile -Path $bcJsonPath -Value ([ordered]@{
            generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
            status = "not_produced"
            reason = "validation stopped before both scenario_b and scenario_c completed"
        })
    }
    if (-not (Test-Path -LiteralPath $bcCsvPath -PathType Leaf)) {
        @([pscustomobject]@{
            scenario = "not_produced"
            runId = ""
            status = "validation_stopped_before_bc_completion"
        }) | Export-Csv -LiteralPath $bcCsvPath -NoTypeInformation -Encoding utf8
        Add-Evidence -Path $bcCsvPath -Kind "csv" -Status "created"
    }

    $blockingCount = @($Blockers | Where-Object { $_.severity -ne "INFO" }).Count
    $summary = @(
        "# Functional Validation Summary",
        "",
        "- Verdict: $Verdict",
        "- CleanRoom: $([bool]$CleanRoom)",
        "- Full: $([bool]$Full)",
        "- Evidence: $([bool]$Evidence)",
        "- UI: $([bool]$Ui)",
        "- Area: $AreaCode",
        "- Scenario B run: $(if ($null -ne $RunB -and $null -ne $RunB.run) { $RunB.run.id } else { 'not-run' })",
        "- Scenario C run: $(if ($null -ne $RunC -and $null -ne $RunC.run) { $RunC.run.id } else { 'not-run' })",
        "- Tests recorded: $($TestRows.Count)",
        "- Blockers recorded: $blockingCount"
    )
    Save-TextFile -Path (Join-Path $RunRoot "FUNCTIONAL-VALIDATION-SUMMARY.md") -Lines $summary

    $dbLines = @(
        "# DB Validation",
        "",
        "- Exports:",
        "  - exports/db-simulation-runs.csv",
        "  - exports/db-processing-attempts.csv",
        "  - exports/db-risk-assessments.csv",
        "- Status: see TESTS-RUN.csv and BLOCKERS.csv."
    )
    Save-TextFile -Path (Join-Path $RunRoot "DB-VALIDATION.md") -Lines $dbLines

    $rabbitLines = @(
        "# RabbitMQ Validation",
        "",
        "- Exports:",
        "  - exports/rabbitmq-overview.json",
        "  - exports/rabbitmq-queues.json",
        "  - exports/rabbitmq-bindings.json",
        "- Status: see TESTS-RUN.csv and BLOCKERS.csv."
    )
    Save-TextFile -Path (Join-Path $RunRoot "RABBITMQ-VALIDATION.md") -Lines $rabbitLines

    $uiLines = @(
        "# UI Validation",
        "",
        "- Playwright requested: $([bool]$Ui)",
        "- Screenshots directory: $ScreenshotsDir",
        "- Result JSON: exports/ui-smoke-result.json",
        "- Status: see TESTS-RUN.csv."
    )
    Save-TextFile -Path (Join-Path $RunRoot "UI-VALIDATION.md") -Lines $uiLines

    $next = @(
        "# Next Steps",
        "",
        "- Do not advance to Phase 4 without approval.",
        "- If verdict is PASS, Phase 4 can focus on RFX remediation and deeper functional fixes.",
        "- If verdict is FAILED, use RFX-CANDIDATES.csv as the Phase 4 queue.",
        "- If verdict is BLOCKED, clear the environment blocker first and rerun this harness."
    )
    Save-TextFile -Path (Join-Path $RunRoot "NEXT-STEPS.md") -Lines $next

    Add-Evidence -Path (Join-Path $RunRoot "EVIDENCE-MANIFEST.csv") -Kind "csv" -Status "created"
    $EvidenceRows | Export-Csv -LiteralPath (Join-Path $RunRoot "EVIDENCE-MANIFEST.csv") -NoTypeInformation -Encoding utf8
}

try {
    Write-JsonFile -Path (Join-Path $RunRoot "run-spec.json") -Value ([ordered]@{
        runRoot = $RunRoot
        sourceRepoRoot = $SourceRepoRoot
        repoRoot = $RepoRoot
        cleanCloneRoot = if ($CleanRoom) { $RepoRoot } else { $null }
        apiRoot = $ApiRoot
        apiBaseUrl = $ApiBaseUrl
        preventionBaseUrl = $PreventionBaseUrl
        webUrl = $WebUrl
        areaCode = $AreaCode
        smoke = [bool]$Smoke
        full = [bool]$Full
        evidence = [bool]$Evidence
        ui = [bool]$Ui
        cleanRoom = [bool]$CleanRoom
        sensorCount = $SensorCount
        numberOfCycles = $NumberOfCycles
        intervalSeconds = $IntervalSeconds
        timeoutSeconds = $TimeoutSeconds
    })

    if ($CleanRoom) {
        foreach ($step in @(
            @{ name = "np-doctor-before-mutation"; args = @("doctor"); timeout = 300 },
            @{ name = "np-init-local"; args = @("init-local", "-Force"); timeout = 180 },
            @{ name = "np-prepare-local"; args = @("prepare-local"); timeout = 1800 },
            @{ name = "np-clean-local"; args = @("clean-local"); timeout = 600 },
            @{ name = "np-doctor-after-prepare"; args = @("doctor"); timeout = 300 },
            @{ name = "np-up"; args = @("up"); timeout = 900 },
            @{ name = "np-start"; args = @("start", "-NoBrowser"); timeout = 900 },
            @{ name = "np-health"; args = @("health"); timeout = 300 }
        )) {
            $result = if ($step.name -eq "np-start") {
                Invoke-StartRuntimeCommand
            }
            else {
                Invoke-LoggedCommand -Name $step.name -Executable "pwsh" -Arguments (@("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\np.ps1")) + $step.args) -TimeoutSeconds $step.timeout
            }
            Add-Test -Name $step.name -Status ($(if ($result.ExitCode -eq 0) { "PASS" } else { "FAIL" })) -Detail "exit=$($result.ExitCode)" -EvidencePath $result.LogPath
            if ($result.ExitCode -ne 0) {
                Add-Blocker -Severity "BLOCKER" -Area "environment" -Command "scripts\\np.ps1 $($step.args -join ' ')" -RootCause "Clean-room startup command failed. $($result.Output)" -EvidencePath $result.LogPath -NextStep "Fix local runtime precondition and rerun Phase 3."
                throw "Clean-room startup failed at $($step.name)."
            }
        }
    }

    Test-HttpStatus -Name "Backoffice API /health" -Uri "$ApiRoot/health" | Out-Null
    Test-HttpStatus -Name "Prevention /health/live" -Uri "$PreventionBaseUrl/health/live" | Out-Null
    Test-HttpStatus -Name "Prevention /health/ready" -Uri "$PreventionBaseUrl/health/ready" -Required:$false | Out-Null
    Test-HttpStatus -Name "webUI HTTP" -Uri $WebUrl | Out-Null

    $login = (Invoke-ApiJson -Method "POST" -Path "/users-roles/login" -Body @{ usernameOrEmail = $AdminUsername; password = $AdminPassword } -EvidenceName "auth-login-redacted.json").Json
    if ($null -eq $login -or [string]::IsNullOrWhiteSpace([string]$login.token)) {
        Add-Blocker -Severity "BLOCKER" -Area "auth" -Command "POST /api/users-roles/login" -RootCause "Login returned no token." -EvidencePath (Join-Path $ExportsDir "auth-login-redacted.json") -NextStep "Inspect local admin bootstrap and auth API."
        throw "Login returned no token."
    }
    $AuthToken = [string]$login.token
    Add-Test -Name "API login admin" -Status "PASS" -Detail "Token returned for $($login.username); roles=$($login.roles -join '+')" -EvidencePath (Join-Path $ExportsDir "auth-login-redacted.json")

    $me = (Invoke-ApiJson -Method "GET" -Path "/users-roles/me" -Token $AuthToken -EvidenceName "auth-me.json").Json
    Add-Test -Name "Protected /users-roles/me" -Status "PASS" -Detail "Authenticated as $($me.username)" -EvidencePath (Join-Path $ExportsDir "auth-me.json")

    $capabilities = (Invoke-ApiJson -Method "GET" -Path "/users-roles/me/capabilities" -Token $AuthToken -EvidenceName "auth-capabilities.json").Json
    $requiredCaps = @("run.read", "simulation.execute", "area.read", "scenario.read")
    $missingCaps = @($requiredCaps | Where-Object { @($capabilities.capabilities) -notcontains $_ })
    if ($missingCaps.Count -gt 0) {
        Add-Blocker -Severity "BLOCKER" -Area "auth" -Command "GET /api/users-roles/me/capabilities" -RootCause "Missing capabilities: $($missingCaps -join ', ')" -EvidencePath (Join-Path $ExportsDir "auth-capabilities.json") -NextStep "Fix local admin role/capability bootstrap."
        throw "Missing required capabilities."
    }
    Add-Test -Name "Admin capabilities" -Status "PASS" -Detail "Required capabilities present." -EvidencePath (Join-Path $ExportsDir "auth-capabilities.json")

    $areas = (Invoke-ApiJson -Method "GET" -Path "/control/areas" -Token $AuthToken -EvidenceName "areas.json").Json
    Add-Test -Name "Areas endpoint" -Status "PASS" -Detail "areas=$(@($areas).Count)" -EvidencePath (Join-Path $ExportsDir "areas.json")
    $scenarios = (Invoke-ApiJson -Method "GET" -Path "/control/areas/$AreaCode/scenarios" -Token $AuthToken -EvidenceName "area-scenarios.json").Json
    $scenarioCodes = @($scenarios | ForEach-Object { $_.code })
    foreach ($requiredScenario in @("scenario_b", "scenario_c")) {
        if ($scenarioCodes -notcontains $requiredScenario) {
            Add-Blocker -Severity "BLOCKER" -Area "catalog" -Command "GET /api/control/areas/$AreaCode/scenarios" -RootCause "Missing $requiredScenario." -EvidencePath (Join-Path $ExportsDir "area-scenarios.json") -NextStep "Inspect seeded local catalog."
            throw "Missing required scenario $requiredScenario."
        }
    }
    Add-Test -Name "Area scenarios B/C" -Status "PASS" -Detail "scenario_b and scenario_c present." -EvidencePath (Join-Path $ExportsDir "area-scenarios.json")

    $summaryBefore = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30" -Token $AuthToken -EvidenceName "runtime-summary-before.json").Json
    Add-Test -Name "Runtime summary before" -Status "PASS" -Detail "pipeline inbox=$($summaryBefore.pipeline.inboxTotal)" -EvidencePath (Join-Path $ExportsDir "runtime-summary-before.json")

    if ($Full -or $Smoke) {
        $RunB = Start-RuntimeScenario -ScenarioCode "scenario_b" -DegradationProfiles @("none") -RunLabel "phase3-smoke-b" -Seed 2026070601
        if ($null -ne $RunB.run -and $RunB.run.status -notin @("Completed", "Failed", "Rejected", "Cancelled")) {
            $RunB.run = Wait-RuntimeRunTerminal -RunId $RunB.run.id -WaitSeconds 120
        }
        Write-JsonFile -Path (Join-Path $ExportsDir "scenario-b-run.json") -Value $RunB
        if ($null -eq $RunB.run -or $RunB.run.status -ne "Completed") {
            Add-Blocker -Severity "BLOCKER" -Area "functional" -Command "POST /api/control/runtime/runs scenario_b" -RootCause "scenario_b did not complete. Status=$($RunB.status)" -EvidencePath (Join-Path $ExportsDir "scenario-b-run.json") -NextStep "Phase 4: inspect runtime orchestration and Simulator logs."
            throw "scenario_b did not complete."
        }
        $auditWaitB = Wait-RuntimeRunAuditConverged -RunId $RunB.run.id -Expectation "clean" -WaitSeconds $TimeoutSeconds
        $AuditB = $auditWaitB.Audit
        $TimingB = $auditWaitB.Timing
        Write-JsonFile -Path (Join-Path $ExportsDir "scenario-b-audit.json") -Value $AuditB
        Write-JsonFile -Path (Join-Path $ExportsDir "scenario-b-timings.json") -Value $TimingB
        $SummaryAfterB = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30" -Token $AuthToken -EvidenceName "runtime-summary-after-b.json").Json
        Add-Test -Name "scenario_b audit convergence wait" -Status ($(if ($auditWaitB.Converged) { "PASS" } else { "FAIL" })) -Detail $auditWaitB.Reason -EvidencePath (Join-Path $ExportsDir "scenario-b-timings.json")
        if (-not $auditWaitB.Converged -or [int]$AuditB.acceptedReadings -le 0 -or [int]$AuditB.riskAssessments -le 0 -or [int]$AuditB.missingEvents -ne 0) {
            Add-Blocker -Severity "BLOCKER" -Area "functional" -Command "scenario_b audit" -RootCause "scenario_b audit did not prove clean accepted/risk path after waiting for pipeline convergence. $($auditWaitB.Reason)" -EvidencePath (Join-Path $ExportsDir "scenario-b-audit.json") -NextStep "Inspect product publishing/Prevention pipeline if accepted readings never reach expectedEvents."
            Add-Rfx -Severity "HIGH" -Title "scenario_b with degradationProfile=none produced missing events after convergence wait" -EvidencePath (Join-Path $ExportsDir "scenario-b-audit.json") -RecommendedPhase4Fix "Inspect Simulator publishing, RabbitMQ delivery, Prevention processing, and expected event arithmetic. Current evidence showed acceptedReadings=$($AuditB.acceptedReadings), riskAssessments=$($AuditB.riskAssessments), missingEvents=$($AuditB.missingEvents)."
        }
        else {
            Add-Test -Name "scenario_b end-to-end" -Status "PASS" -Detail "accepted=$($AuditB.acceptedReadings) risk=$($AuditB.riskAssessments) missing=$($AuditB.missingEvents)" -EvidencePath (Join-Path $ExportsDir "scenario-b-audit.json")
        }

        $RunC = Start-RuntimeScenario -ScenarioCode "scenario_c" -DegradationProfiles @("missing-readings") -RunLabel "phase3-smoke-c" -Seed 2026070602
        if ($null -ne $RunC.run -and $RunC.run.status -notin @("Completed", "Failed", "Rejected", "Cancelled")) {
            $RunC.run = Wait-RuntimeRunTerminal -RunId $RunC.run.id -WaitSeconds 120
        }
        Write-JsonFile -Path (Join-Path $ExportsDir "scenario-c-run.json") -Value $RunC
        if ($null -eq $RunC.run -or $RunC.run.status -ne "Completed") {
            Add-Blocker -Severity "BLOCKER" -Area "functional" -Command "POST /api/control/runtime/runs scenario_c" -RootCause "scenario_c did not complete. Status=$($RunC.status)" -EvidencePath (Join-Path $ExportsDir "scenario-c-run.json") -NextStep "Phase 4: inspect runtime orchestration and Simulator logs."
            throw "scenario_c did not complete."
        }
        $auditWaitC = Wait-RuntimeRunAuditConverged -RunId $RunC.run.id -Expectation "missing-readings" -WaitSeconds $TimeoutSeconds
        $AuditC = $auditWaitC.Audit
        $TimingC = $auditWaitC.Timing
        Write-JsonFile -Path (Join-Path $ExportsDir "scenario-c-audit.json") -Value $AuditC
        Write-JsonFile -Path (Join-Path $ExportsDir "scenario-c-timings.json") -Value $TimingC
        $SummaryAfterC = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/summary?areaCode=$AreaCode&recentMinutes=30" -Token $AuthToken -EvidenceName "runtime-summary-after-c.json").Json
        $profilesC = Get-ResolvedProfiles -RunResponse $RunC
        Add-Test -Name "scenario_c audit convergence wait" -Status ($(if ($auditWaitC.Converged) { "PASS" } else { "FAIL" })) -Detail $auditWaitC.Reason -EvidencePath (Join-Path $ExportsDir "scenario-c-timings.json")
        if (-not $auditWaitC.Converged -or $profilesC -notcontains "missing-readings" -or [int]$AuditC.missingEvents -le 0 -or [int]$AuditC.acceptedReadings -le 0 -or [int]$AuditC.riskAssessments -le 0) {
            Add-Blocker -Severity "BLOCKER" -Area "functional" -Command "scenario_c audit" -RootCause "scenario_c did not prove missing-readings path after waiting for pipeline convergence. $($auditWaitC.Reason)" -EvidencePath (Join-Path $ExportsDir "scenario-c-audit.json") -NextStep "Inspect degradation profile handling and processing audit."
        }
        else {
            Add-Test -Name "scenario_c end-to-end" -Status "PASS" -Detail "accepted=$($AuditC.acceptedReadings) risk=$($AuditC.riskAssessments) missing=$($AuditC.missingEvents)" -EvidencePath (Join-Path $ExportsDir "scenario-c-audit.json")
        }

        $rowB = New-ComparisonRow -Name "scenario_b" -RunResponse $RunB -Audit $AuditB -Timings $TimingB -Summary $SummaryAfterB
        $rowC = New-ComparisonRow -Name "scenario_c" -RunResponse $RunC -Audit $AuditC -Timings $TimingC -Summary $SummaryAfterC
        $comparisonRows = @($rowB, $rowC)
        $comparisonRows | Export-Csv -LiteralPath (Join-Path $RunRoot "BC-COMPARISON.csv") -NoTypeInformation -Encoding utf8
        Add-Evidence -Path (Join-Path $RunRoot "BC-COMPARISON.csv") -Kind "csv" -Status "created"
        Write-JsonFile -Path (Join-Path $RunRoot "BC-COMPARISON.json") -Value ([ordered]@{
            generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
            rows = $comparisonRows
            delta = [ordered]@{
                missingEvents = ([int]$rowC.missingEvents - [int]$rowB.missingEvents)
                acceptedReadings = ([int]$rowC.acceptedReadings - [int]$rowB.acceptedReadings)
                riskAssessments = ([int]$rowC.riskAssessments - [int]$rowB.riskAssessments)
            }
        })
        Save-TextFile -Path (Join-Path $RunRoot "BC-COMPARISON.md") -Lines @(
            "# B/C Comparison",
            "",
            "- Scenario B run: $($rowB.runId)",
            "- Scenario C run: $($rowC.runId)",
            "- Scenario B accepted/risk/missing/rejected/quarantined: $($rowB.acceptedReadings)/$($rowB.riskAssessments)/$($rowB.missingEvents)/$($rowB.rejected)/$($rowB.quarantined)",
            "- Scenario C accepted/risk/missing/rejected/quarantined: $($rowC.acceptedReadings)/$($rowC.riskAssessments)/$($rowC.missingEvents)/$($rowC.rejected)/$($rowC.quarantined)",
            "- Delta missing events C-B: $([int]$rowC.missingEvents - [int]$rowB.missingEvents)",
            "- Coverage B/C: $($rowB.coverageStatus) / $($rowC.coverageStatus)",
            "- Freshness B/C: $($rowB.freshnessStatus) / $($rowC.freshnessStatus)"
        )
        Add-Test -Name "B/C comparison" -Status "PASS" -Detail "Comparison CSV/JSON/MD created." -EvidencePath (Join-Path $RunRoot "BC-COMPARISON.md")
    }

    Export-PostgresCsv -Name "db-simulation-runs.csv" -Sql 'select * from control.simulation_runs order by "CreatedAt" desc limit 100;' | Out-Null
    Export-PostgresCsv -Name "db-processing-attempts.csv" -Sql 'select * from pipeline.processing_attempts order by "StartedAt" desc limit 200;' | Out-Null
    Export-PostgresCsv -Name "db-risk-assessments.csv" -Sql 'select * from projection.risk_assessment_log order by "CreatedAt" desc limit 200;' | Out-Null
    Add-Test -Name "Postgres exports" -Status "PASS" -Detail "Simulation runs, processing attempts and risk assessments exported." -EvidencePath (Join-Path $ExportsDir "db-simulation-runs.csv")

    Invoke-RabbitApi -Path "overview" -OutFile "rabbitmq-overview.json" | Out-Null
    Invoke-RabbitApi -Path "queues" -OutFile "rabbitmq-queues.json" | Out-Null
    Invoke-RabbitApi -Path "bindings" -OutFile "rabbitmq-bindings.json" | Out-Null

    $operationalHealth = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/observability/health" -Token $AuthToken -EvidenceName "runtime-operational-health.json").Json
    Add-Test -Name "Runtime operational health" -Status "PASS" -Detail "components=$(@($operationalHealth.components).Count)" -EvidencePath (Join-Path $ExportsDir "runtime-operational-health.json")
    $rabbitFromApi = (Invoke-ApiJson -Method "GET" -Path "/control/runtime/observability/rabbitmq" -Token $AuthToken -EvidenceName "runtime-rabbitmq-api.json").Json
    Add-Test -Name "Runtime RabbitMQ API metrics" -Status "PASS" -Detail "queues=$(@($rabbitFromApi.queues).Count) status=$($rabbitFromApi.collectionStatus)" -EvidencePath (Join-Path $ExportsDir "runtime-rabbitmq-api.json")

    if ($Ui) {
        Invoke-UiSmoke
    }

    $simProcess = @(Get-SimulatorProcessSnapshot)
    $simProcess | Select-Object ProcessId, Name, CommandLine | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $LogsDir "simulator-processes-before-stop.json") -Encoding utf8
    Add-Evidence -Path (Join-Path $LogsDir "simulator-processes-before-stop.json") -Kind "json" -Status "created"
    if ($simProcess.Count -eq 0) {
        Add-Test -Name "Simulator not persistent before stop" -Status "PASS" -Detail "No Simulator.Host process found."
    }
    else {
        Add-Test -Name "Simulator not persistent before stop" -Status "FAIL" -Detail "Found $($simProcess.Count) Simulator.Host process(es)." -EvidencePath (Join-Path $LogsDir "simulator-processes-before-stop.json")
        Add-Rfx -Severity "HIGH" -Title "Simulator.Host process remained alive after API-launched runs" -EvidencePath (Join-Path $LogsDir "simulator-processes-before-stop.json") -RecommendedPhase4Fix "Inspect process lifecycle in local runtime orchestration."
    }
}
catch {
    if ($Verdict -eq "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_PASS") {
        Add-Blocker -Severity "BLOCKER" -Area "functional" -Command "Invoke-LocalFunctionalValidation" -RootCause $_.Exception.Message -EvidencePath "" -NextStep "Inspect logs and rerun after fixing the failing phase."
    }
    Add-Test -Name "phase3 harness exception" -Status "FAIL" -Detail $_.Exception.Message
}
finally {
    $stop = Invoke-LoggedCommand -Name "np-stop" -Executable "pwsh" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\np.ps1"), "stop") -TimeoutSeconds 300
    Add-Test -Name "np-stop" -Status ($(if ($stop.ExitCode -eq 0) { "PASS" } else { "FAIL" })) -Detail "exit=$($stop.ExitCode)" -EvidencePath $stop.LogPath
    $down = Invoke-LoggedCommand -Name "np-down" -Executable "pwsh" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\np.ps1"), "down") -TimeoutSeconds 300
    Add-Test -Name "np-down" -Status ($(if ($down.ExitCode -eq 0) { "PASS" } else { "FAIL" })) -Detail "exit=$($down.ExitCode)" -EvidencePath $down.LogPath

    $containers = & docker ps -a --filter "name=np-" --format "{{json .}}" 2>&1
    $containers | Set-Content -LiteralPath (Join-Path $LogsDir "docker-containers-final.jsonl") -Encoding utf8
    Add-Evidence -Path (Join-Path $LogsDir "docker-containers-final.jsonl") -Kind "jsonl" -Status "created"
    $simAfter = @(Get-SimulatorProcessSnapshot)
    $simAfter | Select-Object ProcessId, Name, CommandLine | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $LogsDir "simulator-processes-after-stop.json") -Encoding utf8
    Add-Evidence -Path (Join-Path $LogsDir "simulator-processes-after-stop.json") -Kind "json" -Status "created"
    if ($simAfter.Count -eq 0) {
        Add-Test -Name "Simulator not persistent after stop" -Status "PASS" -Detail "No Simulator.Host process found."
    }
    else {
        Add-Test -Name "Simulator not persistent after stop" -Status "FAIL" -Detail "Found $($simAfter.Count) Simulator.Host process(es)." -EvidencePath (Join-Path $LogsDir "simulator-processes-after-stop.json")
    }

    if (@($TestRows | Where-Object { $_.status -eq "FAIL" -and $_.name -ne "Prevention /health/ready" }).Count -gt 0 -and $Verdict -eq "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_PASS") {
        $Verdict = "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_FAILED"
    }
    Complete-Reports
    if ($DockerConfigWasIsolated) {
        if ($null -eq $PreviousDockerConfig) {
            Remove-Item Env:DOCKER_CONFIG -ErrorAction SilentlyContinue
        }
        else {
            $env:DOCKER_CONFIG = $PreviousDockerConfig
        }
    }
    Write-Host $Verdict
    Write-Host "RunRoot=$RunRoot"
    if ($Verdict -eq "PHASE_3_LOCAL_FUNCTIONAL_VALIDATION_PASS") { exit 0 }
    exit 1
}
