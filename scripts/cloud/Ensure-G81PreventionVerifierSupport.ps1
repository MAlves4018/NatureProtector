[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$ClusterName,
    [ValidateSet("staging", "production")][string]$Environment = "staging",
    [string]$Namespace = "",
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$AllowProduction
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Environment -eq "production" -and -not $AllowProduction) {
    throw "Production verifier support apply requires -AllowProduction."
}

if ([string]::IsNullOrWhiteSpace($Namespace)) {
    $Namespace = if ($Environment -eq "staging") {
        "natureprotector-staging"
    } else {
        "natureprotector-production"
    }
}

if ($Environment -eq "staging" -and $Namespace -ne "natureprotector-staging") {
    throw "Staging verifier support must target namespace natureprotector-staging."
}

if ($Environment -eq "production" -and $Namespace -ne "natureprotector-production") {
    throw "Production verifier support must target namespace natureprotector-production."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$overlayPath = Join-Path $repositoryRoot "infra/gcp/kubernetes/g8-1/verifier-support/overlays/$Environment"
$fieldManager = "natureprotector-verifier-support-foundation"

if (-not (Test-Path -LiteralPath $overlayPath)) {
    throw "Verifier support overlay was not found: $overlayPath"
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

function Invoke-Captured {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command,
        [switch]$AllowFailure
    )

    $stdoutPath = Join-Path $EvidenceDirectory "$Name.stdout.txt"
    $stderrPath = Join-Path $EvidenceDirectory "$Name.stderr.txt"

    $output = & $Command 2>&1
    $exit = $LASTEXITCODE
    $stdout = @()
    $stderr = @()
    foreach ($line in $output) {
        if ($line -is [System.Management.Automation.ErrorRecord]) {
            $stderr += $line.ToString()
        } else {
            $stdout += [string]$line
        }
    }

    $stdout | Set-Content -LiteralPath $stdoutPath -Encoding utf8
    $stderr | Set-Content -LiteralPath $stderrPath -Encoding utf8

    if ($exit -ne 0 -and -not $AllowFailure) {
        throw "Command failed ($Name) with exit code $exit. See $stdoutPath and $stderrPath."
    }

    return [ordered]@{
        name = $Name
        exit_code = $exit
        stdout_path = $stdoutPath
        stderr_path = $stderrPath
        stdout = ($stdout -join "`n")
        stderr = ($stderr -join "`n")
    }
}

function Assert-CanI {
    param(
        [Parameter(Mandatory)][string]$Verb,
        [Parameter(Mandatory)][string]$Resource
    )

    $result = Invoke-Captured -Name "auth-can-i-$Verb-$($Resource -replace '[^a-zA-Z0-9.-]', '-')" -Command {
        kubectl auth can-i $Verb $Resource -n $Namespace
    }

    if ($result.stdout.Trim() -ne "yes") {
        throw "Current Kubernetes identity cannot '$Verb' '$Resource' in namespace '$Namespace'."
    }
}

function Ensure-Namespace {
    $namespaceExists = Invoke-Captured -Name "kubectl-get-namespace" -Command {
        kubectl get namespace $Namespace -o name
    } -AllowFailure

    if ($namespaceExists.exit_code -ne 0) {
        Invoke-Captured -Name "kubectl-create-namespace" -Command {
            kubectl create namespace $Namespace
        } | Out-Null
    }

    Invoke-Captured -Name "kubectl-label-namespace" -Command {
        kubectl label namespace $Namespace `
            app.kubernetes.io/part-of=natureprotector `
            phase=g8-1 `
            pod-security.kubernetes.io/enforce=restricted `
            pod-security.kubernetes.io/audit=restricted `
            pod-security.kubernetes.io/warn=restricted `
            --overwrite
    } | Out-Null
}

$startedAt = (Get-Date).ToUniversalTime().ToString("o")

$context = Invoke-Captured -Name "kubectl-current-context" -Command {
    kubectl config current-context
}

Ensure-Namespace

foreach ($resource in @(
    "serviceaccounts",
    "roles.rbac.authorization.k8s.io",
    "rolebindings.rbac.authorization.k8s.io",
    "networkpolicies.networking.k8s.io"
)) {
    foreach ($verb in @("get", "create", "patch", "update")) {
        Assert-CanI -Verb $verb -Resource $resource
    }
}

$renderPath = Join-Path $EvidenceDirectory "verifier-support-$Environment.rendered.yaml"
Invoke-Captured -Name "render-verifier-support-$Environment" -Command {
    kubectl kustomize $overlayPath
} | Out-Null
Get-Content -LiteralPath (Join-Path $EvidenceDirectory "render-verifier-support-$Environment.stdout.txt") |
    Set-Content -LiteralPath $renderPath -Encoding utf8

Invoke-Captured -Name "server-dry-run-verifier-support-$Environment" -Command {
    kubectl apply --dry-run=server -f $renderPath --field-manager=$fieldManager -n $Namespace
} | Out-Null

Invoke-Captured -Name "apply-verifier-support-$Environment" -Command {
    kubectl apply -f $renderPath --field-manager=$fieldManager -n $Namespace
} | Out-Null

$objects = @(
    "serviceaccount/natureprotector-deploy-verifier",
    "role/natureprotector-deploy-verifier",
    "rolebinding/natureprotector-deploy-verifier",
    "networkpolicy/natureprotector-deploy-verifier"
)

Invoke-Captured -Name "get-verifier-support-$Environment-yaml" -Command {
    kubectl -n $Namespace get $objects -o yaml
} | Out-Null

$jsonPath = Join-Path $EvidenceDirectory "verifier-support-$Environment.live.json"
Invoke-Captured -Name "get-verifier-support-$Environment-json" -Command {
    kubectl -n $Namespace get $objects -o json
} | Out-Null
Get-Content -LiteralPath (Join-Path $EvidenceDirectory "get-verifier-support-$Environment-json.stdout.txt") |
    Set-Content -LiteralPath $jsonPath -Encoding utf8

$live = Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json
$items = @($live.items)

foreach ($kind in @("ServiceAccount", "Role", "RoleBinding", "NetworkPolicy")) {
    $item = $items | Where-Object {
        $_.kind -eq $kind -and
        $_.metadata.name -eq "natureprotector-deploy-verifier" -and
        $_.metadata.namespace -eq $Namespace
    } | Select-Object -First 1

    if ($null -eq $item) {
        throw "Verifier support object is missing after apply: $kind/$Namespace/natureprotector-deploy-verifier"
    }
}

$roleBinding = $items | Where-Object { $_.kind -eq "RoleBinding" } | Select-Object -First 1
$subject = @($roleBinding.subjects) | Where-Object {
    $_.kind -eq "ServiceAccount" -and
    $_.name -eq "natureprotector-deploy-verifier"
} | Select-Object -First 1

if ($null -eq $subject -or $subject.namespace -ne $Namespace) {
    throw "Verifier RoleBinding subject must reference service account natureprotector-deploy-verifier in namespace $Namespace."
}

if ($roleBinding.roleRef.kind -ne "Role" -or
    $roleBinding.roleRef.name -ne "natureprotector-deploy-verifier" -or
    $roleBinding.roleRef.apiGroup -ne "rbac.authorization.k8s.io") {
    throw "Verifier RoleBinding roleRef is not bound to the expected Role."
}

$summary = [ordered]@{
    schema_version = 1
    status = "PASS"
    project_id = $ProjectId
    region = $Region
    cluster_name = $ClusterName
    environment = $Environment
    namespace = $Namespace
    kube_context = $context.stdout.Trim()
    overlay_path = $overlayPath
    field_manager = $fieldManager
    support_objects = $objects
    started_at = $startedAt
    completed_at = (Get-Date).ToUniversalTime().ToString("o")
    cloud_mutation = $false
    kubernetes_mutation = $true
    production_applied = ($Environment -eq "production")
}

$summary |
    ConvertTo-Json -Depth 10 |
    Set-Content -LiteralPath (Join-Path $EvidenceDirectory "verifier-support-$Environment-summary.json") -Encoding utf8

Write-Host "VERIFIER_SUPPORT_ENSURED"
Write-Host "environment=$Environment"
Write-Host "namespace=$Namespace"
Write-Host "evidence=$EvidenceDirectory"
