import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import axe from 'axe-core';
import { useEffect } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TokenProvider, useToken } from '../context/TokenContext';
import { createUiV2RuntimeSummaryFixture } from './fixtures';
import { UiV2App } from './UiV2App';

const areaFixture = {
  id: 'area-001',
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
    id: 'scenario-001',
    code: 'scenario_a',
    name: 'Scenario A - Base',
    scenarioKind: 'Base',
    configurationVersionNumber: 1,
    description: 'Moderate summer day',
    baseScenarioCode: null,
    datasetBindingCount: 1,
  },
  {
    id: 'scenario-002',
    code: 'scenario_b',
    name: 'Scenario B - High Risk',
    scenarioKind: 'HighRisk',
    configurationVersionNumber: 1,
    description: 'Critical fire-weather context',
    baseScenarioCode: null,
    datasetBindingCount: 2,
  },
];

function renderUiV2(isDark = false) {
  return render(
    <TokenProvider>
      <UiV2App isDark={isDark} />
    </TokenProvider>,
  );
}

function renderAuthenticatedUiV2(roles: string[], isDark = false) {
  return render(
    <TokenProvider>
      <AuthenticatedUiV2 roles={roles} isDark={isDark} />
    </TokenProvider>,
  );
}

function AuthenticatedUiV2({ roles, isDark }: { roles: string[]; isDark: boolean }) {
  const { user, login } = useToken();

  useEffect(() => {
    if (!user) {
      void login(`user-${roles.join('-')}`, 'password');
    }
  }, [login, roles, user]);

  return user ? <UiV2App isDark={isDark} /> : null;
}

