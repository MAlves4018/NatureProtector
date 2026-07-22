import { fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RunsPage } from './RunsPage';

const navigateMock = vi.fn();
const setSelectedRunIdMock = vi.fn();
const refreshSelectedRunMock = vi.fn();

let activityState: any;

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
}));

vi.mock('../components/ExportActions', () => ({
  ExportActions: ({ filename, content }: { filename: string; content: string }) => (
    <button type="button" data-testid={`export-${filename}`}>
      export:{filename}:{content.length}
    </button>
  ),
}));

vi.mock('../components/PageHeader', () => ({
  PageHeader: ({ title, subtitle }: { title: string; subtitle: string }) => (
    <header>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </header>
  ),
}));

vi.mock('../components/RunProgressCockpit', () => ({
  RunProgressCockpit: ({ selectedRunId, onRefresh }: { selectedRunId: string; onRefresh: () => void }) => (
    <button type="button" onClick={onRefresh}>
      progress:{selectedRunId}
    </button>
  ),
}));

vi.mock('../components/RunScientificMetrics', () => ({
  RunScientificMetrics: ({ audit }: { audit: any }) => (
    <div data-testid="scientific-metrics">scientific:{audit?.score100 ?? 'none'}</div>
  ),
}));

vi.mock('../components/StatusBadge', () => ({
  StatusBadge: ({ label, state }: { label: string; state: string }) => (
    <span data-testid="status-badge">
      {label}:{state}
    </span>
  ),
}));

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({
    copy: (key: string) =>
      ({
        'run.selectLabel': 'Selecionar run',
        'state.loading': 'A carregar',
        'run.none': 'Nenhuma run',
        'value.noEvidence': 'Sem evidência',
      })[key] ?? key,
  }),
}));

vi.mock('../state/ActivityContext', () => ({
  useUiActivity: () => activityState,
}));

describe('RunsPage', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    setSelectedRunIdMock.mockClear();
    refreshSelectedRunMock.mockClear();
    activityState = buildActivityState();
  });

  it('renders selected run identity, history accounting and navigation actions', () => {
    render(<RunsPage />);

    expect(screen.getByRole('heading', { name: 'Espaço da execução' })).toBeInTheDocument();
    expect(screen.getByText('2 de 2')).toBeInTheDocument();
    expect(screen.getAllByText('run-b').length).toBeGreaterThan(0);
    expect(screen.getAllByText('esperados / aceites / processados').length).toBeGreaterThan(0);
    expect(screen.getByText('30 / 30 / 30')).toBeInTheDocument();
    expect(screen.getAllByText('SimulationRunId').length).toBeGreaterThan(0);
    expect(screen.getAllByText('scenario_b live').length).toBeGreaterThan(0);
    expect(screen.getByText('progress:run-b')).toBeInTheDocument();
    expect(screen.getByTestId('scientific-metrics')).toHaveTextContent('scientific:82');

    fireEvent.click(screen.getByRole('button', { name: 'progress:run-b' }));
    expect(refreshSelectedRunMock).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: /Comparar cenários/i }));
    fireEvent.click(screen.getByRole('button', { name: /Consultas preparadas/i }));
    fireEvent.click(screen.getByRole('button', { name: /Abrir evidência/i }));

    expect(navigateMock).toHaveBeenNthCalledWith(1, '/scenario-compare?runId=run-b');
    expect(navigateMock).toHaveBeenNthCalledWith(2, '/queries?runId=run-b');
    expect(navigateMock).toHaveBeenNthCalledWith(3, '/evidence?runId=run-b');
  });

  it('filters run history by search scenario profile and status, then changes selection', () => {
    render(<RunsPage />);

    fireEvent.change(screen.getByLabelText('Pesquisar ID ou cenário'), { target: { value: 'scenario_a' } });
    expect(screen.getByText('1 de 2')).toBeInTheDocument();
    expect(screen.getByText('run-a')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Cenário'), { target: { value: 'scenario_a' } });
    fireEvent.change(screen.getByLabelText('Perfil'), { target: { value: 'none' } });
    fireEvent.change(screen.getByLabelText('Estado'), { target: { value: 'Completed' } });
    fireEvent.click(screen.getByRole('button', { name: 'Abrir' }));

    expect(setSelectedRunIdMock).toHaveBeenCalledWith('run-a');
  });

  it('renders lifecycle accounting quality and evidence tabs with defensive values', () => {
    render(<RunsPage />);

    fireEvent.click(screen.getByRole('button', { name: /Ciclo de vida/i }));
    expect(screen.getByText('Ciclo de vida observado')).toBeInTheDocument();
    expect(screen.getByText('Pedido aceite')).toBeInTheDocument();
    expect(screen.getByText('Settled')).toBeInTheDocument();
    expect(screen.getByText('Durações defensáveis')).toBeInTheDocument();
    expect(screen.getByText('pipeline')).toBeInTheDocument();
    expect(screen.getAllByText('950 ms').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('button', { name: /Contabilidade/i }));
    expect(screen.getByTestId('run-accounting-panel')).toBeInTheDocument();
    expect(within(screen.getByTestId('run-accounting-panel')).getByText('Esperados')).toBeInTheDocument();
    expect(screen.getByText('Fecho reconciliado')).toBeInTheDocument();
    expect(screen.getByText('Sim')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Qualidade/i }));
    expect(screen.getByTestId('run-quality-panel')).toBeInTheDocument();
    expect(screen.getByText('Retries')).toBeInTheDocument();
    expect(screen.getByText('Avaliações de risco')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Evidência' }));
    expect(screen.getByTestId('run-evidence-panel')).toBeInTheDocument();
    expect(screen.getByText('Pacote exportável da run')).toBeInTheDocument();
    expect(screen.getByText('Recolhida para esta run')).toBeInTheDocument();
    expect(screen.getByText('DataScope')).toBeInTheDocument();
    expect(screen.getAllByText('run-b').length).toBeGreaterThan(0);
  });

  it('renders empty state when no run is selected', () => {
    activityState = buildActivityState({
      selectedRunId: '',
      selectedRun: null,
      runContext: {
        resolvedRunId: null,
        state: 'idle',
        run: null,
        fields: [],
      },
    });

    render(<RunsPage />);

    expect(screen.getByText('Selecione uma run')).toBeInTheDocument();
    expect(screen.getByText('O workspace mantém todos os indicadores associados à mesma SimulationRunId.')).toBeInTheDocument();
  });
});

