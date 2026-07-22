import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DeploymentHealthPage } from './DeploymentHealthPage';
import { DeploymentsPage } from './DeploymentsPage';
import { MissionControlPage } from './MissionControlPage';
import { OperationCategoryPage } from './OperationCategoryPage';
import { OverviewPage } from './OverviewPage';

const navigateMock = vi.fn();
const setSelectedRunIdMock = vi.fn();
const refreshObservabilityMock = vi.fn();
const refreshOperationsMock = vi.fn();

let operationsState: {
  catalog: Array<Record<string, unknown>>;
  operations: Array<Record<string, unknown>>;
  environments: Array<Record<string, unknown>>;
  loading: boolean;
  error: Error | null;
  refresh: () => Promise<void>;
};

let observabilityState: {
  operationalHealth: Record<string, unknown> | null;
  rabbitMqMetrics: Record<string, unknown> | null;
  observabilityError: Error | null;
};

let activeAlerts: Array<{ id: string; severity: string; message: string }>;

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
}));

vi.mock('../components/AreaSelector', () => ({
  AreaSelector: ({ compact }: { compact?: boolean }) => (
    <div data-testid="area-selector">{compact ? 'compact' : 'full'}</div>
  ),
}));

vi.mock('../components/DataStatusSummary', () => ({
  DataStatusSummary: ({ showDetails = true }: { showDetails?: boolean }) => (
    <div data-testid="data-status-summary">{showDetails ? 'details' : 'summary'}</div>
  ),
}));

vi.mock('../components/PageHeader', () => ({
  PageHeader: ({ title, subtitle, actions }: { title: string; subtitle: string; actions?: React.ReactNode }) => (
    <header>
      <h1>{title}</h1>
      <p>{subtitle}</p>
      {actions}
    </header>
  ),
}));

vi.mock('../operations/OperationLauncher', () => ({
  OperationLauncher: ({ definition }: { definition: Record<string, unknown> }) => (
    <button type="button">launch:{String(definition.operationId)}</button>
  ),
}));

vi.mock('../operations/OperationStatus', () => ({
  OperationStatus: ({ operation, compact }: { operation: Record<string, unknown>; compact?: boolean }) => (
    <article aria-label={String(operation.id)}>
      status:{String(operation.displayName)}:{String(operation.status)}:{compact ? 'compact' : 'full'}
    </article>
  ),
}));

vi.mock('../operations/OperationsContext', () => ({
  useOperations: () => operationsState,
}));

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({ copy: (key: string) => (key === 'readiness.title' ? 'Readiness comprovada' : key) }),
}));

vi.mock('../state/RiskContext', () => ({
  useUiRisk: () => ({
    riskModel: {
      canShowScore: true,
      scoreDisplay: '82/100',
      classDisplay: 'Elevado',
      summary: 'risco observado',
      state: 'Warning',
    },
    summary: {
      currentRun: null,
      latestRun: null,
    },
  }),
}));

vi.mock('../state/ActivityContext', () => ({
  useUiActivity: () => ({
    runContext: {
      state: 'completed',
      run: {
        id: 'run-overview-1',
        status: 'Completed',
        scenarioName: 'Nominal A',
        areaCode: 'PT-11',
        numberOfCycles: 3,
      },
    },
    setSelectedRunId: setSelectedRunIdMock,
  }),
}));

vi.mock('../state/ObservabilityContext', () => ({
  useUiObservability: () => ({
    ...observabilityState,
    refreshObservability: refreshObservabilityMock,
  }),
}));

vi.mock('../state/AlertContext', () => ({
  useUiAlerts: () => ({ activeAlerts }),
}));

vi.mock('../state/useUiSurfaces', () => ({
  useReadinessItems: () => [
    {
      item: 'Runtime',
      status: 'READY',
      evidence: 'health probe live',
      limitation: 'local only',
    },
  ],
}));

