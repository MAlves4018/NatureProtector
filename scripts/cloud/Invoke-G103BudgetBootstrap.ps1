[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$Confirmation,
    [string]$EvidenceDirectory = (Join-Path (Get-Location) "artifacts/g10-3-budgets"),
    [string]$PythonExecutable = $env:NATUREPROTECTOR_VALIDATION_PYTHON,
    [switch]$Execute
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expected = "CREATE_NATUREPROTECTOR_BUDGET_ALERTS_ONLY"
if ($Confirmation -ne $expected) { throw "Confirmation must be exactly: $expected" }
if (-not $Execute) { throw "No changes were made. Re-run with -Execute after reviewing the budget input." }
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

function Write-EvidenceText {
    param(
        [Parameter(Mandatory)][AllowEmptyString()]$Value,
        [Parameter(Mandatory)][string]$Path
    )
    $previous = $WhatIfPreference
    try {
        $WhatIfPreference = $false
        ($Value -join [Environment]::NewLine) | Set-Content -LiteralPath $Path -Encoding utf8
    } finally {
        $WhatIfPreference = $previous
    }
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
    if ([string]::IsNullOrWhiteSpace($RequestedPython)) { $RequestedPython = $env:NATUREPROTECTOR_VALIDATION_PYTHON }
    if ([string]::IsNullOrWhiteSpace($RequestedPython)) { throw "Pass -PythonExecutable or set NATUREPROTECTOR_VALIDATION_PYTHON." }
    if (-not (Test-Path -LiteralPath $RequestedPython -PathType Leaf)) { throw "PythonExecutable does not exist: $RequestedPython" }
    $resolved = (Resolve-Path -LiteralPath $RequestedPython).Path
    if ($resolved -match "\\msys64\\") { throw "MSYS2 Python is not accepted for validation: $resolved" }
    & $resolved -c "import jsonschema, yaml, hcl2" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Validation Python imports failed for: $resolved" }
    return $resolved
}
$summary = [ordered]@{
    schema_version = 1
    phase = "G10.3_BUDGET_BOOTSTRAP"
    mode = if ($WhatIfPreference) { "WHAT_IF" } else { "EXECUTE" }
    started_at = (Get-Date).ToUniversalTime().ToString("o")
    input_path = $InputPath
    budgets = @()
    budget_is_hard_cap = $false
    pubsub_notifications_created = $false
    data_plane_created = $false
    status = "RUNNING"
}

Push-Location $RepositoryRoot
try {
    $ResolvedPythonExecutable = Resolve-ValidationPython -RequestedPython $PythonExecutable
    & $ResolvedPythonExecutable scripts/cloud/Test-G103BudgetInput.py --input $InputPath --output (Join-Path $EvidenceDirectory "budget-input-result.json")
    if ($LASTEXITCODE -ne 0) { throw "Budget input validation failed." }
    $ownerInput = Get-Content -Raw -LiteralPath $InputPath | ConvertFrom-Json
    if (-not $ownerInput.execution.create_budget_alerts) {
        throw "The owner input must explicitly set execution.create_budget_alerts=true before creating budget alerts."
    }
    $quotaBudget = @($ownerInput.budgets | Where-Object { $_.role -eq "platform" -and $_.scope -eq "project" } | Select-Object -First 1)
    if ($quotaBudget.Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$quotaBudget[0].project_id)) {
        throw "A platform project budget is required so billing budget commands can use an explicit quota project."
    }
    $quotaProjectId = [string]$quotaBudget[0].project_id

    $active = (gcloud auth list --filter="status:ACTIVE" --format="value(account)").Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($active)) { throw "Unable to resolve the active gcloud account." }
    if ($active -ne $ownerInput.expected_gcloud_account) { throw "Active gcloud account '$active' does not match expected '$($ownerInput.expected_gcloud_account)'." }
    $summary.active_account = $active
    $summary.billing_account_id = $ownerInput.billing_account_id

    $billingJson = gcloud billing accounts describe $ownerInput.billing_account_id --format=json
    if ($LASTEXITCODE -ne 0) { throw "Billing account is not visible." }
    $billing = $billingJson | ConvertFrom-Json
    if ($billing.open -ne $true) { throw "Billing account is not open." }
    Write-EvidenceText -Value $billingJson -Path (Join-Path $EvidenceDirectory "billing-account.json")

    $existingJson = gcloud billing budgets list --billing-account=$($ownerInput.billing_account_id) --billing-project=$quotaProjectId --format=json
    if ($LASTEXITCODE -ne 0) { throw "Unable to list billing budgets." }
    Write-EvidenceText -Value $existingJson -Path (Join-Path $EvidenceDirectory "budgets-before.json")
    $existing = @($existingJson | ConvertFrom-Json)

    foreach ($budget in $ownerInput.budgets) {
        $displayName = [string]$budget.display_name
        $projectId = if ($budget.PSObject.Properties.Name -contains "project_id") { [string]$budget.project_id } else { $null }
        if ([double]$budget.amount -ne [math]::Floor([double]$budget.amount)) {
            throw "Budget '$displayName' must use a whole currency unit for gcloud budget CLI execution."
        }
        $match = @($existing | Where-Object { $_.displayName -eq $displayName })
        if ($match.Count -gt 0) {
            $existingBudget = $match[0]
            $existingAmount = $existingBudget.amount.specifiedAmount
            $existingUnits = if ($existingAmount.PSObject.Properties.Name -contains "units") { [int64]$existingAmount.units } else { 0 }
            $existingNanos = if ($existingAmount.PSObject.Properties.Name -contains "nanos") { [int64]$existingAmount.nanos } else { 0 }
            if ($existingAmount.currencyCode -ne [string]$budget.currency -or $existingUnits -ne [int64]$budget.amount -or $existingNanos -ne 0) {
                throw "Existing budget '$displayName' does not match the requested amount/currency. Reconcile it explicitly before treating D3 as idempotent."
            }
            $summary.budgets += [ordered]@{
                role = $budget.role
                display_name = $displayName
                scope = $budget.scope
                status = "EXISTS_NOT_MODIFIED"
            }
            continue
        }

        $amountText = [Convert]::ToString([double]$budget.amount, [Globalization.CultureInfo]::InvariantCulture)
        $arguments = @(
            "billing", "budgets", "create",
            "--billing-account=$($ownerInput.billing_account_id)",
            "--billing-project=$quotaProjectId",
            "--display-name=$displayName",
            "--budget-amount=${amountText}$($budget.currency)",
            "--calendar-period=$($budget.calendar_period)",
            "--credit-types-treatment=include-all-credits",
            "--ownership-scope=billing-account"
        )
        if ($budget.scope -eq "project") {
            $arguments += "--filter-projects=projects/$projectId"
        }
        foreach ($threshold in $ownerInput.thresholds) {
            $thresholdText = [Convert]::ToString([double]$threshold, [Globalization.CultureInfo]::InvariantCulture)
            $arguments += "--threshold-rule=percent=${thresholdText},basis=current-spend"
        }

        $planned = $arguments -join " "
        if ($PSCmdlet.ShouldProcess($displayName, "Create Cloud Billing budget alert")) {
            & gcloud @arguments
            if ($LASTEXITCODE -ne 0) { throw "Failed to create budget '$displayName'." }
            $status = "CREATED"
        } else {
            $status = "PLANNED"
        }
        $summary.budgets += [ordered]@{
            role = $budget.role
            display_name = $displayName
            scope = $budget.scope
            project_id = $projectId
            amount = $budget.amount
            currency = $budget.currency
            status = $status
            command = $planned
        }
    }

    if (-not $WhatIfPreference) {
        $afterJson = gcloud billing budgets list --billing-account=$($ownerInput.billing_account_id) --billing-project=$quotaProjectId --format=json
        if ($LASTEXITCODE -ne 0) { throw "Unable to list budgets after creation." }
        Write-EvidenceText -Value $afterJson -Path (Join-Path $EvidenceDirectory "budgets-after.json")
    }
    $summary.status = if ($WhatIfPreference) { "WHAT_IF_PASS" } else { "PASS" }
    Write-Warning "Cloud Billing budgets generate alerts; they do not stop or cap spending."
} catch {
    $summary.status = "FAIL"
    $summary.error = $_.Exception.Message
    throw
} finally {
    Pop-Location
    $summary.completed_at = (Get-Date).ToUniversalTime().ToString("o")
    Write-EvidenceJson -Value $summary -Path (Join-Path $EvidenceDirectory "budget-bootstrap-summary.json") -Depth 12
    Write-Host "Evidence: $EvidenceDirectory"
}
