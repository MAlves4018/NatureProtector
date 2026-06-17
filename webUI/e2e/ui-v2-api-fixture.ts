import type { Page, Route } from '@playwright/test';

export type RoleProfile = 'Admin' | 'Sim' | 'Pipeline' | 'Unknown';
export type SummaryState = 'ready' | 'partial' | 'stale' | 'blocked' | 'null' | 'unknown';
export type SummaryFailure = '500' | 'network' | 'timeout';

export interface UiV2ApiFixtureOptions {
  profile?: RoleProfile;
  loginStatus?: 200 | 401;
  meStatus?: 200 | 401;
  summaryState?: SummaryState;
  summaryFailure?: SummaryFailure;
  startRunStatus?: 200 | 403 | 500;
}

export interface ObservedApiRequest {
  method: string;
  path: string;
  authorization: string | null;
  postData: unknown;
}

export interface UiV2ApiFixture {
  requests: ObservedApiRequest[];
}

const now = '2026-06-16T10:00:00Z';
const runId = '11111111-1111-4111-8111-111111111111';
const startedAt = '2026-06-16T09:55:00Z';
const endedAt = '2026-06-16T09:56:30Z';

export async function installUiV2ApiFixture(
  page: Page,
  options: UiV2ApiFixtureOptions = {},
): Promise<UiV2ApiFixture> {
  const requests: ObservedApiRequest[] = [];

  await page.context().route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/api/, '') || '/';
    requests.push({
      method: request.method(),
      path,
      authorization: request.headers().authorization ?? null,
      postData: parsePostData(request.postData()),
    });

    await handleApiRoute(route, path, options);
  });

  return { requests };
}

async function handleApiRoute(route: Route, path: string, options: UiV2ApiFixtureOptions) {
  const profile = options.profile ?? 'Admin';

  if (path === '/users-roles/login' && route.request().method() === 'POST') {
    if (options.loginStatus === 401) {
      await json(route, { status: 401, title: 'Unauthorized', message: 'Invalid credentials' }, 401);
      return;
    }

    await json(route, loginResponse(profile));
    return;
  }

  if (path === '/users-roles/me') {
    if (options.meStatus === 401 || authorizationHeader(route) === 'Bearer expired-token') {
      await json(route, { status: 401, title: 'Unauthorized', message: 'Session expired' }, 401);
      return;
    }

    await json(route, currentUser(profile));
    return;
  }

  if (path === '/users-roles/logout' && route.request().method() === 'POST') {
    await route.fulfill({ status: 204, body: '' });
    return;
  }

  if (path === '/control/areas') {
    await json(route, [areaFixture]);
    return;
  }

  if (path === '/control/areas/proenca-a-nova/scenarios') {
    await json(route, scenarioFixtures);
    return;
  }

  if (path === '/control/simulation-runs') {
    await json(route, [baseRun()]);
    return;
  }

  if (path === `/control/runtime/runs/${runId}`) {
    await json(route, baseRun());
    return;
  }

  if (path === `/control/runtime/runs/${runId}/audit`) {
    await json(route, runAudit());
    return;
  }

  if (path === `/control/runtime/runs/${runId}/timings`) {
    await json(route, runTimings());
    return;
  }

  if (path === '/control/runtime/summary') {
    await fulfillRuntimeSummary(route, options);
    return;
  }

  if (path === '/control/runtime/observability/health') {
    await json(route, healthResponse());
    return;
  }

  if (path === '/control/runtime/observability/rabbitmq') {
    await json(route, rabbitMqMetrics());
    return;
  }

  if (path === '/control/runtime/observability/evidence') {
    await json(route, evidenceCatalog());
    return;
  }

  if (path === '/control/runtime/observability/evidence/ui-v2-runtime-smoke') {
    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/plain; charset=utf-8',
        'Content-Disposition': 'attachment; filename="ui-v2-runtime-smoke.txt"',
      },
      body: 'ui-v2 runtime smoke evidence',
    });
    return;
  }

  if (path === '/control/runtime/runs' && route.request().method() === 'POST') {
    if (options.startRunStatus === 403) {
      await json(route, { status: 403, title: 'Forbidden', message: 'Forbidden by mock RBAC' }, 403);
      return;
    }

    if (options.startRunStatus === 500) {
      await json(route, { status: 500, title: 'Server Error', message: 'Runtime run failed' }, 500);
      return;
    }

    await json(route, startRunResponse());
    return;
  }

  if (path === '/dev/controlled-validation/p3') {
    await json(route, {
      phase: 'P3NegativePipeline',
      environment: 'Development',
      available: profile === 'Admin',
      message: profile === 'Admin' ? 'P3 controlled validation available' : 'Forbidden for this profile',
      messageCount: 3,
      executableCases: 3,
      blockedCases: 0,
    });
    return;
  }

  if (path === '/dev/controlled-validation/p3/run' && route.request().method() === 'POST') {
    await json(route, {
      requestId: '44444444-4444-4444-8444-444444444444',
      runLabel: 'p3-ui-v2',
      phase: 'P3NegativePipeline',
      status: 'Accepted',
      environment: 'Development',
      message: 'P3 accepted by fixture',
      requestedAtUtc: now,
      messageCount: 3,
      executableCases: 3,
      blockedCases: 0,
      evidencePath: null,
      queryPackPath: null,
      auditRequired: true,
      run: baseRun(),
      notes: [],
    });
    return;
  }

  await json(route, { status: 404, title: 'Not Found', message: `Unhandled E2E API route: ${path}` }, 404);
}