describe('operational pages', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    setSelectedRunIdMock.mockClear();
    refreshObservabilityMock.mockClear();
    refreshOperationsMock.mockClear();
    activeAlerts = [];
    observabilityState = {
      operationalHealth: {
        observedAt: '2026-07-21T12:00:00Z',
        components: [
          {
            component: 'Backoffice API',
            status: 'Healthy',
            source: 'liveness',
            ageSeconds: 12,
            lastSuccessAt: '2026-07-21T11:59:50Z',
            lastFailureAt: null,
            reason: 'HTTP 200',
          },
          {
            component: 'RabbitMQ',
            status: 'Degraded',
            source: 'management',
            ageSeconds: 120,
            lastSuccessAt: '2026-07-21T11:58:00Z',
            lastFailureAt: '2026-07-21T11:57:00Z',
            reason: 'queue backlog',
          },
        ],
      },
      rabbitMqMetrics: {
        queues: [{ queueRole: 'PrimaryWorkQueue', messagesTotal: 4, consumers: 2 }],
      },
      observabilityError: null,
    };
    operationsState = {
      loading: false,
      error: null,
      catalog: [
        {
          operationId: 'deploy-staging',
          category: 'deployment',
          displayName: 'Deploy staging',
          title: 'Deploy staging',
          description: 'Staging smoke',
          environments: ['staging'],
          riskLevel: 'Medium',
          authorized: true,
          requiredCapability: 'DeploymentOperator',
          availability: 'implemented',
          evidenceLevel: 'local-evidence',
        },
        {
          operationId: 'deploy-prod',
          category: 'deployment',
          displayName: 'Deploy produção',
          title: 'Deploy produção',
          description: 'Release protegida',
          environments: ['production'],
          riskLevel: 'High',
          authorized: false,
          requiredCapability: 'DeploymentAdmin',
          availability: 'implemented',
          evidenceLevel: 'operator-evidence',
        },
        {
          operationId: 'quality-smoke',
          category: 'quality',
          displayName: 'Quality smoke',
          title: 'Quality smoke',
          description: 'Suite curta',
          environments: ['local'],
          riskLevel: 'Low',
          authorized: true,
          requiredCapability: 'QaOperator',
          availability: 'implemented',
          evidenceLevel: 'test-results',
        },
      ],
      operations: [
        {
          id: 'deployment-op-1',
          operationId: 'deploy-staging',
          category: 'deployment',
          displayName: 'Deploy staging',
          status: 'Queued',
          provider: 'local',
          requestedBy: 'operator',
          evidenceLevel: 'local-evidence',
          startedAt: '2026-07-21T12:00:00Z',
          updatedAt: '2026-07-21T12:00:05Z',
          completedAt: null,
        },
        {
          id: 'quality-op-1',
          operationId: 'quality-smoke',
          category: 'quality',
          displayName: 'Quality smoke',
          status: 'Completed',
          provider: 'local',
          requestedBy: 'qa',
          evidenceLevel: 'test-results',
          startedAt: '2026-07-21T12:00:00Z',
          updatedAt: '2026-07-21T12:00:03Z',
          completedAt: '2026-07-21T12:00:03Z',
        },
      ],
      environments: [
        {
          environment: 'staging',
          projectId: 'np-staging',
          observedState: 'declared',
          deployable: true,
          limitations: [],
        },
        {
          environment: 'production',
          projectId: 'np-production',
          observedState: 'locked',
          deployable: true,
          limitations: ['requires owner approval'],
        },
      ],
      refresh: refreshOperationsMock,
    };
  });

  it('renders overview health, backlog, active run, alerts and navigation actions', () => {
    activeAlerts = [{ id: 'alert-1', severity: 'Warning', message: 'vento alto' }];

    render(<OverviewPage />);

    expect(screen.getByText('Visão geral operacional')).toBeInTheDocument();
    expect(screen.getByText('Degraded')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByText('2 consumidores')).toBeInTheDocument();
    expect(screen.getByText('82/100')).toBeInTheDocument();
    expect(screen.getByText('SimulationRunId · run-overview-1')).toBeInTheDocument();
    expect(screen.getByText('Warning')).toBeInTheDocument();
    expect(screen.getByText('vento alto')).toBeInTheDocument();
    expect(screen.getByText('health probe live')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Atualizar readiness/i }));
    expect(refreshObservabilityMock).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: /Abrir run/i }));
    expect(setSelectedRunIdMock).toHaveBeenCalledWith('run-overview-1');
    expect(navigateMock).toHaveBeenCalledWith('/runs');
  });

  it('renders deployment health with global degradation, component detail and errors', () => {
    observabilityState.observabilityError = new Error('observability offline');

    render(<DeploymentHealthPage />);

    expect(screen.getByText('Observed deployment degraded')).toBeInTheDocument();
    expect(screen.getByText('1/2 healthy')).toBeInTheDocument();
    expect(screen.getByText('Operational: 1')).toBeInTheDocument();
    expect(screen.getByText('Degraded: 1')).toBeInTheDocument();
    expect(screen.getByText('Backoffice API')).toBeInTheDocument();
    expect(screen.getByText('RabbitMQ')).toBeInTheDocument();
    expect(screen.getByText('2min')).toBeInTheDocument();
    expect(screen.getByText('observability offline')).toBeInTheDocument();
  });

  it('filters operation category definitions and history by category', () => {
    render(<OperationCategoryPage category="quality" title="Qualidade" subtitle="Suites auditáveis" />);

    expect(screen.getByRole('heading', { name: 'Qualidade' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'launch:quality-smoke' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'launch:deploy-staging' })).not.toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'quality-op-1' })).toHaveTextContent(
      'status:Quality smoke:Completed:full',
    );
    expect(screen.queryByRole('article', { name: 'deployment-op-1' })).not.toBeInTheDocument();
  });

  it('filters deployment catalog by environment and opens selected operation detail', () => {
    render(<DeploymentsPage />);

    expect(screen.getByText('Provider')).toBeInTheDocument();
    expect(screen.getByText('Pedidos em fila')).toBeInTheDocument();
    expect(screen.getByText('Deploy staging')).toBeInTheDocument();
    expect(screen.queryByText('Deploy produção')).not.toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'deployment-op-1' })).toHaveTextContent(
      'status:Deploy staging:Queued:compact',
    );

    fireEvent.click(screen.getByRole('button', { name: 'Abrir detalhe' }));
    expect(screen.getByRole('heading', { name: 'Deploy staging' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'launch:deploy-staging' })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Ambiente'), { target: { value: 'production' } });
    expect(screen.getByText('Deploy produção')).toBeInTheDocument();
    expect(screen.getByText('Bloqueada por role')).toBeInTheDocument();
    expect(screen.getByText('Requer DeploymentAdmin')).toBeInTheDocument();
  });

  it('renders mission control readiness from operations, environments and approvals', () => {
    operationsState.catalog.push(
      {
        operationId: 'evidence-campaign',
        category: 'evidence',
        displayName: 'Evidence campaign',
        availability: 'implemented',
      },
      {
        operationId: 'production-deploy',
        category: 'deployment',
        displayName: 'Production deploy',
        availability: 'implemented',
        authorized: false,
        limitation: 'separate production decision',
      },
    );
    operationsState.operations.unshift(
      {
        id: 'evidence-op-1',
        operationId: 'evidence-campaign',
        category: 'evidence',
        displayName: 'Evidence campaign',
        status: 'Completed',
        evidenceLevel: 'PROVED_LOCAL',
      },
      {
        id: 'approval-op-1',
        operationId: 'production-deploy',
        category: 'deployment',
        displayName: 'Production deployment',
        status: 'AwaitingApproval',
        evidenceLevel: 'REQUEST_ONLY',
      },
    );

    render(<MissionControlPage />);

    expect(screen.getByText('Mission Control')).toBeInTheDocument();
    expect(screen.getAllByText('Quality').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Evidence').length).toBeGreaterThan(0);
    expect(screen.getByText('np-staging · declared; não equivale a deployment observado.')).toBeInTheDocument();
    expect(screen.getByText('requires owner approval')).toBeInTheDocument();
    expect(screen.getByText('1 PENDING')).toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'evidence-op-1' })).toHaveTextContent(
      'status:Evidence campaign:Completed:compact',
    );

    fireEvent.click(screen.getByRole('button', { name: 'Atualizar' }));
    expect(refreshOperationsMock).toHaveBeenCalledTimes(1);
  });
});
