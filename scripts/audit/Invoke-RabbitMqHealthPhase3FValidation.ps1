[CmdletBinding()]
param(
    [switch]$IncludeLocalMigrationExercise,
    [switch]$IncludeCloudStaticValidation,
    [switch]$KeepInfrastructure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$migrationScript = Join-Path $repositoryRoot 'scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1'
$startedByThisRun = $false

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-PythonValidator {
    param([Parameter(Mandatory = $true)][string]$Path)
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) { $python = Get-Command python3 -ErrorAction Stop }
    Write-Host "> $($python.Source) $Path"
    & $python.Source $Path
    if ($LASTEXITCODE -ne 0) { throw "Python validator failed: $Path" }
}

function ConvertTo-ApiSegment {
    param([Parameter(Mandatory = $true)][string]$Value)
    return [uri]::EscapeDataString($Value)
}

function Invoke-LocalManagementApi {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body,
        [switch]$AllowNotFound
    )

    $uri = "http://127.0.0.1:15672$Path"
    $token = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('np:np_dev_pass'))
    $headers = @{ Authorization = "Basic $token"; Accept = 'application/json' }
    $parameters = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        TimeoutSec = 15
        ErrorAction = 'Stop'
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ($Body | ConvertTo-Json -Depth 20 -Compress)
    }
    try {
        return Invoke-RestMethod @parameters
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($AllowNotFound -and $statusCode -eq 404) { return $null }
        throw
    }
}

