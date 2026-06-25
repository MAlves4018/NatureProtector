[CmdletBinding()]
param(
    [string]$ProjectId,
    [string]$Region,
    [string]$ApiListPath,
    [string]$BudgetDisplayName = 'NatureProtector project guardrail',
    [string]$BudgetAmount,
    [string]$EvidenceDirectory,
    [switch]$Apply,
    [switch]$AllowBillingLink
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common-Cloud.ps1')

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

$ProjectId = Get-CloudSetting `
    -ParameterValue $ProjectId `
    -EnvironmentName 'NATUREPROTECTOR_PROJECT_ID' `
    -Required

$BillingAccountId = Get-CloudSetting `
    -EnvironmentName 'NATUREPROTECTOR_BILLING_ACCOUNT_ID' `
    -Required

$Region = Get-CloudSetting `
    -ParameterValue $Region `
    -EnvironmentName 'NATUREPROTECTOR_REGION' `
    -DefaultValue 'europe-southwest1'

$BudgetAmount = Get-CloudSetting `
    -ParameterValue $BudgetAmount `
    -EnvironmentName 'NATUREPROTECTOR_BUDGET_AMOUNT' `
    -DefaultValue '20EUR'

$EvidenceDirectory = Get-CloudSetting `
    -ParameterValue $EvidenceDirectory `
    -EnvironmentName 'NATUREPROTECTOR_EVIDENCE_DIR' `
    -DefaultValue (Join-Path $repositoryRoot 'artifacts\cloud-setup')

if ([string]::IsNullOrWhiteSpace($ApiListPath)) {
    $ApiListPath = Join-Path $repositoryRoot 'config\cloud\required-apis.txt'
}

Assert-CloudProjectId $ProjectId
Assert-BillingAccountId $BillingAccountId
Assert-CloudRegion $Region

if (-not (Test-CommandAvailable 'gcloud')) {
    throw 'A Google Cloud CLI é obrigatória.'
}

function Get-BudgetAmountSpec {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value -notmatch '^(?<amount>[0-9]+(?:\.[0-9]+)?)(?<currency>[A-Z]{3})?$') {
        throw "Budget inválido: '$Value'. Use, por exemplo, 20EUR."
    }

    return [pscustomobject]@{
        Amount = [decimal]$Matches.amount
        Currency = if ([string]::IsNullOrWhiteSpace($Matches.currency)) { $null } else { $Matches.currency }
    }
}

function Get-BudgetAmountValue {
    param([AllowNull()]$Budget)

    $specified = $Budget.amount.specifiedAmount
    if ($null -eq $specified) {
        return $null
    }

    $unitsProperty = $specified.PSObject.Properties['units']
    $nanosProperty = $specified.PSObject.Properties['nanos']
    $currencyProperty = $specified.PSObject.Properties['currencyCode']

    $units = if ($null -eq $unitsProperty -or $null -eq $unitsProperty.Value) { 0 } else { [decimal]$unitsProperty.Value }
    $nanos = if ($null -eq $nanosProperty -or $null -eq $nanosProperty.Value) { 0 } else { [decimal]$nanosProperty.Value / 1000000000 }

    return [pscustomobject]@{
        Amount = $units + $nanos
        Currency = if ($null -eq $currencyProperty -or $null -eq $currencyProperty.Value) { $null } else { [string]$currencyProperty.Value }
    }
}

function Get-BudgetThresholdKey {
    param([Parameter(Mandatory = $true)]$Rule)

    $basis = [string]$Rule.spendBasis
    if ([string]::IsNullOrWhiteSpace($basis)) {
        $basis = 'CURRENT_SPEND'
    }

    $thresholdPercent = [decimal]$Rule.thresholdPercent
    $thresholdText = $thresholdPercent.ToString('0.####', [Globalization.CultureInfo]::InvariantCulture)
    return ('{0}:{1}' -f $basis, $thresholdText)
}

