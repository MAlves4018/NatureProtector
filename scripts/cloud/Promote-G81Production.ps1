[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$StagingEvidenceDirectory,
    [Parameter(Mandatory)][string]$PlatformProjectId,
    [Parameter(Mandatory)][string]$ProductionProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$ClusterName,
    [Parameter(Mandatory)][string]$ReleaseName,
    [Parameter(Mandatory)][string]$RuntimeNetwork,
    [Parameter(Mandatory)][string]$RuntimeSubnetwork,
    [Parameter(Mandatory)][string]$CloudSqlPrivateIp,
    [Parameter(Mandatory)][string]$RabbitMqHost,
    [Parameter(Mandatory)][string]$RabbitMqTlsServerName,
    [Parameter(Mandatory)][string]$OtelEndpoint,
    [Parameter(Mandatory)][string]$SimulatorServiceAccount,
    [Parameter(Mandatory)][string]$MigrationServiceAccount,
    [Parameter(Mandatory)][string]$BootstrapServiceAccount,
    [Parameter(Mandatory)][string]$SmokeServiceAccount,
    [Parameter(Mandatory)][string]$FrontendOrigin,
    [Parameter(Mandatory)][string]$BootstrapAdminUsername,
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
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$Confirmation,
    [string]$FirstReleaseConfirmation = "",
    [ValidateSet("verified", "services-only-bootstrap")][string]$DeploymentMode = "verified",
    [string]$EdgeBootstrapConfirmation = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "EvidenceChecksums.ps1")
if ($Confirmation -ne "PROMOTE_VERIFIED_RELEASE_TO_PRODUCTION") { throw "Invalid production confirmation." }
if ($DeploymentMode -eq "services-only-bootstrap" -and $EdgeBootstrapConfirmation -ne "BOOTSTRAP_SERVICES_BEFORE_EDGE") {
    throw "Services-only bootstrap requires the explicit BOOTSTRAP_SERVICES_BEFORE_EDGE confirmation."
}
if ($Region -ne "europe-southwest1") { throw "Unexpected region '$Region'." }
if ($PlatformProjectId -match "(?i)cn2526" -or $ProductionProjectId -match "(?i)cn2526") { throw "CN projects are forbidden." }
if ($ReleaseName -notmatch '^[a-z][a-z0-9-]{0,62}$') { throw "Invalid release name." }


python scripts/cloud/Test-G81ReleaseManifest.py $ManifestPath
if ($LASTEXITCODE -ne 0) { throw "Invalid manifest." }
foreach ($required in @("release-manifest.json", "checksums.sha256", "staging-deployment-summary.json")) {
    if (-not (Test-Path (Join-Path $StagingEvidenceDirectory $required))) { throw "Missing staging evidence: $required" }
}
Test-G81EvidenceChecksums -Directory $StagingEvidenceDirectory
$stagingSummary = Get-Content -Raw (Join-Path $StagingEvidenceDirectory "staging-deployment-summary.json") | ConvertFrom-Json
if (-not $stagingSummary.staging_verified) { throw "Staging was not verified." }
if ($stagingSummary.release_name -ne $ReleaseName) { throw "Release name differs from staging evidence." }
$expected = (Get-FileHash -Algorithm SHA256 $ManifestPath).Hash
$observed = (Get-FileHash -Algorithm SHA256 (Join-Path $StagingEvidenceDirectory "release-manifest.json")).Hash
if ($expected -ne $observed) { throw "Staging and production manifests differ." }
$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json -AsHashtable
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

function Invoke-GcloudJson {
    param([Parameter(Mandatory)][string[]]$Arguments, [string]$OutputPath, [switch]$AllowFailure)
    $output = & gcloud @Arguments
    $exit = $LASTEXITCODE
    if ($exit -ne 0 -and -not $AllowFailure) { throw "gcloud failed: gcloud $($Arguments -join ' ')" }
    if ($OutputPath) { $output | Set-Content -Encoding utf8 $OutputPath }
    return [ordered]@{ exit_code = $exit; output = ($output -join "`n") }
}

function Test-ExistingProductionBaseline {
    param([Parameter(Mandatory)][string]$Pipeline, [Parameter(Mandatory)][string]$Target)
    $result = Invoke-GcloudJson -Arguments @(
        "deploy", "rollouts", "list", "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$Pipeline", "--filter=targetId=$Target AND state=SUCCEEDED", "--limit=1", "--format=json"
    ) -AllowFailure
    if ($result.exit_code -ne 0) { return $false }
    return (@($result.output | ConvertFrom-Json).Count -gt 0)
}

