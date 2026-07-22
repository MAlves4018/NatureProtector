import { describe, expect, it } from 'vitest';
import { createUiRuntimeSummaryFixture } from './fixtures';
import {
  buildUiAdminActions,
  buildUiEvidenceItems,
  buildUiP3Surface,
  buildUiPipelineSurface,
  buildUiQaSuites,
  buildUiReadinessItems,
} from './technicalSurfaces';

describe('technical surfaces', () => {
  it('marks missing pipeline instrumentation explicitly instead of inferring health', () => {
    const summary = createUiRuntimeSummaryFixture();
    const surface = buildUiPipelineSurface({ summary, run: summary.latestRun, audit: null, timings: null }, 'en');

    expect(surface.state).toBe('partial');
    expect(surface.fields.find((field) => field.label === 'Prevention.Host health')?.state).toBe('unknown');
    expect(surface.fields.find((field) => field.label === 'Queue state')?.state).toBe('unknown');
    expect(surface.limitations).toContain('Pipeline health is not inferred from absence of errors.');
  });

  it('finds queues by role and separates measured primary from disabled auxiliary values', () => {
    const summary = createUiRuntimeSummaryFixture();
    const observedAt = '2026-06-14T10:00:00Z';
    const surface = buildUiPipelineSurface(
      {
        summary,
        run: summary.latestRun,
        audit: null,
        timings: null,
        rabbitMq: {
          observedAt,
          source: 'RabbitMQ Management HTTP API',
          collectionStatus: 'Unavailable',
          limitations: [],
          queues: [
            {
              queueName: 'np.custom.ingestion',
              queueRole: 'PrimaryWorkQueue',
              enabled: true,
              consumerRequired: true,
              blocksRuntimeHealth: true,
              messagesReady: 0,
              messagesUnacknowledged: 0,
              messagesTotal: 0,
              consumers: 1,
              observedAt,
              source: 'RabbitMQ Management HTTP API',
              collectionStatus: 'Measured',
              limitation: null,
            },
            {
              queueName: 'np.custom.raw',
              queueRole: 'AuxiliaryDiagnosticQueue',
              enabled: false,
              consumerRequired: false,
              blocksRuntimeHealth: false,
              messagesReady: null,
              messagesUnacknowledged: null,
              messagesTotal: null,
              consumers: null,
              observedAt,
              source: 'RabbitMQ Management HTTP API',
              collectionStatus: 'NotApplicable',
              limitation: 'Queue is disabled by configuration.',
            },
          ],
        },
      },
      'en',
    );

    expect(surface.fields.find((field) => field.label === 'Ingestion ready')?.value).toBe('0');
    expect(surface.fields.find((field) => field.label === 'Ingestion ready')?.state).toBe('ready');
    expect(surface.fields.find((field) => field.label === 'Observability ready')?.value).toBe('-');
    expect(surface.fields.find((field) => field.label === 'Observability ready')?.state).toBe('partial');
    expect(surface.fields.find((field) => field.label === 'Ingestion ready')?.scope).toBe(
      'np.custom.ingestion messages_ready',
    );
  });

  it('maps runtime health, timing and failure details without promoting partial data to ready', () => {
    const summary = createUiRuntimeSummaryFixture({
      pipeline: {
        ...createUiRuntimeSummaryFixture().pipeline,
        latestFailedAttempts: [
          {
            id: 1,
            eventId: 'event-1',
            simulationRunId: 'run-001',
            outcome: 'Failed',
            errorCode: 'TRANSIENT',
            errorMessage: 'temporary broker error',
            startedAt: '2026-06-13T21:01:02Z',
            finishedAt: '2026-06-13T21:01:03Z',
            durationMs: 1000,
            retryCount: 1,
          },
        ],
        latestRejected: [
          {
            id: 2,
            eventId: 'event-2',
            simulationRunId: 'run-001',
            rejectionCode: 'DUPLICATE',
            rejectionReason: 'Already processed',
            rejectedAt: '2026-06-13T21:01:04Z',
          },
        ],
        latestQuarantined: [
          {
            id: 3,
            eventId: 'event-3',
            simulationRunId: 'run-001',
            quarantineCode: 'SCHEMA',
            quarantineReason: 'Invalid payload',
            quarantinedAt: '2026-06-13T21:01:05Z',
          },
        ],
      },
    });
    const correlatedRun = {
      ...summary.latestRun!,
      orchestratorCorrelationId: 'corr-123',
    };
    const surface = buildUiPipelineSurface(
      {
        summary,
        run: correlatedRun,
        audit: {
          run: correlatedRun,
          expectedEvents: 36,
          acceptedReadings: 34,
          riskAssessments: 34,
          missingEvents: 2,
          rejected: 1,
          retryAttempts: 2,
          quarantined: 1,
          qualityFlagsSummary: [],
          eligibilitySummary: [],
          scoreComponents: null,
          indexComparison: null,
          limitations: [],
        } as any,
        timings: {
          runDurationMs: 1234,
          timeToFirstInboxMs: 23,
          timeToFirstProcessingAttemptMs: 34,
          timeToFirstRiskAssessmentMs: 45,
          firstInboxReceivedAt: '2026-06-13T21:01:02Z',
          lastProcessingAttemptFinishedAt: '2026-06-13T21:01:10Z',
          firstRiskAssessmentCreatedAt: '2026-06-13T21:01:11Z',
          limitations: ['timing limitation'],
        } as any,
        health: {
          generatedAt: '2026-06-13T21:02:00Z',
          components: [
            {
              component: 'Prevention.Host',
              status: 'Healthy',
              reason: 'ready',
              observedAt: '2026-06-13T21:02:00Z',
              source: 'health endpoint',
              scope: 'service',
              limitation: null,
            },
            {
              component: 'PostgreSQL',
              status: 'Degraded',
              reason: 'slow',
              observedAt: '2026-06-13T21:02:00Z',
              source: 'database probe',
              scope: 'database',
              limitation: 'probe only',
            },
            {
              component: 'InfluxDB',
              status: 'Unhealthy',
              reason: 'offline',
              observedAt: '2026-06-13T21:02:00Z',
              source: 'health endpoint',
              scope: 'metrics',
              limitation: null,
            },
          ],
          limitations: [{ code: 'HEALTH_LIMIT', message: 'health limitation' }],
        } as any,
        observabilityError: new Error('observability unavailable'),
      },
      'en',
    );

    expect(surface.fields.find((field) => field.label === 'Correlation ID')?.value).toBe('corr-123');
    expect(surface.fields.find((field) => field.label === 'Prevention.Host health')?.state).toBe('ready');
    expect(surface.fields.find((field) => field.label === 'PostgreSQL health')?.state).toBe('partial');
    expect(surface.fields.find((field) => field.label === 'InfluxDB health')?.state).toBe('blocked');
    expect(surface.fields.find((field) => field.label === 'Retry count')?.value).toBe('2');
    expect(surface.fields.find((field) => field.label === 'Rejection reason')?.value).toBe(
      'DUPLICATE: Already processed',
    );
    expect(surface.fields.find((field) => field.label === 'Quarantine reason')?.value).toBe('SCHEMA: Invalid payload');
    expect(surface.fields.find((field) => field.label === 'Latency')?.value).toContain('run 1234ms');
    expect(surface.limitations).toEqual(
      expect.arrayContaining(['timing limitation', 'health limitation', 'observability unavailable']),
    );
  });

  it('keeps HTTP evidence scoped to catalog availability and selected run identity', () => {
    const summary = createUiRuntimeSummaryFixture();
    const items = buildUiEvidenceItems(
      {
        summary,
        run: summary.latestRun,
        audit: {
          run: summary.latestRun!,
          expectedEvents: 36,
          acceptedReadings: 36,
          riskAssessments: 36,
          missingEvents: 0,
          rejected: 0,
          retryAttempts: 0,
          quarantined: 0,
          qualityFlagsSummary: [],
          eligibilitySummary: [],
          scoreComponents: null,
          indexComparison: null,
          limitations: [],
        } as any,
        timings: { limitations: [] } as any,
        catalog: {
          observedAt: '2026-06-13T21:30:00Z',
          source: 'catalog',
          limitations: [],
          items: [
            {
              evidenceId: 'run-001',
              title: 'Live run packet',
              type: 'runtime',
              status: 'Ready',
              generatedAt: '2026-06-13T21:30:00Z',
              environment: 'local',
              scope: 'SimulationRunId run-001',
              version: '1',
              size: 42,
              contentAvailable: true,
              downloadAvailable: true,
              limitation: null,
            },
            {
              evidenceId: 'run-002',
              title: 'Other packet',
              type: 'runtime',
              status: 'Missing',
              generatedAt: '2026-06-13T21:30:00Z',
              environment: 'local',
              scope: 'other',
              version: '1',
              size: 0,
              contentAvailable: false,
              downloadAvailable: false,
              limitation: 'not generated',
            },
          ],
        } as any,
      },
      'en',
    );

    expect(items[0]).toMatchObject({
      evidenceId: 'run-001',
      availability: 'ready',
      reference: '/api/control/runtime/observability/evidence/run-001',
    });
    expect(items[1]).toMatchObject({ evidenceId: 'run-002', availability: 'not-available' });
    expect(items.find((item) => item.evidenceId === 'selected-run-audit-timings')?.availability).toBe('partial');
  });

  it('distinguishes prior QA evidence from recorded M05 execution', () => {
    const suites = buildUiQaSuites();

    expect(suites.find((suite) => suite.suiteId === 'm04-ui-focused')?.status).toBe('Passed');
    expect(suites.find((suite) => suite.suiteId === 'm05-final-gates')?.testExecution).toBe('Last recorded execution');
    expect(suites.find((suite) => suite.suiteId === 'm05-final-gates')?.status).toBe(
      'Passed with dependency findings recorded',
    );
  });

  it('keeps P3 experimental and not integrated when availability was not queried', () => {
    const p3 = buildUiP3Surface(null, null, 'en');

    expect(p3.status).toContain('Experimental');
    expect(p3.integrationStatus).toContain('Not integrated');
    expect(p3.fields.find((field) => field.label === 'Runtime availability')?.state).toBe('not-confirmed');
  });

  it('reports P3 availability and query errors distinctly', () => {
    const available = buildUiP3Surface(
      {
        phase: 'P3NegativePipeline',
        available: false,
        environment: 'Production',
        message: 'Evidence mode disabled',
        messageCount: 12,
        executableCases: 0,
        blockedCases: 12,
      },
      null,
      'en',
    );
    const errored = buildUiP3Surface(null, new Error('403 Forbidden'), 'en');

    expect(available.readiness).toContain('Not available in Production');
    expect(available.fields.find((field) => field.label === 'Runtime availability')?.state).toBe('blocked');
    expect(errored.readiness).toBe('Not confirmed: 403 Forbidden');
  });

  it('exposes the guarded runtime reset only to an authorized runtime writer', () => {
    const actions = buildUiAdminActions({ roles: ['Admin'] });
    const reset = actions.find((action) => action.action === 'Runtime reset');

    expect(reset?.availability).toBe('partial');
    expect(reset?.confirmationRequired).toMatch(/RESET_RUNTIME_STATE/);
    expect(
      buildUiAdminActions({ roles: ['View'] }).find((action) => action.action === 'Runtime reset')?.availability,
    ).toBe('blocked');
  });

  it('builds readiness items from health, RabbitMQ, roles and evidence catalog state', () => {
    const summary = createUiRuntimeSummaryFixture();
    const readiness = buildUiReadinessItems({
      summary,
      run: summary.latestRun,
      user: { roles: ['Pipeline', 'Sim'] },
      health: {
        generatedAt: '2026-06-13T21:30:00Z',
        components: [
          {
            component: 'RabbitMQ',
            status: 'AuthRequired',
            reason: 'management API requires auth',
            observedAt: '2026-06-13T21:30:00Z',
            source: 'RabbitMQ Management',
            scope: 'broker',
            limitation: 'credentials not configured',
          },
          {
            component: 'Grafana',
            status: 'NotInstrumented',
            reason: 'no probe',
            observedAt: '2026-06-13T21:30:00Z',
            source: 'runtime health',
            scope: 'dashboard',
            limitation: null,
          },
        ],
        limitations: [],
      } as any,
      rabbitMq: {
        observedAt: '2026-06-13T21:31:00Z',
        source: 'RabbitMQ Management',
        collectionStatus: 'Measured',
        limitations: [{ code: 'SAMPLE', message: 'point-in-time sample' }],
        queues: [
          {
            queueName: 'primary',
            queueRole: 'PrimaryWorkQueue',
            enabled: true,
            consumerRequired: true,
            blocksRuntimeHealth: true,
            messagesReady: 2,
            messagesUnacknowledged: 3,
            messagesTotal: 5,
            consumers: 1,
            observedAt: '2026-06-13T21:31:00Z',
            source: 'RabbitMQ Management',
            collectionStatus: 'Measured',
            limitation: null,
          },
        ],
      },
      evidence: {
        observedAt: '2026-06-13T21:32:00Z',
        source: 'catalog',
        limitations: [{ code: 'CATALOG', message: 'catalog limitation' }],
        items: [],
      } as any,
    });

    expect(readiness.find((item) => item.item === 'RabbitMQ')?.status).toBe('partial');
    expect(readiness.find((item) => item.item === 'Grafana')?.status).toBe('not-instrumented');
    expect(readiness.find((item) => item.item === 'RabbitMQ backlog')?.evidence).toContain('5 mensagens');
    expect(readiness.find((item) => item.item === 'Profiles')?.evidence).toContain('Pipeline, Sim');
    expect(readiness.find((item) => item.item === 'Evidence HTTP')?.limitation).toContain('catalog limitation');
    expect(readiness.find((item) => item.item === 'Reset / rebaseline')?.status).toBe('partial');
  });
});