describe('UiV2App', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    window.history.replaceState(null, '', '/ui-v2');
    vi.stubGlobal('fetch', createFetchMock());
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('keeps the public landing limited to product, data status, help and login', async () => {
    renderUiV2();

    expect(await screen.findByRole('heading', { name: 'NatureProtector UI v2' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'NatureProtector', level: 2 })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /entrar/i })).toHaveAttribute('href', '/login');
    expect(screen.getByText('Data Status')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Pipeline/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Simulacao/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Qualidade/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Administracao/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /P3 experimental/i })).not.toBeInTheDocument();

    await screen.findByRole('option', { name: /Proenca-a-Nova/i });
    fireEvent.change(await screen.findByLabelText(/selecionar area/i), { target: { value: 'proenca-a-nova' } });
    await waitFor(() => expect(screen.getByText(/Area resolvida pelo catalogo disponivel/i)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /abrir vista de leitura/i }));
    expect(await screen.findByRole('heading', { name: /Estado dos dados/i })).toBeInTheDocument();
  });

  it('keeps the skip link hidden until focus and moves focus to main content', async () => {
    renderUiV2();

    const skip = await screen.findByRole('link', { name: 'Saltar para o conteúdo' });
    const main = document.getElementById('ui-v2-main');

    expect(skip).toHaveClass('ui-v2-skip');
    fireEvent.click(skip);

    expect(document.activeElement).toBe(main);
  });

  it('passes a basic axe scan for the public landing', async () => {
    const { container } = renderUiV2();

    await screen.findByRole('heading', { name: 'NatureProtector UI v2' });
    await screen.findByRole('option', { name: /Proenca-a-Nova/i });
    const result = await axe.run(container, {
      rules: {
        'color-contrast': { enabled: false },
      },
    });

    expect(result.violations).toEqual([]);
  });

  it('opens contextual help from the F1 shortcut', async () => {
    renderUiV2();

    await screen.findByRole('heading', { name: 'NatureProtector UI v2' });
    fireEvent.keyDown(window, { key: 'F1' });

    expect(screen.getByRole('dialog', { name: /ajuda contextual/i })).toBeInTheDocument();
    expect(screen.getByText(/nao depende de path local de docs/i)).toBeInTheDocument();
  });

  it('keeps simulation hidden for Pipeline profiles and combines quality with evidence', async () => {
    vi.stubGlobal('fetch', createFetchMock({ roles: ['Pipeline'] }));
    renderAuthenticatedUiV2(['Pipeline']);

    expect(await screen.findByRole('button', { name: /Risco e dados/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /simulacao/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Evidencia$/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /^Pipeline$/i }));
    expect(await screen.findByRole('heading', { name: /Pipeline e observabilidade/i })).toBeInTheDocument();
    expect(screen.getByText('Ingestion ready')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Qualidade e evidencia/i }));
    expect(await screen.findByRole('heading', { name: /Qualidade e evidencia/i })).toBeInTheDocument();
    expect(screen.getByText(/Latest test execution/i)).toBeInTheDocument();
    expect(screen.getByText(/Historical evidence/i)).toBeInTheDocument();
    expect(screen.getByText(/M05 initial workspace snapshot/i)).toBeInTheDocument();
  });

  it('shows executable simulation with controlled degradation for Sim profiles', async () => {
    vi.stubGlobal('fetch', createFetchMock({ roles: ['Sim'] }));
    renderAuthenticatedUiV2(['Sim']);

    fireEvent.change(await screen.findByLabelText(/selecionar area/i), { target: { value: 'proenca-a-nova' } });
    fireEvent.click(await screen.findByRole('button', { name: /simulacao/i }));

    expect(await screen.findByRole('heading', { name: /^Simulacao$/i })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: /^Degradacao$/i })).toHaveDisplayValue('none');
    expect(screen.queryByText(/Sem capability de execucao/i)).not.toBeInTheDocument();
  });

  it('submits a runtime run from the frontend when the Admin capability profile is active', async () => {
    const fetchMock = createFetchMock({ roles: ['Admin'] });
    vi.stubGlobal('fetch', fetchMock);

    renderAuthenticatedUiV2(['Admin']);

    await screen.findByRole('heading', { name: 'NatureProtector UI v2' });
    fireEvent.change(await screen.findByLabelText(/selecionar area/i), { target: { value: 'proenca-a-nova' } });
    fireEvent.click(await screen.findByRole('button', { name: /simulacao/i }));

    const submitButton = await screen.findByRole('button', { name: /Iniciar simulacao/i });
    await waitFor(() => expect(submitButton).not.toBeDisabled());
    fireEvent.click(submitButton);

    expect(await screen.findByRole('heading', { name: /Contexto de run/i })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/control/runtime/runs',
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('shows proportional administration and experimental P3 only for Admin profiles', async () => {
    const fetchMock = createFetchMock({ roles: ['Admin'] });
    vi.stubGlobal('fetch', fetchMock);

    renderAuthenticatedUiV2(['Admin'], true);

    expect(await screen.findByText('Dark')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: /^Administracao$/i }));
    expect(await screen.findByRole('heading', { name: /Administracao proporcional/i })).toBeInTheDocument();
    expect(screen.getByText('Runtime reset')).toBeInTheDocument();
    expect(screen.getAllByText('blocked').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('button', { name: /P3 experimental/i }));
    expect(await screen.findByRole('heading', { name: /P3 experimental/i })).toBeInTheDocument();
    expect(screen.getByText(/Not integrated into scoring/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/dev/controlled-validation/p3', expect.anything());
  });

  it('shows an error state when the runtime summary request fails', async () => {
    vi.stubGlobal('fetch', createFetchMock({ failSummary: true, roles: ['Pipeline'] }));

    renderAuthenticatedUiV2(['Pipeline']);
    fireEvent.change(await screen.findByLabelText(/selecionar area/i), { target: { value: 'proenca-a-nova' } });
    fireEvent.click(await screen.findByRole('button', { name: /Risco e dados/i }));

    await waitFor(() => expect(screen.getByText('summary unavailable')).toBeInTheDocument());
    expect(screen.getByText(/Sem score apresentavel/i)).toBeInTheDocument();
  });
});

function createFetchMock(options: { failSummary?: boolean; roles?: string[] } = {}) {
  const summary = createUiV2RuntimeSummaryFixture();
  const latestRun = summary.latestRun!;
  const runList = [
    {
      id: latestRun.id,
      areaCode: latestRun.areaCode,
      scenarioCode: latestRun.scenarioCode,
      scenarioName: latestRun.scenarioName,
      status: latestRun.status,
      configurationVersionNumber: latestRun.configurationVersionNumber,
      createdAt: latestRun.createdAt,
      startedAt: latestRun.startedAt,
      endedAt: latestRun.endedAt,
      logicalStartTimestamp: latestRun.logicalStartTimestamp,
      intervalSeconds: latestRun.intervalSeconds,
      numberOfCycles: latestRun.numberOfCycles,
      executionSeed: latestRun.executionSeed,
      metadataJson: latestRun.metadataJson,
    },
  ];

  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost');
    const path = url.pathname;

    if (path === '/api/users-roles/login' && init?.method === 'POST') {
      const roles = options.roles ?? [];
      return jsonResponse({
        token: 'test-token',
        userId: 'user-001',
        username: 'test-user',
        fullName: 'Test User',
        email: 'test@example.invalid',
        roles,
      });
    }

    if (path === '/api/control/areas') {
      return jsonResponse([areaFixture]);
    }

    if (path === '/api/control/runtime/summary') {
      if (options.failSummary) {
        throw new Error('summary unavailable');
      }
      return jsonResponse(summary);
    }

    if (path === '/api/control/areas/proenca-a-nova/scenarios') {
      return jsonResponse(scenarioFixtures);
    }

    if (path === '/api/control/simulation-runs') {
      return jsonResponse(runList);
    }

    if (path === `/api/control/runtime/runs/${latestRun.id}`) {
      return jsonResponse(latestRun);
    }

    if (path === `/api/control/runtime/runs/${latestRun.id}/audit`) {
      return jsonResponse({
        run: latestRun,
        expectedEvents: 36,
        acceptedReadings: 36,
        missingEvents: 0,
        rejected: 0,
        quarantined: 0,
        retryAttempts: 0,
        riskAssessments: 36,
        qualityFlagsSummary: [],
        eligibilitySummary: [],
        areaSnapshot: null,
        limitations: [],
        scoreComponents: summary.scoreComponents,
        indexComparison: summary.indexComparison,
      });
    }

    if (path === `/api/control/runtime/runs/${latestRun.id}/timings`) {
      return jsonResponse({
        simulationRunId: latestRun.id,
        runDurationMs: 780000,
        startedAt: latestRun.startedAt,
        endedAt: latestRun.endedAt,
        firstInboxReceivedAt: latestRun.startedAt,
        firstProcessingAttemptStartedAt: latestRun.startedAt,
        lastProcessingAttemptFinishedAt: latestRun.endedAt,
        firstRiskAssessmentCreatedAt: latestRun.startedAt,
        firstAlertTriggeredAt: null,
        timeToFirstInboxMs: 1000,
        timeToFirstProcessingAttemptMs: 1500,
        timeToFirstRiskAssessmentMs: 2000,
        timeToFirstAlertMs: null,
        attempts: {},
        stages: [],
        limitations: [],
      });
    }

    if (path === '/api/control/runtime/runs' && init?.method === 'POST') {
      const requested = JSON.parse(String(init.body));
      return jsonResponse({
        requestId: 'request-001',
        orchestratorCorrelationId: 'corr-001',
        status: 'Validated',
        message: 'Request validated by test fixture.',
        requestedAtUtc: '2026-06-13T22:10:00Z',
        requested,
        run: {
          ...latestRun,
          runOverrides: {
            requested,
            resolved: requested,
            selectedSensorNames: [],
          },
        },
        warnings: [],
        logDirectory: null,
        evidenceDirectory: null,
      });
    }

    if (path === '/api/dev/controlled-validation/p3') {
      return jsonResponse({
        phase: 'P3NegativePipeline',
        environment: 'Development',
        available: true,
        message: 'Controlled validation P3 execution is available in this environment.',
        messageCount: 11,
        executableCases: 10,
        blockedCases: 2,
      });
    }

    return new Response(JSON.stringify({ message: `Unhandled ${path}` }), {
      status: 404,
      headers: { 'Content-Type': 'application/json' },
    });
  });
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}
