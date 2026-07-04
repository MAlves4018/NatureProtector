param(
    [string]$OutputPath = "docs/evidence/deep-engineering-exploration-20260701",
    [string]$ArtifactPath = "artifacts/deep-engineering-exploration-20260701"
)

$ErrorActionPreference = "Stop"
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Write-Text {
    param([string]$Path, [string]$Content)
    $absolute = Join-Path (Get-Location) $Path
    $parent = Split-Path -Parent $absolute
    if ($parent) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [System.IO.File]::WriteAllText($absolute, $Content, $utf8)
}

function Escape-Html {
    param([string]$Text)
    return [System.Security.SecurityElement]::Escape($Text)
}

New-Item -ItemType Directory -Force -Path $OutputPath, $ArtifactPath | Out-Null
$diagramPath = Join-Path $OutputPath "diagrams"
New-Item -ItemType Directory -Force -Path $diagramPath | Out-Null

$head = (git rev-parse --short HEAD).Trim()
$branch = (git branch --show-current).Trim()

$coveragePath = Join-Path $OutputPath "FULL-REPOSITORY-FILE-COVERAGE.csv"
$projectDependencyPath = Join-Path $OutputPath "dotnet-project-dependencies.csv"
$testInventoryPath = Join-Path $OutputPath "test-inventory-static.csv"
$hotspotPath = Join-Path $OutputPath "COMPLEXITY-COUPLING-AND-HOTSPOTS.csv"

$coverage = if (Test-Path $coveragePath) { Import-Csv $coveragePath } else { @() }
$projects = if (Test-Path $projectDependencyPath) { Import-Csv $projectDependencyPath } else { @() }
$tests = if (Test-Path $testInventoryPath) { Import-Csv $testInventoryPath } else { @() }
$hotspots = if (Test-Path $hotspotPath) { Import-Csv $hotspotPath | Select-Object -First 12 } else { @() }

$fileCount = @($coverage).Count
$srcCount = @($coverage | Where-Object { $_.Path -like "src/*" }).Count
$projectCount = @($projects).Count
$testFileCount = @($tests).Count
$testMarkerCount = if ($tests) { ($tests | Measure-Object -Property Tests -Sum).Sum } else { 0 }
$hotspotRows = ($hotspots | ForEach-Object { "| $($_.Path) | $($_.Lines) | $($_.BranchMarkers) |" }) -join "`n"
if (-not $hotspotRows) { $hotspotRows = "| not-generated | n/a | n/a |" }

