[CmdletBinding()]
param(
    [ValidateSet("pre-commit", "pre-push")]
    [string]$Mode = "pre-commit",
    [string[]]$Path,
    [int]$LargeFileLimitBytes = 5242880
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$Failures = New-Object System.Collections.Generic.List[string]
$BillingAccountPattern = '(?i)(?<!<)[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}(?!>)'
$ServiceAccountJsonPattern = '"type"\s*:\s*"' + 'service_' + 'account"'
$PrivateKeyPattern = '-----BEGIN (RSA |EC |OPENSSH )?' + 'PRIVATE ' + 'KEY-----'
$TokenLikePattern = '(?i)(authorization:\s*bearer|ya29\.|ghp_[A-Za-z0-9_]+|token\s*[:=]\s*["''][^"'']+)'

function Add-Failure([string]$Message) {
    $Failures.Add($Message)
}

function Get-HookFiles {
    if ($Path -and $Path.Count -gt 0) { return $Path }
    $args = if ($Mode -eq "pre-commit") { @("diff", "--cached", "--name-only", "--diff-filter=ACMR") } else { @("diff", "--name-only", "HEAD") }
    $files = & git @args
    if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate files for $Mode." }
    return @($files | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-JsonFile([string]$File) {
    try { Get-Content -Raw -LiteralPath $File | ConvertFrom-Json | Out-Null }
    catch { Add-Failure "Invalid JSON: $File" }
}

function Test-YamlFile([string]$File) {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) {
        Add-Failure "YAML parser unavailable for $File"
        return
    }
    $script = "import pathlib, sys, yaml; list(yaml.safe_load_all(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8')))"
    & $python.Source -c $script $File
    if ($LASTEXITCODE -ne 0) { Add-Failure "Invalid YAML: $File" }
}

$files = @(Get-HookFiles)
foreach ($relative in $files) {
    if ([string]::IsNullOrWhiteSpace($relative)) { continue }
    $full = if ([IO.Path]::IsPathRooted($relative)) { $relative } else { Join-Path $RepoRoot $relative }
    $displayPath = if ([IO.Path]::IsPathRooted($relative)) {
        try { [IO.Path]::GetRelativePath($RepoRoot, $full) } catch { $relative }
    } else {
        $relative
    }
    $normalized = $displayPath.Replace('\', '/')

    if ($normalized -match '(^|/)\.terraform(/|$)') { Add-Failure "Blocked Terraform cache path: $displayPath" }
    if ($normalized -match '\.tfstate(\.|$)|\.tfstate$') { Add-Failure "Blocked Terraform state file: $displayPath" }
    if ($normalized -match '(?i)(^|/)(providers?|bin|obj)/|\.exe$|\.dll$|\.pdb$') { Add-Failure "Blocked binary/provider artifact: $displayPath" }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }

    $item = Get-Item -LiteralPath $full
    if ($item.Length -gt $LargeFileLimitBytes) { Add-Failure "Blocked large file: $displayPath ($($item.Length) bytes)" }

    $text = Get-Content -Raw -LiteralPath $full -ErrorAction SilentlyContinue
    if ($text -match $BillingAccountPattern) { Add-Failure "Blocked Billing Account ID pattern: $displayPath" }
    if ($text -match $ServiceAccountJsonPattern) { Add-Failure "Blocked service-account JSON key: $displayPath" }
    if ($text -match $PrivateKeyPattern) { Add-Failure "Blocked private key: $displayPath" }
    if ($text -match $TokenLikePattern) { Add-Failure "Blocked token-like secret: $displayPath" }

    switch ([IO.Path]::GetExtension($full).ToLowerInvariant()) {
        ".json" { Test-JsonFile $full }
        ".yml" { Test-YamlFile $full }
        ".yaml" { Test-YamlFile $full }
    }
}

if ($Mode -eq "pre-push") {
    $commands = @(
        @("pwsh", @("-NoProfile", "-File", "scripts/np.ps1", "validate", "-WhatIf")),
        @("terraform", @("-chdir=infra/gcp/terraform/g8-1-state-bootstrap", "fmt", "-check", "-recursive")),
        @("terraform", @("-chdir=infra/gcp/terraform/g8-1-platform", "fmt", "-check", "-recursive")),
        @("terraform", @("-chdir=infra/gcp/terraform/g8-1-environment", "fmt", "-check", "-recursive"))
    )
    foreach ($command in $commands) {
        $tool = Get-Command $command[0] -ErrorAction SilentlyContinue
        if (-not $tool) {
            Add-Failure "Required pre-push tool missing: $($command[0])"
            continue
        }
        & $tool.Source @($command[1])
        if ($LASTEXITCODE -ne 0) { Add-Failure "Pre-push command failed: $($command[0]) $($command[1] -join ' ')" }
    }
}

if ($Failures.Count -gt 0) {
    Write-Host "HOOK_POLICY_FAIL"
    $Failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "HOOK_POLICY_PASS"
exit 0
