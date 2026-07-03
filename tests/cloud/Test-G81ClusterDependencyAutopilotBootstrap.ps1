$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$wrapperPath = Join-Path $repoRoot "scripts/cloud/Install-G81ClusterDependencies.ps1"
$bashInstallerPath = Join-Path $repoRoot "scripts/cloud/install-g81-cluster-dependencies-autopilot.sh"
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

    $posix = Join-Path $bin $Name
    $escapedTarget = $Target.Replace("'", "'\''")
    "#!/usr/bin/env bash`npwsh -NoProfile -ExecutionPolicy Bypass -File '$escapedTarget' ""`$@""`nexit `$?`n" |
        Set-Content -LiteralPath $posix -Encoding utf8NoBOM
    $chmod = Get-Command chmod -ErrorAction SilentlyContinue
    if ($chmod) {
        & $chmod.Source +x $posix
    }
}

function Assert-CommandResolvesToShim {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ShimDirectory
    )

    $resolved = Get-Command $Name -CommandType Application -ErrorAction Stop |
        Select-Object -First 1

    $expected = [IO.Path]::TrimEndingDirectorySeparator(
        [IO.Path]::GetFullPath($ShimDirectory)
    )
    $actual = [IO.Path]::TrimEndingDirectorySeparator(
        [IO.Path]::GetFullPath((Split-Path -Parent $resolved.Source))
    )

    if ($actual -ne $expected) {
        throw "Command did not resolve to test shim. Name=$Name Source=$($resolved.Source) Expected=$expected"
    }
}

function Get-RealBash {
    $gitBash = "C:\Program Files\Git\bin\bash.exe"
    if (Test-Path -LiteralPath $gitBash -PathType Leaf) {
        return $gitBash
    }

    $candidate = Get-Command bash -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($candidate -and $candidate.Source -notmatch "\\Windows\\(system32|System32|WindowsApps)\\bash\.exe$") {
        return $candidate.Source
    }

    return $null
}

function ConvertTo-BashPath {
    param([Parameter(Mandatory)][string]$Path)

    $cygpath = Get-Command cygpath -ErrorAction SilentlyContinue
    if ($cygpath) {
        $converted = & $cygpath.Source -u $Path
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($converted)) {
            return ($converted | Select-Object -First 1)
        }
    }

    if ($Path -match '^([A-Za-z]):\\(.*)$') {
        $drive = $matches[1].ToLowerInvariant()
        $tail = $matches[2] -replace '\\', '/'
        return "/$drive/$tail"
    }

    return ($Path -replace '\\', '/')
}

$fakeGcloud = Join-Path $tempRoot "fake-gcloud.ps1"
@'
$joined = $args -join " "
$jsonPolicy = '{"bindings":[{"role":"roles/artifactregistry.writer","members":["serviceAccount:np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"]},{"role":"roles/artifactregistry.reader","members":["serviceAccount:np-staging-gke-nodes@natureprotector-500518.iam.gserviceaccount.com"]},{"role":"roles/storage.objectAdmin","members":["serviceAccount:np-cd-deploy@natureprotector-500518.iam.gserviceaccount.com"]}]}'

