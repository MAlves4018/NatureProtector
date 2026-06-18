param(
    [string]$Version = "",
    [string]$OutputRoot = "artifacts/release",
    [switch]$SkipRestore,
    [switch]$SkipFrontendInstall,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $sourceRevision = $env:GITHUB_SHA
    if ([string]::IsNullOrWhiteSpace($sourceRevision)) {
        $sourceRevision = $env:BUILD_SOURCEVERSION
    }

    $revisionLabel = if ([string]::IsNullOrWhiteSpace($sourceRevision)) {
        "nogit"
    }
    else {
        $sourceRevision.Substring(0, [System.Math]::Min(12, $sourceRevision.Length))
    }

    $Version = "local-$timestamp-$revisionLabel"
}

$releaseRoot = Join-Path $repoRoot (Join-Path $OutputRoot "natureprotector-$Version")
$publishRoot = Join-Path $releaseRoot "publish"
$frontendRoot = Join-Path $releaseRoot "webUI"
$evidenceRoot = Join-Path $releaseRoot "evidence"
$dataRoot = Join-Path $releaseRoot "data"

$dotnetProjects = @(
    @{ Name = "backoffice-api"; Path = "src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.csproj" },
    @{ Name = "prevention-host"; Path = "src\NatureProtector.Prevention.Host\NatureProtector.Prevention.Host.csproj" },
    @{ Name = "simulator-host"; Path = "src\NatureProtector.Simulator.Host\NatureProtector.Simulator.Host.csproj" },
    @{ Name = "postgres-bootstrap"; Path = "src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj" }
)

$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    version = $Version
    dryRun = [bool]$DryRun
    packageRootName = "natureprotector-$Version"
    dotnetProjects = $dotnetProjects
    frontend = "webUI"
    outputs = [ordered]@{
        publish = "publish"
        frontend = "webUI"
        data = "data"
        evidence = "evidence"
        manifest = "release-evidence-manifest.json"
        internalChecksums = "checksums.sha256"
        sbom = "evidence/sbom.json"
        archive = "natureprotector-$Version.zip"
        externalArchiveChecksum = "natureprotector-$Version.zip.sha256"
    }
    integrity = [ordered]@{
        internalChecksums = "SHA-256 over every package file except checksums.sha256 itself."
        archiveChecksum = "SHA-256 over the final zip, stored outside the archive."
        signing = "Not performed locally; external signing or GitHub attestation must be added by the release environment."
    }
    scope = "Local release candidate packaging for academic/demo delivery. This does not sign artifacts or attest provenance unless external signing infrastructure is provided."
}

function Convert-DotnetInventoryToComponents {
    param([Parameter(Mandatory = $true)]$Inventory)

    $components = [System.Collections.Generic.List[object]]::new()
    foreach ($project in @($Inventory.projects)) {
        foreach ($framework in @($project.frameworks)) {
            foreach ($package in @($framework.topLevelPackages)) {
                $components.Add([pscustomobject]@{
                    type = "dotnet"
                    scope = "topLevel"
                    name = $package.id
                    version = $package.resolvedVersion
                    requestedVersion = $package.requestedVersion
                    project = $project.path
                    framework = $framework.framework
                }) | Out-Null
            }

            foreach ($package in @($framework.transitivePackages)) {
                $components.Add([pscustomobject]@{
                    type = "dotnet"
                    scope = "transitive"
                    name = $package.id
                    version = $package.resolvedVersion
                    requestedVersion = $null
                    project = $project.path
                    framework = $framework.framework
                }) | Out-Null
            }
        }
    }

    return @($components)
}

function Add-NpmComponents {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Seen,
        [Parameter(Mandatory = $true)]
        [object]$Components,
        [Parameter(Mandatory = $true)]
        [object]$Dependencies,
        [string]$Parent = ""
    )

    foreach ($property in @($Dependencies.PSObject.Properties)) {
        $dependency = $property.Value
        $name = $property.Name
        $version = if ($dependency.PSObject.Properties.Name -contains "version") { $dependency.version } else { "" }
        $key = "$name@$version"

        if (-not $Seen.Contains($key)) {
            $Seen[$key] = $true
            $Components.Add([pscustomobject]@{
                type = "npm"
                scope = "dependency"
                name = $name
                version = $version
                parent = $Parent
                resolved = if ($dependency.PSObject.Properties.Name -contains "resolved") { $dependency.resolved } else { $null }
            }) | Out-Null
        }

        if ($dependency.PSObject.Properties.Name -contains "dependencies") {
            Add-NpmComponents -Seen $Seen -Components $Components -Dependencies $dependency.dependencies -Parent $key
        }
    }
}

