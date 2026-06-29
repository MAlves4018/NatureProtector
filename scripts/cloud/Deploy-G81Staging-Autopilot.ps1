[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$PlatformProjectId,
    [Parameter(Mandatory)][string]$StagingProjectId,
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
    [ValidateSet("verified", "services-only-bootstrap")][string]$DeploymentMode = "verified",
    [string]$EdgeBootstrapConfirmation = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Region -ne "europe-southwest1") { throw "Unexpected region '$Region'." }
if ($PlatformProjectId -match "(?i)cn2526" -or $StagingProjectId -match "(?i)cn2526") {
    throw "CN projects are forbidden."
}
if ($ReleaseName -notmatch '^[a-z][a-z0-9-]{0,62}$') { throw "Invalid Cloud Deploy release name." }
if ($DeploymentMode -eq "services-only-bootstrap" -and $EdgeBootstrapConfirmation -ne "BOOTSTRAP_SERVICES_BEFORE_EDGE") {
    throw "Services-only bootstrap requires the explicit BOOTSTRAP_SERVICES_BEFORE_EDGE confirmation."
}

python scripts/cloud/Test-G81ReleaseManifest.py $ManifestPath
if ($LASTEXITCODE -ne 0) { throw "Invalid G8.1 release manifest." }
$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json -AsHashtable
$images = $manifest.images
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

function Invoke-GcloudJson {
    param([Parameter(Mandatory)][string[]]$Arguments, [string]$OutputPath, [switch]$AllowFailure)
    $output = & gcloud @Arguments
    $exit = $LASTEXITCODE
    if ($exit -ne 0 -and -not $AllowFailure) { throw "gcloud failed: gcloud $($Arguments -join ' ')" }
    if ($OutputPath -and $exit -eq 0) { $output | Set-Content -Encoding utf8 $OutputPath }
    return [ordered]@{ exit_code = $exit; output = ($output -join "`n") }
}

function Wait-CloudDeployRollout {
    param(
        [Parameter(Mandatory)][string]$Pipeline,
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$OutputPath,
        [int]$TimeoutMinutes = 60
    )
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $rolloutName = $null
    while ((Get-Date) -lt $deadline) {
        $result = Invoke-GcloudJson -Arguments @(
            "deploy", "rollouts", "list",
            "--project=$PlatformProjectId", "--region=$Region",
            "--delivery-pipeline=$Pipeline", "--release=$ReleaseName",
            "--filter=targetId=$Target", "--sort-by=~createTime", "--limit=1", "--format=json"
        )
        $items = @($result.output | ConvertFrom-Json)
        if ($items.Count -gt 0) {
            $rolloutName = [string]$items[0].name
            $state = [string]$items[0].state
            $result.output | Set-Content -Encoding utf8 $OutputPath
            if ($state -eq "SUCCEEDED") { return $rolloutName }
            if ($state -in @("FAILED", "CANCELLED", "HALTED")) {
                throw "Staging rollout for $Pipeline reached terminal state $state."
            }
        }
        Start-Sleep -Seconds 15
    }
    throw "Timed out waiting for staging rollout for $Pipeline. Last rollout: $rolloutName"
}

# Cluster-scoped controllers were installed and proved by the outer
# Autopilot-aware runner. This avoids reapplying the upstream cert-manager
# manifest with its kube-system leader-election default and ensures all
# operator images are mirrored into the same-project Artifact Registry.
if ($env:NP_G81_OPERATORS_READY -ne "true") {
    throw "The Autopilot-aware operator foundation has not been proved."
}
if ([string]::IsNullOrWhiteSpace($env:NP_G81_OPERATOR_EVIDENCE) -or
    -not (Test-Path -LiteralPath $env:NP_G81_OPERATOR_EVIDENCE -PathType Container)) {
    throw "Operator foundation evidence is unavailable."
}
$operatorEvidenceTarget = Join-Path $EvidenceDirectory "cluster-dependencies"
if (Test-Path -LiteralPath $operatorEvidenceTarget) {
    Remove-Item -Recurse -Force -LiteralPath $operatorEvidenceTarget
}
Copy-Item -Recurse -Force -LiteralPath $env:NP_G81_OPERATOR_EVIDENCE -Destination $operatorEvidenceTarget

# Schema, bootstrap and Simulator are explicit immutable-image gates. They are
# deployed before services so the cloud API never points to a missing job.
& ./scripts/cloud/Deploy-G81RuntimeJobs.ps1 `
    -EnvironmentName staging `
    -ManifestPath $ManifestPath `
    -ProjectId $StagingProjectId `
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
if ($LASTEXITCODE -ne 0) { throw "Runtime job preparation failed." }

# Cloud Deploy target parameters inject environment-specific values after
# rendering. RabbitmqCluster.spec.image is a CRD field, so Skaffold image
# substitution does not rewrite it. Prepare a release-scoped source copy and
# replace only that CRD placeholder with the signed manifest digest.
$sourceRoot = Join-Path $EvidenceDirectory "cloud-deploy-source"
if (Test-Path -LiteralPath $sourceRoot) {
    Remove-Item -Recurse -Force -LiteralPath $sourceRoot
}
Copy-Item -Recurse -Force -LiteralPath "infra/gcp" -Destination $sourceRoot
$rabbitMqManifest = Join-Path $sourceRoot "kubernetes/g8-1/base/rabbitmq.yaml"
$rabbitMqManifestText = Get-Content -Raw -LiteralPath $rabbitMqManifest
if ($rabbitMqManifestText -notmatch "RABBITMQ_IMAGE_BY_DIGEST") {
    throw "RabbitMQ manifest does not contain the digest placeholder."
}
$rabbitMqManifestText.Replace("RABBITMQ_IMAGE_BY_DIGEST", [string]$images.rabbitmq.reference) |
    Set-Content -Encoding utf8 -LiteralPath $rabbitMqManifest
