import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DatabaseQueriesPage } from './DatabaseQueriesPage';
import { ScenarioComparisonPage } from './ScenarioComparisonPage';
import { api } from '../services/api';

const setSearchParamsMock = vi.fn();

let searchParams = new URLSearchParams();

const runs = [
  {
    id: 'run-b',
    scenarioCode: 'scenario_b',
    areaCode: 'PT-11',
    status: 'Completed',
    configurationVersionNumber: 7,
    executionSeed: 123,
    numberOfCycles: 3,
    intervalSeconds: 1,
    createdAt: '2026-07-21T12:00:00Z',
    startedAt: '2026-07-21T12:00:01Z',
    endedAt: '2026-07-21T12:00:04Z',
    runOverrides: {
      requested: { degradationProfiles: ['none'] },
      resolved: { sensorCount: 10, degradationProfiles: ['none'] },
    },
  },
  {
    id: 'run-c',
    scenarioCode: 'scenario_c',
    areaCode: 'PT-11',
    status: 'Completed',
    configurationVersionNumber: 8,
    executionSeed: 456,
    numberOfCycles: 3,
    intervalSeconds: 2,
    createdAt: '2026-07-21T12:10:00Z',
    startedAt: '2026-07-21T12:10:01Z',
    endedAt: '2026-07-21T12:10:07Z',
    runOverrides: {
      requested: { degradationProfiles: ['missing'] },
      resolved: { sensorCount: 9, degradationProfiles: ['missing'] },
    },
  },
];

const auditByRun = {
  'run-b': {
    expectedEvents: 30,
    acceptedReadings: 30,
    riskAssessments: 30,
    missingEvents: 0,
    rejected: 0,
    retryAttempts: 1,
    quarantined: 0,
    qualityFlagsSummary: [{ status: 'Clean', count: 30 }],
    eligibilitySummary: [{ status: 'Eligible', count: 30 }],
    scoreComponents: {
      npScore: 84.5,
      baseRisk: 70,
      adjustedScore: 75,
      score100: 84,
      confidenceFactor: 0.98,
      integrityFactor: 1,
      npRiskClassLabel: 'Elevado',
    },
    indexComparison: {
      fireWeatherIndex: 31.2,
      normalizedFireWeatherIndex: 0.77,
      fireWeatherCalculationStatus: 'Calculated',
      fireWeatherIndexValueSource: 'live',
      fireWeatherIpmaClassLabel: 'Muito elevado',
      logicalDate: '2026-07-21',
      keetchByramDroughtIndex: 420,
      normalizedKeetchByramDroughtIndex: 0.62,
      kbdiCalculationStatus: 'Calculated',
      kbdiValueSource: 'live',
      kbdiDrynessClassLabel: 'Dry',
      kbdiAntecedentDays: 30,
      portugueseContextRiskProxyClass: 'High',
      portugueseContextRiskProxyLabel: 'Proxy alto',
      territorialHazardProxyClass: 'High',
      provenance: 'pipeline',
    },
  },
  'run-c': {
    expectedEvents: 30,
    acceptedReadings: 24,
    riskAssessments: 24,
    missingEvents: 6,
    rejected: 2,
    retryAttempts: 3,
    quarantined: 1,
    qualityFlagsSummary: [{ status: 'Missing', count: 6 }],
    eligibilitySummary: [
      { status: 'Eligible', count: 24 },
      { status: 'Blocked', count: 6 },
    ],
    scoreComponents: {
      npScore: 63.1,
      baseRisk: 69,
      adjustedScore: 55,
      score100: 63,
      confidenceFactor: 0.78,
      integrityFactor: 0.8,
      npRiskClassLabel: 'Aviso',
    },
    indexComparison: {
      fireWeatherIndex: 29.5,
      keetchByramDroughtIndex: 390,
      portugueseContextRiskProxyLabel: 'Proxy degradado',
    },
  },
};

const timingsByRun = {
  'run-b': {
    runDurationMs: 3000,
    timeToFirstInboxMs: 120,
    firstAlertTriggeredAt: '2026-07-21T12:00:03Z',
    attempts: { p50Ms: 10, p95Ms: 20 },
    stages: [{ stage: 'publish_to_receive', p50Ms: 11, sampleCount: 30 }],
    timeline: [{ stage: 'Started', timestamp: '2026-07-21T12:00:01Z' }],
  },
  'run-c': {
    runDurationMs: 6000,
    timeToFirstInboxMs: 180,
    firstAlertTriggeredAt: null,
    attempts: { p50Ms: 12, p95Ms: 25 },
    stages: [{ stage: 'publish_to_receive', p50Ms: 15, sampleCount: 24 }],
    timeline: [{ stage: 'Started', timestamp: '2026-07-21T12:10:01Z' }],
  },
};

