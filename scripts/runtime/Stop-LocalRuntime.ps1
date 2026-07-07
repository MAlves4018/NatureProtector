[CmdletBinding()]
param(
    [int[]]$Ports = @(5254, 5260, 5173)
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

function Get-ProcessCommandLine {
    param([int]$ProcessId)

    try {
        $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop
        return [string]$processInfo.CommandLine
    }
    catch {
        return ''
    }
}

function Test-IsNatureProtectorProcess {
    param(
        [string]$RepositoryRoot,
        [string]$ProcessName,
        [string]$CommandLine
    )

    if ($ProcessName.StartsWith('NatureProtector.', [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if (($ProcessName -eq 'dotnet' -or $ProcessName -eq 'node' -or $ProcessName -eq 'pwsh') -and
        $CommandLine.IndexOf($RepositoryRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
    }

    return $false
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$stopped = @()

foreach ($port in $Ports) {
    $connections = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    foreach ($connection in $connections) {
        $process = Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            continue
        }

        $commandLine = Get-ProcessCommandLine -ProcessId $process.Id
        if (-not (Test-IsNatureProtectorProcess -RepositoryRoot $repoRoot -ProcessName $process.ProcessName -CommandLine $commandLine)) {
            Write-Warning "Port ${port} is owned by PID $($process.Id) ($($process.ProcessName)); not stopped because it does not look like this repo runtime."
            continue
        }

        Write-Host "Stopping local runtime process on port ${port}: PID $($process.Id) ($($process.ProcessName))"
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
        $stopped += [pscustomobject]@{
            Port = $port
            ProcessId = $process.Id
            ProcessName = $process.ProcessName
        }
    }
}

if ($stopped.Count -eq 0) {
    Write-Host "No NatureProtector local runtime process was listening on ports $($Ports -join ', ')."
}
else {
    $stopped | ConvertTo-Json -Depth 4 | Write-Host
}
