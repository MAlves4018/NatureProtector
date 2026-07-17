import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import path from 'node:path';

const outputRoot = process.env.UI_REVISION_SCREENSHOTS;

test.beforeEach(async ({ page }) => {
  await installApiFixture(page);
});

test('public surface remains bounded and accessible', async ({ page }, testInfo) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'NatureProtector' }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
  await capture(page, testInfo.project.name, 'public');

  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((violation) => violation.impact === 'critical')).toEqual([]);
});

test('admin sees the operational overview and responsive shell', async ({ page }, testInfo) => {
  await login(page);

  await expect(page.getByRole('heading', { name: 'Visão geral operacional' })).toBeVisible();
  await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
  await expect(page.getByText(/SimulationRunId/).first()).toBeVisible();
  await expect(page.getByText('Saúde global')).toBeVisible();
  await capture(page, testInfo.project.name, 'overview');

  const result = await new AxeBuilder({ page }).analyze();
  expect(result.violations.filter((violation) => violation.impact === 'critical')).toEqual([]);
});

test('rate limiting is presented with Retry-After countdown', async ({ page }) => {
  await page.route('**/api/control/runtime/runs', (route) =>
    route.fulfill({
      status: 429,
      contentType: 'application/json',
      headers: { 'Retry-After': '3' },
      body: JSON.stringify({ status: 429, title: 'Too Many Requests', message: 'Rate limit reached.' }),
    }),
  );
  await login(page);
  await page.goto('/simulation');
  await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
  await page.getByRole('button', { name: /Revisão/i }).click();
  await page.getByRole('button', { name: /Iniciar simulação/i }).click();
  await expect(page.getByText(/Limite de pedidos atingido. Tente novamente em [0-3]s/)).toBeVisible();
});

test('runtime reset remains guarded and reports a dry-run truthfully', async ({ page }) => {
  await login(page);
  await page.goto('/admin');
  await expect(page.getByRole('heading', { name: 'Runtime reset' })).toBeVisible();
  await page.getByLabel(/Escreva RESET_RUNTIME_STATE/i).fill('RESET_RUNTIME_STATE');
  await page.getByRole('button', { name: 'Executar dry-run' }).click();
  await expect(page.getByRole('heading', { name: 'Pré-visualização do reset' })).toBeVisible();
  await expect(page.getByText('DryRunReady')).toBeVisible();
});

test('recovered product flows expose progress, queries, evidence and deployments', async ({ page }, testInfo) => {
  await login(page);
  await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');

  await page.goto('/simulation');
  await expect(page.getByRole('heading', { name: /^Simulação$/i })).toBeVisible();
  await capture(page, testInfo.project.name, 'simulation-create');

  await page.goto('/runs');
  await capture(page, testInfo.project.name, 'run-empty');
  await page.getByLabel(/selecionar execução/i).selectOption('run-ui-review-001');
  await expect(page).toHaveURL(/runId=run-ui-review-001/);
  await expect(page.getByText('Cockpit da execução')).toBeVisible();
  await page.reload();
  await expect(page.getByLabel(/selecionar execução/i)).toHaveValue('run-ui-review-001');
  await page.getByLabel('Pesquisar ID ou cenário').fill('run-ui-review-002');
  await expect(page.getByRole('cell', { name: 'run-ui-review-002' })).toBeVisible();
  await page.getByLabel('Pesquisar ID ou cenário').fill('');
  await capture(page, testInfo.project.name, 'run-cockpit');

  await page.goto('/scenario-compare');
  await page.getByLabel('Run A').selectOption('run-ui-review-001');
  await page.getByLabel('Run B').selectOption('run-ui-review-002');
  await expect(page).toHaveURL(/runA=run-ui-review-001/);
  await expect(page).toHaveURL(/runB=run-ui-review-002/);
  await page.getByRole('button', { name: /^Comparar$/i }).click();
  await expect(page.getByRole('cell', { name: 'Cenário' })).toBeVisible();
  await expect(page.getByText(/A: run-ui-review-001 · B: run-ui-review-002/)).toBeVisible();
  await capture(page, testInfo.project.name, 'comparison');

  await page.goto('/queries');
  await expect(page.getByRole('heading', { name: 'Consultas preparadas' })).toBeVisible();
  await page.getByRole('button', { name: /Convergência do accounting/i }).click();
  await page.getByRole('button', { name: /Executar preset/i }).click();
  await expect(page.getByRole('columnheader', { name: 'expected' })).toBeVisible();
  await expect(page.getByText(/Resultado associado a SimulationRunId: run-ui-review-001/)).toBeVisible();
  await capture(page, testInfo.project.name, 'prepared-queries');

  await page.goto('/evidence');
  await expect(page.getByRole('heading', { name: 'Cockpit de evidência' })).toBeVisible();
  await capture(page, testInfo.project.name, 'evidence-cockpit');

  await page.goto('/pipeline');
  await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible();
  await capture(page, testInfo.project.name, 'operations');

  await page.goto('/deployments');
  await expect(page.getByRole('heading', { name: 'Deployments' })).toBeVisible();
  await expect(page.getByText(/Queued significa/i)).toBeVisible();
  await capture(page, testInfo.project.name, 'deployments');
});