const operationByRun = {
  'run-b': {
    id: 'op-b',
    operationId: 'scenario-b',
    category: 'runtime',
    state: 'Completed',
    status: 'Completed',
    evidenceId: 'evidence-run-b',
    evidenceLevel: 'PROVED_LOCAL',
    acceptedAt: '2026-07-21T12:00:00Z',
    systemCompletedAt: '2026-07-21T12:00:04Z',
    finishedAt: '2026-07-21T12:00:05Z',
    accounting: {
      expectedObservations: 30,
      acceptedObservations: 30,
      processedInbox: 30,
      pendingInbox: 0,
      processingInbox: 0,
      retryPendingInbox: 0,
      quarantinedInbox: 0,
      settled: true,
    },
  },
  'run-c': {
    id: 'op-c',
    operationId: 'scenario-c',
    category: 'runtime',
    state: 'Completed',
    status: 'Completed',
    evidenceId: null,
    evidenceLevel: 'RUN_SCOPED',
    acceptedAt: '2026-07-21T12:10:00Z',
    systemCompletedAt: '2026-07-21T12:10:07Z',
    finishedAt: '2026-07-21T12:10:09Z',
    accounting: {
      expectedObservations: 30,
      acceptedObservations: 24,
      processedInbox: 24,
      pendingInbox: 0,
      processingInbox: 0,
      retryPendingInbox: 0,
      quarantinedInbox: 1,
      settled: true,
    },
  },
};

vi.mock('react-router-dom', () => ({
  useSearchParams: () => [searchParams, setSearchParamsMock],
}));

vi.mock('../components/PageHeader', () => ({
  PageHeader: ({ title, subtitle }: { title: string; subtitle: string }) => (
    <header>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </header>
  ),
}));

vi.mock('../components/ExportActions', () => ({
  ExportActions: ({ filename, content }: { filename: string; content: string }) => (
    <button type="button" data-content={content}>
      export:{filename}
    </button>
  ),
}));

vi.mock('../state/AreaContext', () => ({
  useUiArea: () => ({ resolvedAreaCode: 'PT-11' }),
}));

vi.mock('../state/ActivityContext', () => ({
  useUiActivity: () => ({
    selectedRunId: 'run-b',
    selectedRun: runs[0],
    runs,
  }),
}));

vi.mock('../services/api', () => ({
  api: {
    getRuntimeRun: vi.fn(async (runId: 'run-b' | 'run-c') => runs.find((run) => run.id === runId)),
    getRuntimeRunAudit: vi.fn(async (runId: 'run-b' | 'run-c') => auditByRun[runId]),
    getRuntimeRunTimings: vi.fn(async (runId: 'run-b' | 'run-c') => timingsByRun[runId]),
    getRuntimeOperationByRun: vi.fn(async (runId: 'run-b' | 'run-c') => operationByRun[runId]),
    getRuntimeRabbitMqMetrics: vi.fn(async () => ({
      queues: [{ queue: 'natureprotector.readings', queueRole: 'PrimaryWorkQueue', messagesTotal: 4, consumers: 2 }],
    })),
    listRuntimeEvidence: vi.fn(async () => ({
      items: [
        { evidenceId: 'evidence-run-b', scope: 'run-b', path: 'evidence/run-b' },
        { evidenceId: 'other', scope: 'other-run', path: 'evidence/other' },
      ],
    })),
  },
}));

