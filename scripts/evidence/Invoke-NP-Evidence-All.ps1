<#
.SYNOPSIS
Runs the NatureProtector evidence harness.

.DESCRIPTION
Sequential wrapper for EVC evidence collection. Writes all run outputs under EvidenceRoot/RunId.
Does not promote claims automatically. Does not execute cloud/deploy/destroy.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$RdRoot,
    [Parameter(Mandatory=$true)][string]$EvidenceRoot,
    [Parameter(Mandatory=$true)][string]$RunId,
    [ValidateSet('DryRun','Formal')][string]$Mode='DryRun',
    [string]$ReadinessRoot,
    [switch]$ContinueOnFailure,
    [switch]$SkipRuntime,
    [switch]$SkipScenarios,
    [switch]$SkipUi,
    [switch]$SkipCoverage,
    [switch]$SkipObservability,
    [switch]$SkipCompression,
    [switch]$NoSecretValues,
    [switch]$WhatIfSafe
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RunRoot = Join-Path $EvidenceRoot $RunId
$LogRoot = Join-Path $RunRoot 'logs'

New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
New-Item -ItemType Directory -Force -Path $LogRoot | Out-Null

$folders = @(
    '00-baseline',
    '01-build-test',
    '02-coverage',
    '03-data-schema',
    '04-runtime',
    '05-api',
    '06-scenarios',
    '07-ui',
    '08-observability',
    '09-manual',
    '10-summaries',
    'artifacts',
    'logs'
)

foreach ($folder in $folders) {
    New-Item -ItemType Directory -Force -Path (Join-Path $RunRoot $folder) | Out-Null
}

$commandLog = Join-Path $RunRoot 'COMMANDS-RUN.md'
"# Commands Run`n`nRunId: $RunId`nMode: $Mode`n" | Set-Content -LiteralPath $commandLog -Encoding UTF8

function Add-CommandLog {
    param([string]$Text)
    Add-Content -LiteralPath $commandLog -Value "`n## $Text" -Encoding UTF8
}

function Invoke-NPHarnessStep {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$ScriptName,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )

    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    $outLog = Join-Path $LogRoot "$Name.stdout-stderr.log"
    $exitLog = Join-Path $LogRoot "$Name.exit-code.txt"
    $errLog = Join-Path $LogRoot "$Name-harness-error.log"

    if (-not (Test-Path -LiteralPath $scriptPath)) {
        $message = "Missing script: $scriptPath"
        $message | Set-Content -LiteralPath $errLog -Encoding UTF8
        "1" | Set-Content -LiteralPath $exitLog -Encoding UTF8
        if (-not $ContinueOnFailure) { throw $message }
        return
    }

    $cmdText = 'powershell -ExecutionPolicy Bypass -File ' + $scriptPath + ' ' + ($Arguments -join ' ')
    Add-CommandLog ($Name + [Environment]::NewLine + $cmdText)

    try {
        $processArgs = @('-ExecutionPolicy','Bypass','-File',$scriptPath) + $Arguments
        $output = & powershell @processArgs 2>&1
        $exitCode = if ($null -ne $LASTEXITCODE) { [int]$LASTEXITCODE } else { 0 }

        ($output | Out-String) | Set-Content -LiteralPath $outLog -Encoding UTF8
        "$exitCode" | Set-Content -LiteralPath $exitLog -Encoding UTF8

        if ($exitCode -ne 0) {
            $message = "$Name exited with code $exitCode"
            $message | Set-Content -LiteralPath $errLog -Encoding UTF8
            if (-not $ContinueOnFailure) { throw $message }
        }
    }
    catch {
        $_.Exception.Message | Set-Content -LiteralPath $errLog -Encoding UTF8
        "1" | Set-Content -LiteralPath $exitLog -Encoding UTF8
        if (-not $ContinueOnFailure) { throw }
    }
}

