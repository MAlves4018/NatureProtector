[CmdletBinding()]
param(
    [string]$ProjectId = "natureprotector-500518",
    [string]$Region = "europe-southwest1",
    [string]$ClusterName = "np-staging",
    [string]$CloudSqlInstance = "np-staging-postgres",
    [string]$RuntimeNetwork = "np-staging",
    [string]$RuntimeSubnetwork = "np-staging-europe-southwest1",
    [string]$WorkerPool = "np-staging-deploy",
    [string]$HttpsAddress = "np-staging-https",
    [string]$ManagedCertificate = "np-staging",
    [string[]]$SecretNames = @(
        "np-staging-bootstrap-admin-password",
        "np-staging-cloud-sql-server-ca",
        "np-staging-jwt-signing-key",
        "np-staging-postgres-app-password",
        "np-staging-postgres-migration-password",
        "np-staging-rabbitmq-app-password",
        "np-staging-rabbitmq-app-username",
        "np-staging-rabbitmq-ca-certificate",
        "np-staging-rabbitmq-tls-certificate",
        "np-staging-rabbitmq-tls-private-key"
    ),
    [string[]]$ServiceAccounts = @(
        "np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com",
        "np-deploy-staging@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-api@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-bootstrap@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-frontend@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-migrations@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-otel@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-prevention@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-secret-sync@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-simulator@natureprotector-500518.iam.gserviceaccount.com",
        "np-staging-smoke@natureprotector-500518.iam.gserviceaccount.com"
    ),
    [switch]$RequireActiveCertificate,
    [switch]$SkipKubeCredentials,
    [string]$GcloudCommand = "gcloud"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($ProjectId -ne "natureprotector-500518") { throw "Only the canonical staging project is supported." }
if ($Region -ne "europe-southwest1") { throw "Unexpected staging region '$Region'." }
if ($ClusterName -ne "np-staging") { throw "Unexpected staging cluster '$ClusterName'." }
if ($CloudSqlInstance -ne "np-staging-postgres") { throw "Unexpected staging Cloud SQL instance '$CloudSqlInstance'." }

$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([Parameter(Mandatory)][string]$Value)
    $failures.Add($Value) | Out-Null
    Write-Host $Value
}

function Invoke-GcloudReadiness {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$ResourceMarker,
        [switch]$Json
    )

    $output = & $GcloudCommand @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | ForEach-Object { [string]$_ }) -join "`n"
    if ($exitCode -ne 0) {
        if ($text -match "(?i)permission|denied|forbidden") {
            Add-Failure "PERMISSION_DENIED=$ResourceMarker"
        }
        else {
            Add-Failure "MISSING_RESOURCE=$ResourceMarker"
        }
        return $null
    }

    if (-not $Json) { return $text }
    try {
        return $text | ConvertFrom-Json
    }
    catch {
        Add-Failure "INVALID_RESOURCE_JSON=$ResourceMarker"
        return $null
    }
}

$cluster = Invoke-GcloudReadiness -Json -ResourceMarker "GKE_CLUSTER:$ClusterName" -Arguments @(
    "container", "clusters", "describe", $ClusterName,
    "--project=$ProjectId", "--region=$Region", "--format=json"
)
if ($cluster) {
    if ([string]$cluster.status -ne "RUNNING") { Add-Failure "NOT_READY=GKE_CLUSTER:${ClusterName}:$($cluster.status)" }
    if (-not [bool]$cluster.autopilot.enabled) { Add-Failure "NOT_READY=GKE_CLUSTER:${ClusterName}:AUTOPILOT_DISABLED" }
}

Invoke-GcloudReadiness -Json -ResourceMarker "VPC:$RuntimeNetwork" -Arguments @(
    "compute", "networks", "describe", $RuntimeNetwork,
    "--project=$ProjectId", "--format=json"
) | Out-Null

Invoke-GcloudReadiness -Json -ResourceMarker "SUBNETWORK:$RuntimeSubnetwork" -Arguments @(
    "compute", "networks", "subnets", "describe", $RuntimeSubnetwork,
    "--project=$ProjectId", "--region=$Region", "--format=json"
) | Out-Null

$cloudSql = Invoke-GcloudReadiness -Json -ResourceMarker "CLOUD_SQL:$CloudSqlInstance" -Arguments @(
    "sql", "instances", "describe", $CloudSqlInstance,
    "--project=$ProjectId", "--format=json"
)
if ($cloudSql -and [string]$cloudSql.state -ne "RUNNABLE") {
    Add-Failure "NOT_READY=CLOUD_SQL:${CloudSqlInstance}:$($cloudSql.state)"
}

Invoke-GcloudReadiness -Json -ResourceMarker "CLOUD_BUILD_WORKER_POOL:$WorkerPool" -Arguments @(
    "builds", "worker-pools", "describe", $WorkerPool,
    "--project=$ProjectId", "--region=$Region", "--format=json"
) | Out-Null

Invoke-GcloudReadiness -Json -ResourceMarker "EDGE_IP:$HttpsAddress" -Arguments @(
    "compute", "addresses", "describe", $HttpsAddress,
    "--project=$ProjectId", "--global", "--format=json"
) | Out-Null

$certificate = Invoke-GcloudReadiness -Json -ResourceMarker "MANAGED_CERTIFICATE:$ManagedCertificate" -Arguments @(
    "compute", "ssl-certificates", "describe", $ManagedCertificate,
    "--project=$ProjectId", "--global", "--format=json"
)
if ($RequireActiveCertificate -and $certificate -and [string]$certificate.managed.status -ne "ACTIVE") {
    Add-Failure "NOT_READY=MANAGED_CERTIFICATE:${ManagedCertificate}:$($certificate.managed.status)"
}

foreach ($secretName in $SecretNames) {
    Invoke-GcloudReadiness -Json -ResourceMarker "SECRET:$secretName" -Arguments @(
        "secrets", "describe", $secretName,
        "--project=$ProjectId", "--format=json"
    ) | Out-Null
}

foreach ($serviceAccount in $ServiceAccounts) {
    Invoke-GcloudReadiness -Json -ResourceMarker "SERVICE_ACCOUNT:$serviceAccount" -Arguments @(
        "iam", "service-accounts", "describe", $serviceAccount,
        "--project=$ProjectId", "--format=json"
    ) | Out-Null
}

if (-not $SkipKubeCredentials) {
    Invoke-GcloudReadiness -ResourceMarker "GKE_CREDENTIALS:$ClusterName" -Arguments @(
        "container", "clusters", "get-credentials", $ClusterName,
        "--project=$ProjectId", "--region=$Region", "--dns-endpoint", "--quiet"
    ) | Out-Null
}

if ($failures.Count -gt 0) {
    Write-Host "STAGING_FOUNDATION_READINESS=FAIL"
    Write-Host "STAGING_FOUNDATION_FAILURES=$($failures.Count)"
    exit 1
}

Write-Host "STAGING_FOUNDATION_READINESS=PASS"
exit 0
