param(
    [switch]$OpenBrowser,
    [switch]$NoBrowser,
    [switch]$SkipDocker,
    [switch]$SkipBootstrap,
    [switch]$ForceRestart,
    [int]$ApiPort = 5254,
    [int]$WebPort = 5173
)

$ErrorActionPreference = 'Stop'

function Resolve-RepositoryRoot {
    $current = Split-Path -Parent $PSCommandPath
    while ($current) {
        if (Test-Path (Join-Path $current 'NatureProtector.sln')) {
            return (Resolve-Path $current).Path
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current) {
            break
        }

        $current = $parent
    }

    throw 'Could not resolve repository root from the launcher script path.'
}

function Read-DotEnv {
    param([string]$Path)

    $values = @{}
    if (-not (Test-Path $Path)) {
        return $values
    }

    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#') -or -not $trimmed.Contains('=')) {
            continue
        }

        $parts = $trimmed.Split('=', 2)
        $name = $parts[0].Trim()
        $value = $parts[1].Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if ($name) {
            $values[$name] = $value
        }
    }

    return $values
}

function Get-ConfigValue {
    param(
        [hashtable]$Values,
        [string]$Name,
        [string]$DefaultValue
    )

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }

    return $DefaultValue
}

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

    $process = Start-Process -FilePath 'powershell.exe' `
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

$repositoryRoot = Resolve-RepositoryRoot
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
if (-not (Test-Path $composeFile)) {
    throw "Docker Compose file not found at $composeFile."
}

$dotEnv = Read-DotEnv -Path (Join-Path $repositoryRoot '.env')
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$evidenceRoot = Join-Path $repositoryRoot 'docs\evidence\dev-runtime'
$runRoot = Join-Path $evidenceRoot $timestamp
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

$postgresHost = Get-ConfigValue -Values $dotEnv -Name 'POSTGRES_HOST' -DefaultValue 'localhost'
$postgresPort = [int](Get-ConfigValue -Values $dotEnv -Name 'POSTGRES_PORT' -DefaultValue '5432')
$postgresDb = Get-ConfigValue -Values $dotEnv -Name 'POSTGRES_DB' -DefaultValue 'natureprotector'
$postgresUser = Get-ConfigValue -Values $dotEnv -Name 'POSTGRES_USER' -DefaultValue 'np'
$postgresPassword = Get-ConfigValue -Values $dotEnv -Name 'POSTGRES_PASSWORD' -DefaultValue 'np_dev_pass'
$rabbitPort = [int](Get-ConfigValue -Values $dotEnv -Name 'RABBITMQ_AMQP_PORT' -DefaultValue '5672')
$rabbitUser = Get-ConfigValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_USER' -DefaultValue 'np'
$rabbitPassword = Get-ConfigValue -Values $dotEnv -Name 'RABBITMQ_DEFAULT_PASS' -DefaultValue 'np_dev_pass'
$influxPort = [int](Get-ConfigValue -Values $dotEnv -Name 'INFLUXDB_PORT' -DefaultValue '8181')

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
Assert-PortAvailable -Port $WebPort -Name 'webUI' -AllowForceRestart:$ForceRestart.IsPresent -RepositoryRoot $repositoryRoot

if (-not $SkipBootstrap) {
    $bootstrapScript = Join-Path $repositoryRoot 'scripts\dev\bootstrap-local-runtime.ps1'
    if (Test-Path $bootstrapScript) {
        Write-Host 'Running local runtime bootstrap...'
        & $bootstrapScript
    }
    else {
        Write-Host 'No bootstrap-local-runtime.ps1 found; skipping bootstrap.'
    }
}

$apiUrl = "http://127.0.0.1:$ApiPort"
$webUrl = "http://127.0.0.1:$WebPort"
$developerUrl = "$webUrl"

$commonEnvironment = @{
    ASPNETCORE_ENVIRONMENT  = 'Development'
    DOTNET_ENVIRONMENT      = 'Development'
    ASPNETCORE_URLS         = $apiUrl
    POSTGRES_HOST           = $postgresHost
    POSTGRES_PORT           = [string]$postgresPort
    POSTGRES_DB             = $postgresDb
    POSTGRES_USER           = $postgresUser
    POSTGRES_PASSWORD       = $postgresPassword
    RabbitMq__HostName      = 'localhost'
    RabbitMq__Port          = [string]$rabbitPort
    RabbitMq__UserName      = $rabbitUser
    RabbitMq__Password      = $rabbitPassword
    InfluxDb__Url           = "http://localhost:$influxPort"
    VITE_API_PROXY_TARGET   = $apiUrl
}

$processes = @()
$processes += Start-LoggedPowerShell `
    -Name 'Backoffice API' `
    -WorkingDirectory $repositoryRoot `
    -Environment $commonEnvironment `
    -Command 'dotnet run --no-restore --configfile NuGet.Config --project src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj --no-launch-profile' `
    -LogPath (Join-Path $runRoot 'backoffice-api.log') `
    -ErrorLogPath (Join-Path $runRoot 'backoffice-api.err.log') `
    -Port $ApiPort `
    -Url $apiUrl

$processes += Start-LoggedPowerShell `
    -Name 'Prevention Host' `
    -WorkingDirectory $repositoryRoot `
    -Environment $commonEnvironment `
    -Command 'dotnet run --no-restore --configfile NuGet.Config --project src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj' `
    -LogPath (Join-Path $runRoot 'prevention-host.log') `
    -ErrorLogPath (Join-Path $runRoot 'prevention-host.err.log') `
    -Port $null `
    -Url ''

$processes += Start-LoggedPowerShell `
    -Name 'webUI' `
    -WorkingDirectory (Join-Path $repositoryRoot 'webUI') `
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

    Write-Host 'Waiting for webUI readiness...'
    Wait-TcpPort -HostName '127.0.0.1' -Port $WebPort -TimeoutSeconds 60 -Name 'webUI'
    Wait-HttpReady -Url $webUrl -TimeoutSeconds 60 -Name 'webUI'
}
catch {
    $logSummary = ($processes | ForEach-Object { "$($_.Name): $($_.LogPath) / $($_.ErrorLogPath)" }) -join [Environment]::NewLine
    throw "$($_.Exception.Message)$([Environment]::NewLine)Logs:$([Environment]::NewLine)$logSummary"
}

$summaryPath = Join-Path $runRoot 'launcher-summary.md'
$processRows = $processes | ForEach-Object {
    "- $($_.Name): PID $($_.Id), Port $($_.Port), URL $($_.Url), Log $($_.LogPath), ErrorLog $($_.ErrorLogPath), Script $($_.ScriptPath)"
}

@(
    '# Local Runtime Launcher'
    ''
    "StartedAt: $(Get-Date -Format o)"
    "Repository: $repositoryRoot"
    "ForceRestart: $($ForceRestart.IsPresent)"
    "SkipDocker: $($SkipDocker.IsPresent)"
    "SkipBootstrap: $($SkipBootstrap.IsPresent)"
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
) | Set-Content -Path $summaryPath -Encoding UTF8

Write-Host "Launcher summary: $summaryPath"
Write-Host "Backoffice API: $apiUrl"
Write-Host "webUI: $webUrl"
Write-Host "Developer Runtime View: $developerUrl"

if ($OpenBrowser -and -not $NoBrowser) {
    Start-Process $developerUrl | Out-Null
}

Write-Host 'Launcher completed. Services continue in background.'
Write-Host "Logs: $runRoot"
