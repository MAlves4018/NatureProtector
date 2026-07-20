<#
.SYNOPSIS
Regression checks for scripts/workspace.ps1 and related local scripts.

.DESCRIPTION
These checks intentionally avoid Docker-dependent flows, Git commands, and
destructive operations. Workspace commands are executed in -PlanOnly mode where
they would otherwise mutate the local environment.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$WorkspaceScript = Join-Path $RepoRoot "scripts\workspace.ps1"
$Failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([string]$Message)
    $Failures.Add($Message) | Out-Null
    Write-Host "[FAIL] $Message"
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        Add-Failure $Message
    }
    else {
        Write-Host "[OK] $Message"
    }
}

function Assert-False {
    param(
        [bool]$Condition,
        [string]$Message
    )

    Assert-True (-not $Condition) $Message
}

function Get-OptionalFileHash {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return "<missing>"
    }

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Test-PowerShellSyntax {
    param([string]$Path)

    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors) {
        Add-Failure "$Path has parse errors: $($errors -join '; ')"
    }
    else {
        Write-Host "[OK] $Path parses"
    }
}

function Invoke-WorkspaceCommand {
    param([string[]]$Arguments)

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $WorkspaceScript @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    [pscustomobject]@{
        ExitCode = $exitCode
        Output = (($output | Out-String).Trim())
        Command = ".\scripts\workspace.ps1 $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $WorkspaceScript)) {
    throw "Workspace script not found at $WorkspaceScript"
}

$envPath = Join-Path $RepoRoot ".env"
$envExamplePath = Join-Path $RepoRoot ".env.example"
$envHashBefore = Get-OptionalFileHash $envPath
$envExampleHashBefore = Get-OptionalFileHash $envExamplePath

@(
    $WorkspaceScript,
    (Join-Path $RepoRoot "scripts\tests\export-coverage-gaps.ps1"),
    (Join-Path $RepoRoot "scripts\tests\export-test-inventory.ps1"),
    (Join-Path $RepoRoot "scripts\tests\run-mutation.ps1"),
    (Join-Path $RepoRoot "scripts\ci\run-secret-scan.ps1"),
    (Join-Path $RepoRoot "scripts\ci\check-secret-canaries.ps1"),
    (Join-Path $RepoRoot "scripts\release\build-release-candidate.ps1"),
    (Join-Path $RepoRoot "scripts\release\test-clean-install.ps1"),
    (Join-Path $RepoRoot "scripts\release\test-functional-package-smoke.ps1"),
    (Join-Path $RepoRoot "scripts\release\test-package-tamper-detection.ps1"),
    (Join-Path $RepoRoot "scripts\release\test-postgres-backup-restore.ps1"),
    (Join-Path $RepoRoot "scripts\release\test-postgres-real-data-backup-restore.ps1"),
    (Join-Path $RepoRoot "scripts\dev\start-local-runtime.ps1"),
    (Join-Path $RepoRoot "scripts\runtime\Start-LocalRuntime.ps1"),
    (Join-Path $RepoRoot "scripts\runtime\Test-LocalRuntimeHealth.ps1"),
    (Join-Path $RepoRoot "scripts\setup\Initialize-LocalWorkspace.ps1"),
    (Join-Path $RepoRoot "scripts\performance\run-system-capacity-workload.ps1"),
    (Join-Path $RepoRoot "scripts\docs\export-documentation-quality.ps1"),
    (Join-Path $RepoRoot "scripts\observability\export-telemetry-catalog.ps1"),
    (Join-Path $RepoRoot "scripts\observability\test-otlp-collector-smoke.ps1"),
    (Join-Path $RepoRoot "scripts\validation\export-artifact-inventory.ps1"),
    (Join-Path $RepoRoot "infra\scripts\up.ps1"),
    (Join-Path $RepoRoot "infra\scripts\down.ps1"),
    (Join-Path $RepoRoot "scripts\postgres\bootstrap-control-plane.ps1"),
    (Join-Path $RepoRoot "scripts\setup\Setup-LocalEnvironment.ps1"),
    (Join-Path $RepoRoot "scripts\setup\Test-LocalPrerequisites.ps1")
) | ForEach-Object { Test-PowerShellSyntax $_ }

$help = Invoke-WorkspaceCommand @("help")
Assert-True ($help.ExitCode -eq 0) "help exits successfully"
Assert-True ($help.Output -match "setup" -and $help.Output -match "validate" -and $help.Output -match "reset") "help lists workspace commands"
Assert-True ($help.Output -match "Does not execute Git commands") "help documents Git-safe behavior"

$workspaceSource = Get-Content -LiteralPath $WorkspaceScript -Raw
Assert-False ($workspaceSource -match "GetRelativePath") "workspace script avoids Path.GetRelativePath for PowerShell 5.1 compatibility"
Assert-True ($workspaceSource -match "check-dotnet-audit\.ps1" -and $workspaceSource -match "audit:ci") "workspace Security profile uses audited package policy scripts"
Assert-True ($workspaceSource -match "check-secret-canaries\.ps1" -and $workspaceSource -match "RuntimeEvidenceHttpSecurityTests") "workspace Security profile executes canary and security-focused tests"
Assert-True ($workspaceSource -match "run-benchmarks\.ps1" -and $workspaceSource -match "TimeoutSeconds") "workspace PerformanceSmoke profile uses bounded benchmark wrapper"

$setupPlan = Invoke-WorkspaceCommand @("setup", "-PlanOnly", "-NoDependencyRestore", "-NoPlaywrightInstall", "-NonInteractive")
Assert-True ($setupPlan.ExitCode -eq 0) "setup -PlanOnly exits successfully"
Assert-True ($setupPlan.Output -match "Git executable" -and $setupPlan.Output -match "without executing Git") "setup plan reports Git without executing it"

$quickPlan = Invoke-WorkspaceCommand @("validate", "-Profile", "Quick", "-PlanOnly", "-NonInteractive")
Assert-True ($quickPlan.ExitCode -eq 0) "validate Quick -PlanOnly exits successfully"
Assert-True ($quickPlan.Output -match "Validation profile: Quick") "validate Quick reports selected profile"
Assert-True ($quickPlan.Output -match "dotnet build" -and $quickPlan.Output -match "npm run check:toolchain") "validate Quick plans expected commands"

$securityPlan = Invoke-WorkspaceCommand @("validate", "-Profile", "Security", "-PlanOnly", "-NonInteractive")
Assert-True ($securityPlan.ExitCode -eq 0) "validate Security -PlanOnly exits successfully"
Assert-True ($securityPlan.Output -match "check-dotnet-audit\.ps1" -and $securityPlan.Output -match "npm run audit:ci") "validate Security plans package audit policy commands"
Assert-True ($securityPlan.Output -match "check-secret-canaries\.ps1" -and $securityPlan.Output -match "JwtAuthenticationTests") "validate Security plans canary and authorization security tests"
Assert-True ($securityPlan.Output -match "artifacts\\validation\\workspace-profiles\\security") "validate Security plans security artifacts"

$performancePlan = Invoke-WorkspaceCommand @("validate", "-Profile", "PerformanceSmoke", "-PlanOnly", "-NonInteractive")
Assert-True ($performancePlan.ExitCode -eq 0) "validate PerformanceSmoke -PlanOnly exits successfully"
Assert-True ($performancePlan.Output -match "run-benchmarks\.ps1" -and $performancePlan.Output -match "SerializationBenchmarks\.SerializeEnvelopeBatch") "validate PerformanceSmoke plans bounded benchmark wrapper"
Assert-True ($performancePlan.Output -match "TimeoutSeconds 180" -and $performancePlan.Output -match "artifacts\\validation\\workspace-profiles\\performance-smoke") "validate PerformanceSmoke plans timeout and artifact output"

$resetWithoutConfirm = Invoke-WorkspaceCommand @("reset", "-PlanOnly", "-NonInteractive")
Assert-True ($resetWithoutConfirm.ExitCode -ne 0) "reset without confirmation is blocked"
Assert-True ($resetWithoutConfirm.Output -match "RESET_LOCAL_INFRA") "reset failure mentions confirmation token"

$resetPlan = Invoke-WorkspaceCommand @("reset", "-PlanOnly", "-Confirm", "RESET_LOCAL_INFRA", "-NonInteractive")
Assert-True ($resetPlan.ExitCode -eq 0) "reset -PlanOnly with confirmation exits successfully"
Assert-True ($resetPlan.Output -match "reset-local-infra.ps1") "reset plan targets the local infra reset script"

$upScript = Get-Content -LiteralPath (Join-Path $RepoRoot "infra\scripts\up.ps1") -Raw
Assert-False ($upScript -match 'Copy-Item\s+["'']\.env\.example["'']\s+["'']\.env["'']') "up.ps1 does not copy .env.example to .env"
Assert-True ($upScript -match "will not create or edit \.env") "up.ps1 documents no .env mutation"

$bootstrapScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\postgres\bootstrap-control-plane.ps1") -Raw
Assert-True ($bootstrapScript -match '\[string\]\$Configuration\s*=\s*.+Release') "bootstrap defaults to Release configuration"
Assert-True ($bootstrapScript -match "Bootstrap execution is blocked") "bootstrap blocks execution after build failure"
Assert-False ($bootstrapScript -match "Write-Warning.*continu") "bootstrap no longer warns and continues after build failure"
Assert-True ($bootstrapScript -match "POSTGRES_HOST" -and $bootstrapScript -match "POSTGRES_PORT" -and $bootstrapScript -match "EnvironmentFirst") "bootstrap resolves the configured PostgreSQL target"
Assert-False ($bootstrapScript -match 'Test-NetConnection\s+-ComputerName\s+["'']localhost["'']\s+-Port\s+5433') "bootstrap does not hardcode localhost:5433"

$prepareScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\setup\Initialize-LocalWorkspace.ps1") -Raw
Assert-True ($prepareScript -match "dotnet restore" -and $prepareScript -match "NuGet.Config") "prepare-local restores the locked .NET dependency graph"
Assert-True ($prepareScript -match "npm ci") "prepare-local installs frontend dependencies from package-lock.json"
Assert-False ($prepareScript -match "npm install") "prepare-local does not mutate the frontend lockfile through npm install"

$runtimeHealthScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\runtime\Test-LocalRuntimeHealth.ps1") -Raw
Assert-True ($runtimeHealthScript -match "BACKOFFICE_API_PORT" -and $runtimeHealthScript -match "PREVENTION_HOST_PORT" -and $runtimeHealthScript -match "WEBUI_PORT") "runtime health consumes canonical local ports"
Assert-True ($runtimeHealthScript -match "/health/live" -and $runtimeHealthScript -match "/health/ready") "runtime health distinguishes liveness and readiness"

$runtimeStartScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\runtime\Start-LocalRuntime.ps1") -Raw
Assert-True ($runtimeStartScript -match '&\s+\$launcher\s+@launcherParameters') "runtime wrapper invokes the persistent launcher in-process"
Assert-False ($runtimeStartScript -match '&\s+pwsh\s+@arguments') "runtime wrapper does not retain native child pipes"

$prereqScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\setup\Test-LocalPrerequisites.ps1") -Raw
Assert-False ($prereqScript -match 'git"\s+@\("--version"\)') "local prerequisite check does not execute git --version"
Assert-True ($prereqScript -match "without executing Git") "local prerequisite check records Git-safe behavior"
Assert-False ($prereqScript -match "can create it from \\.env\\.example") "local prerequisite check does not claim scripts create .env"
Assert-True ($prereqScript -match "init-local -Force") "local prerequisite check points to the canonical .env initializer"

$setupScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\setup\Setup-LocalEnvironment.ps1") -Raw
Assert-False ($setupScript -match 'Copy-Item\s+\(Join-Path \$repoRoot "\.env\.example"\)\s+\$dotEnvPath') "setup orchestrator does not copy .env.example to .env"
Assert-True ($setupScript -match "will not create or edit \.env") "setup orchestrator documents no .env mutation"

$mutationScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\tests\run-mutation.ps1") -Raw
Assert-True ($mutationScript -match "TemporarySolution") "mutation wrapper uses an isolated temporary solution for Smoke profile"
Assert-True ($mutationScript -match "NatureProtector\.MutationSmoke") "mutation wrapper names the isolated smoke solution"
Assert-True ($mutationScript -match "Resolve-Reporters") "mutation wrapper validates reporter selection"
Assert-True ($mutationScript -match "BLOCKED_AFTER_REMEDIATION_ATTEMPT") "mutation wrapper preserves post-remediation blocked classification"
Assert-False ($mutationScript -match 'solution\s*=\s*"NatureProtector\.sln"') "mutation Smoke profile does not force the full repository solution"

$secretScanScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\ci\run-secret-scan.ps1") -Raw
$secretCanaryScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\ci\check-secret-canaries.ps1") -Raw
Assert-True ($secretScanScript -match "SkipGitBackedScans") "secret scan supports a no-Git validation mode"
Assert-True ($secretScanScript -match "check-secret-canaries\.ps1") "secret scan still runs canary checks"
Assert-True ($secretScanScript -match 'EndsWith\("/\.env"') "secret scan no-Git mode excludes local .env"
Assert-True ($secretScanScript -match '\\.np_evidence_python') "secret scan excludes local evidence Python environments"
Assert-True ($secretScanScript -match 'node_modules\|bin\|obj\|dist\|coverage\|TestResults\|artifacts\|graphify-out') "secret scan excludes generated working-tree roots"
Assert-True ($secretCanaryScript -match "NoGit") "secret canary scan supports filesystem enumeration without Git"
Assert-True ($secretCanaryScript -match 'EndsWith\("/\.env"') "secret canary no-Git mode excludes local .env"

$releaseCandidateScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\release\build-release-candidate.ps1") -Raw
Assert-False ($releaseCandidateScript -match 'git\s+-C') "release candidate builder does not execute Git for default version metadata"
Assert-True ($releaseCandidateScript -match "GITHUB_SHA") "release candidate builder can use CI-provided revision metadata"
Assert-True ($releaseCandidateScript -match '\$publishArguments \+= "--no-restore"') "release candidate builder propagates SkipRestore to dotnet publish"
Assert-True ($releaseCandidateScript -match '\$dotnetDependencyInventory = dotnet list') "release candidate builder captures dotnet inventory before writing"
Assert-True ($releaseCandidateScript -match '\$npmDependencyInventory = npm --prefix') "release candidate builder captures npm inventory before writing"
Assert-True ($releaseCandidateScript -match "sbom\.json") "release candidate builder writes local SBOM evidence"
Assert-True ($releaseCandidateScript -match 'Copy-Item -Path \(Join-Path \$repoRoot "data\\\*"\)') "release candidate builder packages bootstrap data inputs"
Assert-True ($releaseCandidateScript -match 'if \(\$LASTEXITCODE -ne 0\) \{ exit \$LASTEXITCODE \}') "release candidate builder checks dependency inventory exit codes"

$functionalPackageSmokeScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\release\test-functional-package-smoke.ps1") -Raw
Assert-True ($functionalPackageSmokeScript -match "GetTempPath") "functional package smoke expands outside the source tree"
Assert-True ($functionalPackageSmokeScript -match "BackofficeApi__ControlPlaneEnabled") "functional package smoke starts API package with control plane disabled"
Assert-True ($functionalPackageSmokeScript -match "/health") "functional package smoke probes Backoffice API health"
Assert-True ($functionalPackageSmokeScript -match "np_pkg_smoke_") "functional package smoke uses an isolated bootstrap database"
Assert-True ($functionalPackageSmokeScript -match 'foreach \(\$runIndex in 1\.\.2\)' -and $functionalPackageSmokeScript -match "idempotentRuns") "functional package smoke validates idempotent package bootstrap"
Assert-False ($functionalPackageSmokeScript -match '(?im)^\s*(&\s*)?(git|git\.exe)\b|Start-Process\s+["'']?(git|git\.exe)|Invoke-.*\bgit\b') "functional package smoke does not invoke Git"

$cleanInstallScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\release\test-clean-install.ps1") -Raw
Assert-True ($cleanInstallScript -match "evidence/sbom\.json") "clean install requires packaged SBOM"
Assert-True ($cleanInstallScript -match "data/manifests/datasets/proenca-a-nova-dataset-plan\.json") "clean install requires packaged bootstrap data manifest"

$realDataRestoreScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\release\test-postgres-real-data-backup-restore.ps1") -Raw
Assert-True ($realDataRestoreScript -match "postgres-real-restore") "real-data restore validation writes under artifacts/release"
Assert-True ($realDataRestoreScript -match "control\.areas") "real-data restore validation checks canonical control tables"
Assert-True ($realDataRestoreScript -match "dropdb") "real-data restore validation cleans temporary restore database"
Assert-True ($realDataRestoreScript -match "does not switch the live application") "real-data restore validation preserves live database scope"
Assert-False ($realDataRestoreScript -match '(?im)^\s*(&\s*)?(git|git\.exe)\b|Start-Process\s+["'']?(git|git\.exe)|Invoke-.*\bgit\b') "real-data restore validation does not invoke Git"

$localRuntimeScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\dev\start-local-runtime.ps1") -Raw
Assert-True ($localRuntimeScript -match "Invoke-DotnetProjectBuild") "local runtime launcher builds API and Prevention sequentially"
Assert-True ($localRuntimeScript -match "dotnet build .* -c Release --no-restore") "local runtime launcher uses Release no-restore builds"
Assert-True ($localRuntimeScript -match "dotnet run -c Release --no-build --no-restore") "local runtime launcher starts dotnet services without rebuilding"
Assert-False ($localRuntimeScript -match "dotnet run --no-restore --configfile") "local runtime launcher no longer uses concurrent Debug dotnet run builds"

$systemCapacityScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\performance\run-system-capacity-workload.ps1") -Raw
Assert-True ($systemCapacityScript -match 'ValidateSet\("Calibration", "B0", "B1", "B2"\)') "system capacity workload defines Calibration/B0/B1/B2 profiles"
Assert-True ($systemCapacityScript -match "artifacts/performance") "system capacity workload writes under artifacts/performance by default"
Assert-True ($systemCapacityScript -match "environment\.json") "system capacity workload writes environment artifact"
Assert-True ($systemCapacityScript -match "workload\.json") "system capacity workload writes workload artifact"
Assert-True ($systemCapacityScript -match "measurements\.csv") "system capacity workload writes CSV measurements"
Assert-True ($systemCapacityScript -match "measurements\.json") "system capacity workload writes JSON measurements"
Assert-True ($systemCapacityScript -match "run-failures\.json") "system capacity workload writes failure classification artifact"
Assert-True ($systemCapacityScript -match "summary\.md") "system capacity workload writes summary markdown"
Assert-True ($systemCapacityScript -match "requires a previous calibration summary") "system capacity workload blocks B profiles without calibration evidence"
Assert-True ($systemCapacityScript -match "Wait-RunEvidence") "system capacity workload waits for persisted run evidence before measuring"
Assert-True ($systemCapacityScript -match "Wait-QueueDrain") "system capacity workload measures ingestion backlog drain time"
Assert-True ($systemCapacityScript -match "observationWaitSeconds") "system capacity workload uses profile-specific observation windows"
Assert-True ($systemCapacityScript -match "ConvertTo-Json -InputObject") "system capacity workload writes empty JSON collections deterministically"
Assert-True ($systemCapacityScript -match "np\.ingestion\.readings") "system capacity workload reports pipeline queue depth separately from auxiliary queues"
Assert-True ($systemCapacityScript -match "publisher_timestamp_not_persisted|PublishedAt is not persisted") "system capacity workload preserves latency limitation"
Assert-False ($systemCapacityScript -match '\bgit\b') "system capacity workload does not execute Git"

$benchmarkScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\performance\run-benchmarks.ps1") -Raw
Assert-True ($benchmarkScript -match 'ValidateSet\("B0", "B1", "B2"\)') "benchmark wrapper defines B0/B1/B2 profiles"
Assert-True ($benchmarkScript -match "TimeoutSeconds") "benchmark wrapper supports bounded timeout"
Assert-True ($benchmarkScript -match "SummarizeOnlyDirectory") "benchmark wrapper can regenerate summaries without rerunning benchmarks"
Assert-True ($benchmarkScript -match "summary\.json") "benchmark wrapper writes JSON summary"
Assert-True ($benchmarkScript -match "summary\.md") "benchmark wrapper writes Markdown summary"
Assert-True ($benchmarkScript -match "BenchmarkDotNet microbenchmark summary") "benchmark wrapper preserves engineering measurement scope"
Assert-True ($benchmarkScript -match "standardErrorNanoseconds") "benchmark wrapper preserves BenchmarkDotNet standard error"
Assert-True ($benchmarkScript -match "standardDeviationNanoseconds") "benchmark wrapper preserves BenchmarkDotNet standard deviation"
Assert-True ($benchmarkScript -match "gen0CollectionsPer1000Operations") "benchmark wrapper preserves normalized GC metrics"
Assert-True ($benchmarkScript -match 'ExitCode -eq 0 -and \$reports\.Count -gt 0 -and \$rows\.Count -gt 0') "benchmark wrapper classifies valid reports as ready"
Assert-False ($benchmarkScript -match '\bgit\b') "benchmark wrapper does not execute Git"

$documentationQualityScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\docs\export-documentation-quality.ps1") -Raw
Assert-True ($documentationQualityScript -match "artifacts\\validation\\documentation-quality") "documentation quality inventory writes under artifacts/validation"
Assert-True ($documentationQualityScript -match "Mojibake|utf8_as_cp1252|replacement_character") "documentation quality inventory detects encoding artifacts"
Assert-True ($documentationQualityScript -match "ClaimScope") "documentation quality inventory detects claim-scope review terms"
Assert-True ($documentationQualityScript -match "Get-DocumentScope") "documentation quality inventory classifies document scope"
Assert-True ($documentationQualityScript -match "Get-FindingClassification") "documentation quality inventory classifies findings"
Assert-True ($documentationQualityScript -match "invalid_utf8") "documentation quality inventory detects invalid UTF-8"
Assert-True ($documentationQualityScript -match "FailOnCanonicalMojibake") "documentation quality inventory supports opt-in canonical mojibake failure"
Assert-True ($documentationQualityScript -match "FailOnCanonicalDefects") "documentation quality inventory supports opt-in canonical defect failure"
Assert-True ($documentationQualityScript -match "findings\.json") "documentation quality inventory writes JSON findings"
Assert-True ($documentationQualityScript -match "findings\.csv") "documentation quality inventory writes CSV findings"
Assert-True ($documentationQualityScript -match "summary\.md") "documentation quality inventory writes Markdown summary"
Assert-True ($documentationQualityScript -match "corrected-files\.md") "documentation quality inventory writes corrected files summary"
Assert-True ($documentationQualityScript -match "remaining-review\.md") "documentation quality inventory writes remaining review summary"
Assert-True ($documentationQualityScript -match "does not execute Git commands") "documentation quality inventory documents Git-safe behavior"
Assert-False ($documentationQualityScript -match '(?im)^\s*(&\s*)?(git|git\.exe)\b|Start-Process\s+["'']?(git|git\.exe)|Invoke-.*\bgit\b') "documentation quality inventory does not invoke Git"

$telemetryCatalogScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\observability\export-telemetry-catalog.ps1") -Raw
Assert-True ($telemetryCatalogScript -match "HostTelemetry\.cs") "telemetry catalog is derived from HostTelemetry"
Assert-True ($telemetryCatalogScript -match "telemetry-catalog\.json") "telemetry catalog writes JSON output"
Assert-True ($telemetryCatalogScript -match "metrics\.csv") "telemetry catalog writes metrics CSV"
Assert-True ($telemetryCatalogScript -match "HighIdentifier") "telemetry catalog classifies high-cardinality tags"
Assert-False ($telemetryCatalogScript -match '(?im)^\s*(&\s*)?(git|git\.exe)\b|Start-Process\s+["'']?(git|git\.exe)|Invoke-.*\bgit\b') "telemetry catalog does not invoke Git"

$otlpCollectorSmokeScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\observability\test-otlp-collector-smoke.ps1") -Raw
Assert-True ($otlpCollectorSmokeScript -match "otel/opentelemetry-collector-contrib:0\.130\.0") "OTLP collector smoke pins collector image"
Assert-True ($otlpCollectorSmokeScript -match "file/traces") "OTLP collector smoke exports traces to file"
Assert-True ($otlpCollectorSmokeScript -match "file/metrics") "OTLP collector smoke exports metrics to file"
Assert-True ($otlpCollectorSmokeScript -match "OTEL_EXPORTER_OTLP_ENDPOINT") "OTLP collector smoke configures OTLP endpoint"
Assert-True ($otlpCollectorSmokeScript -match "NatureProtector.Backoffice.Api") "OTLP collector smoke verifies service name"
Assert-False ($otlpCollectorSmokeScript -match '(?im)^\s*(&\s*)?(git|git\.exe)\b|Start-Process\s+["'']?(git|git\.exe)|Invoke-.*\bgit\b') "OTLP collector smoke does not invoke Git"

$artifactInventoryScript = Get-Content -LiteralPath (Join-Path $RepoRoot "scripts\validation\export-artifact-inventory.ps1") -Raw
Assert-True ($artifactInventoryScript -match "artifacts\\validation\\artifact-hygiene") "artifact inventory writes under artifacts/validation"
Assert-True ($artifactInventoryScript -match "inventory\.json") "artifact inventory writes JSON inventory"
Assert-True ($artifactInventoryScript -match "inventory\.csv") "artifact inventory writes CSV inventory"
Assert-True ($artifactInventoryScript -match "summary\.md") "artifact inventory writes Markdown summary"
Assert-True ($artifactInventoryScript -match "ReviewLargePreserveByDefault") "artifact inventory classifies large outputs without deleting"
Assert-True ($artifactInventoryScript -match "secretNameCandidates") "artifact inventory records filename-only secret candidate counts"
Assert-True ($artifactInventoryScript -match "does not execute Git commands") "artifact inventory documents Git-safe behavior"
Assert-False ($artifactInventoryScript -match '(?im)^\s*(&\s*)?(git|git\.exe)\b|Start-Process\s+["'']?(git|git\.exe)|Invoke-.*\bgit\b') "artifact inventory does not invoke Git"

$engineeringWorkflow = Get-Content -LiteralPath (Join-Path $RepoRoot ".github\workflows\engineering-foundations.yml") -Raw
Assert-True ($engineeringWorkflow -match "test-workspace-script\.ps1") "engineering workflow runs workspace regression checks"
Assert-True ($engineeringWorkflow -match "export-test-inventory\.ps1") "engineering workflow exports the test inventory"
Assert-True ($engineeringWorkflow -match "export-coverage-gaps\.ps1") "engineering workflow exports coverage gaps"
Assert-True ($engineeringWorkflow -match "artifacts/validation/") "engineering workflow uploads validation artifacts"

$envHashAfter = Get-OptionalFileHash $envPath
$envExampleHashAfter = Get-OptionalFileHash $envExamplePath
Assert-True ($envHashBefore -eq $envHashAfter) ".env hash is unchanged"
Assert-True ($envExampleHashBefore -eq $envExampleHashAfter) ".env.example hash is unchanged"

if ($Failures.Count -gt 0) {
    Write-Host ""
    Write-Host "$($Failures.Count) workspace regression check(s) failed."
    exit 1
}

Write-Host ""
Write-Host "Workspace regression checks passed."
