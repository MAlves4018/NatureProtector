<#
.SYNOPSIS
Runs the PostgreSQL control-plane bootstrap.

.DESCRIPTION
Builds the solution, verifies that local PostgreSQL is reachable, prepares the
repository .NET environment, and runs NatureProtector.Postgres.Bootstrap.

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
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$projectPath = Join-Path $repoRoot "src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj"
$solutionPath = Join-Path $repoRoot "NatureProtector.sln"
$bootstrapDllPath = Join-Path $repoRoot "src\NatureProtector.Postgres.Bootstrap\bin\$Configuration\net9.0\NatureProtector.Postgres.Bootstrap.dll"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Bootstrap project not found at $projectPath"
}

if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "Solution not found at $solutionPath"
}

$postgresReachable = Test-NetConnection -ComputerName "localhost" -Port 5433 -InformationLevel Quiet

if (-not $postgresReachable) {
    throw "PostgreSQL is not reachable at localhost:5433. Start the Docker Compose 'postgres' service first or adjust the local endpoint configuration."
}

Push-Location $repoRoot
try {
    & (Join-Path $repoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1") -Quiet | Out-Null

    if (-not $SkipBuild) {
        dotnet build .\NatureProtector.sln -c $Configuration --nologo -v minimal -m:1 --configfile NuGet.Config

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build .\\NatureProtector.sln -c $Configuration --nologo -v minimal -m:1 --configfile NuGet.Config failed with exit code $LASTEXITCODE. Bootstrap execution is blocked to avoid running stale binaries."
        }
    }

    if (-not (Test-Path -LiteralPath $bootstrapDllPath)) {
        throw "Bootstrap output not found at $bootstrapDllPath. Run without -SkipBuild or build the project in $Configuration first."
    }

    dotnet run --project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj -c $Configuration --no-build --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run --project .\\src\\NatureProtector.Postgres.Bootstrap\\NatureProtector.Postgres.Bootstrap.csproj -c $Configuration --no-build --no-restore failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
