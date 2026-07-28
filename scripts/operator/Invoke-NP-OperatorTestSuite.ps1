[CmdletBinding()]
param(
    [ValidateSet('Smoke', 'Functional', 'Full')]
    [string]$Profile = 'Functional',
    [string]$RepositoryRoot = 'C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector',
    [string]$ExternalResultsRoot = '',
    [switch]$StopOnFailure,
    [switch]$PreserveDockerVolumes,
    [switch]$SkipFrontendBrowserInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'NatureProtector.sln') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'docker-compose.yml') -PathType Leaf)) {
    throw "NatureProtector repository not found at $RepositoryRoot"
}

Import-Module (Join-Path $RepositoryRoot 'scripts\acceptance\modules\Acceptance.Common.psm1') -Force -ErrorAction Stop

$runId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
$runRoot = Join-Path $RepositoryRoot "artifacts\final-acceptance\operator-$runId"
$componentsRoot = Join-Path $runRoot 'components'
New-Item -ItemType Directory -Force -Path $componentsRoot | Out-Null
if (-not [string]::IsNullOrWhiteSpace($ExternalResultsRoot)) {
    $ExternalResultsRoot = [System.IO.Path]::GetFullPath($ExternalResultsRoot)
    New-Item -ItemType Directory -Force -Path $ExternalResultsRoot | Out-Null
}

