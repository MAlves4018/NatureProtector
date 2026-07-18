[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselineId,
    [ValidateSet("plan", "static", "quality", "full")][string]$Profile = "plan",
    [string]$RunId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"),
    [string]$PythonExecutable = "python",
    [string]$ApiBaseUrl = "http://localhost:5254",
    [string]$PostgresDsnEnvironmentVariable = "NATUREPROTECTOR_POSTGRES_DSN",
    [switch]$Execute,
    [switch]$ContinueOnError,
    [switch]$IncludeE2E,
    [switch]$SkipNpmCi,
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$RequireLiveDatabase,
    [switch]$RequireLiveRuntime,
    [switch]$RequireDatabaseTrace,
    [switch]$ResetRuntime,
    [switch]$RunHttp,
    [ValidateSet("Calibration", "B0", "B1", "B2")][string]$HttpProfile = "B1",
    [switch]$IncludeWeb,
    [switch]$RunMicrobenchmarks,
    [ValidateSet("B0", "B1", "B2")][string]$BenchmarkProfile = "B1",
    [string]$SystemRunDirectory,
    [switch]$RequireHttp,
    [switch]$RequireMicrobenchmarks,
    [switch]$RequireSystem,
    [switch]$ExecuteP3,
    [switch]$AcknowledgeNonProduction,
    [string]$P3RunLabel,
    [string]$AuditDirectory,
    [switch]$RequireP3,
    [switch]$RequireAudit,
    [int]$NpScoreBootstrapIterations = 500,
    [string]$EvidenceClosureConfig = "config/evidence/evidence-gap-closure.json"
)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$runner = Join-Path $PSScriptRoot "run-report-evidence-campaign.py"
$verifier = Join-Path $PSScriptRoot "verify-report-evidence-campaign.py"
$argsList = @(
    $runner, "--repo", $repoRoot, "--baseline-id", $BaselineId,
    "--profile", $Profile, "--run-id", $RunId,
    "--api-base-url", $ApiBaseUrl,
    "--postgres-dsn-env", $PostgresDsnEnvironmentVariable,
    "--http-profile", $HttpProfile,
    "--benchmark-profile", $BenchmarkProfile,
    "--np-score-bootstrap-iterations", $NpScoreBootstrapIterations,
    "--evidence-closure-config", $EvidenceClosureConfig
)
if ($Execute) { $argsList += "--execute" }
if ($ContinueOnError) { $argsList += "--continue-on-error" }
if ($IncludeE2E) { $argsList += "--include-e2e" }
if ($SkipNpmCi) { $argsList += "--skip-npm-ci" }
if ($NoRestore) { $argsList += "--no-restore" }
if ($NoBuild) { $argsList += "--no-build" }
if ($RequireLiveDatabase) { $argsList += "--require-live-database" }
if ($RequireLiveRuntime) { $argsList += "--require-live-runtime" }
if ($RequireDatabaseTrace) { $argsList += "--require-database-trace" }
if ($ResetRuntime) { $argsList += "--reset-runtime" }
if ($RunHttp) { $argsList += "--run-http" }
if ($IncludeWeb) { $argsList += "--include-web" }
if ($RunMicrobenchmarks) { $argsList += "--run-microbenchmarks" }
if ($SystemRunDirectory) { $argsList += @("--system-run-directory", $SystemRunDirectory) }
if ($RequireHttp) { $argsList += "--require-http" }
if ($RequireMicrobenchmarks) { $argsList += "--require-microbenchmarks" }
if ($RequireSystem) { $argsList += "--require-system" }
if ($ExecuteP3) { $argsList += "--execute-p3" }
if ($AcknowledgeNonProduction) { $argsList += "--acknowledge-non-production" }
if ($P3RunLabel) { $argsList += @("--p3-run-label", $P3RunLabel) }
if ($AuditDirectory) { $argsList += @("--audit-directory", $AuditDirectory) }
if ($RequireP3) { $argsList += "--require-p3" }
if ($RequireAudit) { $argsList += "--require-audit" }

& $PythonExecutable @argsList
if ($LASTEXITCODE -ne 0) { throw "Phase 8 campaign runner failed with exit code $LASTEXITCODE." }
$campaignRoot = Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/08-campaign/$RunId"
& $PythonExecutable $verifier $campaignRoot
if ($LASTEXITCODE -ne 0) { throw "Phase 8 campaign verification failed with exit code $LASTEXITCODE." }
Write-Host "PHASE_8_OUTPUT=$campaignRoot"
if ($Execute) {
    $phase10Output = Join-Path $repoRoot "artifacts/report-evidence/$BaselineId/10-evidence-intelligence/$RunId"
    $phase10Collector = Join-Path $PSScriptRoot "collect-evidence-intelligence.py"
    $phase10Verifier = Join-Path $PSScriptRoot "verify-evidence-intelligence.py"
    & $PythonExecutable $phase10Collector --repo $repoRoot --baseline-id $BaselineId --run-id $RunId --output $phase10Output --overwrite
    if ($LASTEXITCODE -ne 0) { throw "Phase 10 evidence intelligence failed with exit code $LASTEXITCODE." }
    & $PythonExecutable $phase10Verifier $phase10Output
    if ($LASTEXITCODE -ne 0) { throw "Phase 10 verification failed with exit code $LASTEXITCODE." }
    Write-Host "PHASE_10_OUTPUT=$phase10Output"
}
