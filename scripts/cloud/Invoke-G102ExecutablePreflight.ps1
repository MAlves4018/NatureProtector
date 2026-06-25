[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [string]$EvidenceDirectory = (Join-Path (Get-Location) "artifacts/g10-2-preflight"),
    [switch]$RequireAllTools,
    [switch]$SkipFrontend,
    [switch]$SkipDotNet,
    [switch]$SkipTerraform,
    [switch]$SkipKustomize,
    [switch]$SkipCloudReadOnlyChecks,
    [string]$PythonExecutable = $env:NATUREPROTECTOR_VALIDATION_PYTHON
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$InputPath = (Resolve-Path $InputPath).Path
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$EvidenceDirectory = (Resolve-Path $EvidenceDirectory).Path

$results = [ordered]@{
    schema_version = 1
    phase = "G10.2"
    mode = "PREFLIGHT_READ_ONLY"
    started_at = (Get-Date).ToUniversalTime().ToString("o")
    repository_root = $RepositoryRoot
    input_path = $InputPath
    checks = @()
    blockers = @()
    cloud_mutations = $false
    data_plane_created = $false
}

function Add-Result {
    param([string]$Name, [string]$Status, [string]$Detail = "")
    $script:results.checks += [ordered]@{ name = $Name; status = $Status; detail = $Detail }
    if ($Status -eq "BLOCKED" -or $Status -eq "FAIL") { $script:results.blockers += $Name }
}

function Test-Tool {
    param([string]$Name)
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        Add-Result -Name "tool:$Name" -Status "BLOCKED" -Detail "command not found"
        if ($RequireAllTools) { throw "Required tool is missing: $Name" }
        return $false
    }
    Add-Result -Name "tool:$Name" -Status "PASS" -Detail $command.Source
    return $true
}

function Invoke-Checked {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) { throw "exit code $LASTEXITCODE" }
        Add-Result -Name $Name -Status "PASS"
    } catch {
        Add-Result -Name $Name -Status "FAIL" -Detail $_.Exception.Message
    }
}

function Resolve-ValidationPython {
    param([AllowEmptyString()][string]$RequestedPython)

    if ([string]::IsNullOrWhiteSpace($RequestedPython)) {
        $command = Get-Command python -ErrorAction SilentlyContinue
        if ($null -eq $command -or $command.Source -match "\\msys64\\") {
            throw "A compatible validation Python was not provided and no non-MSYS2 python was found."
        }
        $RequestedPython = $command.Source
    }
    if (-not (Test-Path -LiteralPath $RequestedPython -PathType Leaf)) { throw "PythonExecutable does not exist: $RequestedPython" }
    $resolved = (Resolve-Path -LiteralPath $RequestedPython).Path
    if ($resolved -match "\\msys64\\") { throw "MSYS2 Python is not accepted for validation: $resolved" }
    $probe = & $resolved -c "import json, sys; import jsonschema, yaml, hcl2; print(json.dumps({'python_executable': sys.executable, 'python_version': sys.version, 'python_platform': sys.platform, 'jsonschema_available': True, 'yaml_available': True, 'hcl2_available': True}))" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($probe)) { throw "Validation Python imports failed for: $resolved" }
    $runtime = $probe | ConvertFrom-Json
    if ($runtime.python_platform -ne "win32") { throw "PythonExecutable must report sys.platform=win32; got '$($runtime.python_platform)'." }
    $runtime | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "python-runtime.json") -Encoding utf8
    Add-Result -Name "tool:python" -Status "PASS" -Detail $resolved
    return $resolved
}

