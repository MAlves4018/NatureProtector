[CmdletBinding()]
param(
    [switch]$SkipDotnet,
    [switch]$SkipFrontend,
    [switch]$SkipContainers,
    [switch]$KeepContainers,
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repositoryRoot

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$artifactRoot = Join-Path $repositoryRoot "artifacts\g1-container-readiness\$timestamp"
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

$composeArgs = @(
    "compose",
    "-f", "docker-compose.yml",
    "-f", "docker-compose.g1.yml"
)
$generatedJwtSigningKey = $false

function Invoke-Captured {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $logPath = Join-Path $artifactRoot "$Name.log"
    & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE. See $logPath"
    }
}

function Wait-HttpHealthy {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Uri,
        [int]$Attempts = 60,
        [int]$DelaySeconds = 2
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                [pscustomobject]@{
                    name = $Name
                    uri = $Uri
                    statusCode = $response.StatusCode
                    checkedAtUtc = [DateTimeOffset]::UtcNow
                } | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $artifactRoot "$Name-health.json")
                return
            }
        }
        catch {
            if ($attempt -eq $Attempts) {
                throw "$Name did not become healthy at $Uri. Last error: $($_.Exception.Message)"
            }
        }

        Start-Sleep -Seconds $DelaySeconds
    }
}

$toolchain = [ordered]@{
    capturedAtUtc = [DateTimeOffset]::UtcNow
    repositoryRoot = $repositoryRoot
    dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { (& dotnet --version) } else { $null }
    node = if (Get-Command node -ErrorAction SilentlyContinue) { (& node --version) } else { $null }
    npm = if (Get-Command npm -ErrorAction SilentlyContinue) { (& npm --version) } else { $null }
    docker = if (Get-Command docker -ErrorAction SilentlyContinue) { (& docker --version) } else { $null }
}
$toolchain | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $artifactRoot "toolchain.json")

