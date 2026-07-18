[CmdletBinding()]
param(
    [switch]$IncludeDockerIntegration,
    [switch]$SkipRestore,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repositoryRoot 'NatureProtector.sln'
$preventionHostTests = Join-Path $repositoryRoot 'tests\NatureProtector.Prevention.Host.Tests\NatureProtector.Prevention.Host.Tests.csproj'
$integrationTests = Join-Path $repositoryRoot 'tests\NatureProtector.IntegrationTests\NatureProtector.IntegrationTests.csproj'

function Assert-Command {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    Write-Host "> dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) {
        $python = Get-Command python3 -ErrorAction Stop
    }

    & $python.Source '.\scripts\audit\Test-RabbitMqHealthPhase3DPackage.py'
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 3D static package validation failed with exit code $LASTEXITCODE."
    }

    Assert-Command dotnet

    if (-not $SkipRestore) {
        Invoke-DotNet @('restore', $solution)
    }

    if (-not $SkipBuild) {
        Invoke-DotNet @(
            'build', $solution,
            '-c', 'Release',
            '--no-restore',
            '--nologo',
            '/nodeReuse:false'
        )
    }

    Invoke-DotNet @(
        'test', $preventionHostTests,
        '-c', 'Release',
        '--no-restore',
        '--no-build',
        '--nologo',
        '--filter', 'FullyQualifiedName~PreventionDatabaseHealthCheckTests|FullyQualifiedName~PreventionHealthRegistrationTests|FullyQualifiedName~PreventionRuntimeStateTests'
    )

    if ($IncludeDockerIntegration) {
        Assert-Command docker

        & (Join-Path $repositoryRoot 'scripts\ci\Start-DockerIntegrationServices.ps1')
        if ($LASTEXITCODE -ne 0) {
            throw "Start-DockerIntegrationServices.ps1 failed with exit code $LASTEXITCODE."
        }

        $env:NP_RUN_PREVENTION_READINESS_PHASE3D = 'true'
        $env:NP_TEST_POSTGRES_HOST = '127.0.0.1'
        $env:NP_TEST_POSTGRES_PORT = '5433'
        $env:NP_TEST_POSTGRES_USER = 'np'
        $env:NP_TEST_POSTGRES_PASSWORD = 'np_dev_pass'
        $env:NP_TEST_RABBITMQ_HOST = '127.0.0.1'
        $env:NP_TEST_RABBITMQ_PORT = '5672'
        $env:NP_TEST_RABBITMQ_MANAGEMENT_URL = 'http://127.0.0.1:15672'
        $env:NP_TEST_RABBITMQ_USER = 'np'
        $env:NP_TEST_RABBITMQ_PASSWORD = 'np_dev_pass'

        try {
            Invoke-DotNet @(
                'test', $integrationTests,
                '-c', 'Release',
                '--no-restore',
                '--no-build',
                '--nologo',
                '--filter', 'FullyQualifiedName~PreventionReadiness_',
                '--logger', 'console;verbosity=normal'
            )
        }
        finally {
            Remove-Item Env:NP_RUN_PREVENTION_READINESS_PHASE3D -ErrorAction SilentlyContinue
        }
    }

    Write-Host 'PHASE3D_VALIDATION=PASS'
}
finally {
    Pop-Location
}