function Wait-ProductionRollout {
    param([Parameter(Mandatory)][string]$Pipeline, [Parameter(Mandatory)][string]$RolloutId, [int]$TimeoutMinutes = 120)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        $path = Join-Path $EvidenceDirectory "$Pipeline-production-rollout.json"
        $result = Invoke-GcloudJson -Arguments @(
            "deploy", "rollouts", "describe", $RolloutId,
            "--project=$PlatformProjectId", "--region=$Region",
            "--delivery-pipeline=$Pipeline", "--release=$ReleaseName", "--format=json"
        ) -OutputPath $path
        $rollout = $result.output | ConvertFrom-Json
        $state = [string]$rollout.state
        if ($state -eq "SUCCEEDED") { return }
        if ($state -in @("FAILED", "CANCELLED", "HALTED")) { throw "Production rollout $Pipeline failed with $state." }
        Start-Sleep -Seconds 20
    }
    throw "Timed out waiting for production rollout $Pipeline."
}

# Cluster-scoped controllers are a separately sealed dependency layer.
# Exact tagged release assets are resolved through the GitHub API and their
# published SHA-256 digests are verified before server-side apply.
& ./scripts/cloud/Install-G81ClusterDependencies.ps1 `
    -ProjectId $ProductionProjectId `
    -Region $Region `
    -ClusterName $ClusterName `
    -LockPath "infra/gcp/kubernetes/g8-1/operator-lock.json" `
    -EvidenceDirectory (Join-Path $EvidenceDirectory "cluster-dependencies")
if ($LASTEXITCODE -ne 0) { throw "G8.1 cluster dependency bootstrap failed." }

# Production schema expansion, idempotent bootstrap and the Simulator job must
# exist before any API traffic can be shifted to the new revision.
& ./scripts/cloud/Deploy-G81RuntimeJobs.ps1 `
    -EnvironmentName production `
    -ManifestPath $ManifestPath `
    -ProjectId $ProductionProjectId `
    -Region $Region `
    -RuntimeNetwork $RuntimeNetwork `
    -RuntimeSubnetwork $RuntimeSubnetwork `
    -CloudSqlPrivateIp $CloudSqlPrivateIp `
    -RabbitMqHost $RabbitMqHost `
    -RabbitMqTlsServerName $RabbitMqTlsServerName `
    -OtelEndpoint $OtelEndpoint `
    -SimulatorServiceAccount $SimulatorServiceAccount `
    -MigrationServiceAccount $MigrationServiceAccount `
    -BootstrapServiceAccount $BootstrapServiceAccount `
    -PostgresAppPasswordSecret $PostgresAppPasswordSecret `
    -PostgresAppPasswordVersion $PostgresAppPasswordVersion `
    -PostgresMigrationPasswordSecret $PostgresMigrationPasswordSecret `
    -PostgresMigrationPasswordVersion $PostgresMigrationPasswordVersion `
    -BootstrapAdminPasswordSecret $BootstrapAdminPasswordSecret `
    -BootstrapAdminPasswordVersion $BootstrapAdminPasswordVersion `
    -RabbitMqUsernameSecret $RabbitMqUsernameSecret `
    -RabbitMqUsernameVersion $RabbitMqUsernameVersion `
    -RabbitMqPasswordSecret $RabbitMqPasswordSecret `
    -RabbitMqPasswordVersion $RabbitMqPasswordVersion `
    -RabbitMqCaSecret $RabbitMqCaSecret `
    -RabbitMqCaVersion $RabbitMqCaVersion `
    -CloudSqlCaSecret $CloudSqlCaSecret `
    -CloudSqlCaVersion $CloudSqlCaVersion `
    -EvidenceDirectory (Join-Path $EvidenceDirectory "runtime-jobs")
if ($LASTEXITCODE -ne 0) { throw "Production runtime job preparation failed." }

$pipelines = @(
    [ordered]@{ name = "natureprotector-prevention"; target = "np-gke-production"; canary = $false },
    [ordered]@{ name = "natureprotector-api"; target = "np-run-production"; canary = $true },
    [ordered]@{ name = "natureprotector-frontend"; target = "np-run-production"; canary = $true }
)
$baselineByPipeline = @{}
foreach ($pipeline in $pipelines) {
    $baselineByPipeline[$pipeline.name] = Test-ExistingProductionBaseline -Pipeline $pipeline.name -Target $pipeline.target
}
$missingCanaryBaseline = @($pipelines | Where-Object { $_.canary -and -not $baselineByPipeline[$_.name] })
if ($missingCanaryBaseline.Count -gt 0 -and $FirstReleaseConfirmation -ne "I_ACCEPT_FIRST_RELEASE_HAS_NO_CANARY_BASELINE") {
    throw "At least one production target has no prior successful rollout, so Cloud Deploy cannot split traffic against a baseline. Supply the explicit first-release confirmation."
}

$productionSummary = @()
foreach ($pipeline in $pipelines) {
    $rolloutId = "prod-$ReleaseName"
    $existing = Invoke-GcloudJson -Arguments @(
        "deploy", "rollouts", "describe", $rolloutId,
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$($pipeline.name)", "--release=$ReleaseName", "--format=json"
    ) -AllowFailure

    if ($existing.exit_code -ne 0) {
        & gcloud deploy releases promote `
            --project=$PlatformProjectId --region=$Region `
            --delivery-pipeline=$pipeline.name --release=$ReleaseName `
            --to-target=$pipeline.target --rollout-id=$rolloutId --quiet
        if ($LASTEXITCODE -ne 0) { throw "Promotion creation failed for $($pipeline.name)." }
        $existingState = "PENDING_APPROVAL"
    } else {
        $existingRollout = $existing.output | ConvertFrom-Json
        $existingState = [string]$existingRollout.state
        if ([string]$existingRollout.targetId -ne [string]$pipeline.target) {
            throw "Existing production rollout $rolloutId is bound to an unexpected target."
        }
    }

    if ($existingState -ne "SUCCEEDED") {
        & gcloud deploy rollouts approve $rolloutId `
            --project=$PlatformProjectId --region=$Region `
            --delivery-pipeline=$pipeline.name --release=$ReleaseName --quiet
        if ($LASTEXITCODE -ne 0) { throw "Approval failed for $($pipeline.name)." }
        Wait-ProductionRollout -Pipeline $pipeline.name -RolloutId $rolloutId
    } else {
        $existing.output | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "$($pipeline.name)-production-rollout.json")
    }

    $productionSummary += [ordered]@{
        pipeline = $pipeline.name
        target = $pipeline.target
        rollout = $rolloutId
        state = "SUCCEEDED"
        reused_existing_rollout = ($existing.exit_code -eq 0)
        had_prior_production_baseline = [bool]$baselineByPipeline[$pipeline.name]
        rollout_strategy = $(if ($pipeline.canary) { "verified-canary" } else { "verified-rolling" })
    }
}

