[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ComposeFile = ".github/docker/standard-cd-integration.compose.yml",
    [string]$ProjectName = "np-standard-cd-it",
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$ResolvedComposeFile = (Resolve-Path (Join-Path $RepoRoot $ComposeFile)).Path
$ComposeArgs = @("compose", "--project-name", $ProjectName, "--file", $ResolvedComposeFile)

function Invoke-Docker {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Wait-Until {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Probe
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastExit = 1
    while ((Get-Date) -lt $deadline) {
        & $Probe
        $lastExit = $LASTEXITCODE
        if ($lastExit -eq 0) {
            Write-Host "$Name readiness: PASS"
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "$Name did not become ready within $TimeoutSeconds seconds. Last exit code: $lastExit"
}

if ($PSCmdlet.ShouldProcess($ResolvedComposeFile, "start Docker integration services")) {
    Invoke-Docker ($ComposeArgs + @("up", "-d"))
}

if ($WhatIfPreference) {
    Write-Host "WhatIf: Docker integration readiness probes were not executed."
    return
}

Wait-Until "PostgreSQL" {
    & docker @($ComposeArgs + @("exec", "-T", "postgres", "pg_isready", "-U", "np", "-d", "natureprotector"))
}

Wait-Until "RabbitMQ" {
    & docker @($ComposeArgs + @("exec", "-T", "rabbitmq", "rabbitmq-diagnostics", "-q", "ping"))
}

Wait-Until "RabbitMQ management API" {
    & docker @($ComposeArgs + @("exec", "-T", "rabbitmq", "rabbitmqctl", "list_vhosts", "name"))
}

Wait-Until "InfluxDB" {
    & docker exec np-influxdb-it sh -lc 'influxdb3 create database --host http://127.0.0.1:8181 np_telemetry >/dev/null 2>&1 || true'
    & docker exec np-influxdb-it influxdb3 query --host http://127.0.0.1:8181 --database np_telemetry 'SELECT 1' 2>$null
}

Write-Host "Docker integration services are ready."
