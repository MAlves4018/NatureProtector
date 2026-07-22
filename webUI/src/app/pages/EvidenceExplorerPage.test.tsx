import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { EvidenceExplorerPage } from './EvidenceExplorerPage';

const compareMock = vi.fn();
const downloadRuntimeEvidenceMock = vi.fn();

let operationsState: any;
let observabilityState: any;
let activityState: any;
let capabilitiesState: Set<string>;

vi.mock('../components/PageHeader', () => ({
  PageHeader: ({ title, subtitle }: { title: string; subtitle: string }) => (
    <header>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </header>
  ),
}));

vi.mock('../components/ComparisonBarChart', () => ({
  ComparisonBarChart: ({ comparison }: { comparison: any }) => (
    <div data-testid="comparison-chart">
      {comparison
        ? `${comparison.leftStatus}:${comparison.rightStatus}:${comparison.sharedArtifacts.length}`
        : 'no-comparison'}
    </div>
  ),
}));

vi.mock('../operations/OperationLauncher', () => ({
  OperationLauncher: ({ definition }: { definition: any }) => (
    <button type="button">launch:{definition.operationId}</button>
  ),
}));

vi.mock('../operations/OperationStatus', () => ({
  OperationStatus: ({ operation, compact }: { operation: any; compact?: boolean }) => (
    <article aria-label={operation.id}>
      status:{operation.displayName}:{operation.status}:{compact ? 'compact' : 'full'}
    </article>
  ),
}));

vi.mock('../operations/OperationsContext', () => ({
  useOperations: () => operationsState,
}));

vi.mock('../services/api', () => ({
  api: {
    downloadRuntimeEvidence: (evidenceId: string) => downloadRuntimeEvidenceMock(evidenceId),
  },
}));

vi.mock('../state/CapabilityContext', () => ({
  useUiCapabilities: () => ({ capabilities: capabilitiesState }),
}));

vi.mock('../state/ObservabilityContext', () => ({
  useUiObservability: () => observabilityState,
}));

vi.mock('../state/ActivityContext', () => ({
  useUiActivity: () => activityState,
}));

describe('EvidenceExplorerPage', () => {
  beforeEach(() => {
    compareMock.mockReset();
    downloadRuntimeEvidenceMock.mockReset();
    capabilitiesState = new Set(['evidence.download']);
    operationsState = buildOperationsState();
    observabilityState = buildObservabilityState();
    activityState = buildActivityState();
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:runtime-evidence'),
      revokeObjectURL: vi.fn(),
    });
  });

  it('renders run-scoped claims, scoped runtime evidence and governed campaign launcher', () => {
    render(<EvidenceExplorerPage />);

    expect(screen.getByRole('heading', { name: 'Cockpit de evidência' })).toBeInTheDocument();
    expect(screen.getByText('Campanhas no catálogo')).toBeInTheDocument();
    expect(screen.getByText('Execuções registadas')).toBeInTheDocument();
    expect(screen.getByText('Artefactos da run')).toBeInTheDocument();
    expect(screen.getByText('Transferíveis')).toBeInTheDocument();
    expect(screen.getByText('Accounting run-scoped')).toBeInTheDocument();
    expect(screen.getByText('30/30 aceites; 30 avaliados')).toBeInTheDocument();
    expect(screen.getByText('Lifecycle e settlement')).toBeInTheDocument();
    expect(screen.getByText('Completed; settled=true')).toBeInTheDocument();
    expect(screen.getByText('Timings persistidos')).toBeInTheDocument();
    expect(screen.getByText('14000.0 ms; 30 tentativas')).toBeInTheDocument();
    expect(screen.getByText('Índices científicos persistidos')).toBeInTheDocument();
    expect(screen.getByText('NP=82; FWI=31.2; KBDI=420')).toBeInTheDocument();
    expect(screen.getByText('Run evidence package')).toBeInTheDocument();
    expect(screen.getByText('Direct operation evidence')).toBeInTheDocument();
    expect(screen.queryByText('Other run evidence')).not.toBeInTheDocument();
    expect(screen.getByText('observability delayed')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Evidence campaign/i }));
    expect(screen.getByRole('button', { name: 'launch:evidence-campaign' })).toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'evidence-op-1' })).toHaveTextContent(
      'status:Evidence campaign:Succeeded:compact',
    );
    expect(screen.getByRole('article', { name: 'runtime-op-1' })).toHaveTextContent(
      'status:Runtime run:Completed:compact',
    );
  });

  it('downloads scoped evidence with backend filename and reports failures', async () => {
    const clickMock = vi.fn();
    const originalCreateElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = originalCreateElement(tagName);
      if (tagName === 'a') {
        Object.defineProperty(element, 'click', { value: clickMock });
      }
      return element;
    });
    downloadRuntimeEvidenceMock.mockResolvedValueOnce({
      blob: new Blob(['payload'], { type: 'text/plain' }),
      filename: 'run-b.txt',
    });

    render(<EvidenceExplorerPage />);

    fireEvent.click(screen.getAllByRole('button', { name: /Transferir/i })[0]);
    expect(await screen.findByRole('status')).toHaveTextContent('Artefacto evidence-run-b transferido.');
    expect(downloadRuntimeEvidenceMock).toHaveBeenCalledWith('evidence-run-b');
    expect(clickMock).toHaveBeenCalledTimes(1);
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:runtime-evidence');

    downloadRuntimeEvidenceMock.mockRejectedValueOnce(new Error('download denied'));
    fireEvent.click(screen.getAllByRole('button', { name: /Transferir/i })[0]);
    expect(await screen.findByText('download denied')).toBeInTheDocument();
  });

  it('disables downloads without capability and handles comparison success and failure', async () => {
    capabilitiesState = new Set();
    compareMock.mockResolvedValueOnce({
      leftOperationId: 'left-op',
      rightOperationId: 'right-op',
      leftStatus: 'Succeeded',
      rightStatus: 'Failed',
      onlyOnLeft: ['left.csv'],
      onlyOnRight: [],
      sharedArtifacts: ['shared.json'],
      evidenceLevel: 'runtime',
    });

    render(<EvidenceExplorerPage />);

    expect(screen.getAllByRole('button', { name: /Transferir/i })[0]).toBeDisabled();
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'evidence-op-1' } });
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'runtime-op-1' } });
    fireEvent.click(screen.getByRole('button', { name: /Comparar/i }));

    await waitFor(() => expect(compareMock).toHaveBeenCalledWith('evidence-op-1', 'runtime-op-1'));
    expect(screen.getByTestId('comparison-chart')).toHaveTextContent('Succeeded:Failed:1');

    compareMock.mockRejectedValueOnce(new Error('comparison failed'));
    fireEvent.click(screen.getByRole('button', { name: /Comparar/i }));
    expect(await screen.findByText('comparison failed')).toBeInTheDocument();
  });

  it('renders empty states when no run evidence or campaign is available', () => {
    operationsState = {
      catalog: [],
      operations: [],
      compare: compareMock,
    };
    observabilityState = {
      evidenceCatalog: { items: [] },
      observabilityError: null,
    };
    activityState = buildActivityState({
      selectedRunId: '',
      runAudit: null,
      runTimings: null,
      runOperation: null,
    });

    render(<EvidenceExplorerPage />);

    expect(screen.getByText('Selecione uma execução')).toBeInTheDocument();
    expect(screen.getByText('O runtime não publicou artefactos consultáveis.')).toBeInTheDocument();
    expect(screen.getByText('Sem campanhas autorizadas para este perfil.')).toBeInTheDocument();
    expect(screen.getByText('Sem execuções de evidence registadas.')).toBeInTheDocument();
    expect(screen.getAllByText('Não verificado').length).toBeGreaterThan(0);
  });
});

