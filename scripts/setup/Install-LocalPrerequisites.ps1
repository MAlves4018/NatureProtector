<#
.SYNOPSIS
Suggests or installs local tools needed by the NatureProtector baseline.

.DESCRIPTION
This script is opt-in. By default it only reports missing supported
dependencies and prints suggested winget commands. It never edits .env, never
starts Docker Compose, and never deletes Docker volumes.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$DryRun,
    [switch]$InstallMissing,
    [switch]$InstallGit,
    [switch]$InstallDotNet,
    [switch]$InstallNode,
    [switch]$InstallDocker,
    [switch]$Yes
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"

function Test-CommandExists {
    param([string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Get-DotNetExpectedMajor {
    param([string]$RepoRoot)

    $propsPath = Join-Path $RepoRoot "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $propsPath)) {
        return 9
    }

    try {
        $props = [xml](Get-Content -Raw -LiteralPath $propsPath)
        $targetFramework = [string]$props.Project.PropertyGroup.TargetFramework
        if ($targetFramework -match '^net(?<major>\d+)\.') {
            return [int]$Matches["major"]
        }
    }
    catch {
    }

    return 9
}

function Test-DotNetExpected {
    param([int]$ExpectedMajor)

    if (-not (Test-CommandExists "dotnet")) {
        return $false
    }

    $version = (& dotnet --version 2>$null | Select-Object -First 1 | Out-String).Trim()
    return $version -match "^$ExpectedMajor\."
}

function Add-Dependency {
    param(
        [System.Collections.Generic.List[object]]$Dependencies,
        [string]$Key,
        [string]$Name,
        [bool]$Installed,
        [string]$WingetId,
        [string]$ManualInstructions,
        [bool]$Selected
    )

    [void]$Dependencies.Add([pscustomobject]@{
        Key = $Key
        Name = $Name
        Installed = $Installed
        WingetId = $WingetId
        ManualInstructions = $ManualInstructions
        Selected = $Selected
    })
}

function Confirm-Install {
    param([string]$Name)

    if ($Yes) {
        return $true
    }

    $answer = Read-Host "Install $Name now? Type YES to continue"
    return $answer -eq "YES"
}

function Install-WithWinget {
    param(
        [string]$Name,
        [string]$WingetId
    )

    $arguments = @("install", "--id", $WingetId, "--exact", "--source", "winget")
    Write-Host "Running: winget $($arguments -join ' ')"

    if ($DryRun -or $WhatIfPreference) {
        return
    }

    if (-not (Confirm-Install $Name)) {
        Write-Host "Skipped $Name."
        return
    }

    if ($PSCmdlet.ShouldProcess($Name, "winget install $WingetId")) {
        & winget @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "winget install failed for $Name with exit code $LASTEXITCODE."
        }
    }
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
Set-Location $repoRoot

$expectedDotNetMajor = Get-DotNetExpectedMajor $repoRoot
$dependencies = [System.Collections.Generic.List[object]]::new()

Add-Dependency $dependencies "Git" "Git" (Test-CommandExists "git") "Git.Git" "Install Git from https://git-scm.com/download/win" ($InstallMissing -or $InstallGit)
Add-Dependency $dependencies "DotNet" ".NET SDK $expectedDotNetMajor" (Test-DotNetExpected $expectedDotNetMajor) "Microsoft.DotNet.SDK.$expectedDotNetMajor" "Install .NET SDK $expectedDotNetMajor from https://dotnet.microsoft.com/download" ($InstallMissing -or $InstallDotNet)
Add-Dependency $dependencies "Node" "Node.js LTS and npm" ((Test-CommandExists "node") -and (Test-CommandExists "npm")) "OpenJS.NodeJS.LTS" "Install Node.js LTS from https://nodejs.org/" ($InstallMissing -or $InstallNode)
Add-Dependency $dependencies "Docker" "Docker Desktop" (Test-CommandExists "docker") "Docker.DockerDesktop" "Install Docker Desktop from https://www.docker.com/products/docker-desktop/" ($InstallMissing -or $InstallDocker)

$dockerEngine = if (Test-CommandExists "docker") { Invoke-NpExternalCommand "docker" @("info", "--format", "{{.ServerVersion}}") } else { $null }
$compose = if (Test-CommandExists "docker") { Invoke-NpExternalCommand "docker" @("compose", "version") } else { $null }

Write-Host "NatureProtector local prerequisite installer"
Write-Host "Repository root: $repoRoot"
Write-Host ""

$missing = @($dependencies | Where-Object { -not $_.Installed })
foreach ($dependency in $dependencies) {
    if ($dependency.Installed) {
        Write-Host "[OK]   $($dependency.Name) found"
    }
    else {
        Write-Host "[FAIL] $($dependency.Name) not found"
        Write-Host "Suggested:"
        Write-Host "  winget install --id $($dependency.WingetId) --exact --source winget"
        Write-Host "Manual:"
        Write-Host "  $($dependency.ManualInstructions)"
        Write-Host ""
    }
}

if ($dockerEngine -and $dockerEngine.ExitCode -ne 0) {
    Write-Host "[WARN] Docker engine is not reachable. Open Docker Desktop manually and wait until it is running."
}

if ($compose -and $compose.ExitCode -ne 0) {
    Write-Host "[WARN] Docker Compose v2 was not confirmed. Docker Desktop normally provides it."
}

if ($missing.Count -eq 0) {
    Write-Host "All supported installable dependencies were found."
    Write-Host "Run scripts\setup\Test-LocalPrerequisites.ps1 again to validate engine status and ports."
    exit 0
}

$selected = @($dependencies | Where-Object { -not $_.Installed -and $_.Selected })
if (-not $InstallMissing -and -not $InstallGit -and -not $InstallDotNet -and -not $InstallNode -and -not $InstallDocker) {
    Write-Host "Run with -InstallMissing to install supported missing dependencies, or use an individual -Install* flag."
    exit 0
}

if (-not (Test-CommandExists "winget")) {
    Write-Host "[FAIL] winget was not found. Install the missing dependencies manually:"
    foreach ($dependency in $selected) {
        Write-Host "  $($dependency.Name): $($dependency.ManualInstructions)"
    }
    exit 1
}

foreach ($dependency in $selected) {
    Install-WithWinget $dependency.Name $dependency.WingetId
}

Write-Host ""
Write-Host "Installation phase complete. Open a new PowerShell window, start Docker Desktop if needed, then run:"
Write-Host "  .\scripts\setup\Test-LocalPrerequisites.ps1"