if ($joined -match "projects describe natureprotector-500518") {
    Write-Output "22505444922"
    exit 0
}
if ($joined -match "container clusters describe np-staging") {
    if ($env:NP_FAKE_CLUSTER_MODE -eq "standard") {
        Write-Output '{"autopilot":{"enabled":false}}'
    }
    else {
        Write-Output '{"autopilot":{"enabled":true},"nodeConfig":{"serviceAccount":"np-staging-gke-nodes@natureprotector-500518.iam.gserviceaccount.com"}}'
    }
    exit 0
}
if ($joined -match "container clusters get-credentials np-staging") {
    Write-Output "kubeconfig updated"
    exit 0
}
if ($joined -match "artifacts repositories describe np-releases") {
    Write-Output '{"name":"np-releases","format":"DOCKER","dockerConfig":{"immutableTags":true}}'
    exit 0
}
if ($joined -match "artifacts repositories get-iam-policy np-releases") {
    Write-Output $jsonPolicy
    exit 0
}
if ($joined -match "artifacts docker images describe") {
    if ($joined -match "value\(image_summary\.digest\)") {
        Write-Output "sha256:1111111111111111111111111111111111111111111111111111111111111111"
    }
    else {
        Write-Output '{"image_summary":{"digest":"sha256:1111111111111111111111111111111111111111111111111111111111111111"}}'
    }
    exit 0
}
if ($joined -match "storage buckets describe") {
    Write-Output '{"name":"bucket","iamConfiguration":{"uniformBucketLevelAccess":{"enabled":true},"publicAccessPrevention":"enforced"}}'
    exit 0
}
if ($joined -match "storage buckets get-iam-policy") {
    Write-Output $jsonPolicy
    exit 0
}
if ($joined -match "storage buckets add-iam-policy-binding") {
    Write-Output "bucket policy updated"
    exit 0
}
if ($joined -match "storage cp") {
    $destination = $args[-1]
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    if ($destination -like "*operator-mirror-overall.txt") { "0" | Set-Content -LiteralPath $destination -Encoding utf8 }
    elseif ($destination -like "*operator-mirror-status.tsv") { "" | Set-Content -LiteralPath $destination -Encoding utf8 }
    else { "OPERATOR_MIRROR_BUILD_SUCCEEDED" | Set-Content -LiteralPath $destination -Encoding utf8 }
    exit 0
}
if ($joined -match "iam service-accounts describe" -or $joined -match "iam service-accounts get-iam-policy") {
    Write-Output "{}"
    exit 0
}
if ($joined -match "projects get-iam-policy") {
    Write-Output $jsonPolicy
    exit 0
}
if ($joined -match "builds submit") {
    Write-Output "fake-build-1"
    exit 0
}
if ($joined -match "builds describe fake-build-1") {
    Write-Output '{"status":"SUCCESS"}'
    exit 0
}
if ($joined -match "container binauthz policy export") {
    Write-Output "defaultAdmissionRule: {}"
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
if ($joined -match "auth can-i") {
    Write-Output "yes"
    exit 0
}
if ($joined -match "get storageclass") {
    Write-Output '{"items":[{"metadata":{"name":"standard-rwo","annotations":{"storageclass.kubernetes.io/is-default-class":"true"}}}]}'
    exit 0
}
if ($joined -match "get apiservice v1beta1.external.metrics.k8s.io") {
    Write-Output '{"status":{"conditions":[{"type":"Available","status":"True"}]}}'
    exit 0
}
if ($joined -match "get deployment cert-manager cert-manager-cainjector") {
    Write-Output '{"items":[{"metadata":{"name":"cert-manager"},"spec":{"template":{"spec":{"containers":[{"name":"cert-manager","args":["--leader-election-namespace=cert-manager"]}]}}}},{"metadata":{"name":"cert-manager-cainjector"},"spec":{"template":{"spec":{"containers":[{"name":"cert-manager-cainjector","args":["--leader-election-namespace=cert-manager"]}]}}}}]}'
    exit 0
}
if ($joined -match "get deployment .* -o json") {
    Write-Output '{"metadata":{"namespace":"test","name":"deployment","generation":1},"spec":{"replicas":1,"selector":{"matchLabels":{"app":"x"}}},"status":{"readyReplicas":1,"availableReplicas":1,"updatedReplicas":1,"observedGeneration":1}}'
    exit 0
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
if ($joined -match "get pod -o name") {
    exit 0
}
if ($joined -match "get namespace default") {
    Write-Output '{"metadata":{"name":"default"}}'
    exit 0
}
if ($joined -match "get secret webhook-server-cert") {
    Write-Output '{"metadata":{"name":"webhook-server-cert"}}'
    exit 0
}
if ($joined -match "wait --for=condition=Established" -or $joined -match "apply" -or $joined -match "delete" -or $joined -match "api-resources") {
    Write-Output "ok"
    exit 0
}
if ($joined -match "get deployments,statefulsets,replicasets,pods,services,endpoints,events,leases" -or $joined -match "get deployments,replicasets,pods,services,endpoints,events" -or $joined -match "get apiservices" -or $joined -match "get crd") {
    Write-Output '{"items":[]}'
    exit 0
}
if ($joined -match "describe") {
    Write-Output "fake describe"
    exit 0
}
if ($joined -match "logs") {
    Write-Output "fake logs"
    exit 0
}
Write-Output "fake kubectl $joined"
exit 0
'@ | Set-Content -LiteralPath $fakeKubectl -Encoding utf8

$fakeGh = Join-Path $tempRoot "fake-gh.ps1"
@'
function Get-ManifestContent {
    param([Parameter(Mandatory)][string]$Asset)
    $docs = switch ($Asset) {
        "cert-manager.yaml" {
            @(
                "kind: Role`nmetadata:`n  name: cert-manager:leaderelection`n  namespace: kube-system",
                "kind: RoleBinding`nmetadata:`n  name: cert-manager:leaderelection`n  namespace: kube-system",
                "kind: Role`nmetadata:`n  name: cert-manager-cainjector:leaderelection`n  namespace: kube-system",
                "kind: RoleBinding`nmetadata:`n  name: cert-manager-cainjector:leaderelection`n  namespace: kube-system",
                "kind: Deployment`nmetadata:`n  name: cert-manager`nspec:`n  template:`n    spec:`n      containers:`n      - name: cert-manager`n        image: quay.io/jetstack/cert-manager-controller:v1.20.2`n        args:`n        - --leader-election-namespace=kube-system`n        - --acme-http01-solver-image=quay.io/jetstack/cert-manager-acmesolver:v1.20.2",
                "kind: Deployment`nmetadata:`n  name: cert-manager-cainjector`nspec:`n  template:`n    spec:`n      containers:`n      - name: cert-manager-cainjector`n        image: quay.io/jetstack/cert-manager-cainjector:v1.20.2`n        args:`n        - --leader-election-namespace=kube-system",
                "kind: Deployment`nmetadata:`n  name: cert-manager-webhook`nspec:`n  template:`n    spec:`n      containers:`n      - name: cert-manager-webhook`n        image: quay.io/jetstack/cert-manager-webhook:v1.20.2"
            )
        }
        "cluster-operator.yml" {
            @("kind: Deployment`nmetadata:`n  name: rabbitmq-cluster-operator`nspec:`n  template:`n    spec:`n      containers:`n      - name: rabbitmq-cluster-operator`n        image: docker.io/rabbitmqoperator/cluster-operator:2.17.2")
        }
        "messaging-topology-operator-with-certmanager.yaml" {
            @("kind: Deployment`nmetadata:`n  name: messaging-topology-operator`nspec:`n  template:`n    spec:`n      containers:`n      - name: messaging-topology-operator`n        image: docker.io/rabbitmqoperator/messaging-topology-operator:1.19.0")
        }
        "keda-2.18.2.yaml" {
            @(
                "kind: Deployment`nmetadata:`n  name: keda-operator`nspec:`n  template:`n    spec:`n      containers:`n      - name: keda-operator`n        image: ghcr.io/kedacore/keda:2.18.2",
                "kind: Deployment`nmetadata:`n  name: keda-metrics-apiserver`nspec:`n  template:`n    spec:`n      containers:`n      - name: keda-metrics-apiserver`n        image: ghcr.io/kedacore/keda-metrics-apiserver:2.18.2",
                "kind: Deployment`nmetadata:`n  name: keda-admission`nspec:`n  template:`n    spec:`n      containers:`n      - name: keda-admission`n        image: ghcr.io/kedacore/keda-admission-webhooks:2.18.2"
            )
        }
        default { throw "Unknown fake asset $Asset" }
    }
    return ($docs -join "`n---`n") + "`n"
}
function Get-AssetSha256 {
    param([Parameter(Mandatory)][string]$Asset)
    $bytes = [Text.Encoding]::UTF8.GetBytes((Get-ManifestContent -Asset $Asset))
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "")
    }
    finally {
        $sha.Dispose()
    }
}
$joined = $args -join " "
$assets = @("cert-manager.yaml", "cluster-operator.yml", "messaging-topology-operator-with-certmanager.yaml", "keda-2.18.2.yaml")
if ($args.Count -ge 2 -and $args[0] -eq "api") {
    $assetObjects = @()
    $id = 1
    foreach ($asset in $assets) {
        $assetObjects += [ordered]@{ id = $id; name = $asset; digest = "sha256:$(Get-AssetSha256 -Asset $asset)" }
        $id++
    }
    [ordered]@{ id = 100; published_at = "2026-07-03T00:00:00Z"; assets = $assetObjects } |
        ConvertTo-Json -Depth 6
    exit 0
}
if ($args.Count -ge 3 -and $args[0] -eq "release" -and $args[1] -eq "download") {
    $pattern = $null
    $dir = $null
    for ($i = 0; $i -lt $args.Count; $i++) {
        if ($args[$i] -eq "--pattern") { $pattern = $args[$i + 1] }
        if ($args[$i] -eq "--dir") { $dir = $args[$i + 1] }
    }
    if ([string]::IsNullOrWhiteSpace($pattern) -or [string]::IsNullOrWhiteSpace($dir)) {
        Write-Error "Fake gh release download missing pattern or dir: $joined"
        exit 2
    }
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    [IO.File]::WriteAllText(
        (Join-Path $dir $pattern),
        (Get-ManifestContent -Asset $pattern),
        [Text.UTF8Encoding]::new($false)
    )
    exit 0
}
Write-Output "{}"
exit 0
'@ | Set-Content -LiteralPath $fakeGh -Encoding utf8