describe('evidence diagnostic pages', () => {
  beforeEach(() => {
    searchParams = new URLSearchParams();
    setSearchParamsMock.mockClear();
  });

  it('executes prepared diagnostic presets against the selected run and exports scoped results', async () => {
    render(<DatabaseQueriesPage />);

    expect(screen.getByText('Consultas preparadas')).toBeInTheDocument();
    expect(screen.getByText('run-b')).toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('Nome ou grupo'), { target: { value: 'evidence' } });
    fireEvent.click(screen.getByRole('button', { name: /Evidence Evidence disponível/i }));
    fireEvent.click(screen.getByRole('button', { name: /Executar preset/i }));

    expect(await screen.findByText('evidence-run-b')).toBeInTheDocument();
    expect(screen.getByText('evidence/run-b')).toBeInTheDocument();
    expect(screen.queryByText('evidence/other')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'export:evidence-run-b.csv' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'export:evidence-run-b.json' })).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText('Nome ou grupo'), { target: { value: 'rabbitmq' } });
    fireEvent.click(screen.getByRole('button', { name: /Pipeline Backlog RabbitMQ/i }));
    fireEvent.click(screen.getByRole('button', { name: /Executar preset/i }));

    expect(await screen.findByText('natureprotector.readings')).toBeInTheDocument();
    expect(screen.getByText(/não mantém uma série histórica run-scoped/i)).toBeInTheDocument();
  });

  it('renders every run-scoped prepared diagnostic preset from live endpoint contracts', async () => {
    render(<DatabaseQueriesPage />);

    await executePreset('Resumo da run', 'scenario_b');
    expect(screen.getByText('Completed')).toBeInTheDocument();

    await executePreset('Convergência do accounting', 'settled');
    expect(screen.getByText('true')).toBeInTheDocument();

    await executePreset('NP Score', '84.500');
    expect(screen.getByText('Elevado')).toBeInTheDocument();

    await executePreset('FWI', 'Muito elevado');
    expect(screen.getByText('31.200')).toBeInTheDocument();

    await executePreset('KBDI', '420');
    expect(screen.getByText('Dry')).toBeInTheDocument();

    await executePreset('Portuguese Proxy', 'Proxy alto');
    expect(screen.getByText('pipeline')).toBeInTheDocument();

    await executePreset('Qualidade por sensor', 'Clean');
    expect(screen.getByText('Eligible')).toBeInTheDocument();

    await executePreset('Integridade, confidence e coverage', '0.980');
    expect(screen.getByText('100')).toBeInTheDocument();

    await executePreset('Latências do pipeline', 'publish_to_receive');
    expect(screen.getByText('30')).toBeInTheDocument();

    await executePreset('Throughput', '10');
    expect(screen.getByText('durationMs')).toBeInTheDocument();

    await executePreset('Retries e quarantine', 'retryPending');
    expect(screen.getByText('retries')).toBeInTheDocument();

    await executePreset('Alertas da run', '2026-07-21T12:00:03Z');
    expect(screen.getAllByText(/primeiro alerta/i).length).toBeGreaterThan(0);

    await executePreset('Estado e duração de cada fase', 'SystemCompleted');
    expect(screen.getByText('4000')).toBeInTheDocument();
  });

  it('compares two persisted runs with explicit warnings and metric deltas', async () => {
    render(<ScenarioComparisonPage />);

    fireEvent.click(screen.getByRole('button', { name: /Comparar/i }));

    await waitFor(() => expect(screen.getByText('Valores A/B e diferenças')).toBeInTheDocument());
    expect(screen.getByText('Seeds diferentes.')).toBeInTheDocument();
    expect(screen.getByText('Duração ou cadência configurada diferente.')).toBeInTheDocument();
    expect(screen.getByText('Número de sensores diferente.')).toBeInTheDocument();
    expect(screen.getByText('Perfis de degradação resolvidos diferentes.')).toBeInTheDocument();
    expect(screen.getByText('Versões de configuração diferentes.')).toBeInTheDocument();
    expect(screen.getByText('Coverage (%)')).toBeInTheDocument();
    expect(screen.getByText('100')).toBeInTheDocument();
    expect(screen.getByText('80')).toBeInTheDocument();
    expect(screen.getAllByText('-20.0%').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'export:comparacao-run-b-run-c.csv' })).toBeInTheDocument();
  });

  it('keeps historical query-string run ids visible without inventing a run catalog entry', () => {
    searchParams = new URLSearchParams('runA=historical-run&runB=run-c');

    render(<ScenarioComparisonPage />);

    expect(screen.getByRole('option', { name: 'historical-run · histórica' })).toBeInTheDocument();
    expect(screen.getByLabelText('Run A')).toHaveValue('historical-run');
    expect(screen.getByLabelText('Run B')).toHaveValue('run-c');
  });

  it('disables comparison when both selected run ids are equal', () => {
    searchParams = new URLSearchParams('runA=run-b&runB=run-b');

    render(<ScenarioComparisonPage />);

    expect(screen.getByRole('button', { name: /Comparar/i })).toBeDisabled();
  });

  it('surfaces run-scoped API errors without rendering stale comparison rows', async () => {
    vi.mocked(api.getRuntimeRunAudit).mockRejectedValueOnce(new Error('audit unavailable'));

    render(<ScenarioComparisonPage />);

    fireEvent.click(screen.getByRole('button', { name: /Comparar/i }));

    expect(await screen.findByText('audit unavailable')).toBeInTheDocument();
    expect(screen.queryByText('Valores A/B e diferenças')).not.toBeInTheDocument();
  });

  it('compares persisted runs even when direct operation association is unavailable', async () => {
    vi.mocked(api.getRuntimeOperationByRun)
      .mockRejectedValueOnce(new Error('operation unavailable'))
      .mockRejectedValueOnce(new Error('operation unavailable'));

    render(<ScenarioComparisonPage />);

    fireEvent.click(screen.getByRole('button', { name: /Comparar/i }));

    await waitFor(() => expect(screen.getByText('Valores A/B e diferenças')).toBeInTheDocument());
    expect(screen.getByText('Ligação direta a evidenceId')).toBeInTheDocument();
    expect(screen.getAllByText('Indisponível').length).toBeGreaterThan(0);
  });
});

async function executePreset(title: string, expectedText: string) {
  fireEvent.change(screen.getByPlaceholderText('Nome ou grupo'), { target: { value: title } });
  const preset = screen
    .getAllByRole('button')
    .find((button) => button.textContent?.toLocaleLowerCase('pt-PT').includes(title.toLocaleLowerCase('pt-PT')));
  expect(preset, `prepared preset ${title}`).toBeTruthy();
  fireEvent.click(preset!);
  fireEvent.click(screen.getByRole('button', { name: /Executar preset/i }));
  await waitFor(() => expect(screen.getAllByText(expectedText).length).toBeGreaterThan(0));
}
