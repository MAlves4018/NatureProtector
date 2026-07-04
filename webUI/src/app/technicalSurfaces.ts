import type {
  ControlledValidationP3AvailabilityResponse,
  RabbitMqMetricsResponse,
  RabbitMqQueueMetricResponse,
  RuntimeEvidenceCatalogResponse,
  RuntimeOperationalHealthResponse,
  RuntimeRunAuditResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  SimulationRunResponse,
  User,
} from './types';
import { formatUiDate, translate, UiLocale } from './i18n';

export type UiTechnicalState =
  | 'ready'
  | 'partial'
  | 'blocked'
  | 'unknown'
  | 'not-available'
  | 'not-instrumented'
  | 'not-confirmed'
  | 'no-evidence';

export interface UiTechnicalField {
  label: string;
  value: string;
  state: UiTechnicalState;
  source: string;
  timestamp: string;
  scope: string;
  limitation: string;
}

export interface UiPipelineSurface {
  state: UiTechnicalState;
  summary: string;
  fields: UiTechnicalField[];
  limitations: string[];
}

export interface UiQaSuite {
  suiteId: string;
  suiteName: string;
  category: string;
  testDefinition: string;
  testExecution: string;
  status: string;
  executedAt: string | null;
  environment: string;
  passed: number | null;
  failed: number | null;
  skipped: number | null;
  blocked: number | null;
  duration: string;
  coverage: string;
  reportReference: string;
  evidenceReference: string;
  limitations: string[];
}

export interface UiEvidenceItem {
  evidenceId: string;
  title: string;
  type: string;
  source: string;
  createdAt: string | null;
  executedAt: string | null;
  environment: string;
  scope: string;
  supportsClaims: string[];
  doesNotSupportClaims: string[];
  availability: UiTechnicalState;
  reference: string;
  limitations: string[];
}

export interface UiAdminAction {
  capability: string;
  action: string;
  riskLevel: 'Low' | 'Medium' | 'High';
  authorizationState: string;
  confirmationRequired: string;
  auditAvailable: string;
  availability: UiTechnicalState;
  limitations: string[];
}

export interface UiP3Surface {
  objective: string;
  status: string;
  integrationStatus: string;
  expectedInputs: string;
  expectedOutputs: string;
  existingEvidence: string;
  readiness: string;
  nextGate: string;
  fields: UiTechnicalField[];
  limitations: string[];
}

export interface UiReadinessItem {
  item: string;
  status: UiTechnicalState;
  evidence: string;
  limitation: string;
}