New-CmdShim -Name "gcloud" -Target $fakeGcloud
New-CmdShim -Name "bash" -Target $fakeBash
New-CmdShim -Name "kubectl" -Target $fakeKubectl
New-CmdShim -Name "gh" -Target $fakeGh

function Set-TestPath {
    $env:PATH = @(
        $bin
        $script:OriginalPath
    ) -join [System.IO.Path]::PathSeparator
}

function Invoke-WrapperInstaller {
    param(
        [int]$BashExit = 0,
        [string]$PodFailure = "",
        [int]$TimeoutSeconds = 1800,
        [bool]$FoundationReady = $false
    )

    Set-TestPath
    foreach ($command in @("gcloud", "kubectl", "gh", "bash")) {
        Assert-CommandResolvesToShim -Name $command -ShimDirectory $bin
    }
    $env:NP_FAKE_BASH_EXIT = [string]$BashExit
    $env:NP_FAKE_POD_FAILURE = $PodFailure
    $env:NP_FAKE_CLUSTER_MODE = "autopilot"
    $env:NP_FAKE_FOUNDATION_READY = if ($FoundationReady) { "true" } else { "false" }
    $env:NP_FAKE_BASH_RECORD = Join-Path $tempRoot "bash-record-$([Guid]::NewGuid().ToString('N')).json"
    Remove-Item -LiteralPath $env:NP_FAKE_BASH_RECORD -Force -ErrorAction SilentlyContinue

    $output = & pwsh -NoProfile -File $wrapperPath `
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

function Invoke-RealBashInstaller {
    param([Parameter(Mandatory)][string]$BashPath)

    Set-TestPath
    foreach ($command in @("gcloud", "kubectl", "gh")) {
        Assert-CommandResolvesToShim -Name $command -ShimDirectory $bin
    }

    $env:NP_FAKE_CLUSTER_MODE = "autopilot"
    $env:NP_FAKE_FOUNDATION_READY = "false"
    $env:NP_FAKE_POD_FAILURE = ""
    $env:NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS = "300"
    $realEvidence = Join-Path $tempRoot "real-bash-evidence"
    New-Item -ItemType Directory -Force -Path $realEvidence | Out-Null

    $output = & $BashPath (ConvertTo-BashPath -Path $bashInstallerPath) `
        natureprotector-500518 `
        europe-southwest1 `
        np-staging `
        (ConvertTo-BashPath -Path $lockPath) `
        (ConvertTo-BashPath -Path $realEvidence) 2>&1

    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output | ForEach-Object { [string]$_ }) -join "`n"
        Evidence = $realEvidence
    }
}

