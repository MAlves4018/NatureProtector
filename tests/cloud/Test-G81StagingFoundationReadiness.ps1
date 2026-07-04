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

if ($args.Count -ge 2 -and $args[0] -eq "projects" -and $args[1] -eq "get-iam-policy") {
    if ($scenario -eq "secret-access-denied") {
        Write-Output ""
        exit 0
    }
    Write-Output "serviceAccount:np-staging-migrations@natureprotector-500518.iam.gserviceaccount.com"
    Write-Output "serviceAccount:np-staging-bootstrap@natureprotector-500518.iam.gserviceaccount.com"
    Write-Output "serviceAccount:np-staging-api@natureprotector-500518.iam.gserviceaccount.com"
    Write-Output "serviceAccount:np-staging-prevention@natureprotector-500518.iam.gserviceaccount.com"
    Write-Output "serviceAccount:np-staging-secret-sync@natureprotector-500518.iam.gserviceaccount.com"
    Write-Output "serviceAccount:np-staging-simulator@natureprotector-500518.iam.gserviceaccount.com"
    Write-Output "serviceAccount:np-staging-smoke@natureprotector-500518.iam.gserviceaccount.com"
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

if ($joined -match "secrets versions describe") {
    if ($scenario -eq "secret-version-missing" -and $joined -match "np-staging-rabbitmq-ca-certificate") {
        [Console]::Error.WriteLine("NOT_FOUND: secret version was not found")
        exit 1
    }
    if ($scenario -eq "secret-version-disabled" -and $joined -match "np-staging-rabbitmq-ca-certificate") {
        Write-Json '{"state":"DISABLED"}'
    }
    if ($scenario -eq "secret-version-destroyed" -and $joined -match "np-staging-rabbitmq-ca-certificate") {
        Write-Json '{"state":"DESTROYED"}'
    }
    if ($scenario -eq "secret-version-permission-denied" -and $joined -match "np-staging-rabbitmq-ca-certificate") {
        Write-Output "PERMISSION_DENIED: caller lacks secretmanager.versions.get"
        exit 1
    }
    Write-Json '{"state":"ENABLED"}'
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
        "-SkipKubeCredentials",
        "-CloudSqlCaVersion", "2",
        "-RabbitMqCaVersion", "7",
        "-RabbitMqTlsCertificateVersion", "8",
        "-RabbitMqTlsPrivateKeyVersion", "9"
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
    if ($pass.Output -notmatch "STAGING_SECRET_VERSION_READINESS=PASS") {
        throw "Expected secret version readiness pass marker: $($pass.Output)"
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

    $missingVersion = Invoke-Readiness -Scenario "secret-version-missing"
    if ($missingVersion.ExitCode -eq 0 -or $missingVersion.Output -notmatch "MISSING_SECRET_VERSION=np-staging-rabbitmq-ca-certificate:7") {
        throw "Expected missing secret version marker: $($missingVersion.Output)"
    }

    $disabledVersion = Invoke-Readiness -Scenario "secret-version-disabled"
    if ($disabledVersion.ExitCode -eq 0 -or $disabledVersion.Output -notmatch "DISABLED_SECRET_VERSION=np-staging-rabbitmq-ca-certificate:7") {
        throw "Expected disabled secret version marker: $($disabledVersion.Output)"
    }

    $destroyedVersion = Invoke-Readiness -Scenario "secret-version-destroyed"
    if ($destroyedVersion.ExitCode -eq 0 -or $destroyedVersion.Output -notmatch "DESTROYED_SECRET_VERSION=np-staging-rabbitmq-ca-certificate:7") {
        throw "Expected destroyed secret version marker: $($destroyedVersion.Output)"
    }

    $versionDenied = Invoke-Readiness -Scenario "secret-version-permission-denied"
    if ($versionDenied.ExitCode -eq 0 -or $versionDenied.Output -notmatch "PERMISSION_DENIED=SECRET_VERSION:np-staging-rabbitmq-ca-certificate:7") {
        throw "Expected secret version permission marker: $($versionDenied.Output)"
    }

    $accessDenied = Invoke-Readiness -Scenario "secret-access-denied"
    if ($accessDenied.ExitCode -eq 0 -or $accessDenied.Output -notmatch "SECRET_ACCESS_DENIED=np-staging-migrations@natureprotector-500518.iam.gserviceaccount.com:np-staging-rabbitmq-ca-certificate:7") {
        throw "Expected secret accessor marker: $($accessDenied.Output)"
    }

    $latest = & pwsh -NoProfile -File $scriptPath -GcloudCommand $fakeGcloud -SkipKubeCredentials -CloudSqlCaVersion 2 -RabbitMqCaVersion latest -RabbitMqTlsCertificateVersion 8 -RabbitMqTlsPrivateKeyVersion 9 2>&1
    $latestOutput = ($latest | ForEach-Object { [string]$_ }) -join "`n"
    if ($LASTEXITCODE -eq 0 -or $latestOutput -notmatch "MISSING_SECRET_VERSION=np-staging-rabbitmq-ca-certificate:latest") {
        throw "Expected latest to be rejected: $latestOutput"
    }
    if ($latestOutput -match "PRIVATE KEY|BEGIN") {
        throw "Secret payload text must not appear in readiness logs."
    }

    Write-Host "G81_STAGING_FOUNDATION_READINESS_TEST=PASS"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_GCLOUD_SCENARIO -ErrorAction SilentlyContinue
}
