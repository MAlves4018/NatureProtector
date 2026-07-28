Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-NpAcceptanceJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowNull()][object]$Value
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    $Value | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Resolve-NpAcceptanceOutputRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [AllowEmptyString()][string]$OutputRoot,
        [Parameter(Mandatory = $true)][string]$RunId,
        [switch]$Overwrite
    )

    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts'))
    $acceptanceRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'final-acceptance'))
    if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
        $OutputRoot = Join-Path $acceptanceRoot $RunId
    }
    elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
        $OutputRoot = Join-Path $RepoRoot $OutputRoot
    }

    $resolved = [System.IO.Path]::GetFullPath($OutputRoot)
    $acceptancePrefix = $acceptanceRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($resolved.Equals($acceptanceRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not ($resolved + [System.IO.Path]::DirectorySeparatorChar).StartsWith($acceptancePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Acceptance output must be a run-scoped child of: $acceptanceRoot"
    }

    if (Test-Path -LiteralPath $resolved) {
        $existing = @(Get-ChildItem -LiteralPath $resolved -Force -ErrorAction SilentlyContinue)
        if ($existing.Count -gt 0 -and -not $Overwrite) {
            throw "Acceptance output already exists and is not empty: $resolved. Use -Overwrite to replace only this run directory."
        }
        if ($existing.Count -gt 0 -and $Overwrite) {
            $existing | Remove-Item -Recurse -Force
        }
    }
    New-Item -ItemType Directory -Force -Path $resolved | Out-Null
    return $resolved
}


function Resolve-NpAcceptanceCommandPath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Executable)

    if (Test-Path -LiteralPath $Executable -PathType Leaf) {
        return [System.IO.Path]::GetFullPath($Executable)
    }

    $candidatePaths = [System.Collections.Generic.List[string]]::new()
    $names = [System.Collections.Generic.List[string]]::new()
    $names.Add($Executable) | Out-Null
    if ([string]::IsNullOrWhiteSpace([System.IO.Path]::GetExtension($Executable))) {
        foreach ($suffix in @('.exe', '.com', '.cmd', '.bat', '.ps1')) {
            $names.Add("$Executable$suffix") | Out-Null
        }
    }

    foreach ($name in @($names | Select-Object -Unique)) {
        foreach ($command in @(Get-Command $name -All -ErrorAction SilentlyContinue)) {
            $source = ''
            if ($command.PSObject.Properties.Name -contains 'Source') { $source = [string]$command.Source }
            if ([string]::IsNullOrWhiteSpace($source) -and $command.PSObject.Properties.Name -contains 'Path') { $source = [string]$command.Path }
            if (-not [string]::IsNullOrWhiteSpace($source) -and (Test-Path -LiteralPath $source -PathType Leaf)) {
                $candidatePaths.Add([System.IO.Path]::GetFullPath($source)) | Out-Null
            }
        }
    }

    if ($Executable -ieq 'npm') {
        foreach ($nodeName in @('node.exe', 'node')) {
            foreach ($nodeCommand in @(Get-Command $nodeName -All -ErrorAction SilentlyContinue)) {
                $nodeSource = ''
                if ($nodeCommand.PSObject.Properties.Name -contains 'Source') { $nodeSource = [string]$nodeCommand.Source }
                if ([string]::IsNullOrWhiteSpace($nodeSource) -and $nodeCommand.PSObject.Properties.Name -contains 'Path') { $nodeSource = [string]$nodeCommand.Path }
                if (-not [string]::IsNullOrWhiteSpace($nodeSource)) {
                    $sibling = Join-Path (Split-Path -Parent $nodeSource) 'npm.cmd'
                    if (Test-Path -LiteralPath $sibling -PathType Leaf) {
                        $candidatePaths.Add([System.IO.Path]::GetFullPath($sibling)) | Out-Null
                    }
                }
            }
        }
    }

    $rank = @{ '.exe' = 0; '.com' = 1; '.cmd' = 2; '.bat' = 3; '.ps1' = 4 }
    $selected = @($candidatePaths | Select-Object -Unique | Sort-Object @{ Expression = {
        $extension = [System.IO.Path]::GetExtension($_).ToLowerInvariant()
        if ($rank.ContainsKey($extension)) { return [int]$rank[$extension] }
        return 10
    } }, @{ Expression = { $_.Length } }, @{ Expression = { $_ } } | Select-Object -First 1)

    if ($selected.Count -eq 0) {
        throw "Command not found or not executable: $Executable"
    }
    return [string]$selected[0]
}