try {
    if (-not $SkipDotnet) {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
            throw "dotnet was not found. Use -SkipDotnet only when recording an explicitly partial run."
        }

        Invoke-Captured "dotnet-restore" "dotnet" @(
            "restore", "NatureProtector.sln", "--configfile", "NuGet.Config"
        )
        Invoke-Captured "dotnet-build" "dotnet" @(
            "build", "NatureProtector.sln", "--configuration", $Configuration, "--no-restore"
        )
        Invoke-Captured "dotnet-tests" "dotnet" @(
            "test", "NatureProtector.sln", "--configuration", $Configuration,
            "--no-build", "--no-restore", "--logger", "trx;LogFileName=g1-tests.trx",
            "--results-directory", $artifactRoot
        )
    }

    if (-not $SkipFrontend) {
        if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
            throw "npm was not found. Use -SkipFrontend only when recording an explicitly partial run."
        }

        Push-Location (Join-Path $repositoryRoot "webUI")
        try {
            Invoke-Captured "frontend-npm-ci" "npm" @("ci", "--ignore-scripts")
            Invoke-Captured "frontend-toolchain" "npm" @("run", "check:toolchain")
            Invoke-Captured "frontend-lint" "npm" @("run", "lint")
            Invoke-Captured "frontend-typecheck" "npm" @("run", "typecheck")
            Invoke-Captured "frontend-test" "npm" @("test", "--", "--maxWorkers=1")
            Invoke-Captured "frontend-build" "npm" @("run", "build")
            Invoke-Captured "frontend-audit-script-tests" "npm" @("run", "test:audit-script")
            Invoke-Captured "frontend-audit-policy" "npm" @("run", "audit:ci")

            Get-ChildItem -Path . -File -Filter "npm-audit*" -ErrorAction SilentlyContinue |
                Copy-Item -Destination $artifactRoot -Force
            if (Test-Path "test-results\vitest-junit.xml") {
                Copy-Item "test-results\vitest-junit.xml" (Join-Path $artifactRoot "frontend-vitest-junit.xml") -Force
            }
        }
        finally {
            Pop-Location
        }
    }

    if (-not $SkipContainers) {
        if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
            throw "docker was not found. Use -SkipContainers only when recording an explicitly partial run."
        }

        if (-not (Test-Path (Join-Path $repositoryRoot ".env"))) {
            throw ".env is required by the existing local compose baseline. This script does not create or modify it."
        }

        if (Test-Path Env:NP_G1_JWT_SIGNING_KEY) {
            if ([System.Text.Encoding]::UTF8.GetByteCount($env:NP_G1_JWT_SIGNING_KEY) -lt 32) {
                throw "NP_G1_JWT_SIGNING_KEY must contain at least 32 UTF-8 bytes."
            }
        }
        else {
            $env:NP_G1_JWT_SIGNING_KEY = [Convert]::ToBase64String(
                [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
            $generatedJwtSigningKey = $true
        }

        # --quiet validates the merged model without writing resolved secrets to evidence.
        Invoke-Captured "compose-config" "docker" ($composeArgs + @("config", "--quiet"))
        Invoke-Captured "container-build" "docker" ($composeArgs + @(
            "build", "postgres-bootstrap", "backoffice-api", "prevention", "simulator", "frontend"
        ))
        Invoke-Captured "infra-up" "docker" ($composeArgs + @(
            "up", "-d", "postgres", "rabbitmq", "influxdb"
        ))
        Invoke-Captured "postgres-bootstrap" "docker" ($composeArgs + @(
            "--profile", "bootstrap", "run", "--rm", "postgres-bootstrap"
        ))
        Invoke-Captured "runtime-up" "docker" ($composeArgs + @(
            "up", "-d", "backoffice-api", "prevention", "frontend"
        ))

        Wait-HttpHealthy "backoffice-live" "http://localhost:5254/health/live"
        Wait-HttpHealthy "backoffice-ready" "http://localhost:5254/health/ready"
        Wait-HttpHealthy "prevention-live" "http://localhost:5260/health/live"
        Wait-HttpHealthy "prevention-ready" "http://localhost:5260/health/ready"
        Wait-HttpHealthy "frontend" "http://localhost:5173/healthz"

        Invoke-Captured "simulator-smoke" "docker" ($composeArgs + @(
            "--profile", "simulator", "run", "--rm",
            "-e", "Simulator__NumberOfCycles=2",
            "-e", "Simulator__IntervalSeconds=1",
            "-e", "Simulator__RunOverrides__SensorCount=1",
            "simulator"
        ))
        Invoke-Captured "compose-ps" "docker" ($composeArgs + @("ps", "--all"))
        Invoke-Captured "compose-logs" "docker" ($composeArgs + @(
            "logs", "--no-color", "--timestamps", "backoffice-api", "prevention", "frontend"
        ))
    }

    $executionStatus = if (-not $SkipDotnet -and -not $SkipFrontend -and -not $SkipContainers) {
        "PROVED_IN_OWNER_ENVIRONMENT"
    }
    else {
        "PARTIAL_OWNER_EXECUTION"
    }

    [pscustomobject]@{
        phase = "G1"
        status = $executionStatus
        completedAtUtc = [DateTimeOffset]::UtcNow
        artifactRoot = $artifactRoot
        skipped = [ordered]@{
            dotnet = [bool]$SkipDotnet
            frontend = [bool]$SkipFrontend
            containers = [bool]$SkipContainers
        }
    } | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $artifactRoot "g1-result.json")
}
catch {
    [pscustomobject]@{
        phase = "G1"
        status = "FAILED_OR_PARTIAL"
        failedAtUtc = [DateTimeOffset]::UtcNow
        message = $_.Exception.Message
        artifactRoot = $artifactRoot
    } | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $artifactRoot "g1-result.json")
    throw
}
finally {
    if (-not $SkipContainers -and -not $KeepContainers -and (Get-Command docker -ErrorAction SilentlyContinue)) {
        $downArguments = $composeArgs + @("down", "--remove-orphans")
        & docker @downArguments 2>&1 |
            Tee-Object -FilePath (Join-Path $artifactRoot "compose-down.log")
    }

    if ($generatedJwtSigningKey) {
        Remove-Item Env:NP_G1_JWT_SIGNING_KEY -ErrorAction SilentlyContinue
    }

    Get-ChildItem -Path $artifactRoot -File -Recurse |
        Get-FileHash -Algorithm SHA256 |
        Sort-Object Path |
        ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), $_.Path.Substring($artifactRoot.Length + 1).Replace('\\', '/') } |
        Set-Content -Encoding utf8 (Join-Path $artifactRoot "checksums.sha256")
}
