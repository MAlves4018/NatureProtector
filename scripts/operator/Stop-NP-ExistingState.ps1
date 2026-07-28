[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,
    [string]$OutputRoot = '',
    [switch]$PreserveDockerVolumes,
    [switch]$AllowUnknownPortOwners
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'NatureProtector.sln') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'docker-compose.yml') -PathType Leaf)) {
    throw "RepositoryRoot is not a NatureProtector checkout: $RepositoryRoot"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepositoryRoot ('artifacts\operator-test-kit\cleanup-' + (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$started = (Get-Date).ToUniversalTime()
$rows = [System.Collections.Generic.List[object]]::new()
$unknownOwners = [System.Collections.Generic.List[object]]::new()

function Add-Row {
    param([string]$Kind, [string]$Status, [string]$Detail, [AllowNull()][object]$Data = $null)
    $rows.Add([pscustomobject]@{ kind = $Kind; status = $Status; detail = $Detail; data = $Data }) | Out-Null
    Write-Host "[$Status] $Kind - $Detail"
}

function Get-CommandLine {
    param([int]$ProcessId)
    try { return [string](Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction Stop).CommandLine } catch { return '' }
}

function Get-ExcludedProcessIds {
    $set = [System.Collections.Generic.HashSet[int]]::new()
    $cursor = [int]$PID
    while ($cursor -gt 0 -and $set.Add($cursor)) {
        try {
            $info = Get-CimInstance Win32_Process -Filter "ProcessId=$cursor" -ErrorAction Stop
            $cursor = [int]$info.ParentProcessId
        }
        catch { break }
    }
    return ,$set
}

function Test-IsNatureProtectorProcess {
    param([string]$Name, [string]$CommandLine)
    if ($Name.StartsWith('NatureProtector.', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($Name -in @('dotnet', 'node', 'npm', 'npx', 'vite', 'pwsh', 'powershell', 'python', 'py')) {
        return -not [string]::IsNullOrWhiteSpace($CommandLine) -and
            $CommandLine.IndexOf($RepositoryRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
    return $false
}

$excluded = Get-ExcludedProcessIds
$processSnapshot = @(Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
    [pscustomobject]@{ id = $_.Id; name = $_.ProcessName; commandLine = Get-CommandLine -ProcessId $_.Id }
})
$processSnapshot | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'processes-before.json') -Encoding utf8

foreach ($process in $processSnapshot) {
    if ($excluded.Contains([int]$process.id)) { continue }
    if (-not (Test-IsNatureProtectorProcess -Name ([string]$process.name) -CommandLine ([string]$process.commandLine))) { continue }
    try {
        Stop-Process -Id ([int]$process.id) -Force -ErrorAction Stop
        Add-Row -Kind 'process' -Status 'STOPPED' -Detail "PID $($process.id) ($($process.name))" -Data $process
    }
    catch {
        Add-Row -Kind 'process' -Status 'FAIL' -Detail "Could not stop PID $($process.id): $($_.Exception.Message)" -Data $process
    }
}

Start-Sleep -Milliseconds 750
foreach ($port in @(5254, 5260, 5173)) {
    $connections = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    foreach ($connection in $connections) {
        $ownerId = [int]$connection.OwningProcess
        if ($excluded.Contains($ownerId)) { continue }
        $owner = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
        if ($null -eq $owner) { continue }
        $commandLine = Get-CommandLine -ProcessId $ownerId
        if (Test-IsNatureProtectorProcess -Name $owner.ProcessName -CommandLine $commandLine) {
            try {
                Stop-Process -Id $ownerId -Force -ErrorAction Stop
                Add-Row -Kind 'port-owner' -Status 'STOPPED' -Detail "Port $port; PID $ownerId ($($owner.ProcessName))"
            }
            catch { Add-Row -Kind 'port-owner' -Status 'FAIL' -Detail "Port $port; PID $ownerId could not be stopped: $($_.Exception.Message)" }
        }
        else {
            $entry = [pscustomobject]@{ port = $port; processId = $ownerId; processName = $owner.ProcessName; commandLine = $commandLine }
            $unknownOwners.Add($entry) | Out-Null
            Add-Row -Kind 'port-owner' -Status 'BLOCKED' -Detail "Port $port belongs to unrelated/unknown PID $ownerId ($($owner.ProcessName)); it was not killed." -Data $entry
        }
    }
}

$dockerAvailable = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
$dockerReady = $false
if ($dockerAvailable) {
    $dockerInfoLog = Join-Path $OutputRoot 'docker-info.log'
    & docker info *> $dockerInfoLog
    $dockerReady = $LASTEXITCODE -eq 0
}
if ($dockerReady) {
    $composeArgs = @('compose', '--project-directory', $RepositoryRoot, '-f', (Join-Path $RepositoryRoot 'docker-compose.yml'), 'down', '--remove-orphans')
    if (-not $PreserveDockerVolumes) { $composeArgs += '-v' }
    & docker @composeArgs 2>&1 | Tee-Object -FilePath (Join-Path $OutputRoot 'docker-compose-down.log') | Write-Host
    if ($LASTEXITCODE -eq 0) {
        Add-Row -Kind 'docker' -Status 'CLEAN' -Detail $(if ($PreserveDockerVolumes) { 'Project containers and networks removed; volumes preserved.' } else { 'Project containers, networks and compose volumes removed.' })
    }
    else { Add-Row -Kind 'docker' -Status 'FAIL' -Detail "docker compose down failed with exit code $LASTEXITCODE." }
}
elseif ($dockerAvailable) {
    Add-Row -Kind 'docker' -Status 'BLOCKED' -Detail 'Docker CLI exists, but the Docker daemon is not reachable.'
}
else {
    Add-Row -Kind 'docker' -Status 'BLOCKED' -Detail 'Docker CLI is not installed or not present in PATH.'
}

$tempCli = Join-Path ([System.IO.Path]::GetTempPath()) 'np-local-cli'
if (Test-Path -LiteralPath $tempCli -PathType Container) {
    Remove-Item -LiteralPath $tempCli -Recurse -Force -ErrorAction SilentlyContinue
    Add-Row -Kind 'temporary-state' -Status 'CLEAN' -Detail "Removed $tempCli"
}

$remaining = @(Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
    $commandLine = Get-CommandLine -ProcessId $_.Id
    if (-not $excluded.Contains([int]$_.Id) -and (Test-IsNatureProtectorProcess -Name $_.ProcessName -CommandLine $commandLine)) {
        [pscustomobject]@{ id = $_.Id; name = $_.ProcessName; commandLine = $commandLine }
    }
})
$remaining | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'processes-after.json') -Encoding utf8
$unknownOwners | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'unknown-port-owners.json') -Encoding utf8
$rows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'cleanup.csv') -NoTypeInformation -Encoding utf8

$failed = @($rows | Where-Object status -eq 'FAIL').Count
$blockedUnknown = $unknownOwners.Count -gt 0 -and -not $AllowUnknownPortOwners
$status = if ($failed -gt 0 -or $remaining.Count -gt 0) { 'FAIL' } elseif ($blockedUnknown) { 'BLOCKED' } else { 'PASS' }
$result = [ordered]@{
    schemaVersion = 1
    status = $status
    repositoryRoot = $RepositoryRoot
    dockerAvailable = $dockerAvailable
    dockerReady = $dockerReady
    dockerVolumesRemoved = -not [bool]$PreserveDockerVolumes
    unknownPortOwnerCount = $unknownOwners.Count
    remainingNatureProtectorProcessCount = $remaining.Count
    startedAtUtc = $started.ToString('o')
    completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    outputRoot = $OutputRoot
}
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'cleanup-result.json') -Encoding utf8
Write-Host ($result | ConvertTo-Json -Depth 10)
if ($status -eq 'PASS') { exit 0 }
if ($status -eq 'BLOCKED') { exit 2 }
exit 1
