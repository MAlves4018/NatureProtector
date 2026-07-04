[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$checks = [System.Collections.Generic.List[object]]::new()
function Add-ToolingCheck {
    param([string]$Name, [bool]$Passed, [string]$Detail)
    $checks.Add([pscustomobject]@{ name = $Name; passed = $Passed; detail = $Detail })
    if (-not $Passed) { throw "$Name failed: $Detail" }
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'scripts')
Add-ToolingCheck 'repository-root' (Test-Path -LiteralPath (Join-Path $repoRoot 'NatureProtector.sln')) $repoRoot

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("np-tooling-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    $envPath = Join-Path $tempRoot '.env'
    @('PLAIN=value', 'DOUBLE="quoted"', "SINGLE='quoted'", 'EQUALS=a=b') | Set-Content -LiteralPath $envPath -Encoding UTF8
    $values = Read-NpDotEnv -Path $envPath -QuoteHandling Both
    Add-ToolingCheck 'dotenv-plain' ($values.PLAIN -eq 'value') $values.PLAIN
    Add-ToolingCheck 'dotenv-double' ($values.DOUBLE -eq 'quoted') $values.DOUBLE
    Add-ToolingCheck 'dotenv-single' ($values.SINGLE -eq 'quoted') $values.SINGLE
    Add-ToolingCheck 'dotenv-equals' ($values.EQUALS -eq 'a=b') $values.EQUALS

    $oldValue = [Environment]::GetEnvironmentVariable('NP_TOOLING_TEST_VALUE')
    try {
        [Environment]::SetEnvironmentVariable('NP_TOOLING_TEST_VALUE', 'environment')
        $config = @{ NP_TOOLING_TEST_VALUE = 'file' }
        Add-ToolingCheck 'config-file-precedence' ((Get-NpConfigValue -Values $config -Name 'NP_TOOLING_TEST_VALUE' -Fallback 'fallback') -eq 'file') 'file'
        Add-ToolingCheck 'config-environment-precedence' ((Get-NpConfigValue -Values $config -Name 'NP_TOOLING_TEST_VALUE' -Fallback 'fallback' -EnvironmentFirst) -eq 'environment') 'environment'
    }
    finally {
        [Environment]::SetEnvironmentVariable('NP_TOOLING_TEST_VALUE', $oldValue)
    }

    $child = Join-Path $tempRoot 'child/file.txt'
    New-Item -ItemType Directory -Path (Split-Path -Parent $child) | Out-Null
    'test' | Set-Content -LiteralPath $child -Encoding UTF8
    $relative = Get-NpRelativePath -BasePath $tempRoot -Path $child
    Add-ToolingCheck 'relative-path' (($relative -replace '\\', '/') -eq 'child/file.txt') $relative
    $underRoot = Get-NpPathUnderRoot -Root $tempRoot -Path $child
    Add-ToolingCheck 'path-under-root' (($underRoot -replace '\\', '/') -eq 'child/file.txt') $underRoot

    $jsonPath = Join-Path $tempRoot 'value.json'
    Write-NpJsonFile -Value ([ordered]@{ value = 42 }) -Path $jsonPath -Depth 4
    $json = Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json
    Add-ToolingCheck 'json-write' ($json.value -eq 42) $jsonPath

    $absolute = Get-NpAbsolutePath -Path $child
    Assert-NpPathExists -Path $absolute -Description 'temporary test file' -ExpectDirectory $false
    Add-ToolingCheck 'absolute-path' ([System.IO.Path]::IsPathRooted($absolute)) $absolute

    $port = Get-NpFreeTcpPort
    Add-ToolingCheck 'free-port' ($port -gt 0 -and $port -le 65535) "$port"
    Add-ToolingCheck 'nearest-rank' ((Get-NpPercentileNearestRank -Values @(1, 2, 3, 4) -Percentile 95) -eq 4) '4'

    $pwshPath = (Get-Process -Id $PID).Path
    $external = Invoke-NpExternalCommand -Command $pwshPath -Arguments @('-NoProfile', '-Command', "Write-Output 'np-tooling-ok'")
    Add-ToolingCheck 'external-command' ($external.ExitCode -eq 0 -and $external.Output -match 'np-tooling-ok') $external.Output
    Add-ToolingCheck 'command-version' ((Get-NpCommandLineVersion -Command $pwshPath -Arguments @('-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()')) -ne 'Not available') $pwshPath

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        $listenPort = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
        Add-ToolingCheck 'tcp-endpoint' (Test-NpTcpEndpoint -HostName '127.0.0.1' -Port $listenPort -TimeoutMilliseconds 2000) "$listenPort"
    }
    finally {
        $listener.Stop()
    }
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

[pscustomobject]@{
    schema_version = 1
    status = 'PASS'
    checks = $checks
} | ConvertTo-Json -Depth 6
