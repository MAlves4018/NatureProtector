Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'


function Get-NPPoint5ObjectProperty {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)][AllowNull()]$InputObject,
        [Parameter(Mandatory=$true)][string]$Name
    )

    if ($null -eq $InputObject) { return $null }

    if ($InputObject -is [System.Collections.IDictionary]) {
        if ($InputObject.Contains($Name)) {
            return $InputObject[$Name]
        }

        foreach ($key in $InputObject.Keys) {
            if ([string]::Equals([string]$key, $Name, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $InputObject[$key]
            }
        }

        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -ne $property) {
        return $property.Value
    }

    foreach ($candidate in @($InputObject.PSObject.Properties)) {
        if ([string]::Equals([string]$candidate.Name, $Name, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $candidate.Value
        }
    }

    return $null
}

function Get-NPPoint5NestedValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)][AllowNull()]$InputObject,
        [Parameter(Mandatory=$true)][string[]]$Path
    )

    $current = $InputObject
    foreach ($segment in $Path) {
        if ($null -eq $current) { return $null }
        $current = Get-NPPoint5ObjectProperty -InputObject $current -Name $segment
    }

    return $current
}

function ConvertFrom-NPPoint5MetadataJson {
    [CmdletBinding()]
    param([Parameter(Mandatory=$false)][AllowNull()]$Run)

    $metadataJson = Get-NPPoint5ObjectProperty -InputObject $Run -Name 'metadataJson'
    if ([string]::IsNullOrWhiteSpace([string]$metadataJson)) {
        return $null
    }

    try {
        return ([string]$metadataJson) | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function ConvertTo-NPPoint5StringList {
    [CmdletBinding()]
    param([Parameter(Mandatory=$false)][AllowNull()]$Value)

    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($item in @($Value)) {
        $text = [string]$item
        if (-not [string]::IsNullOrWhiteSpace($text) -and -not $result.Contains($text)) {
            $result.Add($text)
        }
    }

    return @($result)
}

function Get-NPPoint5ResolvedProfiles {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][AllowNull()]$Run)

    $profiles = @(
        ConvertTo-NPPoint5StringList -Value (
            Get-NPPoint5NestedValue -InputObject $Run -Path @('runOverrides','resolved','degradationProfiles')
        )
    )
    if (@($profiles).Count -gt 0) { return @($profiles) }

    $single = [string](
        Get-NPPoint5NestedValue -InputObject $Run -Path @('runOverrides','resolved','degradationProfile')
    )
    if (-not [string]::IsNullOrWhiteSpace($single)) { return @($single) }

    $metadata = ConvertFrom-NPPoint5MetadataJson -Run $Run
    foreach ($path in @(
        @('run_overrides','resolved','degradation_profiles'),
        @('runOverrides','resolved','degradationProfiles')
    )) {
        $profiles = @(
            ConvertTo-NPPoint5StringList -Value (
                Get-NPPoint5NestedValue -InputObject $metadata -Path $path
            )
        )
        if (@($profiles).Count -gt 0) { return @($profiles) }
    }

    foreach ($path in @(
        @('run_overrides','resolved','degradation_profile'),
        @('runOverrides','resolved','degradationProfile')
    )) {
        $single = [string](Get-NPPoint5NestedValue -InputObject $metadata -Path $path)
        if (-not [string]::IsNullOrWhiteSpace($single)) { return @($single) }
    }

    return @('none')
}


function Get-NPPoint5PropertySum {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)][AllowNull()]$InputObject,
        [Parameter(Mandatory=$true)][string]$Property
    )

    [decimal]$sum = 0
    foreach ($item in @($InputObject)) {
        if ($null -eq $item) { continue }

        $value = Get-NPPoint5ObjectProperty -InputObject $item -Name $Property
        if ($null -eq $value) { continue }

        [decimal]$parsed = 0
        if ([decimal]::TryParse(
            [string]$value,
            [System.Globalization.NumberStyles]::Any,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$parsed
        )) {
            $sum += $parsed
        }
    }

    if ($sum -eq [decimal][int]$sum) {
        return [int]$sum
    }

    return $sum
}