async function fulfillRuntimeSummary(route: Route, options: UiV2ApiFixtureOptions) {
  if (options.summaryFailure === 'network') {
    await route.abort('failed');
    return;
  }

  if (options.summaryFailure === 'timeout') {
    await route.abort('timedout');
    return;
  }

  if (options.summaryFailure === '500') {
    await json(route, { status: 500, title: 'Server Error', message: 'Runtime summary failed' }, 500);
    return;
  }

  if (options.summaryState === 'null') {
    await json(route, null);
    return;
  }

  await json(route, runtimeSummary(options.summaryState ?? 'ready'));
}

function loginResponse(profile: RoleProfile) {
  const user = currentUser(profile);

  return {
    userId: user.id,
    username: user.username,
    fullName: user.fullName,
    email: user.email,
    roles: user.roles,
    token: `${profile.toLowerCase()}-token`,
  };
}

function currentUser(profile: RoleProfile) {
  return {
    id: `00000000-0000-4000-8000-${profileId(profile)}`,
    username: `${profile.toLowerCase()}-operator`,
    fullName: `${profile} Operator`,
    email: `${profile.toLowerCase()}@natureprotector.test`,
    roles: profile === 'Unknown' ? ['Observer'] : [profile],
  };
}

function profileId(profile: RoleProfile) {
  return profile === 'Admin'
    ? '000000000001'
    : profile === 'Sim'
      ? '000000000002'
      : profile === 'Pipeline'
        ? '000000000003'
        : '000000000004';
}

const areaFixture = {
  id: '22222222-2222-4222-8222-222222222222',
  code: 'proenca-a-nova',
  name: 'Proenca-a-Nova',
  countryCode: 'PT',
  configurationVersionNumber: 1,
  gridCellCount: 12,
  sensorNodeCount: 2,
  scenarioCount: 2,
};

const scenarioFixtures = [
  {
    id: '33333333-3333-4333-8333-333333333331',
    code: 'scenario_b',
    name: 'Scenario B nominal',
    scenarioKind: 'Nominal',
    configurationVersionNumber: 1,
    description: 'Nominal fixture',
    baseScenarioCode: null,
    datasetBindingCount: 1,
  },
  {
    id: '33333333-3333-4333-8333-333333333332',
    code: 'scenario_c',
    name: 'Scenario C degraded',
    scenarioKind: 'Degraded',
    configurationVersionNumber: 1,
    description: 'Degraded fixture',
    baseScenarioCode: 'scenario_b',
    datasetBindingCount: 1,
  },
];