function New-NpAcceptanceProcessInvocation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [string[]]$Arguments = @()
    )

    $resolvedPath = Resolve-NpAcceptanceCommandPath -Executable $Executable
    $extension = [System.IO.Path]::GetExtension($resolvedPath).ToLowerInvariant()
    if ($extension -notin @('.cmd', '.bat', '.ps1')) {
        return [pscustomobject]@{
            FilePath = $resolvedPath
            Arguments = @($Arguments)
            ResolvedPath = $resolvedPath
            Wrapped = $false
        }
    }

    $payloadJson = [ordered]@{
        commandPath = $resolvedPath
        arguments = @($Arguments)
    } | ConvertTo-Json -Depth 5 -Compress
    $payloadBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($payloadJson))
    $childScriptTemplate = @'
$payloadJson = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__PAYLOAD__'))
$payload = $payloadJson | ConvertFrom-Json
$global:LASTEXITCODE = $null
& ([string]$payload.commandPath) @($payload.arguments)
$commandSucceeded = $?
$nativeExitCode = $LASTEXITCODE
if ($null -ne $nativeExitCode) { exit [int]$nativeExitCode }
if (-not $commandSucceeded) { exit 1 }
exit 0
'@
    $childScript = $childScriptTemplate.Replace('__PAYLOAD__', $payloadBase64)
    $encodedCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($childScript))
    $pwshPath = if (Test-Path -LiteralPath (Join-Path $PSHOME 'pwsh.exe') -PathType Leaf) {
        Join-Path $PSHOME 'pwsh.exe'
    }
    else {
        Resolve-NpAcceptanceCommandPath -Executable 'pwsh'
    }

    return [pscustomobject]@{
        FilePath = $pwshPath
        Arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encodedCommand)
        ResolvedPath = $resolvedPath
        Wrapped = $true
    }
}

function Get-NpAcceptanceMissingCommands {
    [CmdletBinding()]
    param([string[]]$Commands = @())

    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($command in @($Commands | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)) {
        try { [void](Resolve-NpAcceptanceCommandPath -Executable $command) }
        catch { $missing.Add($command) | Out-Null }
    }
    return @($missing)
}

function ConvertTo-NpAcceptanceQuotedArgument {
    [CmdletBinding()]
    param([AllowEmptyString()][string]$Value)

    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value.Replace('"', '\"')) + '"'
}

function ConvertTo-NpAcceptanceCommandText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [string[]]$Arguments = @()
    )

    $parts = @($Executable) + @($Arguments | ForEach-Object { ConvertTo-NpAcceptanceQuotedArgument -Value $_ })
    return ($parts -join ' ')
}