export function buildUiPipelineSurface(
  input: {
    summary: RuntimeSummaryResponse | null;
    run: RuntimeRunSummaryResponse | SimulationRunResponse | null;
    audit: RuntimeRunAuditResponse | null;
    timings: RuntimeRunTimingSummaryResponse | null;
    health?: RuntimeOperationalHealthResponse | null;
    rabbitMq?: RabbitMqMetricsResponse | null;
    observabilityError?: Error | null;
  },
  locale: UiLocale,
): UiPipelineSurface {
  const summary = input.summary;
  const run = input.audit?.run ?? input.run ?? summary?.currentRun ?? summary?.latestRun ?? null;
  const generatedAt = formatUiDate(summary?.generatedAtUtc, locale);
  const runTimestamp = formatUiDate(run?.endedAt ?? run?.startedAt ?? run?.createdAt, locale);
  const latestFailed = summary?.pipeline.latestFailedAttempts[0] ?? null;
  const latestRejected = summary?.pipeline.latestRejected[0] ?? null;
  const latestQuarantined = summary?.pipeline.latestQuarantined[0] ?? null;
  const rabbitMq = input.rabbitMq ?? input.health?.rabbitMq ?? null;
  const prevention = findComponent(input.health, 'Prevention.Host');
  const rabbitMqHealth = findComponent(input.health, 'RabbitMQ');
  const simulator = findComponent(input.health, 'Simulator.Host');
  const postgres = findComponent(input.health, 'PostgreSQL');
  const influx = findComponent(input.health, 'InfluxDB');
  const grafana = findComponent(input.health, 'Grafana');
  const ingestionQueue = findQueue(rabbitMq, 'np.ingestion.readings');
  const observabilityQueue = findQueue(rabbitMq, 'np.observability.raw');

  const fields: UiTechnicalField[] = [
    field(
      'Run ID',
      run?.id,
      run ? 'ready' : 'not-available',
      'control.simulation_runs',
      runTimestamp,
      'Selected/latest run',
      run ? none(locale) : noRecentExecution(locale),
    ),
    field(
      'Area',
      run?.areaCode ?? summary?.areaCode,
      (run?.areaCode ?? summary?.areaCode) ? 'ready' : 'not-available',
      'runtime summary / control.simulation_runs',
      generatedAt,
      'Area code',
      '',
    ),
    field(
      'Scenario',
      run?.scenarioCode,
      run?.scenarioCode ? 'ready' : 'not-available',
      'control.simulation_runs',
      runTimestamp,
      'Scenario code',
      '',
    ),
    field(
      'Correlation ID',
      getRunCorrelationId(run),
      getRunCorrelationId(run) ? 'partial' : 'not-available',
      'SimulationRun.MetadataJson',
      runTimestamp,
      'Run metadata',
      getRunCorrelationId(run) ? 'Metadata field; not a distributed trace.' : 'Not present in selected run metadata.',
    ),
    field(
      'Pipeline state',
      summary?.currentRun ? 'Running run observed' : run?.status,
      run?.status ? 'partial' : 'unknown',
      'runtime summary',
      generatedAt,
      'Runtime summary window',
      'A loaded summary is not a full service health check.',
    ),
    field(
      'Prevention.Host health',
      prevention?.status,
      statusToState(prevention?.status),
      prevention?.source ?? 'runtime operational health',
      formatUiDate(prevention?.observedAt, locale),
      prevention?.scope ?? 'Service health',
      prevention?.limitation ?? prevention?.reason ?? notConfirmed(locale),
    ),
    field(
      'RabbitMQ health',
      rabbitMqHealth?.status ?? rabbitMq?.collectionStatus,
      statusToState(rabbitMqHealth?.status ?? rabbitMq?.collectionStatus),
      rabbitMqHealth?.source ?? rabbitMq?.source ?? 'RabbitMQ Management API',
      formatUiDate(rabbitMqHealth?.observedAt ?? rabbitMq?.observedAt, locale),
      rabbitMqHealth?.scope ?? 'Broker queues',
      rabbitMqHealth?.limitation ??
        rabbitMqHealth?.reason ??
        (rabbitMq ? none(locale) : 'RabbitMQ metrics endpoint not loaded.'),
    ),
    field(
      'Simulator lifecycle',
      simulator?.status,
      statusToState(simulator?.status),
      simulator?.source ?? 'runtime operational health',
      formatUiDate(simulator?.observedAt, locale),
      simulator?.scope ?? 'Latest run lifecycle',
      simulator?.limitation ?? simulator?.reason ?? notConfirmed(locale),
    ),
    field(
      'PostgreSQL health',
      postgres?.status,
      statusToState(postgres?.status),
      postgres?.source ?? 'runtime operational health',
      formatUiDate(postgres?.observedAt, locale),
      postgres?.scope ?? 'Control-plane database',
      postgres?.limitation ?? postgres?.reason ?? notConfirmed(locale),
    ),
    field(
      'InfluxDB health',
      influx?.status,
      statusToState(influx?.status),
      influx?.source ?? 'runtime operational health',
      formatUiDate(influx?.observedAt, locale),
      influx?.scope ?? 'InfluxDB endpoint',
      influx?.limitation ?? influx?.reason ?? notConfirmed(locale),
    ),
    field(
      'Grafana health',
      grafana?.status,
      statusToState(grafana?.status),
      grafana?.source ?? 'runtime operational health',
      formatUiDate(grafana?.observedAt, locale),
      grafana?.scope ?? 'Grafana endpoint',
      grafana?.limitation ?? grafana?.reason ?? notConfirmed(locale),
    ),
    field(
      'Produced at',
      null,
      'not-instrumented',
      'Simulator payload timestamps',
      notAvailable(locale),
      'Per-event stage timestamp',
      'Produced timestamps are not exposed by the runtime contracts.',
    ),
    field(
      'Published at',
      null,
      'not-instrumented',
      'RabbitMQ publisher',
      notAvailable(locale),
      'Per-event stage timestamp',
      'Publisher confirm/published timestamp is not exposed.',
    ),
    field(
      'Consumed at',
      input.timings?.firstInboxReceivedAt,
      input.timings?.firstInboxReceivedAt ? 'partial' : 'not-available',
      'pipeline.event_inbox',
      formatUiDate(input.timings?.firstInboxReceivedAt, locale),
      'Selected run timing summary',
      input.timings?.firstInboxReceivedAt ? 'Represents first inbox row, not every event.' : noEvidence(locale),
    ),
    field(
      'Processed at',
      input.timings?.lastProcessingAttemptFinishedAt,
      input.timings?.lastProcessingAttemptFinishedAt ? 'partial' : 'not-available',
      'pipeline.processing_attempts',
      formatUiDate(input.timings?.lastProcessingAttemptFinishedAt, locale),
      'Selected run timing summary',
      input.timings?.lastProcessingAttemptFinishedAt
        ? 'Represents last exposed processing attempt finish.'
        : noEvidence(locale),
    ),
    field(
      'Risk persisted at',
      input.timings?.firstRiskAssessmentCreatedAt,
      input.timings?.firstRiskAssessmentCreatedAt ? 'partial' : 'not-available',
      'projection.risk_assessment_log',
      formatUiDate(input.timings?.firstRiskAssessmentCreatedAt, locale),
      'Selected run timing summary',
      input.timings?.firstRiskAssessmentCreatedAt
        ? 'First risk assessment only; not end-to-end latency.'
        : noEvidence(locale),
    ),
    field(
      'Projected at',
      summary?.areaOperationalState?.lastProjectionUpdatedAt,
      summary?.areaOperationalState?.lastProjectionUpdatedAt ? 'partial' : 'not-available',
      'projection.area_operational_state',
      formatUiDate(summary?.areaOperationalState?.lastProjectionUpdatedAt, locale),
      'Current area projection',
      summary?.areaOperationalState ? 'Current projection can be affected by later runs.' : noEvidence(locale),
    ),
    field(
      'Retry count',
      input.audit ? String(input.audit.retryAttempts) : null,
      input.audit ? 'ready' : 'not-available',
      'pipeline.processing_attempts',
      runTimestamp,
      'Selected run audit',
      input.audit ? none(locale) : noEvidence(locale),
    ),
    field(
      'Rejection reason',
      latestRejected ? `${latestRejected.rejectionCode}: ${latestRejected.rejectionReason}` : null,
      latestRejected ? 'partial' : 'not-available',
      'pipeline.rejected_events',
      formatUiDate(latestRejected?.rejectedAt, locale),
      'Recent runtime summary window',
      latestRejected ? 'Latest summary item only.' : 'No recent rejection exposed in this summary window.',
    ),
    field(
      'Quarantine reason',
      latestQuarantined ? `${latestQuarantined.quarantineCode}: ${latestQuarantined.quarantineReason}` : null,
      latestQuarantined ? 'partial' : 'not-available',
      'pipeline.quarantined_events',
      formatUiDate(latestQuarantined?.quarantinedAt, locale),
      'Recent runtime summary window',
      latestQuarantined ? 'Latest summary item only.' : 'No recent quarantine exposed in this summary window.',
    ),
    field(
      'Error code',
      latestFailed?.errorCode,
      latestFailed?.errorCode ? 'partial' : 'not-available',
      'pipeline.processing_attempts',
      formatUiDate(latestFailed?.finishedAt ?? latestFailed?.startedAt, locale),
      'Recent failed attempts',
      latestFailed ? 'Latest failed attempt only.' : 'No failed attempt exposed in this summary window.',
    ),
    field(
      'Ingestion ready',
      metricValue(ingestionQueue?.messagesReady),
      queueMetricState(ingestionQueue),
      ingestionQueue?.source ?? 'RabbitMQ Management API',
      formatUiDate(ingestionQueue?.observedAt, locale),
      'np.ingestion.readings messages_ready',
      ingestionQueue?.limitation ?? none(locale),
    ),
    field(
      'Ingestion unacknowledged',
      metricValue(ingestionQueue?.messagesUnacknowledged),
      queueMetricState(ingestionQueue),
      ingestionQueue?.source ?? 'RabbitMQ Management API',
      formatUiDate(ingestionQueue?.observedAt, locale),
      'np.ingestion.readings messages_unacknowledged',
      ingestionQueue?.limitation ?? none(locale),
    ),
    field(
      'Ingestion consumers',
      metricValue(ingestionQueue?.consumers),
      queueMetricState(ingestionQueue),
      ingestionQueue?.source ?? 'RabbitMQ Management API',
      formatUiDate(ingestionQueue?.observedAt, locale),
      'np.ingestion.readings consumers',
      ingestionQueue?.limitation ?? none(locale),
    ),
    field(
      'Observability ready',
      metricValue(observabilityQueue?.messagesReady),
      queueMetricState(observabilityQueue),
      observabilityQueue?.source ?? 'RabbitMQ Management API',
      formatUiDate(observabilityQueue?.observedAt, locale),
      'np.observability.raw messages_ready',
      observabilityQueue?.limitation ?? none(locale),
    ),
    field(
      'Queue state',
      rabbitMq?.collectionStatus,
      statusToState(rabbitMq?.collectionStatus),
      rabbitMq?.source ?? 'RabbitMQ health/management',
      formatUiDate(rabbitMq?.observedAt, locale),
      'Broker queue metrics',
      rabbitMq
        ? 'Ready/unacknowledged/total are measured only when collectionStatus=Measured.'
        : 'RabbitMQ metrics endpoint not loaded.',
    ),
    field(
      'Latency',
      formatLatency(input.timings),
      input.timings?.runDurationMs != null ? 'partial' : 'not-available',
      'runtime run timings',
      runTimestamp,
      'Selected run',
      input.timings?.runDurationMs != null
        ? 'Run duration and first-stage timings only; no full per-event latency.'
        : noEvidence(locale),
    ),
    field(
      'Readiness',
      summary ? 'Runtime summary readable' : null,
      summary ? 'partial' : 'unknown',
      'GET /api/control/runtime/summary',
      generatedAt,
      'Technical read path',
      summary ? 'Readiness is limited to this contract load, not full staging readiness.' : notConfirmed(locale),
    ),
  ];

  const limitations = [
    'Pipeline health is not inferred from absence of errors.',
    input.rabbitMq || input.health?.rabbitMq
      ? 'RabbitMQ queue metrics distinguish measured zero from unavailable values.'
      : 'RabbitMQ queue metrics are not loaded in this UI session.',
    'Publisher timestamps remain gated because the published RabbitMQ contract has no persisted PublishedAt.',
    'Current projections can reflect later runs; use run-scoped audit/timings when available.',
    ...(input.observabilityError ? [input.observabilityError.message] : []),
    ...(summary?.limitations.map((item) => item.message) ?? []),
    ...(input.health?.limitations.map((item) => item.message) ?? []),
    ...(rabbitMq?.limitations.map((item) => item.message) ?? []),
    ...(input.audit?.limitations.map((item) => item.message) ?? []),
    ...(input.timings?.limitations ?? []),
  ];

  return {
    state: summary ? 'partial' : 'unknown',
    summary: summary
      ? `Runtime summary generated at ${generatedAt} for ${summary.areaCode ?? 'all areas'}.`
      : 'No runtime summary has been loaded for this UI session.',
    fields,
    limitations: unique(limitations),
  };
}

