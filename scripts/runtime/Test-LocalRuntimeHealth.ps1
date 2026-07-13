[CmdletBinding()]
param(
    [int]$ApiPort = 0,
    [int]$PreventionPort = 0,
    [int]$WebPort = 0
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

function Test-HttpEndpoint {
    param(
        [string]$Name,
        [string]$Uri,
        [bool]$Required = $true
    )

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -TimeoutSec 5 -ErrorAction Stop
        return [pscustomobject]@{
            Name = $Name
            Uri = $Uri
            Status = 'OK'
            StatusCode = [int]$response.StatusCode
            Required = $Required
            Detail = "HTTP $([int]$response.StatusCode)"
        }
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        return [pscustomobject]@{
            Name = $Name
            Uri = $Uri
            Status = 'FAIL'
            StatusCode = $statusCode
            Required = $Required
            Detail = $_.Exception.Message
        }
    }
}

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @('NatureProtector.sln')
$dotEnv = Read-NpDotEnv -Path (Join-Path $repoRoot '.env')

if ($ApiPort -le 0) {
    $ApiPort = [int](Get-NpConfigValue -Values $dotEnv -Name 'BACKOFFICE_API_PORT' -Fallback '5254' -EnvironmentFirst)
}
if ($PreventionPort -le 0) {
    $PreventionPort = [int](Get-NpConfigValue -Values $dotEnv -Name 'PREVENTION_HOST_PORT' -Fallback '5260' -EnvironmentFirst)
}
if ($WebPort -le 0) {
    $WebPort = [int](Get-NpConfigValue -Values $dotEnv -Name 'WEBUI_PORT' -Fallback '5173' -EnvironmentFirst)
}

$results = @(
    Test-HttpEndpoint -Name 'Backoffice API liveness' -Uri "http://127.0.0.1:$ApiPort/health/live"
    Test-HttpEndpoint -Name 'Backoffice API readiness' -Uri "http://127.0.0.1:$ApiPort/health/ready"
    Test-HttpEndpoint -Name 'Prevention Host liveness' -Uri "http://127.0.0.1:$PreventionPort/health/live"
    Test-HttpEndpoint -Name 'Prevention Host readiness' -Uri "http://127.0.0.1:$PreventionPort/health/ready"
    Test-HttpEndpoint -Name 'webUI' -Uri "http://127.0.0.1:$WebPort"
)

$results | Format-Table -AutoSize | Out-String | Write-Host

$failures = @($results | Where-Object { $_.Required -and $_.Status -ne 'OK' })
if ($failures.Count -gt 0) {
    exit 1
}

exit 0
