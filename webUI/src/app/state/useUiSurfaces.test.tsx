import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAdminActions, useReadinessItems } from './useUiSurfaces';

const mocks = vi.hoisted(() => ({
  token: {
    user: null as null | { roles: string[] },
  },
  risk: {
    summary: null as unknown,
  },
  activity: {
    selectedRun: null as unknown,
    runAudit: null,
    runTimings: null,
  },
  observability: {
    operationalHealth: null as unknown,
    rabbitMqMetrics: null as unknown,
    evidenceCatalog: null as unknown,
    observabilityError: null as Error | null,
  },
  locale: {
    locale: 'pt-PT',
  },
  api: {},
}));

vi.mock('../context/TokenContext', () => ({
  useToken: () => mocks.token,
}));

vi.mock('./RiskContext', () => ({
  useUiRisk: () => mocks.risk,
}));

vi.mock('./ActivityContext', () => ({
  useUiActivity: () => mocks.activity,
}));

vi.mock('./ObservabilityContext', () => ({
  useUiObservability: () => mocks.observability,
}));

vi.mock('./LocaleContext', () => ({
  useUiLocale: () => mocks.locale,
}));

vi.mock('../services/api', () => ({
  api: mocks.api,
}));

describe('useUiSurfaces', () => {
  beforeEach(() => {
    mocks.token.user = null;
    mocks.risk.summary = null;
    mocks.activity.selectedRun = null;
    mocks.activity.runAudit = null;
    mocks.activity.runTimings = null;
    mocks.observability.operationalHealth = null;
    mocks.observability.rabbitMqMetrics = null;
    mocks.observability.evidenceCatalog = null;
    mocks.observability.observabilityError = null;
  });

  it('derives readiness from runtime health, RabbitMQ, evidence and user roles', () => {
    mocks.token.user = { roles: ['Admin', 'Sim'] };
    mocks.risk.summary = {
      generatedAtUtc: '2026-07-21T10:00:00Z',
      areaCode: 'PT-11',
    };
    mocks.activity.selectedRun = { id: 'run-1' };
    mocks.observability.operationalHealth = {
      components: [
        {
          component: 'Prevention.Host',
          status: 'Healthy',
          observedAt: '2026-07-21T10:01:00Z',
          source: 'health endpoint',
          reason: 'ready',
          scope: 'readiness',
          limitation: null,
        },
        {
          component: 'InfluxDB',
          status: 'Degraded',
          observedAt: '2026-07-21T10:01:00Z',
          source: 'health endpoint',
          reason: 'write lag',
          scope: 'readiness',
          limitation: 'lag observed',
        },
      ],
    };
    mocks.observability.rabbitMqMetrics = {
      collectionStatus: 'Measured',
      observedAt: '2026-07-21T10:01:00Z',
      queues: [{ messagesTotal: 3 }, { messagesTotal: 4 }],
      limitations: [],
    };
    mocks.observability.evidenceCatalog = {
      observedAt: '2026-07-21T10:02:00Z',
      items: [{ evidenceId: 'evidence-1' }, { evidenceId: 'evidence-2' }],
      limitations: [{ code: 'limited', message: 'allowlist only' }],
    };

    const { result } = renderHook(() => useReadinessItems());

    expect(result.current).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          item: 'Prevention.Host',
          status: 'ready',
          evidence: expect.stringContaining('ready'),
        }),
        expect.objectContaining({ item: 'InfluxDB', status: 'partial', limitation: 'lag observed' }),
        expect.objectContaining({
          item: 'RabbitMQ backlog',
          status: 'ready',
          evidence: expect.stringContaining('7 mensagens'),
        }),
        expect.objectContaining({ item: 'Runtime API e projections', status: 'ready' }),
        expect.objectContaining({
          item: 'Execução selecionada',
          status: 'ready',
          evidence: expect.stringContaining('run-1'),
        }),
        expect.objectContaining({ item: 'Profiles', status: 'ready', evidence: expect.stringContaining('Admin, Sim') }),
        expect.objectContaining({ item: 'Evidence HTTP', status: 'ready', limitation: 'allowlist only' }),
        expect.objectContaining({ item: 'Reset / rebaseline', status: 'partial' }),
      ]),
    );
  });

  it('blocks admin reset actions for anonymous users and allows them for simulation profiles', () => {
    const anonymous = renderHook(() => useAdminActions());
    expect(anonymous.result.current.find((item) => item.action === 'Runtime reset')).toEqual(
      expect.objectContaining({ availability: 'blocked', authorizationState: 'Backend denies this profile' }),
    );

    mocks.token.user = { roles: ['Sim'] };
    const sim = renderHook(() => useAdminActions());
    expect(sim.result.current.find((item) => item.action === 'Runtime reset')).toEqual(
      expect.objectContaining({
        availability: 'partial',
        authorizationState: 'Backend allows Sim/Admin in Development',
      }),
    );
  });
});