function baseRun(status = 'Completed') {
  return {
    id: runId,
    areaCode: 'proenca-a-nova',
    scenarioCode: 'scenario_b',
    scenarioName: 'Scenario B nominal',
    status,
    configurationVersionNumber: 1,
    createdAt: '2026-06-16T09:54:30Z',
    startedAt,
    endedAt,
    durationSeconds: 90,
    logicalStartTimestamp: '2026-06-16T09:00:00Z',
    intervalSeconds: 60,
    numberOfCycles: 3,
    executionSeed: 42,
    metadataJson: '{"orchestratorCorrelationId":"corr-ui-v2"}',
    metadataJsonStatus: 'Valid',
    orchestratorCorrelationId: 'corr-ui-v2',
    runOverrides: {
      requested: {
        sensorCount: 2,
        numberOfCycles: 3,
        intervalSeconds: 60,
        seed: 42,
        degradationProfile: null,
        degradationProfiles: null,
        orchestratorCorrelationId: 'corr-ui-v2',
      },
      resolved: {
        sensorCount: 2,
        numberOfCycles: 3,
        intervalSeconds: 60,
        seed: 42,
        degradationProfile: null,
        degradationProfiles: null,
        orchestratorCorrelationId: 'corr-ui-v2',
      },
      selectedSensorNames: ['Sensor A', 'Sensor B'],
    },
  };
}

function runtimeSummary(state: SummaryState) {
  const stale = state === 'stale';
  const blocked = state === 'blocked';
  const partial = state === 'partial';
  const unknown = state === 'unknown';

  return {
    generatedAtUtc: now,
    recentWindowMinutes: 30,
    areaCode: unknown ? null : 'proenca-a-nova',
    currentRun: null,
    latestRun: baseRun(),
    pipeline: {
      inboxTotal: 8,
      inboxRecent: 4,
      inboxByStatus: [{ status: 'Processed', count: 4 }],
      attemptsRecent: 4,
      attemptsByOutcomeAndError: [{ outcome: 'Success', errorCode: null, count: 4 }],
      rejectedRecent: 1,
      rejectedTotal: 1,
      rejectedByCode: [{ code: 'invalid_metric', count: 1 }],
      quarantinedRecent: 0,
      quarantinedTotal: 0,
      quarantinedByCode: [],
      latestRejected: [
        {
          id: '55555555-5555-4555-8555-555555555551',
          eventId: '55555555-5555-4555-8555-555555555552',
          rejectionCode: 'invalid_metric',
          rejectionReason: 'Unsupported metric in fixture',
          rejectedAt: now,
          metadataJson: null,
        },
      ],
      latestQuarantined: [],
      latestFailedAttempts: [],
    },
    risk: {
      recentCount: blocked || unknown ? 0 : 4,
      minScore: blocked || unknown ? null : 0.21,
      maxScore: blocked || unknown ? null : 0.73,
      latestTimestamp: blocked || unknown ? null : now,
      recentScores: blocked || unknown ? [] : [{ timestamp: now, riskScore: 0.73, riskLevel: 'High' }],
    },
    areaOperationalState: unknown ? null : {
      areaCode: 'proenca-a-nova',
      configurationVersionNumber: 1,
      snapshotTimestamp: now,
      aggregateRiskScore: blocked ? 0 : 0.73,
      aggregateRiskLevel: blocked ? 'Blocked' : 'High',
      severity: blocked ? 'Blocked' : 'Warning',
      summary: blocked ? 'Blocked fixture summary' : 'Fixture risk summary',
      assessmentCount: blocked ? 0 : 4,
      updatedAt: now,
      alertState: 'Warning',
      coverageStatus: partial ? 'Partial' : 'Complete',
      freshnessStatus: stale ? 'Stale' : 'Fresh',
      carryForwardStatus: stale ? 'ExpiredCarryForward' : 'Current',
      lastAssessmentTimestamp: blocked ? null : now,
      lastProjectionUpdatedAt: now,
      operationalStatusReason: blocked ? 'Blocked by missing required observations' : null,
    },
    cellOperationalStateCount: unknown ? 0 : 12,
    activeAlerts: [],
    freshness: {
      freshCount: stale ? 0 : 4,
      staleCount: stale ? 4 : 0,
      expiredCount: 0,
      oldestIncludedAssessment: stale ? '2026-06-15T09:00:00Z' : startedAt,
      latestIncludedAssessment: stale ? '2026-06-15T09:30:00Z' : now,
      freshSeconds: 600,
      staleSeconds: 1800,
      note: stale ? 'stale fixture' : 'fresh fixture',
    },
    scoreComponents: {
      npScore: blocked || unknown ? null : 0.73,
      baseRisk: blocked || unknown ? null : 0.61,
      adjustedScore: blocked || unknown ? null : 0.73,
      score100: blocked || unknown ? null : 73,
      meteorologyComponent: 0.7,
      droughtComponent: 0.5,
      territoryComponent: 0.6,
      hazardComponent: 0.7,
      fuelComponent: 0.4,
      geomorphologyComponent: 0.3,
      confidenceFactor: partial ? 0.6 : 0.9,
      integrityFactor: partial ? 0.7 : 0.95,
      dominantDriver: 'meteorology',
      parameterSetVersion: 'candidate-v1',
      calculationStatus: blocked ? 'Blocked' : partial ? 'PartialButUsable' : 'CompleteEligible',
      limitations: partial ? 'Fixture partial limitation' : null,
      latestAssessmentTimestamp: blocked ? null : now,
      npRiskClass: blocked ? null : 'High',
      npRiskClassLabel: blocked ? null : 'High candidate',
    },
    indexComparison: {
      fireWeatherIndex: partial ? null : 21.3,
      normalizedFireWeatherIndex: partial ? null : 0.7,
      fireWeatherCalculationStatus: partial ? 'Partial' : 'Calculated',
      keetchByramDroughtIndex: 420,
      normalizedKeetchByramDroughtIndex: 0.56,
      kbdiCalculationStatus: partial ? 'LimitedHistory' : 'Calculated',
      provenance: 'fixture',
      limitations: partial ? 'Limited antecedent fixture' : null,
      dailyPrecipitationMillimeters: 0,
      logicalDate: '2026-06-16T00:00:00Z',
      calculatedFireWeatherIndex: 21.3,
      referenceFireWeatherIndex: null,
      fireWeatherIndexValueSource: 'Calculated',
      fireWeatherIpmaClass: 'High',
      fireWeatherIpmaClassLabel: 'High',
      fireWeatherEffisClass: 'High',
      fireWeatherThresholdDistanceToNextClass: 2.3,
      fireWeatherNextIpmaClass: 'VeryHigh',
      calculatedKeetchByramDroughtIndex: 420,
      referenceKeetchByramDroughtIndex: null,
      kbdiValueSource: 'Calculated',
      kbdiDrynessClass: 'Dry',
      kbdiDrynessClassLabel: 'Dry',
      kbdiAntecedentHistoryQuality: partial ? 'Limited' : 'Complete',
      kbdiAntecedentDays: partial ? 3 : 14,
      portugueseContextRiskProxyClass: 'High',
      portugueseContextRiskProxyLabel: 'High proxy',
      territorialHazardProxyClass: 'Elevated',
      localFwiPercentileStatus: 'Available',
      localFwiPercentile: 0.82,
      localFwiPercentileReason: null,
    },
    limitations: partial ? [{ code: 'fixture_partial', message: 'Fixture partial limitation' }] : [],
    warnings: partial ? ['Partial fixture warning'] : [],
  };
}

