[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$Failures = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$Message) { $Failures.Add($Message) }
function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $Path) -PathType Leaf)) { Add-Failure "Missing file: $Path" }
}
function Invoke-ExpectExit([string]$Name, [int]$ExpectedExitCode, [scriptblock]$Command) {
    & $Command | Out-Null
    if ($LASTEXITCODE -ne $ExpectedExitCode) { Add-Failure "$Name expected exit $ExpectedExitCode but got $LASTEXITCODE" }
}

@(
    "scripts/np.ps1",
    "deploy/environments/common.json",
    "deploy/environments/staging.json",
    "deploy/environments/production.json",
    "deploy/schemas/environment.schema.json",
    "deploy/schemas/operation-result.schema.json",
    ".githooks/pre-commit",
    ".githooks/pre-push",
    "scripts/dev/Install-GitHooks.ps1",
    "scripts/dev/Test-GitHookPolicy.ps1",
    ".github/workflows/ci.yml",
    ".github/workflows/cd-staging.yml",
    ".github/workflows/open-staging.yml",
    ".github/workflows/rollback-staging.yml",
    ".github/workflows/teardown-staging.yml",
    ".github/workflows/_validate.yml",
    ".github/workflows/_release.yml",
    ".github/workflows/_deploy.yml",
    ".github/workflows/_qualify.yml",
    ".github/actions/setup-toolchain/action.yml",
    ".github/actions/cloud-auth/action.yml",
    ".github/actions/resolve-release/action.yml",
    ".github/actions/collect-evidence/action.yml"
) | ForEach-Object { Require-File $_ }

$staging = Get-Content -Raw -LiteralPath (Join-Path $RepoRoot "deploy/environments/staging.json") | ConvertFrom-Json
$production = Get-Content -Raw -LiteralPath (Join-Path $RepoRoot "deploy/environments/production.json") | ConvertFrom-Json
if ($staging.project_id -ne "natureprotector-500518" -or $staging.region -ne "europe-southwest1") { Add-Failure "Staging project/region guard mismatch." }
if ($staging.default_ttl_hours -ne 4) { Add-Failure "Staging TTL is not 4 hours." }
if ($production.deployable -ne $false) { Add-Failure "Production must remain non-deployable." }

$np = Get-Content -Raw -LiteralPath (Join-Path $RepoRoot "scripts/np.ps1")
foreach ($token in @("Remove-SecretText", "BILLING_ENV_SET", "natureprotector-500518", "europe-southwest1", "AUTHORIZE_EPHEMERAL_STAGING_APPLY_MAX_20_EUR_TTL_4H")) {
    if ($np -notmatch [regex]::Escape($token)) { Add-Failure "np.ps1 missing token: $token" }
}
if ($np -match '(?i)[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}') { Add-Failure "np.ps1 contains concrete Billing Account ID." }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("np-hook-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
    $ok = Join-Path $tmp "ok.json"
    '{"ok":true}' | Set-Content -LiteralPath $ok -Encoding utf8
    Invoke-ExpectExit "hook-valid-json" 0 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/dev/Test-GitHookPolicy.ps1") -Path $ok }

    $large = Join-Path $tmp "large.bin"
    [IO.File]::WriteAllBytes($large, (New-Object byte[] 32))
    Invoke-ExpectExit "hook-large-file" 1 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/dev/Test-GitHookPolicy.ps1") -Path $large -LargeFileLimitBytes 8 }

    $billing = Join-Path $tmp "billing.txt"
    ("ABCDEF" + "-123456-" + "7890AB") | Set-Content -LiteralPath $billing -Encoding utf8
    Invoke-ExpectExit "hook-billing-id" 1 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/dev/Test-GitHookPolicy.ps1") -Path $billing }

    $key = Join-Path $tmp "key.json"
    ('{"type":"' + 'service_' + 'account"}') | Set-Content -LiteralPath $key -Encoding utf8
    Invoke-ExpectExit "hook-service-account" 1 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/dev/Test-GitHookPolicy.ps1") -Path $key }
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Invoke-ExpectExit "np-doctor" 0 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/np.ps1") -EvidenceRoot (Join-Path ([IO.Path]::GetTempPath()) "np-standard-cd-test-evidence") doctor }
Invoke-ExpectExit "np-inventory" 0 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/np.ps1") -EvidenceRoot (Join-Path ([IO.Path]::GetTempPath()) "np-standard-cd-test-evidence") inventory }
Invoke-ExpectExit "np-staging-open-whatif" 0 { pwsh -NoProfile -File (Join-Path $RepoRoot "scripts/np.ps1") -EvidenceRoot (Join-Path ([IO.Path]::GetTempPath()) "np-standard-cd-test-evidence") staging open -TtlHours 4 -WhatIf }

if ($Failures.Count -gt 0) {
    Write-Host "STANDARD_CD_STATIC_FAIL"
    $Failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "STANDARD_CD_STATIC_PASS"
