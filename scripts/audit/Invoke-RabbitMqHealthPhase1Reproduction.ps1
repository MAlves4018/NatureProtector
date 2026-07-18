[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$OutputDirectory = "artifacts/operational-audit/rabbitmq-health-phase1",
    [string]$Configuration = "Release",
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$KeepInfrastructure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repoRoot $OutputDirectory
}
$resolvedOutput = [System.IO.Path]::GetFullPath($resolvedOutput)
$composeFile = Join-Path $repoRoot ".github/docker/standard-cd-integration.compose.yml"
$projectName = "np-standard-cd-it"
$composeArgs = @(
    "compose",
    "--project-name", $projectName,
    "--file", $composeFile
)
$testProject = Join-Path $repoRoot "tests/NatureProtector.IntegrationTests/NatureProtector.IntegrationTests.csproj"
$startedByThisRun = $false
$runStartedAt = [DateTimeOffset]::UtcNow
$testExitCode = $null

function Assert-Command {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-External {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$LogPath,
        [switch]$AllowFailure
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    if ($LogPath) {
        & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $LogPath | Out-Host
    }
    else {
        & $FilePath @Arguments | Out-Host
    }

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "$FilePath failed with exit code $exitCode."
    }

    return $exitCode
}

function Export-DockerEvidence {
    param([Parameter(Mandatory)][string]$Prefix)

    Invoke-External docker ($composeArgs + @("ps", "--all")) `
        -LogPath (Join-Path $resolvedOutput "$Prefix-compose-ps.txt") `
        -AllowFailure | Out-Null

    Invoke-External docker ($composeArgs + @("logs", "--no-color")) `
        -LogPath (Join-Path $resolvedOutput "$Prefix-compose-logs.txt") `
        -AllowFailure | Out-Null

    Invoke-External docker @(
        "exec", "np-rabbitmq-it", "rabbitmqctl", "list_queues", "-p", "/",
        "name", "type", "durable", "messages_ready", "messages_unacknowledged",
        "messages", "consumers", "policy", "arguments"
    ) -LogPath (Join-Path $resolvedOutput "$Prefix-rabbitmq-queues.txt") -AllowFailure | Out-Null

    Invoke-External docker @(
        "exec", "np-rabbitmq-it", "rabbitmqctl", "list_bindings", "-p", "/",
        "source_name", "source_kind", "destination_name", "destination_kind",
        "routing_key", "arguments"
    ) -LogPath (Join-Path $resolvedOutput "$Prefix-rabbitmq-bindings.txt") -AllowFailure | Out-Null

    Invoke-External docker @(
        "exec", "np-rabbitmq-it", "rabbitmqctl", "list_policies", "-p", "/"
    ) -LogPath (Join-Path $resolvedOutput "$Prefix-rabbitmq-policies.txt") -AllowFailure | Out-Null
}

Assert-Command dotnet
Assert-Command docker

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

if ($WhatIfPreference) {
    Write-Host "WhatIf: would start/reuse local Docker integration services, build the solution, and run Purpose=OperationalAudit tests."
    Write-Host "WhatIf: no Docker, database, RabbitMQ, .NET test, or cloud operation was executed."
    return
}

$existingIntegrationContainer = @(& docker ps -a --format "{{.Names}}" | Where-Object {
    $_ -in @("np-postgres-it", "np-rabbitmq-it", "np-influxdb-it")
})

