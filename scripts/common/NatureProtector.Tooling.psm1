Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-NpRepositoryRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$StartPath,
        [Parameter(Mandatory = $true)][string[]]$RequiredPaths
    )

    $current = Get-Item -LiteralPath $StartPath
    if (-not $current.PSIsContainer) {
        $current = $current.Directory
    }

    while ($null -ne $current) {
        $allPresent = $true
        foreach ($requiredPath in $RequiredPaths) {
            if (-not (Test-Path -LiteralPath (Join-Path $current.FullName $requiredPath))) {
                $allPresent = $false
                break
            }
        }

        if ($allPresent) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not locate repository root from $StartPath. Required paths: $($RequiredPaths -join ', ')."
}

function Read-NpDotEnv {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [ValidateSet('Both', 'BothTrim', 'Double', 'None')][string]$QuoteHandling = 'Both',
        [switch]$Required,
        [string]$MissingFileMessage = ''
    )

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        if ($Required) {
            if (-not [string]::IsNullOrWhiteSpace($MissingFileMessage)) {
                throw $MissingFileMessage
            }
            throw ".env not found at $Path."
        }
        return $values
    }

    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#') -or -not $line.Contains('=')) {
            continue
        }

        $parts = $line.Split('=', 2)
        $name = $parts[0].Trim()
        $value = $parts[1].Trim()
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        if ($QuoteHandling -eq 'BothTrim') {
            $value = $value.Trim('"').Trim("'")
        }
        elseif ($QuoteHandling -eq 'Both') {
            if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }
        elseif ($QuoteHandling -eq 'Double') {
            $value = $value.Trim('"')
        }

        $values[$name] = $value
    }

    return $values
}

function Get-NpConfigValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][hashtable]$Values,
        [Parameter(Mandatory = $true)][string]$Name,
        [Alias('DefaultValue')][AllowEmptyString()][string]$Fallback = '',
        [switch]$EnvironmentFirst
    )

    if ($EnvironmentFirst) {
        $fromEnvironment = [Environment]::GetEnvironmentVariable($Name)
        if (-not [string]::IsNullOrWhiteSpace($fromEnvironment)) {
            return $fromEnvironment
        }
    }

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }

    return $Fallback
}

function Get-NpRelativePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][Alias('Root')][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString(), [System.StringComparison]::Ordinal)) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($baseFullPath)
    $pathUri = [System.Uri]::new([System.IO.Path]::GetFullPath($Path))
    $relativeUri = $baseUri.MakeRelativeUri($pathUri)
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-NpPathUnderRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][Alias('BasePath')][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($fullRoot.Length).TrimStart('\', '/')
    }

    return $fullPath
}

function Invoke-NpExternalCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][Alias('Name', 'FileName')][string]$Command,
        [string[]]$Arguments = @(),
        [hashtable]$Environment = @{},
        [switch]$ThrowOnStartFailure
    )

    try {
        $resolvedCommand = Get-Command $Command -ErrorAction Stop
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $resolvedCommand.Source
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        foreach ($entry in $Environment.GetEnumerator()) {
            $startInfo.Environment[$entry.Key] = [string]$entry.Value
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
            $startInfo.Arguments = ($quotedArguments -join ' ')
        }

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        [void]$process.Start()
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        $text = (($standardOutput + $standardError) | Out-String).Trim()
        $exitCode = $process.ExitCode
        if ($text -match 'error during connect|Acesso negado|Access is denied|permission denied|Cannot connect') {
            $exitCode = 1
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = $text
        }
    }
    catch {
        if ($ThrowOnStartFailure) {
            throw
        }
        return [pscustomobject]@{
            ExitCode = 1
            Output = $_.Exception.Message
        }
    }
}

function Test-NpTcpEndpoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutMilliseconds = 2000
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)) {
            return $false
        }
        $client.EndConnect($async)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

function Resolve-NpValidationPython {
    [CmdletBinding()]
    param(
        [AllowEmptyString()][string]$RequestedPython,
        [string]$EnvironmentVariable = 'NATUREPROTECTOR_VALIDATION_PYTHON',
        [string[]]$RequiredModules = @('jsonschema', 'yaml', 'hcl2')
    )

    if ([string]::IsNullOrWhiteSpace($RequestedPython)) {
        $RequestedPython = [Environment]::GetEnvironmentVariable($EnvironmentVariable)
    }
    if ([string]::IsNullOrWhiteSpace($RequestedPython)) {
        throw "Pass -PythonExecutable or set $EnvironmentVariable."
    }
    if (-not (Test-Path -LiteralPath $RequestedPython -PathType Leaf)) {
        throw "PythonExecutable does not exist: $RequestedPython"
    }

    $resolved = (Resolve-Path -LiteralPath $RequestedPython).Path
    if ($resolved -match '\\msys64\\') {
        throw "MSYS2 Python is not accepted for validation: $resolved"
    }

    $importStatement = 'import ' + ($RequiredModules -join ', ')
    & $resolved -c $importStatement 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Validation Python imports failed for: $resolved"
    }
    return $resolved
}

function Write-NpJsonFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$Depth = 10,
        [switch]$DisableWhatIf,
        [switch]$NullWhenEmpty
    )

    $previous = $WhatIfPreference
    try {
        if ($DisableWhatIf) {
            $WhatIfPreference = $false
        }
        $json = ConvertTo-Json -InputObject $Value -Depth $Depth
        if ($NullWhenEmpty -and [string]::IsNullOrWhiteSpace($json)) {
            $json = 'null'
        }
        $json | Set-Content -LiteralPath $Path -Encoding UTF8
    }
    finally {
        if ($DisableWhatIf) {
            $WhatIfPreference = $previous
        }
    }
}

function Get-NpAbsolutePath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-NpPathExists {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description,
        [Parameter(Mandatory = $true)][bool]$ExpectDirectory
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
    $item = Get-Item -LiteralPath $Path
    if ($ExpectDirectory -and -not $item.PSIsContainer) {
        throw "$Description must be a directory: $Path"
    }
    if (-not $ExpectDirectory -and $item.PSIsContainer) {
        throw "$Description must be a file: $Path"
    }
}

function Get-NpFreeTcpPort {
    [CmdletBinding()]
    param()
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Get-NpCommandLineVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [string[]]$Arguments = @()
    )

    try {
        $output = & $Command @Arguments 2>$null | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($output)) {
            return 'Not available'
        }
        return "$output".Trim()
    }
    catch {
        return 'Not available'
    }
}

function Get-NpPercentileNearestRank {
    [CmdletBinding()]
    param(
        [double[]]$Values,
        [Parameter(Mandatory = $true)][double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return $null
    }
    $sorted = @($Values | Sort-Object)
    $rank = [Math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    $rank = [Math]::Max(0, [Math]::Min($rank, $sorted.Count - 1))
    return [Math]::Round([double]$sorted[$rank], 2)
}

Export-ModuleMember -Function @(
    'Find-NpRepositoryRoot',
    'Read-NpDotEnv',
    'Get-NpConfigValue',
    'Get-NpRelativePath',
    'Get-NpPathUnderRoot',
    'Invoke-NpExternalCommand',
    'Test-NpTcpEndpoint',
    'Resolve-NpValidationPython',
    'Write-NpJsonFile',
    'Get-NpAbsolutePath',
    'Assert-NpPathExists',
    'Get-NpFreeTcpPort',
    'Get-NpCommandLineVersion',
    'Get-NpPercentileNearestRank'
)
