[CmdletBinding()]
param(
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$IncludeDockerIntegration,
    [switch]$KeepInfrastructure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repositoryRoot 'NatureProtector.sln'
$simulatorTests = Join-Path $repositoryRoot 'tests\NatureProtector.Simulator.Host.Tests\NatureProtector.Simulator.Host.Tests.csproj'
$integrationTests = Join-Path $repositoryRoot 'tests\NatureProtector.IntegrationTests\NatureProtector.IntegrationTests.csproj'
$startedByThisRun = $false

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

function Invoke-PythonValidator {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) {
        $python = Get-Command python3 -ErrorAction Stop
    }

    & $python.Source '.\scripts\audit\Test-RabbitMqHealthPhase3GPackage.py'
    if ($LASTEXITCODE -ne 0) {
        throw 'Phase 3G static package validation failed.'
    }
}

Push-Location $repositoryRoot
try {
    Invoke-PythonValidator
    Write-Host 'PHASE3G_PACKAGE_STATIC_CHECK=PASS'

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
        'test', $simulatorTests,
        '-c', 'Release',
        '--no-restore', '--no-build', '--nologo',
        '--filter',
        'FullyQualifiedName~RabbitMqPublishGuaranteesTests|FullyQualifiedName~SimulationRunnerTests|FullyQualifiedName~ControlledValidationOrchestratorTests|FullyQualifiedName~ControlledValidationRunnerTests'
    )

    Write-Host 'PHASE3G_TYPED_PUBLISH_OUTCOMES_AND_PROCESS_EXIT_PROVED'

    if ($IncludeDockerIntegration) {
        Assert-Command docker
        Assert-Command pwsh

        $existing = @(& docker ps -a --format '{{.Names}}' | Where-Object {
            $_ -in @('np-postgres-it', 'np-rabbitmq-it', 'np-influxdb-it')
        })

        & '.\scripts\ci\Start-DockerIntegrationServices.ps1'
        if ($LASTEXITCODE -ne 0) {
            throw 'Docker integration infrastructure failed to start or become ready.'
        }
        $startedByThisRun = $existing.Count -eq 0

        $env:NP_RUN_OPERATIONAL_AUDIT_PHASE3G = 'true'
        try {
            Invoke-DotNet @(
                'test', $integrationTests,
                '-c', 'Release',
                '--no-restore', '--no-build', '--nologo',
                '--filter',
                'FullyQualifiedName~PartialNack_PrimaryProcessesOnce_AndSameEventIdRetryIsIdempotent|FullyQualifiedName~PublishedSimulator_PartialNack_ExitsNonZero_MarksRunFailed_WhilePrimaryProcessesOnce',
                '--logger',
                'console;verbosity=normal'
            )
        }
        finally {
            Remove-Item Env:NP_RUN_OPERATIONAL_AUDIT_PHASE3G -ErrorAction SilentlyContinue
        }

        Write-Host 'PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED'
        Write-Host 'PHASE3G_PUBLISHED_RUNTIME_PARTIAL_DELIVERY_PROVED'
    }

    Write-Host 'PHASE3G_VALIDATION=PASS'
}
finally {
    Pop-Location

    if ($startedByThisRun -and -not $KeepInfrastructure) {
        & docker compose `
            --project-name np-standard-cd-it `
            --file (Join-Path $repositoryRoot '.github\docker\standard-cd-integration.compose.yml') `
            down -v --remove-orphans
    }
}
