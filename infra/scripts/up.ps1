<#
.SYNOPSIS
Levanta a baseline local em Docker Compose.

.DESCRIPTION
O script muda para a raiz do repositório, exige que exista um `.env` local,
prepara o ficheiro local de admin token do InfluxDB 3 e executa
`docker compose up -d` para arrancar a infraestrutura de apoio.

.NOTES
- Deve ser usado antes de correr a API, o simulador ou a pipeline de prevenção
  quando estes dependem dos serviços containerizados.
#>

[CmdletBinding()]
param(
    [switch]$SkipWorkspacePreparation
)

$ErrorActionPreference = "Stop"

function Assert-CommandAvailable {
    param(
        [string]$Command,
        [string]$InstallHint
    )

    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        throw "$Command was not found on PATH. $InstallHint"
    }
}

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
            if ($argument -match '\s|"' ) {
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
    $exitCode = $process.ExitCode
    if ($text -match "error during connect|Acesso negado|Access is denied|permission denied|Cannot connect") {
        $exitCode = 1
    }

    if ($exitCode -ne 0) {
        throw "$FailureMessage Output: $text"
    }

    return $text
}

# Move para a raiz do projeto, independentemente de onde o script é chamado.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
Set-Location $ProjectRoot

$WorkspaceScript = Join-Path $ProjectRoot "scripts\workspace.ps1"
if (-not $SkipWorkspacePreparation -and (Test-Path -LiteralPath $WorkspaceScript)) {
    $workspaceOutput = Invoke-CheckedExternalCommand `
        "pwsh" `
        @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $WorkspaceScript, "setup") `
        "Workspace setup failed before Docker infrastructure startup." `
        $ProjectRoot

    if (-not [string]::IsNullOrWhiteSpace($workspaceOutput)) {
        Write-Host $workspaceOutput
    }
}

$ComposeFile = Join-Path $ProjectRoot "docker-compose.yml"

if (-not (Test-Path -LiteralPath $ComposeFile)) {
    throw "docker-compose.yml not found at $ComposeFile. ProjectRoot resolved to $ProjectRoot."
}

Assert-CommandAvailable "docker" "Install Docker Desktop, open it, and re-run scripts\setup\Test-LocalPrerequisites.ps1."
Invoke-CheckedExternalCommand "docker" @("info", "--format", "{{.ServerVersion}}") "Docker engine is not reachable. Start Docker Desktop before running up.ps1." | Out-Null
Invoke-CheckedExternalCommand "docker" @("compose", "version") "Docker Compose v2 is not available. Install/update Docker Desktop before running up.ps1." | Out-Null

if (-not (Test-Path ".env")) {
    throw ".env is missing. Run scripts\np.ps1 init-local -Force before running infra/scripts/up.ps1."
}

# Prepara o ficheiro local de admin token usado pelo InfluxDB 3 no primeiro arranque
# depois de volumes novos. Este script will not create or edit .env.
$InfluxAdminTokenScript = Join-Path $ProjectRoot "scripts\influx\Ensure-InfluxAdminTokenFile.ps1"
if (Test-Path $InfluxAdminTokenScript) {
    $tokenOutput = Invoke-CheckedExternalCommand `
        "pwsh" `
        @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $InfluxAdminTokenScript) `
        "Influx admin token script failed."

    if (-not [string]::IsNullOrWhiteSpace($tokenOutput)) {
        Write-Host $tokenOutput
    }
}
else {
    throw "Influx admin token script not found at $InfluxAdminTokenScript."
}

# Levanta a infraestrutura em background para que os restantes processos possam
# ser arrancados em separado.
Write-Host "Starting Docker Compose infrastructure..."

$composeOutput = Invoke-CheckedExternalCommand `
    "docker" `
    @("compose", "--project-directory", $ProjectRoot, "-f", $ComposeFile, "up", "-d") `
    "Docker Compose up failed." `
    $ProjectRoot

if (-not [string]::IsNullOrWhiteSpace($composeOutput)) {
    Write-Host $composeOutput
}

# Garante explicitamente a database temporal usada pela baseline local.
$InfluxEnsureScript = Join-Path $ProjectRoot "scripts\influx\Ensure-InfluxDatabase.ps1"
if (Test-Path $InfluxEnsureScript) {
    Write-Host "Ensuring local InfluxDB database..."

    try {
        & $InfluxEnsureScript
    }
    catch {
        throw "Influx database provisioning script failed. $($_.Exception.Message)"
    }
}
else {
    Write-Warning "Influx provisioning script not found at $InfluxEnsureScript. Run scripts\influx\Ensure-InfluxDatabase.ps1 when available."
}