$functionalSmokePassed = $false
$productionVerified = $false
$edgeBootstrapPending = ($DeploymentMode -eq "services-only-bootstrap")
if ($DeploymentMode -eq "verified") {
    & ./scripts/cloud/Invoke-G81FunctionalSmoke.ps1 `
        -EnvironmentName production `
        -ManifestPath $ManifestPath `
        -ProjectId $ProductionProjectId `
        -Region $Region `
        -FrontendOrigin $FrontendOrigin `
        -SmokeServiceAccount $SmokeServiceAccount `
        -AdminUsername $BootstrapAdminUsername `
        -AdminPasswordSecret $BootstrapAdminPasswordSecret `
        -AdminPasswordVersion $BootstrapAdminPasswordVersion `
        -EvidenceDirectory (Join-Path $EvidenceDirectory "functional-smoke")
    if ($LASTEXITCODE -ne 0) { throw "Production functional smoke failed." }
    $functionalSmokePassed = $true
    $productionVerified = $true
}

Copy-Item $ManifestPath (Join-Path $EvidenceDirectory "release-manifest.json")
[ordered]@{
    schema_version = 1
    environment = "production"
    deployment_mode = $DeploymentMode
    source_commit = $manifest.source_commit
    release_name = $ReleaseName
    staging_manifest_sha256 = $expected.ToLowerInvariant()
    first_release_without_canary_baseline = ($missingCanaryBaseline.Count -gt 0)
    runtime_jobs_prepared = $true
    rollouts = $productionSummary
    functional_smoke_passed = $functionalSmokePassed
    edge_bootstrap_pending = $edgeBootstrapPending
    production_rollouts_succeeded = $true
    production_verified = $productionVerified
    production_authorized = $false
    production_deployed = $true
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "production-deployment-summary.json")

Write-G81EvidenceChecksums -EvidenceDirectory $EvidenceDirectory | Out-Null
