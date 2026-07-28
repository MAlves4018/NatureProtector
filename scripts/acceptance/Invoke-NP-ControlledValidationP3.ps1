[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [string]$ApiBaseUrl = 'http://127.0.0.1:5254',
    [string]$PythonExecutable = 'python',
    [string]$ConnectionString = $env:NP_POSTGRES_CONNECTION_STRING,
    [int]$TimeoutSeconds = 600,
    [switch]$Execute,
    [switch]$AcknowledgeNonProduction,
    [switch]$PreserveOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $dotEnvPath = Join-Path $RepoRoot '.env'
    if (Test-Path -LiteralPath $dotEnvPath) {
        $dotEnv = @{}
        foreach ($line in Get-Content -LiteralPath $dotEnvPath) {
            if ($line -match '^\s*([^#=\s]+)\s*=\s*(.*)\s*$') {
                $dotEnv[$Matches[1]] = $Matches[2].Trim().Trim('"')
            }
        }
        $requiredKeys = @('POSTGRES_HOST', 'POSTGRES_PORT', 'POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD')
        if (@($requiredKeys | Where-Object { -not $dotEnv.ContainsKey($_) -or [string]::IsNullOrWhiteSpace([string]$dotEnv[$_]) }).Count -eq 0) {
            $ConnectionString = "Host=$($dotEnv.POSTGRES_HOST);Port=$($dotEnv.POSTGRES_PORT);Database=$($dotEnv.POSTGRES_DB);Username=$($dotEnv.POSTGRES_USER);Password=$($dotEnv.POSTGRES_PASSWORD)"
        }
    }
}
$ArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts'))
$P3OutputBase = [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'acceptance\controlled-validation-p3'))
$FinalAcceptanceBase = [System.IO.Path]::GetFullPath((Join-Path $ArtifactsRoot 'final-acceptance'))
$runId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $P3OutputBase $runId
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot $OutputRoot
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$p3Prefix = $P3OutputBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$finalAcceptancePrefix = $FinalAcceptanceBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$isStandaloneRun = -not $OutputRoot.Equals($P3OutputBase, [System.StringComparison]::OrdinalIgnoreCase) -and
    ($OutputRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($p3Prefix, [System.StringComparison]::OrdinalIgnoreCase)
$isOrchestratedRun = ($OutputRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($finalAcceptancePrefix, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $isStandaloneRun -and -not $isOrchestratedRun) {
    throw "OutputRoot must be a run-scoped child of $P3OutputBase or an orchestrated final-acceptance component."
}
if ((Test-Path -LiteralPath $OutputRoot) -and -not $PreserveOutput) {
    Get-ChildItem -LiteralPath $OutputRoot -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$acceptancePath = Join-Path $OutputRoot 'acceptance-result.json'
$startedAt = (Get-Date).ToUniversalTime()
$nativeStatus = 'NOT_STARTED'
$normalizedStatus = 'HARNESS_ERROR'
$detail = ''
$controlledRunLabel = "controlled-validation-p3-negative-pipeline-$runId-acceptance"

function Write-AcceptanceResult {
    param([string]$Status, [string]$NativeStatus, [string]$Detail)

    [ordered]@{
        schemaVersion = 1
        component = 'controlled-validation-p3-audited'
        status = $Status
        nativeStatus = $NativeStatus
        detail = $Detail
        runLabel = $controlledRunLabel
        startedAtUtc = $startedAt.ToString('o')
        completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        outputRoot = $OutputRoot
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $acceptancePath -Encoding utf8
}

try {
    if (-not $Execute -or -not $AcknowledgeNonProduction) {
        $normalizedStatus = 'BLOCKED_PREREQUISITE'
        $nativeStatus = 'BLOCKED_EXPLICIT_CONFIRMATION_REQUIRED'
        $detail = 'Both -Execute and -AcknowledgeNonProduction are required.'
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 2
    }
    if ([string]::IsNullOrWhiteSpace($env:NP_RELIABILITY_AUTH_TOKEN)) {
        $normalizedStatus = 'BLOCKED_PREREQUISITE'
        $nativeStatus = 'BLOCKED_AUTH_TOKEN_MISSING'
        $detail = 'NP_RELIABILITY_AUTH_TOKEN is required.'
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 2
    }
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        $normalizedStatus = 'BLOCKED_PREREQUISITE'
        $nativeStatus = 'BLOCKED_POSTGRES_CONNECTION_MISSING'
        $detail = 'A PostgreSQL connection could not be derived from NP_POSTGRES_CONNECTION_STRING, -ConnectionString or the local .env file.'
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 2
    }

    if ($null -eq (Get-Command $PythonExecutable -ErrorAction SilentlyContinue)) {
        $normalizedStatus = 'BLOCKED_PREREQUISITE'
        $nativeStatus = 'BLOCKED_TOOL_MISSING'
        $detail = "Required command is unavailable: $PythonExecutable"
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 2
    }
    if ($null -eq (Get-Command psql -ErrorAction SilentlyContinue) -and $null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
        $normalizedStatus = 'BLOCKED_PREREQUISITE'
        $nativeStatus = 'BLOCKED_POSTGRES_CLIENT_MISSING'
        $detail = 'Neither local psql nor Docker is available for the PostgreSQL audit.'
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 2
    }

    $executionRoot = Join-Path $OutputRoot 'execution'
    $auditRoot = Join-Path $OutputRoot 'postgres-audit'
    $verificationRoot = Join-Path $OutputRoot 'verification'
    $logsRoot = Join-Path $OutputRoot 'logs'
    New-Item -ItemType Directory -Force -Path $executionRoot, $auditRoot, $verificationRoot, $logsRoot | Out-Null

    $p3Args = @(
        (Join-Path $RepoRoot 'scripts\reliability\run-controlled-validation-p3.py'),
        '--api-base-url', $ApiBaseUrl,
        '--run-label', $controlledRunLabel,
        '--timeout-seconds', [string]$TimeoutSeconds,
        '--output', $executionRoot,
        '--execute',
        '--acknowledge-non-production'
    )
    $p3Stdout = Join-Path $logsRoot 'p3.stdout.log'
    $p3Stderr = Join-Path $logsRoot 'p3.stderr.log'
    & $PythonExecutable @p3Args 1> $p3Stdout 2> $p3Stderr
    if ($LASTEXITCODE -ne 0) {
        $statusPath = Join-Path $executionRoot 'status.json'
        $nativeStatus = if (Test-Path -LiteralPath $statusPath) { [string]((Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json).status) } else { "P3_EXIT_$LASTEXITCODE" }
        $normalizedStatus = if ($LASTEXITCODE -in @(2, 3)) { 'BLOCKED_PREREQUISITE' } else { 'FAIL' }
        $detail = "Controlled P3 execution did not reach an auditable success state (exit=$LASTEXITCODE)."
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        if ($normalizedStatus -eq 'BLOCKED_PREREQUISITE') { exit 2 }
        exit 1
    }

    $p3StatusPath = Join-Path $executionRoot 'status.json'
    if (-not (Test-Path -LiteralPath $p3StatusPath)) { throw 'P3 status.json was not produced.' }
    $p3Status = Get-Content -LiteralPath $p3StatusPath -Raw | ConvertFrom-Json
    if ([string]$p3Status.status -ne 'PASS_AUDIT_REQUIRED') {
        $normalizedStatus = 'FAIL'
        $nativeStatus = [string]$p3Status.status
        $detail = 'P3 execution returned success code without PASS_AUDIT_REQUIRED.'
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 1
    }

    $auditArgs = @{
        ConnectionString = $ConnectionString
        OutputRoot = $auditRoot
        RunId = 'p3'
        ControlledValidationRunLabel = $controlledRunLabel
    }
    $auditStdout = Join-Path $logsRoot 'postgres-audit.stdout.log'
    $auditStderr = Join-Path $logsRoot 'postgres-audit.stderr.log'
    & (Join-Path $RepoRoot 'tools\data-audit\run-postgres-audit.ps1') @auditArgs 1> $auditStdout 2> $auditStderr
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL audit failed with exit code $LASTEXITCODE." }

    $collectorArgs = @(
        (Join-Path $RepoRoot 'scripts\evidence\collect-reliability-evidence.py'),
        '--repo', $RepoRoot,
        '--baseline-id', "acceptance-$runId",
        '--run-id', 'verified',
        '--output', $verificationRoot,
        '--api-base-url', $ApiBaseUrl,
        '--audit-directory', (Join-Path $auditRoot 'p3'),
        '--require-audit',
        '--no-latest-pointer'
    )
    $verificationStdout = Join-Path $logsRoot 'verification.stdout.log'
    $verificationStderr = Join-Path $logsRoot 'verification.stderr.log'
    & $PythonExecutable @collectorArgs 1> $verificationStdout 2> $verificationStderr
    if ($LASTEXITCODE -ne 0) { throw "Reliability audit verification failed with exit code $LASTEXITCODE." }

    $summaryPath = Join-Path $verificationRoot 'phase6-summary.json'
    if (-not (Test-Path -LiteralPath $summaryPath)) { throw 'phase6-summary.json was not produced.' }
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ([string]$summary.postgresAuditStatus -ne 'PASS') {
        $normalizedStatus = 'FAIL'
        $nativeStatus = "P3_EXECUTION_PASS_AUDIT_$($summary.postgresAuditStatus)"
        $detail = 'The exact-run PostgreSQL reconciliation did not pass.'
        Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
        exit 1
    }

    $normalizedStatus = 'PASS'
    $nativeStatus = 'P3_EXECUTION_AND_EXACT_RUN_AUDIT_PASS'
    $detail = 'Controlled P3 execution and exact-run PostgreSQL reconciliation passed.'
    Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
    exit 0
}
catch {
    $normalizedStatus = 'HARNESS_ERROR'
    $nativeStatus = 'P3_ACCEPTANCE_HARNESS_ERROR'
    $detail = $_.Exception.Message
    Write-AcceptanceResult -Status $normalizedStatus -NativeStatus $nativeStatus -Detail $detail
    [Console]::Error.WriteLine($detail)
    exit 3
}
