[CmdletBinding()]
param(
    [switch]$Force
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Stop'

function New-InfluxApiToken {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $payload = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    return "apiv3_$payload"
}

function Set-DotEnvValue {
    param(
        [string[]]$Lines,
        [string]$Name,
        [string]$Value
    )

    $pattern = "^\s*#?\s*$([regex]::Escape($Name))="
    $updated = $false
    $result = foreach ($line in $Lines) {
        if (-not $updated -and $line -match $pattern) {
            $updated = $true
            "$Name=$Value"
        }
        else {
            $line
        }
    }

    if (-not $updated) {
        $result += "$Name=$Value"
    }

    return @($result)
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln', 'docker-compose.yml')
$examplePath = Join-Path $repoRoot '.env.example'
$dotEnvPath = Join-Path $repoRoot '.env'
$existingToken = $null

if (-not (Test-Path -LiteralPath $examplePath -PathType Leaf)) {
    throw ".env.example not found at $examplePath."
}

$dotEnvExists = Test-Path -LiteralPath $dotEnvPath -PathType Leaf
if ($dotEnvExists -and -not $Force) {
    throw ".env already exists at $dotEnvPath. Re-run with -Force to regenerate the local development file."
}

if ($dotEnvExists) {
    $existingValues = Read-NpDotEnv -Path $dotEnvPath -QuoteHandling Double
    if ($existingValues.ContainsKey('INFLUXDB_TOKEN')) {
        $existingToken = [string]$existingValues['INFLUXDB_TOKEN']
    }
}

$lines = @(Get-Content -LiteralPath $examplePath)
$token = New-InfluxApiToken

$lines = Set-DotEnvValue -Lines $lines -Name 'INFLUXDB_TOKEN' -Value $token
$lines = Set-DotEnvValue -Lines $lines -Name 'NP_BOOTSTRAP_ADMIN_USERNAME' -Value 'admin'
$lines = Set-DotEnvValue -Lines $lines -Name 'NP_BOOTSTRAP_ADMIN_PASSWORD' -Value 'admin123'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($dotEnvPath, (($lines -join [Environment]::NewLine) + [Environment]::NewLine), $utf8NoBom)

$influxTokenScript = Join-Path $repoRoot 'scripts\influx\Ensure-InfluxAdminTokenFile.ps1'
if (-not (Test-Path -LiteralPath $influxTokenScript -PathType Leaf)) {
    throw "Influx token helper not found at $influxTokenScript."
}

& $influxTokenScript | ForEach-Object {
    $text = [string]$_
    $text -replace '(?i)(token=)[^ \r\n]+', '$1<redacted>'
}

Write-Host "Local .env written: $dotEnvPath"
Write-Host "Local bootstrap admin: username=admin password=admin123"
Write-Host "InfluxDB token generated with required apiv3_ prefix; value was not printed."

if ($Force -and -not [string]::IsNullOrWhiteSpace($existingToken) -and $existingToken -ne $token) {
    Write-Warning "INFLUXDB_TOKEN was regenerated. Existing NatureProtector InfluxDB volumes initialized with the previous token may reject the new token with HTTP 401."
    Write-Warning "For local/dev, run '.\scripts\np.ps1 clean-local' to remove only this project's compose containers, networks and volumes. Do not use docker system prune."
}
