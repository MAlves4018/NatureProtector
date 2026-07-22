import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AboutPage } from './AboutPage';
import { ApprovalsPage } from './ApprovalsPage';
import { CloudResourcesPage } from './CloudResourcesPage';
import { DashboardsPage } from './DashboardsPage';
import { DataContextPage } from './DataContextPage';
import { QaTestSuitePage } from './QaTestSuitePage';
import { QualityRunsPage } from './QualityRunsPage';

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({
    copy: (key: string) =>
      ({
        'about.title': 'Sobre',
        'about.subtitle': 'Contexto do produto',
        'about.body': 'NatureProtector protege claims com evidence.',
      })[key] ?? key,
  }),
}));

vi.mock('../state/CapabilityContext', () => ({
  useUiCapabilities: () => ({ isDark: true }),
}));

vi.mock('../state/AreaContext', () => ({
  useUiArea: () => ({ selectedAreaCode: 'PT-11' }),
}));

vi.mock('../components/AreaSelector', () => ({
  AreaSelector: ({ compact }: { compact?: boolean }) => (
    <div data-testid="area-selector">{compact ? 'compact-area-selector' : 'area-selector'}</div>
  ),
}));

vi.mock('../components/DataStatusSummary', () => ({
  DataStatusSummary: ({ showDetails = true }: { showDetails?: boolean }) => (
    <div data-testid="data-status-summary">{showDetails ? 'details' : 'summary'}</div>
  ),
}));

vi.mock('../components/views/dashBoards', () => ({
  DashBoards: ({ isDark, areaCode }: { isDark: boolean; areaCode: string }) => (
    <div data-testid="dashboards">
      {isDark ? 'dark' : 'light'}:{areaCode}
    </div>
  ),
}));

vi.mock('./OperationCategoryPage', () => ({
  OperationCategoryPage: ({ category, title, subtitle }: { category: string; title: string; subtitle: string }) => (
    <section aria-label={category}>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </section>
  ),
}));

vi.mock('../operations/OperationsContext', () => ({
  useOperations: () => ({
    pendingApprovals: [
      {
        id: 'approval-1',
        displayName: 'Deploy guarded runtime',
        status: 'PendingApproval',
        category: 'cloud',
        startedAt: '2026-07-21T12:00:00Z',
        updatedAt: '2026-07-21T12:01:00Z',
        steps: [],
        artifacts: [],
      },
    ],
    decide: vi.fn(async (id: string, decision: string, comment?: string) => ({
      id,
      displayName: `${decision}:${comment ?? 'no-comment'}`,
      status: 'Approved',
    })),
    environments: [
      {
        environment: 'local',
        observedState: 'configured',
        projectId: 'np-local',
        region: 'europe-west1',
        deployable: false,
        evidenceLevel: 'static',
        resources: [{ resourceType: 'database', name: 'postgres', state: 'declared' }],
      },
    ],
    catalog: [
      {
        operationId: 'cloud-plan',
        category: 'cloud',
        title: 'Plan cloud change',
        description: 'Dry-run only.',
      },
      {
        operationId: 'quality-run',
        category: 'quality',
        title: 'Quality run',
        description: 'Not shown in cloud page.',
      },
    ],
    operations: [
      {
        id: 'cloud-op-1',
        displayName: 'Cloud dry run',
        status: 'Completed',
        category: 'cloud',
        startedAt: '2026-07-21T12:00:00Z',
        updatedAt: '2026-07-21T12:01:00Z',
        steps: [],
        artifacts: [],
      },
      {
        id: 'quality-op-1',
        displayName: 'Quality historical run',
        status: 'Completed',
        category: 'quality',
        startedAt: '2026-07-21T12:00:00Z',
        updatedAt: '2026-07-21T12:01:00Z',
        steps: [],
        artifacts: [],
      },
    ],
  }),
}));

vi.mock('../operations/OperationLauncher', () => ({
  OperationLauncher: ({ definition }: { definition: { operationId: string; title: string } }) => (
    <button type="button">launch:{definition.operationId}:{definition.title}</button>
  ),
}));

vi.mock('../operations/OperationStatus', () => ({
  OperationStatus: ({ operation }: { operation: { id: string; displayName: string; status: string } }) => (
    <article aria-label={operation.id}>
      {operation.displayName}:{operation.status}
    </article>
  ),
}));

