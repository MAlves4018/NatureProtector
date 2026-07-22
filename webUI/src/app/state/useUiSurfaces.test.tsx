import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAdminActions, useP3Data, useP3Surface, useReadinessItems } from './useUiSurfaces';

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
  capabilities: {
    canReadProtectedP3: false,
  },
  api: {
    getControlledValidationP3Availability: vi.fn(),
  },
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

vi.mock('./CapabilityContext', () => ({
  useUiCapabilities: () => mocks.capabilities,
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
    mocks.capabilities.canReadProtectedP3 = false;
    mocks.api.getControlledValidationP3Availability.mockReset();
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

  it('does not query P3 availability without the protected capability', () => {
    const { result } = renderHook(() => useP3Data());

    expect(result.current.p3Availability).toBeNull();
    expect(result.current.p3Error).toBeNull();
    expect(result.current.p3Loading).toBe(false);
    expect(mocks.api.getControlledValidationP3Availability).not.toHaveBeenCalled();
  });

  it('loads P3 availability for protected profiles and exposes it through the P3 surface', async () => {
    mocks.capabilities.canReadProtectedP3 = true;
    mocks.api.getControlledValidationP3Availability.mockResolvedValue({
      phase: 'P3NegativePipeline',
      environment: 'Development',
      available: true,
      message: 'ready for controlled validation',
      messageCount: 6,
      executableCases: 5,
      blockedCases: 1,
    });

    const data = renderHook(() => useP3Data());

    await waitFor(() => expect(data.result.current.p3Availability).not.toBeNull());
    expect(data.result.current.p3Loading).toBe(false);
    expect(mocks.api.getControlledValidationP3Availability).toHaveBeenCalledTimes(1);

    const surface = renderHook(() => useP3Surface());
    await waitFor(() =>
      expect(surface.result.current.readiness).toContain('Available in Development: ready for controlled validation'),
    );
    expect(surface.result.current.expectedInputs).toBe('6 messages, 5 executable cases, 1 blocked cases');
  });

  it('records P3 availability failures without retry loops', async () => {
    mocks.capabilities.canReadProtectedP3 = true;
    mocks.api.getControlledValidationP3Availability.mockRejectedValue(new Error('p3 unavailable'));

    const { result } = renderHook(() => useP3Data());

    await waitFor(() => expect(result.current.p3Error?.message).toBe('p3 unavailable'));
    expect(result.current.p3Availability).toBeNull();
    expect(result.current.p3Loading).toBe(false);
    expect(mocks.api.getControlledValidationP3Availability).toHaveBeenCalledTimes(1);
  });
});
