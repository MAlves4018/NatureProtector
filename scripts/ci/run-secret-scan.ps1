param(
    [string]$RepositoryRoot = ".",
    [string]$OutputDirectory = ".\artifacts\secret-scan",
    [string]$GitleaksVersion = "8.28.0",
    [string]$HistoryLogOptions = "--all",
    [switch]$SkipInstall,
    [switch]$IncludeUntracked
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path $RepositoryRoot).Path
$outputRoot = Join-Path $repoRoot $OutputDirectory
$configPath = Join-Path $repoRoot ".gitleaks.toml"
$summaryPath = Join-Path $outputRoot "secret-scan-summary.json"

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

function Test-WindowsPlatform {
    return [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
}

function Test-MacPlatform {
    if (Test-WindowsPlatform) {
        return $false
    }

    try {
        return ((& uname -s 2>$null) -eq "Darwin")
    }
    catch {
        return $false
    }
}

function Get-GitleaksAssetName {
    param([string]$Version)

    if (Test-WindowsPlatform) {
        return "gitleaks_$($Version)_windows_x64.zip"
    }

    if (Test-MacPlatform) {
        return "gitleaks_$($Version)_darwin_x64.tar.gz"
    }

    return "gitleaks_$($Version)_linux_x64.tar.gz"
}

function Find-GitleaksOnPath {
    $command = Get-Command gitleaks -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $null
}

function Install-Gitleaks {
    param(
        [string]$Version,
        [string]$InstallRoot
    )

    $assetName = Get-GitleaksAssetName -Version $Version
    $downloadUrl = "https://github.com/gitleaks/gitleaks/releases/download/v$Version/$assetName"
    $archivePath = Join-Path $InstallRoot $assetName

    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null

    Write-Host "Downloading Gitleaks $Version from official GitHub release."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath -UseBasicParsing

    if ($assetName.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) {
        Expand-Archive -Path $archivePath -DestinationPath $InstallRoot -Force
    }
    else {
        & tar -xzf $archivePath -C $InstallRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to extract $archivePath."
        }
    }

    $candidate = Get-ChildItem -LiteralPath $InstallRoot -Recurse -File |
        Where-Object { $_.Name -eq "gitleaks" -or $_.Name -eq "gitleaks.exe" } |
        Select-Object -First 1

    if (-not $candidate) {
        throw "Gitleaks executable was not found after extracting $assetName."
    }

    return $candidate.FullName
}

function Get-GitleaksPath {
    $path = Find-GitleaksOnPath
    if ($path) {
        return $path
    }

    if ($SkipInstall) {
        throw "Gitleaks was not found on PATH and -SkipInstall was set."
    }

    $installRoot = Join-Path $repoRoot "artifacts\tools\gitleaks-$GitleaksVersion"
    if (Test-Path -LiteralPath $installRoot -PathType Container) {
        $existing = Get-ChildItem -LiteralPath $installRoot -Recurse -File |
            Where-Object { $_.Name -eq "gitleaks" -or $_.Name -eq "gitleaks.exe" } |
            Select-Object -First 1

        if ($existing) {
            return $existing.FullName
        }
    }

    return Install-Gitleaks -Version $GitleaksVersion -InstallRoot $installRoot
}

function Invoke-Gitleaks {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [string]$ReportPath,
        [string]$StandardInput = $null
    )

    Write-Host "Running $Name..."

    if ($null -ne $StandardInput) {
        $StandardInput | & $script:gitleaksPath @Arguments
    }
    else {
        & $script:gitleaksPath @Arguments
    }

    $exitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        "[]" | Set-Content -Encoding utf8 -Path $ReportPath
    }

    if ($exitCode -eq 0) {
        Write-Host "$Name passed. Report: $ReportPath"
        return "passed"
    }

    if ($exitCode -eq 1) {
        Write-Warning "$Name found potential secrets. Report: $ReportPath"
        $script:hasFindings = $true
        return "findings"
    }

    throw "$Name failed with exit code $exitCode."
}

