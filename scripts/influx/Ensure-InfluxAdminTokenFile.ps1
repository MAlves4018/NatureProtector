<#
.SYNOPSIS
Creates the local InfluxDB 3 preconfigured admin token file from .env.

.DESCRIPTION
This script is non-destructive. It reads INFLUXDB_TOKEN from .env and writes
a local, ignored JSON token file used by InfluxDB 3 during first startup after
volume recreation.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

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
        throw ".env not found at $Path. Create it from .env.example before running this script."
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

$repoRoot = Find-RepositoryRoot
$dotEnvPath = Join-Path $repoRoot ".env"
$envValues = Read-DotEnv $dotEnvPath

if (-not $envValues.ContainsKey("INFLUXDB_TOKEN") -or [string]::IsNullOrWhiteSpace($envValues["INFLUXDB_TOKEN"])) {
    throw "Missing INFLUXDB_TOKEN in .env."
}

$token = [string]$envValues["INFLUXDB_TOKEN"]

if ($token -match "REPLACE_WITH|CHANGE_ME|<") {
    throw "INFLUXDB_TOKEN in .env is still a placeholder. Set a local apiv3_ token before starting InfluxDB."
}

if (-not $token.StartsWith("apiv3_")) {
    throw "INFLUXDB_TOKEN must start with 'apiv3_' for the InfluxDB 3 offline admin token file."
}

$tokenDir = Join-Path $repoRoot "data/runtime/influx"
$tokenFile = Join-Path $tokenDir "admin-token.json"

New-Item -ItemType Directory -Force -Path $tokenDir | Out-Null

$tokenDocument = [ordered]@{
    token = $token
    name = "natureprotector-local-admin"
    description = "Local development admin token for NatureProtector InfluxDB 3"
}

$json = $tokenDocument | ConvertTo-Json -Depth 4

$shouldWrite = $true

if (Test-Path -LiteralPath $tokenFile) {
    try {
        $existing = Get-Content -Raw -LiteralPath $tokenFile | ConvertFrom-Json

        if ($existing.token -eq $token -and $existing.name -eq $tokenDocument.name) {
            $shouldWrite = $false
        }
    }
    catch {
        $shouldWrite = $true
    }
}

if ($shouldWrite) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($tokenFile, $json, $utf8NoBom)
    
    Write-Host "InfluxDB admin token file written: data/runtime/influx/admin-token.json"
}
else {
    Write-Host "InfluxDB admin token file already matches .env."
}

Write-Host "Summary: token_file=data/runtime/influx/admin-token.json token_source=.env token_prefix=apiv3_"