function New-ReleaseSbom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DotnetInventoryPath,
        [Parameter(Mandatory = $true)]
        [string]$NpmInventoryPath,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $dotnetInventory = Get-Content -LiteralPath $DotnetInventoryPath -Raw | ConvertFrom-Json
    $npmInventory = Get-Content -LiteralPath $NpmInventoryPath -Raw | ConvertFrom-Json
    $dotnetComponents = @(Convert-DotnetInventoryToComponents -Inventory $dotnetInventory)
    $npmComponents = [System.Collections.Generic.List[object]]::new()
    $seenNpm = @{}

    if ($npmInventory.PSObject.Properties.Name -contains "dependencies") {
        Add-NpmComponents -Seen $seenNpm -Components $npmComponents -Dependencies $npmInventory.dependencies
    }

    $sbom = [ordered]@{
        schema = "NatureProtector.ReleaseSbom.v1"
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        sourceInventories = @(
            "dotnet-dependency-inventory.json",
            "npm-dependency-inventory.json"
        )
        componentCount = $dotnetComponents.Count + $npmComponents.Count
        components = @($dotnetComponents + @($npmComponents))
        scope = "Local package dependency inventory derived from dotnet list package and npm ls. It is not a signed attestation."
    }

    $sbom | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}

if ($DryRun) {
    New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $releaseRoot "release-evidence-manifest.json") -Encoding UTF8
    Write-Host "Dry run complete. Manifest: $(Join-Path $releaseRoot "release-evidence-manifest.json")"
    exit 0
}

New-Item -ItemType Directory -Force -Path $publishRoot, $frontendRoot, $evidenceRoot, $dataRoot | Out-Null

if (-not $SkipRestore) {
    dotnet restore (Join-Path $repoRoot "NatureProtector.sln") --configfile (Join-Path $repoRoot "NuGet.Config") --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

foreach ($project in $dotnetProjects) {
    $projectPath = Join-Path $repoRoot $project.Path
    $outputPath = Join-Path $publishRoot $project.Name
    $publishArguments = @(
        "publish",
        $projectPath,
        "-c",
        "Release",
        "-o",
        $outputPath,
        "--nologo",
        "-v",
        "minimal"
    )
    if ($SkipRestore) {
        $publishArguments += "--no-restore"
    }

    dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$webRoot = Join-Path $repoRoot "webUI"
if (-not $SkipFrontendInstall) {
    npm --prefix $webRoot ci
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

npm --prefix $webRoot run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -Path (Join-Path $webRoot "dist\*") -Destination $frontendRoot -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot "data\*") -Destination $dataRoot -Recurse -Force

$dotnetDependencyInventory = dotnet list (Join-Path $repoRoot "NatureProtector.sln") package --include-transitive --format json
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$dotnetDependencyInventoryPath = Join-Path $evidenceRoot "dotnet-dependency-inventory.json"
$dotnetDependencyInventory | Set-Content -Path $dotnetDependencyInventoryPath -Encoding UTF8

$npmDependencyInventory = npm --prefix $webRoot ls --all --json
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$npmDependencyInventoryPath = Join-Path $evidenceRoot "npm-dependency-inventory.json"
$npmDependencyInventory | Set-Content -Path $npmDependencyInventoryPath -Encoding UTF8

New-ReleaseSbom `
    -DotnetInventoryPath $dotnetDependencyInventoryPath `
    -NpmInventoryPath $npmDependencyInventoryPath `
    -OutputPath (Join-Path $evidenceRoot "sbom.json")

if (Test-Path (Join-Path $webRoot "npm-audit.json")) {
    Copy-Item -Path (Join-Path $webRoot "npm-audit.json") -Destination (Join-Path $evidenceRoot "npm-audit.json") -Force
}

foreach ($auditArtifact in @("npm-audit.diagnostics.json", "npm-audit.policy.json", "npm-audit.exit-code.txt")) {
    $source = Join-Path $webRoot $auditArtifact
    if (Test-Path $source) {
        Copy-Item -Path $source -Destination (Join-Path $evidenceRoot $auditArtifact) -Force
    }
}

$manifest | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $releaseRoot "release-evidence-manifest.json") -Encoding UTF8

Get-ChildItem -Path $releaseRoot -File -Recurse |
    Where-Object { $_.Name -ne "checksums.sha256" } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($releaseRoot.Length).TrimStart("\", "/").Replace("\", "/")
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash.ToLowerInvariant())  $relative"
    } |
    Set-Content -Path (Join-Path $releaseRoot "checksums.sha256") -Encoding ASCII

$archivePath = Join-Path (Split-Path -Parent $releaseRoot) "natureprotector-$Version.zip"
if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $releaseRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal

$archiveHash = Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath
$archiveChecksumPath = "$archivePath.sha256"
"$($archiveHash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $archivePath)" |
    Set-Content -Path $archiveChecksumPath -Encoding ASCII

Write-Host "Release candidate built: $releaseRoot"
Write-Host "Archive: $archivePath"
Write-Host "Archive checksum: $archiveChecksumPath"