export function buildUiQaSuites(): UiQaSuite[] {
  return [
    {
      suiteId: 'm04-ui-focused',
      suiteName: 'Focused frontend tests',
      category: 'component, i18n, accessibility smoke, capability UX',
      testDefinition: 'npm test -- src/app src/app/services/api.test.ts',
      testExecution: 'Last recorded execution',
      status: 'Passed',
      executedAt: '2026-06-14',
      environment: 'Local M04 handoff',
      passed: 20,
      failed: 0,
      skipped: 0,
      blocked: 0,
      duration: 'Not recorded',
      coverage: 'app 81.28% lines',
      reportReference: 'NatureProtector.brain/control/M04-CORE-CAPABILITY-EXPANSION/handoff.md',
      evidenceReference: 'M04 browser-smoke-summary.md and handoff',
      limitations: ['Prior mission evidence; M05 final gates must be read from the M05 handoff after execution.'],
    },
    {
      suiteId: 'm04-backoffice-api',
      suiteName: 'Backoffice API authorization/runtime tests',
      category: 'API, integration, authorization',
      testDefinition:
        'dotnet test tests/NatureProtector.Backoffice.Api.Tests/NatureProtector.Backoffice.Api.Tests.csproj',
      testExecution: 'Last recorded execution',
      status: 'Passed',
      executedAt: '2026-06-14',
      environment: 'Local M04 handoff',
      passed: 91,
      failed: 0,
      skipped: 0,
      blocked: 0,
      duration: 'Not recorded',
      coverage: 'Backend baseline before M05: 82% lines / 68.1% branches',
      reportReference: 'NatureProtector.brain/control/M04-CORE-CAPABILITY-EXPANSION/handoff.md',
      evidenceReference: 'Backoffice.Api.Tests output recorded in M04 handoff',
      limitations: ['Recorded result includes known NU1902 dependency warning; not external validation.'],
    },
    {
      suiteId: 'm05-final-gates',
      suiteName: 'M05 final local gates',
      category: 'typecheck, focused tests, build, coverage, diff check',
      testDefinition:
        'npm run typecheck; npm test; npm run test:coverage -- src/app src/app/services/api.test.ts; npm run build; dotnet test NatureProtector.sln --no-restore',
      testExecution: 'Last recorded execution',
      status: 'Passed with dependency findings recorded',
      executedAt: '2026-06-14',
      environment: 'Local M05 run',
      passed: 1212,
      failed: 0,
      skipped: 0,
      blocked: 0,
      duration: 'Frontend all tests 5.83s; dotnet solution 67.8s; build 10.85s',
      coverage: 'app 84.12% lines; all frontend 30.72% lines',
      reportReference: 'NatureProtector.brain/control/M05-TECHNICAL-QA-AND-HARDENING/handoff.md',
      evidenceReference: 'webUI/test-results/vitest-junit.xml plus terminal validation recorded in M05 handoff',
      limitations: [
        'Counts combine broad frontend and .NET solution test totals; typecheck/build/security checks are command gates, not test cases.',
      ],
    },
    {
      suiteId: 'security-dependency-checks',
      suiteName: 'Security and dependency checks',
      category: 'security',
      testDefinition: 'Targeted secret scan, npm audit, dotnet vulnerable package listing when executed',
      testExecution: 'Last recorded execution',
      status: 'Findings recorded',
      executedAt: '2026-06-14',
      environment: 'Local M05 run',
      passed: null,
      failed: null,
      skipped: null,
      blocked: null,
      duration: 'Not available',
      coverage: 'Not applicable',
      reportReference: 'NatureProtector.brain/control/M05-TECHNICAL-QA-AND-HARDENING/handoff.md',
      evidenceReference: 'npm audit JSON output, dotnet list package output, targeted rg scan',
      limitations: [
        'npm audit returned 3 high vulnerabilities in Vite/esbuild chain; no forced or major dependency fix applied in M05.',
        'dotnet vulnerable package listing returned known moderate OpenTelemetry advisory NU1902.',
        'Targeted scan found only test-token/password test fixture strings and technical authorization labels.',
      ],
    },
  ];
}