try {
    if ($existingIntegrationContainer.Count -eq 0) {
        if ($PSCmdlet.ShouldProcess($composeFile, "start isolated Docker integration infrastructure")) {
            & (Join-Path $repoRoot "scripts/ci/Start-DockerIntegrationServices.ps1")
            if ($LASTEXITCODE -ne 0) {
                throw "Start-DockerIntegrationServices.ps1 failed with exit code $LASTEXITCODE."
            }
            $startedByThisRun = $true
        }
    }
    else {
        Write-Host "Reusing existing integration containers: $($existingIntegrationContainer -join ', ')"
        & (Join-Path $repoRoot "scripts/ci/Start-DockerIntegrationServices.ps1")
        if ($LASTEXITCODE -ne 0) {
            throw "Existing Docker integration infrastructure did not become ready."
        }
    }

    Export-DockerEvidence -Prefix "before"

    if (-not $SkipRestore) {
        Invoke-External dotnet @("restore", (Join-Path $repoRoot "NatureProtector.sln")) `
            -LogPath (Join-Path $resolvedOutput "dotnet-restore.txt") | Out-Null
    }

    if (-not $SkipBuild) {
        Invoke-External dotnet @(
            "build", (Join-Path $repoRoot "NatureProtector.sln"),
            "-c", $Configuration,
            "--no-restore",
            "--nologo",
            "/nodeReuse:false"
        ) -LogPath (Join-Path $resolvedOutput "dotnet-build.txt") | Out-Null
    }

    $env:NP_RUN_OPERATIONAL_AUDIT_PHASE1 = "true"
    $env:NP_TEST_POSTGRES_HOST = "127.0.0.1"
    $env:NP_TEST_POSTGRES_PORT = "5433"
    $env:NP_TEST_POSTGRES_USER = "np"
    $env:NP_TEST_POSTGRES_PASSWORD = "np_dev_pass"
    $env:NP_TEST_RABBITMQ_HOST = "127.0.0.1"
    $env:NP_TEST_RABBITMQ_PORT = "5672"
    $env:NP_TEST_RABBITMQ_MANAGEMENT_URL = "http://127.0.0.1:15672"
    $env:NP_TEST_RABBITMQ_CONTAINER = "np-rabbitmq-it"
    $env:NP_TEST_RABBITMQ_USER = "np"
    $env:NP_TEST_RABBITMQ_PASSWORD = "np_dev_pass"
    $env:NP_TEST_INFLUXDB_URL = "http://127.0.0.1:8181"
    $env:NP_TEST_INFLUXDB_TOKEN = "local-test-token"
    $env:NP_TEST_INFLUXDB_ORGANIZATION = "natureprotector"
    $env:NP_TEST_INFLUXDB_BUCKET = "np_telemetry"
    $env:NP_TEST_INFLUXDB_CONTAINER = "np-influxdb-it"

    $resultsDirectory = Join-Path $resolvedOutput "TestResults"
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    $testArguments = @(
        "test", $testProject,
        "-c", $Configuration,
        "--no-restore",
        "--no-build",
        "--nologo",
        "-v", "normal",
        "--filter", "Purpose=OperationalAudit",
        "--logger", "trx;LogFileName=rabbitmq-health-phase1.trx",
        "--results-directory", $resultsDirectory
    )

    $testExitCode = Invoke-External dotnet $testArguments `
        -LogPath (Join-Path $resolvedOutput "phase1-test-console.txt") `
        -AllowFailure

    Export-DockerEvidence -Prefix "after"

    $markers = @(
        "PHASE1_RAW_GROWTH_REPRODUCED",
        "PHASE1_PARTIAL_NACK_REPRODUCED",
        "PHASE1_MANDATORY_PARTIAL_ROUTING_REPRODUCED",
        "PHASE1_MANDATORY_WRONG_DESTINATION_REPRODUCED",
        "PHASE3C_BACKOFFICE_READINESS_REMEDIATED",
        "PHASE3D_PREVENTION_READINESS_REMEDIATED"
    )
    $consolePath = Join-Path $resolvedOutput "phase1-test-console.txt"
    $evidenceTextParts = [System.Collections.Generic.List[string]]::new()
    if (Test-Path $consolePath) {
        $evidenceTextParts.Add((Get-Content -Raw $consolePath))
    }
    Get-ChildItem -Path $resultsDirectory -Filter "*.trx" -File -ErrorAction SilentlyContinue |
        ForEach-Object {
            $evidenceTextParts.Add((Get-Content -Raw $_.FullName))
        }
    $evidenceText = $evidenceTextParts -join "`n"

    $markerResults = foreach ($marker in $markers) {
        [ordered]@{
            marker = $marker
            observed = $evidenceText.Contains($marker, [StringComparison]::Ordinal)
        }
    }

    $metadata = [ordered]@{
        schema_version = "1.0"
        purpose = "RabbitMQ and health/readiness local characterization with integrated remediation awareness"
        repository_root = $repoRoot
        started_at_utc = $runStartedAt.ToString("O")
        ended_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
        cloud_accessed = $false
        docker_infrastructure_started_by_this_run = $startedByThisRun
        test_exit_code = $testExitCode
        expected_markers = $markerResults
        verdict = if ($testExitCode -eq 0 -and ($markerResults.observed -notcontains $false)) {
            "PHASE1_CHARACTERIZATION_COMPLETE"
        }
        else {
            "PHASE1_INCOMPLETE_OR_FAILED"
        }
    }
    $metadata | ConvertTo-Json -Depth 8 | Set-Content `
        -Path (Join-Path $resolvedOutput "phase1-run-metadata.json") `
        -Encoding utf8

    if ($testExitCode -ne 0) {
        throw "Operational audit tests failed with exit code $testExitCode. Inspect '$resolvedOutput'."
    }

    $missingMarkers = @($markerResults | Where-Object { -not $_.observed })
    if ($missingMarkers.Count -gt 0) {
        throw "Tests returned success but expected evidence markers were missing: $($missingMarkers.marker -join ', ')."
    }

    Write-Host "PHASE1_CHARACTERIZATION_COMPLETE"
    Write-Host "Evidence directory: $resolvedOutput"
}
finally {
    Remove-Item Env:NP_RUN_OPERATIONAL_AUDIT_PHASE1 -ErrorAction SilentlyContinue

    if ($startedByThisRun -and -not $KeepInfrastructure) {
        Invoke-External docker ($composeArgs + @("down", "-v", "--remove-orphans")) `
            -LogPath (Join-Path $resolvedOutput "cleanup-compose-down.txt") `
            -AllowFailure | Out-Null
    }
    elseif ($KeepInfrastructure) {
        Write-Host "Docker integration infrastructure was preserved because -KeepInfrastructure was supplied."
    }
}
