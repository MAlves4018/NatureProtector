[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidateSet("staging", "production")][string]$Environment,
    [Parameter(Mandatory)][string]$ProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$TerraformRoot,
    [Parameter(Mandatory)][string]$TerraformStateBucket,
    [Parameter(Mandatory)][string]$TerraformStatePrefix,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$Confirmation
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-EvidenceChecksums {
    param([Parameter(Mandatory)][string]$Directory)
    $checksumPath = Join-Path $Directory "checksums.sha256"
    foreach ($line in Get-Content -LiteralPath $checksumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw "Malformed evidence checksum entry: $line" }
        $relative = $Matches[2].Replace('\\','/')
        if ([IO.Path]::IsPathRooted($relative) -or $relative.Split('/') -contains '..') { throw "Unsafe evidence path: $relative" }
        $path = Join-Path $Directory $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Checksummed evidence file is missing: $relative" }
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
        if ($actual -ne $Matches[1]) { throw "Evidence checksum mismatch: $relative" }
    }
}

$expected = "DESTROY_NATUREPROTECTOR_{0}_AFTER_EVIDENCE_EXPORT" -f $Environment.ToUpperInvariant()
if ($Confirmation -ne $expected) { throw "Confirmation must be exactly $expected" }
if ($ProjectId -match "(?i)cn2526|course|student") { throw "CN/course projects are forbidden for G8.1." }
if ($Region -ne "europe-southwest1") { throw "Unexpected primary region: $Region" }
if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) { throw "Evidence directory does not exist." }
if ($TerraformStatePrefix -ne "environments/$Environment/g8-1") { throw "Unexpected Terraform state prefix." }
if ([string]::IsNullOrWhiteSpace($TerraformStateBucket)) { throw "Terraform state bucket is required." }

$canonicalRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../infra/gcp/terraform/g8-1-environment")).Path
$requestedRoot = (Resolve-Path $TerraformRoot).Path
if ($requestedRoot -ne $canonicalRoot) { throw "Teardown must use the canonical G8.1 environment Terraform root." }

$requiredEvidence = @(
    "release-manifest.json",
    "rollouts.json",
    "runtime-summary.json",
    "terraform.auto.tfvars.json",
    "checksums.sha256"
)
foreach ($file in $requiredEvidence) {
    if (-not (Test-Path -LiteralPath (Join-Path $EvidenceDirectory $file) -PathType Leaf)) { throw "Missing required evidence file: $file" }
}
Test-EvidenceChecksums -Directory $EvidenceDirectory

$tfvarsPath = Join-Path $EvidenceDirectory "terraform.auto.tfvars.json"
$tfvars = Get-Content -Raw -LiteralPath $tfvarsPath | ConvertFrom-Json
if ($tfvars.project_id -ne $ProjectId -or $tfvars.environment -ne $Environment) {
    throw "Terraform evidence configuration does not match the requested project/environment."
}

$receipt = [ordered]@{
    schema_version = 1
    environment = $Environment
    project_id = $ProjectId
    region = $Region
    state_bucket = $TerraformStateBucket
    state_prefix = $TerraformStatePrefix
    started_at = (Get-Date).ToUniversalTime().ToString("o")
    evidence_export_verified = $true
    terraform_destroy_executed = $false
    residual_resource_check = "pending"
}
$receiptPath = Join-Path $EvidenceDirectory "teardown-receipt.json"
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receiptPath -Encoding utf8

if ($PSCmdlet.ShouldProcess("$Environment in $ProjectId", "Terraform teardown after evidence export")) {
    terraform -chdir=$canonicalRoot init -reconfigure -input=false `
        -backend-config="bucket=$TerraformStateBucket" `
        -backend-config="prefix=$TerraformStatePrefix"
    if ($LASTEXITCODE -ne 0) { throw "Terraform backend initialization failed." }

    terraform -chdir=$canonicalRoot apply -input=false -auto-approve `
        -var-file=$tfvarsPath `
        -var="deletion_protection=false" `
        -var="create_edge=false" `
        -var="create_data_plane=false"
    if ($LASTEXITCODE -ne 0) { throw "Terraform teardown apply failed." }
    $receipt.terraform_destroy_executed = $true

    $residual = gcloud asset search-all-resources --scope="projects/$ProjectId" --format=json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw "Cloud Asset residual-resource query failed." }
    $managedResidual = @($residual | Where-Object { $_.displayName -like "np-$Environment*" -or $_.name -like "*natureprotector*" })
    $receipt.residual_resource_check = if ($managedResidual.Count -eq 0) { "passed" } else { "failed" }
    $receipt.residual_resources = $managedResidual
    $receipt.completed_at = (Get-Date).ToUniversalTime().ToString("o")
    $receipt | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    if ($managedResidual.Count -ne 0) { throw "Residual NatureProtector resources remain after teardown." }
}