export function buildUiEvidenceItems(
  input: {
    summary: RuntimeSummaryResponse | null;
    run: RuntimeRunSummaryResponse | SimulationRunResponse | null;
    audit: RuntimeRunAuditResponse | null;
    timings: RuntimeRunTimingSummaryResponse | null;
    catalog?: RuntimeEvidenceCatalogResponse | null;
  },
  locale: UiLocale,
): UiEvidenceItem[] {
  const run = input.audit?.run ?? input.run ?? input.summary?.latestRun ?? null;
  const catalogItems: UiEvidenceItem[] = (input.catalog?.items ?? []).slice(0, 8).map((item) => ({
    evidenceId: item.evidenceId,
    title: item.title,
    type: `HTTP evidence ${item.type}`,
    source: 'GET /api/control/runtime/observability/evidence',
    createdAt: item.generatedAt,
    executedAt: item.generatedAt,
    environment: item.environment,
    scope: item.scope,
    supportsClaims: ['Evidence artifact is listed by the allowlisted HTTP catalog.'],
    doesNotSupportClaims: ['Arbitrary filesystem access', 'Brain exposure', 'scientific validation'],
    availability: item.contentAvailable ? 'ready' : 'not-available',
    reference: `/api/control/runtime/observability/evidence/${item.evidenceId}`,
    limitations: item.limitation ? [item.limitation] : [],
  }));

  return [
    ...catalogItems,
    {
      evidenceId: 'm05-initial-snapshot',
      title: 'M05 initial workspace snapshot',
      type: 'Git/runtime snapshot',
      source: 'NatureProtector.brain control folder',
      createdAt: '2026-06-14',
      executedAt: '2026-06-14',
      environment: 'Local workspace',
      scope: 'Git read-only state, diff, containers, listening ports',
      supportsClaims: ['M05 entry state was captured before code changes.'],
      doesNotSupportClaims: ['Scientific validation', 'runtime correctness', 'external deployment readiness'],
      availability: 'ready',
      reference: 'NatureProtector.brain/control/M05-TECHNICAL-QA-AND-HARDENING/initial-snapshot/',
      limitations: ['Snapshot may include pre-existing M02-M04 dirty tree changes.'],
    },
    {
      evidenceId: 'm04-browser-smoke-run',
      title: 'M04 local browser smoke run',
      type: 'Runtime side effect',
      source: 'control.simulation_runs read-only SQL query',
      createdAt: '2026-06-14T02:20:04Z',
      executedAt: '2026-06-14T02:20:04Z',
      environment: 'Local development runtime',
      scope: 'Run 66c877d8-eb4e-4d63-aac2-78899917a884, area proenca-a-nova, scenario_b, 1 cycle, interval 1s, seed 42',
      supportsClaims: ['M04 UI could submit the existing runtime run endpoint as Admin.'],
      doesNotSupportClaims: ['Clean demo baseline', 'scientific evidence', 'production readiness'],
      availability: 'partial',
      reference: 'NatureProtector.brain/control/M04-CORE-CAPABILITY-EXPANSION/browser-smoke-summary.md',
      limitations: ['The run is preserved as local state and must not be hidden by destructive reset.'],
    },
    {
      evidenceId: 'runtime-summary-current-session',
      title: 'Runtime summary loaded by the platform',
      type: 'API runtime state',
      source: 'GET /api/control/runtime/summary',
      createdAt: input.summary?.generatedAtUtc ?? null,
      executedAt: input.summary?.generatedAtUtc ?? null,
      environment: 'Current UI/API session',
      scope: input.summary
        ? `${input.summary.areaCode ?? 'all areas'}, recent window ${input.summary.recentWindowMinutes} minutes`
        : 'No summary loaded',
      supportsClaims: input.summary ? ['UI displays persisted runtime summary fields without frontend scoring.'] : [],
      doesNotSupportClaims: ['RabbitMQ queue health', 'full service health', 'external validation'],
      availability: input.summary ? 'partial' : 'not-available',
      reference: 'webUI/src/app/services/api.ts',
      limitations: input.summary
        ? ['Summary freshness depends on persisted projections and selected recent window.']
        : [noEvidence(locale)],
    },
    {
      evidenceId: 'selected-run-audit-timings',
      title: 'Selected run audit and timings',
      type: 'Run-scoped API evidence',
      source: 'GET /api/control/runtime/runs/{id}/audit and /timings',
      createdAt: run?.createdAt ?? null,
      executedAt: run?.endedAt ?? run?.startedAt ?? null,
      environment: 'Current UI/API session',
      scope: run ? `Run ${run.id}` : 'No selected run',
      supportsClaims:
        input.audit || input.timings ? ['Run-scoped audit/timing details are available for the selected run.'] : [],
      doesNotSupportClaims: ['Per-event end-to-end latency for every pipeline stage'],
      availability: input.audit || input.timings ? 'partial' : 'not-available',
      reference: 'src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeController.cs',
      limitations: ['Timing summary exposes selected persisted stages only.'],
    },
    {
      evidenceId: 'p3-experimental-context',
      title: 'P3 controlled-validation context',
      type: 'Experimental documentation/API context',
      source: 'Dev controlled-validation controller and P3 evidence docs',
      createdAt: '2026-06-14',
      executedAt: null,
      environment: 'Development/Evidence only when authorized',
      scope: 'P3 negative-pipeline controlled-validation candidate',
      supportsClaims: ['P3 is defined as a separate experimental context.'],
      doesNotSupportClaims: ['P3 integrated into scoring', 'P3 runtime executed by M05', 'P3 externally validated'],
      availability: 'partial',
      reference: 'src/NatureProtector.Backoffice.Api/Controllers/DevControlledValidationController.cs',
      limitations: ['M05 does not execute or integrate P3.'],
    },
  ];
}