function CommonArgs {
    param([switch]$WithRepoRoot)

    $args = @(
        '-EvidenceRoot', $EvidenceRoot,
        '-RunId', $RunId,
        '-Mode', $Mode
    )

    if ($WithRepoRoot) {
        $args += @('-RepoRoot', $RepoRoot)
    }

    if ($ContinueOnFailure) {
        $args += '-ContinueOnFailure'
    }

    return $args
}

$readinessArgs = CommonArgs
if (-not [string]::IsNullOrWhiteSpace($ReadinessRoot)) {
    $readinessArgs += @('-ReadinessRoot', $ReadinessRoot)
}

Invoke-NPHarnessStep -Name 'Readiness' -ScriptName 'Test-NP-EvidenceReadiness.ps1' -Arguments $readinessArgs

Invoke-NPHarnessStep -Name 'Baseline' -ScriptName 'Invoke-NP-Evidence-Baseline.ps1' -Arguments (CommonArgs -WithRepoRoot)

Invoke-NPHarnessStep -Name 'Tests' -ScriptName 'Invoke-NP-Evidence-Tests.ps1' -Arguments (CommonArgs -WithRepoRoot)

if (-not $SkipCoverage) {
    Invoke-NPHarnessStep -Name 'Coverage' -ScriptName 'Invoke-NP-Evidence-Coverage.ps1' -Arguments (CommonArgs -WithRepoRoot)
}

Invoke-NPHarnessStep -Name 'DataSchema' -ScriptName 'Invoke-NP-Evidence-DataSchema.ps1' -Arguments (CommonArgs -WithRepoRoot)

if (-not $SkipRuntime) {
    Invoke-NPHarnessStep -Name 'Runtime' -ScriptName 'Invoke-NP-Evidence-Runtime.ps1' -Arguments (CommonArgs -WithRepoRoot)
}

$apiArgs = CommonArgs -WithRepoRoot
$apiArgs += @('-ApiBaseUrl','http://localhost:5254')
Invoke-NPHarnessStep -Name 'Api' -ScriptName 'Invoke-NP-Evidence-Api.ps1' -Arguments $apiArgs

if (-not $SkipScenarios) {
    Invoke-NPHarnessStep -Name 'Scenarios' -ScriptName 'Invoke-NP-Evidence-Scenarios.ps1' -Arguments (CommonArgs -WithRepoRoot)
}

if (-not $SkipUi) {
    Invoke-NPHarnessStep -Name 'Ui' -ScriptName 'Invoke-NP-Evidence-Ui.ps1' -Arguments (CommonArgs -WithRepoRoot)
}

if (-not $SkipObservability) {
    Invoke-NPHarnessStep -Name 'Observability' -ScriptName 'Invoke-NP-Evidence-Observability.ps1' -Arguments (CommonArgs -WithRepoRoot)
}

Invoke-NPHarnessStep -Name 'Summarize' -ScriptName 'Summarize-NP-Evidence.ps1' -Arguments (CommonArgs)

if (-not $SkipCompression) {
    Invoke-NPHarnessStep -Name 'Compress' -ScriptName 'Compress-NP-Evidence.ps1' -Arguments (CommonArgs)
}

$summaryLines = @(
    '# Invoke-NP-Evidence-All Summary',
    '',
    "RunId: $RunId",
    "Mode: $Mode",
    "RepoRoot: $RepoRoot",
    "RdRoot: $RdRoot",
    "EvidenceRoot: $EvidenceRoot",
    "ReadinessRoot: $ReadinessRoot",
    '',
    'This wrapper only orchestrates evidence scripts.',
    'Claims are not promoted automatically.',
    'Cloud/deploy/destroy commands are not executed by this wrapper.'
)

$summaryPath = Join-Path $RunRoot '10-summaries/INVOKE-ALL-SUMMARY.md'
$summaryLines | Set-Content -LiteralPath $summaryPath -Encoding UTF8
