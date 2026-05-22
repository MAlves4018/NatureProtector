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

$ErrorActionPreference = "Stop"
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
        $name = $parts[0].Trim()
        $value = $parts[1].Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if (-not [string]::IsNullOrWhiteSpace($name)) {
            $values[$name] = $value
        }
    }

    return $values
}

function Get-ConfigValue {
    param(
        [hashtable]$Values,
        [string]$Name,
        [string]$Fallback = ""
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

function Invoke-ExternalCommand {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [hashtable]$Environment = @{}
    )

    $command = Get-Command $FileName -ErrorAction Stop
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

$repoRoot = Find-RepositoryRoot
Set-Location $repoRoot

$dotEnvPath = Join-Path $repoRoot ".env"
if (-not (Test-Path -LiteralPath $dotEnvPath)) {
    throw "Missing .env. Create it from .env.example before provisioning InfluxDB."
}

$envValues = Read-DotEnv -Path $dotEnvPath

$database = Get-ConfigValue -Values $envValues -Name "INFLUXDB_DATABASE" -Fallback ""
if ([string]::IsNullOrWhiteSpace($database)) {
    throw "Missing INFLUXDB_DATABASE in .env. Add INFLUXDB_DATABASE=np_telemetry."
}

$bucket = Get-ConfigValue -Values $envValues -Name "INFLUXDB_BUCKET" -Fallback $database
if ($bucket -ne $database) {
    Write-Warning "INFLUXDB_BUCKET ('$bucket') differs from INFLUXDB_DATABASE ('$database'). InfluxDB 3 uses database semantics; ensuring '$database'."
}

$token = Get-ConfigValue -Values $envValues -Name "INFLUXDB_TOKEN" -Fallback ""
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Missing INFLUXDB_TOKEN in .env. Add the local InfluxDB admin token before provisioning."
}

if ($token -match "REPLACE_WITH|CHANGE_ME|<") {
    throw "INFLUXDB_TOKEN in .env is still a placeholder. Set a local apiv3_ token before provisioning InfluxDB."
}

$portText = Get-ConfigValue -Values $envValues -Name "INFLUXDB_PORT" -Fallback "8181"
$influxPort = 0
if (-not [int]::TryParse($portText, [ref]$influxPort)) {
    throw "Invalid INFLUXDB_PORT '$portText' in .env."
}

$influxUrl = Get-ConfigValue -Values $envValues -Name "INFLUXDB_URL" -Fallback "http://localhost:$influxPort"
$containerName = Get-ConfigValue -Values $envValues -Name "INFLUXDB_CONTAINER" -Fallback "np-influxdb"

Write-Host "NatureProtector InfluxDB provisioning"
Write-Host "Repository root: $repoRoot"
Write-Host "InfluxDB URL: $influxUrl"
Write-Host "Expected database: $database"
Write-Host "Container: $containerName"

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$lastHttp = $null
do {
    if (Test-TcpPort -HostName "localhost" -Port $influxPort) {
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

$listResult = Invoke-ExternalCommand `
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
    exit 0
}

Write-Host "InfluxDB database '$database' is missing; creating it now."
$createResult = Invoke-ExternalCommand `
    -FileName "docker" `
    -Arguments @("exec", "-e", "INFLUXDB3_AUTH_TOKEN", $containerName, "influxdb3", "create", "database", "-H", $cliHost, $database) `
    -Environment $cliEnvironment

if ($createResult.ExitCode -ne 0) {
    throw "Could not create InfluxDB database '$database'. Output: $($createResult.Output)"
}

$verifyResult = Invoke-ExternalCommand `
    -FileName "docker" `
    -Arguments @("exec", "-e", "INFLUXDB3_AUTH_TOKEN", $containerName, "influxdb3", "show", "databases", "-H", $cliHost, "--format", "csv") `
    -Environment $cliEnvironment

if ($verifyResult.ExitCode -ne 0 -or (($verifyResult.Output -split "(`r`n|`n|`r)") | ForEach-Object { $_.Trim() }) -notcontains $database) {
    throw "InfluxDB database '$database' was created but could not be verified. Output: $($verifyResult.Output)"
}

Write-Host "InfluxDB database '$database' created and verified."
Write-Host "Summary: reachable=true token=accepted created=true database=$database"