export function buildUiAdminActions(user: Pick<User, 'roles'> | null | undefined): UiAdminAction[] {
  const roles = user?.roles ?? [];
  const isAdmin = roles.includes('Admin');
  const isSim = roles.includes('Sim');
  const canRuntimeWrite = isAdmin || isSim;

  return [
    {
      capability: 'simulation.execute',
      action: 'Start runtime simulation run',
      riskLevel: 'Medium',
      authorizationState: canRuntimeWrite ? 'Backend allows Sim/Admin in Development' : 'Backend denies this profile',
      confirmationRequired: 'Requested/resolved review before submit; creates local runtime data',
      auditAvailable: 'Run row, audit endpoint and timing endpoint when run is created',
      availability: canRuntimeWrite ? 'partial' : 'blocked',
      limitations: ['M05 does not change backend authorization or run semantics.'],
    },
    {
      capability: 'admin.execute',
      action: 'Runtime reset',
      riskLevel: 'High',
      authorizationState: 'Backend endpoint exists for Sim/Admin in Development; exposed via M05 UI with dry-run',
      confirmationRequired: 'RESET confirm token and dry-run support in backend contract',
      auditAvailable: 'Before/after table counts in reset response',
      availability: isAdmin ? 'partial' : 'blocked',
      limitations: ['Preserving M04 smoke state is required; reset can be destructive. Dry-run is available before execution.'],
    },
    {
      capability: 'admin.execute',
      action: 'Execute runtime diagnostics',
      riskLevel: 'Low',
      authorizationState: 'Backend POST diagnostics requires Sim/Admin',
      confirmationRequired: 'No destructive confirmation; request is read-oriented but POST-gated',
      auditAvailable: 'Structured diagnostic result, no persistent audit added by M05',
      availability: 'not-available',
      limitations: ['M05 displays diagnostic source availability but does not add a diagnostic execution UI.'],
    },
    {
      capability: 'p3.run',
      action: 'Start P3 controlled validation',
      riskLevel: 'Medium',
      authorizationState: 'Backend requires Sim/Admin and Development/Evidence environment',
      confirmationRequired: 'P3 run request contract exists; M05 does not expose execution',
      auditAvailable: 'Evidence/query pack paths when backend run is executed outside M05',
      availability: 'blocked',
      limitations: ['P3 remains experimental and not integrated.'],
    },
    {
      capability: 'admin.read',
      action: 'User/role administration',
      riskLevel: 'High',
      authorizationState: isAdmin ? 'Existing user-role endpoints require Admin' : 'Not available to this profile',
      confirmationRequired: 'Out of M05 scope',
      auditAvailable: 'Not assessed by M05',
      availability: 'not-available',
      limitations: ['M05 does not redesign auth, roles, or user management.'],
    },
  ];
}

