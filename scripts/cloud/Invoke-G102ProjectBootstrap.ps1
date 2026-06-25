[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$Confirmation,
    [string]$EvidenceDirectory = (Join-Path (Get-Location) "artifacts/g10-2-bootstrap"),
    [string]$PythonExecutable = $env:NATUREPROTECTOR_VALIDATION_PYTHON,
    [switch]$Execute
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expected = "CREATE_EMPTY_NATUREPROTECTOR_PROJECTS_AND_LINK_APPROVED_BILLING"
if ($Confirmation -ne $expected) { throw "Confirmation must be exactly: $expected" }
if (-not $Execute) { throw "No changes were made. Re-run with -Execute after reviewing the generated plan." }
foreach ($tool in @("gcloud")) {
    if ($null -eq (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Required tool not found: $tool" }
}

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$InputPath = (Resolve-Path $InputPath).Path
$previousWhatIfPreference = $WhatIfPreference
try {
    $WhatIfPreference = $false
    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $EvidenceDirectory = (Resolve-Path $EvidenceDirectory).Path
} finally {
    $WhatIfPreference = $previousWhatIfPreference
}

function Write-EvidenceJson {
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string]$Path,
        [int]$Depth = 10
    )
    $previous = $WhatIfPreference
    try {
        $WhatIfPreference = $false
        $Value | ConvertTo-Json -Depth $Depth | Set-Content -LiteralPath $Path -Encoding utf8
    } finally {
        $WhatIfPreference = $previous
    }
}

function Resolve-ValidationPython {
    param([AllowEmptyString()][string]$RequestedPython)

    $candidates = @()
    $explicitRequested = -not [string]::IsNullOrWhiteSpace($RequestedPython)
    if (-not [string]::IsNullOrWhiteSpace($RequestedPython)) {
        if (-not (Test-Path -LiteralPath $RequestedPython -PathType Leaf)) {
            throw "PythonExecutable does not exist: $RequestedPython"
        }
        $resolvedRequest = (Resolve-Path -LiteralPath $RequestedPython).Path
        if ($resolvedRequest -match "\\msys64\\") {
            throw "MSYS2 Python is not accepted for validation: $resolvedRequest"
        }
        $candidates += $resolvedRequest
    } elseif (-not [string]::IsNullOrWhiteSpace($env:NATUREPROTECTOR_VALIDATION_PYTHON)) {
        if (-not (Test-Path -LiteralPath $env:NATUREPROTECTOR_VALIDATION_PYTHON -PathType Leaf)) {
            throw "NATUREPROTECTOR_VALIDATION_PYTHON does not exist: $($env:NATUREPROTECTOR_VALIDATION_PYTHON)"
        }
        $candidates += (Resolve-Path -LiteralPath $env:NATUREPROTECTOR_VALIDATION_PYTHON).Path
    } else {
        $py = Get-Command py -ErrorAction SilentlyContinue
        if ($null -ne $py) {
            foreach ($version in @("-3.12", "-3.11", "-3")) {
                $probe = & py $version -c "import sys; print(sys.executable)" 2>$null
                if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($probe)) {
                    $candidates += $probe.Trim()
                }
            }
        }
        foreach ($command in @(Get-Command python -All -ErrorAction SilentlyContinue)) {
            if ($command.Source -notmatch "\\msys64\\") {
                $candidates += $command.Source
            }
        }
    }

    foreach ($candidate in @($candidates | Select-Object -Unique)) {
        $resolved = (Resolve-Path -LiteralPath $candidate -ErrorAction SilentlyContinue).Path
        if ([string]::IsNullOrWhiteSpace($resolved)) { continue }
        if ($resolved -match "\\msys64\\") { continue }
        $probeJson = & $resolved -c "import json, platform, sys; import jsonschema, yaml, hcl2; print(json.dumps({'python_executable': sys.executable, 'python_version': sys.version, 'python_platform': sys.platform, 'platform': platform.platform(), 'jsonschema_available': True, 'yaml_available': True, 'hcl2_available': True}))" 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($probeJson)) {
            $runtime = $probeJson | ConvertFrom-Json
            if ($runtime.python_platform -ne "win32") {
                throw "PythonExecutable must report sys.platform=win32; got '$($runtime.python_platform)'."
            }
            return [ordered]@{
                executable = $resolved
                runtime = $runtime
            }
        }
        if ($explicitRequested) {
            throw "Validation Python imports failed for: $resolved"
        }
    }
    throw "No compatible Windows CPython runtime with jsonschema, yaml and hcl2 was found. Pass -PythonExecutable explicitly."
}

