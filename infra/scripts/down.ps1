<#
.SYNOPSIS
Stops the local Docker Compose baseline.

.DESCRIPTION
Changes to the repository root and runs docker compose down with explicit
command and exit-code validation.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Invoke-CheckedExternalCommand {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$FailureMessage,
        [string]$WorkingDirectory = (Get-Location).Path
    )

    $command = Get-Command $FileName -ErrorAction Stop
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $command.Source
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        $startInfo.WorkingDirectory = $WorkingDirectory
    }

    if ($Arguments.Count -gt 0) {
        $quotedArguments = foreach ($argument in $Arguments) {
            if ($argument -match '\s|"') {
                '"' + ($argument -replace '"', '\"') + '"'
            }
            else {
                $argument
            }
        }

        $startInfo.Arguments = ($quotedArguments -join " ")
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $text = (($standardOutput + $standardError) | Out-String).Trim()
    if ($process.ExitCode -ne 0) {
        throw "$FailureMessage Output: $text"
    }

    return $text
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$composeFile = Join-Path $projectRoot "docker-compose.yml"

if (-not (Test-Path -LiteralPath $composeFile)) {
    throw "docker-compose.yml not found at $composeFile."
}

$output = Invoke-CheckedExternalCommand `
    "docker" `
    @("compose", "--project-directory", $projectRoot, "-f", $composeFile, "down") `
    "Docker Compose down failed." `
    $projectRoot

if (-not [string]::IsNullOrWhiteSpace($output)) {
    Write-Host $output
}