export function buildUiP3Surface(
  availability: ControlledValidationP3AvailabilityResponse | null,
  availabilityError: Error | null,
  locale: UiLocale,
): UiP3Surface {
  const counts = availability
    ? `${availability.messageCount} messages, ${availability.executableCases} executable cases, ${availability.blockedCases} blocked cases`
    : notConfirmed(locale);
  const readiness = availability
    ? `${availability.available ? 'Available' : 'Not available'} in ${availability.environment}: ${availability.message}`
    : availabilityError
      ? `Not confirmed: ${availabilityError.message}`
      : 'Not confirmed for this profile/session';

  return {
    objective: 'Controlled validation P3 negative-pipeline candidate context.',
    status: 'Experimental / not externally validated',
    integrationStatus: 'Not integrated into scoring, alert semantics, or the main simulator runtime by M05.',
    expectedInputs: counts,
    expectedOutputs: 'Evidence path, query pack path and run audit requirement when executed outside M05.',
    existingEvidence: 'P3 controller/tests and controlled-validation evidence references in repository documentation.',
    readiness,
    nextGate: 'Dedicated evidence review before any integration decision.',
    fields: [
      field(
        'Phase',
        availability?.phase ?? 'P3NegativePipeline',
        availability ? 'ready' : 'partial',
        'DevControlledValidationController',
        notAvailable(locale),
        'P3 availability contract',
        '',
      ),
      field(
        'Runtime availability',
        availability?.available == null ? null : String(availability.available),
        availability ? (availability.available ? 'partial' : 'blocked') : 'not-confirmed',
        'GET /api/dev/controlled-validation/p3',
        notAvailable(locale),
        'Authorized Sim/Admin profiles only',
        availability ? availability.message : 'Endpoint is backend-protected and was not queried for this profile.',
      ),
      field(
        'Integration status',
        'Not integrated',
        'blocked',
        'M05 scope guardrail',
        '2026-06-14',
        'Main runtime/scoring/alerts',
        'No P3 runtime/scoring integration was added.',
      ),
      field(
        'Validation status',
        'Not externally validated',
        'no-evidence',
        'Project stance',
        '2026-06-14',
        'Scientific/operational validation',
        'P3 remains candidate evidence only.',
      ),
    ],
    limitations: [
      'M05 does not start P3 controlled validation.',
      'M05 does not integrate P3 into scoring, alert semantics, RabbitMQ events, schema, or simulator runtime.',
      'Availability endpoint is backend-protected and environment-gated.',
    ],
  };
}