function runAudit() {
  return {
    run: baseRun(),
    expectedEvents: 6,
    acceptedReadings: 4,
    missingEvents: 1,
    rejected: 1,
    quarantined: 0,
    retryAttempts: 1,
    riskAssessments: 4,
    qualityFlagsSummary: [{ status: 'Complete', count: 4 }],
    eligibilitySummary: [{ status: 'CompleteEligible', count: 4 }],
    areaSnapshot: {
      snapshotTimestamp: now,
      aggregateRiskScore: 0.73,
      aggregateRiskLevel: 'High',
      assessmentCount: 4,
      summary: 'Fixture audit summary',
    },
    limitations: [],
    scoreComponents: runtimeSummary('ready').scoreComponents,
    indexComparison: runtimeSummary('ready').indexComparison,
    dataScope: {
      requestedRunId: runId,
      resolvedRunId: runId,
      dataRunId: runId,
      observedAt: now,
      source: 'fixture',
      scope: 'run',
      limitations: [],
    },
  };
}

function runTimings() {
  return {
    simulationRunId: runId,
    runDurationMs: 90000,
    startedAt,
    endedAt,
    firstInboxReceivedAt: '2026-06-16T09:55:05Z',
    firstProcessingAttemptStartedAt: '2026-06-16T09:55:06Z',
    lastProcessingAttemptFinishedAt: '2026-06-16T09:56:00Z',
    firstRiskAssessmentCreatedAt: '2026-06-16T09:55:12Z',
    firstAlertTriggeredAt: null,
    timeToFirstInboxMs: 5000,
    timeToFirstProcessingAttemptMs: 6000,
    timeToFirstRiskAssessmentMs: 12000,
    timeToFirstAlertMs: null,
    attempts: {
      attemptCount: 4,
      successfulAttempts: 4,
      failedAttempts: 0,
      quarantinedAttempts: 0,
      minDurationMs: 40,
      avgDurationMs: 55,
      maxDurationMs: 70,
    },
    stages: [
      {
        stage: 'RiskAssessment',
        outcome: 'Success',
        errorCode: null,
        count: 4,
        firstStartedAt: '2026-06-16T09:55:06Z',
        lastFinishedAt: '2026-06-16T09:56:00Z',
        minDurationMs: 40,
        avgDurationMs: 55,
        maxDurationMs: 70,
      },
    ],
    limitations: [],
    dataScope: {
      requestedRunId: runId,
      resolvedRunId: runId,
      dataRunId: runId,
      observedAt: now,
      source: 'fixture',
      scope: 'run',
      limitations: [],
    },
    timeline: [],
  };
}

