$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$scriptPath = Join-Path $repoRoot "scripts/cloud/Test-G81StagingFoundationReadiness.ps1"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("np-g81-foundation-readiness-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
$fakeGcloud = Join-Path $tempRoot "fake-gcloud.ps1"

@'
$ErrorActionPreference = "Stop"
$joined = $args -join " "
$scenario = $env:NP_FAKE_GCLOUD_SCENARIO

function Write-Json([string]$json) {
    Write-Output $json
    exit 0
}

if ($scenario -eq "missing-cluster" -and $joined -match "container clusters describe np-staging") {
    [Console]::Error.WriteLine("NOT_FOUND: cluster np-staging was not found")
    exit 1
}

if ($scenario -eq "permission-denied" -and $joined -match "sql instances describe np-staging-postgres") {
    Write-Output "PERMISSION_DENIED: caller lacks cloudsql.instances.get"
    exit 1
}

if ($scenario -eq "multiple-missing" -and $joined -match "compute addresses describe np-staging-https") {
    [Console]::Error.WriteLine("NOT_FOUND: address np-staging-https was not found")
    exit 1
}

if ($scenario -eq "multiple-missing" -and $joined -match "builds worker-pools describe np-staging-deploy") {
    [Console]::Error.WriteLine("NOT_FOUND: worker pool np-staging-deploy was not found")
    exit 1
}

if ($joined -match "container clusters describe np-staging") {
    if ($scenario -eq "cluster-not-ready") {
        Write-Json '{"status":"PROVISIONING","autopilot":{"enabled":true}}'
    }
    Write-Json '{"status":"RUNNING","autopilot":{"enabled":true}}'
}

if ($joined -match "sql instances describe np-staging-postgres") {
    Write-Json '{"state":"RUNNABLE"}'
}

if ($joined -match "compute ssl-certificates describe np-staging") {
    if ($scenario -eq "certificate-not-active") {
        Write-Json '{"managed":{"status":"PROVISIONING"}}'
    }
    Write-Json '{"managed":{"status":"ACTIVE"}}'
}

if ($joined -match "container clusters get-credentials np-staging") {
    Write-Output "kubeconfig updated"
    exit 0
}

Write-Json '{}'
'@ | Set-Content -LiteralPath $fakeGcloud -Encoding utf8

function Invoke-Readiness {
    param(
        [string]$Scenario,
        [switch]$RequireActiveCertificate
    )

    $env:NP_FAKE_GCLOUD_SCENARIO = $Scenario
    $arguments = @(
        "-NoProfile",
        "-File", $scriptPath,
        "-GcloudCommand", $fakeGcloud,
        "-SkipKubeCredentials"
    )
    if ($RequireActiveCertificate) { $arguments += "-RequireActiveCertificate" }
    $output = & pwsh @arguments 2>&1
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output | ForEach-Object { [string]$_ }) -join "`n"
    }
}

try {
    $pass = Invoke-Readiness -Scenario "pass" -RequireActiveCertificate
    if ($pass.ExitCode -ne 0 -or $pass.Output -notmatch "STAGING_FOUNDATION_READINESS=PASS") {
        throw "Expected pass readiness but got exit $($pass.ExitCode): $($pass.Output)"
    }

    $missing = Invoke-Readiness -Scenario "missing-cluster"
    if ($missing.ExitCode -eq 0 -or $missing.Output -notmatch "MISSING_RESOURCE=GKE_CLUSTER:np-staging") {
        throw "Expected missing cluster marker: $($missing.Output)"
    }
    if ($missing.Output -match "STAGING_FOUNDATION_READINESS=PASS") {
        throw "Missing cluster must not pass."
    }

    $denied = Invoke-Readiness -Scenario "permission-denied"
    if ($denied.ExitCode -eq 0 -or $denied.Output -notmatch "PERMISSION_DENIED=CLOUD_SQL:np-staging-postgres") {
        throw "Expected permission marker: $($denied.Output)"
    }

    $notReady = Invoke-Readiness -Scenario "cluster-not-ready"
    if ($notReady.ExitCode -eq 0 -or $notReady.Output -notmatch "NOT_READY=GKE_CLUSTER:np-staging:PROVISIONING") {
        throw "Expected not-ready marker: $($notReady.Output)"
    }

    $cert = Invoke-Readiness -Scenario "certificate-not-active" -RequireActiveCertificate
    if ($cert.ExitCode -eq 0 -or $cert.Output -notmatch "NOT_READY=MANAGED_CERTIFICATE:np-staging:PROVISIONING") {
        throw "Expected certificate not-ready marker: $($cert.Output)"
    }

    $multi = Invoke-Readiness -Scenario "multiple-missing"
    if (
        $multi.ExitCode -eq 0 -or
        $multi.Output -notmatch "MISSING_RESOURCE=CLOUD_BUILD_WORKER_POOL:np-staging-deploy" -or
        $multi.Output -notmatch "MISSING_RESOURCE=EDGE_IP:np-staging-https" -or
        $multi.Output -notmatch "STAGING_FOUNDATION_FAILURES=2"
    ) {
        throw "Expected aggregated missing resource markers: $($multi.Output)"
    }

    Write-Host "G81_STAGING_FOUNDATION_READINESS_TEST=PASS"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_GCLOUD_SCENARIO -ErrorAction SilentlyContinue
}