function buildActivityState(overrides: Record<string, unknown> = {}) {
  const runA = {
    id: 'run-a',
    scenarioCode: 'scenario_a',
    scenarioName: 'scenario_a nominal',
    status: 'Completed',
    executionSeed: 101,
    numberOfCycles: 2,
    intervalSeconds: 1,
    createdAt: '2026-07-21T11:00:00Z',
    startedAt: '2026-07-21T11:00:01Z',
    endedAt: '2026-07-21T11:00:10Z',
    metadataJson: '{"run_overrides":{"resolved":{"degradation_profiles":["none"],"sensor_count":15}}}',
  };
  const runB = {
    id: 'run-b',
    scenarioCode: 'scenario_b',
    scenarioName: 'scenario_b live',
    status: 'Completed',
    executionSeed: 202,
    numberOfCycles: 3,
    intervalSeconds: 1,
    createdAt: '2026-07-21T12:00:00Z',
    startedAt: '2026-07-21T12:00:01Z',
    endedAt: '2026-07-21T12:00:15Z',
    metadataJson: '{"run_overrides":{"resolved":{"degradation_profiles":["duplicate"],"sensor_count":10}}}',
  };

  return {
    runs: [runA, runB],
    runsLoading: false,
    selectedRun: runB,
    selectedRunId: 'run-b',
    setSelectedRunId: setSelectedRunIdMock,
    refreshSelectedRun: refreshSelectedRunMock,
    runContext: {
      resolvedRunId: 'run-b',
      state: 'Completed',
      run: runB,
      fields: [
        { label: 'Estado', value: 'Completed' },
        { label: 'Cenário', value: 'scenario_b' },
      ],
    },
    runAudit: {
      run: runB,
      expectedEvents: 30,
      acceptedReadings: 30,
      missingEvents: 0,
      rejected: 1,
      quarantined: 2,
      retryAttempts: 3,
      riskAssessments: 30,
      score100: 82,
      dataScope: { dataRunId: 'run-b' },
    },
    runTimings: {
      simulationRunId: 'run-b',
      startedAt: '2026-07-21T12:00:01Z',
      firstInboxReceivedAt: '2026-07-21T12:00:02Z',
      firstProcessingAttemptStartedAt: '2026-07-21T12:00:03Z',
      firstRiskAssessmentCreatedAt: '2026-07-21T12:00:04Z',
      lastProcessingAttemptFinishedAt: '2026-07-21T12:00:10Z',
      endedAt: '2026-07-21T12:00:15Z',
      runDurationMs: 14000,
      timeToFirstInboxMs: 1000,
      timeToFirstProcessingAttemptMs: 2000,
      timeToFirstRiskAssessmentMs: 3000,
      timeToFirstAlertMs: null,
      attempts: {
        attemptCount: 30,
        avgDurationMs: 400,
        p50DurationMs: 350,
        p95DurationMs: 900,
        p99DurationMs: 950,
        maxDurationMs: 950,
      },
      stages: [
        {
          stage: 'pipeline',
          outcome: 'Succeeded',
          errorCode: null,
          count: 30,
          avgDurationMs: 300,
          maxDurationMs: 950,
        },
      ],
      dataScope: { dataRunId: 'run-b' },
    },
    runOperation: {
      simulationRunId: 'run-b',
      acceptedAt: '2026-07-21T12:00:00Z',
      startedAt: '2026-07-21T12:00:01Z',
      systemCompletedAt: '2026-07-21T12:00:16Z',
      finishedAt: '2026-07-21T12:00:18Z',
      accounting: {
        expectedObservations: 30,
        acceptedObservations: 30,
        processedInbox: 30,
        pendingInbox: 0,
        processingInbox: 0,
        retryPendingInbox: 0,
        quarantinedInbox: 2,
        settled: true,
      },
    },
    ...overrides,
  };
}