export function buildUiReadinessItems(input: {
  summary: RuntimeSummaryResponse | null;
  run: RuntimeRunSummaryResponse | SimulationRunResponse | null;
  user: Pick<User, 'roles'> | null | undefined;
}): UiReadinessItem[] {
  const roles = input.user?.roles ?? [];
  return [
    {
      item: 'Docker local services',
      status: 'not-confirmed',
      evidence: 'M05 handoff records np-postgres, np-rabbitmq, np-influxdb and np-grafana active at entry.',
      limitation: 'The browser UI cannot verify Docker health directly.',
    },
    {
      item: 'Runtime API read path',
      status: input.summary ? 'partial' : 'unknown',
      evidence: input.summary ? 'Runtime summary loaded in current UI session.' : 'No runtime summary loaded.',
      limitation: 'Summary load is not a full health/readiness probe.',
    },
    {
      item: 'Demo run selection',
      status: input.run ? 'partial' : 'not-available',
      evidence: input.run ? `Selected/latest run ${input.run.id}.` : 'No run selected or loaded.',
      limitation: 'M04 smoke run is preserved and may affect latest-run ordering.',
    },
    {
      item: 'Profiles',
      status: roles.length > 0 ? 'partial' : 'not-confirmed',
      evidence:
        roles.length > 0 ? `Current profile roles: ${roles.join(', ')}.` : 'Unsigned prototype read-only profile.',
      limitation: 'Real Pipeline/Sim/Admin browser journeys require existing local identities.',
    },
    {
      item: 'Reset/rebaseline',
      status: 'blocked',
      evidence: 'M05 guardrail: no destructive reset or volume deletion.',
      limitation: 'Clean demo requires an explicit safe reset/rebaseline decision outside this UI action.',
    },
  ];
}

