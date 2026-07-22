import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { HttpError } from '../services/httpError';
import { SimulationPage } from './SimulationPage';

const navigateMock = vi.fn();
const setSelectedScenarioCodeMock = vi.fn();
const setSimulationFormMock = vi.fn();
const submitSimulationMock = vi.fn();
const refreshSelectedRunMock = vi.fn();

let activityState: any;
let simulationState: any;

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
}));

vi.mock('../components/AreaSelector', () => ({
  AreaSelector: ({ compact }: { compact?: boolean }) => (
    <div data-testid="area-selector">{compact ? 'compact' : 'full'}</div>
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
        'simulation.title': 'Executar simulação',
        'simulation.subtitle': 'Runtime local governado',
        'scenario.selectLabel': 'Selecionar cenário',
        'scenario.none': 'Sem cenário',
        'simulation.sensorCount': 'Sensores',
        'simulation.cycles': 'Ciclos',
        'simulation.interval': 'Intervalo',
        'simulation.seed': 'Seed',
        'simulation.degradation': 'Degradação',
        'simulation.runLabel': 'Run label',
        'simulation.wait': 'Esperar conclusão',
        'simulation.waitTimeout': 'Timeout',
        'simulation.waitTimeoutHelp': 'Timeout deve cobrir ciclos e margem.',
        'simulation.asyncTimeoutHelp': 'Execução assíncrona.',
        'simulation.evidence': 'Recolher evidence',
        'simulation.parallel': 'Permitir paralelo',
        'simulation.executing': 'A executar',
        'simulation.execute': 'Executar',
        'simulation.readOnly': 'Sem permissão de execução.',
        'simulation.review': 'Revisão',
        'simulation.idle': 'idle',
        'config.requested': 'Pedido',
        'config.resolved': 'Resolvido',
        'config.state': 'Estado',
      })[key] ?? key,
  }),
}));

vi.mock('../state/ActivityContext', () => ({
  useUiActivity: () => activityState,
}));

vi.mock('../state/SimulationContext', () => ({
  minimumSynchronousWaitSeconds: (form: any) => form.numberOfCycles * form.intervalSeconds + 30,
  toggleDegradationProfile: (current: string[], profile: string, checked: boolean) =>
    profile === 'none' ? [] : checked ? [...current, profile] : current.filter((item) => item !== profile),
  useUiSimulation: () => simulationState,
}));