$script:OriginalPath = $env:PATH
try {
    $success = Invoke-WrapperInstaller -TimeoutSeconds 1200
    if ($success.ExitCode -ne 0 -or $success.Output -notmatch "CLUSTER_DEPENDENCY_STATUS=READY") {
        throw "Autopilot wrapper success path failed: $($success.Output)"
    }
    $record = Get-Content -Raw -LiteralPath $success.RecordPath | ConvertFrom-Json
    if ($record.timeout -ne "1200") { throw "Rollout timeout was not propagated to bash installer." }
    if (($record.args -join " ") -match "production") { throw "Autopilot installer test referenced production." }

    $alreadyReady = Invoke-WrapperInstaller -FoundationReady $true
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

    $imagePull = Invoke-WrapperInstaller -BashExit 42 -PodFailure "image-pull"
    if ($imagePull.ExitCode -eq 0 -or $imagePull.Output -notmatch "CLUSTER_DEPENDENCY_FAILURE_CLASS=IMAGE_PULL") {
        throw "Expected IMAGE_PULL classification: $($imagePull.Output)"
    }
    if ($imagePull.Output -notmatch "CLUSTER_DEPENDENCY_DIAGNOSTICS_BEGIN" -or $imagePull.Output -notmatch "CLUSTER_DEPENDENCY_DIAGNOSTICS_END") {
        throw "Expected diagnostics markers on failure."
    }

    $crash = Invoke-WrapperInstaller -BashExit 42 -PodFailure "crash"
    if ($crash.Output -notmatch "CLUSTER_DEPENDENCY_FAILURE_CLASS=CONTAINER_CRASH") {
        throw "Expected CONTAINER_CRASH classification: $($crash.Output)"
    }

    $quota = Invoke-WrapperInstaller -BashExit 42 -PodFailure "quota"
    if ($quota.Output -notmatch "CLUSTER_DEPENDENCY_FAILURE_CLASS=QUOTA") {
        throw "Expected QUOTA classification: $($quota.Output)"
    }

    $realBash = Get-RealBash
    if (-not $realBash) {
        throw "Real bash is required for Autopilot bootstrap validation; WSL stub bash is not sufficient."
    }
    $real = Invoke-RealBashInstaller -BashPath $realBash
    if ($real.ExitCode -ne 0) {
        throw "Real Bash Autopilot bootstrap test failed: $($real.Output)"
    }
    foreach ($marker in @(
        "PYTHON_RUNTIME=",
        "PYYAML_IMPORT=PASS",
        "FRESH_LINUX_AMD64_OPERATOR_MIRROR_PROVED",
        "KEDA_EXPLICIT_RESOURCE_REQUESTS_CONFIRMED",
        "OPERATOR_FOUNDATION_PROVED",
        "CLUSTER_DEPENDENCY_STATUS=READY"
    )) {
        if ($real.Output -notmatch [regex]::Escape($marker)) {
            throw "Real Bash output did not contain required marker '$marker': $($real.Output)"
        }
    }
    $patchedKeda = Join-Path $real.Evidence "patched/keda-2.18.2.yaml"
    $patchedCertManager = Join-Path $real.Evidence "patched/cert-manager.yaml"
    $patchedText = (Get-Content -Raw -LiteralPath $patchedKeda) + "`n" + (Get-Content -Raw -LiteralPath $patchedCertManager)
    if ($patchedText -match "type:\s*Recreate") { throw "Autopilot patch must not force Recreate strategy globally." }
    if ($patchedText -match "kubernetes.io/arch") { throw "Autopilot patch must not force node architecture globally." }
    if ((Get-Content -Raw -LiteralPath $patchedKeda) -notmatch "cpu:\s*100m") {
        throw "KEDA bootstrap request patch was not applied."
    }
    if ((Get-Content -Raw -LiteralPath $patchedCertManager) -match "cpu:\s*100m") {
        throw "KEDA bootstrap request patch leaked into cert-manager."
    }

    Write-Host "WINDOWS_AUTOPILOT_TEST=PASS"
    Write-Host "REAL_BASH_TEST=PASS"
    Write-Host "PYTHON_RESOLUTION_TEST=PASS"
    Write-Host "PYYAML_TEST=PASS"
    Write-Host "G81_CLUSTER_DEPENDENCY_AUTOPILOT_BOOTSTRAP_TEST=PASS"
}
finally {
    $env:PATH = $script:OriginalPath
    Remove-Item Env:NP_FAKE_BASH_EXIT -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_POD_FAILURE -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_CLUSTER_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_FOUNDATION_READY -ErrorAction SilentlyContinue
    Remove-Item Env:NP_FAKE_BASH_RECORD -ErrorAction SilentlyContinue
    Remove-Item Env:NP_CLUSTER_DEPENDENCY_ROLLOUT_TIMEOUT_SECONDS -ErrorAction SilentlyContinue
    if ($env:NP_PRESERVE_AUTOPILOT_TEST_TEMP -ne "1") {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "AUTOPILOT_TEST_TEMP=$tempRoot"
    }
}