$releaseSpecs = @(
    [ordered]@{
        Pipeline = "natureprotector-api"
        Target = "np-run-staging"
        Skaffold = "cloud-deploy/g8-1/api/skaffold.yaml"
        Images = "API_IMAGE_BY_DIGEST=$($images.'backoffice-api'.reference),SMOKE_IMAGE_BY_DIGEST=$($images.'functional-smoke'.reference)"
    },
    [ordered]@{
        Pipeline = "natureprotector-frontend"
        Target = "np-run-staging"
        Skaffold = "cloud-deploy/g8-1/frontend/skaffold.yaml"
        Images = "FRONTEND_IMAGE_BY_DIGEST=$($images.frontend.reference),SMOKE_IMAGE_BY_DIGEST=$($images.'functional-smoke'.reference)"
    },
    [ordered]@{
        Pipeline = "natureprotector-prevention"
        Target = "np-gke-staging"
        Skaffold = "cloud-deploy/g8-1/prevention/skaffold.yaml"
        Images = "PREVENTION_IMAGE_BY_DIGEST=$($images.prevention.reference),RABBITMQ_IMAGE_BY_DIGEST=$($images.rabbitmq.reference),OTEL_IMAGE_BY_DIGEST=$($images.'otel-collector'.reference),CLOUDSDK_IMAGE_BY_DIGEST=$($images.'cloud-deploy-verifier'.reference)"
    }
)

$rolloutSummary = @()
foreach ($spec in $releaseSpecs) {
    $pipeline = [string]$spec.Pipeline
    $target = [string]$spec.Target
    $skaffold = [string]$spec.Skaffold
    $imageMappings = [string]$spec.Images

    $existing = Invoke-GcloudJson -Arguments @(
        "deploy", "releases", "describe", $ReleaseName,
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$pipeline", "--format=json"
    ) -AllowFailure

    if ($existing.exit_code -eq 0) {
        $release = $existing.output | ConvertFrom-Json
        if ([string]$release.annotations.sourceCommit -ne [string]$manifest.source_commit -or
            [string]$release.annotations.buildRunId -ne [string]$manifest.build_run_id) {
            throw "Existing Cloud Deploy release $ReleaseName for $pipeline is bound to different source evidence."
        }
    } else {
        & gcloud deploy releases create $ReleaseName `
            --project=$PlatformProjectId --region=$Region `
            --delivery-pipeline=$pipeline `
            --source=$sourceRoot --skaffold-file=$skaffold `
            --images=$imageMappings --enable-initial-rollout `
            --annotations="sourceCommit=$($manifest.source_commit),buildRunId=$($manifest.build_run_id),environment=staging" `
            --quiet
        if ($LASTEXITCODE -ne 0) { throw "Cloud Deploy release failed: $pipeline" }
    }

    Invoke-GcloudJson -Arguments @(
        "deploy", "releases", "describe", $ReleaseName,
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$pipeline", "--format=json"
    ) -OutputPath (Join-Path $EvidenceDirectory "$pipeline-release.json") | Out-Null

    $rolloutEvidence = Join-Path $EvidenceDirectory "$pipeline-staging-rollout.json"
    $rolloutName = Wait-CloudDeployRollout -Pipeline $pipeline -Target $target -OutputPath $rolloutEvidence
    $rolloutSummary += [ordered]@{ pipeline = $pipeline; target = $target; rollout = $rolloutName; state = "SUCCEEDED" }
}

$functionalSmokePassed = $false
$stagingVerified = $false
$edgeBootstrapPending = ($DeploymentMode -eq "services-only-bootstrap")
if ($DeploymentMode -eq "verified") {
    & ./scripts/cloud/Invoke-G81FunctionalSmoke.ps1 `
        -EnvironmentName staging `
        -ManifestPath $ManifestPath `
        -ProjectId $StagingProjectId `
        -Region $Region `
        -FrontendOrigin $FrontendOrigin `
        -SmokeServiceAccount $SmokeServiceAccount `
        -AdminUsername $BootstrapAdminUsername `
        -AdminPasswordSecret $BootstrapAdminPasswordSecret `
        -AdminPasswordVersion $BootstrapAdminPasswordVersion `
        -EvidenceDirectory (Join-Path $EvidenceDirectory "functional-smoke")
    if ($LASTEXITCODE -ne 0) { throw "Staging functional smoke failed." }
    $functionalSmokePassed = $true
    $stagingVerified = $true
}

Copy-Item $ManifestPath (Join-Path $EvidenceDirectory "release-manifest.json")
[ordered]@{
    schema_version = 1
    environment = "staging"
    deployment_mode = $DeploymentMode
    source_commit = $manifest.source_commit
    release_name = $ReleaseName
    runtime_jobs_prepared = $true
    rollouts = $rolloutSummary
    functional_smoke_passed = $functionalSmokePassed
    edge_bootstrap_pending = $edgeBootstrapPending
    staging_verified = $stagingVerified
    production_authorized = $false
    production_deployed = $false
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "staging-deployment-summary.json")

Get-FileHash -Algorithm SHA256 (Get-ChildItem -File -Recurse $EvidenceDirectory | Where-Object Name -ne "checksums.sha256") |
    Sort-Object Path |
    ForEach-Object { "$($_.Hash.ToLowerInvariant())  $($_.Path.Substring($EvidenceDirectory.Length).TrimStart('\\','/').Replace('\\','/'))" } |
    Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "checksums.sha256")