function field(
  label: string,
  value: string | number | null | undefined,
  state: UiTechnicalState,
  source: string,
  timestamp: string,
  scope: string,
  limitation: string,
): UiTechnicalField {
  return {
    label,
    value: value === null || value === undefined || value === '' ? valueForState(state) : String(value),
    state,
    source,
    timestamp,
    scope,
    limitation,
  };
}

function getRunCorrelationId(run: RuntimeRunSummaryResponse | SimulationRunResponse | null) {
  return run && 'orchestratorCorrelationId' in run ? run.orchestratorCorrelationId : null;
}

function formatLatency(timings: RuntimeRunTimingSummaryResponse | null) {
  if (!timings || timings.runDurationMs == null) {
    return null;
  }

  const parts = [`run ${Math.round(timings.runDurationMs)}ms`];
  if (timings.timeToFirstInboxMs != null) {
    parts.push(`first inbox ${Math.round(timings.timeToFirstInboxMs)}ms`);
  }
  if (timings.timeToFirstProcessingAttemptMs != null) {
    parts.push(`first processing ${Math.round(timings.timeToFirstProcessingAttemptMs)}ms`);
  }
  if (timings.timeToFirstRiskAssessmentMs != null) {
    parts.push(`first risk ${Math.round(timings.timeToFirstRiskAssessmentMs)}ms`);
  }
  return parts.join('; ');
}

function findComponent(health: RuntimeOperationalHealthResponse | null | undefined, component: string) {
  return health?.components.find((item) => item.component === component) ?? null;
}

function findQueue(rabbitMq: RabbitMqMetricsResponse | null | undefined, queueName: string) {
  return rabbitMq?.queues.find((item) => item.queueName === queueName) ?? null;
}

function statusToState(status: string | null | undefined): UiTechnicalState {
  switch (status) {
    case 'Healthy':
    case 'Measured':
      return 'ready';
    case 'Degraded':
    case 'NotApplicable':
      return 'partial';
    case 'Unhealthy':
      return 'blocked';
    case 'NotInstrumented':
      return 'not-instrumented';
    case 'Unavailable':
    case 'Error':
      return 'not-available';
    default:
      return 'unknown';
  }
}

function queueMetricState(queue: RabbitMqQueueMetricResponse | null | undefined): UiTechnicalState {
  if (!queue) {
    return 'unknown';
  }

  return queue.collectionStatus === 'Measured' ? 'ready' : statusToState(queue.collectionStatus);
}

function metricValue(value: number | null | undefined) {
  return value === null || value === undefined ? null : String(value);
}

function valueForState(_state: UiTechnicalState) {
  return '-';
}

function notAvailable(locale: UiLocale) {
  return translate(locale, 'value.notAvailable');
}

function notConfirmed(locale: UiLocale) {
  return translate(locale, 'value.notConfirmed');
}

function noEvidence(locale: UiLocale) {
  return translate(locale, 'value.noEvidence');
}

function noRecentExecution(locale: UiLocale) {
  return translate(locale, 'value.noRecentExecution');
}

function none(locale: UiLocale) {
  return translate(locale, 'value.noneReported');
}

function unique(values: string[]) {
  return Array.from(new Set(values.filter(Boolean)));
}