function Get-GitLines {
    param([string[]]$Arguments)

    $output = & git -C $repoRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Copy-RepositorySnapshot {
    param(
        [string]$DestinationRoot,
        [string[]]$RelativePaths
    )

    $destinationRootFull = [System.IO.Path]::GetFullPath($DestinationRoot)
    New-Item -ItemType Directory -Force -Path $destinationRootFull | Out-Null

    foreach ($relativePath in $RelativePaths) {
        $sourcePath = Join-Path $repoRoot $relativePath
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            continue
        }

        $normalizedRelativePath = $relativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar
        $destinationPath = Join-Path $destinationRootFull $normalizedRelativePath
        $destinationPathFull = [System.IO.Path]::GetFullPath($destinationPath)

        if (-not $destinationPathFull.StartsWith($destinationRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to copy path outside snapshot root: $relativePath"
        }

        $parent = Split-Path -Parent $destinationPathFull
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPathFull -Force
    }
}

function Remove-SnapshotDirectory {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $leafName = Split-Path -Leaf $fullPath

    if (-not $fullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $leafName.StartsWith("np-secret-scan-", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected snapshot directory: $Path"
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Missing Gitleaks config: $configPath"
}

$script:gitleaksPath = Get-GitleaksPath
$script:hasFindings = $false

$versionOutput = & $script:gitleaksPath version
Write-Host "Using $versionOutput"

$commonArgs = @(
    "--config", $configPath,
    "--gitleaks-ignore-path", $repoRoot,
    "--report-format", "json",
    "--redact=100",
    "--no-banner",
    "--no-color"
)

$historyReport = Join-Path $outputRoot "gitleaks-history.json"
$stagedReport = Join-Path $outputRoot "gitleaks-staged.json"
$workingTreeReport = Join-Path $outputRoot "gitleaks-working-tree.json"

$scans = New-Object System.Collections.Generic.List[object]

$historyArguments = @(
    "git",
    "--log-opts", $HistoryLogOptions,
    "--report-path", $historyReport
) + $commonArgs + @($repoRoot)

$historyStatus = Invoke-Gitleaks -Name "Gitleaks history scan" -ReportPath $historyReport -Arguments $historyArguments
$scans.Add([ordered]@{ name = "history"; status = $historyStatus; report = $historyReport; logOptions = $HistoryLogOptions })

$stagedArguments = @(
    "git",
    "--staged",
    "--report-path", $stagedReport
) + $commonArgs + @($repoRoot)

$stagedStatus = Invoke-Gitleaks -Name "Gitleaks staged scan" -ReportPath $stagedReport -Arguments $stagedArguments
$scans.Add([ordered]@{ name = "staged"; status = $stagedStatus; report = $stagedReport })

$snapshotRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("np-secret-scan-" + [System.Guid]::NewGuid().ToString("N"))
try {
    $pathArguments = @("ls-files")
    if ($IncludeUntracked) {
        $pathArguments += @("--cached", "--modified", "--others", "--exclude-standard")
    }

    $paths = Get-GitLines -Arguments $pathArguments |
        Where-Object { $_ -notlike "graphify-out/*" } |
        Sort-Object -Unique

    Copy-RepositorySnapshot -DestinationRoot $snapshotRoot -RelativePaths $paths

    $workingTreeArguments = @(
        "dir",
        "--report-path", $workingTreeReport
    ) + $commonArgs + @($snapshotRoot)

    $workingTreeStatus = Invoke-Gitleaks -Name "Gitleaks working tree scan" -ReportPath $workingTreeReport -Arguments $workingTreeArguments
    $scans.Add([ordered]@{ name = "working-tree"; status = $workingTreeStatus; report = $workingTreeReport; includeUntracked = [bool]$IncludeUntracked; fileCount = @($paths).Count })
}
finally {
    Remove-SnapshotDirectory -Path $snapshotRoot
}

Write-Host "Running high-signal regex canary scan..."
& (Join-Path $PSScriptRoot "check-secret-canaries.ps1") -RepositoryRoot $repoRoot
if ($LASTEXITCODE -ne 0) {
    throw "Secret canary scan failed with exit code $LASTEXITCODE."
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    scanner = "gitleaks"
    scannerVersion = $versionOutput
    config = $configPath
    outputDirectory = $outputRoot
    redaction = "100%"
    scans = $scans
    canaryScan = "passed"
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -Path $summaryPath
Write-Host "Secret scan summary written to $summaryPath"

if ($script:hasFindings) {
    throw "Gitleaks found potential secrets. Review redacted reports under $outputRoot."
}

Write-Host "No secrets found by Gitleaks or canary scan."
