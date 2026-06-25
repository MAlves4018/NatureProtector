[CmdletBinding()]
param(
    [string]$ProjectId,
    [string]$Region,
    [string]$ConfigurationName,
    [string]$ApiListPath,
    [string]$EvidenceDirectory,
    [switch]$RequireTerraform,
    [switch]$RequireDocker,
    [switch]$RequireKubectl
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
    -EnvironmentName 'NATUREPROTECTOR_BILLING_ACCOUNT_ID'

$Region = Get-CloudSetting `
    -ParameterValue $Region `
    -EnvironmentName 'NATUREPROTECTOR_REGION' `
    -DefaultValue 'europe-southwest1'

$ConfigurationName = Get-CloudSetting `
    -ParameterValue $ConfigurationName `
    -EnvironmentName 'NATUREPROTECTOR_GCLOUD_CONFIGURATION' `
    -DefaultValue 'natureprotector-personal'

$EvidenceDirectory = Get-CloudSetting `
    -ParameterValue $EvidenceDirectory `
    -EnvironmentName 'NATUREPROTECTOR_EVIDENCE_DIR' `
    -DefaultValue (Join-Path $repositoryRoot 'artifacts\cloud-setup')

if ([string]::IsNullOrWhiteSpace($ApiListPath)) {
    $ApiListPath = Join-Path $repositoryRoot 'config\cloud\required-apis.txt'
}

Assert-CloudProjectId $ProjectId
Assert-CloudRegion $Region

if (-not [string]::IsNullOrWhiteSpace($BillingAccountId)) {
    Assert-BillingAccountId $BillingAccountId
}

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Name,
        [ValidateSet('PASS', 'FAIL', 'WARN')][string]$Status,
        [string]$Details
    )

    $checks.Add([pscustomobject]@{
        name    = $Name
        status  = $Status
        details = $Details
    })
}

Write-CloudStep 'Ferramentas'

$requirements = [ordered]@{
    gcloud    = $true
    terraform = [bool]$RequireTerraform
    docker    = [bool]$RequireDocker
    kubectl   = [bool]$RequireKubectl
}

foreach ($entry in $requirements.GetEnumerator()) {
    $available = Test-CommandAvailable $entry.Key

    if ($available) {
        Add-Check -Name "tool:$($entry.Key)" -Status PASS -Details 'Disponível no PATH.'
        Write-Host "[PASS] $($entry.Key)"
    }
    elseif ($entry.Value) {
        Add-Check -Name "tool:$($entry.Key)" -Status FAIL -Details 'Obrigatório, mas não encontrado.'
        Write-Host "[FAIL] $($entry.Key)" -ForegroundColor Red
    }
    else {
        Add-Check -Name "tool:$($entry.Key)" -Status WARN -Details 'Não encontrado; opcional nesta execução.'
        Write-Host "[WARN] $($entry.Key)" -ForegroundColor Yellow
    }
}

if (-not (Test-CommandAvailable 'gcloud')) {
    $evidence = [ordered]@{
        timestampUtc = Get-CloudTimestamp
        projectId    = $ProjectId
        checks       = $checks
        verdict      = 'FAIL'
    }

    $path = Write-CloudEvidence `
        -Data $evidence `
        -Directory $EvidenceDirectory `
        -FilePrefix 'cloud-setup-test'

    Write-Host "Evidence: $path"
    exit 1
}

Write-CloudStep 'Configuração gcloud'

$configResult = Invoke-GCloudText -Arguments @(
    'config', 'configurations', 'list',
    '--filter=is_active:true',
    '--format=value(name)'
) -AllowFailure

$activeConfiguration = $configResult.Output.Trim()

if ($activeConfiguration -eq $ConfigurationName) {
    Add-Check -Name 'gcloud:configuration' -Status PASS -Details $activeConfiguration
}
else {
    Add-Check -Name 'gcloud:configuration' -Status FAIL -Details "Ativa='$activeConfiguration'; esperada='$ConfigurationName'."
}

$activeAccount = (Invoke-GCloudText -Arguments @(
    'config', 'get-value', 'account', '--quiet'
) -AllowFailure).Output.Trim()

if (-not [string]::IsNullOrWhiteSpace($activeAccount) -and $activeAccount -ne '(unset)') {
    Add-Check -Name 'gcloud:account' -Status PASS -Details $activeAccount
}
else {
    Add-Check -Name 'gcloud:account' -Status FAIL -Details 'Nenhuma conta ativa.'
}

$configuredProject = (Invoke-GCloudText -Arguments @(
    'config', 'get-value', 'project', '--quiet'
) -AllowFailure).Output.Trim()

if ($configuredProject -eq $ProjectId) {
    Add-Check -Name 'gcloud:project' -Status PASS -Details $configuredProject
}
else {
    Add-Check -Name 'gcloud:project' -Status FAIL -Details "Configurado='$configuredProject'; esperado='$ProjectId'."
}

$configuredRegion = (Invoke-GCloudText -Arguments @(
    'config', 'get-value', 'compute/region', '--quiet'
) -AllowFailure).Output.Trim()

if ($configuredRegion -eq $Region) {
    Add-Check -Name 'gcloud:region' -Status PASS -Details $configuredRegion
}
else {
    Add-Check -Name 'gcloud:region' -Status FAIL -Details "Configurada='$configuredRegion'; esperada='$Region'."
}