function Get-ObjectPropertyValue {
    param(
        [AllowNull()]$InputObject,
        [Parameter(Mandatory = $true)][string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-BudgetSemanticMatch {
    param(
        [Parameter(Mandatory = $true)]$Budget,
        [Parameter(Mandatory = $true)]$ExpectedAmount,
        [Parameter(Mandatory = $true)][string]$ProjectId,
        [AllowEmptyString()][string]$ProjectNumber
    )

    $actualAmount = Get-BudgetAmountValue $Budget
    if ($null -eq $actualAmount) {
        return $false
    }

    if ($actualAmount.Amount -ne $ExpectedAmount.Amount) {
        return $false
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedAmount.Currency) -and $actualAmount.Currency -ne $ExpectedAmount.Currency) {
        return $false
    }

    $budgetFilter = Get-ObjectPropertyValue -InputObject $Budget -PropertyName 'budgetFilter'
    $calendarPeriod = Get-ObjectPropertyValue -InputObject $budgetFilter -PropertyName 'calendarPeriod'
    if ([string]$calendarPeriod -ne 'MONTH') {
        return $false
    }

    $projectFiltersValue = Get-ObjectPropertyValue -InputObject $budgetFilter -PropertyName 'projects'
    $projectFilters = @($projectFiltersValue)
    $expectedProjectFilters = @("projects/$ProjectId")
    if (-not [string]::IsNullOrWhiteSpace($ProjectNumber)) {
        $expectedProjectFilters += "projects/$ProjectNumber"
    }

    if (($projectFilters.Count -ne 1) -or ($expectedProjectFilters -notcontains $projectFilters[0])) {
        return $false
    }

    $expectedThresholds = @(
        'CURRENT_SPEND:0.25',
        'CURRENT_SPEND:0.5',
        'CURRENT_SPEND:0.75',
        'CURRENT_SPEND:0.9',
        'CURRENT_SPEND:1',
        'FORECASTED_SPEND:1'
    )
    $thresholdRules = @(Get-ObjectPropertyValue -InputObject $Budget -PropertyName 'thresholdRules')
    $actualThresholds = @($thresholdRules | ForEach-Object { Get-BudgetThresholdKey $_ } | Sort-Object)
    $expectedSorted = @($expectedThresholds | Sort-Object)

    if ($actualThresholds.Count -ne $expectedSorted.Count) {
        return $false
    }

    for ($index = 0; $index -lt $expectedSorted.Count; $index++) {
        if ($actualThresholds[$index] -ne $expectedSorted[$index]) {
            return $false
        }
    }

    return $true
}

function Test-BudgetUpdateCandidate {
    param(
        [Parameter(Mandatory = $true)]$Budget,
        [Parameter(Mandatory = $true)]$ExpectedAmount
    )

    $actualAmount = Get-BudgetAmountValue $Budget
    if ($null -eq $actualAmount) {
        return $false
    }

    $sameAmount = $actualAmount.Amount -eq $ExpectedAmount.Amount
    $sameCurrency = [string]::IsNullOrWhiteSpace($ExpectedAmount.Currency) -or $actualAmount.Currency -eq $ExpectedAmount.Currency
    $budgetFilter = Get-ObjectPropertyValue -InputObject $Budget -PropertyName 'budgetFilter'
    $calendarPeriod = Get-ObjectPropertyValue -InputObject $budgetFilter -PropertyName 'calendarPeriod'
    $monthly = ([string]$calendarPeriod -eq 'MONTH') -or [string]::IsNullOrWhiteSpace([string]$calendarPeriod)

    return $sameAmount -and $sameCurrency -and $monthly
}

function Get-MaskedBudgetName {
    param(
        [AllowEmptyString()][string]$BudgetName,
        [Parameter(Mandatory = $true)][string]$BillingAccountId
    )

    if ([string]::IsNullOrWhiteSpace($BudgetName)) {
        return $null
    }

    return $BudgetName.Replace($BillingAccountId, (Get-MaskedBillingAccountId $BillingAccountId))
}

$mode = if ($Apply) { 'APPLY' } else { 'PLAN_ONLY' }
$budgetAmountSpec = Get-BudgetAmountSpec $BudgetAmount

Write-CloudStep "Inicialização do projeto — $mode"
Write-Host "Projeto: $ProjectId"
Write-Host "Billing: $(Get-MaskedBillingAccountId $BillingAccountId)"
Write-Host "Região: $Region"
Write-Host "Lista de APIs: $ApiListPath"
Write-Host "Budget: $BudgetDisplayName / $BudgetAmount"

$projectResult = Invoke-GCloudJson -Arguments @(
    'projects', 'describe', $ProjectId
)

if (-not $projectResult.Succeeded) {
    throw "Não foi possível aceder ao projeto '$ProjectId'."
}

$projectState = [string]$projectResult.Data.lifecycleState
if ($projectState -ne 'ACTIVE') {
    throw "O projeto não está ACTIVE. Estado: $projectState"
}
$projectNumber = [string]$projectResult.Data.projectNumber

Write-CloudStep 'Ligação da faturação'

$billingResult = Invoke-GCloudJson -Arguments @(
    'billing', 'projects', 'describe', $ProjectId
)

if (-not $billingResult.Succeeded) {
    throw "Não foi possível consultar a faturação do projeto: $($billingResult.Error)"
}

$currentBillingName = [string]$billingResult.Data.billingAccountName
$currentBillingId = $currentBillingName -replace '^billingAccounts/', ''
$billingEnabled = [bool]$billingResult.Data.billingEnabled
$billingMatches = $billingEnabled -and ($currentBillingId -eq $BillingAccountId)

if ($billingMatches) {
    Write-Host '[OK] Projeto já ligado à billing account esperada.' -ForegroundColor Green
}
else {
    Write-Warning "Ligação atual: enabled=$billingEnabled; billing=$(Get-MaskedBillingAccountId $currentBillingId)"
    Write-Host "[PLAN] Ligar '$ProjectId' à billing account indicada no ambiente."

    if ($Apply) {
        if (-not $AllowBillingLink) {
            throw 'A ligação de billing exige simultaneamente -Apply e -AllowBillingLink.'
        }

        Invoke-InteractiveGCloud -Arguments @(
            'billing', 'projects', 'link', $ProjectId,
            "--billing-account=$BillingAccountId",
            '--quiet'
        )

        $verifyBilling = Invoke-GCloudJson -Arguments @(
            'billing', 'projects', 'describe', $ProjectId
        )

        $verifiedName = [string]$verifyBilling.Data.billingAccountName
        $verifiedId = $verifiedName -replace '^billingAccounts/', ''

        if ((-not [bool]$verifyBilling.Data.billingEnabled) -or ($verifiedId -ne $BillingAccountId)) {
            throw 'A ligação de billing não ficou no estado esperado.'
        }

        $billingEnabled = $true
        $billingMatches = $true
        Write-Host '[OK] Billing ligada e verificada.' -ForegroundColor Green
    }
}

Write-CloudStep 'APIs base'

$requiredApis = Get-RequiredCloudApis -Path $ApiListPath
$enabledResult = Invoke-GCloudText -Arguments @(
    'services', 'list',
    '--enabled',
    "--project=$ProjectId",
    '--format=value(config.name)'
)

$enabledApis = @(
    $enabledResult.Output -split "`r?`n" |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
)

$missingApis = @(
    $requiredApis |
        Where-Object { $enabledApis -notcontains $_ }
)

if ($missingApis.Count -eq 0) {
    Write-Host '[OK] Todas as APIs base já estão ativas.' -ForegroundColor Green
}
else {
    Write-Host '[PLAN] APIs a ativar:'
    $missingApis | ForEach-Object { Write-Host "  - $_" }

    if ($Apply) {
        $enableArguments = @('services', 'enable')
        $enableArguments += $missingApis
        $enableArguments += "--project=$ProjectId"
        $enableArguments += '--quiet'
        Invoke-InteractiveGCloud -Arguments $enableArguments

        Write-Host '[OK] APIs base ativadas.' -ForegroundColor Green
    }
}

Write-CloudStep 'Budget do projeto'

$budgetListResult = Invoke-GCloudJson -Arguments @(
    'billing', 'budgets', 'list',
    "--billing-account=$BillingAccountId"
) -AllowFailure

$budgetCheckSucceeded = $budgetListResult.Succeeded
$budgetExists = $false
$budgetDecision = 'BUDGET_INSPECTION_FAILED'
$budgetMatchedBy = $null
$budgetTargetName = $null
$budgetTargetDisplayName = $null

if ($budgetCheckSucceeded) {
    $budgets = @($budgetListResult.Data)
    $semanticMatches = @(
        $budgets |
            Where-Object { Test-BudgetSemanticMatch -Budget $_ -ExpectedAmount $budgetAmountSpec -ProjectId $ProjectId -ProjectNumber $projectNumber }
    )
    $updateCandidates = @(
        $budgets |
            Where-Object { Test-BudgetUpdateCandidate -Budget $_ -ExpectedAmount $budgetAmountSpec }
    )

    if ($semanticMatches.Count -gt 0) {
        $budgetExists = $true
        $budgetDecision = 'REUSE_EXISTING_BUDGET'
        $budgetMatchedBy = 'semantic-project-amount-currency-period-thresholds'
        $budgetTargetName = [string]$semanticMatches[0].name
        $budgetTargetDisplayName = [string]$semanticMatches[0].displayName
        Write-Host "[OK] Budget equivalente já existe: '$budgetTargetDisplayName'." -ForegroundColor Green
    }
    elseif ($updateCandidates.Count -gt 0) {
        $budgetExists = $true
        $budgetDecision = 'UPDATE_EXISTING_BUDGET'
        $budgetMatchedBy = 'amount-currency-period-candidate'
        $budgetTargetName = [string]$updateCandidates[0].name
        $budgetTargetDisplayName = [string]$updateCandidates[0].displayName
        Write-Host "[PLAN] Atualizar budget existente '$budgetTargetDisplayName' para abranger apenas '$ProjectId' com thresholds completos."
    }
    else {
        $budgetDecision = 'CREATE_NAMED_BUDGET'
        $budgetMatchedBy = 'none'
        Write-Host "[PLAN] Criar budget '$BudgetDisplayName' com montante $BudgetAmount."
    }
}
else {
    Write-Warning "Não foi possível listar budgets: $($budgetListResult.Error)"
    if ($Apply) {
        throw 'Em modo Apply não é seguro criar um budget sem primeiro conseguir listar os existentes.'
    }
}

if ($Apply -and $budgetCheckSucceeded -and ($budgetDecision -eq 'UPDATE_EXISTING_BUDGET')) {
    $budgetArguments = @(
        'billing', 'budgets', 'update',
        $budgetTargetName,
        "--display-name=$BudgetDisplayName",
        "--budget-amount=$BudgetAmount",
        '--calendar-period=month',
        "--filter-projects=projects/$ProjectId",
        '--clear-threshold-rules',
        '--add-threshold-rule=percent=0.25',
        '--add-threshold-rule=percent=0.50',
        '--add-threshold-rule=percent=0.75',
        '--add-threshold-rule=percent=0.90',
        '--add-threshold-rule=percent=1.00',
        '--add-threshold-rule=percent=1.00,basis=forecasted-spend',
        '--quiet'
    )

    Invoke-InteractiveGCloud -Arguments $budgetArguments
    Write-Host '[OK] Budget existente atualizado.' -ForegroundColor Green
    $budgetExists = $true
}

if ($Apply -and $budgetCheckSucceeded -and ($budgetDecision -eq 'CREATE_NAMED_BUDGET')) {
    $budgetArguments = @(
        'billing', 'budgets', 'create',
        "--billing-account=$BillingAccountId",
        "--display-name=$BudgetDisplayName",
        "--budget-amount=$BudgetAmount",
        '--calendar-period=month',
        "--filter-projects=projects/$ProjectId",
        '--threshold-rule=percent=0.25',
        '--threshold-rule=percent=0.50',
        '--threshold-rule=percent=0.75',
        '--threshold-rule=percent=0.90',
        '--threshold-rule=percent=1.00',
        '--threshold-rule=percent=1.00,basis=forecasted-spend',
        '--quiet'
    )

    Invoke-InteractiveGCloud -Arguments $budgetArguments
    Write-Host '[OK] Budget criado.' -ForegroundColor Green
    $budgetExists = $true
}

$evidence = [ordered]@{
    timestampUtc             = Get-CloudTimestamp
    mode                     = $mode
    projectId                = $ProjectId
    projectState             = $projectState
    region                   = $Region
    billingAccountIdMasked   = Get-MaskedBillingAccountId $BillingAccountId
    billingEnabled           = $billingEnabled
    billingMatchesExpected   = $billingMatches
    requiredApis             = $requiredApis
    missingApisBeforeApply   = $missingApis
    budgetDisplayName        = $BudgetDisplayName
    budgetAmount             = $BudgetAmount
    budgetInspectionSucceeded = $budgetCheckSucceeded
    budgetDecision           = $budgetDecision
    budgetMatchedBy          = $budgetMatchedBy
    budgetTargetNameMasked   = Get-MaskedBudgetName -BudgetName $budgetTargetName -BillingAccountId $BillingAccountId
    budgetTargetDisplayName  = $budgetTargetDisplayName
    budgetExistsAfterRun     = $budgetExists
    createdRuntimeResources  = $false
}

$evidencePath = Write-CloudEvidence `
    -Data $evidence `
    -Directory $EvidenceDirectory `
    -FilePrefix 'cloud-project-initialization'

Write-CloudStep 'Resultado'
Write-Host "Evidence: $evidencePath"

if (-not $Apply) {
    Write-Host 'Nenhuma mutação cloud foi executada. Reveja o plano e repita com -Apply quando estiver pronto.' -ForegroundColor Yellow
}
else {
    Write-Host 'Inicialização administrativa concluída. Nenhum recurso de runtime foi criado.' -ForegroundColor Green
}
