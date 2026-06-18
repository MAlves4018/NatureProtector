param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [string]$OutputRoot = "artifacts/release/functional-package-smoke",
    [int]$StartupTimeoutSeconds = 30,
    [string]$ComposeFile = "docker-compose.yml",
    [string]$PostgresService = "postgres",
    [string]$PostgresContainerId = "",
    [string]$PostgresHost = "localhost",
    [int]$PostgresPort = 5433,
    [string]$PostgresUser = "np",
    [string]$PostgresPassword = "np_dev_pass",
    [switch]$KeepExpandedPackage
)

$ErrorActionPreference = "Stop"

function Convert-ToPackagePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return $Path.Substring($Root.Length).TrimStart("\", "/").Replace("\", "/")
}

function Read-ChecksumFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $entries = [ordered]@{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $Path) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line -notmatch "^(?<hash>[a-fA-F0-9]{64})\s+(?<path>.+)$") {
            throw "Invalid checksum line ${lineNumber}: $line"
        }

        $relativePath = $Matches["path"].Trim().Replace("\", "/")
        if ([System.IO.Path]::IsPathRooted($relativePath) -or $relativePath.Contains("../") -or $relativePath.Contains("..\")) {
            throw "Unsafe checksum path: $relativePath"
        }

        $entries[$relativePath] = $Matches["hash"].ToLowerInvariant()
    }

    return $entries
}

function Assert-ArchiveChecksum {
    param([Parameter(Mandatory = $true)][string]$Archive)

    $checksumPath = "$Archive.sha256"
    if (-not (Test-Path -LiteralPath $checksumPath)) {
        throw "Archive checksum file is missing: $checksumPath"
    }

    $entries = Read-ChecksumFile -Path $checksumPath
    $archiveName = Split-Path -Leaf $Archive
    if (-not $entries.Contains($archiveName)) {
        throw "Archive checksum file does not contain an entry for $archiveName."
    }

    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Archive).Hash.ToLowerInvariant()
    if ($entries[$archiveName] -ne $actual) {
        throw "Archive checksum mismatch for $archiveName."
    }
}

function Assert-PackageChecksums {
    param([Parameter(Mandatory = $true)][string]$InstallRoot)

    $checksumsPath = Join-Path $InstallRoot "checksums.sha256"
    if (-not (Test-Path -LiteralPath $checksumsPath)) {
        throw "checksums.sha256 is missing from the expanded package."
    }

    $entries = Read-ChecksumFile -Path $checksumsPath
    if ($entries.Count -eq 0) {
        throw "checksums.sha256 is empty."
    }

    $packageFiles = Get-ChildItem -LiteralPath $InstallRoot -File -Recurse |
        Where-Object { $_.Name -ne "checksums.sha256" } |
        ForEach-Object { Convert-ToPackagePath -Root $InstallRoot -Path $_.FullName } |
        Sort-Object

    $packageFileSet = @{}
    foreach ($file in $packageFiles) {
        $packageFileSet[$file] = $true
    }

    foreach ($file in $packageFiles) {
        if (-not $entries.Contains($file)) {
            throw "Package contains a file not covered by checksums.sha256: $file"
        }
    }

    foreach ($relativePath in $entries.Keys) {
        if (-not $packageFileSet.ContainsKey($relativePath)) {
            throw "checksums.sha256 contains an entry for a missing file: $relativePath"
        }

        $fullPath = Join-Path $InstallRoot ($relativePath.Replace("/", "\"))
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
        if ($entries[$relativePath] -ne $actual) {
            throw "Package checksum mismatch for $relativePath."
        }
    }

    return $entries.Count
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Invoke-DockerPostgres {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Container,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & docker exec $Container @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "docker exec $Container $($Arguments -join ' ') failed with exit code $LASTEXITCODE. Output: $($output | Out-String)"
    }

    return (($output | Out-String).Trim())
}

function Resolve-PostgresContainer {
    param(
        [string]$ConfiguredContainer,
        [string]$ComposePath,
        [string]$ServiceName
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredContainer)) {
        return $ConfiguredContainer
    }

    if (Test-Path -LiteralPath $ComposePath) {
        $composeOutput = & docker compose -f $ComposePath ps -q $ServiceName 2>&1
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($composeOutput | Out-String))) {
            return (($composeOutput | Select-Object -First 1).ToString().Trim())
        }
    }

    $containers = & docker ps --format "{{.Names}}" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Could not enumerate Docker containers. Output: $($containers | Out-String)"
    }

    $match = $containers |
        Where-Object { $_ -match "postgres" } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($match)) {
        throw "Could not resolve a running PostgreSQL container for package bootstrap smoke."
    }

    return $match.Trim()
}

function Assert-SafeDatabaseName {
    param([Parameter(Mandatory = $true)][string]$DatabaseName)

    if ($DatabaseName -notmatch '^np_pkg_smoke_[a-zA-Z0-9_]+$') {
        throw "Refusing to create/drop unsafe package smoke database name: $DatabaseName"
    }
}