vi.mock('../state/QaTestContext', () => ({
  useUiQaTests: () => ({
    qaSuites: [
      {
        suiteId: 'qa-pass',
        suiteName: 'Functional smoke',
        category: 'runtime',
        status: 'Passed',
        testDefinition: 'scripts/validation/smoke.ps1',
        executedAt: '2026-07-21T12:00:00Z',
        environment: 'local',
        evidenceReference: 'evidence/smoke',
        limitations: [],
      },
      {
        suiteId: 'qa-finding',
        suiteName: 'Evidence audit',
        category: 'evidence',
        status: 'FindingsOpen',
        testDefinition: 'scripts/validation/evidence.ps1',
        executedAt: null,
        environment: 'local',
        evidenceReference: 'evidence/audit',
        limitations: ['requires live runtime'],
      },
      {
        suiteId: 'qa-unknown',
        suiteName: 'Mutation',
        category: 'mutation',
        status: 'NotRun',
        testDefinition: 'scripts/tests/run-mutation.ps1',
        executedAt: null,
        environment: 'local',
        evidenceReference: 'evidence/mutation',
        limitations: [],
      },
    ],
  }),
}));

describe('simple page wrappers', () => {
  it('renders about copy through the locale authority', () => {
    render(<AboutPage />);

    expect(screen.getByRole('heading', { name: 'Sobre', level: 2 })).toBeInTheDocument();
    expect(screen.getByText('Contexto do produto')).toBeInTheDocument();
    expect(screen.getByText('NatureProtector protege claims com evidence.')).toBeInTheDocument();
  });

  it('passes dashboard theme and selected area to the dashboard view', () => {
    render(<DashboardsPage />);

    expect(screen.getByTestId('dashboards')).toHaveTextContent('dark:PT-11');
  });

  it('binds quality runs to the quality operation category', () => {
    render(<QualityRunsPage />);

    expect(screen.getByRole('region', { name: 'quality' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Execuções de qualidade', level: 1 })).toBeInTheDocument();
    expect(screen.getByText(/não aceita comandos arbitrários/i)).toBeInTheDocument();
  });

  it('renders data context with localized copy and scoped data widgets', () => {
    render(<DataContextPage />);

    expect(screen.getByText('context.title')).toBeInTheDocument();
    expect(screen.getByText('context.subtitle')).toBeInTheDocument();
    expect(screen.getByTestId('data-status-summary')).toHaveTextContent('details');
    expect(screen.getByTestId('area-selector')).toHaveTextContent('compact-area-selector');
  });

  it('lists pending approvals and submits an auditable decision comment', async () => {
    render(<ApprovalsPage />);

    await screen.findByRole('article', { name: 'approval-1' });
    fireEvent.change(screen.getByLabelText(/Comentário da decisão/i), { target: { value: 'reviewed' } });
    fireEvent.click(screen.getByRole('button', { name: /Aprovar/i }));

    expect(await screen.findByText('approve:reviewed: Approved')).toBeInTheDocument();
  });

  it('shows only cloud definitions and cloud history on the cloud resources page', () => {
    render(<CloudResourcesPage />);

    expect(screen.getByText('np-local')).toBeInTheDocument();
    expect(screen.getByText('postgres')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /launch:cloud-plan/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /quality-run/i })).not.toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'cloud-op-1' })).toHaveTextContent('Cloud dry run:Completed');
    expect(screen.queryByRole('article', { name: 'quality-op-1' })).not.toBeInTheDocument();
  });

  it('renders QA suites with passed, finding and unknown states plus limitations', () => {
    render(<QaTestSuitePage />);

    expect(screen.getByText('Suites documentadas (3)')).toBeInTheDocument();
    expect(screen.getByText('Functional smoke')).toBeInTheDocument();
    expect(screen.getByText('Evidence audit')).toBeInTheDocument();
    expect(screen.getByText('Mutation')).toBeInTheDocument();
    expect(screen.getByText('requires live runtime')).toBeInTheDocument();
    expect(screen.getByText('scripts/tests/run-mutation.ps1')).toBeInTheDocument();
  });
});