function healthResponse() {
  return {
    observedAt: now,
    components: [
      component('Prevention.Host', 'Healthy'),
      component('RabbitMQ', 'Healthy'),
      component('Simulator.Host', 'Healthy'),
      component('PostgreSQL', 'Healthy'),
      component('InfluxDB', 'Degraded', 'Fixture degraded but reachable'),
      component('Grafana', 'NotApplicable'),
    ],
    rabbitMq: rabbitMqMetrics(),
    limitations: [{ code: 'fixture_health', message: 'Health fixture only' }],
  };
}

function component(componentName: string, status: string, reason = 'Fixture status') {
  return {
    component: componentName,
    status,
    observedAt: now,
    source: 'fixture',
    reason,
    lastSuccessAt: now,
    lastFailureAt: null,
    ageSeconds: 5,
    scope: 'D2 Playwright fixture',
    limitation: null,
  };
}

function rabbitMqMetrics() {
  return {
    observedAt: now,
    source: 'RabbitMQ Management API fixture',
    collectionStatus: 'Measured',
    queues: [
      queue('np.ingestion.readings', 0, 0, 0, 1),
      queue('np.observability.raw', 1, 0, 1, 1),
    ],
    limitations: [],
  };
}

function queue(queueName: string, ready: number, unacknowledged: number, total: number, consumers: number) {
  return {
    queueName,
    messagesReady: ready,
    messagesUnacknowledged: unacknowledged,
    messagesTotal: total,
    consumers,
    observedAt: now,
    source: 'fixture',
    collectionStatus: 'Measured',
    limitation: null,
  };
}

function evidenceCatalog() {
  return {
    observedAt: now,
    items: [
      {
        evidenceId: 'ui-v2-runtime-smoke',
        title: 'Runtime smoke evidence',
        type: 'text',
        generatedAt: now,
        environment: 'Current UI/API session',
        scope: 'D2 Playwright fixture',
        version: 'v1',
        contentAvailable: true,
        downloadAvailable: true,
        size: 28,
        status: 'Ready',
        limitation: null,
      },
    ],
    limitations: [],
  };
}

function startRunResponse() {
  const run = baseRun('Completed');

  return {
    requestId: '66666666-6666-4666-8666-666666666666',
    orchestratorCorrelationId: 'corr-start-ui-v2',
    status: 'Completed',
    message: 'Started by fixture',
    requestedAtUtc: now,
    requested: {
      sensorCount: 2,
      numberOfCycles: 3,
      intervalSeconds: 60,
      seed: 42,
      degradationProfile: 'noise',
      degradationProfiles: ['noise'],
      orchestratorCorrelationId: 'corr-start-ui-v2',
    },
    run: {
      ...run,
      id: '77777777-7777-4777-8777-777777777777',
      orchestratorCorrelationId: 'corr-start-ui-v2',
      metadataJson: '{"orchestratorCorrelationId":"corr-start-ui-v2"}',
    },
    warnings: [],
    logDirectory: null,
    evidenceDirectory: null,
  };
}

async function json(route: Route, body: unknown, status = 200) {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: body === null ? 'null' : JSON.stringify(body),
  });
}

function parsePostData(value: string | null): unknown {
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function authorizationHeader(route: Route) {
  return route.request().headers().authorization ?? null;
}