test('prepared query errors and capability blockers are explicit', async ({ page }, testInfo) => {
  await login(page);
  await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
  await page.route('**/api/control/runtime/runs/run-ui-review-001', (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ status: 503, title: 'Unavailable', message: 'Diagnostic store unavailable.' }),
    }),
  );
  await page.goto('/queries');
  await page.getByRole('button', { name: /Executar preset/i }).click();
  await expect(page.getByText('Diagnostic store unavailable.')).toBeVisible();
  await capture(page, testInfo.project.name, 'query-error');

  await page.unroute('**/api/**');
  await installApiFixture(page, {
    roles: ['Pipeline'],
    capabilities: [
      'demo.read',
      'area.read',
      'risk.read',
      'pipeline.read',
      'run.read',
      'quality.read',
      'evidence.read',
      'evidence.download',
      'evidence.compare',
      'data_context.read',
      'help.read',
    ],
  });
  await page.evaluate(() => {
    localStorage.removeItem('token');
    sessionStorage.clear();
  });
  await login(page);
  await page.goto('/queries');
  await expect(page.getByRole('heading', { name: 'Acesso negado' })).toBeVisible();
  await capture(page, testInfo.project.name, 'role-blocked');
});

test('prepared query results can be exported', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop', 'One browser download proof is sufficient.');
  await login(page);
  await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
  await page.goto('/queries');
  await page.getByRole('button', { name: /Executar preset/i }).click();
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Exportar' }).first().click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.csv$/);
});

async function capture(page: Page, viewport: string, name: string) {
  if (!outputRoot) return;
  await page.screenshot({ path: path.join(outputRoot, `${name}-${viewport}.png`), fullPage: true });
}

async function login(page: Page) {
  await page.goto('/login');
  await page.getByLabel('Username or email').fill('admin');
  await page.getByLabel('Password').fill('password');
  await page.getByRole('button', { name: /^Sign in$/ }).click();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBe('browser-fixture-token');
  await page.goto('/');
}

