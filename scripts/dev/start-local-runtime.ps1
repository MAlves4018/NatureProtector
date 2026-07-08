param(
    [switch]$OpenBrowser,
    [switch]$NoBrowser,
    [switch]$SkipDocker,
    [switch]$SkipBootstrap,
    [switch]$ForceRestart,
    [int]$ApiPort = 5254,
    [int]$PreventionPort = 5260,
    [int]$WebPort = 5173,
    [bool]$RunSimulator = $true,
    [string]$ScenarioCode = "scenario_b",
    [int]$SensorCount = 2,
    [int]$NumberOfCycles = 1,
    [int]$IntervalSeconds = 1,
    [int]$Seed = 20260706,
    [string]$DegradationProfile = "none",
    [bool]$WaitForSimulatorCompletion = $true,
    [int]$SimulatorTimeoutSeconds = 180
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

function ConvertTo-PowerShellSingleQuotedLiteral {
    param([string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Get-ProcessCommandLine {
    param([int]$ProcessId)

    try {
        $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop
        return [string]$processInfo.CommandLine
    }
    catch {
        return ''
    }
}

function Get-ListeningPortOwners {
    param([int]$Port)

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    $owners = @()
    foreach ($connection in $connections) {
        try {
            $process = Get-Process -Id $connection.OwningProcess -ErrorAction Stop
            $owners += [pscustomobject]@{
                Id          = $process.Id
                ProcessName = $process.ProcessName
                CommandLine = Get-ProcessCommandLine -ProcessId $process.Id
            }
        }
        catch {
            $owners += [pscustomobject]@{
                Id          = $connection.OwningProcess
                ProcessName = '<unknown>'
                CommandLine = ''
            }
        }
    }

    return $owners | Sort-Object Id -Unique
}

function Test-IsLocalNatureProtectorProcess {
    param(
        [pscustomobject]$Owner,
        [string]$RepositoryRoot
    )

    $commandLine = [string]$Owner.CommandLine
    $processName = [string]$Owner.ProcessName

    if ($processName.StartsWith('NatureProtector.', [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if (($processName -eq 'dotnet' -or $processName -eq 'node') -and $commandLine.IndexOf($RepositoryRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
    }

    return $false
}

function Stop-LocalPortOwners {
    param(
        [int]$Port,
        [string]$Name,
        [string]$RepositoryRoot
    )

    $owners = @(Get-ListeningPortOwners -Port $Port)
    foreach ($owner in $owners) {
        if (-not (Test-IsLocalNatureProtectorProcess -Owner $owner -RepositoryRoot $RepositoryRoot)) {
            throw "$Name port $Port is occupied by PID $($owner.Id) ($($owner.ProcessName)), which does not look like a local NatureProtector process. Stop it manually or choose another port."
        }

        Write-Host "Stopping $Name port owner PID $($owner.Id) ($($owner.ProcessName))..."
        Stop-Process -Id $owner.Id -Force -ErrorAction Stop
    }
}

function Assert-PortAvailable {
    param(
        [int]$Port,
        [string]$Name,
        [bool]$AllowForceRestart,
        [string]$RepositoryRoot
    )

    $owners = @(Get-ListeningPortOwners -Port $Port)
    if ($owners.Count -eq 0) {
        return
    }

    $summary = ($owners | ForEach-Object { "PID $($_.Id) ($($_.ProcessName))" }) -join ', '
    if (-not $AllowForceRestart) {
        throw "$Name port $Port is already in use by $summary. Re-run with -ForceRestart to stop local NatureProtector processes, or stop the process manually."
    }

    Stop-LocalPortOwners -Port $Port -Name $Name -RepositoryRoot $RepositoryRoot
    Start-Sleep -Seconds 1

    $remainingOwners = @(Get-ListeningPortOwners -Port $Port)
    if ($remainingOwners.Count -gt 0) {
        $remaining = ($remainingOwners | ForEach-Object { "PID $($_.Id) ($($_.ProcessName))" }) -join ', '
        throw "$Name port $Port is still occupied after -ForceRestart: $remaining."
    }
}

function Stop-NatureProtectorLocalProcesses {
    param([string]$RepositoryRoot)

    $candidates = Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like 'NatureProtector.*' -or $_.ProcessName -eq 'dotnet' -or $_.ProcessName -eq 'node' }

    foreach ($candidate in $candidates) {
        $owner = [pscustomobject]@{
            Id          = $candidate.Id
            ProcessName = $candidate.ProcessName
            CommandLine = Get-ProcessCommandLine -ProcessId $candidate.Id
        }

        if (Test-IsLocalNatureProtectorProcess -Owner $owner -RepositoryRoot $RepositoryRoot) {
            Write-Host "Stopping local NatureProtector process PID $($owner.Id) ($($owner.ProcessName))..."
            Stop-Process -Id $owner.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

function Test-PostgresTarget {
    param(
        [string]$HostName,
        [int]$Port
    )

    Write-Host "Checking PostgreSQL target $HostName`:$Port..."
    $available = Test-NetConnection -ComputerName $HostName -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue
    if (-not $available) {
        throw "PostgreSQL is not reachable at $HostName`:$Port. Check .env, Docker port mappings, or start Docker before launching the runtime."
    }
}

function Wait-TcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutSeconds = 60,
        [string]$Name = "$HostName`:$Port"
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-NetConnection -ComputerName $HostName -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "$Name did not become reachable on TCP port $Port within $TimeoutSeconds seconds."
}

function Wait-HttpReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 60,
        [string]$Name = $Url
    )

    $readyStatusCodes = @(200, 301, 302, 401, 404)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5 -MaximumRedirection 0 -ErrorAction Stop
            if ($readyStatusCodes -contains [int]$response.StatusCode) {
                return
            }

            $lastError = "HTTP $($response.StatusCode)"
        }
        catch {
            $statusCode = $null
            if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }

            if ($statusCode -and $readyStatusCodes -contains $statusCode) {
                return
            }

            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    }

    throw "$Name did not become HTTP-ready at $Url within $TimeoutSeconds seconds. Last error: $lastError"
}

function Invoke-DotnetProjectBuild {
    param(
        [string]$ProjectPath,
        [string]$Name
    )

    Write-Host "Building $Name in Release before launch..."
    & dotnet build $ProjectPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "$Name Release build failed with exit code $LASTEXITCODE."
    }
}

function Start-LoggedPowerShell {
    param(
        [string]$Name,
        [string]$WorkingDirectory,
        [hashtable]$Environment,
        [string]$Command,
        [string]$LogPath,
        [string]$ErrorLogPath,
        [Nullable[int]]$Port,
        [string]$Url
    )

    $envLines = foreach ($entry in $Environment.GetEnumerator()) {
        '$env:{0} = {1}' -f $entry.Key, (ConvertTo-PowerShellSingleQuotedLiteral -Value ([string]$entry.Value))
    }

    $script = @(
        '$ErrorActionPreference = ''Stop'''
        $envLines
        $Command
    ) -join [Environment]::NewLine

    $scriptPath = Join-Path (Split-Path -Parent $LogPath) ("start-" + ($Name -replace '[^A-Za-z0-9_.-]', '-') + ".ps1")
    Set-Content -Path $scriptPath -Value $script -Encoding UTF8

    $process = Start-Process -FilePath 'pwsh.exe' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) `
        -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $LogPath `
        -RedirectStandardError $ErrorLogPath `
        -WindowStyle Hidden `
        -PassThru

    return [pscustomobject]@{
        Name         = $Name
        Id           = $process.Id
        Port         = $Port
        Url          = $Url
        LogPath      = $LogPath
        ErrorLogPath = $ErrorLogPath
        ScriptPath   = $scriptPath
    }
}

function Set-CurrentProcessEnvironment {
    param([hashtable]$Environment)

    foreach ($entry in $Environment.GetEnumerator()) {
        Set-Item -Path "Env:$($entry.Key)" -Value ([string]$entry.Value)
    }
}

$repositoryRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln')
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
if (-not (Test-Path $composeFile)) {
    throw "Docker Compose file not found at $composeFile."
}

$dotEnv = Read-NpDotEnv -Path (Join-Path $repositoryRoot '.env')
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$evidenceRoot = Join-Path $repositoryRoot 'docs\evidence\dev-runtime'
$runRoot = Join-Path $evidenceRoot $timestamp
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

$postgresHost = Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_HOST' -DefaultValue 'localhost'
$postgresPort = [int](Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_PORT' -DefaultValue '5432')
$postgresDb = Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_DB' -DefaultValue 'natureprotector'
$postgresUser = Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_USER' -DefaultValue 'np'
$postgresPassword = Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_PASSWORD' -DefaultValue 'np_dev_pass'
$rabbitPort = [int](Get-NpConfigValue -Values $dotEnv -Name 'RABBITMQ_AMQP_PORT' -DefaultValue '5672')
$rabbitUser = Get-NpConfigValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_USER' -DefaultValue 'np'
$rabbitPassword = Get-NpConfigValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_PASS' -DefaultValue 'np_dev_pass'
$influxPort = [int](Get-NpConfigValue -Values $dotEnv -Name 'INFLUXDB_PORT' -DefaultValue '8181')
$bootstrapAdminUsername = Get-NpConfigValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_USERNAME' -DefaultValue 'admin'
$bootstrapAdminPassword = Get-NpConfigValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_PASSWORD' -DefaultValue 'admin123'

if ($ForceRestart) {
    Stop-NatureProtectorLocalProcesses -RepositoryRoot $repositoryRoot
    Start-Sleep -Seconds 1
}

if (-not $SkipDocker) {
    Write-Host 'Starting Docker dependencies...'
    & docker compose --project-directory $repositoryRoot -f $composeFile up -d

    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed with exit code $LASTEXITCODE."
    }
}

Test-PostgresTarget -HostName $postgresHost -Port $postgresPort
Assert-PortAvailable -Port $ApiPort -Name 'Backoffice API' -AllowForceRestart:$ForceRestart.IsPresent -RepositoryRoot $repositoryRoot
Assert-PortAvailable -Port $PreventionPort -Name 'Prevention Host' -AllowForceRestart:$ForceRestart.IsPresent -RepositoryRoot $repositoryRoot
Assert-PortAvailable -Port $WebPort -Name 'webUI' -AllowForceRestart:$ForceRestart.IsPresent -RepositoryRoot $repositoryRoot

if (-not $SkipBootstrap) {
    $bootstrapScript = Join-Path $repositoryRoot 'scripts\postgres\bootstrap-control-plane.ps1'
    if (Test-Path $bootstrapScript) {
        Write-Host 'Running control-plane bootstrap...'
        Set-Item -Path Env:NP_BOOTSTRAP_ADMIN_PASSWORD -Value $bootstrapAdminPassword
        & $bootstrapScript
    }
    else {
        Write-Host 'No bootstrap-control-plane.ps1 found; skipping bootstrap.'
    }
}

$apiUrl = "http://127.0.0.1:$ApiPort"
$preventionUrl = "http://127.0.0.1:$PreventionPort"
$webUrl = "http://127.0.0.1:$WebPort"
$developerUrl = "$webUrl"

$commonEnvironment = @{
    ASPNETCORE_ENVIRONMENT                                             = 'Development'
    DOTNET_ENVIRONMENT                                                 = 'Development'
    BackofficeApi__LocalRuntimeProcessLaunchEnabled                    = 'true'
    POSTGRES_HOST                                                      = $postgresHost
    POSTGRES_PORT                                                      = [string]$postgresPort
    POSTGRES_DB                                                        = $postgresDb
    POSTGRES_USER                                                      = $postgresUser
    POSTGRES_PASSWORD                                                  = $postgresPassword
    RabbitMq__HostName                                                 = 'localhost'
    RabbitMq__Port                                                     = [string]$rabbitPort
    RabbitMq__UserName                                                 = $rabbitUser
    RabbitMq__Password                                                 = $rabbitPassword
    InfluxDb__Url                                                      = "http://localhost:$influxPort"
    InfluxDb__Token                                                    = (Get-NpConfigValue -Values $dotEnv -Name 'INFLUXDB_TOKEN' -DefaultValue '')
    VITE_API_PROXY_TARGET                                              = $apiUrl
    ControlledValidation__ProcessingFaults__Enabled                    = 'true'
    ControlledValidation__ProcessingFaults__EnableBuiltInP3Cases       = 'true'
    ControlledValidation__ProcessingFaults__AllowedRunLabelPrefixes__0 = 'controlled-validation-p3-negative-pipeline-'
    NP_BOOTSTRAP_ADMIN_USERNAME                                        = $bootstrapAdminUsername
    NP_BOOTSTRAP_ADMIN_PASSWORD                                        = $bootstrapAdminPassword
}

$apiEnvironment = $commonEnvironment.Clone()
$apiEnvironment['ASPNETCORE_URLS'] = $apiUrl

$preventionEnvironment = $commonEnvironment.Clone()
$preventionEnvironment['ASPNETCORE_URLS'] = $preventionUrl

$webUiRoot = Join-Path $repositoryRoot 'webUI'
$webUiNodeModules = Join-Path $webUiRoot 'node_modules'
if (-not (Test-Path $webUiNodeModules)) {
    throw "webUI dependencies were not found at $webUiNodeModules. Run 'cd .\webUI; npm ci; cd ..' before starting the local runtime."
}

$apiProject = Join-Path $repositoryRoot 'src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj'
$preventionProject = Join-Path $repositoryRoot 'src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj'
$simulatorProject = Join-Path $repositoryRoot 'src\NatureProtector.Simulator.Host\NatureProtector.Simulator.Host.csproj'
Invoke-DotnetProjectBuild -ProjectPath $apiProject -Name 'Backoffice API'
Invoke-DotnetProjectBuild -ProjectPath $preventionProject -Name 'Prevention Host'
if ($RunSimulator) {
    Invoke-DotnetProjectBuild -ProjectPath $simulatorProject -Name 'Simulator Host'
}

$processes = @()
$processes += Start-LoggedPowerShell `
    -Name 'Backoffice API' `
    -WorkingDirectory $repositoryRoot `
    -Environment $apiEnvironment `
    -Command 'dotnet run -c Release --no-build --no-restore --project src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj --no-launch-profile' `
    -LogPath (Join-Path $runRoot 'backoffice-api.log') `
    -ErrorLogPath (Join-Path $runRoot 'backoffice-api.err.log') `
    -Port $ApiPort `
    -Url $apiUrl

$processes += Start-LoggedPowerShell `
    -Name 'Prevention Host' `
    -WorkingDirectory $repositoryRoot `
    -Environment $preventionEnvironment `
    -Command 'dotnet run -c Release --no-build --no-restore --project src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj --no-launch-profile' `
    -LogPath (Join-Path $runRoot 'prevention-host.log') `
    -ErrorLogPath (Join-Path $runRoot 'prevention-host.err.log') `
    -Port $PreventionPort `
    -Url $preventionUrl

$processes += Start-LoggedPowerShell `
    -Name 'webUI' `
    -WorkingDirectory $webUiRoot `
    -Environment $commonEnvironment `
    -Command "npm run dev -- --host 127.0.0.1 --port $WebPort --strictPort" `
    -LogPath (Join-Path $runRoot 'webui.log') `
    -ErrorLogPath (Join-Path $runRoot 'webui.err.log') `
    -Port $WebPort `
    -Url $webUrl

try {
    Write-Host 'Waiting for Backoffice API readiness...'
    Wait-TcpPort -HostName '127.0.0.1' -Port $ApiPort -TimeoutSeconds 60 -Name 'Backoffice API'
    Wait-HttpReady -Url "$apiUrl/api/control/areas" -TimeoutSeconds 60 -Name 'Backoffice API'

    Write-Host 'Waiting for Prevention Host liveness...'
    Wait-TcpPort -HostName '127.0.0.1' -Port $PreventionPort -TimeoutSeconds 60 -Name 'Prevention Host'
    Wait-HttpReady -Url "$preventionUrl/health/live" -TimeoutSeconds 60 -Name 'Prevention Host'

    Write-Host 'Waiting for webUI readiness...'
    Wait-TcpPort -HostName '127.0.0.1' -Port $WebPort -TimeoutSeconds 60 -Name 'webUI'
    Wait-HttpReady -Url $webUrl -TimeoutSeconds 60 -Name 'webUI'

    if ($RunSimulator) {
        $scenarioScript = Join-Path $repositoryRoot 'scripts\scenarios\run-scenario.ps1'
        if (-not (Test-Path -LiteralPath $scenarioScript)) {
            throw "Scenario runner not found at $scenarioScript."
        }

        $simulatorRunSpecPath = Join-Path $runRoot 'simulator-run-spec.json'
        $simulatorRunLogPath = Join-Path $runRoot 'simulator-host.log'
        $runLabel = "start-local-runtime-$timestamp-$ScenarioCode"
        $simulatorRunSpec = [ordered]@{
            version = "1.0"
            areaCode = "proenca-a-nova"
            scenarioCode = $ScenarioCode
            sensorCount = $SensorCount
            numberOfCycles = $NumberOfCycles
            intervalSeconds = $IntervalSeconds
            seed = $Seed
            startTimestamp = [DateTimeOffset]::UtcNow.ToString("o")
            degradationProfile = $DegradationProfile
            collectEvidence = $false
            waitForCompletion = $WaitForSimulatorCompletion
            timeoutSeconds = $SimulatorTimeoutSeconds
            allowParallelRun = $false
            runLabel = $runLabel
        }

        $simulatorRunSpec | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $simulatorRunSpecPath -Encoding UTF8
        Set-CurrentProcessEnvironment -Environment $commonEnvironment

        Write-Host "Running Simulator Host through scenario runner..."
        & $scenarioScript `
            -SpecPath $simulatorRunSpecPath `
            -PollIntervalSeconds 1 *>&1 |
            Tee-Object -FilePath $simulatorRunLogPath

        if ($LASTEXITCODE -ne 0) {
            throw "Simulator Host scenario run failed with exit code $LASTEXITCODE. Log: $simulatorRunLogPath"
        }
    }
}
catch {
    $logSummary = ($processes | ForEach-Object { "$($_.Name): $($_.LogPath) / $($_.ErrorLogPath)" }) -join [Environment]::NewLine
    throw "$($_.Exception.Message)$([Environment]::NewLine)Logs:$([Environment]::NewLine)$logSummary"
}

$summaryPath = Join-Path $runRoot 'launcher-summary.md'
$processJsonPath = Join-Path $runRoot 'runtime-processes.json'
$processRows = $processes | ForEach-Object {
    "- $($_.Name): PID $($_.Id), Port $($_.Port), URL $($_.Url), Log $($_.LogPath), ErrorLog $($_.ErrorLogPath), Script $($_.ScriptPath)"
}

$processes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $processJsonPath -Encoding UTF8

@(
    '# Local Runtime Launcher'
    ''
    "StartedAt: $(Get-Date -Format o)"
    "Repository: $repositoryRoot"
    "ForceRestart: $($ForceRestart.IsPresent)"
    "SkipDocker: $($SkipDocker.IsPresent)"
    "SkipBootstrap: $($SkipBootstrap.IsPresent)"
    "RunSimulator: $RunSimulator"
    "ScenarioCode: $ScenarioCode"
    ''
    '## Effective Targets'
    ''
    "- Backoffice API: $apiUrl"
    "- webUI: $webUrl"
    "- Developer Runtime View: $developerUrl"
    "- PostgreSQL: $postgresHost`:$postgresPort/$postgresDb as $postgresUser"
    "- RabbitMQ AMQP: localhost:$rabbitPort"
    "- InfluxDB: http://localhost:$influxPort"
    ''
    '## Processes'
    ''
    $processRows
    ''
    '## Notes'
    ''
    '- The launcher aborts if the API or webUI port is occupied, unless -ForceRestart can safely stop a local NatureProtector process.'
    '- PostgreSQL connectivity is checked before starting application processes.'
    '- Simulator.Host is validated as a scenario process, not as a long-running HTTP service.'
) | Set-Content -Path $summaryPath -Encoding UTF8

Write-Host "Launcher summary: $summaryPath"
Write-Host "Runtime process manifest: $processJsonPath"
Write-Host "Backoffice API: $apiUrl"
Write-Host "Prevention Host health: $preventionUrl"
Write-Host "Simulator Host: scenario execution via scripts/scenarios/run-scenario.ps1"
Write-Host "webUI: $webUrl"
Write-Host "Developer Runtime View: $developerUrl"

if ($OpenBrowser -and -not $NoBrowser) {
    Start-Process $developerUrl | Out-Null
}

Write-Host 'Launcher completed. Services continue in background.'
Write-Host "Logs: $runRoot"