$startedAt = (Get-Date).ToUniversalTime()
$stageRows = [System.Collections.Generic.List[object]]::new()
$commandRows = [System.Collections.Generic.List[object]]::new()
$activeStageRoot = $runRoot
$overallException = $null
$lockPath = Join-Path $RepositoryRoot 'artifacts\operator-test-kit\.operator-suite.lock'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $lockPath) | Out-Null
if (Test-Path -LiteralPath $lockPath) {
    throw "Another operator test suite may already be running. Lock: $lockPath"
}
[ordered]@{ processId = $PID; runId = $runId; startedAtUtc = $startedAt.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath $lockPath -Encoding utf8

function Convert-ToSafeText {
    param([AllowNull()][string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $safe = $Text -replace '(?i)(Authorization\s*[:=]\s*Bearer\s+)[A-Za-z0-9._-]+', '${1}<redacted>'
    $safe = $safe -replace '(?i)("token"\s*:\s*")[^"]+("\s*)', '${1}<redacted>${2}'
    $safe = $safe -replace '(?i)(password\s*[:=]\s*)[^,\s}]+', '${1}<redacted>'
    return $safe
}

function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Executable,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = $RepositoryRoot,
        [int]$TimeoutSeconds = 1800,
        [hashtable]$Environment = @{},
        [int[]]$AcceptedExitCodes = @(0)
    )

    $logs = Join-Path $activeStageRoot 'logs'
    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    $stdout = Join-Path $logs "$Id.stdout.log"
    $stderr = Join-Path $logs "$Id.stderr.log"
    $combined = Join-Path $logs "$Id.log"
    $commandText = "$Executable " + (($Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ')
    Write-Host "`n> $commandText"

    $invocation = New-NpAcceptanceProcessInvocation -Executable $Executable -Arguments $Arguments
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = [string]$invocation.FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @($invocation.Arguments)) { [void]$startInfo.ArgumentList.Add([string]$argument) }
    foreach ($entry in $Environment.GetEnumerator()) { $startInfo.Environment[[string]$entry.Key] = [string]$entry.Value }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = Get-Date
    try {
        if (-not $process.Start()) { throw "Could not start $Executable" }
        $outTask = $process.StandardOutput.ReadToEndAsync()
        $errTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill($true) } catch { }
            $process.WaitForExit(15000) | Out-Null
            throw "Command timed out after $TimeoutSeconds seconds: $commandText"
        }
        $stdoutText = Convert-ToSafeText $outTask.GetAwaiter().GetResult()
        $stderrText = Convert-ToSafeText $errTask.GetAwaiter().GetResult()
        [System.IO.File]::WriteAllText($stdout, $stdoutText, [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText($stderr, $stderrText, [System.Text.UTF8Encoding]::new($false))
        @(
            "> $commandText"
            "exitCode=$($process.ExitCode)"
            "durationSeconds=$([Math]::Round(((Get-Date)-$started).TotalSeconds,3))"
            "resolvedExecutable=$($invocation.ResolvedPath)"
            "wrappedWindowsShim=$($invocation.Wrapped)"
            ''
            $stdoutText
            $stderrText
        ) | Set-Content -LiteralPath $combined -Encoding utf8
        if (-not ($AcceptedExitCodes -contains [int]$process.ExitCode)) {
            $tail = @((($stdoutText + "`n" + $stderrText) -split "`r?`n") | Select-Object -Last 25) -join [Environment]::NewLine
            throw "Command failed with exit code $($process.ExitCode): $commandText`n$tail"
        }
        $commandRows.Add([pscustomobject]@{ stage = Split-Path $activeStageRoot -Leaf; id = $Id; command = $commandText; exitCode = $process.ExitCode; log = $combined }) | Out-Null
        return [pscustomobject]@{ exitCode = $process.ExitCode; stdout = $stdoutText; stderr = $stderrText; log = $combined; durationSeconds = [Math]::Round(((Get-Date)-$started).TotalSeconds,3) }
    }
    finally { $process.Dispose() }
}

function Invoke-Stage {
    param([string]$Id, [string]$Description, [scriptblock]$Action)
    $script:activeStageRoot = Join-Path $componentsRoot $Id
    New-Item -ItemType Directory -Force -Path $activeStageRoot | Out-Null
    $stageStarted = Get-Date
    Write-Host "`n============================================================"
    Write-Host "STAGE: $Id - $Description"
    Write-Host "============================================================"
    $status = 'PASS'
    $detail = 'Completed successfully.'
    try { & $Action }
    catch {
        $status = 'FAIL'
        $detail = Convert-ToSafeText $_.Exception.Message
        $_ | Out-String | ForEach-Object { Convert-ToSafeText $_ } | Set-Content -LiteralPath (Join-Path $activeStageRoot 'exception.txt') -Encoding utf8
        Write-Host "[FAIL] $detail" -ForegroundColor Red
    }
    $duration = [Math]::Round(((Get-Date)-$stageStarted).TotalSeconds,3)
    $stageRows.Add([pscustomobject]@{ id = $Id; description = $Description; status = $status; durationSeconds = $duration; detail = $detail; evidence = $activeStageRoot }) | Out-Null
    if ($status -eq 'FAIL' -and $StopOnFailure) { throw "Stage $Id failed: $detail" }
    return $status -eq 'PASS'
}

function Invoke-Np {
    param([string]$Id, [string[]]$Arguments, [int]$TimeoutSeconds = 900)
    return Invoke-LoggedProcess -Id $Id -Executable 'pwsh' -Arguments (@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\np.ps1')) + $Arguments) -TimeoutSeconds $TimeoutSeconds
}

function Invoke-ScopedCleanup {
    param([string]$Id)
    $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\operator\Stop-NP-ExistingState.ps1'),'-RepositoryRoot',$RepositoryRoot,'-OutputRoot',(Join-Path $activeStageRoot "cleanup-$Id"))
    if ($PreserveDockerVolumes) { $args += '-PreserveDockerVolumes' }
    Invoke-LoggedProcess -Id "cleanup-$Id" -Executable 'pwsh' -Arguments $args -TimeoutSeconds 900 | Out-Null
}

function Read-DotEnv {
    $values = @{}
    $path = Join-Path $RepositoryRoot '.env'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $values }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*([^#=\s]+)\s*=\s*(.*)\s*$') { $values[$Matches[1]] = $Matches[2].Trim().Trim('"').Trim("'") }
    }
    return $values
}

function Get-DockerIntegrationEnvironment {
    $values = Read-DotEnv
    $read = {
        param([string]$Name, [string]$Fallback)
        if ($values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$values[$Name])) {
            return [string]$values[$Name]
        }
        return $Fallback
    }

    $postgresPort = & $read 'POSTGRES_PORT' '5433'
    $postgresUser = & $read 'POSTGRES_USER' 'np'
    $postgresPassword = & $read 'POSTGRES_PASSWORD' 'np_dev_pass'
    $rabbitPort = & $read 'RABBITMQ_AMQP_PORT' '5672'
    $rabbitUser = & $read 'RABBITMQ_DEFAULT_USER' 'np'
    $rabbitPassword = & $read 'RABBITMQ_DEFAULT_PASS' 'np_dev_pass'
    $influxPort = & $read 'INFLUXDB_PORT' '8181'
    $influxToken = & $read 'INFLUXDB_TOKEN' 'local-test-token'
    $influxOrganization = & $read 'INFLUXDB_ORGANIZATION' 'natureprotector'
    $influxBucket = & $read 'INFLUXDB_BUCKET' 'np_telemetry'

    return @{
        NP_TEST_POSTGRES_HOST = 'localhost'
        NP_TEST_POSTGRES_PORT = $postgresPort
        NP_TEST_POSTGRES_USER = $postgresUser
        NP_TEST_POSTGRES_PASSWORD = $postgresPassword
        NP_TEST_RABBITMQ_HOST = 'localhost'
        NP_TEST_RABBITMQ_PORT = $rabbitPort
        NP_TEST_RABBITMQ_USER = $rabbitUser
        NP_TEST_RABBITMQ_PASSWORD = $rabbitPassword
        NP_TEST_INFLUXDB_URL = "http://localhost:$influxPort"
        NP_TEST_INFLUXDB_TOKEN = $influxToken
        NP_TEST_INFLUXDB_ORGANIZATION = $influxOrganization
        NP_TEST_INFLUXDB_BUCKET = $influxBucket
        # Always override any stale user/process value. The test fixture otherwise accepts
        # an inherited stopped container ID and attempts docker exec against it.
        NP_TEST_INFLUXDB_CONTAINER = 'np-influxdb'
    }
}

function Assert-DockerIntegrationInfrastructureReady {
    param([Parameter(Mandatory = $true)][hashtable]$Environment)

    $inheritedInfluxContainer = [Environment]::GetEnvironmentVariable('NP_TEST_INFLUXDB_CONTAINER')
    $containerRows = [System.Collections.Generic.List[object]]::new()
    foreach ($containerName in @('np-postgres', 'np-rabbitmq', 'np-influxdb')) {
        $id = 'preflight-' + $containerName.Replace('np-', '') + '-inspect'
        # Do not access .State.Health for every container. Docker omits that map
        # entirely when a service has no healthcheck, which makes Go-template
        # conditionals fail before they can evaluate the else branch.
        $result = Invoke-LoggedProcess -Id $id -Executable 'docker' -Arguments @(
            'inspect',
            '--format',
            '{{.Id}}|{{.State.Running}}',
            $containerName
        ) -TimeoutSeconds 60

        $parts = ([string]$result.stdout).Trim() -split '\|', 2
        if ($parts.Count -ne 2) {
            throw "Could not parse docker state for $containerName. Output: $($result.stdout)"
        }
        $running = [string]$parts[1]
        if ($running -ne 'true') {
            throw "Required Docker container $containerName is not running."
        }

        $health = 'not-configured'
        if ($containerName -eq 'np-influxdb') {
            $healthResult = Invoke-LoggedProcess -Id 'preflight-influx-health' -Executable 'docker' -Arguments @(
                'inspect',
                '--format',
                '{{.State.Health.Status}}',
                $containerName
            ) -TimeoutSeconds 60
            $health = ([string]$healthResult.stdout).Trim()
            if ($health -ne 'healthy') {
                throw "Required Docker container np-influxdb is running but health is '$health'."
            }
        }

        $containerRows.Add([pscustomobject]@{
            name = $containerName
            id = [string]$parts[0]
            running = $true
            health = $health
        }) | Out-Null
    }

    $influxUrl = [string]$Environment['NP_TEST_INFLUXDB_URL']
    $influxToken = [string]$Environment['NP_TEST_INFLUXDB_TOKEN']
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri ($influxUrl.TrimEnd('/') + '/health') -Headers @{ Authorization = "Bearer $influxToken" } -TimeoutSec 15 -ErrorAction Stop
        if ([int]$response.StatusCode -lt 200 -or [int]$response.StatusCode -ge 300) {
            throw "InfluxDB health returned HTTP $([int]$response.StatusCode)."
        }
    }
    catch {
        throw "InfluxDB authenticated health preflight failed at $influxUrl/health: $($_.Exception.Message)"
    }

    [ordered]@{
        schemaVersion = 1
        inheritedInfluxContainer = $(if ([string]::IsNullOrWhiteSpace($inheritedInfluxContainer)) { $null } else { $inheritedInfluxContainer })
        effectiveInfluxContainer = [string]$Environment['NP_TEST_INFLUXDB_CONTAINER']
        influxUrl = $influxUrl
        authenticatedHealth = 'PASS'
        containers = @($containerRows)
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $activeStageRoot 'docker-integration-preflight.json') -Encoding utf8
}

function Save-DockerDiagnostics {
    param([string]$Prefix)
    try {
        Invoke-LoggedProcess -Id "$Prefix-compose-ps" -Executable 'docker' -Arguments @('compose','--project-directory',$RepositoryRoot,'-f',(Join-Path $RepositoryRoot 'docker-compose.yml'),'ps','--all') -TimeoutSeconds 120 -AcceptedExitCodes @(0,1) | Out-Null
    }
    catch { $_ | Out-String | Set-Content -LiteralPath (Join-Path $activeStageRoot "$Prefix-compose-ps-error.txt") -Encoding utf8 }
    try {
        Invoke-LoggedProcess -Id "$Prefix-compose-logs" -Executable 'docker' -Arguments @('compose','--project-directory',$RepositoryRoot,'-f',(Join-Path $RepositoryRoot 'docker-compose.yml'),'logs','--no-color','--timestamps') -TimeoutSeconds 300 -AcceptedExitCodes @(0,1) | Out-Null
    }
    catch { $_ | Out-String | Set-Content -LiteralPath (Join-Path $activeStageRoot "$Prefix-compose-logs-error.txt") -Encoding utf8 }
}

function Test-NpRuntimeReady {
    foreach ($uri in @('http://127.0.0.1:5254/health', 'http://127.0.0.1:5260/health/live', 'http://127.0.0.1:5173')) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 5 -ErrorAction Stop
            if ([int]$response.StatusCode -lt 200 -or [int]$response.StatusCode -ge 400) { return $false }
        }
        catch { return $false }
    }
    return $true
}

