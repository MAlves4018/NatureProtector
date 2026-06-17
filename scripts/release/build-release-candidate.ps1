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
    $gitSha = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
    if ([string]::IsNullOrWhiteSpace($gitSha)) {
        $gitSha = "nogit"
    }

    $Version = "local-$timestamp-$gitSha"
}

$releaseRoot = Join-Path $repoRoot (Join-Path $OutputRoot "natureprotector-$Version")
$publishRoot = Join-Path $releaseRoot "publish"
$frontendRoot = Join-Path $releaseRoot "webUI"
$evidenceRoot = Join-Path $releaseRoot "evidence"

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
        evidence = "evidence"
        manifest = "release-evidence-manifest.json"
        internalChecksums = "checksums.sha256"
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

if ($DryRun) {
    New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $releaseRoot "release-evidence-manifest.json") -Encoding UTF8
    Write-Host "Dry run complete. Manifest: $(Join-Path $releaseRoot "release-evidence-manifest.json")"
    exit 0
}

New-Item -ItemType Directory -Force -Path $publishRoot, $frontendRoot, $evidenceRoot | Out-Null

if (-not $SkipRestore) {
    dotnet restore (Join-Path $repoRoot "NatureProtector.sln") --configfile (Join-Path $repoRoot "NuGet.Config") --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

foreach ($project in $dotnetProjects) {
    $projectPath = Join-Path $repoRoot $project.Path
    $outputPath = Join-Path $publishRoot $project.Name
    dotnet publish $projectPath -c Release -o $outputPath --nologo -v minimal
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

dotnet list (Join-Path $repoRoot "NatureProtector.sln") package --include-transitive --format json |
    Set-Content -Path (Join-Path $evidenceRoot "dotnet-dependency-inventory.json") -Encoding UTF8

npm --prefix $webRoot ls --all --json |
    Set-Content -Path (Join-Path $evidenceRoot "npm-dependency-inventory.json") -Encoding UTF8

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
