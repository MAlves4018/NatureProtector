[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
. (Join-Path $repoRoot "scripts/cloud/EvidenceChecksums.ps1")

$root = Join-Path ([IO.Path]::GetTempPath()) ("np-checksum-portability-" + [guid]::NewGuid().ToString("N"))
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $root "nested/deeper") | Out-Null
    Set-Content -LiteralPath (Join-Path $root "root.txt") -Value "root" -Encoding utf8
    Set-Content -LiteralPath (Join-Path $root "nested/evidence.txt") -Value "evidence" -Encoding utf8
    '{"status":"ok"}' | Set-Content -LiteralPath (Join-Path $root "nested/deeper/result.json") -Encoding utf8
    "old  checksums.sha256" | Set-Content -LiteralPath (Join-Path $root "checksums.sha256") -Encoding utf8

    $checksumPath = Write-G81EvidenceChecksums -EvidenceDirectory $root
    $firstRun = Get-Content -LiteralPath $checksumPath
    Test-G81EvidenceChecksums -Directory $root
    $checksumPath = Write-G81EvidenceChecksums -EvidenceDirectory $root
    $secondRun = Get-Content -LiteralPath $checksumPath

    if (($firstRun -join "`n") -ne ($secondRun -join "`n")) {
        throw "Checksum generation is not deterministic across repeated runs."
    }

    $expectedPaths = @(
        "nested/deeper/result.json",
        "nested/evidence.txt",
        "root.txt"
    )
    if ($secondRun.Count -ne $expectedPaths.Count) {
        throw "Expected $($expectedPaths.Count) checksum entries, got $($secondRun.Count)."
    }

    $observedPaths = @()
    foreach ($line in $secondRun) {
        if ($line -notmatch '^([0-9a-f]{64})  (.+)$') {
            throw "Malformed checksum line: $line"
        }
        $relativePath = $Matches[2]
        $observedPaths += $relativePath
        if ($relativePath.Contains('\')) { throw "Path contains a backslash: $relativePath" }
        if ([IO.Path]::IsPathRooted($relativePath)) { throw "Path is absolute: $relativePath" }
        if (($relativePath -split '/') -contains '..') { throw "Path escapes root: $relativePath" }
        if ($relativePath -eq "checksums.sha256") { throw "Checksum file included itself." }
        $filePath = Join-Path $root $relativePath
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Missing checksummed file: $relativePath" }
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $filePath).Hash.ToLowerInvariant()
        if ($actual -ne $Matches[1]) { throw "Checksum mismatch: $relativePath" }
    }

    $expectedJoined = ($expectedPaths | Sort-Object) -join "`n"
    $observedJoined = ($observedPaths | Sort-Object) -join "`n"
    if ($expectedJoined -ne $observedJoined) {
        throw "Unexpected checksum paths: $observedJoined"
    }

    Write-Host "CHECKSUM_PORTABILITY_RUNTIME_TEST=PASS"
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
