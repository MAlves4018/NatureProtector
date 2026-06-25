[CmdletBinding()]
param(
    [string]$Account,
    [string]$ProjectId,
    [string]$Region,
    [string]$ConfigurationName,
    [switch]$SkipAdc,
    [switch]$ConfigureDocker,
    [switch]$NonInteractive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common-Cloud.ps1')

$ProjectId = Get-CloudSetting `
    -ParameterValue $ProjectId `
    -EnvironmentName 'NATUREPROTECTOR_PROJECT_ID' `
    -Required

$Region = Get-CloudSetting `
    -ParameterValue $Region `
    -EnvironmentName 'NATUREPROTECTOR_REGION' `
    -DefaultValue 'europe-southwest1'

$ConfigurationName = Get-CloudSetting `
    -ParameterValue $ConfigurationName `
    -EnvironmentName 'NATUREPROTECTOR_GCLOUD_CONFIGURATION' `
    -DefaultValue 'natureprotector-personal'

Assert-CloudProjectId $ProjectId
Assert-CloudRegion $Region

Write-CloudStep 'Ferramentas locais'

$toolNames = @('gcloud', 'terraform', 'docker', 'kubectl')
foreach ($tool in $toolNames) {
    if (Test-CommandAvailable $tool) {
        Write-Host "[OK] $tool"
    }
    elseif ($tool -eq 'gcloud') {
        throw "A Google Cloud CLI é obrigatória e não foi encontrada no PATH."
    }
    else {
        Write-Warning "$tool não foi encontrado. Pode ser necessário para Terraform, contentores ou Kubernetes."
    }
}

Write-CloudStep 'Configuração gcloud isolada'

$configResult = Invoke-GCloudText -Arguments @(
    'config', 'configurations', 'list', '--format=value(name)'
)

$configurationNames = @(Split-CloudCommandLines $configResult.Output)

if ($configurationNames -contains $ConfigurationName) {
    Invoke-InteractiveGCloud -Arguments @(
        'config', 'configurations', 'activate', $ConfigurationName
    )
}
else {
    Invoke-InteractiveGCloud -Arguments @(
        'config', 'configurations', 'create', $ConfigurationName, '--activate'
    )
}

Write-CloudStep 'Autenticação da conta humana'

$accountsResult = Invoke-GCloudJson -Arguments @('auth', 'list')
$credentialedAccounts = @()
if ($accountsResult.Succeeded -and ($null -ne $accountsResult.Data)) {
    $credentialedAccounts = @($accountsResult.Data)
}

if ([string]::IsNullOrWhiteSpace($Account)) {
    $active = @(
        $credentialedAccounts |
            Where-Object { $_.status -eq 'ACTIVE' } |
            Select-Object -First 1
    )

    if ($active.Count -gt 0) {
        $Account = [string]$active[0].account
    }
    elseif ($NonInteractive) {
        throw 'Não existe conta ativa e o modo NonInteractive não permite iniciar o fluxo de login.'
    }
    else {
        Invoke-InteractiveGCloud -Arguments @('auth', 'login')
        $Account = (Invoke-GCloudText -Arguments @(
            'config', 'get-value', 'account', '--quiet'
        )).Output.Trim()
    }
}
else {
    $known = @(
        $credentialedAccounts |
            Where-Object { $_.account -eq $Account }
    )

    if (($known.Count -eq 0) -and $NonInteractive) {
        throw "A conta '$Account' não está autenticada e o modo NonInteractive não permite login."
    }

    if ($known.Count -eq 0) {
        Invoke-InteractiveGCloud -Arguments @('auth', 'login', $Account)
    }
}

if ([string]::IsNullOrWhiteSpace($Account)) {
    throw 'Não foi possível determinar a conta Google Cloud.'
}

Write-CloudStep 'Defaults locais do projeto'

Invoke-InteractiveGCloud -Arguments @('config', 'set', 'account', $Account)
Invoke-InteractiveGCloud -Arguments @('config', 'set', 'project', $ProjectId)
Invoke-InteractiveGCloud -Arguments @('config', 'set', 'compute/region', $Region)

$projectResult = Invoke-GCloudJson -Arguments @(
    'projects', 'describe', $ProjectId
)

if (-not $projectResult.Succeeded) {
    throw "A conta '$Account' não consegue descrever o projeto '$ProjectId'."
}

Write-Host "[OK] Acesso ao projeto confirmado: $ProjectId" -ForegroundColor Green

if (-not $SkipAdc) {
    Write-CloudStep 'Application Default Credentials'

    if ($NonInteractive) {
        $adcCheck = Invoke-GCloudText -Arguments @(
            'auth', 'application-default', 'print-access-token'
        ) -AllowFailure

        if ($adcCheck.ExitCode -ne 0) {
            throw 'ADC não está configurado e o modo NonInteractive não permite abrir o login.'
        }
    }
    else {
        Invoke-InteractiveGCloud -Arguments @(
            'auth', 'application-default', 'login', $Account
        )
    }

    Invoke-InteractiveGCloud -Arguments @(
        'auth', 'application-default', 'set-quota-project', $ProjectId
    )

    Write-Host '[OK] ADC e quota project configurados.' -ForegroundColor Green
}

if ($ConfigureDocker) {
    Write-CloudStep 'Autenticação Docker no Artifact Registry'

    if (-not (Test-CommandAvailable 'docker')) {
        throw 'Foi pedido -ConfigureDocker, mas o comando docker não está disponível.'
    }

    $artifactHost = "$Region-docker.pkg.dev"
    Invoke-InteractiveGCloud -Arguments @(
        'auth', 'configure-docker', $artifactHost, '--quiet'
    )

    Write-Host "[OK] Docker configurado para $artifactHost" -ForegroundColor Green
}

Write-CloudStep 'Resumo'

Write-Host "Conta: $Account"
Write-Host "Projeto: $ProjectId"
Write-Host "Região: $Region"
Write-Host "Configuração gcloud: $ConfigurationName"
Write-Host 'Setup local concluído. Execute Test-CloudSetup.ps1 para validação read-only.' -ForegroundColor Green
