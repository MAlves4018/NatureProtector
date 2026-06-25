[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

$requiredFiles = @(
    'scripts\cloud\Common-Cloud.ps1',
    'scripts\cloud\Set-CloudEnvironment.ps1',
    'scripts\cloud\Setup-CloudDeveloper.ps1',
    'scripts\cloud\Initialize-CloudProject.ps1',
    'scripts\cloud\Test-CloudSetup.ps1',
    'scripts\cloud\Remove-StagingResources.ps1',
    'config\cloud\required-apis.txt',
    'docs\cloud\local-cloud-setup.md',
    'docs\cloud\admin-cloud-bootstrap.md',
    'docs\cloud\environment-variables.md'
)

$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([Parameter(Mandatory = $true)][string]$Message)
    $failures.Add($Message)
}

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Failure "Ficheiro em falta: $relativePath"
    }
}

$scanPaths = @(
    Join-Path $repositoryRoot 'scripts\cloud'
    Join-Path $repositoryRoot 'config\cloud'
    Join-Path $repositoryRoot 'docs\cloud'
)

$filesToScan = @(
    Get-ChildItem -Path $scanPaths -Recurse -File -ErrorAction SilentlyContinue
    | Where-Object {
        $_.Extension -notin @('.pyc', '.pyo') -and
        $_.FullName -notmatch '[\\/](?:__pycache__|bin|obj)[\\/]'
    }
)

$forbiddenPatterns = [ordered]@{
    'Billing Account ID concreto' = '(?i)(?<!<)[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}(?!>)'
    'Chave privada'               = '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
    'Service account JSON'        = '"type"\s*:\s*"service_account"'
    'Token OAuth'                 = '(?i)ya29\.[A-Za-z0-9_-]+'
    'Password hardcoded'          = '(?i)(password|passwd|pwd)\s*=\s*["''][^"'']+["'']'
}

foreach ($file in $filesToScan) {
    $content = Get-Content -Raw -LiteralPath $file.FullName

    foreach ($pattern in $forbiddenPatterns.GetEnumerator()) {
        if ($content -match $pattern.Value) {
            Add-Failure "$($pattern.Key) encontrado em $($file.FullName)"
        }
    }
}

$commonPath = Join-Path $repositoryRoot 'scripts\cloud\Common-Cloud.ps1'
$initializePath = Join-Path $repositoryRoot 'scripts\cloud\Initialize-CloudProject.ps1'
if (Test-Path $initializePath) {
    $initializeContent = Get-Content -Raw -LiteralPath $initializePath

    if ($initializeContent -notmatch 'NATUREPROTECTOR_BILLING_ACCOUNT_ID') {
        Add-Failure 'Initialize-CloudProject.ps1 não lê NATUREPROTECTOR_BILLING_ACCOUNT_ID.'
    }

    $topLevelParamBlock = [regex]::Match($initializeContent, '(?s)^\[CmdletBinding\(\)\]\s*param\((.*?)\)\s*Set-StrictMode').Groups[1].Value
    if ($topLevelParamBlock -match '\[string\]\$BillingAccountId') {
        Add-Failure 'Initialize-CloudProject.ps1 aceita BillingAccountId por parâmetro; deve obtê-lo exclusivamente do ambiente.'
    }

    if ($initializeContent -notmatch 'AllowBillingLink') {
        Add-Failure 'Initialize-CloudProject.ps1 não exige o gate AllowBillingLink.'
    }

    if ($initializeContent -match '(?i)container clusters create|sql instances create|compute forwarding-rules create') {
        Add-Failure 'Initialize-CloudProject.ps1 contém criação de recursos de runtime.'
    }

    if ($initializeContent -notmatch 'PLAN_ONLY') {
        Add-Failure 'Initialize-CloudProject.ps1 não expõe modo PLAN_ONLY.'
    }

    if ($initializeContent -notmatch '\$Apply') {
        Add-Failure 'Initialize-CloudProject.ps1 não separa plano de aplicação com $Apply.'
    }

    if ($initializeContent -notmatch 'A ligação de billing exige simultaneamente -Apply e -AllowBillingLink') {
        Add-Failure 'Initialize-CloudProject.ps1 não recusa alteração de billing sem -AllowBillingLink.'
    }
}

$removeStagingPath = Join-Path $repositoryRoot 'scripts\cloud\Remove-StagingResources.ps1'
if (Test-Path $removeStagingPath) {
    $removeStagingContent = Get-Content -Raw -LiteralPath $removeStagingPath

    if ($removeStagingContent -notmatch '\$ProjectId -notmatch ''staging''') {
        Add-Failure 'Remove-StagingResources.ps1 não recusa teardown fora de staging.'
    }
}