Write-CloudStep 'Acesso e autenticação'

$projectResult = Invoke-GCloudJson -Arguments @(
    'projects', 'describe', $ProjectId
) -AllowFailure

if ($projectResult.Succeeded -and ([string]$projectResult.Data.lifecycleState -eq 'ACTIVE')) {
    Add-Check -Name 'project:access' -Status PASS -Details 'Projeto acessível e ACTIVE.'
}
else {
    Add-Check -Name 'project:access' -Status FAIL -Details $projectResult.Error
}

$adcResult = Invoke-GCloudText -Arguments @(
    'auth', 'application-default', 'print-access-token'
) -AllowFailure

if ($adcResult.ExitCode -eq 0 -and (-not [string]::IsNullOrWhiteSpace($adcResult.Output))) {
    Add-Check -Name 'auth:adc' -Status PASS -Details 'ADC conseguiu emitir um token; o token não foi registado.'
}
else {
    Add-Check -Name 'auth:adc' -Status FAIL -Details 'ADC indisponível ou inválido.'
}

Write-CloudStep 'Faturação'

$billingResult = Invoke-GCloudJson -Arguments @(
    'billing', 'projects', 'describe', $ProjectId
) -AllowFailure

if ($billingResult.Succeeded) {
    $linkedName = [string]$billingResult.Data.billingAccountName
    $linkedId = $linkedName -replace '^billingAccounts/', ''
    $enabled = [bool]$billingResult.Data.billingEnabled

    if (-not $enabled) {
        Add-Check -Name 'billing:enabled' -Status FAIL -Details 'Billing não está ativa no projeto.'
    }
    elseif ([string]::IsNullOrWhiteSpace($BillingAccountId)) {
        Add-Check -Name 'billing:enabled' -Status PASS -Details "Ativa; ID ligado=$(Get-MaskedBillingAccountId $linkedId)."
        Add-Check -Name 'billing:expected-id' -Status WARN -Details 'NATUREPROTECTOR_BILLING_ACCOUNT_ID não definida; correspondência não verificada.'
    }
    elseif ($linkedId -eq $BillingAccountId) {
        Add-Check -Name 'billing:enabled' -Status PASS -Details 'Ativa.'
        Add-Check -Name 'billing:expected-id' -Status PASS -Details (Get-MaskedBillingAccountId $linkedId)
    }
    else {
        Add-Check -Name 'billing:expected-id' -Status FAIL -Details "Ligada=$(Get-MaskedBillingAccountId $linkedId); esperada=$(Get-MaskedBillingAccountId $BillingAccountId)."
    }
}
else {
    Add-Check -Name 'billing:inspection' -Status FAIL -Details $billingResult.Error
}

Write-CloudStep 'APIs'

$requiredApis = Get-RequiredCloudApis -Path $ApiListPath
$enabledResult = Invoke-GCloudText -Arguments @(
    'services', 'list',
    '--enabled',
    "--project=$ProjectId",
    '--format=value(config.name)'
) -AllowFailure

if ($enabledResult.ExitCode -eq 0) {
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
        Add-Check -Name 'apis:required' -Status PASS -Details "$($requiredApis.Count) APIs base ativas."
    }
    else {
        Add-Check -Name 'apis:required' -Status FAIL -Details "Em falta: $($missingApis -join ', ')"
    }
}
else {
    Add-Check -Name 'apis:inspection' -Status FAIL -Details $enabledResult.Error
}

Write-CloudStep 'Artifact Registry'

$artifactResult = Invoke-GCloudJson -Arguments @(
    'artifacts', 'repositories', 'list',
    "--project=$ProjectId",
    "--location=$Region"
) -AllowFailure

if ($artifactResult.Succeeded) {
    $repositories = @($artifactResult.Data)
    Add-Check -Name 'artifact-registry:access' -Status PASS -Details "$($repositories.Count) repositório(s) visível(eis) na região."
}
else {
    Add-Check -Name 'artifact-registry:access' -Status WARN -Details $artifactResult.Error
}

$failures = @($checks | Where-Object { $_.status -eq 'FAIL' })
$warnings = @($checks | Where-Object { $_.status -eq 'WARN' })
$verdict = if ($failures.Count -gt 0) { 'FAIL' } elseif ($warnings.Count -gt 0) { 'PASS_WITH_WARNINGS' } else { 'PASS' }

$evidence = [ordered]@{
    timestampUtc           = Get-CloudTimestamp
    projectId              = $ProjectId
    region                 = $Region
    configurationExpected  = $ConfigurationName
    billingExpectedMasked  = Get-MaskedBillingAccountId $BillingAccountId
    checks                 = $checks
    verdict                = $verdict
}

$evidencePath = Write-CloudEvidence `
    -Data $evidence `
    -Directory $EvidenceDirectory `
    -FilePrefix 'cloud-setup-test'

Write-CloudStep 'Veredito'
Write-Host "Verdict: $verdict"
Write-Host "Evidence: $evidencePath"

foreach ($check in $checks) {
    $line = "[$($check.status)] $($check.name): $($check.details)"
    if ($check.status -eq 'FAIL') {
        Write-Host $line -ForegroundColor Red
    }
    elseif ($check.status -eq 'WARN') {
        Write-Host $line -ForegroundColor Yellow
    }
    else {
        Write-Host $line -ForegroundColor Green
    }
}

if ($failures.Count -gt 0) {
    exit 1
}