async function installApiFixture(
  page: Page,
  profile: { roles?: string[]; capabilities?: string[] } = {},
) {
  const roles = profile.roles ?? ['Admin'];
  const capabilities =
    profile.capabilities ??
    [
      'demo.read',
      'area.read',
      'risk.read',
      'pipeline.read',
      'run.read',
      'scenario.read',
      'simulation.read',
      'simulation.execute',
      'qa.read',
      'quality.read',
      'evidence.read',
      'evidence.download',
      'evidence.execute.campaign',
      'evidence.compare',
      'deployment.read',
      'deployment.plan',
      'deployment.deploy.staging',
      'deployment.rollback',
      'cloud.read',
      'approval.review',
      'users.manage',
      'roles.manage',
      'limitations.read',
      'admin.read',
      'admin.execute',
      'p3.read',
      'data_context.read',
      'help.read',
    ];
  const run = {
    id: 'run-ui-review-001',
    areaCode: 'proenca-a-nova',
    scenarioCode: 'scenario_b',
    scenarioName: 'Scenario B - High Risk',
    status: 'Completed',
    configurationVersionNumber: 1,
    createdAt: '2026-07-16T18:00:00Z',
    startedAt: '2026-07-16T18:01:00Z',
    endedAt: '2026-07-16T18:08:00Z',
    durationSeconds: 420,
    logicalStartTimestamp: '2026-07-16T18:00:00Z',
    intervalSeconds: 60,
    numberOfCycles: 7,
    executionSeed: 42,
    metadataJson: null,
    metadataJsonStatus: 'valid',
    orchestratorCorrelationId: 'corr-ui-review',
    runOverrides: null,
  };
  const comparisonRun = {
    ...run,
    id: 'run-ui-review-002',
    scenarioCode: 'scenario_c',
    scenarioName: 'Scenario C - Missing readings',
    executionSeed: 43,
    orchestratorCorrelationId: 'corr-ui-review-comparison',
  };
  const area = {
    id: 'area-001',
    code: 'proenca-a-nova',
    name: 'Proença-a-Nova',
    countryCode: 'PT',
    configurationVersionNumber: 1,
    gridCellCount: 12,
    sensorNodeCount: 2,
    scenarioCount: 2,
  };

  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname.replace('/api', '');
    const json = (body: unknown, status = 200, headers: Record<string, string> = {}) =>
      route.fulfill({ status, contentType: 'application/json', headers, body: JSON.stringify(body) });

    if (pathname === '/users-roles/login') {
      return json({
        token: 'browser-fixture-token',
        userId: 'admin-001',
        username: 'admin',
        fullName: 'UI Review Admin',
        email: 'admin@example.invalid',
        roles,
      });
    }
    if (pathname === '/users-roles/me') {
      return json({
        id: 'admin-001',
        username: 'admin',
        fullName: 'UI Review Admin',
        email: 'admin@example.invalid',
        roles,
      });
    }
    if (pathname === '/users-roles/me/capabilities') {
      return json({
        roles,
        capabilities,
        authority: 'browser-fixture-policy',
        evaluatedAt: '2026-07-16T18:00:00Z',
      });
    }
    if (pathname === '/control/areas') return json([area]);
    if (pathname.includes('/scenarios')) {
      return json([{ id: 'scenario-001', code: 'scenario_b', name: 'Scenario B - High Risk', scenarioKind: 'HighRisk', configurationVersionNumber: 1, description: 'Operational high-risk fixture', baseScenarioCode: null, datasetBindingCount: 2 }]);
    }
    if (pathname === '/control/simulation-runs') return json([run, comparisonRun]);
    const scopedRun = pathname.includes(comparisonRun.id) ? comparisonRun : run;
    if (pathname === `/control/runtime/runs/${scopedRun.id}`) return json(scopedRun);
    if (pathname === `/control/runtime/runs/${scopedRun.id}/operation`) return json({
      operationId: scopedRun === run ? '22222222-2222-2222-2222-222222222222' : '44444444-4444-4444-4444-444444444444', requestId: '33333333-3333-3333-3333-333333333333', correlationId: 'fixture-correlation', simulationRunId: scopedRun.id,
      requestedState: 'Requested', providerState: 'Succeeded', runState: 'Completed', processingState: 'Settled', state: 'SystemCompleted', terminalOutcome: 'Succeeded',
      acceptedAt: scopedRun.startedAt, updatedAt: scopedRun.endedAt, startedAt: scopedRun.startedAt, producerCompletedAt: scopedRun.endedAt, systemCompletedAt: scopedRun.endedAt, finishedAt: scopedRun.endedAt,
      failureCode: null, failureDetail: null, evidenceId: 'fixture-run-evidence', evidenceLocation: 'docs/evidence/fixture',
      accounting: { expectedObservations: 14, acceptedObservations: 14, pendingInbox: 0, processingInbox: 0, retryPendingInbox: 0, processedInbox: 14, quarantinedInbox: 0, settled: true },
    });
    if (pathname.endsWith('/audit')) {
      return json({ run: scopedRun, expectedEvents: 14, acceptedReadings: scopedRun === run ? 14 : 12, missingEvents: scopedRun === run ? 0 : 2, rejected: 0, quarantined: 0, retryAttempts: 0, riskAssessments: scopedRun === run ? 14 : 12, qualityFlagsSummary: [], eligibilitySummary: [], areaSnapshot: null, limitations: [], scoreComponents: null, indexComparison: null });
    }
    if (pathname.endsWith('/timings')) {
      return json({ simulationRunId: scopedRun.id, runDurationMs: 420000, startedAt: scopedRun.startedAt, endedAt: scopedRun.endedAt, firstInboxReceivedAt: scopedRun.startedAt, firstProcessingAttemptStartedAt: scopedRun.startedAt, lastProcessingAttemptFinishedAt: scopedRun.endedAt, firstRiskAssessmentCreatedAt: scopedRun.startedAt, firstAlertTriggeredAt: null, timeToFirstInboxMs: 500, timeToFirstProcessingAttemptMs: 800, timeToFirstRiskAssessmentMs: 1200, timeToFirstAlertMs: null, attempts: { attemptCount: 14, successfulAttempts: 14, failedAttempts: 0, quarantinedAttempts: 0, minDurationMs: 20, avgDurationMs: 30, maxDurationMs: 50, p50DurationMs: 28, p95DurationMs: 47, p99DurationMs: 49 }, stages: [], limitations: [] });
    }
    if (pathname === '/control/runtime/summary') {
      return json({
        generatedAtUtc: '2026-07-16T18:08:00Z', recentWindowMinutes: 30, areaCode: area.code, currentRun: null, latestRun: run,
        pipeline: { inboxTotal: 14, inboxRecent: 14, inboxByStatus: [{ status: 'Processed', count: 14 }], attemptsRecent: 14, attemptsByOutcomeAndError: [], rejectedRecent: 0, rejectedTotal: 0, rejectedByCode: [], quarantinedRecent: 0, quarantinedTotal: 0, quarantinedByCode: [], latestRejected: [], latestQuarantined: [], latestFailedAttempts: [] },
        risk: { recentCount: 14, minScore: 0.42, maxScore: 0.79, latestTimestamp: '2026-07-16T18:08:00Z', recentScores: [] },
        areaOperationalState: { areaCode: area.code, configurationVersionNumber: 1, snapshotTimestamp: '2026-07-16T18:08:00Z', aggregateRiskScore: 0.79, aggregateRiskLevel: 'High', severity: 'High', summary: 'Observed fixture', assessmentCount: 14, updatedAt: '2026-07-16T18:08:00Z', alertState: 'Monitoring', coverageStatus: 'Complete', freshnessStatus: 'Fresh', carryForwardStatus: 'None', lastAssessmentTimestamp: '2026-07-16T18:08:00Z', lastProjectionUpdatedAt: '2026-07-16T18:08:00Z', operationalStatusReason: null },
        cellOperationalStateCount: 12, activeAlerts: [], freshness: { freshCount: 12, staleCount: 0, expiredCount: 0, oldestIncludedAssessment: '2026-07-16T18:01:00Z', latestIncludedAssessment: '2026-07-16T18:08:00Z', freshSeconds: 120, staleSeconds: 300, note: 'Browser fixture' },
        scoreComponents: { npScore: 0.79, baseRisk: 0.74, adjustedScore: 0.79, score100: 79, meteorologyComponent: 0.8, droughtComponent: 0.7, territoryComponent: 0.6, hazardComponent: 0.7, fuelComponent: 0.7, geomorphologyComponent: 0.6, confidenceFactor: 1, integrityFactor: 1, dominantDriver: 'Meteorology', parameterSetVersion: 'Candidate Parameter Set V1.0', calculationStatus: 'Complete', limitations: null, latestAssessmentTimestamp: '2026-07-16T18:08:00Z', npRiskClass: 'High', npRiskClassLabel: 'Elevado' },
        indexComparison: null, limitations: [], warnings: [],
      });
    }
    if (pathname === '/control/runtime/observability/health') {
      return json({ observedAt: '2026-07-16T18:08:00Z', components: [{ component: 'PostgreSQL', status: 'Healthy', observedAt: '2026-07-16T18:08:00Z', source: 'health', reason: 'Reachable', lastSuccessAt: '2026-07-16T18:08:00Z', lastFailureAt: null, ageSeconds: 0, scope: 'runtime', limitation: null }], rabbitMq: { observedAt: '2026-07-16T18:08:00Z', source: 'management', collectionStatus: 'Measured', queues: [], limitations: [] }, limitations: [] });
    }
    if (pathname === '/control/runtime/observability/rabbitmq') {
      return json({ observedAt: '2026-07-16T18:08:00Z', source: 'management', collectionStatus: 'Measured', queues: [{ queueName: 'prevention', queueRole: 'PrimaryWorkQueue', enabled: true, consumerRequired: true, blocksRuntimeHealth: true, messagesReady: 0, messagesUnacknowledged: 0, messagesTotal: 0, consumers: 1, observedAt: '2026-07-16T18:08:00Z', source: 'management', collectionStatus: 'Measured', limitation: null }], limitations: [] });
    }
    if (pathname === '/control/runtime/observability/evidence') {
      return json({
        observedAt: '2026-07-16T18:08:00Z',
        items: [
          {
            evidenceId: 'run-ui-review-001-summary',
            title: 'Runtime run summary',
            type: 'runtime-json',
            generatedAt: '2026-07-16T18:08:00Z',
            environment: 'local',
            scope: run.id,
            version: '4dfdc2f',
            contentAvailable: true,
            downloadAvailable: true,
            size: 1240,
            status: 'RUNTIME_VALIDATED_LOCAL',
            limitation: null,
          },
        ],
        limitations: [],
      });
    }
    if (pathname.startsWith('/control/runtime/observability/evidence/')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'Content-Disposition': 'attachment; filename="runtime-summary.json"' },
        body: JSON.stringify({ simulationRunId: run.id, status: run.status }),
      });
    }
    if (pathname === '/control/runtime/diagnostics' && request.method() === 'GET') {
      return json({
        diagnostics: [
          {
            id: 'latest-run-expected-vs-observed',
            title: 'Eventos esperados vs observados',
            description: 'Run-scoped accounting.',
          },
          { id: 'inbox-by-status', title: 'Inbox por estado', description: 'Persisted inbox states.' },
          {
            id: 'latest-run-quality-by-profile',
            title: 'Qualidade por perfil',
            description: 'Persisted quality.',
          },
          {
            id: 'compare-latest-b-vs-c',
            title: 'Comparar B vs C',
            description: 'Latest persisted runs.',
          },
        ],
      });
    }
    if (pathname.startsWith('/control/runtime/diagnostics/') && request.method() === 'POST') {
      const diagnosticId = pathname.split('/').at(-1);
      if (diagnosticId === 'compare-latest-b-vs-c') {
        return json({
          id: diagnosticId,
          title: 'Comparação B vs C',
          description: 'Persisted comparison fixture',
          columns: ['scenario', 'metric', 'value'],
          rows: [
            { scenario: 'scenario_b', metric: 'expected events', value: '14' },
            { scenario: 'scenario_c', metric: 'expected events', value: '14' },
          ],
          limitations: [],
        });
      }
      return json({
        id: diagnosticId,
        title: 'Eventos esperados vs observados',
        description: 'Persisted accounting fixture',
        columns: ['metric', 'value'],
        rows: [
          { metric: 'expectedEvents', value: '14' },
          { metric: 'acceptedReadings', value: '14' },
          { metric: 'riskAssessments', value: '14' },
        ],
        limitations: [],
      });
    }
    if (pathname === '/control/operations/catalog') {
      return json([
        operationDefinition({
          operationId: 'evidence-static',
          category: 'evidence',
          displayName: 'Campanha de evidência estática',
          description: 'Executa o perfil fechado de evidence estática.',
          requiredCapability: 'evidence.execute.campaign',
          environments: ['ci'],
          authorized: capabilities.includes('evidence.execute.campaign'),
        }),
        operationDefinition({
          operationId: 'staging-plan',
          category: 'deployment',
          displayName: 'Planear staging',
          description: 'Prepara um plano de staging sem executar deployment.',
          requiredCapability: 'deployment.plan',
          environments: ['staging'],
          authorized: capabilities.includes('deployment.plan'),
          requiresConfirmation: true,
          confirmationTemplate: 'PLAN staging',
        }),
        operationDefinition({
          operationId: 'production-plan',
          category: 'deployment',
          displayName: 'Planear produção',
          description: 'Operação presente no catálogo, sem workflow autoritativo.',
          requiredCapability: 'deployment.plan',
          environments: ['production'],
          authorized: false,
          requiresConfirmation: true,
          requiresApproval: true,
          confirmationTemplate: 'PLAN production',
          availability: 'blocked-no-authoritative-workflow',
          evidenceLevel: 'NOT_PROVED',
          limitation: 'Sem workflow autoritativo de plano de produção.',
        }),
      ]);
    }
    if (pathname === '/control/operations') {
      return json([
        operationFixture({
          id: 'operation-evidence-001',
          operationId: 'evidence-static',
          category: 'evidence',
          displayName: 'Campanha de evidência estática',
          status: 'Succeeded',
          environment: 'ci',
          provider: 'GitHub',
          evidenceLevel: 'PROVED_LOCAL',
          artifacts: [
            {
              artifactId: 'artifact-1',
              name: 'summary.json',
              kind: 'json',
              reference: 'artifact://summary',
              sha256: 'abc',
              sizeBytes: 123,
              evidenceLevel: 'PROVED_LOCAL',
            },
          ],
        }),
        operationFixture({
          id: 'operation-deploy-001',
          operationId: 'staging-plan',
          category: 'deployment',
          displayName: 'Planear staging',
          status: 'Queued',
          environment: 'staging',
          limitations: ['Queued não prova execução do provider.'],
        }),
      ]);
    }
    if (pathname.endsWith('/alerts/active')) return json([]);
    if (pathname === '/control/runtime/reset') {
      return json({
        generatedAtUtc: '2026-07-16T18:08:00Z',
        dryRun: true,
        status: 'DryRunReady',
        message: 'Safety checks passed. No data was deleted.',
        before: [{ schema: 'public', table: 'SimulationRuns', count: 1 }],
        after: [{ schema: 'public', table: 'SimulationRuns', count: 1 }],
      });
    }
    if (pathname === '/control/cloud/environments') return json([]);
    return json({ message: `Fixture not implemented for ${pathname}` }, 404);
  });
}

