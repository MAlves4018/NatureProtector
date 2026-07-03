[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$ClusterName,
    [Parameter(Mandatory)][string]$LockPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [ValidateRange(300, 3600)][int]$RolloutTimeoutSeconds = 1800
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Region -ne "europe-southwest1") { throw "Unexpected region '$Region'." }
if ($ProjectId -match "(?i)cn2526") { throw "CN projects are forbidden." }
if (-not (Test-Path -LiteralPath $LockPath -PathType Leaf)) { throw "Operator lock file not found: $LockPath" }
foreach ($command in @("gh", "gcloud", "kubectl")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "Required command is unavailable: $command" }
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

function ConvertTo-BashPath {
    param([Parameter(Mandatory)][string]$Path)

    $cygpath = Get-Command cygpath -ErrorAction SilentlyContinue
    if ($cygpath) {
        $converted = & $cygpath.Source -u $Path
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($converted)) {
            return ($converted | Select-Object -First 1)
        }
    }

    return ($Path -replace '\\', '/')
}

function Get-OptionalProperty {
    param(
        [Parameter(Mandatory)]$Object,
        [Parameter(Mandatory)][string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-G81ClusterDependencyFailureClass {
    param([Parameter(Mandatory)][string]$Namespace)

    $snapshot = & kubectl -n $Namespace get pods -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($snapshot -join "`n"))) {
        return "UNKNOWN"
    }

    $pods = ($snapshot -join "`n") | ConvertFrom-Json
    foreach ($pod in @($pods.items)) {
        $status = Get-OptionalProperty -Object $pod -Name "status"
        if ($null -eq $status) { continue }
        foreach ($condition in @((Get-OptionalProperty -Object $status -Name "conditions")) | Where-Object { $null -ne $_ }) {
            $conditionType = [string](Get-OptionalProperty -Object $condition -Name "type")
            $conditionStatus = [string](Get-OptionalProperty -Object $condition -Name "status")
            if ($conditionType -eq "PodScheduled" -and $conditionStatus -eq "False") {
                $message = [string](Get-OptionalProperty -Object $condition -Name "message")
                if ($message -match "(?i)quota|insufficient|OutOfResource") { return "QUOTA" }
                if ($message -match "(?i)admission|warden|forbidden|denied") { return "RESOURCE_REQUEST_OR_ADMISSION" }
                return "AUTOPILOT_CAPACITY_OR_INITIAL_SCHEDULING"
            }
        }
        foreach ($containerStatus in @((Get-OptionalProperty -Object $status -Name "containerStatuses")) | Where-Object { $null -ne $_ }) {
            $state = Get-OptionalProperty -Object $containerStatus -Name "state"
            $waiting = if ($null -ne $state) { Get-OptionalProperty -Object $state -Name "waiting" } else { $null }
            $terminated = if ($null -ne $state) { Get-OptionalProperty -Object $state -Name "terminated" } else { $null }
            $waitingReason = if ($null -ne $waiting) { [string](Get-OptionalProperty -Object $waiting -Name "reason") } else { "" }
            $terminatedReason = if ($null -ne $terminated) { [string](Get-OptionalProperty -Object $terminated -Name "reason") } else { "" }
            if ($waitingReason -in @("ImagePullBackOff", "ErrImagePull", "InvalidImageName")) { return "IMAGE_PULL" }
            if ($waitingReason -eq "CrashLoopBackOff") { return "CONTAINER_CRASH" }
            if ($terminatedReason -eq "OOMKilled") { return "RESOURCE_REQUEST_OR_ADMISSION" }
            if ($terminatedReason -in @("Error", "ContainerCannotRun")) { return "CONTAINER_CRASH" }
        }
    }

    return "UNKNOWN"
}

function Save-G81ClusterDependencyDiagnostics {
    param(
        [Parameter(Mandatory)][string]$Namespace,
        [Parameter(Mandatory)][string]$DependencyName,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    $diagnosticsDirectory = Join-Path $OutputDirectory "diagnostics"
    New-Item -ItemType Directory -Force -Path $diagnosticsDirectory | Out-Null

    Write-Host "CLUSTER_DEPENDENCY_DIAGNOSTICS_BEGIN"
    & kubectl -n $Namespace get deployments,replicasets,pods,services,endpoints,events -o wide 2>&1 |
        Tee-Object -FilePath (Join-Path $diagnosticsDirectory "$DependencyName-resources-wide.txt")
    & kubectl -n $Namespace get deployments -o yaml 2>&1 |
        Tee-Object -FilePath (Join-Path $diagnosticsDirectory "$DependencyName-deployments.yaml")
    & kubectl -n $Namespace describe pods 2>&1 |
        Tee-Object -FilePath (Join-Path $diagnosticsDirectory "$DependencyName-describe-pods.txt")
    & kubectl get events --all-namespaces --sort-by=.lastTimestamp 2>&1 |
        Tee-Object -FilePath (Join-Path $diagnosticsDirectory "$DependencyName-events.txt")
    foreach ($deployment in @(& kubectl -n $Namespace get deployment -o name 2>$null)) {
        $safe = $deployment -replace '/', '-'
        & kubectl -n $Namespace describe $deployment 2>&1 |
            Tee-Object -FilePath (Join-Path $diagnosticsDirectory "$DependencyName-describe-$safe.txt")
        & kubectl -n $Namespace logs $deployment --all-containers=true --tail=500 2>&1 |
            Tee-Object -FilePath (Join-Path $diagnosticsDirectory "$DependencyName-logs-$safe.txt")
    }
    Write-Host "CLUSTER_DEPENDENCY_DIAGNOSTICS_END"
}

function Test-G81OperatorFoundationReady {
    $lock = Get-Content -Raw $LockPath | ConvertFrom-Json
    if ([int]$lock.schema_version -ne 1) { throw "Unsupported operator lock schema." }

    foreach ($dependency in $lock.dependencies) {
        foreach ($rollout in $dependency.rollouts) {
            & kubectl -n ([string]$dependency.namespace) rollout status ([string]$rollout) --timeout=10s *> $null
            if ($LASTEXITCODE -ne 0) {
                return $false
            }
        }
    }

    return $true
}

function Write-G81OperatorFoundationAlreadyReadyEvidence {
    $lock = Get-Content -Raw $LockPath | ConvertFrom-Json
    $resolved = @()
    foreach ($dependency in $lock.dependencies) {
        $resolved += [ordered]@{
            name = [string]$dependency.name
            repository = [string]$dependency.repository
            tag = [string]$dependency.tag
            asset = [string]$dependency.asset
            namespace = [string]$dependency.namespace
            rollouts = @($dependency.rollouts)
            status = "already-ready"
        }
    }

    [ordered]@{
        schema_version = 2
        project_id = $ProjectId
        region = $Region
        cluster_name = $ClusterName
        lock_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $LockPath).Hash.ToLowerInvariant()
        dependencies = $resolved
        reused_existing_operator_foundation = $true
        external_registry_runtime_dependency = $false
        status = "passed"
    } | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "cluster-dependencies.json")
}

