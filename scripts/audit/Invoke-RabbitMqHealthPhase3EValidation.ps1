[CmdletBinding()]
param(
    [switch]$SkipRestore,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repositoryRoot 'NatureProtector.sln'
$backofficeTests = Join-Path $repositoryRoot 'tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj'
$sharedTests = Join-Path $repositoryRoot 'tests\NatureProtector.Shared.Tests\NatureProtector.Shared.Tests.csproj'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    Write-Host "> dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE." }
}

Push-Location $repositoryRoot
try {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) { $python = Get-Command python3 -ErrorAction Stop }
    & $python.Source '.\scripts\audit\Test-RabbitMqHealthPhase3EPackage.py'
    if ($LASTEXITCODE -ne 0) { throw 'Phase 3E static package validation failed.' }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "Required command 'dotnet' was not found on PATH."
    }

    if (-not $SkipRestore) { Invoke-DotNet @('restore', $solution) }
    if (-not $SkipBuild) {
        Invoke-DotNet @('build', $solution, '-c', 'Release', '--no-restore', '--nologo', '/nodeReuse:false')
    }

    Invoke-DotNet @(
        'test', $backofficeTests,
        '-c', 'Release', '--no-restore', '--no-build', '--nologo',
        '--filter', 'FullyQualifiedName~RabbitMqManagement|FullyQualifiedName~RuntimeObservabilityServiceTests'
    )
    Invoke-DotNet @(
        'test', $sharedTests,
        '-c', 'Release', '--no-restore', '--no-build', '--nologo',
        '--filter', 'FullyQualifiedName~RabbitMqOptionsTests|FullyQualifiedName~PrivateCertificateAuthorityValidatorTests'
    )

    Write-Host 'PHASE3E_MANAGEMENT_HTTPS_PRIVATE_CA_PROVED'
    Write-Host 'PHASE3E_VALIDATION=PASS'
}
finally {
    Pop-Location
}
