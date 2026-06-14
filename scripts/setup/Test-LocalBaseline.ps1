<#
.SYNOPSIS
Validates the local NatureProtector baseline after Docker Compose is running.

.DESCRIPTION
This script is read-only. It checks local services and optional application
endpoints, but it does not create data, delete data, run bootstrap, or start
Simulator.Host/Prevention.Host.
#>

[CmdletBinding()]
param(
    [switch]$InfrastructureOnly,
    [switch]$Runtime,
    [switch]$Full
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

function Find-RepositoryRoot {
    $current = Get-Item -LiteralPath $PSScriptRoot

    while ($null -ne $current) {
        $solution = Join-Path $current.FullName "NatureProtector.sln"
        $compose = Join-Path $current.FullName "docker-compose.yml"

        if ((Test-Path -LiteralPath $solution) -and (Test-Path -LiteralPath $compose)) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not locate repository root from $PSScriptRoot."
}

$script:Results = @()

function Add-Result {
    param(
        [ValidateSet("OK", "WARN", "FAIL")]
        [string]$Status,
        [string]$Name,
        [string]$Detail,
        [bool]$Required = $true
    )

    $script:Results += [pscustomobject]@{
        Status = $Status
        Name = $Name
        Detail = $Detail
        Required = $Required
    }

    $label = ("[{0}]" -f $Status).PadRight(7)
    Write-Host "$label $Name - $Detail"
}

function Read-DotEnv {
    param([string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $values
    }

    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#") -or -not $line.Contains("=")) {
            continue
        }

        $parts = $line.Split("=", 2)
        $values[$parts[0].Trim()] = $parts[1].Trim().Trim('"')
    }

    return $values
}

function Get-ConfigValue {
    param(
        [hashtable]$Values,
        [string]$Name,
        [string]$Fallback
    )

    $fromEnvironment = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($fromEnvironment)) {
        return $fromEnvironment
    }

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }

    return $Fallback
}

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMilliseconds = 2000
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)) {
            return $false
        }

        $client.EndConnect($async)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

function Invoke-HttpCheck {
    param(
        [string]$Uri,
        [hashtable]$Headers = @{}
    )

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -Headers $Headers -TimeoutSec 5 -ErrorAction Stop
        return [pscustomobject]@{
            Success = $true
            StatusCode = [int]$response.StatusCode
            Error = $null
            Content = [string]$response.Content
        }
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        return [pscustomobject]@{
            Success = $false
            StatusCode = $statusCode
            Error = $_.Exception.Message
            Content = ""
        }
    }
}

function Get-BasicAuthHeader {
    param(
        [string]$UserName,
        [string]$Password
    )

    $bytes = [System.Text.Encoding]::ASCII.GetBytes("${UserName}:${Password}")
    return @{
        Authorization = "Basic $([Convert]::ToBase64String($bytes))"
    }
}

function Invoke-ExternalCommand {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [hashtable]$Environment = @{}
    )

    try {
        $command = Get-Command $Name -ErrorAction Stop
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $command.Source
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        foreach ($entry in $Environment.GetEnumerator()) {
            $startInfo.Environment[$entry.Key] = [string]$entry.Value
        }

        if ($Arguments.Count -gt 0) {
            $quotedArguments = foreach ($argument in $Arguments) {
                if ($argument -match '\s|"' ) {
                    '"' + ($argument -replace '"', '\"') + '"'
                }
                else {
                    $argument
                }
            }

            $startInfo.Arguments = ($quotedArguments -join " ")
        }

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        [void]$process.Start()
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        $text = (($standardOutput + $standardError) | Out-String).Trim()
        $exitCode = $process.ExitCode
        if ($text -match "error during connect|Acesso negado|Access is denied|permission denied|Cannot connect") {
            $exitCode = 1
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = $text
        }
    }
    catch {
        return [pscustomobject]@{
            ExitCode = 1
            Output = $_.Exception.Message
        }
    }
}

function Test-DockerContainerRunning {
    param(
        [string]$ContainerName,
        [string]$Name
    )

    $result = Invoke-ExternalCommand "docker" @(
        "inspect",
        "-f",
        "{{.State.Running}}",
        $ContainerName
    )

    if ($result.ExitCode -eq 0 -and $result.Output.Trim().ToLowerInvariant() -eq "true") {
        Add-Result "OK" $Name "$ContainerName is running" $true
        return $true
    }

    Add-Result "FAIL" $Name "$ContainerName is not running or not found: $($result.Output)" $true
    return $false
}

