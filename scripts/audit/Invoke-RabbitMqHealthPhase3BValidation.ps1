[CmdletBinding()]
param(
    [switch]$SkipFrontend
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

    & $python.Source '.\scripts\audit\Test-RabbitMqHealthPhase3BPackage.py'
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 3B static package validation failed with exit code $LASTEXITCODE."
    }

    $dotnet = Get-Command dotnet -ErrorAction Stop

    & $dotnet.Source test `
        '.\tests\NatureProtector.Shared.Tests\NatureProtector.Shared.Tests.csproj' `
        --no-restore `
        --filter 'FullyQualifiedName~RabbitMqOptionsTests|FullyQualifiedName~MessagingConstantsTests'
    if ($LASTEXITCODE -ne 0) {
        throw "Shared RabbitMQ queue-role tests failed with exit code $LASTEXITCODE."
    }

    & $dotnet.Source test `
        '.\tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj' `
        --no-restore `
        --filter 'FullyQualifiedName~RuntimeObservabilityServiceTests|FullyQualifiedName~UnavailableRuntimeObservabilityServiceTests|FullyQualifiedName~OpenApiSemanticTests.OpenApiDocument_DescribesObservabilityTypesAndNullability|FullyQualifiedName~ControlPlaneApiTests.RuntimeObservabilityRabbitMq_DistinguishesMeasuredZeroFromUnavailable'
    if ($LASTEXITCODE -ne 0) {
        throw "Backoffice RabbitMQ observability tests failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipFrontend) {
        $npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
        if ($null -eq $npm) {
            $npm = Get-Command npm -ErrorAction Stop
        }

        Push-Location '.\webUI'
        try {
            & $npm.Source run typecheck:strict
            if ($LASTEXITCODE -ne 0) {
                throw "webUI strict typecheck failed with exit code $LASTEXITCODE."
            }

            & $npm.Source test -- technicalSurfaces.test.ts
            if ($LASTEXITCODE -ne 0) {
                throw "webUI queue-role surface tests failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host 'PHASE3B_VALIDATION=PASS'
}
finally {
    Pop-Location
}