function Invoke-PackageApiHealthCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApiDirectory,
        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,
        [int]$TimeoutSeconds
    )

    $apiDll = Join-Path $ApiDirectory "NatureProtector.Backoffice.Api.dll"
    if (-not (Test-Path -LiteralPath $apiDll)) {
        throw "Backoffice API package DLL is missing: $apiDll"
    }

    $port = Get-FreeTcpPort
    $baseUrl = "http://127.0.0.1:$port"
    $stdoutPath = Join-Path $EvidenceDirectory "backoffice-api.stdout.log"
    $stderrPath = Join-Path $EvidenceDirectory "backoffice-api.stderr.log"

    $previousEnvironment = @{
        "ASPNETCORE_URLS" = $env:ASPNETCORE_URLS
        "ASPNETCORE_ENVIRONMENT" = $env:ASPNETCORE_ENVIRONMENT
        "DOTNET_ENVIRONMENT" = $env:DOTNET_ENVIRONMENT
        "BackofficeApi__ControlPlaneEnabled" = $env:BackofficeApi__ControlPlaneEnabled
    }

    $process = $null
    try {
        $env:ASPNETCORE_URLS = $baseUrl
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:DOTNET_ENVIRONMENT = "Development"
        $env:BackofficeApi__ControlPlaneEnabled = "false"

        $process = Start-Process `
            -FilePath "dotnet" `
            -ArgumentList @($apiDll) `
            -WorkingDirectory $ApiDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -WindowStyle Hidden `
            -PassThru

        $deadline = (Get-Date).AddSeconds([Math]::Max(1, $TimeoutSeconds))
        $lastError = $null
        while ((Get-Date) -lt $deadline) {
            if ($process.HasExited) {
                throw "Backoffice API exited before /health became ready. ExitCode=$($process.ExitCode)"
            }

            try {
                $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 2
                if ([int]$response.StatusCode -eq 200) {
                    return [ordered]@{
                        status = "passed"
                        url = "$baseUrl/health"
                        statusCode = [int]$response.StatusCode
                        processId = $process.Id
                    }
                }
            }
            catch {
                $lastError = $_.Exception.Message
            }

            Start-Sleep -Milliseconds 250
        }

        throw "Backoffice API /health did not become ready within $TimeoutSeconds seconds. Last error: $lastError"
    }
    finally {
        foreach ($key in $previousEnvironment.Keys) {
            if ($null -eq $previousEnvironment[$key]) {
                Remove-Item "env:$key" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "env:$key" $previousEnvironment[$key]
            }
        }

        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(5000) | Out-Null
        }
    }
}

