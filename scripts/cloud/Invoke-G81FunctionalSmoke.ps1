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

function Invoke-SmokeDiagnosticCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$OutputPath
    )

    $output = & $FilePath @Arguments 2>&1
    $exit = $LASTEXITCODE
    $output | Set-Content -Encoding utf8 -LiteralPath $OutputPath
    return [ordered]@{ exit_code = $exit; output = ($output -join "`n") }
}

function Get-FunctionalSmokeFailureClass {
    param([Parameter(Mandatory)][string]$FailedStep)

    if ($FailedStep -match '^FRONTEND_(HEALTH|INDEX)_TRANSPORT_FAILED$') { return "TLS_OR_CERTIFICATE" }
    if ($FailedStep -eq "FRONTEND_HEALTH_HTTP_FAILED") { return "FRONTEND_HEALTH" }
    if ($FailedStep -eq "FRONTEND_INDEX_HTTP_FAILED") { return "FRONTEND_INDEX" }
    if ($FailedStep -match '^LOGIN') { return "LOGIN" }
    if ($FailedStep -match '^JWT|TOKEN') { return "JWT_OR_AUTH" }
    if ($FailedStep -match '^AREAS') { return "AREAS_ENDPOINT" }
    if ($FailedStep -match '^USER_CREATE') { return "USER_CREATE" }
    if ($FailedStep -match '^USER_READ') { return "USER_READ" }
    if ($FailedStep -match '^USER_DELETE') { return "USER_DELETE" }
    if ($FailedStep -match 'TIMEOUT') { return "TIMEOUT" }
    return "OTHER_PROVED_CAUSE"
}

function Test-SmokeOriginMatchesManagedCertificate {
    param([Parameter(Mandatory)][string]$OutputDirectory)

    $certificateName = if ($EnvironmentName -eq "staging") { "np-staging" } else { "np-production" }
    $outputPath = Join-Path $OutputDirectory "managed-certificate.json"
    $result = Invoke-SmokeDiagnosticCommand -FilePath "gcloud" -Arguments @(
        "compute", "ssl-certificates", "describe", $certificateName,
        "--project=$ProjectId", "--global", "--format=json"
    ) -OutputPath $outputPath
    if ($result.exit_code -ne 0) { return $true }

    try {
        $certificate = Get-Content -Raw -LiteralPath $outputPath | ConvertFrom-Json
        $domains = @($certificate.managed.domains | ForEach-Object { [string]$_ })
        return [string]$certificate.managed.status -eq "ACTIVE" -and $domains -contains $frontendUri.Host
    }
    catch {
        return $true
    }
}

function Get-LatestSmokeExecutionName {
    param([Parameter(Mandatory)][string]$JobName)

    $result = & gcloud run jobs executions list `
        --job=$JobName `
        --project=$ProjectId `
        --region=$Region `
        --sort-by="~metadata.creationTimestamp" `
        --limit=1 `
        --format=json
    if ($LASTEXITCODE -ne 0) { return "" }
    $items = @($result | ConvertFrom-Json)
    if ($items.Count -eq 0) { return "" }
    if ($items[0].metadata.name) { return [string]$items[0].metadata.name }
    if ($items[0].name) { return ([string]$items[0].name -split '/')[-1] }
    return ""
}

function Write-FunctionalSmokeFailureDiagnostics {
    param(
        [Parameter(Mandatory)][string]$JobName,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $executionName = Get-LatestSmokeExecutionName -JobName $JobName
    if ([string]::IsNullOrWhiteSpace($executionName)) {
        [ordered]@{
            schema_version = 1
            status = "failed"
            failure_class = "UNKNOWN"
            failed_step = "UNKNOWN"
        } | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "functional-smoke-failure-summary.json")
        Write-Host "FUNCTIONAL_SMOKE_FAILURE_CLASS=UNKNOWN"
        Write-Host "FUNCTIONAL_SMOKE_FAILED_STEP=UNKNOWN"
        Write-Host "FUNCTIONAL_SMOKE_DIAGNOSTICS=$OutputDirectory"
        return
    }

    Invoke-SmokeDiagnosticCommand -FilePath "gcloud" -Arguments @(
        "run", "jobs", "executions", "describe", $executionName,
        "--project=$ProjectId", "--region=$Region", "--format=json"
    ) -OutputPath (Join-Path $OutputDirectory "functional-smoke-execution.json") | Out-Null

    Invoke-SmokeDiagnosticCommand -FilePath "gcloud" -Arguments @(
        "run", "jobs", "executions", "describe", $executionName,
        "--project=$ProjectId", "--region=$Region", "--format=yaml"
    ) -OutputPath (Join-Path $OutputDirectory "functional-smoke-execution.yaml") | Out-Null

    $filter = "resource.type=`"cloud_run_job`" AND resource.labels.job_name=`"$JobName`" AND resource.labels.location=`"$Region`" AND labels.`"run.googleapis.com/execution_name`"=`"$executionName`""
    Invoke-SmokeDiagnosticCommand -FilePath "gcloud" -Arguments @(
        "logging", "read", $filter,
        "--project=$ProjectId", "--format=json", "--limit=500"
    ) -OutputPath (Join-Path $OutputDirectory "functional-smoke-logs.json") | Out-Null
    Invoke-SmokeDiagnosticCommand -FilePath "gcloud" -Arguments @(
        "logging", "read", $filter,
        "--project=$ProjectId", "--format=value(timestamp,severity,textPayload,jsonPayload.message)", "--limit=500"
    ) -OutputPath (Join-Path $OutputDirectory "functional-smoke-logs.txt") | Out-Null

    $logsText = Get-Content -Raw -LiteralPath (Join-Path $OutputDirectory "functional-smoke-logs.txt")
    $failedStep = "UNKNOWN"
    $stageMatch = [regex]::Match($logsText, 'FUNCTIONAL_SMOKE_STAGE=([A-Z0-9_]+)')
    if ($stageMatch.Success) { $failedStep = $stageMatch.Groups[1].Value }
    $failureClass = if ($failedStep -eq "UNKNOWN") { "UNKNOWN" } else { Get-FunctionalSmokeFailureClass -FailedStep $failedStep }
    if ($failedStep -match '^FRONTEND_(HEALTH|INDEX)_TRANSPORT_FAILED$' -and
        -not (Test-SmokeOriginMatchesManagedCertificate -OutputDirectory $OutputDirectory)) {
        $failureClass = "SMOKE_CONFIGURATION"
    }

    [ordered]@{
        schema_version = 1
        status = "failed"
        execution = $executionName
        failure_class = $failureClass
        failed_step = $failedStep
        frontend_origin = $FrontendOrigin
        production_authorized = $false
        production_deployed = $false
    } | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "functional-smoke-failure-summary.json")

    Write-Host "FUNCTIONAL_SMOKE_FAILURE_CLASS=$failureClass"
    Write-Host "FUNCTIONAL_SMOKE_FAILED_STEP=$failedStep"
    Write-Host "FUNCTIONAL_SMOKE_DIAGNOSTICS=$OutputDirectory"
}

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
if ($LASTEXITCODE -ne 0) {
    Write-FunctionalSmokeFailureDiagnostics `
        -JobName $job `
        -OutputDirectory (Join-Path $EvidenceDirectory "functional-smoke-failure-diagnostics")
    throw "Functional smoke execution failed."
}

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
