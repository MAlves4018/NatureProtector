[CmdletBinding()]
param(
    [switch]$IncludeDockerIntegration
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $repositoryRoot

try {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) {
        $python = Get-Command python3 -ErrorAction Stop
    }

    & $python.Source '.\scripts\audit\Test-RabbitMqHealthPhase3APackage.py'
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 3A static package validation failed with exit code $LASTEXITCODE."
    }

    $dotnet = Get-Command dotnet -ErrorAction Stop

    & $dotnet.Source test `
        '.\tests\NatureProtector.Shared.Tests\NatureProtector.Shared.Tests.csproj' `
        --no-restore `
        --filter 'FullyQualifiedName~RabbitMqOptionsTests'
    if ($LASTEXITCODE -ne 0) {
        throw "RabbitMqOptions tests failed with exit code $LASTEXITCODE."
    }

    & $dotnet.Source test `
        '.\tests\NatureProtector.Simulator.Host.Tests\NatureProtector.Simulator.Host.Tests.csproj' `
        --no-restore `
        --filter 'FullyQualifiedName~RabbitMqReadingPublisherBehaviorTests|FullyQualifiedName~RabbitMqControlledValidationMessagePublisherBehaviorTests'
    if ($LASTEXITCODE -ne 0) {
        throw "Simulator topology tests failed with exit code $LASTEXITCODE."
    }

    & $dotnet.Source test `
        '.\tests\NatureProtector.Prevention.Host.Tests\NatureProtector.Prevention.Host.Tests.csproj' `
        --no-restore `
        --filter 'FullyQualifiedName~PreventionWorkerTests.DeclareTopology'
    if ($LASTEXITCODE -ne 0) {
        throw "Prevention topology tests failed with exit code $LASTEXITCODE."
    }

    if ($IncludeDockerIntegration) {
        & $dotnet.Source test `
            '.\tests\NatureProtector.IntegrationTests\NatureProtector.IntegrationTests.csproj' `
            --no-restore `
            --filter 'FullyQualifiedName~DockerRabbitMqPublisherTests'
        if ($LASTEXITCODE -ne 0) {
            throw "RabbitMQ Docker integration tests failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host 'PHASE3A_VALIDATION=PASS'
}
finally {
    Pop-Location
}
