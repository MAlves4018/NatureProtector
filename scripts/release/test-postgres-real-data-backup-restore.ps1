param(
    [string]$ComposeFile = "docker-compose.yml",
    [string]$Service = "postgres",
    [string]$ContainerId = "",
    [string]$User = "np",
    [string]$SourceDatabase = "natureprotector",
    [string]$OutputRoot = "artifacts/release/postgres-real-restore",
    [switch]$KeepRestoreDatabase
)

$ErrorActionPreference = "Stop"

function Invoke-DockerPostgres {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Container,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & docker exec $Container @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "docker exec failed with exit code $LASTEXITCODE. Output: $($output | Out-String)"
    }

    return ($output | Out-String).Trim()
}

function Invoke-PostgresScalar {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Container,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    return Invoke-DockerPostgres `
        -Container $Container `
        -Arguments @("psql", "-v", "ON_ERROR_STOP=1", "-t", "-A", "-U", $User, "-d", $Database, "-c", $Sql)
}

function Assert-SafeDatabaseName {
    param([Parameter(Mandatory = $true)][string]$Name)

    if ($Name -notmatch '^[a-zA-Z_][a-zA-Z0-9_]*$') {
        throw "Unsafe database name: $Name"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$containerId = $ContainerId.Trim()
$restoreDatabase = "np_real_restore_$timestamp"

Assert-SafeDatabaseName $SourceDatabase
Assert-SafeDatabaseName $restoreDatabase

if ([string]::IsNullOrWhiteSpace($containerId)) {
    $containerId = (& docker compose -f (Join-Path $repoRoot $ComposeFile) ps -q $Service).Trim()
}

if ([string]::IsNullOrWhiteSpace($containerId)) {
    throw "Could not resolve docker compose service '$Service'."
}

$runDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $timestamp)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$tables = @(
    "control.areas",
    "control.grid_cells",
    "control.sensor_nodes",
    "control.scenario_definitions",
    "control.dataset_artifacts"
)

$containerDumpPath = "/tmp/$SourceDatabase-real-$timestamp.dump"

try {
    $sourceExists = Invoke-PostgresScalar `
        -Container $containerId `
        -Database "postgres" `
        -Sql "SELECT 1 FROM pg_database WHERE datname = '$SourceDatabase';"

    if ($sourceExists -ne "1") {
        throw "Source database '$SourceDatabase' does not exist."
    }

    Invoke-DockerPostgres `
        -Container $containerId `
        -Arguments @("createdb", "-U", $User, $restoreDatabase) | Out-Null

    Invoke-DockerPostgres `
        -Container $containerId `
        -Arguments @("pg_dump", "-U", $User, "-d", $SourceDatabase, "-Fc", "-f", $containerDumpPath) | Out-Null

    Invoke-DockerPostgres `
        -Container $containerId `
        -Arguments @("pg_restore", "-U", $User, "-d", $restoreDatabase, $containerDumpPath) | Out-Null

    & docker cp "$containerId`:$containerDumpPath" (Join-Path $runDirectory "$SourceDatabase-real-$timestamp.dump")
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed while copying dump artifact."
    }

    $tableCounts = foreach ($table in $tables) {
        $sourceCount = [int](Invoke-PostgresScalar -Container $containerId -Database $SourceDatabase -Sql "SELECT count(*) FROM $table;")
        $restoreCount = [int](Invoke-PostgresScalar -Container $containerId -Database $restoreDatabase -Sql "SELECT count(*) FROM $table;")

        if ($sourceCount -ne $restoreCount) {
            throw "Restored count mismatch for $table. Source=$sourceCount Restore=$restoreCount"
        }

        [ordered]@{
            table = $table
            sourceCount = $sourceCount
            restoreCount = $restoreCount
        }
    }

    $requiredNonEmptyTables = @(
        "control.areas",
        "control.grid_cells",
        "control.sensor_nodes",
        "control.scenario_definitions"
    )

    foreach ($table in $requiredNonEmptyTables) {
        $row = $tableCounts | Where-Object { $_.table -eq $table } | Select-Object -First 1
        if ($row.restoreCount -le 0) {
            throw "Restored table $table is unexpectedly empty."
        }
    }

    $manifest = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        containerId = $containerId
        sourceDatabase = $SourceDatabase
        restoreDatabase = $restoreDatabase
        dumpPath = Join-Path $runDirectory "$SourceDatabase-real-$timestamp.dump"
        tableCounts = @($tableCounts)
        status = "ready"
        scope = "Real local PostgreSQL backup/restore validation using the current NatureProtector control-plane database. It restores into a temporary database and compares canonical control table counts. It does not switch the live application to the restored database."
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runDirectory "real-data-backup-restore-result.json") -Encoding UTF8
    Write-Host "PostgreSQL real-data backup/restore validation complete: $runDirectory"
}
finally {
    if (-not $KeepRestoreDatabase) {
        try {
            Invoke-DockerPostgres `
                -Container $containerId `
                -Arguments @("dropdb", "-U", $User, "--if-exists", $restoreDatabase) | Out-Null
        }
        catch {
            Write-Warning "Failed to drop restore database '$restoreDatabase': $($_.Exception.Message)"
        }
    }

    try {
        Invoke-DockerPostgres `
            -Container $containerId `
            -Arguments @("rm", "-f", $containerDumpPath) | Out-Null
    }
    catch {
        Write-Warning "Failed to remove container dump '$containerDumpPath': $($_.Exception.Message)"
    }
}