function Invoke-BootstrapProbe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BootstrapDirectory,
        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$HostName,
        [int]$Port,
        [Parameter(Mandatory = $true)]
        [string]$UserName,
        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    $bootstrapDll = Join-Path $BootstrapDirectory "NatureProtector.Postgres.Bootstrap.dll"
    if (-not (Test-Path -LiteralPath $bootstrapDll)) {
        throw "Postgres bootstrap package DLL is missing: $bootstrapDll"
    }

    $previousEnvironment = @{
        "POSTGRES_HOST" = $env:POSTGRES_HOST
        "POSTGRES_PORT" = $env:POSTGRES_PORT
        "POSTGRES_DB" = $env:POSTGRES_DB
        "POSTGRES_USER" = $env:POSTGRES_USER
        "POSTGRES_PASSWORD" = $env:POSTGRES_PASSWORD
    }

    $runs = @()
    try {
        $env:POSTGRES_HOST = $HostName
        $env:POSTGRES_PORT = $Port.ToString()
        $env:POSTGRES_DB = $Database
        $env:POSTGRES_USER = $UserName
        $env:POSTGRES_PASSWORD = $Password

        foreach ($runIndex in 1..2) {
            $stdoutPath = Join-Path $EvidenceDirectory "postgres-bootstrap-run-$runIndex.stdout.log"
            $stderrPath = Join-Path $EvidenceDirectory "postgres-bootstrap-run-$runIndex.stderr.log"

            $process = Start-Process `
                -FilePath "dotnet" `
                -ArgumentList @($bootstrapDll) `
                -WorkingDirectory $BootstrapDirectory `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath `
                -WindowStyle Hidden `
                -Wait `
                -PassThru

            $runs += [ordered]@{
                run = $runIndex
                exitCode = $process.ExitCode
                stdout = (Split-Path -Leaf $stdoutPath)
                stderr = (Split-Path -Leaf $stderrPath)
            }

            if ($process.ExitCode -ne 0) {
                return [ordered]@{
                    status = "failed"
                    database = $Database
                    idempotentRuns = @($runs)
                }
            }
        }

        return [ordered]@{
            status = "passed"
            database = $Database
            idempotentRuns = @($runs)
        }
    }
    finally {
        foreach ($key in $previousEnvironment.Keys) {
            if ($null -eq $previousEnvironment[$key]) {
                Remove-Item "env:$key" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "env:$key" $previousEnvironment[$key]
            }
        }
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$archive = (Resolve-Path $ArchivePath).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $timestamp)
$externalRoot = Join-Path ([System.IO.Path]::GetTempPath()) (Join-Path "NatureProtectorPackageSmoke" $timestamp)
$installRoot = Join-Path $externalRoot "expanded"

if ($externalRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Functional package smoke install root must be outside the source tree: $externalRoot"
}

New-Item -ItemType Directory -Force -Path $runDirectory, $installRoot | Out-Null

$apiHealth = $null
$bootstrapProbe = $null
$installRootRemoved = $false
$bootstrapDatabase = "np_pkg_smoke_$(Get-Date -Format 'yyyyMMddHHmmss')"
$postgresContainer = $null

try {
    Assert-ArchiveChecksum -Archive $archive
    Expand-Archive -Path $archive -DestinationPath $installRoot -Force
    $checksumLineCount = Assert-PackageChecksums -InstallRoot $installRoot

    $frontendIndex = Join-Path $installRoot "webUI\index.html"
    if (-not (Test-Path -LiteralPath $frontendIndex)) {
        throw "Frontend package index.html is missing."
    }

    $frontendAssetCount = (Get-ChildItem -LiteralPath (Join-Path $installRoot "webUI") -File -Recurse | Measure-Object).Count
    if ($frontendAssetCount -le 1) {
        throw "Frontend package does not contain built static assets."
    }

    $apiHealth = Invoke-PackageApiHealthCheck `
        -ApiDirectory (Join-Path $installRoot "publish\backoffice-api") `
        -EvidenceDirectory $runDirectory `
        -TimeoutSeconds $StartupTimeoutSeconds

    Assert-SafeDatabaseName -DatabaseName $bootstrapDatabase
    $postgresContainer = Resolve-PostgresContainer `
        -ConfiguredContainer $PostgresContainerId `
        -ComposePath (Join-Path $repoRoot $ComposeFile) `
        -ServiceName $PostgresService
    Invoke-DockerPostgres `
        -Container $postgresContainer `
        -Arguments @("createdb", "-U", $PostgresUser, $bootstrapDatabase) | Out-Null

    $bootstrapProbe = Invoke-BootstrapProbe `
        -BootstrapDirectory (Join-Path $installRoot "publish\postgres-bootstrap") `
        -EvidenceDirectory $runDirectory `
        -Database $bootstrapDatabase `
        -HostName $PostgresHost `
        -Port $PostgresPort `
        -UserName $PostgresUser `
        -Password $PostgresPassword

    $status = if ($apiHealth.status -eq "passed" -and $bootstrapProbe.status -eq "passed") {
        "ready"
    }
    else {
        "failed"
    }

    $manifest = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        archive = $archive
        expandedInstallRoot = $installRoot
        installRootOutsideSourceTree = -not $installRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)
        checksumLineCount = $checksumLineCount
        apiHealth = $apiHealth
        frontend = [ordered]@{
            status = "passed"
            index = "webUI/index.html"
            assetCount = $frontendAssetCount
        }
        postgresBootstrapFromPackage = $bootstrapProbe
        preventionSimulatorWorkloadFromPackage = [ordered]@{
            status = "not_executed"
            reason = "Functional package smoke validates API health, static assets and idempotent bootstrap. Full Simulator->RabbitMQ->Prevention workload remains covered by published runtime process tests."
        }
        status = $status
        scope = "Local functional package smoke. Proves package checksum integrity, frontend static assets, Backoffice.Api /health startup and idempotent Postgres bootstrap from an expansion outside the source tree. Does not prove full Simulator->RabbitMQ->Prevention workload from the package."
    }

    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runDirectory "functional-package-smoke-result.json") -Encoding UTF8
    Write-Host "Functional package smoke complete: $runDirectory"
}
finally {
    if ($postgresContainer -and -not [string]::IsNullOrWhiteSpace($bootstrapDatabase)) {
        try {
            Invoke-DockerPostgres `
                -Container $postgresContainer `
                -Arguments @("dropdb", "-U", $PostgresUser, "--if-exists", $bootstrapDatabase) | Out-Null
        }
        catch {
            Write-Warning "Failed to drop package smoke database '$bootstrapDatabase': $($_.Exception.Message)"
        }
    }

    if (-not $KeepExpandedPackage -and (Test-Path -LiteralPath $externalRoot)) {
        Remove-Item -LiteralPath $externalRoot -Recurse -Force -ErrorAction SilentlyContinue
        $installRootRemoved = $true
    }

    if ($installRootRemoved -and (Test-Path -LiteralPath (Join-Path $runDirectory "functional-package-smoke-result.json"))) {
        $existing = Get-Content -LiteralPath (Join-Path $runDirectory "functional-package-smoke-result.json") -Raw | ConvertFrom-Json
        $existing | Add-Member -NotePropertyName expandedInstallRootRemoved -NotePropertyValue $true -Force
        $existing | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runDirectory "functional-package-smoke-result.json") -Encoding UTF8
    }
}
