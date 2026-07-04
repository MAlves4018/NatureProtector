$ErrorActionPreference = "Stop"

function Reset-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
        return
    }

    Get-ChildItem -LiteralPath $Path -Force | Remove-Item -Recurse -Force
}

$repoRoot = Get-NpAbsolutePath -Path (Join-Path $PSScriptRoot "..\..")
$docfxRoot = Join-Path $repoRoot "docs\docfx"
$docfxConfig = Join-Path $docfxRoot "docfx.json"
$docfxArtifactsRoot = Join-Path $docfxRoot "artifacts"
$docfxApiInput = Join-Path $docfxArtifactsRoot "api-input"
$docfxApi = Join-Path $docfxRoot "api"
$docfxOutput = Join-Path $docfxRoot "output"
$srcRoot = Join-Path $repoRoot "src"
$solutionPath = Join-Path $repoRoot "NatureProtector.sln"
$nugetConfig = Join-Path $repoRoot "NuGet.Config"

Assert-NpPathExists -Path $repoRoot -Description "Repository root" -ExpectDirectory $true
Assert-NpPathExists -Path $docfxRoot -Description "DocFX root" -ExpectDirectory $true
Assert-NpPathExists -Path $docfxConfig -Description "DocFX configuration" -ExpectDirectory $false
Assert-NpPathExists -Path $srcRoot -Description "Source root" -ExpectDirectory $true
Assert-NpPathExists -Path $solutionPath -Description "Solution file" -ExpectDirectory $false
Assert-NpPathExists -Path $nugetConfig -Description "NuGet configuration" -ExpectDirectory $false

foreach ($directory in @($docfxArtifactsRoot, $docfxApiInput, $docfxApi, $docfxOutput)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCommand) {
    throw "The 'dotnet' executable is not available on PATH."
}

$docfxCommand = Get-Command docfx -ErrorAction SilentlyContinue
if (-not $docfxCommand) {
    throw "The 'docfx' executable is not available on PATH."
}

Write-Host "Building solution for DocFX metadata..."
& $dotnetCommand.Source build $solutionPath --configuration Release --nologo -v minimal --configfile $nugetConfig

Reset-DirectoryContents -Path $docfxApiInput

$projectDirectories = Get-ChildItem -Path $srcRoot -Directory | Sort-Object Name
$copiedAssemblies = 0

foreach ($projectDirectory in $projectDirectories) {
    $projectOutput = Join-Path $projectDirectory.FullName "bin\Release\net9.0"

    if (-not (Test-Path -LiteralPath $projectOutput)) {
        continue
    }

    $assemblyFiles = Get-ChildItem -Path $projectOutput -File -Filter "NatureProtector*.dll" |
        Where-Object { $_.Name -notlike "*.Tests.dll" }

    foreach ($assemblyFile in $assemblyFiles) {
        $targetDirectory = Join-Path $docfxApiInput $projectDirectory.Name
        New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null

        Copy-Item -LiteralPath $assemblyFile.FullName -Destination (Join-Path $targetDirectory $assemblyFile.Name) -Force

        $xmlPath = [System.IO.Path]::ChangeExtension($assemblyFile.FullName, ".xml")
        if (Test-Path -LiteralPath $xmlPath) {
            Copy-Item -LiteralPath $xmlPath -Destination (Join-Path $targetDirectory ([System.IO.Path]::GetFileName($xmlPath))) -Force
        }

        $copiedAssemblies++
    }
}

if ($copiedAssemblies -eq 0) {
    throw "No Release assemblies were collected for DocFX metadata under $srcRoot"
}

Push-Location $docfxRoot

try {
    Write-Host "Generating DocFX metadata..."
    & $docfxCommand.Source metadata docfx.json

    Write-Host "Building DocFX site..."
    & $docfxCommand.Source build docfx.json
}
finally {
    Pop-Location
}

Write-Host "DocFX site generated at:"
Write-Host (Join-Path $docfxOutput "index.html")
