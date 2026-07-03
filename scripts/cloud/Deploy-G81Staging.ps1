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
    [Parameter(Mandatory)][string]$RabbitMqTlsCertificateVersion,
    [Parameter(Mandatory)][string]$RabbitMqTlsPrivateKeyVersion,
    [Parameter(Mandatory)][string]$CloudSqlCaSecret,
    [Parameter(Mandatory)][string]$CloudSqlCaVersion,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [ValidateSet("verified", "services-only-bootstrap")][string]$DeploymentMode = "verified",
    [string]$EdgeBootstrapConfirmation = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "EvidenceChecksums.ps1")

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

& ./scripts/cloud/Test-G81StagingFoundationReadiness.ps1 `
    -ProjectId $StagingProjectId `
    -Region $Region `
    -ClusterName $ClusterName `
    -RuntimeNetwork $RuntimeNetwork `
    -RuntimeSubnetwork $RuntimeSubnetwork `
    -RequireActiveCertificate:($DeploymentMode -eq "verified") `
    -CloudSqlCaSecret $CloudSqlCaSecret `
    -CloudSqlCaVersion $CloudSqlCaVersion `
    -RabbitMqCaSecret $RabbitMqCaSecret `
    -RabbitMqCaVersion $RabbitMqCaVersion `
    -RabbitMqTlsCertificateVersion $RabbitMqTlsCertificateVersion `
    -RabbitMqTlsPrivateKeyVersion $RabbitMqTlsPrivateKeyVersion
if ($LASTEXITCODE -ne 0) { throw "Staging foundation readiness failed before deployment." }

function Invoke-GcloudJson {
    param([Parameter(Mandatory)][string[]]$Arguments, [string]$OutputPath, [switch]$AllowFailure)
    $output = & gcloud @Arguments
    $exit = $LASTEXITCODE
    if ($exit -ne 0 -and -not $AllowFailure) { throw "gcloud failed: gcloud $($Arguments -join ' ')" }
    if ($OutputPath -and $exit -eq 0) { $output | Set-Content -Encoding utf8 $OutputPath }
    return [ordered]@{ exit_code = $exit; output = ($output -join "`n") }
}

function Invoke-DiagnosticCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$OutputPath
    )

    $output = & $FilePath @Arguments 2>&1
    $exit = $LASTEXITCODE
    $output | Set-Content -Encoding utf8 -LiteralPath $OutputPath
    return $exit
}