function Invoke-G81AutopilotClusterDependencyInstaller {
    $scriptPath = Join-Path $PSScriptRoot "install-g81-cluster-dependencies-autopilot.sh"
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Autopilot cluster dependency installer not found: $scriptPath"
    }
    if (-not (Get-Command bash -ErrorAction SilentlyContinue)) {
        throw "Required command is unavailable for Autopilot cluster dependencies: bash"
    }

    Write-Host "CLUSTER_DEPENDENCY=cert-manager"
    Write-Host "CLUSTER_DEPENDENCY_STATUS=WAITING"
    Write-Host "CERT_MANAGER_WAITING_FOR_AUTOPILOT_CAPACITY"

    & gcloud container clusters get-credentials $ClusterName `
        --project=$ProjectId --region=$Region --dns-endpoint --quiet
    if ($LASTEXITCODE -ne 0) { throw "Unable to acquire DNS-endpoint GKE credentials." }

    if (Test-G81OperatorFoundationReady) {
        Write-G81OperatorFoundationAlreadyReadyEvidence
        Write-Host "OPERATOR_FOUNDATION_ALREADY_READY"
        Write-Host "CLUSTER_DEPENDENCY=cert-manager"
        Write-Host "CLUSTER_DEPENDENCY_STATUS=READY"
        exit 0
    }

    $env:NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS = [string]$RolloutTimeoutSeconds
    $arguments = @(
        (ConvertTo-BashPath -Path $scriptPath),
        $ProjectId,
        $Region,
        $ClusterName,
        (ConvertTo-BashPath -Path $LockPath),
        (ConvertTo-BashPath -Path $EvidenceDirectory)
    )
    & bash @arguments
    $exitCode = $LASTEXITCODE
    Remove-Item Env:NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS -ErrorAction SilentlyContinue
    if ($exitCode -ne 0) {
        $failureClass = Get-G81ClusterDependencyFailureClass -Namespace "cert-manager"
        Save-G81ClusterDependencyDiagnostics -Namespace "cert-manager" -DependencyName "cert-manager" -OutputDirectory $EvidenceDirectory
        Write-Host "CLUSTER_DEPENDENCY=cert-manager"
        Write-Host "CLUSTER_DEPENDENCY_STATUS=FAILED"
        Write-Host "CLUSTER_DEPENDENCY_FAILURE_CLASS=$failureClass"
        exit $exitCode
    }

    Write-Host "CLUSTER_DEPENDENCY=cert-manager"
    Write-Host "CLUSTER_DEPENDENCY_STATUS=READY"
    exit 0
}

$clusterJson = & gcloud container clusters describe $ClusterName `
    --project=$ProjectId --region=$Region --format=json
