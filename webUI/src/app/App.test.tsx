import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ChakraProvider, defaultSystem } from '@chakra-ui/react';
import axe from 'axe-core';
import { useEffect } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TokenProvider, useToken } from './context/TokenContext';
import { createUiRuntimeSummaryFixture } from './fixtures';
import { App } from './App';

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

function renderUi(isDark = false) {
  return render(
    <ChakraProvider value={defaultSystem}>
      <TokenProvider>
        <App />
      </TokenProvider>
    </ChakraProvider>,
  );
}

function renderAuthenticatedUi(roles: string[], isDark = false) {
  return render(
    <ChakraProvider value={defaultSystem}>
      <TokenProvider>
        <AuthenticatedUi roles={roles} isDark={isDark} />
      </TokenProvider>
    </ChakraProvider>,
  );
}

function AuthenticatedUi({ roles, isDark }: { roles: string[]; isDark: boolean }) {
  const { user, login } = useToken();

  useEffect(() => {
    if (!user) {
      void login(`user-${roles.join('-')}`, 'password');
    }
  }, [login, roles, user]);

  return user ? <App /> : null;
}

describe('App', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    window.history.replaceState(null, '', '/');
    vi.stubGlobal('fetch', createFetchMock());
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('keeps the public landing limited to product, data status, help and login', async () => {
    renderUi();

    expect((await screen.findAllByRole('heading', { name: 'NatureProtector' })).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /entrar/i })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: /Leitura pública/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Pipeline/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Simulação/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Qualidade/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Administração/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /P3 experimental/i })).not.toBeInTheDocument();

    await screen.findByRole('option', { name: /Proenca-a-Nova/i });
    fireEvent.change(await screen.findByLabelText(/selecionar área/i), { target: { value: 'proenca-a-nova' } });
    await waitFor(() => expect(screen.getByText(/Área resolvida pelo catálogo disponível/i)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /abrir vista de leitura/i }));
    expect(await screen.findByRole('heading', { name: /Estado dos dados/i })).toBeInTheDocument();
  });

  it('keeps the skip link hidden until focus and moves focus to main content', async () => {
    renderUi();

    const skip = await screen.findByRole('link', { name: 'Saltar para o conteúdo' });
    const main = document.getElementById('ui-main');

    expect(skip).toHaveClass('ui-skip');
    fireEvent.click(skip);

    expect(document.activeElement).toBe(main);
  });

  it('passes a basic axe scan for the public landing', async () => {
    const { container } = renderUi();

    await screen.findAllByRole('heading', { name: 'NatureProtector' });
    await screen.findByRole('option', { name: /Proenca-a-Nova/i });
    const result = await axe.run(container, {
      rules: {
        'color-contrast': { enabled: false },
      },
    });

    expect(result.violations).toEqual([]);
  });

  it('opens contextual help from the F1 shortcut', async () => {
    renderUi();

    await screen.findAllByRole('heading', { name: 'NatureProtector' });
    fireEvent.keyDown(window, { key: 'F1' });

    expect(screen.getByRole('dialog', { name: /ajuda contextual/i })).toBeInTheDocument();
    expect(screen.getByText(/não depende de um caminho local para a documentação/i)).toBeInTheDocument();
  });

  it('keeps keyboard focus inside contextual help and restores it on close', async () => {
    renderUi();

    const englishButton = await screen.findByRole('button', { name: 'EN' });
    englishButton.focus();
    fireEvent.keyDown(window, { key: 'F1' });

    const dialog = await screen.findByRole('dialog', { name: /ajuda contextual/i });
    const close = await screen.findByRole('button', { name: /fechar ajuda/i });
    await waitFor(() => expect(close).toHaveFocus());

    fireEvent.keyDown(dialog, { key: 'Tab' });
    expect(close).toHaveFocus();

    fireEvent.keyDown(dialog, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: /ajuda contextual/i })).not.toBeInTheDocument());
    expect(englishButton).toHaveFocus();
  });

  it('keeps simulation hidden and exposes separate mission, quality and evidence pages for Pipeline profiles', async () => {
    vi.stubGlobal('fetch', createFetchMock({ roles: ['Pipeline'] }));
    renderAuthenticatedUi(['Pipeline']);

    expect(await screen.findByRole('button', { name: /Risco e dados/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Simulação$/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Técnico/i }));
    expect(screen.getByRole('button', { name: /Evidence Explorer/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Deployments$/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /^Pipeline$/i }));
    expect(await screen.findByRole('heading', { name: /Pipeline e observabilidade/i })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Qualidade e evidencia/i }));
    expect(await screen.findByRole('heading', { name: /Qualidade e evidência/i })).toBeInTheDocument();
    expect(screen.getByText(/Latest test execution/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Evidence Explorer/i }));
    expect(await screen.findByRole('heading', { name: /Evidence Explorer/i })).toBeInTheDocument();
  });

  it('shows executable simulation with controlled degradation for Sim profiles', async () => {
    vi.stubGlobal('fetch', createFetchMock({ roles: ['Sim'] }));
    renderAuthenticatedUi(['Sim']);

    fireEvent.change(await screen.findByLabelText(/selecionar área/i), { target: { value: 'proenca-a-nova' } });
    fireEvent.click(await screen.findByRole('button', { name: /Simulações/i }));
    fireEvent.click(await screen.findByRole('button', { name: /simulação/i }));

    expect(await screen.findByRole('heading', { name: /^Simulação$/i })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: /^Degradação$/i })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'none' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'missing-readings' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'noise' })).toBeInTheDocument();
    expect(screen.queryByText(/Sem capability de execucao/i)).not.toBeInTheDocument();
  });

  it('submits a runtime run from the frontend when the Admin capability profile is active', async () => {
    const fetchMock = createFetchMock({ roles: ['Admin'] });
    vi.stubGlobal('fetch', fetchMock);

    renderAuthenticatedUi(['Admin']);

    fireEvent.click(await screen.findByRole('button', { name: /Simulações/i }));
    fireEvent.click(await screen.findByRole('button', { name: /simulação/i }));
    fireEvent.change(await screen.findByLabelText(/selecionar área/i), { target: { value: 'proenca-a-nova' } });

    const submitButton = await screen.findByRole('button', { name: /Iniciar simulação/i });
    await waitFor(() => expect(submitButton).not.toBeDisabled());
    fireEvent.click(submitButton);

    expect(await screen.findByRole('heading', { name: /Contexto da execução/i })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/control/runtime/runs', expect.objectContaining({ method: 'POST' }));
  });

  it('shows proportional administration and experimental P3 only for Admin profiles', async () => {
    const fetchMock = createFetchMock({ roles: ['Admin'] });
    vi.stubGlobal('fetch', fetchMock);

    renderAuthenticatedUi(['Admin'], true);

    expect(await screen.findByText('Dark')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: /^Admin$/i }));
    fireEvent.click(await screen.findByRole('button', { name: /^Administração$/i }));
    expect(await screen.findByRole('heading', { name: /Administração proporcional/i })).toBeInTheDocument();
    expect(screen.getByText('Runtime reset')).toBeInTheDocument();
    expect(screen.getAllByText('blocked').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('button', { name: /P3 experimental/i }));
    expect(await screen.findByRole('heading', { name: /P3 experimental/i })).toBeInTheDocument();
    expect(screen.getByText(/Not integrated into scoring/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/dev/controlled-validation/p3', expect.anything());
  });

  it('shows an error state when the runtime summary request fails', async () => {
    vi.stubGlobal('fetch', createFetchMock({ failSummary: true, roles: ['Pipeline'] }));

    renderAuthenticatedUi(['Pipeline']);
    fireEvent.click(await screen.findByRole('button', { name: /Risco e dados/i }));
    fireEvent.change(await screen.findByLabelText(/selecionar área/i), { target: { value: 'proenca-a-nova' } });

    await waitFor(() => expect(screen.getByText('summary unavailable')).toBeInTheDocument());
    expect(screen.getByText(/Sem score apresent/i)).toBeInTheDocument();
  });
});

function createFetchMock(options: { failSummary?: boolean; roles?: string[] } = {}) {
  const summary = createUiRuntimeSummaryFixture();
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

    if (path === '/api/users-roles/me/capabilities') {
      const { getUiCapabilities } = await import('./capabilities');
      return jsonResponse({
        roles: options.roles ?? [],
        capabilities: [...getUiCapabilities({ roles: options.roles ?? [] })],
        authority: 'test-backend-policy',
        evaluatedAt: '2026-06-28T20:00:00Z',
      });
    }

    if (path === '/api/control/operations/catalog' || path === '/api/control/operations') {
      return jsonResponse([]);
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
