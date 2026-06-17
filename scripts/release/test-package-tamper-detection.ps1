param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [string]$TamperRelativePath = "webUI/index.html",
    [string]$OutputRoot = ".testbin/release-tamper"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$archive = (Resolve-Path $ArchivePath).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$tamperRoot = Join-Path $repoRoot (Join-Path $OutputRoot "tamper-$timestamp")
$extractRoot = Join-Path $tamperRoot "expanded"
$tamperedArchive = Join-Path $tamperRoot "tampered.zip"

New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
Expand-Archive -Path $archive -DestinationPath $extractRoot -Force

$target = Join-Path $extractRoot ($TamperRelativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
if (-not (Test-Path $target)) {
    throw "Tamper target not found: $TamperRelativePath"
}

$bytes = [System.IO.File]::ReadAllBytes($target)
if ($bytes.Length -eq 0) {
    throw "Tamper target is empty: $TamperRelativePath"
}

$bytes[0] = $bytes[0] -bxor 0x01
[System.IO.File]::WriteAllBytes($target, $bytes)

Compress-Archive -Path (Join-Path $extractRoot "*") -DestinationPath $tamperedArchive -CompressionLevel Optimal
$tamperedHash = Get-FileHash -Algorithm SHA256 -LiteralPath $tamperedArchive
"$($tamperedHash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $tamperedArchive)" |
    Set-Content -Path "$tamperedArchive.sha256" -Encoding ASCII

try {
    & (Join-Path $PSScriptRoot "test-clean-install.ps1") -ArchivePath $tamperedArchive -OutputRoot (Join-Path $OutputRoot "clean-install")
    throw "Tampered package unexpectedly passed clean-install validation."
}
catch {
    if ($_.Exception.Message -like "*unexpectedly passed*") {
        throw
    }

    $result = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        sourceArchive = $archive
        tamperedArchive = $tamperedArchive
        tamperedPath = $TamperRelativePath
        status = "tamper-detected"
        validationFailure = $_.Exception.Message
    }

    $result | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $tamperRoot "tamper-detection-result.json") -Encoding UTF8
    Write-Host "Tamper detection validation complete: $tamperRoot"
}