function operationDefinition(
  overrides: Partial<{
    operationId: string;
    category: string;
    displayName: string;
    description: string;
    requiredCapability: string;
    environments: string[];
    authorized: boolean;
    requiresConfirmation: boolean;
    requiresApproval: boolean;
    confirmationTemplate: string;
    availability: string;
    evidenceLevel: string;
    limitation: string | null;
  }>,
) {
  return {
    operationId: 'operation',
    category: 'quality',
    displayName: 'Operation',
    description: 'Closed operation.',
    requiredCapability: 'quality.execute.static',
    riskLevel: 'medium',
    requiresConfirmation: false,
    requiresApproval: false,
    environments: ['ci'],
    inputs: [{ name: 'ref', description: 'Git reference', required: false, defaultValue: 'master' }],
    workflow: '_operation.yml',
    confirmationTemplate: '',
    authorized: true,
    availability: 'implemented',
    evidenceLevel: 'IMPLEMENTED_NOT_PROVED',
    limitation: null,
    ...overrides,
  };
}

function operationFixture(
  overrides: Partial<{
    id: string;
    operationId: string;
    category: string;
    displayName: string;
    status: string;
    environment: string;
    provider: string | null;
    evidenceLevel: string;
    artifacts: unknown[];
    limitations: string[];
  }>,
) {
  return {
    id: 'operation-001',
    operationId: 'operation',
    category: 'quality',
    displayName: 'Operation',
    status: 'Queued',
    environment: 'ci',
    ref: '4dfdc2f',
    requestedBy: 'operator.local',
    requestedByRoles: ['Operations'],
    requestedByCapabilities: [],
    requestedAt: '2026-07-16T17:00:00Z',
    updatedAt: '2026-07-16T17:04:30Z',
    collectEvidence: true,
    riskLevel: 'medium',
    requiresApproval: false,
    provider: null,
    providerReference: null,
    workflow: '_operation.yml',
    planHash: null,
    evidenceLevel: 'IMPLEMENTED_NOT_PROVED',
    inputs: {},
    steps: [],
    artifacts: [],
    approvals: [],
    limitations: [],
    ...overrides,
  };
}
