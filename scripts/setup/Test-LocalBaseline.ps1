<#
.SYNOPSIS
Validates the local NatureProtector baseline after Docker Compose is running.

.DESCRIPTION
This script is read-only. It checks local services and optional application
endpoints, but it does not create data, delete data, run bootstrap, or start
Simulator.Host/Prevention.Host.
#>

[CmdletBinding()]
param()

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
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $separator = $line.IndexOf("=")
        if ($separator -le 0) {
            continue
        }

        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim().Trim('"')
        $values[$key] = $value
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

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($Values[$Name])) {
        return $Values[$Name]
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
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -TimeoutSec 5 -ErrorAction Stop
        return [pscustomobject]@{
            Success = $true
            StatusCode = [int]$response.StatusCode
            Error = $null
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

function Test-ExternalCommand {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    try {
        $command = Get-Command $Name -ErrorAction Stop
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $command.Source
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

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

function Get-InfluxEnabled {
    param([string]$RepoRoot)

    $environmentValue = [Environment]::GetEnvironmentVariable("InfluxDb__Enabled")
    if ([string]::IsNullOrWhiteSpace($environmentValue)) {
        $environmentValue = [Environment]::GetEnvironmentVariable("INFLUXDB_ENABLED")
    }

    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
        $parsed = $false
        if ([bool]::TryParse($environmentValue, [ref]$parsed)) {
            return $parsed
        }
    }

    $settingsPath = Join-Path $RepoRoot "src\NatureProtector.Prevention.Host\appsettings.json"
    if (-not (Test-Path -LiteralPath $settingsPath)) {
        return $false
    }

    try {
        $settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
        if ($null -ne $settings.InfluxDb -and $null -ne $settings.InfluxDb.Enabled) {
            return [bool]$settings.InfluxDb.Enabled
        }
    }
    catch {
        return $false
    }

    return $false
}

$repoRoot = Find-RepositoryRoot
Set-Location $repoRoot

Write-Host "NatureProtector local baseline check"
Write-Host "Repository root: $repoRoot"
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

$postgresDb = Get-ConfigValue $envValues "POSTGRES_DB" "natureprotector"
$postgresUser = Get-ConfigValue $envValues "POSTGRES_USER" "np"
$postgresPort = [int](Get-ConfigValue $envValues "POSTGRES_PORT" "5432")

$influxPort = [int](Get-ConfigValue $envValues "INFLUXDB_PORT" "8181")
$grafanaPort = [int](Get-ConfigValue $envValues "GRAFANA_PORT" "3000")

$dockerInfo = Test-ExternalCommand "docker" @("info", "--format", "{{.ServerVersion}}")
if ($dockerInfo.ExitCode -eq 0) {
    Add-Result "OK" "Docker daemon" $dockerInfo.Output $true
}
else {
    Add-Result "FAIL" "Docker daemon" "docker info failed: $($dockerInfo.Output)" $true
}

$composePs = Test-ExternalCommand "docker" @("compose", "ps")
if ($composePs.ExitCode -eq 0) {
    Add-Result "OK" "Docker Compose project" "docker compose ps completed" $true
}
else {
    Add-Result "FAIL" "Docker Compose project" "docker compose ps failed: $($composePs.Output)" $true
}

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

$pgReady = Test-ExternalCommand "docker" @("compose", "exec", "-T", "postgres", "pg_isready", "-U", $postgresUser, "-d", $postgresDb)
if ($pgReady.ExitCode -eq 0) {
    Add-Result "OK" "PostgreSQL" $pgReady.Output $true
}
elseif (Test-TcpPort "localhost" $postgresPort) {
    Add-Result "FAIL" "PostgreSQL" "TCP localhost:$postgresPort is open, but pg_isready failed: $($pgReady.Output)" $true
}
else {
    Add-Result "FAIL" "PostgreSQL" "localhost:$postgresPort is not reachable and pg_isready failed: $($pgReady.Output)" $true
}

$influxEnabled = Get-InfluxEnabled $repoRoot
$influxRequired = $influxEnabled
$influxUri = "http://localhost:$influxPort/health"
$influxHttp = Invoke-HttpCheck $influxUri
if ($influxHttp.Success) {
    Add-Result "OK" "InfluxDB" "$influxUri returned HTTP $($influxHttp.StatusCode)" $influxRequired
}
elseif (Test-TcpPort "localhost" $influxPort) {
    Add-Result "OK" "InfluxDB" "localhost:$influxPort accepts TCP connections; /health did not return a clean response" $influxRequired
}
elseif ($influxRequired) {
    Add-Result "FAIL" "InfluxDB" "InfluxDb is enabled but localhost:$influxPort is not reachable" $true
}
else {
    Add-Result "WARN" "InfluxDB" "not reachable, but Prevention.Host has InfluxDb:Enabled=false locally" $false
}

$grafanaUri = "http://localhost:$grafanaPort/api/health"
$grafanaHttp = Invoke-HttpCheck $grafanaUri
if ($grafanaHttp.Success) {
    Add-Result "OK" "Grafana" "$grafanaUri returned HTTP $($grafanaHttp.StatusCode)" $true
}
else {
    Add-Result "FAIL" "Grafana" "$grafanaUri failed: $($grafanaHttp.Error)" $true
}

$backofficeUri = "http://localhost:5254/api/control/configurations/active"
$backofficeHttp = Invoke-HttpCheck $backofficeUri
if ($backofficeHttp.Success) {
    Add-Result "OK" "Backoffice API" "$backofficeUri returned HTTP $($backofficeHttp.StatusCode)" $false
}
else {
    Add-Result "WARN" "Backoffice API" "not available or not ready at ${backofficeUri}: $($backofficeHttp.Error)" $false
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