if ($LASTEXITCODE -ne 0) { throw "Unable to describe GKE cluster $ClusterName." }
$cluster = ($clusterJson -join "`n") | ConvertFrom-Json
if ([bool]$cluster.autopilot.enabled) {
    Invoke-G81AutopilotClusterDependencyInstaller
}

$downloadDirectory = Join-Path $EvidenceDirectory "verified-release-assets"
New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null
$lock = Get-Content -Raw $LockPath | ConvertFrom-Json
if ([int]$lock.schema_version -ne 1) { throw "Unsupported operator lock schema." }

& gcloud container clusters get-credentials $ClusterName `
    --project=$ProjectId --region=$Region --dns-endpoint --quiet
if ($LASTEXITCODE -ne 0) { throw "Unable to acquire DNS-endpoint GKE credentials." }

$resolved = @()
foreach ($dependency in $lock.dependencies) {
    if ([string]$dependency.tag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+$') {
        throw "Dependency $($dependency.name) is not pinned to an exact semantic version."
    }
    $releaseJson = & gh api "repos/$($dependency.repository)/releases/tags/$($dependency.tag)"
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve release $($dependency.repository)@$($dependency.tag)." }
    $release = $releaseJson | ConvertFrom-Json
    $asset = @($release.assets | Where-Object { $_.name -eq [string]$dependency.asset })
    if ($asset.Count -ne 1) { throw "Expected exactly one asset '$($dependency.asset)' for $($dependency.name)." }
    $digest = [string]$asset[0].digest
    if ($digest -notmatch '^sha256:[0-9a-fA-F]{64}$') {
        throw "GitHub did not publish a sha256 digest for $($dependency.name)/$($dependency.asset)."
    }
    $destination = Join-Path $downloadDirectory ([string]$dependency.asset)
    Invoke-WebRequest -UseBasicParsing -Uri ([string]$asset[0].browser_download_url) -OutFile $destination
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash.ToLowerInvariant()
    $expected = $digest.Substring(7).ToLowerInvariant()
    if ($actual -ne $expected) { throw "Digest mismatch for $($dependency.name): expected $expected, got $actual." }

    & kubectl apply --server-side --field-manager=natureprotector-g81-foundation -f $destination
    if ($LASTEXITCODE -ne 0) { throw "Failed to apply verified dependency $($dependency.name)." }
    foreach ($rollout in $dependency.rollouts) {
        & kubectl -n ([string]$dependency.namespace) rollout status ([string]$rollout) --timeout=10m
        if ($LASTEXITCODE -ne 0) { throw "Dependency rollout failed: $($dependency.name)/$rollout" }
    }
    $resolved += [ordered]@{
        name = [string]$dependency.name
        repository = [string]$dependency.repository
        tag = [string]$dependency.tag
        release_id = [string]$release.id
        asset = [string]$dependency.asset
        asset_id = [string]$asset[0].id
        sha256 = $actual
        published_at = [string]$release.published_at
        namespace = [string]$dependency.namespace
        rollouts = @($dependency.rollouts)
    }
}

[ordered]@{
    schema_version = 1
    project_id = $ProjectId
    region = $Region
    cluster_name = $ClusterName
    lock_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $LockPath).Hash.ToLowerInvariant()
    dependencies = $resolved
    status = "passed"
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "cluster-dependencies.json")
