param(
    [string]$RepositoryRoot = "."
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

$trackedFiles = & git -C $repoRoot ls-files
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$findings = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $trackedFiles) {
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

Write-Host "No secret canaries found in tracked files."