function Get-NPPoint5ResolvedSensorCount {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][AllowNull()]$Run)

    $candidates = @(
        (Get-NPPoint5NestedValue -InputObject $Run -Path @('runOverrides','resolved','sensorCount'))
    )

    $metadata = ConvertFrom-NPPoint5MetadataJson -Run $Run
    $candidates += @(
        (Get-NPPoint5NestedValue -InputObject $metadata -Path @('run_overrides','resolved','sensor_count')),
        (Get-NPPoint5NestedValue -InputObject $metadata -Path @('runOverrides','resolved','sensorCount')),
        (Get-NPPoint5ObjectProperty -InputObject $metadata -Name 'sensor_count'),
        (Get-NPPoint5ObjectProperty -InputObject $metadata -Name 'sensorCount')
    )

    foreach ($candidate in $candidates) {
        if ($null -eq $candidate) { continue }

        $parsed = 0
        if ([int]::TryParse(
            [string]$candidate,
            [System.Globalization.NumberStyles]::Integer,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$parsed
        ) -and $parsed -ge 0) {
            return $parsed
        }
    }

    return $null
}

function Write-NPPoint5Json {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][AllowNull()]$Value,
        [Parameter(Mandatory=$true)][string]$Path,
        [int]$Depth = 50
    )
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $json = ConvertTo-Json -InputObject $Value -Depth $Depth
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

function Get-NPPoint5Sha256 {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$Path)
    return ([string](Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash).ToLowerInvariant()
}

function ConvertTo-NPPoint5CsvValue {
    param([AllowNull()]$Value)
    if ($null -eq $Value) { return '' }
    $text = [string]$Value
    if ($text.IndexOfAny([char[]]@(',', '"', "`r", "`n")) -ge 0) {
        return '"' + $text.Replace('"', '""') + '"'
    }
    return $text
}

function Write-NPPoint5Csv {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][AllowEmptyCollection()][object[]]$Rows,
        [Parameter(Mandatory=$true)][string[]]$Columns,
        [Parameter(Mandatory=$true)][string]$Path
    )
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add(($Columns -join ','))
    foreach ($row in @($Rows)) {
        $values = foreach ($column in $Columns) {
            $property = $row.PSObject.Properties[$column]
            ConvertTo-NPPoint5CsvValue $(if ($null -eq $property) { $null } else { $property.Value })
        }
        $lines.Add(($values -join ','))
    }
    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
}

function Get-NPPoint5Tool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Command,
        [string[]]$Arguments = @('--version')
    )
    $resolved = Get-Command $Command -ErrorAction SilentlyContinue
    if ($null -eq $resolved) {
        return [ordered]@{
            command = $Command
            available = $false
            exitCode = $null
            output = $null
        }
    }

    try {
        $result = @(& $Command @Arguments 2>&1)
        $exit = $LASTEXITCODE
        if ($null -eq $exit) { $exit = 0 }
        return [ordered]@{
            command = $Command
            available = $true
            exitCode = [int]$exit
            output = (($result | ForEach-Object { [string]$_ }) -join [Environment]::NewLine).Trim()
        }
    } catch {
        return [ordered]@{
            command = $Command
            available = $true
            exitCode = 1
            output = $_.Exception.Message
        }
    }
}