Push-Location $repositoryRoot
try {
    Invoke-PythonValidator '.\scripts\audit\Test-RabbitMqHealthPhase3FPackage.py'
    Write-Host 'PHASE3F_PACKAGE_STATIC_CHECK=PASS'

    if ($IncludeCloudStaticValidation) {
        Invoke-PythonValidator '.\scripts\cloud\Test-EnvironmentRemediationStatic.py'
        Write-Host 'PHASE3F_ENVIRONMENT_STATIC_VALIDATION=PASS'
    }

    if ($IncludeLocalMigrationExercise) {
        Assert-Command docker
        Assert-Command pwsh

        $existing = @(& docker ps -a --format '{{.Names}}' | Where-Object {
            $_ -in @('np-postgres-it', 'np-rabbitmq-it', 'np-influxdb-it')
        })
        if ($existing.Count -eq 0) {
            & '.\scripts\ci\Start-DockerIntegrationServices.ps1'
            if ($LASTEXITCODE -ne 0) { throw 'Docker integration infrastructure failed to start.' }
            $startedByThisRun = $true
        }
        else {
            & '.\scripts\ci\Start-DockerIntegrationServices.ps1'
            if ($LASTEXITCODE -ne 0) { throw 'Existing Docker integration infrastructure is not ready.' }
        }

        $vhost = "np-phase3f-$([guid]::NewGuid().ToString('N'))"
        $exchange = 'np.events'
        $primary = 'np.ingestion.readings'
        $raw = 'np.observability.raw'
        $routingKey = 'simulation.reading.produced'
        $encodedVhost = ConvertTo-ApiSegment $vhost
        $encodedExchange = ConvertTo-ApiSegment $exchange
        $encodedPrimary = ConvertTo-ApiSegment $primary
        $encodedRaw = ConvertTo-ApiSegment $raw

        try {
            Invoke-LocalManagementApi -Method PUT -Path "/api/vhosts/$encodedVhost" -Body @{} | Out-Null
            Invoke-LocalManagementApi -Method PUT -Path "/api/exchanges/$encodedVhost/$encodedExchange" -Body @{
                type = 'topic'; durable = $true; auto_delete = $false; internal = $false; arguments = @{}
            } | Out-Null
            foreach ($queue in @($primary, $raw)) {
                $encodedQueue = ConvertTo-ApiSegment $queue
                Invoke-LocalManagementApi -Method PUT -Path "/api/queues/$encodedVhost/$encodedQueue" -Body @{
                    durable = $true; auto_delete = $false; arguments = @{}
                } | Out-Null
                Invoke-LocalManagementApi -Method POST -Path "/api/bindings/$encodedVhost/e/$encodedExchange/q/$encodedQueue" -Body @{
                    routing_key = $routingKey; arguments = @{}
                } | Out-Null
            }
            Invoke-LocalManagementApi -Method PUT -Path "/api/policies/$encodedVhost/natureprotector-primary-work-queue" -Body @{
                pattern = '^np\.ingestion\.readings$'
                'apply-to' = 'queues'
                priority = 20
                definition = @{
                    overflow = 'reject-publish'
                    'max-length-bytes' = 10485760
                    'delivery-limit' = 8
                }
            } | Out-Null
            Invoke-LocalManagementApi -Method PUT -Path "/api/policies/$encodedVhost/natureprotector-quorum" -Body @{
                pattern = '^np\.'
                'apply-to' = 'queues'
                priority = 10
                definition = @{
                    overflow = 'reject-publish'
                    'max-length-bytes' = 10485760
                    'delivery-limit' = 8
                }
            } | Out-Null

            $env:RABBITMQ_MANAGEMENT_USERNAME = 'np'
            $env:RABBITMQ_MANAGEMENT_PASSWORD = 'np_dev_pass'
            $baseArguments = @{
                ManagementBaseUri = 'http://127.0.0.1:15672'
                AllowInsecureHttp = $true
                VirtualHost = $vhost
                EvidenceDirectory = 'artifacts/operational-audit/rabbitmq-health-phase3f/local-exercise'
            }

            & $migrationScript -Action Inventory @baseArguments
            & $migrationScript -Action Protect @baseArguments `
                -MessageTtlMilliseconds 60000 `
                -MaxLengthBytes 1048576 `
                -Apply `
                -Confirmation "PROTECT_RAW:${vhost}:${raw}"
            & $migrationScript -Action RetireLegacyPolicy @baseArguments `
                -Apply `
                -Confirmation "RETIRE_LEGACY_POLICY:${vhost}:natureprotector-quorum"
            & $migrationScript -Action Unbind @baseArguments `
                -Apply `
                -Confirmation "UNBIND_RAW:${vhost}:${exchange}:${raw}:${routingKey}"
            $verifyOutput = & $migrationScript -Action Verify @baseArguments *>&1 | Out-String
            if ($verifyOutput -notmatch 'PHASE3F_RAW_DISABLED_AND_UNBOUND') {
                throw "Phase 3F verify marker missing: $verifyOutput"
            }
            & $migrationScript -Action Rollback @baseArguments `
                -Apply `
                -Confirmation "ROLLBACK_RAW:${vhost}:${exchange}:${raw}:${routingKey}"
            & $migrationScript -Action Unbind @baseArguments `
                -Apply `
                -Confirmation "UNBIND_RAW:${vhost}:${exchange}:${raw}:${routingKey}"
            $finalVerify = & $migrationScript -Action Verify @baseArguments *>&1 | Out-String
            if ($finalVerify -notmatch 'PHASE3F_RAW_DISABLED_AND_UNBOUND') {
                throw "Final Phase 3F verify marker missing: $finalVerify"
            }

            Write-Host 'PHASE3F_LOCAL_MIGRATION_EXERCISE_PROVED'
        }
        finally {
            Remove-Item Env:RABBITMQ_MANAGEMENT_USERNAME -ErrorAction SilentlyContinue
            Remove-Item Env:RABBITMQ_MANAGEMENT_PASSWORD -ErrorAction SilentlyContinue
            Invoke-LocalManagementApi -Method DELETE -Path "/api/vhosts/$encodedVhost" -AllowNotFound | Out-Null
        }
    }

    Write-Host 'PHASE3F_VALIDATION=PASS'
}
finally {
    Pop-Location
    if ($startedByThisRun -and -not $KeepInfrastructure) {
        & docker compose `
            --project-name np-standard-cd-it `
            --file (Join-Path $repositoryRoot '.github\docker\standard-cd-integration.compose.yml') `
            down -v --remove-orphans
    }
}
