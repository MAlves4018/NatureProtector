<#
.SYNOPSIS
Runs the PostgreSQL control-plane bootstrap.

.DESCRIPTION
Builds the solution, resolves the PostgreSQL target from process environment
or the repository `.env`, verifies that exact target is reachable, prepares the
repository .NET environment, and runs NatureProtector.Postgres.Bootstrap.

The configuration precedence is the same as the application runtime:
process environment -> repository `.env` -> documented local fallback.

.PARAMETER SkipBuild
Skips the solution build. The bootstrap output for the selected configuration
must already exist.

.PARAMETER Configuration
Build configuration used for both build and run. Defaults to Release so local
bootstrap follows the same posture as validation commands.
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Find-NpRepositoryRoot -StartPath $PSScriptRoot -RequiredPaths @(
    'NatureProtector.sln',
    'NuGet.Config',
    'src/NatureProtector.Postgres.Bootstrap/NatureProtector.Postgres.Bootstrap.csproj'
)
$projectPath = Join-Path $repoRoot 'src/NatureProtector.Postgres.Bootstrap/NatureProtector.Postgres.Bootstrap.csproj'
$solutionPath = Join-Path $repoRoot 'NatureProtector.sln'
$bootstrapDllPath = Join-Path $repoRoot "src/NatureProtector.Postgres.Bootstrap/bin/$Configuration/net9.0/NatureProtector.Postgres.Bootstrap.dll"
$dotEnvPath = Join-Path $repoRoot '.env'
$dotEnv = Read-NpDotEnv -Path $dotEnvPath -Required -MissingFileMessage ".env is missing. Run '.\scripts\np.ps1 init-local -Force' before bootstrapping PostgreSQL."

$postgresHost = Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_HOST' -Fallback 'localhost' -EnvironmentFirst
$postgresPortText = Get-NpConfigValue -Values $dotEnv -Name 'POSTGRES_PORT' -Fallback '5433' -EnvironmentFirst
$postgresPort = 0
if (-not [int]::TryParse($postgresPortText, [ref]$postgresPort) -or $postgresPort -lt 1 -or $postgresPort -gt 65535) {
    throw "POSTGRES_PORT must be an integer between 1 and 65535. Effective value: '$postgresPortText'."
}

Write-Host "Checking configured PostgreSQL target $postgresHost`:$postgresPort..."
if (-not (Test-NpTcpEndpoint -HostName $postgresHost -Port $postgresPort -TimeoutMilliseconds 3000)) {
    throw "PostgreSQL is not reachable at configured target $postgresHost`:$postgresPort. Check process environment, .env and Docker port mappings."
}

Push-Location $repoRoot
try {
    & (Join-Path $repoRoot 'scripts/dotnet/Use-RepoDotnetEnvironment.ps1') -Quiet | Out-Null

    if (-not $SkipBuild) {
        & dotnet build $solutionPath `
            -c $Configuration `
            --nologo `
            -v minimal `
            -m:1 `
            --configfile (Join-Path $repoRoot 'NuGet.Config')

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE. Bootstrap execution is blocked to avoid running stale binaries."
        }
    }

    if (-not (Test-Path -LiteralPath $bootstrapDllPath -PathType Leaf)) {
        throw "Bootstrap output not found at $bootstrapDllPath. Run without -SkipBuild or build the project in $Configuration first."
    }

    if (-not $env:NP_BOOTSTRAP_ADMIN_PASSWORD) {
        $adminPassword = Get-NpConfigValue -Values $dotEnv -Name 'NP_BOOTSTRAP_ADMIN_PASSWORD' -Fallback '' -EnvironmentFirst
        if (-not [string]::IsNullOrWhiteSpace($adminPassword)) {
            $env:NP_BOOTSTRAP_ADMIN_PASSWORD = $adminPassword
        }
    }

    & dotnet run `
        --project $projectPath `
        -c $Configuration `
        --no-build `
        --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL bootstrap failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
