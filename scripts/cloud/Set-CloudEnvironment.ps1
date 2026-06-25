[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z][a-z0-9-]{4,28}[a-z0-9]$')]
    [string]$ProjectId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Fa-f]{6}-[0-9A-Fa-f]{6}-[0-9A-Fa-f]{6}$')]
    [string]$BillingAccountId,

    [ValidatePattern('^[a-z]+-[a-z]+[0-9]+$')]
    [string]$Region = 'europe-southwest1',

    [string]$ConfigurationName = 'natureprotector-personal',

    [ValidateSet('Process', 'User')]
    [string]$Scope = 'Process',

    [ValidatePattern('^$|^[a-z][a-z0-9-]{4,28}[a-z0-9]$')]
    [string]$StagingProjectId = '',

    [string]$BudgetAmount = '20EUR',

    [string]$EvidenceDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$settings = [ordered]@{
    NATUREPROTECTOR_PROJECT_ID             = $ProjectId
    NATUREPROTECTOR_BILLING_ACCOUNT_ID     = $BillingAccountId.ToUpperInvariant()
    NATUREPROTECTOR_REGION                 = $Region
    NATUREPROTECTOR_GCLOUD_CONFIGURATION   = $ConfigurationName
    NATUREPROTECTOR_BUDGET_AMOUNT          = $BudgetAmount
}

if (-not [string]::IsNullOrWhiteSpace($StagingProjectId)) {
    $settings['NATUREPROTECTOR_STAGING_PROJECT_ID'] = $StagingProjectId
}

if (-not [string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $settings['NATUREPROTECTOR_EVIDENCE_DIR'] = $EvidenceDirectory
}

foreach ($entry in $settings.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')

    if ($Scope -eq 'User') {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'User')
    }
}

Write-Host "Variáveis NatureProtector configuradas no âmbito: $Scope" -ForegroundColor Green
Write-Host "Project ID: $ProjectId"
Write-Host "Billing Account ID: ******-******-$($BillingAccountId.Substring($BillingAccountId.Length - 6))"
Write-Host "Region: $Region"
Write-Host "gcloud configuration: $ConfigurationName"
Write-Host "Budget amount: $BudgetAmount"

if ($Scope -eq 'User') {
    Write-Warning 'As variáveis persistentes ficam disponíveis automaticamente em novas sessões. Também foram aplicadas ao processo atual.'
}

Write-Warning 'O Billing Account ID não é uma credencial secreta, mas é um identificador administrativo. Não o grave em código, logs públicos ou capturas desnecessárias.'
