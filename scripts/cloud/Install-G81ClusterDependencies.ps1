[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$ClusterName,
    [Parameter(Mandatory)][string]$LockPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory
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