function Invoke-NpStartAndWait {
    param([string]$Id, [int]$TimeoutSeconds = 420)
    $logs = Join-Path $activeStageRoot 'logs'
    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    $stdout = Join-Path $logs "$Id.stdout.tmp.log"
    $stderr = Join-Path $logs "$Id.stderr.tmp.log"
    $combined = Join-Path $logs "$Id.log"
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\np.ps1'),'start','-NoBrowser','-ForceRestart')
    $commandText = 'pwsh ' + (($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ')
    Write-Host "`n> $commandText"
    $started = Get-Date
    $process = $null
    $exitCode = 1
    $note = ''
    try {
        $process = Start-Process -FilePath 'pwsh' -ArgumentList $arguments -WorkingDirectory $RepositoryRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            if (Test-NpRuntimeReady) {
                $exitCode = 0
                if (-not $process.HasExited) {
                    $note = "Runtime became healthy while the np start wrapper was still active; wrapper PID $($process.Id) was stopped after health proof."
                    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                    Start-Sleep -Milliseconds 750
                }
                break
            }
            if ($process.HasExited -and [int]$process.ExitCode -ne 0) {
                $exitCode = [int]$process.ExitCode
                $note = "np start exited before runtime health with code $exitCode."
                break
            }
            Start-Sleep -Seconds 2
        } while ((Get-Date) -lt $deadline)
        if ($exitCode -ne 0 -and -not $process.HasExited) {
            $note = "np start did not produce healthy endpoints within $TimeoutSeconds seconds."
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        $note = $_.Exception.Message
        $exitCode = 1
        if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
    finally {
        if ($null -ne $process) { $process.Dispose() }
    }
    $stdoutText = if (Test-Path -LiteralPath $stdout) { Convert-ToSafeText (Get-Content -LiteralPath $stdout -Raw) } else { '' }
    $stderrText = if (Test-Path -LiteralPath $stderr) { Convert-ToSafeText (Get-Content -LiteralPath $stderr -Raw) } else { '' }
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    @(
        "> $commandText"
        "exitCode=$exitCode"
        "durationSeconds=$([Math]::Round(((Get-Date)-$started).TotalSeconds,3))"
        $note
        ''
        $stdoutText
        $stderrText
    ) | Set-Content -LiteralPath $combined -Encoding utf8
    $commandRows.Add([pscustomobject]@{ stage = Split-Path $activeStageRoot -Leaf; id = $Id; command = $commandText; exitCode = $exitCode; log = $combined }) | Out-Null
    if ($exitCode -ne 0) { throw "Runtime startup failed. See $combined" }
}

function Start-NpRuntime {
    param([string]$Prefix)
    Invoke-ScopedCleanup -Id "$Prefix-before-start"
    Invoke-Np -Id "$Prefix-up" -Arguments @('up') -TimeoutSeconds 1200 | Out-Null
    Invoke-NpStartAndWait -Id "$Prefix-start" -TimeoutSeconds 420
    Invoke-Np -Id "$Prefix-health" -Arguments @('health') -TimeoutSeconds 420 | Out-Null
}

function Get-AdminBearerToken {
    $values = Read-DotEnv
    $username = if ($values.ContainsKey('NP_BOOTSTRAP_ADMIN_USERNAME')) { [string]$values.NP_BOOTSTRAP_ADMIN_USERNAME } else { 'admin' }
    $password = if ($values.ContainsKey('NP_BOOTSTRAP_ADMIN_PASSWORD')) { [string]$values.NP_BOOTSTRAP_ADMIN_PASSWORD } else { 'admin123' }
    $body = @{ usernameOrEmail = $username; password = $password } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5254/api/users-roles/login' -ContentType 'application/json' -Body $body -TimeoutSec 30
    $token = [string]$login.token
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Administrator login returned no bearer token.' }
    return $token
}

function Assert-Prerequisites {
    $required = @('pwsh','dotnet','node','npm','docker','python')
    $toolRows = [System.Collections.Generic.List[object]]::new()
    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($tool in $required) {
        try {
            $resolvedPath = Resolve-NpAcceptanceCommandPath -Executable $tool
            $toolRows.Add([pscustomobject]@{ tool = $tool; status = 'FOUND'; resolvedPath = $resolvedPath; extension = [IO.Path]::GetExtension($resolvedPath) }) | Out-Null
        }
        catch {
            $missing.Add($tool) | Out-Null
            $toolRows.Add([pscustomobject]@{ tool = $tool; status = 'MISSING'; resolvedPath = ''; extension = ''; detail = $_.Exception.Message }) | Out-Null
        }
    }
    $toolRows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $activeStageRoot 'toolchain-resolution.json') -Encoding utf8
    if ($missing.Count -gt 0) { throw "Missing required tools: $($missing -join ', ')" }
    Invoke-LoggedProcess -Id 'docker-info' -Executable 'docker' -Arguments @('info') -TimeoutSeconds 120 | Out-Null
    Invoke-LoggedProcess -Id 'dotnet-version' -Executable 'dotnet' -Arguments @('--version') -TimeoutSeconds 60 | Out-Null
    Invoke-LoggedProcess -Id 'node-version' -Executable 'node' -Arguments @('--version') -TimeoutSeconds 60 | Out-Null
    Invoke-LoggedProcess -Id 'npm-version' -Executable 'npm' -Arguments @('--version') -TimeoutSeconds 60 | Out-Null
    Invoke-LoggedProcess -Id 'python-version' -Executable 'python' -Arguments @('--version') -TimeoutSeconds 60 | Out-Null
}

function Write-FinalResult {
    param([string]$Status)
    $completed = (Get-Date).ToUniversalTime()
    $summary = [ordered]@{
        schemaVersion = 1
        runId = $runId
        profile = $Profile
        status = $Status
        repositoryRoot = $RepositoryRoot
        startedAtUtc = $startedAt.ToString('o')
        completedAtUtc = $completed.ToString('o')
        durationSeconds = [Math]::Round(($completed-$startedAt).TotalSeconds,3)
        stages = @($stageRows)
        outputRoot = $runRoot
    }
    $summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding utf8
    $stageRows | Export-Csv -LiteralPath (Join-Path $runRoot 'tests.csv') -NoTypeInformation -Encoding utf8
    $commandRows | Export-Csv -LiteralPath (Join-Path $runRoot 'commands.csv') -NoTypeInformation -Encoding utf8
    @(
        '# NatureProtector local operator test suite'
        ''
        "- Profile: **$Profile**"
        "- Status: **$Status**"
        "- Repository: ``$RepositoryRoot``"
        "- Started: $($startedAt.ToString('o'))"
        "- Completed: $($completed.ToString('o'))"
        "- Evidence: ``$runRoot``"
        ''
        '## Stages'
        ''
        @($stageRows | ForEach-Object { "- $($_.id): **$($_.status)** — $($_.detail)" })
        ''
        'The suite always performs project-scoped shutdown and Docker cleanup before and after execution. No global docker prune is used.'
    ) | Set-Content -LiteralPath (Join-Path $runRoot 'SUMMARY.md') -Encoding utf8

    $hashPath = Join-Path $runRoot 'hashes.sha256'
    Get-ChildItem -LiteralPath $runRoot -Recurse -File | Where-Object FullName -ne $hashPath | Sort-Object FullName | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($runRoot, $_.FullName).Replace('\','/')
        "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
    } | Set-Content -LiteralPath $hashPath -Encoding utf8

    if (-not [string]::IsNullOrWhiteSpace($ExternalResultsRoot)) {
        Copy-Item -LiteralPath (Join-Path $runRoot 'SUMMARY.md') -Destination (Join-Path $ExternalResultsRoot "SUMMARY-$runId.md") -Force
        Copy-Item -LiteralPath (Join-Path $runRoot 'summary.json') -Destination (Join-Path $ExternalResultsRoot "summary-$runId.json") -Force
        @($runRoot) | Set-Content -LiteralPath (Join-Path $ExternalResultsRoot 'ULTIMO-RESULTADO.txt') -Encoding utf8
        try {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $dossierPath = Join-Path $ExternalResultsRoot "DOSSIER-$runId.zip"
            if (Test-Path -LiteralPath $dossierPath) { Remove-Item -LiteralPath $dossierPath -Force }
            [System.IO.Compression.ZipFile]::CreateFromDirectory($runRoot, $dossierPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
            @($dossierPath) | Set-Content -LiteralPath (Join-Path $ExternalResultsRoot 'ULTIMO-DOSSIER.txt') -Encoding utf8
        }
        catch {
            $_ | Out-String | Set-Content -LiteralPath (Join-Path $ExternalResultsRoot "DOSSIER-$runId-ERROR.txt") -Encoding utf8
        }
    }
    Write-Host "`nNATUREPROTECTOR_OPERATOR_TESTS=$Status"
    Write-Host "PROFILE=$Profile"
    Write-Host "RUN_ROOT=$runRoot"
}

try {
    Invoke-Stage -Id '00-clean-existing-state' -Description 'Stop all repository processes and remove project-scoped Docker state before testing.' -Action {
        Invoke-ScopedCleanup -Id 'initial'
    } | Out-Null

    Invoke-Stage -Id '01-prerequisites' -Description 'Validate PowerShell, .NET, Node, npm, Docker and Python.' -Action {
        Assert-Prerequisites
    } | Out-Null

    Invoke-Stage -Id '02-local-environment' -Description 'Create local environment when absent and restore deterministic dependencies.' -Action {
        if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.env') -PathType Leaf)) {
            Invoke-Np -Id 'init-local' -Arguments @('init-local') -TimeoutSeconds 300 | Out-Null
        }
        else {
            'Existing .env preserved.' | Set-Content -LiteralPath (Join-Path $activeStageRoot 'env-policy.txt') -Encoding utf8
        }
        Invoke-Np -Id 'prepare-local' -Arguments @('prepare-local') -TimeoutSeconds 3600 | Out-Null
        if ($Profile -ne 'Smoke' -and -not $SkipFrontendBrowserInstall) {
            Invoke-LoggedProcess -Id 'playwright-install-chromium' -Executable 'npm' -Arguments @('exec','--','playwright','install','chromium') -WorkingDirectory (Join-Path $RepositoryRoot 'webUI') -TimeoutSeconds 1800 | Out-Null
        }
    } | Out-Null

    Invoke-Stage -Id '03-code-tests' -Description $(if ($Profile -eq 'Smoke') { 'Build backend and validate frontend toolchain.' } else { 'Build and execute all tests that do not depend on Docker services.' }) -Action {
        Invoke-LoggedProcess -Id 'dotnet-build' -Executable 'dotnet' -Arguments @('build','.\NatureProtector.sln','-c','Release','--no-restore','--nologo','-v','minimal','-m:1') -TimeoutSeconds 3600 | Out-Null
        if ($Profile -eq 'Smoke') {
            Invoke-LoggedProcess -Id 'frontend-toolchain' -Executable 'npm' -Arguments @('run','check:toolchain') -WorkingDirectory (Join-Path $RepositoryRoot 'webUI') -TimeoutSeconds 900 | Out-Null
        }
        else {
            $standardResults = Join-Path $activeStageRoot 'test-results'
            New-Item -ItemType Directory -Force -Path $standardResults | Out-Null
            Invoke-LoggedProcess -Id 'dotnet-standard-tests' -Executable 'dotnet' -Arguments @('test','.\NatureProtector.sln','-c','Release','--no-build','--no-restore','--filter','Category!=DockerIntegration','--logger','trx;LogFileName=operator-standard.trx','--results-directory',$standardResults) -TimeoutSeconds 7200 | Out-Null
            Invoke-LoggedProcess -Id 'frontend-typecheck' -Executable 'npm' -Arguments @('run','typecheck') -WorkingDirectory (Join-Path $RepositoryRoot 'webUI') -TimeoutSeconds 1800 | Out-Null
            Invoke-LoggedProcess -Id 'frontend-lint' -Executable 'npm' -Arguments @('run','lint') -WorkingDirectory (Join-Path $RepositoryRoot 'webUI') -TimeoutSeconds 1800 | Out-Null
            Invoke-LoggedProcess -Id 'frontend-format-check' -Executable 'npm' -Arguments @('run','format:check') -WorkingDirectory (Join-Path $RepositoryRoot 'webUI') -TimeoutSeconds 1800 | Out-Null
            Invoke-LoggedProcess -Id 'frontend-unit-tests' -Executable 'npm' -Arguments @('test','--','--run') -WorkingDirectory (Join-Path $RepositoryRoot 'webUI') -TimeoutSeconds 3600 | Out-Null
        }
    } | Out-Null

    if ($Profile -ne 'Smoke') {
        Invoke-Stage -Id '03b-docker-integration-tests' -Description 'Start project infrastructure and execute the complete DockerIntegration test category.' -Action {
            try {
                Invoke-ScopedCleanup -Id 'docker-integration-before'
                Invoke-Np -Id 'docker-integration-up' -Arguments @('up') -TimeoutSeconds 1800 | Out-Null
                $dockerResults = Join-Path $activeStageRoot 'test-results'
                New-Item -ItemType Directory -Force -Path $dockerResults | Out-Null
                $dockerTestEnvironment = Get-DockerIntegrationEnvironment
                Assert-DockerIntegrationInfrastructureReady -Environment $dockerTestEnvironment
                Invoke-LoggedProcess -Id 'dotnet-docker-integration-tests' -Executable 'dotnet' -Arguments @('test','.\NatureProtector.sln','-c','Release','--no-build','--no-restore','--filter','Category=DockerIntegration','--logger','trx;LogFileName=operator-docker-integration.trx','--results-directory',$dockerResults) -Environment $dockerTestEnvironment -TimeoutSeconds 10800 | Out-Null
            }
            catch {
                Save-DockerDiagnostics -Prefix 'docker-integration-failure'
                throw
            }
            finally {
                Save-DockerDiagnostics -Prefix 'docker-integration-final'
                Invoke-ScopedCleanup -Id 'docker-integration-final'
            }
        } | Out-Null
    }

    Invoke-Stage -Id '04-functional-routes-and-runs' -Description 'Start the real stack, call API routes and execute scenario runs.' -Action {
        try {
            # The functional harness auto-starts up/start/health when the runtime is stopped and always owns stop/down.
            $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\validation\Invoke-LocalFunctionalValidation.ps1'))
            if ($Profile -eq 'Smoke') { $arguments += @('-Smoke') } else { $arguments += @('-Full','-Evidence','-Ui') }
            $arguments += @('-RunRoot',(Join-Path $activeStageRoot 'functional-validation'))
            Invoke-LoggedProcess -Id 'functional-validation' -Executable 'pwsh' -Arguments $arguments -TimeoutSeconds $(if ($Profile -eq 'Smoke') { 3600 } else { 7200 }) | Out-Null
        }
        finally { Invoke-ScopedCleanup -Id 'functional-final' }
    } | Out-Null

    if ($Profile -ne 'Smoke') {
        Invoke-Stage -Id '05-degradation-rbac-diagnostics' -Description 'Execute all degradation profiles, RBAC lifecycle, diagnostics, alerts and observability checks.' -Action {
            Invoke-LoggedProcess -Id 'p0-runtime-coverage' -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\acceptance\Invoke-NP-P0RuntimeCoverage.ps1'),'-OutputRoot',(Join-Path $activeStageRoot 'p0-runtime'),'-ConfigPath',(Join-Path $RepositoryRoot 'config\acceptance\p0-runtime-coverage.json'),'-SkipBuild') -TimeoutSeconds 14400 | Out-Null
        } | Out-Null

        Invoke-Stage -Id '06-negative-pipeline-p3' -Description 'Run controlled invalid/retry/quarantine cases and reconcile the exact run in PostgreSQL.' -Action {
            $previousToken = $env:NP_RELIABILITY_AUTH_TOKEN
            try {
                Start-NpRuntime -Prefix 'p3'
                $env:NP_RELIABILITY_AUTH_TOKEN = Get-AdminBearerToken
                Invoke-LoggedProcess -Id 'controlled-validation-p3' -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\acceptance\Invoke-NP-ControlledValidationP3.ps1'),'-OutputRoot',(Join-Path $activeStageRoot 'p3'),'-Execute','-AcknowledgeNonProduction','-TimeoutSeconds','900') -TimeoutSeconds 3600 | Out-Null
            }
            finally {
                if ($null -eq $previousToken) { Remove-Item Env:NP_RELIABILITY_AUTH_TOKEN -ErrorAction SilentlyContinue } else { $env:NP_RELIABILITY_AUTH_TOKEN = $previousToken }
                Invoke-ScopedCleanup -Id 'p3-final'
            }
        } | Out-Null

        Invoke-Stage -Id '07-reset-recovery' -Description 'Validate reset guards, quiescence and post-reset recovery across local stores.' -Action {
            try {
                Invoke-ScopedCleanup -Id 'reset-before'
                Invoke-Np -Id 'reset-up' -Arguments @('up') -TimeoutSeconds 1200 | Out-Null
                Invoke-LoggedProcess -Id 'reset-recovery-matrix' -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\testing\Invoke-SystemResetRecoveryMatrix.ps1'),'-OutputRoot',(Join-Path $activeStageRoot 'matrix'),'-SkipBuild') -TimeoutSeconds 10800 | Out-Null
            }
            finally { Invoke-ScopedCleanup -Id 'reset-final' }
        } | Out-Null
    }

    if ($Profile -eq 'Full') {
        Invoke-Stage -Id '08-security-profile' -Description 'Execute dependency, secret and focused authorization security gates.' -Action {
            Invoke-LoggedProcess -Id 'workspace-security' -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\workspace.ps1'),'validate','-Profile','Security') -TimeoutSeconds 3600 | Out-Null
        } | Out-Null

        foreach ($matrix in @(
            @{ id = '09-multi-replica'; script = 'Invoke-MultiReplicaTemporalMatrix.ps1'; timeout = 14400; description = 'Exercise multi-replica temporal and ownership invariants.' },
            @{ id = '10-autoscaling'; script = 'Invoke-AutoscalingExperimentMatrix.ps1'; timeout = 18000; description = 'Execute the bounded local autoscaling experiment matrix.' }
        )) {
            Invoke-Stage -Id $matrix.id -Description $matrix.description -Action {
                try {
                    Invoke-ScopedCleanup -Id "$($matrix.id)-before"
                    Invoke-Np -Id "$($matrix.id)-up" -Arguments @('up') -TimeoutSeconds 1200 | Out-Null
                    Invoke-LoggedProcess -Id $matrix.id -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot "scripts\testing\$($matrix.script)"),'-OutputRoot',(Join-Path $activeStageRoot 'matrix'),'-SkipBuild') -TimeoutSeconds ([int]$matrix.timeout) | Out-Null
                }
                finally { Invoke-ScopedCleanup -Id "$($matrix.id)-final" }
            } | Out-Null
        }

        Invoke-Stage -Id '11-performance-benchmark' -Description 'Run the bounded serialization benchmark smoke profile.' -Action {
            Invoke-LoggedProcess -Id 'performance-b0' -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\performance\run-benchmarks.ps1'),'-Profile','B0','-Filter','*SerializationBenchmarks.SerializeEnvelopeBatch*','-OutputRoot',(Join-Path $activeStageRoot 'benchmark'),'-TimeoutSeconds','300','-NoBuild') -TimeoutSeconds 1200 | Out-Null
        } | Out-Null

        Invoke-Stage -Id '12-ui-accessibility-rate-capacity' -Description 'Run live role journeys, accessibility, rate limiting and bounded HTTP/system performance.' -Action {
            try {
                Invoke-ScopedCleanup -Id 'ui-before'
                Invoke-LoggedProcess -Id 'ui-performance-coverage' -Executable 'pwsh' -Arguments @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $RepositoryRoot 'scripts\acceptance\Invoke-NP-UiPerformanceCoverage.ps1'),'-OutputRoot',(Join-Path $activeStageRoot 'ui-performance'),'-ConfigPath',(Join-Path $RepositoryRoot 'config\acceptance\ui-performance-coverage.json'),'-SkipBuild') -TimeoutSeconds 14400 | Out-Null
            }
            finally { Invoke-ScopedCleanup -Id 'ui-final' }
        } | Out-Null
    }
}
catch { $overallException = $_ }
finally {
    $script:activeStageRoot = Join-Path $componentsRoot '99-final-cleanup'
    New-Item -ItemType Directory -Force -Path $activeStageRoot | Out-Null
    try {
        Invoke-ScopedCleanup -Id 'final'
        $stageRows.Add([pscustomobject]@{ id = '99-final-cleanup'; description = 'Final project-scoped process and Docker cleanup.'; status = 'PASS'; durationSeconds = 0; detail = 'Final cleanup completed.'; evidence = $activeStageRoot }) | Out-Null
    }
    catch {
        $stageRows.Add([pscustomobject]@{ id = '99-final-cleanup'; description = 'Final project-scoped process and Docker cleanup.'; status = 'FAIL'; durationSeconds = 0; detail = $_.Exception.Message; evidence = $activeStageRoot }) | Out-Null
    }
    Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
}

$failedStages = @($stageRows | Where-Object status -eq 'FAIL')
$status = if ($failedStages.Count -eq 0 -and $null -eq $overallException) { 'PASS' } else { 'FAIL' }
Write-FinalResult -Status $status
try { Start-Process explorer.exe -ArgumentList @($runRoot) | Out-Null } catch { }
if ($status -eq 'PASS') { exit 0 }
exit 1
