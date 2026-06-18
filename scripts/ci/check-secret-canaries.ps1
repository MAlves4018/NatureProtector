param(
    [string]$RepositoryRoot = ".",
    [switch]$NoGit,
    [long]$MaxFileSizeBytes = 1048576
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path $RepositoryRoot).Path
$patterns = [ordered]@{
    GitHubClassicToken = "gh[pousr]_[A-Za-z0-9_]{36,}"
    GitHubFineGrainedToken = "github_pat_[A-Za-z0-9_]{40,}"
    AwsAccessKey = "AKIA[0-9A-Z]{16}"
    OpenAiToken = "sk-[A-Za-z0-9]{20,}"
    SlackToken = "xox[baprs]-[A-Za-z0-9-]{20,}"
    PrivateKey = "-----BEGIN (RSA |DSA |EC |OPENSSH |)??PRIVATE KEY-----"
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$Path
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString(), [System.StringComparison]::Ordinal)) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($baseFullPath)
    $pathUri = [System.Uri]::new([System.IO.Path]::GetFullPath($Path))
    $relativeUri = $baseUri.MakeRelativeUri($pathUri)

    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-RepositoryFiles {
    if (-not $NoGit) {
        $tracked = & git -C $repoRoot ls-files
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        return @($tracked | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    $excludedRoots = @(
        ".git",
        "artifacts",
        "data",
        "docs/architecture/images",
        "docs/evidence",
        "docs/doxygen",
        "docs/planning",
        "docs/report",
        "docs/structurizr",
        "graphify-out",
        "webUI/node_modules",
        "webUI/dist",
        "webUI/coverage",
        "webUI/test-results",
        "webUI/playwright-report"
    )
    $excludedExtensions = @(
        ".dll",
        ".exe",
        ".gif",
        ".gpkg",
        ".jpeg",
        ".jpg",
        ".log",
        ".parquet",
        ".pdb",
        ".pdf",
        ".png",
        ".trx",
        ".webp",
        ".zip"
    )

    return @(
        Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
            Where-Object {
                $relative = Get-RelativePath $repoRoot $_.FullName
                $normalized = $relative -replace '\\', '/'
                $include = $true

                if ($normalized.Equals(".env", [System.StringComparison]::OrdinalIgnoreCase) -or
                    $normalized.EndsWith("/.env", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $include = $false
                }

                if ($normalized -match '(^|/)(bin|obj)/') {
                    $include = $false
                }

                if ($include -and $_.Length -gt $MaxFileSizeBytes) {
                    $include = $false
                }

                if ($include -and $excludedExtensions -contains $_.Extension.ToLowerInvariant()) {
                    $include = $false
                }

                if ($include) {
                    foreach ($excludedRoot in $excludedRoots) {
                        if ($normalized.Equals($excludedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
                            $normalized.StartsWith("$excludedRoot/", [System.StringComparison]::OrdinalIgnoreCase)) {
                            $include = $false
                            break
                        }
                    }
                }

                $include
            } |
            ForEach-Object {
                (Get-RelativePath $repoRoot $_.FullName) -replace '\\', '/'
            }
    )
}

$repositoryFiles = Get-RepositoryFiles
$findings = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $repositoryFiles) {
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    foreach ($entry in $patterns.GetEnumerator()) {
        $matches = Select-String -LiteralPath $fullPath -Pattern $entry.Value -AllMatches -ErrorAction SilentlyContinue
        foreach ($match in $matches) {
            $findings.Add("$($entry.Key):${relativePath}:$($match.LineNumber)")
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Error "Potential secret canaries found:`n$($findings -join [Environment]::NewLine)"
    exit 1
}

$scopeLabel = if ($NoGit) { "repository snapshot files" } else { "tracked files" }
Write-Host "No secret canaries found in $scopeLabel."