$summary = [ordered]@{
    schema_version = 1
    phase = "G10.2_PROJECT_BOOTSTRAP"
    mode = if ($WhatIfPreference) { "WHAT_IF" } else { "EXECUTE" }
    started_at = (Get-Date).ToUniversalTime().ToString("o")
    input_path = $InputPath
    active_account = $null
    billing_account_id = $null
    projects = @()
    cloud_mutations_requested = -not $WhatIfPreference
    projects_created = 0
    billing_links_created = 0
    apis_enabled = $false
    state_foundation_created = $false
    data_plane_created = $false
    estimated_persistent_runtime_cost = 0
    python_runtime = $null
    initial_project_visibility = @()
    status = "RUNNING"
}

function Invoke-GcloudJson {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & gcloud @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "gcloud $($Arguments -join ' ') failed with exit code ${exitCode}: $($output -join [Environment]::NewLine)"
    }
    $text = ($output -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

function Test-ExpectedBillingAccount {
    param(
        [Parameter(Mandatory)]$BillingDescription,
        [Parameter(Mandatory)][string]$BillingAccountId
    )
    $expectedName = "billingAccounts/$BillingAccountId"
    return $BillingDescription.billingEnabled -eq $true -and [string]$BillingDescription.billingAccountName -eq $expectedName
}

function Invoke-BillingLink {
    param(
        [Parameter(Mandatory)][string]$ProjectId,
        [Parameter(Mandatory)][string]$BillingAccountId,
        [int]$MaxAttempts = 6,
        [int]$DelaySeconds = 10
    )
    $lastOutput = $null
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $lastOutput = & gcloud billing projects link $ProjectId --billing-account=$BillingAccountId --quiet 2>&1
        if ($LASTEXITCODE -eq 0) { return }
        if ($attempt -lt $MaxAttempts) {
            Write-Warning "Billing link attempt $attempt failed for $ProjectId; retrying after propagation delay."
            Start-Sleep -Seconds $DelaySeconds
        }
    }
    throw "Failed to link billing for $ProjectId after $MaxAttempts attempts: $($lastOutput -join [Environment]::NewLine)"
}

