[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [string]$EvidenceDirectory = (Join-Path (Get-Location) "artifacts/g10-3-inventory"),
    [string]$PythonExecutable = $env:NATUREPROTECTOR_VALIDATION_PYTHON
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

foreach ($tool in @("gcloud")) {
    if ($null -eq (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Required tool not found: $tool" }
}
$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$InputPath = (Resolve-Path $InputPath).Path
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$EvidenceDirectory = (Resolve-Path $EvidenceDirectory).Path

$summary = [ordered]@{
    schema_version = 1
    phase = "G10.3_CLOUD_INVENTORY"
    mode = "READ_ONLY"
    started_at = (Get-Date).ToUniversalTime().ToString("o")
    input_path = $InputPath
    projects = @()
    checks = @()
    mutations = $false
    data_plane_created_by_script = $false
    status = "RUNNING"
}

function Invoke-InventoryCommand {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$OutputFile
    )
    try {
        $raw = & gcloud @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $text = ($raw -join [Environment]::NewLine)
        $text | Set-Content -LiteralPath $OutputFile -Encoding utf8
        if ($exitCode -ne 0) {
            $script:summary.checks += [ordered]@{ name = $Name; status = "BLOCKED"; detail = $text.Trim() }
            return [ordered]@{ status = "BLOCKED"; count = $null }
        }
        $count = 0
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            try {
                $parsed = $text | ConvertFrom-Json
                $count = @($parsed).Count
            } catch {
                $count = $null
            }
        }
        $script:summary.checks += [ordered]@{ name = $Name; status = "PASS"; detail = "count=$count" }
        return [ordered]@{ status = "PASS"; count = $count }
    } catch {
        $_.Exception.Message | Set-Content -LiteralPath $OutputFile -Encoding utf8
        $script:summary.checks += [ordered]@{ name = $Name; status = "BLOCKED"; detail = $_.Exception.Message }
        return [ordered]@{ status = "BLOCKED"; count = $null }
    }
}

function Resolve-ValidationPython {
    param([AllowEmptyString()][string]$RequestedPython)
    if ([string]::IsNullOrWhiteSpace($RequestedPython)) { $RequestedPython = $env:NATUREPROTECTOR_VALIDATION_PYTHON }
    if ([string]::IsNullOrWhiteSpace($RequestedPython)) { throw "Pass -PythonExecutable or set NATUREPROTECTOR_VALIDATION_PYTHON." }
    if (-not (Test-Path -LiteralPath $RequestedPython -PathType Leaf)) { throw "PythonExecutable does not exist: $RequestedPython" }
    $resolved = (Resolve-Path -LiteralPath $RequestedPython).Path
    if ($resolved -match "\\msys64\\") { throw "MSYS2 Python is not accepted for validation: $resolved" }
    & $resolved -c "import jsonschema, yaml, hcl2" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Validation Python imports failed for: $resolved" }
    return $resolved
}

Push-Location $RepositoryRoot
try {
    $ResolvedPythonExecutable = Resolve-ValidationPython -RequestedPython $PythonExecutable
    & $ResolvedPythonExecutable scripts/cloud/Test-G102BootstrapInput.py --input $InputPath --output (Join-Path $EvidenceDirectory "bootstrap-input-result.json")
    if ($LASTEXITCODE -ne 0) { throw "Bootstrap input validation failed." }
    $ownerInput = Get-Content -Raw -LiteralPath $InputPath | ConvertFrom-Json

    $active = (gcloud auth list --filter="status:ACTIVE" --format="value(account)").Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($active)) { throw "Unable to resolve active gcloud account." }
    if ($active -ne $ownerInput.expected_gcloud_account) { throw "Active gcloud account '$active' does not match expected '$($ownerInput.expected_gcloud_account)'." }
    $summary.active_account = $active
    $summary.billing_account_id = $ownerInput.billing_account_id

    Invoke-InventoryCommand -Name "billing-account" -Arguments @("billing", "accounts", "describe", $ownerInput.billing_account_id, "--format=json") -OutputFile (Join-Path $EvidenceDirectory "billing-account.json") | Out-Null
    Invoke-InventoryCommand -Name "billing-budgets" -Arguments @("billing", "budgets", "list", "--billing-account=$($ownerInput.billing_account_id)", "--format=json") -OutputFile (Join-Path $EvidenceDirectory "billing-budgets.json") | Out-Null

    $projects = @(
        [ordered]@{ role = "platform"; id = [string]$ownerInput.platform_project_id },
        [ordered]@{ role = "staging"; id = [string]$ownerInput.staging_project_id },
        [ordered]@{ role = "production"; id = [string]$ownerInput.production_project_id }
    )
    foreach ($project in $projects) {
        $role = $project.role
        $projectId = $project.id
        $projectDirectory = Join-Path $EvidenceDirectory $role
        New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
        $resourceChecks = [ordered]@{}
        $resourceChecks.project = Invoke-InventoryCommand -Name "${role}:project" -Arguments @("projects", "describe", $projectId, "--format=json") -OutputFile (Join-Path $projectDirectory "project.json")
        $resourceChecks.billing = Invoke-InventoryCommand -Name "${role}:billing" -Arguments @("billing", "projects", "describe", $projectId, "--format=json") -OutputFile (Join-Path $projectDirectory "billing.json")
        $resourceChecks.services = Invoke-InventoryCommand -Name "${role}:enabled-services" -Arguments @("services", "list", "--enabled", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "enabled-services.json")
        $resourceChecks.compute_instances = Invoke-InventoryCommand -Name "${role}:compute-instances" -Arguments @("compute", "instances", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "compute-instances.json")
        $resourceChecks.managed_instance_groups = Invoke-InventoryCommand -Name "${role}:managed-instance-groups" -Arguments @("compute", "instance-groups", "managed", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "managed-instance-groups.json")
        $resourceChecks.cloud_run_services = Invoke-InventoryCommand -Name "${role}:cloud-run-services" -Arguments @("run", "services", "list", "--platform=managed", "--region=$($ownerInput.primary_region)", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "cloud-run-services.json")
        $resourceChecks.cloud_run_jobs = Invoke-InventoryCommand -Name "${role}:cloud-run-jobs" -Arguments @("run", "jobs", "list", "--region=$($ownerInput.primary_region)", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "cloud-run-jobs.json")
        $resourceChecks.gke_clusters = Invoke-InventoryCommand -Name "${role}:gke-clusters" -Arguments @("container", "clusters", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "gke-clusters.json")
        $resourceChecks.sql_instances = Invoke-InventoryCommand -Name "${role}:sql-instances" -Arguments @("sql", "instances", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "sql-instances.json")
        $resourceChecks.storage_buckets = Invoke-InventoryCommand -Name "${role}:storage-buckets" -Arguments @("storage", "buckets", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "storage-buckets.json")
        $resourceChecks.artifact_repositories = Invoke-InventoryCommand -Name "${role}:artifact-repositories" -Arguments @("artifacts", "repositories", "list", "--project=$projectId", "--location=$($ownerInput.primary_region)", "--format=json") -OutputFile (Join-Path $projectDirectory "artifact-repositories.json")
        $resourceChecks.deploy_pipelines = Invoke-InventoryCommand -Name "${role}:deploy-pipelines" -Arguments @("deploy", "delivery-pipelines", "list", "--project=$projectId", "--region=$($ownerInput.primary_region)", "--format=json") -OutputFile (Join-Path $projectDirectory "deploy-pipelines.json")
        $resourceChecks.pubsub_topics = Invoke-InventoryCommand -Name "${role}:pubsub-topics" -Arguments @("pubsub", "topics", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "pubsub-topics.json")
        $resourceChecks.secrets = Invoke-InventoryCommand -Name "${role}:secrets" -Arguments @("secrets", "list", "--project=$projectId", "--format=json") -OutputFile (Join-Path $projectDirectory "secrets.json")

        $knownResourceCount = 0
        foreach ($key in @("compute_instances", "managed_instance_groups", "cloud_run_services", "cloud_run_jobs", "gke_clusters", "sql_instances", "storage_buckets", "artifact_repositories", "deploy_pipelines", "pubsub_topics", "secrets")) {
            $count = $resourceChecks[$key].count
            if ($null -ne $count) { $knownResourceCount += [int]$count }
        }
        if ($resourceChecks.project.status -ne "PASS" -or $resourceChecks.billing.status -ne "PASS") {
            $summary.checks += [ordered]@{ name = "${role}:core-project-proof"; status = "FAIL"; detail = "Project or billing description could not be proved." }
        }
        $summary.projects += [ordered]@{
            role = $role
            project_id = $projectId
            known_resource_count = $knownResourceCount
            inventory = $resourceChecks
        }
    }

    $failures = @($summary.checks | Where-Object { $_.status -eq "FAIL" })
    $blocked = @($summary.checks | Where-Object { $_.status -eq "BLOCKED" })
    $summary.status = if ($failures.Count -gt 0) { "FAIL" } elseif ($blocked.Count -gt 0) { "PARTIAL" } else { "PASS" }
} catch {
    $summary.status = "FAIL"
    $summary.error = $_.Exception.Message
    throw
} finally {
    Pop-Location
    $summary.completed_at = (Get-Date).ToUniversalTime().ToString("o")
    $summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "cloud-inventory-summary.json") -Encoding utf8
    Write-Host "G10.3 inventory: $($summary.status)"
    Write-Host "Evidence: $EvidenceDirectory"
}
