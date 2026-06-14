import { describe, expect, it } from 'vitest';
import { createUiV2RuntimeSummaryFixture } from './fixtures';
import {
  buildUiV2AdminActions,
  buildUiV2P3Surface,
  buildUiV2PipelineSurface,
  buildUiV2QaSuites,
} from './technicalSurfaces';

describe('UI v2 technical surfaces', () => {
  it('marks missing pipeline instrumentation explicitly instead of inferring health', () => {
    const summary = createUiV2RuntimeSummaryFixture();
    const surface = buildUiV2PipelineSurface({ summary, run: summary.latestRun, audit: null, timings: null }, 'en');

    expect(surface.state).toBe('partial');
    expect(surface.fields.find(field => field.label === 'Prevention.Host health')?.state).toBe('unknown');
    expect(surface.fields.find(field => field.label === 'Queue state')?.state).toBe('unknown');
    expect(surface.limitations).toContain('Pipeline health is not inferred from absence of errors.');
  });

  it('shows measured RabbitMQ zero separately from unavailable queue values', () => {
    const summary = createUiV2RuntimeSummaryFixture();
    const observedAt = '2026-06-14T10:00:00Z';
    const surface = buildUiV2PipelineSurface({
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
            queueName: 'np.ingestion.readings',
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
            queueName: 'np.observability.raw',
            messagesReady: null,
            messagesUnacknowledged: null,
            messagesTotal: null,
            consumers: null,
            observedAt,
            source: 'RabbitMQ Management HTTP API',
            collectionStatus: 'Unavailable',
            limitation: 'Queue unavailable.',
          },
        ],
      },
    }, 'en');

    expect(surface.fields.find(field => field.label === 'Ingestion ready')?.value).toBe('0');
    expect(surface.fields.find(field => field.label === 'Ingestion ready')?.state).toBe('ready');
    expect(surface.fields.find(field => field.label === 'Observability ready')?.value).toBe('-');
    expect(surface.fields.find(field => field.label === 'Observability ready')?.state).toBe('not-available');
  });

  it('distinguishes prior QA evidence from recorded M05 execution', () => {
    const suites = buildUiV2QaSuites();

    expect(suites.find(suite => suite.suiteId === 'm04-ui-v2-focused')?.status).toBe('Passed');
    expect(suites.find(suite => suite.suiteId === 'm05-final-gates')?.testExecution).toBe('Last recorded execution');
    expect(suites.find(suite => suite.suiteId === 'm05-final-gates')?.status).toBe('Passed with dependency findings recorded');
  });

  it('keeps P3 experimental and not integrated when availability was not queried', () => {
    const p3 = buildUiV2P3Surface(null, null, 'en');

    expect(p3.status).toContain('Experimental');
    expect(p3.integrationStatus).toContain('Not integrated');
    expect(p3.fields.find(field => field.label === 'Runtime availability')?.state).toBe('not-confirmed');
  });

  it('does not expose destructive reset as available administration', () => {
    const actions = buildUiV2AdminActions({ roles: ['Admin'] });
    const reset = actions.find(action => action.action === 'Runtime reset');

    expect(reset?.availability).toBe('blocked');
    expect(reset?.limitations.join(' ')).toMatch(/not exposed/i);
  });
});