describe('SimulationPage', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    setSelectedScenarioCodeMock.mockClear();
    setSimulationFormMock.mockClear();
    submitSimulationMock.mockClear();
    refreshSelectedRunMock.mockClear();
    activityState = buildActivityState();
    simulationState = buildSimulationState();
  });

  it('renders presets and updates scenario, numeric fields and degradation profiles', () => {
    render(<SimulationPage />);

    expect(screen.getByRole('heading', { name: 'Executar simulação' })).toBeInTheDocument();
    expect(screen.getByTestId('area-selector')).toHaveTextContent('compact');
    fireEvent.change(screen.getByLabelText('Selecionar cenário'), { target: { value: 'scenario_c' } });
    expect(setSelectedScenarioCodeMock).toHaveBeenCalledWith('scenario_c');

    fireEvent.click(screen.getByRole('button', { name: 'Nominal rápido' }));
    let updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm)).toMatchObject({
      sensorCount: 2,
      numberOfCycles: 3,
      intervalSeconds: 5,
      degradationProfiles: [],
      runLabel: 'ui-nominal-quick',
    });

    fireEvent.click(screen.getByRole('button', { name: 'Degradado com evidence' }));
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm)).toMatchObject({
      sensorCount: 6,
      numberOfCycles: 5,
      intervalSeconds: 5,
      degradationProfiles: ['missing-readings'],
      collectEvidence: true,
      runLabel: 'ui-degraded-evidence',
    });

    fireEvent.click(screen.getByRole('button', { name: /Sensores/i }));
    fireEvent.change(screen.getByLabelText('Sensores'), { target: { value: '8' } });
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).sensorCount).toBe(8);

    fireEvent.click(screen.getByRole('button', { name: /Duração/i }));
    fireEvent.change(screen.getByLabelText('Ciclos'), { target: { value: '9' } });
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).numberOfCycles).toBe(9);
    fireEvent.change(screen.getByLabelText('Intervalo'), { target: { value: '11' } });
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).intervalSeconds).toBe(11);
    fireEvent.click(screen.getByRole('button', { name: /Degradações/i }));
    expect(screen.getByLabelText('duplicate')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Execução/i }));
    fireEvent.click(screen.getByLabelText('Esperar conclusão'));
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).waitForCompletion).toBe(false);
    fireEvent.change(screen.getByLabelText('Timeout'), { target: { value: '120' } });
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).waitTimeoutSeconds).toBe(120);
    fireEvent.click(screen.getByLabelText('Recolher evidence'));
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).collectEvidence).toBe(true);
    fireEvent.click(screen.getByLabelText('Permitir paralelo'));
    updater = setSimulationFormMock.mock.calls.at(-1)?.[0] as (form: any) => any;
    expect(updater(simulationState.simulationForm).allowParallelRun).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: /Anterior/i }));
    expect(screen.getByRole('button', { name: '4 Degradações' })).toHaveAttribute('aria-current', 'step');
    fireEvent.click(screen.getByRole('button', { name: /Seguinte/i }));
    expect(screen.getByRole('button', { name: '5 Execução' })).toHaveAttribute('aria-current', 'step');
  });

  it('submits on review and opens persisted run surfaces', () => {
    render(<SimulationPage />);

    fireEvent.click(screen.getByRole('button', { name: '6 Revisão' }));
    fireEvent.click(screen.getByRole('button', { name: 'Executar' }));

    expect(submitSimulationMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId('status-badge')).toHaveTextContent('Completed');
    expect(screen.getByText('op-live-1')).toBeInTheDocument();
    expect(screen.getByText('run-live-1')).toBeInTheDocument();
    expect(screen.getByText('30/30')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'progress:run-live-1' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Abrir resultados' }));
    fireEvent.click(screen.getByRole('button', { name: 'Comparar cenários' }));
    fireEvent.click(screen.getByRole('button', { name: 'Abrir evidence' }));
    fireEvent.click(screen.getByRole('button', { name: 'progress:run-live-1' }));

    expect(navigateMock).toHaveBeenNthCalledWith(1, '/runs?runId=run-live-1');
    expect(navigateMock).toHaveBeenNthCalledWith(2, '/scenario-compare?runId=run-live-1');
    expect(navigateMock).toHaveBeenNthCalledWith(3, '/evidence?runId=run-live-1');
    expect(refreshSelectedRunMock).toHaveBeenCalledTimes(1);
  });

  it('renders runtime unavailable, readonly, async and rate-limit states', () => {
    simulationState = buildSimulationState({
      canExecuteSimulation: false,
      runtimeLaunchAvailable: false,
      simulationError: new HttpError(429, 'Too Many Requests', 'slow down', 12),
      simulationForm: {
        ...buildSimulationState().simulationForm,
        waitForCompletion: false,
      },
    });

    render(<SimulationPage />);

    expect(screen.getByText(/não está disponível neste build/i)).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Tente novamente em 12s');
    fireEvent.click(screen.getByRole('button', { name: /Execução/i }));
    expect(screen.getByText('Execução assíncrona.')).toBeInTheDocument();
  });
});

function buildActivityState() {
  return {
    scenarios: [
      { code: 'scenario_b', name: 'Scenario B' },
      { code: 'scenario_c', name: 'Scenario C' },
    ],
    selectedScenarioCode: 'scenario_b',
    setSelectedScenarioCode: setSelectedScenarioCodeMock,
    selectedRunId: 'run-live-1',
    runAudit: { expectedEvents: 30, acceptedReadings: 30, riskAssessments: 30 },
    runTimings: { simulationRunId: 'run-live-1' },
    runOperation: null,
    refreshSelectedRun: refreshSelectedRunMock,
  };
}

function buildSimulationState(overrides: Record<string, unknown> = {}) {
  return {
    simulationForm: {
      sensorCount: 4,
      numberOfCycles: 3,
      intervalSeconds: 5,
      seed: '42',
      degradationProfiles: [],
      runLabel: 'ui-live',
      waitForCompletion: true,
      collectEvidence: false,
      allowParallelRun: false,
      waitTimeoutSeconds: 60,
    },
    setSimulationForm: setSimulationFormMock,
    simulationReview: {
      requested: {
        areaCode: 'PT-11',
        scenarioCode: 'scenario_b',
        sensorCount: 4,
        numberOfCycles: 3,
      },
      resultStatus: 'Ready',
      resultMessage: 'Ready to execute',
      fields: [
        { label: 'Área', requested: 'PT-11', resolved: 'PT-11', state: 'ok' },
        { label: 'Cenário', requested: 'scenario_b', resolved: 'scenario_b', state: 'ok' },
      ],
    },
    simulationSubmitting: false,
    simulationError: null,
    runtimeOperation: {
      operationId: 'op-live-1',
      simulationRunId: 'run-live-1',
      state: 'Completed',
      processingState: 'Settled',
      failureDetail: null,
      evidenceId: 'evidence-live-1',
      evidenceLocation: null,
      accounting: {
        expectedObservations: 30,
        processedInbox: 30,
        quarantinedInbox: 0,
      },
    },
    canExecuteSimulation: true,
    runtimeLaunchAvailable: true,
    submitSimulation: submitSimulationMock,
    degradationProfiles: ['none', 'missing-readings', 'duplicate'],
    ...overrides,
  };
}