function Test-PostgresTable {
    param(
        [string]$ContainerName,
        [string]$TablePattern,
        [string]$Name,
        [string]$User,
        [string]$Database
    )

    $result = Invoke-ExternalCommand "docker" @(
        "exec",
        $ContainerName,
        "psql",
        "-U",
        $User,
        "-d",
        $Database,
        "-tAc",
        "select count(*) from pg_tables where schemaname || '.' || tablename like '$TablePattern';"
    )

    if ($result.ExitCode -eq 0) {
        $count = 0
        [void][int]::TryParse($result.Output.Trim(), [ref]$count)
        if ($count -gt 0) {
            Add-Result "OK" $Name "$count table(s) found" $true
        }
        else {
            Add-Result "WARN" $Name "no matching tables found; run scripts\postgres\bootstrap-control-plane.ps1 if this is a fresh database" $false
        }
    }
    else {
        Add-Result "WARN" $Name "could not query PostgreSQL tables: $($result.Output)" $false
    }
}

function Test-InfluxDatabase {
    param(
        [string]$ContainerName,
        [string]$Token,
        [string]$Database
    )

    if ([string]::IsNullOrWhiteSpace($Token)) {
        Add-Result "FAIL" "InfluxDB token" "INFLUXDB_TOKEN is missing; cannot validate database '$Database'" $true
        return
    }

    if ([string]::IsNullOrWhiteSpace($Database)) {
        Add-Result "FAIL" "InfluxDB database config" "Missing INFLUXDB_DATABASE in .env. Add INFLUXDB_DATABASE=np_telemetry." $true
        return
    }

    $result = Invoke-ExternalCommand `
        -Name "docker" `
        -Arguments @("exec", "-e", "INFLUXDB3_AUTH_TOKEN", $ContainerName, "influxdb3", "show", "databases", "-H", "http://127.0.0.1:8181", "--format", "csv") `
        -Environment @{ INFLUXDB3_AUTH_TOKEN = $Token }

    if ($result.ExitCode -ne 0) {
        Add-Result "FAIL" "InfluxDB database list" "could not list databases: $($result.Output)" $true
        return
    }

    $databases = @(
        $result.Output -split "(`r`n|`n|`r)" |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -ne "iox::database" }
    )

    if ($databases -contains $Database) {
        Add-Result "OK" "InfluxDB database" "$Database exists" $true
    }
    else {
        Add-Result "FAIL" "InfluxDB database" "$Database does not exist; run .\scripts\influx\Ensure-InfluxDatabase.ps1" $true
    }
}

function Test-ControlPlaneCounts {
    param(
        [string]$ContainerName,
        [string]$User,
        [string]$Database
    )

    $queries = @(
        @{ Name = "Control areas"; Sql = "select count(*) from control.areas;" },
        @{ Name = "Control grid cells"; Sql = "select count(*) from control.grid_cells;" },
        @{ Name = "Control sensors"; Sql = "select count(*) from control.sensor_nodes;" },
        @{ Name = "Control scenarios"; Sql = "select count(*) from control.scenario_definitions;" }
    )

    foreach ($query in $queries) {
        $result = Invoke-ExternalCommand "docker" @(
            "exec",
            $ContainerName,
            "psql",
            "-U",
            $User,
            "-d",
            $Database,
            "-tAc",
            $query.Sql
        )

        if ($result.ExitCode -eq 0) {
            $count = 0
            [void][int]::TryParse($result.Output.Trim(), [ref]$count)
            if ($count -gt 0) {
                Add-Result "OK" $query.Name "$count row(s)" $true
            }
            else {
                Add-Result "WARN" $query.Name "0 rows; run scripts\postgres\bootstrap-control-plane.ps1 before full runtime validation" $false
            }
        }
        else {
            Add-Result "WARN" $query.Name "could not query count: $($result.Output)" $false
        }
    }
}

$repoRoot = Find-RepositoryRoot
Set-Location $repoRoot

if (-not $InfrastructureOnly -and -not $Runtime -and -not $Full) {
    $InfrastructureOnly = $true
}

$checkInfrastructure = $InfrastructureOnly -or $Runtime -or $Full
$checkRuntime = $Runtime -or $Full

Write-Host "NatureProtector local baseline check"
Write-Host "Repository root: $repoRoot"
Write-Host "Mode: $(if ($Full) { 'Full' } elseif ($Runtime) { 'Runtime' } else { 'InfrastructureOnly' })"
Write-Host ""

$dotEnvPath = Join-Path $repoRoot ".env"
$envValues = Read-DotEnv $dotEnvPath
if (Test-Path -LiteralPath $dotEnvPath) {
    Add-Result "OK" ".env" "found" $false
}
else {
    Add-Result "WARN" ".env" "missing; using documented defaults for checks" $false
}

$rabbitUser = Get-ConfigValue $envValues "RABBITMQ_DEFAULT_USER" "np"
$rabbitPass = Get-ConfigValue $envValues "RABBITMQ_DEFAULT_PASS" "np_dev_pass"
$rabbitAmqpPort = [int](Get-ConfigValue $envValues "RABBITMQ_AMQP_PORT" "5672")
$rabbitManagementPort = [int](Get-ConfigValue $envValues "RABBITMQ_MANAGEMENT_PORT" "15672")
$rabbitContainer = Get-ConfigValue $envValues "RABBITMQ_CONTAINER" "np-rabbitmq"

$postgresDb = Get-ConfigValue $envValues "POSTGRES_DB" "natureprotector"
$postgresUser = Get-ConfigValue $envValues "POSTGRES_USER" "np"
$postgresPort = [int](Get-ConfigValue $envValues "POSTGRES_PORT" "5432")
$postgresContainer = Get-ConfigValue $envValues "POSTGRES_CONTAINER" "np-postgres"

$influxPort = [int](Get-ConfigValue $envValues "INFLUXDB_PORT" "8181")
$influxUrl = Get-ConfigValue $envValues "INFLUXDB_URL" "http://localhost:$influxPort"
$influxToken = Get-ConfigValue $envValues "INFLUXDB_TOKEN" ""
$influxDatabase = Get-ConfigValue $envValues "INFLUXDB_DATABASE" ""
$influxContainer = Get-ConfigValue $envValues "INFLUXDB_CONTAINER" "np-influxdb"

$grafanaPort = [int](Get-ConfigValue $envValues "GRAFANA_PORT" "3000")
$grafanaContainer = Get-ConfigValue $envValues "GRAFANA_CONTAINER" "np-grafana"

$apiPort = [int](Get-ConfigValue $envValues "BACKOFFICE_API_PORT" "5254")
$webPort = [int](Get-ConfigValue $envValues "WEBUI_PORT" "5173")

if ($checkInfrastructure) {
    $dockerInfo = Invoke-ExternalCommand "docker" @("info", "--format", "{{.ServerVersion}}")
    if ($dockerInfo.ExitCode -eq 0) {
        Add-Result "OK" "Docker daemon" $dockerInfo.Output $true
    }
    else {
        Add-Result "FAIL" "Docker daemon" "docker info failed: $($dockerInfo.Output)" $true
    }

    Test-DockerContainerRunning $postgresContainer "PostgreSQL container" | Out-Null
    Test-DockerContainerRunning $rabbitContainer "RabbitMQ container" | Out-Null
    Test-DockerContainerRunning $influxContainer "InfluxDB container" | Out-Null
    Test-DockerContainerRunning $grafanaContainer "Grafana container" | Out-Null

    if (Test-TcpPort "localhost" $rabbitAmqpPort) {
        Add-Result "OK" "RabbitMQ AMQP" "localhost:$rabbitAmqpPort accepts TCP connections" $true
    }
    else {
        Add-Result "FAIL" "RabbitMQ AMQP" "localhost:$rabbitAmqpPort is not reachable" $true
    }

    $rabbitHeaders = Get-BasicAuthHeader $rabbitUser $rabbitPass
    $rabbitUri = "http://localhost:$rabbitManagementPort/api/overview"
    $rabbitHttp = Invoke-HttpCheck $rabbitUri $rabbitHeaders
    if ($rabbitHttp.Success) {
        Add-Result "OK" "RabbitMQ management" "$rabbitUri returned HTTP $($rabbitHttp.StatusCode)" $true
    }
    else {
        Add-Result "FAIL" "RabbitMQ management" "$rabbitUri failed: $($rabbitHttp.Error)" $true
    }

    $pgReady = Invoke-ExternalCommand "docker" @(
        "exec",
        $postgresContainer,
        "pg_isready",
        "-U",
        $postgresUser,
        "-d",
        $postgresDb
    )

    if ($pgReady.ExitCode -eq 0) {
        Add-Result "OK" "PostgreSQL" $pgReady.Output $true
    }
    elseif (Test-TcpPort "localhost" $postgresPort) {
        Add-Result "FAIL" "PostgreSQL" "TCP localhost:$postgresPort is open, but pg_isready failed: $($pgReady.Output)" $true
    }
    else {
        Add-Result "FAIL" "PostgreSQL" "localhost:$postgresPort is not reachable and pg_isready failed: $($pgReady.Output)" $true
    }

    Test-PostgresTable $postgresContainer "control.%" "PostgreSQL control schema" $postgresUser $postgresDb

    $influxHeaders = @{}
    if (-not [string]::IsNullOrWhiteSpace($influxToken)) {
        $influxHeaders.Authorization = "Bearer $influxToken"
    }

    $influxHttp = Invoke-HttpCheck "$influxUrl/health" $influxHeaders
    if ($influxHttp.Success) {
        Add-Result "OK" "InfluxDB" "$influxUrl/health returned HTTP $($influxHttp.StatusCode)" $true
    }
    elseif (Test-TcpPort "localhost" $influxPort) {
        Add-Result "FAIL" "InfluxDB" "localhost:$influxPort accepts TCP connections, but authenticated /health failed: $($influxHttp.Error)" $true
    }
    else {
        Add-Result "FAIL" "InfluxDB" "localhost:$influxPort is not reachable" $true
    }

    Test-InfluxDatabase $influxContainer $influxToken $influxDatabase

    $grafanaUri = "http://localhost:$grafanaPort/api/health"
    $grafanaHttp = Invoke-HttpCheck $grafanaUri
    if ($grafanaHttp.Success) {
        Add-Result "OK" "Grafana" "$grafanaUri returned HTTP $($grafanaHttp.StatusCode)" $true
    }
    else {
        Add-Result "FAIL" "Grafana" "$grafanaUri failed: $($grafanaHttp.Error)" $true
    }
}

if ($checkRuntime) {
    $backofficeHealthUri = "http://localhost:$apiPort/health"
    $backofficeHealth = Invoke-HttpCheck $backofficeHealthUri
    if ($backofficeHealth.Success) {
        Add-Result "OK" "Backoffice API health" "$backofficeHealthUri returned HTTP $($backofficeHealth.StatusCode)" $true
    }
    else {
        Add-Result "FAIL" "Backoffice API health" "not available or not ready at ${backofficeHealthUri}: $($backofficeHealth.Error)" $true
    }

    $backofficeAuthGuardUri = "http://localhost:$apiPort/api/control/configurations/active"
    $backofficeAuthGuard = Invoke-HttpCheck $backofficeAuthGuardUri
    if ($backofficeAuthGuard.Success) {
        Add-Result "OK" "Backoffice API auth guard" "$backofficeAuthGuardUri returned HTTP $($backofficeAuthGuard.StatusCode)" $false
    }
    elseif ($backofficeAuthGuard.StatusCode -eq 401 -or $backofficeAuthGuard.StatusCode -eq 403) {
        Add-Result "OK" "Backoffice API auth guard" "$backofficeAuthGuardUri returned HTTP $($backofficeAuthGuard.StatusCode); authenticated endpoint is protected as expected" $false
    }
    else {
        Add-Result "WARN" "Backoffice API auth guard" "unexpected response from ${backofficeAuthGuardUri}: $($backofficeAuthGuard.Error)" $false
    }

    $webUri = "http://localhost:$webPort"
    $webHttp = Invoke-HttpCheck $webUri
    if ($webHttp.Success) {
        Add-Result "OK" "webUI" "$webUri returned HTTP $($webHttp.StatusCode)" $true
    }
    else {
        Add-Result "FAIL" "webUI" "not available or not ready at ${webUri}: $($webHttp.Error)" $true
    }

    $summaryUri = "http://localhost:$apiPort/api/dev/runtime/summary"
    $summaryHttp = Invoke-HttpCheck $summaryUri
    if ($summaryHttp.Success) {
        Add-Result "OK" "Runtime summary" "$summaryUri returned HTTP $($summaryHttp.StatusCode)" $false
    }
    else {
        Add-Result "OK" "Runtime summary optional" "optional endpoint not exposed in this version or not available at ${summaryUri}" $false
    }

    Test-ControlPlaneCounts $postgresContainer $postgresUser $postgresDb
}

Write-Host ""
$requiredFailures = @($script:Results | Where-Object { $_.Status -eq "FAIL" -and $_.Required }).Count
$warnings = @($script:Results | Where-Object { $_.Status -eq "WARN" }).Count
$failures = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Summary: $requiredFailures required failure(s), $failures total failure(s), $warnings warning(s)."

if ($requiredFailures -gt 0) {
    exit 1
}

exit 0