Push-Location $RepositoryRoot
try {
    $ResolvedPythonExecutable = Resolve-ValidationPython -RequestedPython $PythonExecutable
    Invoke-Checked "bootstrap-input-contract" { & $ResolvedPythonExecutable scripts/cloud/Test-G102BootstrapInput.py --input $InputPath --output (Join-Path $EvidenceDirectory "bootstrap-input-result.json") }
    Invoke-Checked "g102-static-policy" { & $ResolvedPythonExecutable scripts/cloud/Test-G102Static.py | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "g102-static.json") -Encoding utf8 }
    Invoke-Checked "g103-static-policy" { & $ResolvedPythonExecutable scripts/cloud/Test-G103Static.py | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "g103-static.json") -Encoding utf8 }
    Invoke-Checked "local-cloud-configuration-contract" { & $ResolvedPythonExecutable scripts/cloud/Test-LocalCloudConfigurationContract.py | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "local-cloud-contract.json") -Encoding utf8 }
    Invoke-Checked "g81-static-policy" { & $ResolvedPythonExecutable scripts/cloud/Test-G81Static.py | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "g81-static.json") -Encoding utf8 }
    Invoke-Checked "g9-convergence-policy" { & $ResolvedPythonExecutable scripts/cloud/Test-G9Convergence.py | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "g9-convergence.json") -Encoding utf8 }

    if (-not $SkipDotNet) {
        if (Test-Tool "dotnet") {
            Invoke-Checked "dotnet-sdk" { dotnet --version | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "dotnet-version.txt") -Encoding utf8 }
            Invoke-Checked "dotnet-restore" { dotnet restore NatureProtector.sln }
            Invoke-Checked "dotnet-build" { dotnet build NatureProtector.sln -c Release --no-restore }
            Invoke-Checked "dotnet-test" { dotnet test NatureProtector.sln -c Release --no-build --logger "trx;LogFileName=g10-2-preflight.trx" }
        }
    }

    if (-not $SkipFrontend) {
        $nodeReady = Test-Tool "node"
        $npmReady = Test-Tool "npm"
        if ($nodeReady -and $npmReady) {
            Push-Location webUI
            try {
                Invoke-Checked "frontend-toolchain" { npm run check:toolchain }
                Invoke-Checked "frontend-clean-install" { npm ci }
                Invoke-Checked "frontend-typecheck" { npm run typecheck }
                Invoke-Checked "frontend-lint" { npm run lint }
                Invoke-Checked "frontend-test" { npm test }
                Invoke-Checked "frontend-build" { npm run build }
            } finally { Pop-Location }
        }
    }

    if (-not $SkipTerraform) {
        if (Test-Tool "terraform") {
            foreach ($root in @("infra/gcp/terraform/g8-1-state-bootstrap", "infra/gcp/terraform/g8-1-platform", "infra/gcp/terraform/g8-1-environment")) {
                Invoke-Checked "terraform-fmt:$root" { terraform "-chdir=$root" fmt -check -recursive }
                Invoke-Checked "terraform-init:$root" { terraform "-chdir=$root" init -backend=false -input=false }
                Invoke-Checked "terraform-validate:$root" { terraform "-chdir=$root" validate }
            }
        }
    }

    if (-not $SkipKustomize) {
        if (Test-Tool "kustomize") {
            foreach ($overlay in @("staging", "production")) {
                $output = Join-Path $EvidenceDirectory "kustomize-$overlay.yaml"
                Invoke-Checked "kustomize-build:$overlay" { kustomize build "infra/gcp/kubernetes/g8-1/overlays/$overlay" | Set-Content -LiteralPath $output -Encoding utf8 }
            }
        }
    }

    if (-not $SkipCloudReadOnlyChecks) {
        $ghReady = Test-Tool "gh"
        $gcloudReady = Test-Tool "gcloud"
        $ownerInput = Get-Content -Raw -LiteralPath $InputPath | ConvertFrom-Json
        if ($ghReady) {
            Invoke-Checked "github-repository-metadata" {
                gh api "repos/$($ownerInput.repository)" --jq '{repository:.full_name,repository_id:(.id|tostring),owner_login:.owner.login,owner_id:(.owner.id|tostring),default_branch:.default_branch,visibility:.visibility}' |
                    Set-Content -LiteralPath (Join-Path $EvidenceDirectory "github-repository.json") -Encoding utf8
            }
        }
        if ($gcloudReady) {
            Invoke-Checked "gcloud-active-account" {
                gcloud auth list --filter="status:ACTIVE" --format=json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "gcloud-active-account.json") -Encoding utf8
            }
            Invoke-Checked "gcloud-billing-visible" {
                gcloud billing accounts list --filter="name:billingAccounts/$($ownerInput.billing_account_id)" --format=json |
                    Set-Content -LiteralPath (Join-Path $EvidenceDirectory "gcloud-billing.json") -Encoding utf8
            }
            Invoke-Checked "gcloud-project-inventory" {
                gcloud projects list --format=json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "gcloud-projects.json") -Encoding utf8
            }
        }
    }
} finally {
    Pop-Location
    $results.completed_at = (Get-Date).ToUniversalTime().ToString("o")
    $results.status = if ($results.blockers.Count -eq 0) { "PASS" } else { "BLOCKED" }
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "preflight-summary.json") -Encoding utf8
    Write-Host "G10.2 preflight: $($results.status)"
    Write-Host "Evidence: $EvidenceDirectory"
}

if ($results.blockers.Count -ne 0) { exit 2 }
