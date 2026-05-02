<#
.SYNOPSIS
Checks local tools and repository files needed by the NatureProtector baseline.

.DESCRIPTION
This script is read-only. It does not install packages, run restore, run
npm install, or modify repository files.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"

function Find-RepositoryRoot {
    $current = Get-Item -LiteralPath $PSScriptRoot

    while ($null -ne $current) {
        $solution = Join-Path $current.FullName "NatureProtector.sln"
        $compose = Join-Path $current.FullName "docker-compose.yml"

        if ((Test-Path -LiteralPath $solution) -and (Test-Path -LiteralPath $compose)) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not locate repository root from $PSScriptRoot."
}

$script:Results = @()

function Add-Result {
    param(
        [ValidateSet("OK", "WARN", "FAIL")]
        [string]$Status,
        [string]$Name,
        [string]$Detail,
        [bool]$Required = $true
    )

    $script:Results += [pscustomobject]@{
        Status = $Status
        Name = $Name
        Detail = $Detail
        Required = $Required
    }

    $label = ("[{0}]" -f $Status).PadRight(7)
    Write-Host "$label $Name - $Detail"
}

function Get-CommandPath {
    param([string]$Name)
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return $null
    }

    return $command.Source
}

function Invoke-VersionCommand {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    try {
        $output = & $Command @Arguments 2>$null | Select-Object -First 1
        if ($LASTEXITCODE -ne 0 -and $null -eq $output) {
            return $null
        }

        return ($output | Out-String).Trim()
    }
    catch {
        return $null
    }
}

function Test-Tool {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$VersionArguments,
        [bool]$Required = $true
    )

    $path = Get-CommandPath $Command
    if ($null -eq $path) {
        if ($Required) {
            Add-Result "FAIL" $Name "$Command was not found on PATH." $true
        }
        else {
            Add-Result "WARN" $Name "$Command was not found on PATH." $false
        }
        return
    }

    $version = Invoke-VersionCommand $Command $VersionArguments
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "found at $path"
    }

    Add-Result "OK" $Name $version $Required
}

$repoRoot = Find-RepositoryRoot
Set-Location $repoRoot

Write-Host "NatureProtector local prerequisite check"
Write-Host "Repository root: $repoRoot"
Write-Host ""

Test-Tool ".NET SDK" "dotnet" @("--version") $true
Test-Tool "Docker CLI" "docker" @("--version") $true

if (Get-CommandPath "docker") {
    $composeVersion = Invoke-VersionCommand "docker" @("compose", "version")
    if ([string]::IsNullOrWhiteSpace($composeVersion)) {
        Add-Result "FAIL" "Docker Compose" "docker compose is not available." $true
    }
    else {
        Add-Result "OK" "Docker Compose" $composeVersion $true
    }
}

$psVersion = $PSVersionTable.PSVersion.ToString()
Add-Result "OK" "PowerShell" $psVersion $true

Test-Tool "Git" "git" @("--version") $false

if (Test-Path -LiteralPath (Join-Path $repoRoot ".env.example")) {
    Add-Result "OK" ".env.example" "found" $true
}
else {
    Add-Result "FAIL" ".env.example" "missing from repository root" $true
}

if (Test-Path -LiteralPath (Join-Path $repoRoot ".env")) {
    Add-Result "OK" ".env" "found" $false
}
else {
    Add-Result "WARN" ".env" "missing; create it from .env.example before running the baseline" $false
}

$frontendPackage = Join-Path $repoRoot "webUI\package.json"
if (Test-Path -LiteralPath $frontendPackage) {
    Add-Result "WARN" "Frontend candidate" "webUI/package.json found; frontend tools are optional for the backend baseline" $false
    Test-Tool "Node.js" "node" @("--version") $false
    Test-Tool "npm" "npm" @("--version") $false

    try {
        $package = Get-Content -Raw -LiteralPath $frontendPackage | ConvertFrom-Json
        if ($null -eq $package.scripts) {
            Add-Result "WARN" "Frontend scripts" "package.json has no scripts; use npm install, npx vite build, then npx vite" $false
        }
        elseif ($null -eq $package.scripts.build) {
            Add-Result "WARN" "Frontend build script" "no build script found; use npx vite build" $false
        }
        else {
            Add-Result "OK" "Frontend build script" $package.scripts.build $false
        }
    }
    catch {
        Add-Result "WARN" "Frontend package.json" "could not parse package.json: $($_.Exception.Message)" $false
    }
}
else {
    Add-Result "OK" "Frontend candidate" "webUI/package.json not found; Node/npm not required" $false
}

Test-Tool "Strawberry Perl / Perl" "perl" @("--version") $false

$miktexCommand = Get-CommandPath "miktex"
$pdflatexCommand = Get-CommandPath "pdflatex"
if ($miktexCommand -or $pdflatexCommand) {
    $detail = "found"
    if ($pdflatexCommand) {
        $version = Invoke-VersionCommand "pdflatex" @("--version")
        if (-not [string]::IsNullOrWhiteSpace($version)) {
            $detail = $version
        }
    }
    Add-Result "OK" "MiKTeX / LaTeX" $detail $false
}
else {
    Add-Result "WARN" "MiKTeX / LaTeX" "not found; only needed for report/documentation workflows" $false
}

Write-Host ""
$requiredFailures = @($script:Results | Where-Object { $_.Status -eq "FAIL" -and $_.Required }).Count
$warnings = @($script:Results | Where-Object { $_.Status -eq "WARN" }).Count
$failures = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Summary: $requiredFailures required failure(s), $failures total failure(s), $warnings warning(s)."

if ($requiredFailures -gt 0) {
    exit 1
}

exit 0