function buildOperationsState() {
  return {
    catalog: [
      {
        operationId: 'evidence-campaign',
        category: 'evidence',
        displayName: 'Evidence campaign',
        description: 'Collects live report-ready evidence.',
        authorized: true,
        availability: 'implemented',
        requiredCapability: 'evidence.run',
      },
      {
        operationId: 'quality-smoke',
        category: 'quality',
        displayName: 'Quality smoke',
        description: 'Not an evidence campaign.',
      },
    ],
    operations: [
      {
        id: 'evidence-op-1',
        category: 'evidence',
        displayName: 'Evidence campaign',
        status: 'Succeeded',
        artifacts: [],
      },
      {
        id: 'runtime-op-1',
        category: 'runtime',
        displayName: 'Runtime run',
        status: 'Completed',
        artifacts: [{ artifactId: 'run-b', name: 'Run export' }],
      },
      {
        id: 'quality-op-1',
        category: 'quality',
        displayName: 'Quality smoke',
        status: 'Succeeded',
        artifacts: [],
      },
    ],
    compare: compareMock,
  };
}

function buildObservabilityState() {
  return {
    observabilityError: new Error('observability delayed'),
    evidenceCatalog: {
      items: [
        {
          evidenceId: 'evidence-run-b',
          title: 'Run evidence package',
          type: 'runtime',
          status: 'Available',
          generatedAt: '2026-07-21T12:00:20Z',
          environment: 'local',
          scope: 'SimulationRunId run-b',
          version: 'v1',
          size: 42,
          contentAvailable: true,
          downloadAvailable: true,
          limitation: null,
        },
        {
          evidenceId: 'operation-evidence',
          title: 'Direct operation evidence',
          type: 'quality',
          status: 'Available',
          generatedAt: null,
          environment: 'local',
          scope: 'detached',
          version: null,
          size: 9,
          contentAvailable: true,
          downloadAvailable: true,
          limitation: 'manual review required',
        },
        {
          evidenceId: 'other-run',
          title: 'Other run evidence',
          type: 'runtime',
          status: 'Available',
          generatedAt: null,
          environment: 'local',
          scope: 'run-c',
          version: null,
          size: 1,
          contentAvailable: true,
          downloadAvailable: true,
          limitation: null,
        },
      ],
    },
  };
}

function buildActivityState(overrides: Record<string, unknown> = {}) {
  return {
    selectedRunId: 'run-b',
    runAudit: {
      expectedEvents: 30,
      acceptedReadings: 30,
      riskAssessments: 30,
      dataScope: { observedAt: '2026-07-21T12:00:21Z' },
      scoreComponents: {
        npScore: 82,
        latestAssessmentTimestamp: '2026-07-21T12:00:18Z',
      },
      indexComparison: {
        fireWeatherIndex: 31.2,
        keetchByramDroughtIndex: 420,
      },
    },
    runTimings: {
      runDurationMs: 14000,
      attempts: { attemptCount: 30 },
      dataScope: { observedAt: '2026-07-21T12:00:22Z' },
    },
    runOperation: {
      state: 'Completed',
      status: 'Completed',
      updatedAt: '2026-07-21T12:00:23Z',
      evidenceId: 'operation-evidence',
      accounting: {
        settled: true,
      },
    },
    ...overrides,
  };
}
