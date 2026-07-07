[CmdletBinding()]
param(
    [int]$ApiPort = 5254,
    [int]$PreventionPort = 5260,
    [int]$WebPort = 5173
)

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

$results = @(
    Test-HttpEndpoint -Name 'Backoffice API health' -Uri "http://127.0.0.1:$ApiPort/health"
    Test-HttpEndpoint -Name 'Prevention Host liveness' -Uri "http://127.0.0.1:$PreventionPort/health/live"
    Test-HttpEndpoint -Name 'webUI' -Uri "http://127.0.0.1:$WebPort"
)

$results | Format-Table -AutoSize | Out-String | Write-Host

$failures = @($results | Where-Object { $_.Required -and $_.Status -ne 'OK' })
if ($failures.Count -gt 0) {
    exit 1
}

exit 0
