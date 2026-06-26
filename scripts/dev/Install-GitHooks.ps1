[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$HooksPath = Join-Path $RepoRoot ".githooks"

if (-not (Test-Path -LiteralPath $HooksPath -PathType Container)) {
    throw "Missing hooks directory: $HooksPath"
}

if ($PSCmdlet.ShouldProcess($RepoRoot, "configure core.hooksPath=.githooks")) {
    & git -C $RepoRoot config core.hooksPath .githooks
    if ($LASTEXITCODE -ne 0) { throw "Failed to configure Git hooks path." }
}

Write-Host "Git hooks configured: .githooks"
