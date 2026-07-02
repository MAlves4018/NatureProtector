Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-G81PortableEvidencePath {
    param(
        [Parameter(Mandatory)][string]$EvidenceDirectory,
        [Parameter(Mandatory)][string]$Path
    )

    $evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
    $filePath = [IO.Path]::GetFullPath($Path)
    $relativePath = [IO.Path]::GetRelativePath($evidenceRoot, $filePath)
    $portablePath = $relativePath.Replace([IO.Path]::DirectorySeparatorChar, [char]'/')

    if ([IO.Path]::IsPathRooted($portablePath)) {
        throw "Evidence checksum path must be relative: $portablePath"
    }
    if (($portablePath -split '/') -contains '..') {
        throw "Evidence checksum path must not escape the evidence directory: $portablePath"
    }
    if ($portablePath.Contains('\')) {
        throw "Evidence checksum path must use portable separators: $portablePath"
    }

    return $portablePath
}

function Write-G81EvidenceChecksums {
    param([Parameter(Mandatory)][string]$EvidenceDirectory)

    $root = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
    $checksumPath = Join-Path $root "checksums.sha256"
    Remove-Item -LiteralPath $checksumPath -Force -ErrorAction SilentlyContinue

    $entries = @(
        Get-ChildItem -LiteralPath $root -File -Recurse |
            ForEach-Object {
                $portablePath = Get-G81PortableEvidencePath -EvidenceDirectory $root -Path $_.FullName
                [pscustomobject]@{
                    Path = $portablePath
                    Line = "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant())  $portablePath"
                }
            } |
            Sort-Object -Property Path
    )

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllLines($checksumPath, [string[]]@($entries | ForEach-Object { $_.Line }), $utf8NoBom)
    return $checksumPath
}

function Test-G81EvidenceChecksums {
    param([Parameter(Mandatory)][string]$Directory)

    $checksumPath = Join-Path $Directory "checksums.sha256"
    foreach ($line in Get-Content -LiteralPath $checksumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "Malformed evidence checksum entry: $line" }
        $relative = $Matches[2]
        if ([IO.Path]::IsPathRooted($relative) -or ($relative -split '/') -contains '..' -or $relative.Contains('\')) {
            throw "Unsafe evidence path: $relative"
        }
        if ($relative -eq "checksums.sha256") {
            throw "Evidence checksum file must not include itself."
        }
        $path = Join-Path $Directory $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Checksummed evidence file is missing: $relative" }
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
        if ($actual -ne $Matches[1]) { throw "Evidence checksum mismatch: $relative" }
    }
}