function Write-CloudDeployRolloutFailureDiagnostics {
    param(
        [Parameter(Mandatory)][string]$Pipeline,
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$RolloutName,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

    Invoke-DiagnosticCommand -FilePath "gcloud" -Arguments @(
        "deploy", "rollouts", "describe", $RolloutName,
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$Pipeline", "--release=$ReleaseName", "--format=json"
    ) -OutputPath (Join-Path $OutputDirectory "rollout.json") | Out-Null

    $jobRunsPath = Join-Path $OutputDirectory "job-runs.json"
    Invoke-DiagnosticCommand -FilePath "gcloud" -Arguments @(
        "deploy", "job-runs", "list",
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$Pipeline", "--release=$ReleaseName", "--rollout=$RolloutName", "--format=json"
    ) -OutputPath $jobRunsPath | Out-Null

    try {
        $jobRuns = @(Get-Content -Raw -LiteralPath $jobRunsPath | ConvertFrom-Json)
        foreach ($jobRun in $jobRuns) {
            $jobRunName = [string]$jobRun.name
            if (-not $jobRunName) { continue }
            $jobRunId = ($jobRunName -split '/')[-1]
            Invoke-DiagnosticCommand -FilePath "gcloud" -Arguments @(
                "deploy", "job-runs", "describe", $jobRunId,
                "--project=$PlatformProjectId", "--region=$Region",
                "--delivery-pipeline=$Pipeline", "--release=$ReleaseName", "--rollout=$RolloutName", "--format=json"
            ) -OutputPath (Join-Path $OutputDirectory "job-run-$jobRunId.json") | Out-Null
        }
    }
    catch {
        $_.Exception.Message | Set-Content -Encoding utf8 -LiteralPath (Join-Path $OutputDirectory "job-run-diagnostics-error.txt")
    }

    if ($Target -eq "np-gke-staging") {
        Invoke-DiagnosticCommand -FilePath "kubectl" -Arguments @(
            "-n", "natureprotector-staging", "get",
            "deploy,pods,svc,scaledobject,triggerauthentication,users.rabbitmq.com,permissions.rabbitmq.com,policies.rabbitmq.com",
            "-o", "wide"
        ) -OutputPath (Join-Path $OutputDirectory "k8s-workloads.txt") | Out-Null
        Invoke-DiagnosticCommand -FilePath "kubectl" -Arguments @(
            "-n", "natureprotector-staging", "get", "events", "--sort-by=.lastTimestamp"
        ) -OutputPath (Join-Path $OutputDirectory "k8s-events.txt") | Out-Null
        Invoke-DiagnosticCommand -FilePath "kubectl" -Arguments @(
            "-n", "natureprotector-staging", "describe", "deployment/natureprotector-prevention"
        ) -OutputPath (Join-Path $OutputDirectory "prevention-describe.txt") | Out-Null
        Invoke-DiagnosticCommand -FilePath "kubectl" -Arguments @(
            "-n", "natureprotector-staging", "logs", "-l", "app=natureprotector-prevention",
            "--all-containers=true", "--tail=200"
        ) -OutputPath (Join-Path $OutputDirectory "prevention-logs.txt") | Out-Null
    }

    Write-Host "CLOUD_DEPLOY_ROLLOUT_DIAGNOSTICS=$OutputDirectory"
}

function Test-CloudRunServiceReady {
    param(
        [Parameter(Mandatory)][string]$ServiceName,
        [Parameter(Mandatory)][string]$OutputPath
    )

    $result = Invoke-GcloudJson -Arguments @(
        "run", "services", "describe", $ServiceName,
        "--project=$StagingProjectId", "--region=$Region", "--format=json"
    ) -OutputPath $OutputPath
    $service = $result.output | ConvertFrom-Json
    $conditions = @($service.status.conditions)
    $ready = $conditions | Where-Object { [string]$_.type -eq "Ready" } | Select-Object -First 1
    if (-not $ready -or [string]$ready.status -ne "True") {
        throw "Cloud Run service $ServiceName is not Ready."
    }
}

function Test-FrontendOriginMatchesManagedCertificate {
    param(
        [Parameter(Mandatory)][Uri]$Origin,
        [Parameter(Mandatory)][string]$OutputPath
    )

    $certificateName = "np-staging"
    $result = Invoke-GcloudJson -Arguments @(
        "compute", "ssl-certificates", "describe", $certificateName,
        "--project=$StagingProjectId", "--global", "--format=json"
    ) -OutputPath $OutputPath
    $certificate = $result.output | ConvertFrom-Json
    if ([string]$certificate.managed.status -ne "ACTIVE") {
        throw "Managed certificate $certificateName is not ACTIVE."
    }

    $domains = @($certificate.managed.domains | ForEach-Object { [string]$_ })
    if ($domains -notcontains $Origin.Host) {
        throw "FrontendOrigin host '$($Origin.Host)' is not covered by managed certificate $certificateName."
    }
}

function Resolve-CurlExecutable {
    $curlCommands = @(
        Get-Command `
            -Name "curl" `
            -CommandType Application `
            -ErrorAction SilentlyContinue
    )

    if ($curlCommands.Count -eq 0) {
        throw "curl executable was not found in PATH."
    }

    $selectedCurl =
        $curlCommands |
        Where-Object {
            $_.Source -and
            (Test-Path -LiteralPath $_.Source)
        } |
        Select-Object -First 1

    if ($null -eq $selectedCurl) {
        throw "curl was found, but no executable curl path could be resolved."
    }

    return [string]$selectedCurl.Source
}

function Test-HttpPrecheck {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

    $bodyPath = Join-Path $OutputDirectory "$Name.body"
    $statusPath = Join-Path $OutputDirectory "$Name.status"
    $curlExecutable = Resolve-CurlExecutable

    $status = & $curlExecutable `
        --silent `
        --show-error `
        --location `
        --connect-timeout "15" `
        --max-time "60" `
        --output $bodyPath `
        --write-out "%{http_code}" `
        $Url

    $exit = $LASTEXITCODE

    [string]$status |
        Set-Content `
            -Encoding ascii `
            -LiteralPath $statusPath

    if ($exit -ne 0 -or [string]$status -notmatch '^2\d\d$') {
        throw "Pre-smoke HTTP check $Name failed with curl exit $exit and status $status."
    }
}

function Test-G81PreSmokeReadiness {
    param(
        [Parameter(Mandatory)][string]$OutputDirectory,
        [Parameter(Mandatory)][object[]]$Rollouts
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $rolloutStates = @{}
    foreach ($rollout in $Rollouts) {
        $rolloutStates[[string]$rollout.pipeline] = [string]$rollout.state
    }
    if ($rolloutStates["natureprotector-api"] -ne "SUCCEEDED") { throw "API rollout is not SUCCEEDED." }
    if ($rolloutStates["natureprotector-frontend"] -ne "SUCCEEDED") { throw "Frontend rollout is not SUCCEEDED." }
    if ($rolloutStates["natureprotector-prevention"] -ne "SUCCEEDED") { throw "Prevention rollout is not SUCCEEDED." }
    Write-Host "API_ROLLOUT=PASS"
    Write-Host "FRONTEND_ROLLOUT=PASS"
    Write-Host "PREVENTION_ROLLOUT=PASS"

    Test-CloudRunServiceReady -ServiceName "natureprotector-api" -OutputPath (Join-Path $OutputDirectory "api-service.json")
    Test-CloudRunServiceReady -ServiceName "natureprotector-frontend" -OutputPath (Join-Path $OutputDirectory "frontend-service.json")

    try { $frontendUri = [Uri]$FrontendOrigin } catch { throw "FrontendOrigin must be an absolute HTTPS origin." }
    Test-FrontendOriginMatchesManagedCertificate -Origin $frontendUri -OutputPath (Join-Path $OutputDirectory "managed-certificate.json")

    Test-HttpPrecheck -Url "$($FrontendOrigin.TrimEnd('/'))/healthz" -Name "frontend-healthz" -OutputDirectory $OutputDirectory
    Write-Host "FRONTEND_HEALTH_PRECHECK=PASS"
    Test-HttpPrecheck -Url "$($FrontendOrigin.TrimEnd('/'))/" -Name "frontend-index" -OutputDirectory $OutputDirectory
    Write-Host "FRONTEND_INDEX_PRECHECK=PASS"

    $kubectlExit = Invoke-DiagnosticCommand -FilePath "kubectl" -Arguments @(
        "-n", "natureprotector-staging", "wait",
        "--for=condition=Available", "deployment/natureprotector-prevention",
        "--timeout=120s"
    ) -OutputPath (Join-Path $OutputDirectory "prevention-available.txt")
    if ($kubectlExit -ne 0) { throw "Prevention deployment is not Available before functional smoke." }

    [ordered]@{
        schema_version = 1
        frontend_origin = $FrontendOrigin
        api_rollout = "SUCCEEDED"
        frontend_rollout = "SUCCEEDED"
        prevention_rollout = "SUCCEEDED"
        frontend_health_precheck = "PASS"
        frontend_index_precheck = "PASS"
        prevention_available = "PASS"
    } | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "pre-smoke-readiness-summary.json")
    Write-Host "PRE_SMOKE_READINESS=PASS"
}

function Wait-CloudDeployRollout {
    param(
        [Parameter(Mandatory)][string]$Pipeline,
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$FailureDiagnosticsDirectory,
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
                Write-CloudDeployRolloutFailureDiagnostics `
                    -Pipeline $Pipeline `
                    -Target $Target `
                    -RolloutName $rolloutName `
                    -OutputDirectory $FailureDiagnosticsDirectory
                throw "Staging rollout for $Pipeline reached terminal state $state. Diagnostics: $FailureDiagnosticsDirectory"
            }
        }
        Start-Sleep -Seconds 15
    }
    throw "Timed out waiting for staging rollout for $Pipeline. Last rollout: $rolloutName"
}

function Get-CloudDeployRolloutId {
    param(
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$SourceCommit
    )

    if ($SourceCommit -notmatch '^[0-9a-f]{40}$') { throw "Invalid source commit for rollout id." }
    $targetId = $Target.ToLowerInvariant() -replace '[^a-z0-9-]', '-'
    $targetId = $targetId.Trim('-')
    if (-not $targetId) { throw "Invalid target for rollout id." }

    $rolloutId = "r-$($SourceCommit.Substring(0, 12))-$targetId"
    if ($rolloutId -notmatch '^[a-z][a-z0-9-]{0,62}$') { throw "Invalid generated Cloud Deploy rollout id." }
    return $rolloutId
}

function Test-OperatorFoundationReady {
    param([Parameter(Mandatory)][string]$OutputDirectory)

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

    $rollouts = @(
        @{ Namespace = "cert-manager"; Resource = "deployment/cert-manager" },
        @{ Namespace = "cert-manager"; Resource = "deployment/cert-manager-cainjector" },
        @{ Namespace = "cert-manager"; Resource = "deployment/cert-manager-webhook" },
        @{ Namespace = "rabbitmq-system"; Resource = "deployment/rabbitmq-cluster-operator" },
        @{ Namespace = "rabbitmq-system"; Resource = "deployment/messaging-topology-operator" },
        @{ Namespace = "keda"; Resource = "deployment/keda-operator" },
        @{ Namespace = "keda"; Resource = "deployment/keda-metrics-apiserver" },
        @{ Namespace = "keda"; Resource = "deployment/keda-admission" }
    )

    foreach ($rollout in $rollouts) {
        $namespace = [string]$rollout.Namespace
        $resource = [string]$rollout.Resource
        & kubectl -n $namespace rollout status $resource --timeout=10s
        if ($LASTEXITCODE -ne 0) { return $false }
    }

    & kubectl get deploy -n cert-manager -o json |
        Set-Content -Encoding utf8 (Join-Path $OutputDirectory "cert-manager-deployments.json")
    & kubectl get deploy -n rabbitmq-system -o json |
        Set-Content -Encoding utf8 (Join-Path $OutputDirectory "rabbitmq-operator-deployments.json")
    & kubectl get deploy -n keda -o json |
        Set-Content -Encoding utf8 (Join-Path $OutputDirectory "keda-deployments.json")

    [ordered]@{
        schema_version = 1
        status = "already-ready"
        manager_policy = "preserve-existing-operator-field-managers"
        rollouts = $rollouts
    } | ConvertTo-Json -Depth 5 |
        Set-Content -Encoding utf8 (Join-Path $OutputDirectory "operator-foundation.json")

    return $true
}

# Cluster-scoped controllers are a separately sealed dependency layer.
# Exact tagged release assets are resolved through the GitHub API and their
# published SHA-256 digests are verified before server-side apply.
$operatorEvidence = Join-Path $EvidenceDirectory "cluster-dependencies"
& gcloud container clusters get-credentials $ClusterName `
    --project=$StagingProjectId --region=$Region --dns-endpoint --quiet
if ($LASTEXITCODE -ne 0) { throw "Unable to acquire DNS-endpoint GKE credentials." }
if (Test-OperatorFoundationReady -OutputDirectory $operatorEvidence) {
    Write-Host "OPERATOR_FOUNDATION_ALREADY_READY"
}
else {
    & ./scripts/cloud/Install-G81ClusterDependencies.ps1 `
        -ProjectId $StagingProjectId `
        -Region $Region `
        -ClusterName $ClusterName `
        -LockPath "infra/gcp/kubernetes/g8-1/operator-lock.json" `
        -EvidenceDirectory $operatorEvidence
    if ($LASTEXITCODE -ne 0) { throw "G8.1 cluster dependency bootstrap failed." }
}

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

$stagingKustomization = Join-Path $sourceRoot "kubernetes/g8-1/overlays/staging/kustomization.yaml"
$stagingKustomizationText = Get-Content -Raw -LiteralPath $stagingKustomization
$secretVersionReplacements = [ordered]@{
    "RABBITMQ_TLS_CERTIFICATE_VERSION" = $RabbitMqTlsCertificateVersion
    "RABBITMQ_TLS_PRIVATE_KEY_VERSION" = $RabbitMqTlsPrivateKeyVersion
    "RABBITMQ_CA_VERSION" = $RabbitMqCaVersion
    "CLOUD_SQL_CA_VERSION" = $CloudSqlCaVersion
}
foreach ($entry in $secretVersionReplacements.GetEnumerator()) {
    if ($stagingKustomizationText -notmatch [regex]::Escape([string]$entry.Key)) {
        throw "Staging kustomization does not contain expected secret version placeholder '$($entry.Key)'."
    }
    $stagingKustomizationText = $stagingKustomizationText.Replace([string]$entry.Key, [string]$entry.Value)
}
if ($stagingKustomizationText -match "/versions/latest") {
    throw "Staging kustomization still contains a latest secret version reference."
}
$stagingKustomizationText | Set-Content -Encoding utf8 -LiteralPath $stagingKustomization

& ./scripts/cloud/Ensure-G81PreventionVerifierSupport.ps1 `
    -ProjectId $StagingProjectId `
    -Region $Region `
    -ClusterName $ClusterName `
    -Environment staging `
    -Namespace natureprotector-staging `
    -EvidenceDirectory (Join-Path $EvidenceDirectory "prevention-verifier-support")

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
    $pipeline = [string]$spec["Pipeline"]
    $target = [string]$spec["Target"]
    $skaffold = [string]$spec["Skaffold"]
    $imagesArg = [string]$spec["Images"]
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
            --images=$imagesArg --disable-initial-rollout `
            --annotations="sourceCommit=$($manifest.source_commit),buildRunId=$($manifest.build_run_id),environment=staging" `
            --quiet
        if ($LASTEXITCODE -ne 0) { throw "Cloud Deploy release failed: $pipeline" }
    }

    Invoke-GcloudJson -Arguments @(
        "deploy", "releases", "describe", $ReleaseName,
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$pipeline", "--format=json"
    ) -OutputPath (Join-Path $EvidenceDirectory "$pipeline-release.json") | Out-Null

    if ($pipeline -eq "natureprotector-prevention") {
        & ./scripts/cloud/Test-G81PreventionPreRolloutQualification.ps1 `
            -ManifestPath $ManifestPath `
            -SourceRoot $sourceRoot `
            -PlatformProjectId $PlatformProjectId `
            -StagingProjectId $StagingProjectId `
            -Region $Region `
            -ClusterName $ClusterName `
            -Target $target `
            -Namespace natureprotector-staging `
            -CloudSqlPrivateIp $CloudSqlPrivateIp `
            -RabbitMqTlsServerName $RabbitMqTlsServerName `
            -RabbitMqTlsCertificateVersion $RabbitMqTlsCertificateVersion `
            -EvidenceDirectory (Join-Path $EvidenceDirectory "prevention-pre-rollout-qualification")
        if ($LASTEXITCODE -ne 0) { throw "Prevention pre-rollout qualification failed." }
    }

    $rolloutEvidence = Join-Path $EvidenceDirectory "$pipeline-staging-rollout.json"
    $existingRollouts = Invoke-GcloudJson -Arguments @(
        "deploy", "rollouts", "list",
        "--project=$PlatformProjectId", "--region=$Region",
        "--delivery-pipeline=$pipeline", "--release=$ReleaseName",
        "--filter=targetId=$target", "--sort-by=~createTime", "--limit=1", "--format=json"
    )
    $existingRolloutItems = @($existingRollouts.output | ConvertFrom-Json)
    if ($existingRolloutItems.Count -eq 0) {
        $rolloutId = Get-CloudDeployRolloutId -Target $target -SourceCommit ([string]$manifest.source_commit)
        & gcloud deploy releases promote `
            --project=$PlatformProjectId --region=$Region `
            --delivery-pipeline=$pipeline `
            --release=$ReleaseName `
            --to-target=$target `
            --rollout-id=$rolloutId `
            --quiet
        if ($LASTEXITCODE -ne 0) { throw "Cloud Deploy rollout failed: $pipeline" }
    }

    $rolloutName = Wait-CloudDeployRollout `
        -Pipeline $pipeline `
        -Target $target `
        -OutputPath $rolloutEvidence `
        -FailureDiagnosticsDirectory (Join-Path $EvidenceDirectory "$pipeline-rollout-failure-diagnostics")
    $rolloutSummary += [ordered]@{ pipeline = $pipeline; target = $target; rollout = $rolloutName; state = "SUCCEEDED" }
}

$functionalSmokePassed = $false
$stagingVerified = $false
$edgeBootstrapPending = ($DeploymentMode -eq "services-only-bootstrap")
if ($DeploymentMode -eq "verified") {
    Test-G81PreSmokeReadiness `
        -OutputDirectory (Join-Path $EvidenceDirectory "pre-smoke-readiness") `
        -Rollouts $rolloutSummary

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

Write-G81EvidenceChecksums -EvidenceDirectory $EvidenceDirectory | Out-Null