Push-Location $RepositoryRoot
try {
    $pythonInfo = Resolve-ValidationPython -RequestedPython $PythonExecutable
    $ResolvedPythonExecutable = $pythonInfo.executable
    $summary.python_runtime = $pythonInfo.runtime
    Write-EvidenceJson -Value $pythonInfo.runtime -Path (Join-Path $EvidenceDirectory "python-runtime.json") -Depth 8

    & $ResolvedPythonExecutable scripts/cloud/Test-G102BootstrapInput.py --input $InputPath --output (Join-Path $EvidenceDirectory "bootstrap-input-result.json")
    if ($LASTEXITCODE -ne 0) { throw "Bootstrap input validation failed." }

    $ownerInput = Get-Content -Raw -LiteralPath $InputPath | ConvertFrom-Json
    if (-not $ownerInput.execution.create_projects -or -not $ownerInput.execution.link_billing) {
        throw "The owner input must explicitly set execution.create_projects=true and execution.link_billing=true before project bootstrap."
    }

    $active = (gcloud auth list --filter="status:ACTIVE" --format="value(account)").Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($active)) { throw "Unable to resolve the active gcloud account." }
    if ($active -ne $ownerInput.expected_gcloud_account) { throw "Active gcloud account '$active' does not match expected '$($ownerInput.expected_gcloud_account)'." }
    $summary.active_account = $active
    $summary.billing_account_id = $ownerInput.billing_account_id

    $billing = Invoke-GcloudJson -Arguments @("billing", "accounts", "describe", $ownerInput.billing_account_id, "--format=json")
    if ($null -eq $billing -or $billing.open -ne $true) { throw "Billing account '$($ownerInput.billing_account_id)' is not visible and open." }
    Write-EvidenceJson -Value $billing -Path (Join-Path $EvidenceDirectory "billing-account.json")

    $projects = @(
        [ordered]@{ Role = "platform"; Id = [string]$ownerInput.platform_project_id; Name = "NatureProtector Platform" },
        [ordered]@{ Role = "staging"; Id = [string]$ownerInput.staging_project_id; Name = "NatureProtector Staging" },
        [ordered]@{ Role = "production"; Id = [string]$ownerInput.production_project_id; Name = "NatureProtector Production" }
    )

    foreach ($project in $projects) {
        $projectId = [string]$project.Id
        $projectName = [string]$project.Name
        if ($projectId -match "(?i)(cn2526|course|student|emailteste)") { throw "Forbidden project id: $projectId" }

        $visible = @(gcloud projects list --filter="projectId=$projectId" --format="value(projectId)")
        if ($LASTEXITCODE -ne 0) { throw "Unable to query existing projects." }
        $exists = $visible.Count -gt 0
        $summary.initial_project_visibility += [ordered]@{
            role = $project.Role
            project_id = $projectId
            visible_before_mutation = $exists
        }
        $action = if ($exists) { "reuse-visible-project" } else { "create-empty-project" }

        if (-not $exists -and $PSCmdlet.ShouldProcess($projectId, "Create empty isolated GCP project")) {
            & gcloud projects create $projectId --name=$projectName --quiet
            if ($LASTEXITCODE -ne 0) { throw "Failed to create project $projectId." }
            $exists = $true
            $action = "created-empty-project"
            $summary.projects_created += 1
        }

        if ($WhatIfPreference) {
            $summary.projects += [ordered]@{
                role = $project.Role
                project_id = $projectId
                existed_before = $visible.Count -gt 0
                planned_action = $action
                billing_link_planned = $true
                project_number = $null
                lifecycle_state = $null
                billing_enabled = $null
            }
            continue
        }

        if (-not $exists) { throw "Project '$projectId' does not exist after the create step." }
        $billingAction = "UNKNOWN"
        $billingDescription = Invoke-GcloudJson -Arguments @("billing", "projects", "describe", $projectId, "--format=json")
        if ($null -ne $billingDescription -and (Test-ExpectedBillingAccount -BillingDescription $billingDescription -BillingAccountId $ownerInput.billing_account_id)) {
            $billingAction = "NO_OP_ALREADY_COMPLIANT"
        } elseif ($null -ne $billingDescription -and $billingDescription.billingEnabled -eq $true) {
            throw "Project '$projectId' is linked to an unexpected billing account. Automatic relink is not allowed."
        } else {
            if ($PSCmdlet.ShouldProcess($projectId, "Link approved billing account")) {
                Invoke-BillingLink -ProjectId $projectId -BillingAccountId $ownerInput.billing_account_id
                $summary.billing_links_created += 1
                $billingAction = "LINK_BILLING"
            }
            $billingDescription = Invoke-GcloudJson -Arguments @("billing", "projects", "describe", $projectId, "--format=json")
        }

        $projectDescription = Invoke-GcloudJson -Arguments @("projects", "describe", $projectId, "--format=json")
        if ($projectDescription.lifecycleState -ne "ACTIVE") { throw "Project '$projectId' is not ACTIVE." }
        if ($billingDescription.billingEnabled -ne $true) { throw "Billing is not enabled for '$projectId'." }
        if (-not (Test-ExpectedBillingAccount -BillingDescription $billingDescription -BillingAccountId $ownerInput.billing_account_id)) {
            throw "Project '$projectId' is linked to an unexpected billing account."
        }

        $summary.projects += [ordered]@{
            role = $project.Role
            project_id = $projectId
            existed_before = $visible.Count -gt 0
            planned_action = $action
            billing_link_planned = $true
            billing_action = $billingAction
            project_number = [string]$projectDescription.projectNumber
            lifecycle_state = [string]$projectDescription.lifecycleState
            billing_enabled = [bool]$billingDescription.billingEnabled
            billing_account_name = [string]$billingDescription.billingAccountName
        }
        Write-EvidenceJson -Value $projectDescription -Path (Join-Path $EvidenceDirectory "$($project.Role)-project.json")
        Write-EvidenceJson -Value $billingDescription -Path (Join-Path $EvidenceDirectory "$($project.Role)-billing.json")
    }

    $summary.status = if ($WhatIfPreference) { "WHAT_IF_PASS" } else { "PASS" }
    Write-Host "Only empty projects and billing links were handled. No APIs, state bucket, control plane or data plane were created."
} catch {
    $runtimeFailure = $_.Exception.Message -match "PythonExecutable|NATUREPROTECTOR_VALIDATION_PYTHON|Validation Python|No compatible Windows CPython|MSYS2 Python|Bootstrap input validation"
    $summary.status = if ($runtimeFailure -and $summary.projects_created -eq 0 -and $summary.billing_links_created -eq 0) { "BLOCKED_BEFORE_MUTATION" } else { "FAIL" }
    $summary.error = $_.Exception.Message
    throw
} finally {
    Pop-Location
    $summary.completed_at = (Get-Date).ToUniversalTime().ToString("o")
    Write-EvidenceJson -Value $summary -Path (Join-Path $EvidenceDirectory "project-bootstrap-summary.json") -Depth 12
    Write-Host "Evidence: $EvidenceDirectory"
}