function Invoke-NpAcceptanceProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Executable,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$OutputDirectory,
        [int]$TimeoutSeconds = 900
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $stdoutPath = Join-Path $OutputDirectory 'runner.stdout.log'
    $stderrPath = Join-Path $OutputDirectory 'runner.stderr.log'
    $combinedPath = Join-Path $OutputDirectory 'runner.log'
    Remove-Item -LiteralPath $stdoutPath, $stderrPath, $combinedPath -Force -ErrorAction SilentlyContinue

    $startedAt = (Get-Date).ToUniversalTime()
    $commandText = ConvertTo-NpAcceptanceCommandText -Executable $Executable -Arguments $Arguments
    try {
        $invocation = New-NpAcceptanceProcessInvocation -Executable $Executable -Arguments $Arguments
        $quotedArguments = @($invocation.Arguments | ForEach-Object { ConvertTo-NpAcceptanceQuotedArgument -Value $_ })
        $process = Start-Process -FilePath $invocation.FilePath `
            -ArgumentList $quotedArguments `
            -WorkingDirectory $WorkingDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru

        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            try { $process.Kill($true) } catch { }
            try { $process.WaitForExit(5000) | Out-Null } catch { }
            $exitCode = 124
        }
        else {
            $exitCode = [int]$process.ExitCode
        }

        $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { '' }
        $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
        @(
            "> $commandText"
            "exitCode=$exitCode"
            "timedOut=$timedOut"
            ''
            $stdout
            $stderr
        ) | Set-Content -LiteralPath $combinedPath -Encoding utf8

        $completedAt = (Get-Date).ToUniversalTime()
        return [pscustomobject]@{
            Id = $Id
            Command = $commandText
            ExitCode = $exitCode
            TimedOut = $timedOut
            StartedAtUtc = $startedAt.ToString('o')
            CompletedAtUtc = $completedAt.ToString('o')
            DurationSeconds = [Math]::Round(($completedAt - $startedAt).TotalSeconds, 3)
            LogPath = $combinedPath
            StartError = ''
        }
    }
    catch {
        $completedAt = (Get-Date).ToUniversalTime()
        @(
            "> $commandText"
            'exitCode=125'
            'timedOut=false'
            ''
            $_.Exception.Message
        ) | Set-Content -LiteralPath $combinedPath -Encoding utf8
        return [pscustomobject]@{
            Id = $Id
            Command = $commandText
            ExitCode = 125
            TimedOut = $false
            StartedAtUtc = $startedAt.ToString('o')
            CompletedAtUtc = $completedAt.ToString('o')
            DurationSeconds = [Math]::Round(($completedAt - $startedAt).TotalSeconds, 3)
            LogPath = $combinedPath
            StartError = $_.Exception.Message
        }
    }
}

function Get-NpAcceptanceDeclaredResult {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$OutputDirectory)

    $resultFiles = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File -Filter 'acceptance-result.json' -ErrorAction SilentlyContinue)
    if ($resultFiles.Count -eq 0) { return $null }
    if ($resultFiles.Count -gt 1) {
        return [pscustomobject]@{
            Status = 'HARNESS_ERROR'
            Detail = "Multiple acceptance-result.json files were produced for one stage: $($resultFiles.Count)."
            Path = $OutputDirectory
            NativeStatus = ''
        }
    }

    $resultPath = $resultFiles[0].FullName
    try {
        $payload = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        $status = [string]$payload.status
        $allowed = @('PASS', 'FAIL', 'BLOCKED_PREREQUISITE', 'HARNESS_ERROR')
        if ($status -notin $allowed) {
            throw "Unsupported declared status '$status'."
        }
        $nativeStatus = if ($payload.PSObject.Properties.Name -contains 'nativeStatus') { [string]$payload.nativeStatus } else { '' }
        $detail = "Delegated harness declared $status."
        if (-not [string]::IsNullOrWhiteSpace($nativeStatus)) {
            $detail = "Delegated harness declared $status (native=$nativeStatus)."
        }
        return [pscustomobject]@{
            Status = $status
            Detail = $detail
            Path = $resultPath
            NativeStatus = $nativeStatus
        }
    }
    catch {
        return [pscustomobject]@{
            Status = 'HARNESS_ERROR'
            Detail = "Invalid acceptance-result.json: $($_.Exception.Message)"
            Path = $resultPath
            NativeStatus = ''
        }
    }
}

function Get-NpAcceptanceStageStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ExitCode,
        [switch]$TimedOut,
        [AllowEmptyString()][string]$StartError
    )

    if ($TimedOut -or -not [string]::IsNullOrWhiteSpace($StartError) -or $ExitCode -in @(124, 125)) {
        return 'HARNESS_ERROR'
    }
    if ($ExitCode -eq 0) { return 'PASS' }
    if ($ExitCode -in @(2, 3)) { return 'BLOCKED_PREREQUISITE' }
    return 'FAIL'
}

function Get-NpAcceptanceOverallStatus {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][object[]]$Rows)

    $statuses = @($Rows | ForEach-Object { [string]$_.status })
    if ($statuses -contains 'HARNESS_ERROR') { return 'HARNESS_ERROR' }
    if ($statuses -contains 'FAIL') { return 'FAIL' }
    if ($statuses -contains 'BLOCKED_PREREQUISITE') { return 'BLOCKED_PREREQUISITE' }
    if ($statuses -contains 'PASS') { return 'PASS' }
    return 'NOT_SELECTED'
}