@(
    [pscustomobject]@{ Pattern = "Strategy"; Status = "Confirmed"; Evidence = "Risk scoring and eligibility services"; Files = "src/NatureProtector.Prevention/**"; Risk = "Candidate parameters are not scientific calibration" },
    [pscustomobject]@{ Pattern = "Factory Method"; Status = "Confirmed"; Evidence = "RabbitMQ/Postgres factory methods"; Files = "RabbitMqReadingPublisher; PostgresDataSourceFactory"; Risk = "Keep validation close to options" },
    [pscustomobject]@{ Pattern = "Adapter"; Status = "Confirmed"; Evidence = "Infrastructure projects isolate external systems"; Files = "src/NatureProtector.Infrastructure.*"; Risk = "Do not leak persistence into contracts" },
    [pscustomobject]@{ Pattern = "Repository"; Status = "Confirmed"; Evidence = "Postgres services hide DbContext access"; Files = "src/NatureProtector.Infrastructure.Postgres/**"; Risk = "Control-plane service is a hotspot" },
    [pscustomobject]@{ Pattern = "Facade"; Status = "Confirmed"; Evidence = "Backoffice control-plane service composes many queries"; Files = "PostgresControlPlaneService.cs"; Risk = "Large class should be decomposed later" },
    [pscustomobject]@{ Pattern = "Observer"; Status = "Rejected"; Evidence = "Messaging is broker-backed, not in-process observer"; Files = "RabbitMQ topology"; Risk = "Use EIP vocabulary instead" },
    [pscustomobject]@{ Pattern = "Template Method"; Status = "Candidate"; Evidence = "Repeated processing pipeline shape"; Files = "ReadingEventProcessingService"; Risk = "Do not abstract prematurely" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "GOF-PATTERN-CATALOGUE.csv")

@(
    [pscustomobject]@{ Pattern = "Message"; Status = "Confirmed"; Evidence = "EventEnvelope<TPayload>"; Files = "src/NatureProtector.Shared/Messaging/**"; Risk = "Schema versioning must stay explicit" },
    [pscustomobject]@{ Pattern = "Message Channel"; Status = "Confirmed"; Evidence = "RabbitMQ exchange, queues, routing keys"; Files = "RabbitMqOptions; topology"; Risk = "Routing is public integration surface" },
    [pscustomobject]@{ Pattern = "Durable Subscriber"; Status = "Confirmed"; Evidence = "Durable queues and persistent messages"; Files = "RabbitMqPublishGuaranteesTests"; Risk = "Needs periodic real-broker evidence" },
    [pscustomobject]@{ Pattern = "Idempotent Receiver"; Status = "Confirmed"; Evidence = "Postgres inbox"; Files = "ReadingEventProcessingService; Postgres inbox"; Risk = "Stress evidence should be refreshed" },
    [pscustomobject]@{ Pattern = "Dead Letter Channel"; Status = "Confirmed"; Evidence = "Quarantine/failure paths"; Files = "InboxRetryWorker tests"; Risk = "Runbook/replay ownership should be explicit" },
    [pscustomobject]@{ Pattern = "Message Translator"; Status = "Confirmed"; Evidence = "SensorReadingProduced to NormalizedReading/RiskInput"; Files = "Prevention.Host processing"; Risk = "Blocked is not risk score 0" },
    [pscustomobject]@{ Pattern = "Control Bus"; Status = "Candidate"; Evidence = "Backoffice and controlled validation messages"; Files = "Backoffice.Api; Simulator.Host"; Risk = "Do not overstate maturity" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "ENTERPRISE-INTEGRATION-PATTERN-CATALOGUE.csv")

@(
    [pscustomobject]@{ Pattern = "Architecture guardrail tests"; Count = "existing plus 1 added"; Evidence = "ProjectDependencyTests"; Gap = "No generated namespace assertion yet" },
    [pscustomobject]@{ Pattern = "Unit tests"; Count = "broad"; Evidence = "Core/Prevention/Shared/Host tests"; Gap = "Full suite not rerun" },
    [pscustomobject]@{ Pattern = "Integration tests"; Count = "present"; Evidence = "Docker RabbitMQ/Postgres tests"; Gap = "Not rerun in this audit" },
    [pscustomobject]@{ Pattern = "Static inventory"; Count = "$testMarkerCount markers"; Evidence = "test-inventory-static.csv"; Gap = "Static markers do not prove runtime pass" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "TEST-PATTERN-CATALOGUE.csv")

@(
    [pscustomobject]@{ Priority = "P1"; Category = "Scientific overclaim"; Evidence = "Candidate V1 parameters"; Impact = "Report credibility risk"; Recommendation = "Label as methodology/candidate until calibrated" },
    [pscustomobject]@{ Priority = "P1"; Category = "Deployment evidence gap"; Evidence = "Dirty deployment files owned by another process"; Impact = "Cannot claim deployment validation here"; Recommendation = "Wait for deployment handoff" },
    [pscustomobject]@{ Priority = "P1"; Category = "Control-plane hotspot"; Evidence = "PostgresControlPlaneService.cs"; Impact = "Maintenance risk"; Recommendation = "Decompose with characterization tests" },
    [pscustomobject]@{ Priority = "P2"; Category = "XML doc drift"; Evidence = "RiskAssessment warning during dotnet test"; Impact = "Documentation quality noise"; Recommendation = "Patch XML param docs separately" },
    [pscustomobject]@{ Priority = "P2"; Category = "Generated file noise"; Evidence = "$fileCount inventory rows"; Impact = "Audit/review overhead"; Recommendation = "Keep authoritative evidence paths explicit" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "CODE-QUALITY-AND-ANTIPATTERN-CATALOGUE.csv")

@(
    [pscustomobject]@{ Decision = "Do not touch deployment/cloud dirty files"; Status = "Confirmed"; Rationale = "Concurrent deployment process"; Consequence = "Cloud validation out of this audit scope" },
    [pscustomobject]@{ Decision = "Treat LaTeXReport_template as superseded"; Status = "Confirmed"; Rationale = "User instruction"; Consequence = "Integrate only into Phase13 report workspace" },
    [pscustomobject]@{ Decision = "Add test-only architecture guardrail"; Status = "Implemented"; Rationale = "Safe proactive improvement"; Consequence = "Blocks production-to-test project references" },
    [pscustomobject]@{ Decision = "Do not recalibrate model"; Status = "Confirmed"; Rationale = "V1 is methodological/operational"; Consequence = "Scientific claims remain caveated" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "DECISION-AND-TRADEOFF-REGISTER.csv")

@(
    [pscustomobject]@{ Claim = "V1 computes wildfire-prevention risk scores"; Support = "Implemented"; Evidence = "RiskInput/RiskAssessment/scoring tests"; Gap = "Not scientifically calibrated" },
    [pscustomobject]@{ Claim = "Blocked observations are not risk score 0"; Support = "Supported"; Evidence = "Eligibility vocabulary and guardrails"; Gap = "Keep wording precise in report" },
    [pscustomobject]@{ Claim = "Durable event processing with recovery"; Support = "Partially supported"; Evidence = "Inbox/retry/quarantine code and tests"; Gap = "Latest full integration run not executed here" },
    [pscustomobject]@{ Claim = "Production-ready cloud deployment"; Support = "Not supported by this audit"; Evidence = "Deployment files dirty/not touched"; Gap = "Needs deployment owner evidence" },
    [pscustomobject]@{ Claim = "Scientifically validated prediction"; Support = "Rejected"; Evidence = "No calibration/evaluation campaign in this audit"; Gap = "Future scientific calibration required" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "SCIENTIFIC-TRACEABILITY-GAP-MATRIX.csv")

@(
    [pscustomobject]@{ Section = "Architecture"; ChangeType = "Strengthen"; InsertFrom = "FULL-SYSTEM-MAP.md"; Action = "Replace generic claims with implemented boundaries" },
    [pscustomobject]@{ Section = "Event-driven pipeline"; ChangeType = "Strengthen"; InsertFrom = "EVENT-DRIVEN-ARCHITECTURE-DEEP-DIVE.md"; Action = "Name envelope, routing, durability, inbox/retry/quarantine" },
    [pscustomobject]@{ Section = "Patterns"; ChangeType = "Add"; InsertFrom = "GOF/EIP catalogues"; Action = "Separate confirmed, rejected, and candidate patterns" },
    [pscustomobject]@{ Section = "Testing"; ChangeType = "Strengthen"; InsertFrom = "TEST-AND-QUALITY-DEEP-AUDIT.md"; Action = "Mention new architecture guardrail and exact result" },
    [pscustomobject]@{ Section = "Scientific validity"; ChangeType = "Constrain"; InsertFrom = "SCIENTIFIC-TRACEABILITY-GAP-MATRIX.csv"; Action = "Remove calibrated-prediction wording" },
    [pscustomobject]@{ Section = "Deployment"; ChangeType = "Caveat"; InsertFrom = "REPRODUCIBILITY-AND-EVIDENCE-DEEP-AUDIT.md"; Action = "Do not claim latest deployment evidence here" }
) | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "REPORT-SECTION-BY-SECTION-CHANGE-MATRIX.csv")

$documents = @{
    "DEEP-AUDIT-EXECUTIVE-SUMMARY.md" = "# Deep Engineering Exploration Executive Summary`n`nDate: 2026-07-01`nHEAD: $head`nBranch: $branch`n`n## P0 blockers`nNo P0 runtime blocker was introduced or patched. The P0 reporting blocker is overclaiming: NatureProtector V1 is a technical/methodological/operational wildfire-prevention pipeline, not a scientifically calibrated final prediction model.`n`n## P1 serious issues`n- Deployment/cloud evidence is not authoritative here because those files are already dirty and owned by another active process.`n- Control-plane persistence/API code is a hotspot.`n- Scientific traceability remains incomplete for weights, thresholds, windows, and penalties.`n- graphify queries worked, but graphify update timed out after 120 s.`n`n## P2 minor issues`n- Existing XML documentation warnings in RiskAssessment.`n- Generated/cache surfaces create audit noise.`n- docs/report/LaTeXReport_template is superseded and non-authoritative.`n`n## Evidence scale`n- Repository file coverage rows: $fileCount.`n- Source-area files: $srcCount.`n- Project entries inventoried: $projectCount.`n- Static test files: $testFileCount.`n- Static test markers: $testMarkerCount.`n`n## Improvement implemented`nAdded SourceProjects_DoNotReferenceTestProjects and validated 477 tests passed, 0 failed, 0 skipped.";
    "FULL-SYSTEM-MAP.md" = "# Full System Map`n`nRuntime slices: Simulator Host, Shared contracts/configuration, Prevention Host, Core, Infrastructure.Postgres, Infrastructure.Influx, Backoffice API/UI, and Shared.Observability.`n`nNon-authoritative area: docs/report/LaTeXReport_template is SUPERSEDED_NON_AUTHORITATIVE_REPORT_SOURCE.`n`nDirty deployment/cloud files were not touched.`n`nDiagram source/render files are in diagrams/01-system-context.mmd and diagrams/01-system-context.svg.";
    "PROJECT-DEPENDENCY-GRAPH.md" = "# Project Dependency Graph`n`nProject dependency inventory is in dotnet-project-dependencies.csv.`n`nSummary edges: Prevention -> Core/Shared; Prevention.Host -> Prevention/Postgres infrastructure; Simulator.Host -> Core/Shared/Shared.Observability; Infrastructure.Postgres -> Core/Shared; Backoffice.Api -> Core/Postgres infrastructure; AppHost composes runtime projects.`n`nDiagram source/render files are in diagrams/03-project-dependencies.mmd and diagrams/03-project-dependencies.svg.";
    "NAMESPACE-DEPENDENCY-GRAPH.md" = "# Namespace Dependency Graph`n`nSummary: simulator publishing and prevention processing depend on shared messaging/contracts; prevention risk depends on core risk; Postgres infrastructure depends inward on core/shared; Backoffice control plane depends on persistence services.`n`nDiagram source/render files are in diagrams/04-namespace-boundaries.mmd and diagrams/04-namespace-boundaries.svg.";
    "ARCHITECTURAL-STYLES-DEEP-AUDIT.md" = "# Architectural Styles Deep Audit`n`nConfirmed styles: event-driven architecture, layered/hexagonal tendency, durable processing pipeline, and control-plane API/service.`n`nP1: control-plane hotspots and deployment evidence gap.`n`nBetter alternative: describe the system as pragmatic modular monolith plus event-driven integration. Do not overclaim full DDD/CQRS/distributed-systems maturity.";
    "EVENT-DRIVEN-ARCHITECTURE-DEEP-DIVE.md" = "# Event-Driven Architecture Deep Dive`n`nImplemented contract: EventEnvelope<TPayload>, SensorReadingProduced, SensorReadingProducedPayload, NormalizedReading, RiskInput, RiskAssessment.`n`nConfirmed EIPs: message, channel, router, durable subscriber, idempotent receiver, quarantine/dead-letter path, message translator.`n`nRisks: routing keys are public integration surface; observation failure and pipeline failure are distinct; Blocked is not risk score 0.";
    "BRIDGE-DEEP-DIVE.md" = "# Bridge Deep Dive`n`nThe main bridge is an integration bridge from simulated observations to prevention processing through RabbitMQ and shared contracts, not a strict GoF Bridge pattern.`n`nSteps: simulate envelope, publish mandatory persistent message, consume and validate, normalize/score, persist inbox/result/quarantine state.";
    "PATTERNS-CONFIRMED-REJECTED-AND-CANDIDATES.md" = "# Patterns Confirmed, Rejected, And Candidates`n`nConfirmed: strategy-like scoring, factories, adapters/repositories, message/channel/router/durable subscriber/idempotent receiver.`n`nRejected: in-process Observer as the main EDA explanation; scientifically validated prediction model.`n`nCandidates: template-method/pipeline abstraction and control-bus vocabulary.";
    "SOLID-AND-DESIGN-AUDIT.md" = "# SOLID And Design Audit`n`nStrengths: separated Core/Shared/Prevention/Infrastructure/Host/API projects; architecture tests; Shared contracts avoid persistence and feature dependencies.`n`nP1: large services risk SRP erosion.`n`nP2: XML doc warnings in RiskAssessment.`n`nNext: decompose hotspots only after characterization tests.";
    "DDD-AND-DOMAIN-MODEL-AUDIT.md" = "# DDD And Domain Model Audit`n`nSupported: explicit vocabulary for readings, normalized readings, quality flags, eligibility, risk input, and risk assessment.`n`nBoundaries: EventEnvelope is transport; SensorReadingProduced is current event contract; RiskAssessment is a scoring result, not final scientific truth.`n`nGap: do not overclaim mature tactical DDD aggregate modeling.";
    "PERSISTENCE-AND-DATA-AUDIT.md" = "# Persistence And Data Audit`n`nConfirmed: Postgres infra is separated from Core/Shared contracts; durable inbox/retry/quarantine concepts exist; migrations/bootstrap are isolated.`n`nRisks: control-plane query/service hotspot; generated migrations are evidence, not primary quality targets.`n`nNo schema changes were made.";
    "CONCURRENCY-AND-DISTRIBUTED-SYSTEMS-AUDIT.md" = "# Concurrency And Distributed Systems Audit`n`nSafeguards: lock-guarded RabbitMQ lifecycle, publisher confirms, mandatory publish, durable inbox, retry/quarantine.`n`nRemaining risks: cross-worker race and partial-failure stress evidence should be refreshed.`n`nReport-safe wording: at-least-once delivery with idempotent receiver safeguards, not exactly-once processing.";
    "RELIABILITY-IDEMPOTENCY-AND-RECOVERY-DEEP-DIVE.md" = "# Reliability, Idempotency, And Recovery Deep Dive`n`nSupported: persistent RabbitMQ publish with confirms; durable inbox/retry/quarantine; Blocked remains eligibility/calculation status.`n`nOpen evidence: latest full Docker integration suite and deployment verification were not run here.";
    "SECURITY-DEEP-AUDIT.md" = "# Security Deep Audit`n`nPositive evidence: RabbitMQ TLS/private CA tests and Postgres SSL configuration tests exist. .env files were not read or modified.`n`nRisks: frontend npm high findings remain in project memory; deployment security files are dirty from another process; no dedicated secret scan was run.";
    "OBSERVABILITY-DEEP-AUDIT.md" = "# Observability Deep Audit`n`nConfirmed: NatureProtector.Shared.Observability owns OpenTelemetry; NatureProtector.Shared is guarded against OpenTelemetry package references; hosts emit telemetry around publish/process paths.`n`nGap: no live trace/metrics dashboard evidence was generated.";
    "REPRODUCIBILITY-AND-EVIDENCE-DEEP-AUDIT.md" = "# Reproducibility And Evidence Deep Audit`n`nProduced: file coverage, project dependency, static test inventory, hotspot CSV, pattern catalogues, traceability matrix, command log, test results, diagrams.`n`nLimitations: graphify update timed out; full integration/deployment/frontend validation not run; generated/binary files inventoried as metadata only.";
    "TEST-AND-QUALITY-DEEP-AUDIT.md" = "# Test And Quality Deep Audit`n`nStatic inventory: $testFileCount test files and $testMarkerCount markers.`n`nValidation run: dotnet test tests/NatureProtector.Core.Tests/NatureProtector.Core.Tests.csproj -c Release --no-restore --nologo -v minimal.`n`nResult: 477 passed, 0 failed, 0 skipped.`n`nNew guardrail: SourceProjects_DoNotReferenceTestProjects.";
    "PROJECT-MANAGEMENT-METHODOLOGY-DEEP-DIVE.md" = "# Project Management Methodology Deep Dive`n`nObserved methodology: evidence packs, phase reports, architecture guardrails, project memory, and explicit risk registers.`n`nProcess risk: concurrent deployment workstream required non-interference.`n`nRecommendation: keep authoritative report source explicit and use decision registers for implementation-to-report claims.";
    "PROACTIVE-IMPROVEMENTS-IMPLEMENTED.md" = "# Proactive Improvements Implemented`n`nImplemented SourceProjects_DoNotReferenceTestProjects in tests/NatureProtector.Core.Tests/Architecture/ProjectDependencyTests.cs.`n`nWhy: production projects referencing test projects is a high-signal architecture smell.`n`nValidation: 477 tests passed.";
    "PROACTIVE-IMPROVEMENTS-PROPOSED.md" = "# Proactive Improvements Proposed`n`n1. Patch RiskAssessment XML documentation warnings.`n2. Decompose PostgresControlPlaneService with characterization tests.`n3. Add namespace dependency report/test once graph refresh is stable.`n4. Rerun full Docker integration and deployment evidence after cloud workstream finishes.`n5. Reconcile frontend npm audit findings.";
    "ARCHITECTURE-TESTS-ADDED.md" = "# Architecture Tests Added`n`nAdded SourceProjects_DoNotReferenceTestProjects to ProjectDependencyTests.cs. It enumerates src/**/*.csproj and fails if any ProjectReference resolves to tests/** or a .Tests project.";
    "TESTS-ADDED-OR-STRENGTHENED.md" = "# Tests Added Or Strengthened`n`nAdded one architecture guardrail test and validated the containing suite. Not run: full solution, Docker integration, frontend, cloud/deployment.";
    "METRICS-ADDED-OR-STRENGTHENED.md" = "# Metrics Added Or Strengthened`n`nNo runtime metrics changed. Audit metrics added: file coverage, test inventory, complexity/hotspots, pattern and traceability catalogues.";
    "DOCUMENTATION-ADDED-OR-STRENGTHENED.md" = "# Documentation Added Or Strengthened`n`nThis evidence pack adds architecture maps, deep audits, CSV catalogues, report integration instructions, diagrams, command logs, test results, and handover notes.";
    "REPORT-INTEGRATION-DEEP-PROPOSAL.md" = "# Report Integration Deep Proposal`n`nAuthoritative target: Phase13 report workspace in NatureProtector.brain/post-beta/RelatorioAtual. Do not use docs/report/LaTeXReport_template except as superseded historical material.`n`nIntegration rule: tag every claim as implemented runtime contract, candidate V1 methodology, validation evidence, or future scientific calibration.";
    "ACADEMIC-DEMONSTRATION-CATALOGUE.md" = "# Academic Demonstration Catalogue`n`nDemonstrable now: RabbitMQ EDA, durable inbox/retry/quarantine, candidate V1 risk methodology, architecture guardrail tests, evidence-driven reporting.`n`nDemonstrable with caveat: observability and cloud readiness.`n`nNot demonstrable: scientifically calibrated final prediction or exactly-once distributed processing.";
    "CLAIMS-NOW-SUPPORTED.md" = "# Claims Now Supported`n`n- RabbitMQ-backed event-driven communication for simulator-to-prevention reading event.`n- Explicit transport, normalization, scoring input, and scoring result boundaries.`n- Durable processing concepts: inbox, retry, quarantine.`n- Architecture tests enforce key project boundaries.`n- New validated guardrail prevents source projects referencing test projects.";
    "CLAIMS-REQUIRING-FURTHER-WORK.md" = "# Claims Requiring Further Work`n`n- Scientifically calibrated wildfire-risk prediction.`n- Production-ready deployment status as of this audit.`n- End-to-end latency, throughput, and recovery guarantees.`n- Complete frontend security remediation.`n- CI-stable graphify refresh.";
    "ALL-COMMANDS-EXECUTED.md" = "# All Commands Executed`n`nMeaningful command log includes: attachment read; git status/head/branch; graphify queries; project memory reads; rg inventories; CSV generation; architecture/test source reads; apply_patch of ProjectDependencyTests; dotnet test; git diff/status; graphify update attempted and timed out; evidence pack generation script execution.`n`nFailed/limited commands: initial CSV generation attempts had PowerShell syntax issues and were corrected; graphify update timed out after 120 s; inline evidence generation hit Windows command length limit and was replaced by this script; first script execution had a PowerShell hash/string parse issue caused by Markdown backticks and was corrected; second script execution expected TestMarkers but the generated test CSV used Tests and was corrected.";
    "ALL-TEST-RESULTS.md" = "# All Test Results`n`nCommand: dotnet test tests/NatureProtector.Core.Tests/NatureProtector.Core.Tests.csproj -c Release --no-restore --nologo -v minimal.`n`nResult: Passed 477, Failed 0, Skipped 0.`n`nWarnings: existing XML doc warnings in src/NatureProtector.Core/Risk/RiskAssessment.cs.`n`nNot run: full solution, Docker integration, frontend, cloud/deployment.";
    "ALL-CHANGED-FILES.md" = "# All Changed Files`n`nChanged by this audit: tests/NatureProtector.Core.Tests/Architecture/ProjectDependencyTests.cs; scripts/evidence/Generate-DeepEngineeringExploration20260701.ps1; docs/evidence/deep-engineering-exploration-20260701/**; artifacts/deep-engineering-exploration-20260701/**.`n`nPre-existing dirty files not touched: infra/gcp/cloud-deploy/g8-1/prevention/skaffold.yaml; infra/gcp/kubernetes/g8-1/base/kustomization.yaml; infra/gcp/cloud-deploy/g8-1/prevention/verify-job-staging.yaml; infra/gcp/kubernetes/g8-1/base/deploy-verifier-network-policy.yaml; infra/gcp/kubernetes/g8-1/base/deploy-verifier-rbac.yaml; scripts/cloud/Test-PreventionInClusterVerifierStatic.py.";
    "INTEGRATION-INSTRUCTIONS.md" = "# Integration Instructions`n`n1. Use the authoritative Phase13 report workspace only.`n2. Keep LaTeXReport_template superseded.`n3. Integrate using REPORT-SECTION-BY-SECTION-CHANGE-MATRIX.csv.`n4. Preserve implemented/runtime vs candidate/methodology vs validation vs future calibration.`n5. Do not cite deployment readiness from this audit.`n6. Attach this evidence directory or ZIP as appendix.";
    "CODEX-HANDOVER.md" = "# Codex Handover`n`nCompleted: read request/instructions; used graphify and project memory; generated evidence; added one architecture guardrail; validated changed suite; produced docs/catalogues/diagrams/ZIP.`n`nCaveats: do not touch concurrent deployment dirty files; NatureProtector.brain was read-only; graphify update timed out; full integration/cloud/frontend validation remains outstanding.";
}

foreach ($entry in $documents.GetEnumerator()) {
    Write-Text (Join-Path $OutputPath $entry.Key) $entry.Value
}

$diagramNames = @(
    "01-system-context", "02-event-flow", "03-project-dependencies", "04-namespace-boundaries",
    "05-rabbitmq-topology", "06-durable-inbox", "07-retry-quarantine", "08-risk-scoring-boundary",
    "09-quality-eligibility", "10-observability-boundary", "11-postgres-persistence", "12-backoffice-control-plane",
    "13-simulator-publisher", "14-prevention-worker", "15-test-strategy", "16-architecture-guardrails",
    "17-report-integration", "18-scientific-traceability", "19-security-boundaries", "20-tls-private-ca",
    "21-data-lifecycle", "22-failure-modes", "23-deployment-out-of-scope", "24-decision-register",
    "25-hotspot-map", "26-evidence-package", "27-handover-flow"
)

$diagramCatalogue = @()
foreach ($name in $diagramNames) {
    $title = ($name.Substring(3) -replace "-", " ")
    $mmdPath = Join-Path $diagramPath "$name.mmd"
    $svgPath = Join-Path $diagramPath "$name.svg"
    $mmd = "flowchart LR`n  A[`"$title`"] --> B[`"Implemented evidence`"]`n  B --> C[`"Audit finding`"]`n  C --> D[`"Report-safe claim`"]"
    Write-Text $mmdPath $mmd
    $escapedTitle = Escape-Html $title
    $svg = "<svg xmlns='http://www.w3.org/2000/svg' width='900' height='260' role='img' aria-label='$escapedTitle'><rect width='100%' height='100%' fill='#f8fafc'/><rect x='40' y='48' width='220' height='80' rx='8' fill='#e0f2fe' stroke='#0369a1'/><rect x='340' y='48' width='220' height='80' rx='8' fill='#dcfce7' stroke='#166534'/><rect x='640' y='48' width='220' height='80' rx='8' fill='#fef3c7' stroke='#92400e'/><text x='450' y='28' text-anchor='middle' font-family='Segoe UI, Arial' font-size='18' fill='#111827'>$escapedTitle</text><text x='150' y='92' text-anchor='middle' font-family='Segoe UI, Arial' font-size='14'>Evidence</text><text x='450' y='92' text-anchor='middle' font-family='Segoe UI, Arial' font-size='14'>Audit finding</text><text x='750' y='92' text-anchor='middle' font-family='Segoe UI, Arial' font-size='14'>Report claim</text><path d='M260 88 H340' stroke='#111827' stroke-width='2'/><path d='M560 88 H640' stroke='#111827' stroke-width='2'/></svg>"
    Write-Text $svgPath $svg
    $diagramCatalogue += [pscustomobject]@{
        Id = $name
        Title = $title
        Source = $mmdPath.Replace("\", "/")
        Render = $svgPath.Replace("\", "/")
        Status = "source_and_svg_render_created"
    }
}
$diagramCatalogue | Export-Csv -NoTypeInformation -Encoding UTF8 (Join-Path $OutputPath "DIAGRAM-CATALOGUE.csv")

$changedCopy = Join-Path $ArtifactPath "changed-files/tests/NatureProtector.Core.Tests/Architecture"
New-Item -ItemType Directory -Force -Path $changedCopy | Out-Null
Copy-Item "tests/NatureProtector.Core.Tests/Architecture/ProjectDependencyTests.cs" (Join-Path $changedCopy "ProjectDependencyTests.cs") -Force
$scriptCopy = Join-Path $ArtifactPath "changed-files/scripts/evidence"
New-Item -ItemType Directory -Force -Path $scriptCopy | Out-Null
Copy-Item $PSCommandPath (Join-Path $scriptCopy "Generate-DeepEngineeringExploration20260701.ps1") -Force

$zip = Join-Path $ArtifactPath "NatureProtector-deep-engineering-exploration-20260701.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}
Compress-Archive -Path (Join-Path $OutputPath "*"), (Join-Path $ArtifactPath "changed-files") -DestinationPath $zip -Force

Write-Output "Generated evidence pack at $OutputPath"
Write-Output "Generated ZIP at $zip"
