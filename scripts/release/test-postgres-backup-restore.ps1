param(
    [string]$ComposeFile = "docker-compose.yml",
    [string]$Service = "postgres",
    [string]$ContainerId = "",
    [string]$User = "np",
    [string]$OutputRoot = "artifacts/release/postgres-restore",
    [switch]$KeepDatabases
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$sourceDatabase = "np_backup_source_$timestamp"
$restoreDatabase = "np_backup_restore_$timestamp"
$containerId = $ContainerId.Trim()

if ([string]::IsNullOrWhiteSpace($containerId)) {
    $containerId = (& docker compose -f (Join-Path $repoRoot $ComposeFile) ps -q $Service).Trim()
}

if ([string]::IsNullOrWhiteSpace($containerId)) {
    throw "Could not resolve docker compose service '$Service'."
}

$runDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $timestamp)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

function Invoke-Postgres {
    param([string]$Command)

    & docker exec $containerId psql -v ON_ERROR_STOP=1 -U $User -d postgres -c $Command
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

try {
    Invoke-Postgres "CREATE DATABASE $sourceDatabase"
    Invoke-Postgres "CREATE DATABASE $restoreDatabase"

    & docker exec $containerId psql -v ON_ERROR_STOP=1 -U $User -d $sourceDatabase -c "CREATE TABLE backup_restore_probe(id integer PRIMARY KEY, label text NOT NULL); INSERT INTO backup_restore_probe VALUES (1, 'restore-ok');"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $containerDumpPath = "/tmp/$sourceDatabase.dump"
    & docker exec $containerId pg_dump -U $User -d $sourceDatabase -Fc -f $containerDumpPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & docker exec $containerId pg_restore -U $User -d $restoreDatabase $containerDumpPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $verification = (& docker exec $containerId psql -t -A -U $User -d $restoreDatabase -c "SELECT count(*) || ':' || min(label) FROM backup_restore_probe;").Trim()
    if ($verification -ne "1:restore-ok") {
        throw "Restore verification failed. Observed '$verification'."
    }

    & docker cp "$containerId`:$containerDumpPath" (Join-Path $runDirectory "$sourceDatabase.dump")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $manifest = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        containerId = $containerId
        sourceDatabase = $sourceDatabase
        restoreDatabase = $restoreDatabase
        verification = $verification
        dumpPath = Join-Path $runDirectory "$sourceDatabase.dump"
        status = "ready"
        scope = "Ephemeral PostgreSQL backup/restore probe using temporary databases only."
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $runDirectory "backup-restore-result.json") -Encoding UTF8
    Write-Host "PostgreSQL backup/restore validation complete: $runDirectory"
}
finally {
    if (-not $KeepDatabases) {
        foreach ($database in @($sourceDatabase, $restoreDatabase)) {
            if ($database -match "^np_backup_(source|restore)_\d{14}$") {
                & docker exec $containerId dropdb -U $User --if-exists $database | Out-Null
            }
        }
    }
}
