<#
.SYNOPSIS
Runs the guided local onboarding flow for NatureProtector.

.DESCRIPTION
This script orchestrates local setup without deleting data. It checks
prerequisites, optionally invokes the opt-in installer, requires an existing
`.env`, starts infrastructure, validates it, and optionally starts the runtime.
#>

[CmdletBinding()]
param(
    [switch]$StartRuntime,
    [switch]$OpenBrowser,
    [switch]$InstallMissing,
    [switch]$Yes
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "== $Name =="
    & $Action
}

function Invoke-ScriptFile {
    param(
        [string]$Path,
        [string[]]$Arguments = @()
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "powershell.exe"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $allArguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $Path
    ) + $Arguments

    $quotedArguments = foreach ($argument in $allArguments) {
        if ($argument -match '\s|"') {
            '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $argument
        }
    }

    $startInfo.Arguments = ($quotedArguments -join " ")

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    [void]$process.Start()

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()

    $process.WaitForExit()

    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        Write-Host $stdout.TrimEnd()
    }

    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        if ($process.ExitCode -eq 0) {
            Write-Host $stderr.TrimEnd()
        }
        else {
            Write-Error $stderr.TrimEnd()
        }
    }

    return [int]$process.ExitCode
}

function Invoke-RequiredScript {
    param(
        [string]$Name,
        [string]$Path,
        [string[]]$Arguments = @()
    )

    Invoke-Step $Name {
        $exitCode = Invoke-ScriptFile -Path $Path -Arguments $Arguments
        if ($exitCode -ne 0) {
            throw "$Name failed with exit code $exitCode."
        }
    }
}

function Assert-InfluxTokenReady {
    param([string]$DotEnvPath)

    $values = @{}
    foreach ($rawLine in Get-Content -LiteralPath $DotEnvPath) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#") -or -not $line.Contains("=")) {
            continue
        }

        $parts = $line.Split("=", 2)
        $values[$parts[0].Trim()] = $parts[1].Trim().Trim('"')
    }

    if (-not $values.ContainsKey("INFLUXDB_TOKEN") -or [string]::IsNullOrWhiteSpace($values["INFLUXDB_TOKEN"])) {
        throw "Missing INFLUXDB_TOKEN in .env. Set a local apiv3_ token before starting infrastructure."
    }

    if ([string]$values["INFLUXDB_TOKEN"] -match "REPLACE_WITH|CHANGE_ME|<") {
        throw "INFLUXDB_TOKEN in .env is still a placeholder. Edit .env, set a local apiv3_ token, then rerun this setup script."
    }
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
Set-Location $repoRoot

$prereqScript = Join-Path $repoRoot "scripts\setup\Test-LocalPrerequisites.ps1"
$installScript = Join-Path $repoRoot "scripts\setup\Install-LocalPrerequisites.ps1"
$upScript = Join-Path $repoRoot "infra\scripts\up.ps1"
$baselineScript = Join-Path $repoRoot "scripts\setup\Test-LocalBaseline.ps1"
$runtimeScript = Join-Path $repoRoot "scripts\dev\start-local-runtime.ps1"

$prereqExitCode = 0
Invoke-Step "Checking local prerequisites" {
    $script:prereqExitCode = Invoke-ScriptFile -Path $prereqScript
}

if ($prereqExitCode -ne 0) {
    if ($InstallMissing) {
        $installArgs = @("-InstallMissing")
        if ($Yes) {
            $installArgs += "-Yes"
        }

        Invoke-RequiredScript "Installing missing prerequisites" $installScript $installArgs
        Invoke-RequiredScript "Rechecking local prerequisites" $prereqScript
    }
    else {
        throw "Prerequisite check failed. Run .\scripts\setup\Install-LocalPrerequisites.ps1 -WhatIf, then rerun setup when dependencies are ready."
    }
}

$dotEnvPath = Join-Path $repoRoot ".env"
if (-not (Test-Path -LiteralPath $dotEnvPath)) {
    throw ".env is missing. Create it manually from .env.example, review local values, then rerun this setup script. This script will not create or edit .env."
}

Assert-InfluxTokenReady $dotEnvPath

Invoke-RequiredScript "Starting local infrastructure" $upScript
Invoke-RequiredScript "Validating infrastructure baseline" $baselineScript @("-InfrastructureOnly")

if ($OpenBrowser -and -not $StartRuntime) {
    Write-Warning "-OpenBrowser was provided without -StartRuntime; no browser will be opened."
}

if ($StartRuntime) {
    $runtimeArgs = @("-ForceRestart")
    if ($OpenBrowser) {
        $runtimeArgs += "-OpenBrowser"
    }

    Invoke-RequiredScript "Starting local runtime" $runtimeScript $runtimeArgs
    Invoke-RequiredScript "Validating full local baseline" $baselineScript @("-Full")
}

Write-Host ""
Write-Host "Local environment setup completed."
