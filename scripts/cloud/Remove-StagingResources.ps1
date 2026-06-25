[CmdletBinding()]
param(
    [string]$ProjectId,
    [Parameter(Mandatory = $true)][string]$TerraformDirectory,
    [string]$EvidenceDirectory,
    [switch]$Apply,
    [string]$Confirmation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common-Cloud.ps1')

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

$ProjectId = Get-CloudSetting `
    -ParameterValue $ProjectId `
    -EnvironmentName 'NATUREPROTECTOR_STAGING_PROJECT_ID' `
    -Required

$EvidenceDirectory = Get-CloudSetting `
    -ParameterValue $EvidenceDirectory `
    -EnvironmentName 'NATUREPROTECTOR_EVIDENCE_DIR' `
    -DefaultValue (Join-Path $repositoryRoot 'artifacts\cloud-setup')

Assert-CloudProjectId $ProjectId

if ($ProjectId -notmatch 'staging') {
    throw "Recusa de segurança: o Project ID '$ProjectId' não contém 'staging'. Este script não deve ser usado no projeto administrativo, platform ou production."
}

if (-not (Test-Path -LiteralPath $TerraformDirectory -PathType Container)) {
    throw "Diretório Terraform não encontrado: $TerraformDirectory"
}

if (-not (Test-CommandAvailable 'terraform')) {
    throw 'Terraform não está disponível no PATH.'
}

$resolvedTerraformDirectory = (Resolve-Path $TerraformDirectory).Path
$planPath = Join-Path $resolvedTerraformDirectory 'natureprotector-destroy.tfplan'
$mode = if ($Apply) { 'APPLY_DESTROY' } else { 'PLAN_DESTROY_ONLY' }

Write-CloudStep "Teardown staging — $mode"
Write-Host "Projeto: $ProjectId"
Write-Host "Terraform: $resolvedTerraformDirectory"

Push-Location $resolvedTerraformDirectory
try {
    $env:TF_VAR_project_id = $ProjectId

    & terraform init -input=false
    if ($LASTEXITCODE -ne 0) {
        throw 'terraform init falhou.'
    }

    & terraform validate
    if ($LASTEXITCODE -ne 0) {
        throw 'terraform validate falhou.'
    }

    & terraform plan -destroy -input=false -out="$planPath"
    if ($LASTEXITCODE -ne 0) {
        throw 'terraform plan -destroy falhou.'
    }

    if ($Apply) {
        $expectedConfirmation = "DESTROY:$ProjectId"
        if ($Confirmation -ne $expectedConfirmation) {
            throw "Confirmação inválida. Repita com -Confirmation '$expectedConfirmation'."
        }

        & terraform apply -input=false -auto-approve "$planPath"
        if ($LASTEXITCODE -ne 0) {
            throw 'terraform apply do plano de destruição falhou.'
        }
    }
}
finally {
    Pop-Location
}

Write-CloudStep 'Inspeção residual read-only'

$residualCommands = @(
    [pscustomobject]@{
        name = 'compute-instances'
        args = @('compute', 'instances', 'list', "--project=$ProjectId", '--format=json')
    },
    [pscustomobject]@{
        name = 'gke-clusters'
        args = @('container', 'clusters', 'list', "--project=$ProjectId", '--format=json')
    },
    [pscustomobject]@{
        name = 'sql-instances'
        args = @('sql', 'instances', 'list', "--project=$ProjectId", '--format=json')
    },
    [pscustomobject]@{
        name = 'forwarding-rules'
        args = @('compute', 'forwarding-rules', 'list', "--project=$ProjectId", '--format=json')
    },
    [pscustomobject]@{
        name = 'disks'
        args = @('compute', 'disks', 'list', "--project=$ProjectId", '--format=json')
    }
)

$residuals = New-Object System.Collections.Generic.List[object]

foreach ($item in $residualCommands) {
    $result = Invoke-GCloudJson -Arguments $item.args -AllowFailure

    if ($result.Succeeded) {
        $resources = @($result.Data)
        $residuals.Add([pscustomobject]@{
            category = $item.name
            inspected = $true
            count = $resources.Count
        })

        Write-Host "$($item.name): $($resources.Count)"
    }
    else {
        $residuals.Add([pscustomobject]@{
            category = $item.name
            inspected = $false
            count = $null
            error = $result.Error
        })

        Write-Warning "Não foi possível inspecionar $($item.name): $($result.Error)"
    }
}

$evidence = [ordered]@{
    timestampUtc       = Get-CloudTimestamp
    mode               = $mode
    projectId          = $ProjectId
    terraformDirectory = $resolvedTerraformDirectory
    destroyPlanPath    = $planPath
    residualInspection = $residuals
}

$evidencePath = Write-CloudEvidence `
    -Data $evidence `
    -Directory $EvidenceDirectory `
    -FilePrefix 'staging-teardown'

Write-CloudStep 'Resultado'
Write-Host "Evidence: $evidencePath"

if (-not $Apply) {
    Write-Host 'Foi criado apenas o plano Terraform de destruição; nada foi removido.' -ForegroundColor Yellow
}
else {
    Write-Host 'O plano Terraform de destruição foi aplicado. Reveja a inspeção residual antes de considerar o teardown fechado.' -ForegroundColor Green
}