if (Test-Path $commonPath) {
    . $commonPath

    $powerShellExe = (Get-Process -Id $PID).Path

    try {
        $empty = Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'exit 0')
        if (($empty.ExitCode -ne 0) -or ($empty.Output -ne '') -or ($empty.Error -ne '')) {
            Add-Failure 'Invoke-CapturedCommand não trata output vazio sem stderr como sucesso limpo.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand lançou exceção em output vazio: $($_.Exception.Message)"
    }

    try {
        $oneLine = Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'Write-Output "one"')
        if ($oneLine.Output -ne 'one') {
            Add-Failure 'Invoke-CapturedCommand não preserva output de uma linha.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand falhou com output de uma linha: $($_.Exception.Message)"
    }

    try {
        $multiLine = Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'Write-Output "one"; Write-Output "two"')
        if ($multiLine.Output -ne "one`ntwo") {
            Add-Failure 'Invoke-CapturedCommand não preserva output de várias linhas.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand falhou com output de várias linhas: $($_.Exception.Message)"
    }

    try {
        $stderrOnly = Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', '[Console]::Error.WriteLine("stderr-only")')
        if (($stderrOnly.Output -ne '') -or ($stderrOnly.Error -ne 'stderr-only')) {
            Add-Failure 'Invoke-CapturedCommand não captura comando que escreve apenas em stderr.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand falhou com stderr-only: $($_.Exception.Message)"
    }

    try {
        $unicode = Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'Write-Output "ação-✓"')
        if ($unicode.Output -ne 'ação-✓') {
            Add-Failure 'Invoke-CapturedCommand não preserva caracteres Unicode.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand falhou com Unicode: $($_.Exception.Message)"
    }

    $failureThrew = $false
    try {
        Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'Write-Error "boom"; exit 7') | Out-Null
    }
    catch {
        $failureThrew = $true
    }
    if (-not $failureThrew) {
        Add-Failure 'Invoke-CapturedCommand não lança exceção para exit code diferente de zero sem AllowFailure.'
    }

    try {
        $allowedFailure = Invoke-CapturedCommand -Command $powerShellExe -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'Write-Error "allowed"; exit 9') -AllowFailure
        if ($allowedFailure.ExitCode -ne 9) {
            Add-Failure 'Invoke-CapturedCommand com AllowFailure não preserva exit code diferente de zero.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand com AllowFailure lançou exceção: $($_.Exception.Message)"
    }

    try {
        $missingCommand = Invoke-CapturedCommand -Command 'natureprotector-command-that-does-not-exist' -AllowFailure
        if (($missingCommand.ExitCode -eq 0) -or [string]::IsNullOrWhiteSpace($missingCommand.Error)) {
            Add-Failure 'Invoke-CapturedCommand não trata comando inexistente como falha capturada.'
        }
    }
    catch {
        Add-Failure "Invoke-CapturedCommand com comando inexistente e AllowFailure lançou exceção: $($_.Exception.Message)"
    }

    if (@(Split-CloudCommandLines '').Count -ne 0) {
        Add-Failure 'Split-CloudCommandLines não trata ausência de configurações como lista vazia.'
    }

    $configurationNames = @(Split-CloudCommandLines "default`nnatureprotector-personal")
    if ($configurationNames -notcontains 'natureprotector-personal') {
        Add-Failure 'Split-CloudCommandLines não deteta configuração existente.'
    }

    $missingBillingEnvName = 'NATUREPROTECTOR_TEST_MISSING_BILLING_ACCOUNT_ID'
    $previousBilling = [Environment]::GetEnvironmentVariable($missingBillingEnvName, 'Process')
    $previousBillingUser = [Environment]::GetEnvironmentVariable($missingBillingEnvName, 'User')
    try {
        [Environment]::SetEnvironmentVariable($missingBillingEnvName, $null, 'Process')
        [Environment]::SetEnvironmentVariable($missingBillingEnvName, $null, 'User')
        $billingMissingThrew = $false
        try {
            Get-CloudSetting -EnvironmentName $missingBillingEnvName -Required | Out-Null
        }
        catch {
            $billingMissingThrew = $true
        }
        if (-not $billingMissingThrew) {
            Add-Failure 'Billing Account ID ausente não é recusado quando obrigatório.'
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable($missingBillingEnvName, $previousBilling, 'Process')
        [Environment]::SetEnvironmentVariable($missingBillingEnvName, $previousBillingUser, 'User')
    }

    $invalidBillingThrew = $false
    try {
        Assert-BillingAccountId 'invalid'
    }
    catch {
        $invalidBillingThrew = $true
    }
    if (-not $invalidBillingThrew) {
        Add-Failure 'Billing Account ID inválido não é recusado.'
    }

    if ((Get-MaskedBillingAccountId 'ABCDEF-123456-7890AB') -ne '******-******-7890AB') {
        Add-Failure 'Billing Account ID não é mascarado como esperado.'
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'STATIC_TEST_FAIL' -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'STATIC_TEST_PASS' -ForegroundColor Green
Write-Host "Ficheiros verificados: $($filesToScan.Count)"
