<#
.SYNOPSIS
Destroys and recreates the local NatureProtector Docker baseline.

.DESCRIPTION
This script is intentionally destructive. It stops the local Docker Compose
baseline, deletes only the known NatureProtector baseline Docker volumes, starts
the infrastructure again, ensures the InfluxDB database exists, and runs local
bootstrap/check scripts when they exist.

This is not the normal daily command. Use infra/scripts/down.ps1 to stop
containers while preserving volumes.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Confirm
)

$ErrorActionPreference = "Stop"

if ($Confirm -ne "RESET_LOCAL_INFRA") {
    throw "Refusing to reset local infrastructure. Re-run with -Confirm RESET_LOCAL_INFRA to delete local baseline Docker volumes."
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
        $values[$parts[0].Trim()] = $parts[1].Trim()
    }

    return $values
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
Set-Location $projectRoot

Write-Warning "This will delete local NatureProtector Docker volumes and their data."

$envValues = Read-DotEnv -Path (Join-Path $projectRoot ".env")
$projectName = "natureprotector"
if ($envValues.ContainsKey("COMPOSE_PROJECT_NAME") -and -not [string]::IsNullOrWhiteSpace($envValues["COMPOSE_PROJECT_NAME"])) {
    $projectName = $envValues["COMPOSE_PROJECT_NAME"]
}

$composeVolumes = @(
    "rabbitmq_data",
    "postgres_data",
    "influxdb_data",
    "influxdb_config",
    "grafana_data"
)

docker compose down
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

foreach ($volume in $composeVolumes) {
    $dockerVolume = "${projectName}_${volume}"
    Write-Host "Removing Docker volume $dockerVolume"
    docker volume rm $dockerVolume

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not remove Docker volume $dockerVolume. It may not exist or may still be in use."
    }
}

# Prepara o ficheiro local de admin token antes de arrancar o InfluxDB com volumes novos.
# Este passo não altera o `.env`; apenas materializa o token local esperado pelo InfluxDB 3.
$ensureInfluxAdminTokenScript = Join-Path $projectRoot "scripts\influx\Ensure-InfluxAdminTokenFile.ps1"
if (Test-Path -LiteralPath $ensureInfluxAdminTokenScript) {
    & $ensureInfluxAdminTokenScript

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
else {
    Write-Error "Influx admin token script not found at $ensureInfluxAdminTokenScript."
    exit 1
}

docker compose up -d
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$ensureInfluxScript = Join-Path $projectRoot "scripts\influx\Ensure-InfluxDatabase.ps1"
if (Test-Path -LiteralPath $ensureInfluxScript) {
    & $ensureInfluxScript

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
else {
    Write-Warning "Influx provisioning script not found at $ensureInfluxScript. Run scripts\influx\Ensure-InfluxDatabase.ps1 when available."
}

$bootstrapScript = Join-Path $projectRoot "scripts\postgres\bootstrap-control-plane.ps1"
if (Test-Path -LiteralPath $bootstrapScript) {
    & $bootstrapScript

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$baselineScript = Join-Path $projectRoot "scripts\setup\Test-LocalBaseline.ps1"
if (Test-Path -LiteralPath $baselineScript) {
    & $baselineScript -InfrastructureOnly

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}