function Get-NpAcceptanceSourceFingerprint {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Root)

    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    $directories = [System.Collections.Generic.Stack[string]]::new()
    $directories.Push($rootFull)

    while ($directories.Count -gt 0) {
        $current = $directories.Pop()
        foreach ($item in Get-ChildItem -LiteralPath $current -Force) {
            $relative = [System.IO.Path]::GetRelativePath($rootFull, $item.FullName).Replace('\', '/')
            $segments = @($relative.Split('/'))
            $excluded = (
                $relative -eq '.git' -or
                $relative -eq '.env' -or
                $relative.StartsWith('.git/') -or
                $relative.StartsWith('.config/') -or
                $relative.StartsWith('.idea/') -or
                $relative.StartsWith('.np_evidence_python_win/') -or
                $relative.StartsWith('.nuget/') -or
                $relative.StartsWith('.pytest_cache/') -or
                $relative.StartsWith('artifacts/') -or
                $relative.StartsWith('BenchmarkDotNet.Artifacts/') -or
                $relative.StartsWith('coveragereport_backend/') -or
                $relative.StartsWith('coveragereport_core/') -or
                $relative.StartsWith('data/runtime/') -or
                $relative.StartsWith('docs/docfx/api/') -or
                $relative.StartsWith('docs/docfx/artifacts/') -or
                $relative.StartsWith('docs/docfx/output/') -or
                $relative.StartsWith('docs/doxygen/output/') -or
                $relative.StartsWith('docs/doxygen/output-local/') -or
                $relative.StartsWith('docs/evidence/') -or
                $relative.StartsWith('docs/RepositorioDocumental/') -or
                $relative.StartsWith('docs/structurizr/output/') -or
                $relative.StartsWith('docs/structurizr/.structurizr/') -or
                $relative.StartsWith('graphify-out/') -or
                $relative.StartsWith('StrykerOutput/') -or
                $relative.StartsWith('.testbin/') -or
                $relative.StartsWith('webUI/coverage/') -or
                $relative.StartsWith('webUI/node_modules/') -or
                $relative.StartsWith('webUI/dist/') -or
                $relative.StartsWith('webUI/test-results/') -or
                $relative.StartsWith('webUI/playwright-report/') -or
                $segments -contains 'bin' -or
                $segments -contains 'obj' -or
                $segments -contains 'TestResults' -or
                $segments -contains '__pycache__' -or
                $segments -contains '.ruff_cache'
            )

            if ($excluded) {
                continue
            }

            if ($item.PSIsContainer) {
                $directories.Push($item.FullName)
            }
            else {
                $files.Add($item) | Out-Null
            }
        }
    }

    $lines = $files |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($rootFull, $_.FullName).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        }

    $payload = (($lines -join "`n") + "`n")
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Write-NpAcceptanceHashManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $outputFull = [System.IO.Path]::GetFullPath($OutputPath)
    Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne $outputFull } |
        Sort-Object FullName |
        ForEach-Object {
            $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
            $relative = [System.IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
            "$($hash.Hash.ToLowerInvariant())  $relative"
        } | Set-Content -LiteralPath $OutputPath -Encoding utf8
}

Export-ModuleMember -Function @(
    'Write-NpAcceptanceJson',
    'Resolve-NpAcceptanceOutputRoot',
    'Resolve-NpAcceptanceCommandPath',
    'New-NpAcceptanceProcessInvocation',
    'ConvertTo-NpAcceptanceQuotedArgument',
    'Get-NpAcceptanceMissingCommands',
    'ConvertTo-NpAcceptanceCommandText',
    'Invoke-NpAcceptanceProcess',
    'Get-NpAcceptanceDeclaredResult',
    'Get-NpAcceptanceStageStatus',
    'Get-NpAcceptanceOverallStatus',
    'Get-NpAcceptanceSourceFingerprint',
    'Write-NpAcceptanceHashManifest'
)
