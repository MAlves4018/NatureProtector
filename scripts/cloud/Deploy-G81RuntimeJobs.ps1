[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet("staging", "production")][string]$EnvironmentName,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$ProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$RuntimeNetwork,
    [Parameter(Mandatory)][string]$RuntimeSubnetwork,
    [Parameter(Mandatory)][string]$CloudSqlPrivateIp,
    [Parameter(Mandatory)][string]$RabbitMqHost,
    [Parameter(Mandatory)][string]$RabbitMqTlsServerName,
    [Parameter(Mandatory)][string]$OtelEndpoint,
    [Parameter(Mandatory)][string]$SimulatorServiceAccount,
    [Parameter(Mandatory)][string]$MigrationServiceAccount,
    [Parameter(Mandatory)][string]$BootstrapServiceAccount,
    [Parameter(Mandatory)][string]$PostgresAppPasswordSecret,
    [Parameter(Mandatory)][string]$PostgresAppPasswordVersion,
    [Parameter(Mandatory)][string]$PostgresMigrationPasswordSecret,
    [Parameter(Mandatory)][string]$PostgresMigrationPasswordVersion,
    [Parameter(Mandatory)][string]$BootstrapAdminPasswordSecret,
    [Parameter(Mandatory)][string]$BootstrapAdminPasswordVersion,
    [Parameter(Mandatory)][string]$RabbitMqUsernameSecret,
    [Parameter(Mandatory)][string]$RabbitMqUsernameVersion,
    [Parameter(Mandatory)][string]$RabbitMqPasswordSecret,
    [Parameter(Mandatory)][string]$RabbitMqPasswordVersion,
    [Parameter(Mandatory)][string]$RabbitMqCaSecret,
    [Parameter(Mandatory)][string]$RabbitMqCaVersion,
    [Parameter(Mandatory)][string]$CloudSqlCaSecret,
    [Parameter(Mandatory)][string]$CloudSqlCaVersion,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Region -ne "europe-southwest1") { throw "Unexpected region '$Region'." }
if ($ProjectId -match "(?i)cn2526") { throw "CN projects are forbidden." }

python scripts/cloud/Test-G81ReleaseManifest.py $ManifestPath
if ($LASTEXITCODE -ne 0) { throw "Invalid G8.1 release manifest." }
$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json -AsHashtable
$images = $manifest.images
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

function Invoke-Gcloud {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$OutputPath
    )

    $output = & gcloud @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gcloud failed: gcloud $($Arguments -join ' ')"
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $output | Set-Content -Encoding utf8 $OutputPath
    }
}

$labels = "environment=$EnvironmentName,phase=g8-1,managed-by=cloud-delivery"
$commonDb = "POSTGRES_REQUIRE_EXPLICIT=true,POSTGRES_HOST=$CloudSqlPrivateIp,POSTGRES_PORT=5432,POSTGRES_DB=natureprotector,POSTGRES_SSL_MODE=VerifyCA,POSTGRES_ROOT_CERTIFICATE=/var/run/secrets/cloudsql/server-ca.pem"
$sqlCaMount = "/var/run/secrets/cloudsql/server-ca.pem=${CloudSqlCaSecret}:${CloudSqlCaVersion}"
$rabbitCaMount = "/var/run/secrets/rabbitmq/ca.crt=${RabbitMqCaSecret}:${RabbitMqCaVersion}"

$migrationJob = "np-postgres-migrations"
Invoke-Gcloud -Arguments @(
    "run", "jobs", "deploy", $migrationJob,
    "--project=$ProjectId", "--region=$Region",
    "--image=$($images.'postgres-migrations'.reference)",
    "--service-account=$MigrationServiceAccount",
    "--network=$RuntimeNetwork", "--subnet=$RuntimeSubnetwork", "--vpc-egress=private-ranges-only",
    "--tasks=1", "--max-retries=0", "--task-timeout=15m",
    "--set-env-vars=$commonDb,POSTGRES_MIGRATION_USER=np_migration,POSTGRES_APP_USER=np_app",
    "--set-secrets=POSTGRES_MIGRATION_PASSWORD=${PostgresMigrationPasswordSecret}:${PostgresMigrationPasswordVersion},POSTGRES_APP_PASSWORD=${PostgresAppPasswordSecret}:${PostgresAppPasswordVersion},$sqlCaMount",
    "--labels=$labels", "--quiet"
)
Invoke-Gcloud -Arguments @(
    "run", "jobs", "execute", $migrationJob,
    "--project=$ProjectId", "--region=$Region", "--wait", "--format=json"
) -OutputPath (Join-Path $EvidenceDirectory "migration-execution.json")
Invoke-Gcloud -Arguments @(
    "run", "jobs", "describe", $migrationJob,
    "--project=$ProjectId", "--region=$Region", "--format=json"
) -OutputPath (Join-Path $EvidenceDirectory "migration-job.json")

