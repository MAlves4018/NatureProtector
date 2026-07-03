$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$scriptPath = Join-Path $repoRoot "scripts/cloud/Install-G81ClusterDependencies.ps1"
$lockPath = Join-Path $repoRoot "infra/gcp/kubernetes/g8-1/operator-lock.json"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("np-g81-autopilot-bootstrap-" + [System.Guid]::NewGuid().ToString("N"))
$bin = Join-Path $tempRoot "bin"
$evidence = Join-Path $tempRoot "evidence"
New-Item -ItemType Directory -Force -Path $bin, $evidence | Out-Null

function New-CmdShim {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Target
    )

    $cmd = Join-Path $bin "$Name.cmd"
    "@echo off`r`npwsh -NoProfile -ExecutionPolicy Bypass -File `"$Target`" %*`r`nexit /b %ERRORLEVEL%`r`n" |
        Set-Content -LiteralPath $cmd -Encoding ascii
}

$fakeGcloud = Join-Path $tempRoot "fake-gcloud.ps1"
@'
$joined = $args -join " "
if ($joined -match "container clusters describe np-staging") {
    if ($env:NP_FAKE_CLUSTER_MODE -eq "standard") {
        Write-Output '{"autopilot":{"enabled":false}}'
    }
    else {
        Write-Output '{"autopilot":{"enabled":true}}'
    }
    exit 0
}
if ($joined -match "container clusters get-credentials np-staging") {
    Write-Output "kubeconfig updated"
    exit 0
}
Write-Output "{}"
exit 0
'@ | Set-Content -LiteralPath $fakeGcloud -Encoding utf8

$fakeBash = Join-Path $tempRoot "fake-bash.ps1"
@'
$recordPath = $env:NP_FAKE_BASH_RECORD
if ([string]::IsNullOrWhiteSpace($recordPath)) {
    Write-Error "NP_FAKE_BASH_RECORD is required by the fake bash shim."
    exit 98
}
$record = @{
    args = $args
    timeout = $env:NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS
} | ConvertTo-Json -Depth 6
$record | Set-Content -LiteralPath $recordPath -Encoding utf8
if ($env:NP_FAKE_BASH_EXIT -and [int]$env:NP_FAKE_BASH_EXIT -ne 0) {
    Write-Output "CLUSTER_DEPENDENCY=cert-manager"
    Write-Output "CLUSTER_DEPENDENCY_STATUS=FAILED"
    exit ([int]$env:NP_FAKE_BASH_EXIT)
}
Write-Output "OPERATOR_FOUNDATION_PROVED"
Write-Output "CLUSTER_DEPENDENCY=cert-manager"
Write-Output "CLUSTER_DEPENDENCY_STATUS=READY"
exit 0
'@ | Set-Content -LiteralPath $fakeBash -Encoding utf8

$fakeKubectl = Join-Path $tempRoot "fake-kubectl.ps1"
@'
$joined = $args -join " "
if ($joined -match "rollout status") {
    if ($env:NP_FAKE_FOUNDATION_READY -eq "true") {
        Write-Output "deployment is ready"
        exit 0
    }
    Write-Output "deployment is not ready"
    exit 1
}
if ($joined -match "get pods -o json") {
    switch ($env:NP_FAKE_POD_FAILURE) {
        "image-pull" {
            Write-Output '{"items":[{"metadata":{"name":"cert-manager-x"},"status":{"containerStatuses":[{"name":"cert-manager","state":{"waiting":{"reason":"ImagePullBackOff"}},"restartCount":0}]}}]}'
            exit 0
        }
        "crash" {
            Write-Output '{"items":[{"metadata":{"name":"cert-manager-x"},"status":{"containerStatuses":[{"name":"cert-manager","state":{"waiting":{"reason":"CrashLoopBackOff"}},"restartCount":3}]}}]}'
            exit 0
        }
        "quota" {
            Write-Output '{"items":[{"metadata":{"name":"cert-manager-x"},"status":{"conditions":[{"type":"PodScheduled","status":"False","reason":"Unschedulable","message":"0/1 nodes available: quota exceeded"}],"containerStatuses":[]}}]}'
            exit 0
        }
    }
    Write-Output '{"items":[]}'
    exit 0
}
if ($joined -match "get deployment -o name") {
    Write-Output "deployment/cert-manager"
    exit 0
}
Write-Output "fake kubectl $joined"
exit 0
'@ | Set-Content -LiteralPath $fakeKubectl -Encoding utf8

$fakeGh = Join-Path $tempRoot "fake-gh.ps1"
"'{}'; exit 0" | Set-Content -LiteralPath $fakeGh -Encoding utf8

New-CmdShim -Name "gcloud" -Target $fakeGcloud
New-CmdShim -Name "bash" -Target $fakeBash
New-CmdShim -Name "kubectl" -Target $fakeKubectl
New-CmdShim -Name "gh" -Target $fakeGh

function Invoke-Installer {
    param(
        [int]$BashExit = 0,
        [string]$PodFailure = "",
        [int]$TimeoutSeconds = 1800,
        [bool]$FoundationReady = $false
    )

    $env:PATH = "$bin;$script:OriginalPath"
    $env:NP_FAKE_BASH_EXIT = [string]$BashExit
    $env:NP_FAKE_POD_FAILURE = $PodFailure
    $env:NP_FAKE_CLUSTER_MODE = "autopilot"
    $env:NP_FAKE_FOUNDATION_READY = if ($FoundationReady) { "true" } else { "false" }
    $env:NP_FAKE_BASH_RECORD = Join-Path $tempRoot "bash-record-$([Guid]::NewGuid().ToString('N')).json"
    Remove-Item -LiteralPath $env:NP_FAKE_BASH_RECORD -Force -ErrorAction SilentlyContinue

    $output = & pwsh -NoProfile -File $scriptPath `
        -ProjectId natureprotector-500518 `
        -Region europe-southwest1 `
        -ClusterName np-staging `
        -LockPath $lockPath `
        -EvidenceDirectory $evidence `
        -RolloutTimeoutSeconds $TimeoutSeconds 2>&1

    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output | ForEach-Object { [string]$_ }) -join "`n"
        RecordPath = $env:NP_FAKE_BASH_RECORD
    }
}