function Get-NPPoint5FreshToken {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$ApiBaseUrl)

    if (
        -not [string]::IsNullOrWhiteSpace($env:NATUREPROTECTOR_RUNTIME_USERNAME) -and
        -not [string]::IsNullOrWhiteSpace($env:NATUREPROTECTOR_RUNTIME_PASSWORD)
    ) {
        $login = Invoke-RestMethod `
            -Method POST `
            -Uri ($ApiBaseUrl.TrimEnd('/') + '/api/users-roles/login') `
            -ContentType 'application/json' `
            -Body (@{
                usernameOrEmail = $env:NATUREPROTECTOR_RUNTIME_USERNAME
                password = $env:NATUREPROTECTOR_RUNTIME_PASSWORD
            } | ConvertTo-Json) `
            -TimeoutSec 30
        $token = [string]$login.token
        if (-not [string]::IsNullOrWhiteSpace($token)) { return $token }
    }

    if (-not [string]::IsNullOrWhiteSpace($env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN)) {
        return [string]$env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN
    }

    throw 'No runtime credentials or bearer token are available.'
}

function Invoke-NPPoint5ApiGet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$ApiBaseUrl,
        [Parameter(Mandatory=$true)][string]$Token,
        [Parameter(Mandatory=$true)][string]$RelativePath,
        [Parameter(Mandatory=$true)][string]$OutputPath,
        [switch]$Optional
    )
    $uri = $ApiBaseUrl.TrimEnd('/') + '/' + $RelativePath.TrimStart('/')
    try {
        $value = Invoke-RestMethod -Method GET -Uri $uri -Headers @{ Authorization = "Bearer $Token" } -TimeoutSec 45
        Write-NPPoint5Json -Value $value -Path $OutputPath
        return [ordered]@{
            uri = $uri
            status = 'PASS'
            statusCode = 200
            file = $OutputPath
            error = $null
        }
    } catch {
        $statusCode = $null
        if ($null -ne $_.Exception.Response) {
            try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { $statusCode = $null }
        }
        return [ordered]@{
            uri = $uri
            status = $(if ($Optional) { 'OPTIONAL_FAIL' } else { 'FAIL' })
            statusCode = $statusCode
            file = $null
            error = $_.Exception.Message
        }
    }
}

function Invoke-NPPoint5Command {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory=$true)][string]$WorkingDirectory,
        [Parameter(Mandatory=$true)][string]$LogRoot,
        [hashtable]$Environment = @{}
    )

    New-Item -ItemType Directory -Path $LogRoot -Force | Out-Null
    $safeName = $Name -replace '[^A-Za-z0-9._-]', '_'
    $stdout = Join-Path $LogRoot "$safeName.stdout.log"
    $stderr = Join-Path $LogRoot "$safeName.stderr.log"
    $started = [DateTimeOffset]::UtcNow

    $oldValues = @{}
    foreach ($key in $Environment.Keys) {
        $oldValues[$key] = [Environment]::GetEnvironmentVariable([string]$key, 'Process')
        [Environment]::SetEnvironmentVariable([string]$key, [string]$Environment[$key], 'Process')
    }

    try {
        try {
            $process = Start-Process `
                -FilePath $FilePath `
                -ArgumentList $Arguments `
                -WorkingDirectory $WorkingDirectory `
                -NoNewWindow `
                -Wait `
                -PassThru `
                -RedirectStandardOutput $stdout `
                -RedirectStandardError $stderr
            $exitCode = [int]$process.ExitCode
            $errorMessage = $null
        } catch {
            $exitCode = 1
            $errorMessage = $_.Exception.Message
            Set-Content -LiteralPath $stderr -Value $_.Exception.ToString() -Encoding UTF8
            if (-not (Test-Path -LiteralPath $stdout)) {
                Set-Content -LiteralPath $stdout -Value '' -Encoding UTF8
            }
        }
    } finally {
        foreach ($key in $Environment.Keys) {
            [Environment]::SetEnvironmentVariable([string]$key, $oldValues[$key], 'Process')
        }
    }

    $finished = [DateTimeOffset]::UtcNow
    return [ordered]@{
        name = $Name
        command = ($FilePath + ' ' + ($Arguments -join ' ')).Trim()
        workingDirectory = $WorkingDirectory
        startedAtUtc = $started.ToString('o')
        finishedAtUtc = $finished.ToString('o')
        durationMs = [Math]::Round(($finished - $started).TotalMilliseconds, 3)
        exitCode = $exitCode
        status = $(if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' })
        stdout = $stdout
        stderr = $stderr
        error = $errorMessage
    }
}

function Write-NPPoint5Hashes {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$Root)
    $manifestPath = Join-Path $Root 'SHA256SUMS.txt'
    $files = @(
        Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName
    )
    $lines = foreach ($file in $files) {
        $relative = [System.IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        "$(Get-NPPoint5Sha256 -Path $file.FullName)  $relative"
    }
    Set-Content -LiteralPath $manifestPath -Value $lines -Encoding UTF8
}

function Test-NPPoint5Hashes {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$Root)
    $manifestPath = Join-Path $Root 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $manifestPath)) { return $false }
    foreach ($line in @(Get-Content -LiteralPath $manifestPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { return $false }
        $expected = $Matches[1].ToLowerInvariant()
        $relative = $Matches[2]
        $path = Join-Path $Root ($relative.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
        if ((Get-NPPoint5Sha256 -Path $path) -ne $expected) { return $false }
    }
    return $true
}
