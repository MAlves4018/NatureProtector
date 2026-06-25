[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet("staging", "production")][string]$EnvironmentName,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$ProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$FrontendOrigin,
    [Parameter(Mandatory)][string]$SmokeServiceAccount,
    [Parameter(Mandatory)][string]$AdminUsername,
    [Parameter(Mandatory)][string]$AdminPasswordSecret,
    [Parameter(Mandatory)][string]$AdminPasswordVersion,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
if ($Region -ne "europe-southwest1") { throw "Unexpected region '$Region'." }
if ($ProjectId -match "(?i)cn2526") { throw "CN projects are forbidden." }
try { $frontendUri = [Uri]$FrontendOrigin } catch { throw "FrontendOrigin must be an absolute HTTPS origin." }
if (-not $frontendUri.IsAbsoluteUri -or $frontendUri.Scheme -ne "https" -or [string]::IsNullOrWhiteSpace($frontendUri.Host)) {
    throw "FrontendOrigin must be an absolute HTTPS origin."
}
if ($frontendUri.Host -match '(?i)\.run\.app$') {
    throw "FrontendOrigin must use the protected edge hostname, not a direct Cloud Run run.app URL."
}
if ($frontendUri.AbsolutePath -ne '/' -or $frontendUri.Query -or $frontendUri.Fragment) {
    throw "FrontendOrigin must be an HTTPS origin without a path, query, or fragment."
}
python scripts/cloud/Test-G81ReleaseManifest.py $ManifestPath
if ($LASTEXITCODE -ne 0) { throw "Invalid G8.1 release manifest." }
$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json -AsHashtable
$image = $manifest.images.'functional-smoke'.reference
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

$job = "np-functional-smoke"
& gcloud run jobs deploy $job `
    --project=$ProjectId --region=$Region `
    --image=$image --service-account=$SmokeServiceAccount `
    --tasks=1 --parallelism=1 --max-retries=0 --task-timeout=15m `
    --set-env-vars="FRONTEND_ORIGIN=$FrontendOrigin,ADMIN_USERNAME=$AdminUsername" `
    --set-secrets="ADMIN_PASSWORD=${AdminPasswordSecret}:${AdminPasswordVersion}" `
    --labels="environment=$EnvironmentName,phase=g8-1,purpose=functional-smoke" `
    --quiet
if ($LASTEXITCODE -ne 0) { throw "Functional smoke job deployment failed." }

& gcloud run jobs execute $job `
    --project=$ProjectId --region=$Region --wait --format=json |
    Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "functional-smoke-execution.json")
if ($LASTEXITCODE -ne 0) { throw "Functional smoke execution failed." }

& gcloud run jobs describe $job `
    --project=$ProjectId --region=$Region --format=json |
    Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "functional-smoke-job.json")
if ($LASTEXITCODE -ne 0) { throw "Functional smoke job description failed." }

[ordered]@{
    schema_version = 1
    environment = $EnvironmentName
    project_id = $ProjectId
    source_commit = $manifest.source_commit
    frontend_origin = $FrontendOrigin
    image = $image
    status = "passed"
    production_authorized = $false
} | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $EvidenceDirectory "functional-smoke-summary.json")
