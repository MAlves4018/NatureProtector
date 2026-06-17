param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [string]$OutputRoot = ".testbin/clean-install"
)

$ErrorActionPreference = "Stop"

function Convert-ToRelativePackagePath {
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
    foreach ($line in Get-Content -Path $Path) {
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
    if (-not (Test-Path $checksumPath)) {
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
    if (-not (Test-Path $checksumsPath)) {
        throw "checksums.sha256 is missing."
    }

    $entries = Read-ChecksumFile -Path $checksumsPath
    if ($entries.Count -eq 0) {
        throw "checksums.sha256 is empty."
    }

    $packageFiles = Get-ChildItem -Path $InstallRoot -File -Recurse |
        Where-Object { $_.Name -ne "checksums.sha256" } |
        ForEach-Object { Convert-ToRelativePackagePath -Root $InstallRoot -Path $_.FullName } |
        Sort-Object

    $packageFileSet = @{}
    foreach ($file in $packageFiles) {
        $packageFileSet[$file] = $true
    }

    $missingChecksumEntries = @()
    foreach ($file in $packageFiles) {
        if (-not $entries.Contains($file)) {
            $missingChecksumEntries += $file
        }
    }

    if ($missingChecksumEntries.Count -gt 0) {
        throw "Package contains files not covered by checksums.sha256: $($missingChecksumEntries -join ', ')"
    }

    $orphanChecksumEntries = @()
    foreach ($relativePath in $entries.Keys) {
        if (-not $packageFileSet.ContainsKey($relativePath)) {
            $orphanChecksumEntries += $relativePath
            continue
        }

        $fullPath = Join-Path $InstallRoot ($relativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
        if ($entries[$relativePath] -ne $actual) {
            throw "Package checksum mismatch for $relativePath."
        }
    }

    if ($orphanChecksumEntries.Count -gt 0) {
        throw "checksums.sha256 contains entries for missing files: $($orphanChecksumEntries -join ', ')"
    }

    return $entries.Count
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$archive = (Resolve-Path $ArchivePath).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$installRoot = Join-Path $repoRoot (Join-Path $OutputRoot "natureprotector-clean-$timestamp")

Assert-ArchiveChecksum -Archive $archive

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Expand-Archive -Path $archive -DestinationPath $installRoot -Force

$requiredPaths = @(
    "publish/backoffice-api/NatureProtector.Backoffice.Api.dll",
    "publish/prevention-host/NatureProtector.Prevention.Host.dll",
    "publish/simulator-host/NatureProtector.Simulator.Host.dll",
    "publish/postgres-bootstrap/NatureProtector.Postgres.Bootstrap.dll",
    "webUI/index.html",
    "release-evidence-manifest.json",
    "checksums.sha256",
    "evidence/dotnet-dependency-inventory.json",
    "evidence/npm-dependency-inventory.json"
)

$missing = @()
foreach ($path in $requiredPaths) {
    if (-not (Test-Path (Join-Path $installRoot $path))) {
        $missing += $path
    }
}

if ($missing.Count -gt 0) {
    throw "Clean install package is missing required paths: $($missing -join ', ')"
}

$checksumLineCount = Assert-PackageChecksums -InstallRoot $installRoot

$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    archive = $archive
    archiveChecksum = "$archive.sha256"
    installRoot = $installRoot
    requiredPathCount = $requiredPaths.Count
    checksumLineCount = $checksumLineCount
    status = "ready"
    scope = "Structural clean-install validation with archive and package hash verification. Service startup still requires configured PostgreSQL, RabbitMQ and InfluxDB."
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $installRoot "clean-install-result.json") -Encoding UTF8
Write-Host "Clean install validation complete: $installRoot"