$script:OriginalPath = $env:PATH
try {
    $success = Invoke-Installer -TimeoutSeconds 1200
    if ($success.ExitCode -ne 0 -or $success.Output -notmatch "CLUSTER_DEPENDENCY_STATUS=READY") {
        throw "Autopilot installer success path failed: $($success.Output)"
    }
    $record = Get-Content -Raw -LiteralPath $success.RecordPath | ConvertFrom-Json
    if ($record.timeout -ne "1200") { throw "Rollout timeout was not propagated to bash installer." }
    if (($record.args -join " ") -match "production") { throw "Autopilot installer test referenced production." }

    $alreadyReady = Invoke-Installer -FoundationReady $true
    if ($alreadyReady.ExitCode -ne 0 -or $alreadyReady.Output -notmatch "OPERATOR_FOUNDATION_ALREADY_READY") {
        throw "Expected already-ready Autopilot path to pass without reinstall: $($alreadyReady.Output)"
    }
    if (Test-Path -LiteralPath $alreadyReady.RecordPath) {
        throw "Already-ready Autopilot path called the bash reinstall path."
    }
    $alreadyReadyEvidence = Join-Path $evidence "cluster-dependencies.json"
    if (-not (Test-Path -LiteralPath $alreadyReadyEvidence)) {
        throw "Already-ready Autopilot path did not write cluster-dependencies evidence."
    }
    $alreadyReadyJson = Get-Content -Raw -LiteralPath $alreadyReadyEvidence | ConvertFrom-Json
    if (-not [bool]$alreadyReadyJson.reused_existing_operator_foundation) {
        throw "Already-ready evidence did not declare reused_existing_operator_foundation=true."
    }

    $imagePull = Invoke-Installer -BashExit 42 -PodFailure "image-pull"
    if ($imagePull.ExitCode -eq 0 -or $imagePull.Output -notmatch "CLUSTER_DEPENDENCY_FAILURE_CLASS=IMAGE_PULL") {
        throw "Expected IMAGE_PULL classification: $($imagePull.Output)"
    }
    if ($imagePull.Output -notmatch "CLUSTER_DEPENDENCY_DIAGNOSTICS_BEGIN" -or $imagePull.Output -notmatch "CLUSTER_DEPENDENCY_DIAGNOSTICS_END") {
        throw "Expected diagnostics markers on failure."
    }

    $crash = Invoke-Installer -BashExit 42 -PodFailure "crash"
    if ($crash.Output -notmatch "CLUSTER_DEPENDENCY_FAILURE_CLASS=CONTAINER_CRASH") {
        throw "Expected CONTAINER_CRASH classification: $($crash.Output)"
    }

    $quota = Invoke-Installer -BashExit 42 -PodFailure "quota"
    if ($quota.Output -notmatch "CLUSTER_DEPENDENCY_FAILURE_CLASS=QUOTA") {
        throw "Expected QUOTA classification: $($quota.Output)"
    }

    Write-Host "G81_CLUSTER_DEPENDENCY_AUTOPILOT_BOOTSTRAP_TEST=PASS"
}
finally {
    $env:PATH = $script:OriginalPath
    Remove-Item Env:NP_FAKE_BASH_EXIT -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_POD_FAILURE -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_CLUSTER_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_FOUNDATION_READY -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_BASH_RECORD -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