$bootstrapJob = "np-postgres-bootstrap"
Invoke-Gcloud -Arguments @(
    "run", "jobs", "deploy", $bootstrapJob,
    "--project=$ProjectId", "--region=$Region",
    "--image=$($images.'postgres-bootstrap'.reference)",
    "--service-account=$BootstrapServiceAccount",
    "--network=$RuntimeNetwork", "--subnet=$RuntimeSubnetwork", "--vpc-egress=private-ranges-only",
    "--tasks=1", "--max-retries=0", "--task-timeout=15m",
    "--set-env-vars=$commonDb,POSTGRES_USER=np_app,NP_BOOTSTRAP_SKIP_SCHEMA_MIGRATION=true",
    "--set-secrets=POSTGRES_PASSWORD=${PostgresAppPasswordSecret}:${PostgresAppPasswordVersion},NP_BOOTSTRAP_ADMIN_PASSWORD=${BootstrapAdminPasswordSecret}:${BootstrapAdminPasswordVersion},$sqlCaMount",
    "--labels=$labels", "--quiet"
)
foreach ($attempt in 1..2) {
    Invoke-Gcloud -Arguments @(
        "run", "jobs", "execute", $bootstrapJob,
        "--project=$ProjectId", "--region=$Region", "--wait", "--format=json"
    ) -OutputPath (Join-Path $EvidenceDirectory "bootstrap-execution-$attempt.json")
}
Invoke-Gcloud -Arguments @(
    "run", "jobs", "describe", $bootstrapJob,
    "--project=$ProjectId", "--region=$Region", "--format=json"
) -OutputPath (Join-Path $EvidenceDirectory "bootstrap-job.json")

$simulatorJob = "natureprotector-simulator"
$simulatorEnvironment = "$commonDb,POSTGRES_USER=np_app,DOTNET_ENVIRONMENT=Production,Simulator__ControlPlaneEnabled=true,RabbitMq__HostName=$RabbitMqHost,RabbitMq__Port=5671,RabbitMq__TlsEnabled=true,RabbitMq__TlsServerName=$RabbitMqTlsServerName,RabbitMq__TlsCertificateAuthorityPath=/var/run/secrets/rabbitmq/ca.crt,OTEL_EXPORTER_OTLP_ENDPOINT=$OtelEndpoint,OTEL_EXPORTER_OTLP_PROTOCOL=grpc"
Invoke-Gcloud -Arguments @(
    "run", "jobs", "deploy", $simulatorJob,
    "--project=$ProjectId", "--region=$Region",
    "--image=$($images.simulator.reference)",
    "--service-account=$SimulatorServiceAccount",
    "--network=$RuntimeNetwork", "--subnet=$RuntimeSubnetwork", "--vpc-egress=private-ranges-only",
    "--tasks=1", "--parallelism=1", "--max-retries=0", "--task-timeout=60m",
    "--set-env-vars=$simulatorEnvironment",
    "--set-secrets=POSTGRES_PASSWORD=${PostgresAppPasswordSecret}:${PostgresAppPasswordVersion},RabbitMq__UserName=${RabbitMqUsernameSecret}:${RabbitMqUsernameVersion},RabbitMq__Password=${RabbitMqPasswordSecret}:${RabbitMqPasswordVersion},$sqlCaMount,$rabbitCaMount",
    "--labels=$labels", "--quiet"
)
Invoke-Gcloud -Arguments @(
    "run", "jobs", "describe", $simulatorJob,
    "--project=$ProjectId", "--region=$Region", "--format=json"
) -OutputPath (Join-Path $EvidenceDirectory "simulator-job.json")

$summary = [ordered]@{
    schema_version = 1
    environment = $EnvironmentName
    project_id = $ProjectId
    region = $Region
    source_commit = $manifest.source_commit
    images = [ordered]@{
        migrations = $images.'postgres-migrations'.reference
        bootstrap = $images.'postgres-bootstrap'.reference
        simulator = $images.simulator.reference
    }
    migration_executed = $true
    bootstrap_executions = 2
    simulator_job_deployed = $true
    production_authorized = $false
    production_deployed = $false
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "runtime-jobs-summary.json")
