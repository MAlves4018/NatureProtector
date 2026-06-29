<#
.SYNOPSIS
Ensures the local InfluxDB 3 database used by NatureProtector exists.

.DESCRIPTION
This script is idempotent. It reads local configuration from environment
variables and `.env`, waits for the local InfluxDB endpoint, authenticates with
the configured token, lists databases, and creates the expected database only
when it is missing.

It never edits `.env` and never deletes Docker volumes.
#>

[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 60,
    [int]$PollSeconds = 2
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Test-InfluxHttp {
    param(
        [string]$Url,
        [string]$Token
    )

    try {
        $headers = @{ Authorization = "Bearer $Token" }
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$Url/health" -Headers $headers -TimeoutSec 5 -ErrorAction Stop
        return [pscustomobject]@{
            Success = $true
            StatusCode = [int]$response.StatusCode
            Error = ""
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

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
Set-Location $repoRoot

$dotEnvPath = Join-Path $repoRoot ".env"
if (-not (Test-Path -LiteralPath $dotEnvPath)) {
    throw "Missing .env. Create it from .env.example before provisioning InfluxDB."
}

$envValues = Read-NpDotEnv -Path $dotEnvPath

$database = Get-NpConfigValue -Values $envValues -Name "INFLUXDB_DATABASE" -Fallback "" -EnvironmentFirst
if ([string]::IsNullOrWhiteSpace($database)) {
    throw "Missing INFLUXDB_DATABASE in .env. Add INFLUXDB_DATABASE=np_telemetry."
}

$bucket = Get-NpConfigValue -Values $envValues -Name "INFLUXDB_BUCKET" -Fallback $database -EnvironmentFirst
if ($bucket -ne $database) {
    Write-Warning "INFLUXDB_BUCKET ('$bucket') differs from INFLUXDB_DATABASE ('$database'). InfluxDB 3 uses database semantics; ensuring '$database'."
}

$token = Get-NpConfigValue -Values $envValues -Name "INFLUXDB_TOKEN" -Fallback "" -EnvironmentFirst
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Missing INFLUXDB_TOKEN in .env. Add the local InfluxDB admin token before provisioning."
}

if ($token -match "REPLACE_WITH|CHANGE_ME|<") {
    throw "INFLUXDB_TOKEN in .env is still a placeholder. Set a local apiv3_ token before provisioning InfluxDB."
}

$portText = Get-NpConfigValue -Values $envValues -Name "INFLUXDB_PORT" -Fallback "8181" -EnvironmentFirst
$influxPort = 0
if (-not [int]::TryParse($portText, [ref]$influxPort)) {
    throw "Invalid INFLUXDB_PORT '$portText' in .env."
}

$influxUrl = Get-NpConfigValue -Values $envValues -Name "INFLUXDB_URL" -Fallback "http://localhost:$influxPort" -EnvironmentFirst
$containerName = Get-NpConfigValue -Values $envValues -Name "INFLUXDB_CONTAINER" -Fallback "np-influxdb" -EnvironmentFirst

Write-Host "NatureProtector InfluxDB provisioning"
Write-Host "Repository root: $repoRoot"
Write-Host "InfluxDB URL: $influxUrl"
Write-Host "Expected database: $database"
Write-Host "Container: $containerName"

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$lastHttp = $null
do {
    if (Test-NpTcpEndpoint -HostName "localhost" -Port $influxPort) {
        $lastHttp = Test-InfluxHttp -Url $influxUrl -Token $token
        if ($lastHttp.Success) {
            break
        }

        if ($lastHttp.StatusCode -eq 401 -or $lastHttp.StatusCode -eq 403) {
            throw "InfluxDB is reachable at $influxUrl, but the configured token was rejected (HTTP $($lastHttp.StatusCode)). Check INFLUXDB_TOKEN in .env."
        }
    }

    if ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds $PollSeconds
    }
} while ((Get-Date) -lt $deadline)

if ($null -eq $lastHttp -or -not $lastHttp.Success) {
    throw "InfluxDB did not become reachable at $influxUrl within $TimeoutSeconds seconds. Check INFLUXDB_PORT, docker compose status, and np-influxdb logs."
}

$cliEnvironment = @{ INFLUXDB3_AUTH_TOKEN = $token }
$cliHost = "http://127.0.0.1:8181"

$listResult = Invoke-NpExternalCommand -ThrowOnStartFailure `
    -FileName "docker" `
    -Arguments @("exec", "-e", "INFLUXDB3_AUTH_TOKEN", $containerName, "influxdb3", "show", "databases", "-H", $cliHost, "--format", "csv") `
    -Environment $cliEnvironment

if ($listResult.ExitCode -ne 0) {
    throw "Could not list InfluxDB databases with the configured token. Output: $($listResult.Output)"
}

$databases = @(
    $listResult.Output -split "(`r`n|`n|`r)" |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -ne "iox::database" }
)

if ($databases -contains $database) {
    Write-Host "InfluxDB database '$database' already exists."
    Write-Host "Summary: reachable=true token=accepted created=false database=$database"
    return
}

Write-Host "InfluxDB database '$database' is missing; creating it now."
$createResult = Invoke-NpExternalCommand -ThrowOnStartFailure `
    -FileName "docker" `
    -Arguments @("exec", "-e", "INFLUXDB3_AUTH_TOKEN", $containerName, "influxdb3", "create", "database", "-H", $cliHost, $database) `
    -Environment $cliEnvironment

if ($createResult.ExitCode -ne 0) {
    throw "Could not create InfluxDB database '$database'. Output: $($createResult.Output)"
}

$verifyResult = Invoke-NpExternalCommand -ThrowOnStartFailure `
    -FileName "docker" `
    -Arguments @("exec", "-e", "INFLUXDB3_AUTH_TOKEN", $containerName, "influxdb3", "show", "databases", "-H", $cliHost, "--format", "csv") `
    -Environment $cliEnvironment

if ($verifyResult.ExitCode -ne 0 -or (($verifyResult.Output -split "(`r`n|`n|`r)") | ForEach-Object { $_.Trim() }) -notcontains $database) {
    throw "InfluxDB database '$database' was created but could not be verified. Output: $($verifyResult.Output)"
}

Write-Host "InfluxDB database '$database' created and verified."
Write-Host "Summary: reachable=true token=accepted created=true database=$database"
