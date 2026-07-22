import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import type {
  CloudEnvironmentResponse,
  EngineeringOperationResponse,
  OperationComparisonResponse,
  OperationDefinitionResponse,
} from '../types/operations';
import { OperationsProvider, useOperations } from './OperationsContext';

interface CapabilityMockState {
  value: {
    user: { id: string; username: string; fullName: string; email: string; roles: string[] } | null;
    capabilities: Set<string>;
  };
}

const capabilityState = vi.hoisted<CapabilityMockState>(() => ({
  value: {
    user: { id: 'user-1', username: 'miguel', fullName: 'Miguel Alves', email: 'miguel@example.test', roles: [] },
    capabilities: new Set(['cloud.read', 'approval.review']),
  },
}));

vi.mock('../state/CapabilityContext', () => ({
  useUiCapabilities: () => capabilityState.value,
}));

vi.mock('../services/api', () => ({
  api: {
    listOperationCatalog: vi.fn(),
    listOperations: vi.fn(),
    listCloudEnvironments: vi.fn(),
    startOperation: vi.fn(),
    cancelOperation: vi.fn(),
    decideOperation: vi.fn(),
    compareEvidenceOperations: vi.fn(),
  },
}));

function OperationsProbe() {
  const { catalog, operations, environments, pendingApprovals, loading, error, refresh, start, cancel, decide, compare } =
    useOperations();

  return (
    <div>
      <p>catalog:{catalog.map((item) => item.operationId).join(',') || 'none'}</p>
      <p>operations:{operations.map((item) => item.id).join(',') || 'none'}</p>
      <p>environments:{environments.map((item) => item.environment).join(',') || 'none'}</p>
      <p>pending:{pendingApprovals.map((item) => item.id).join(',') || 'none'}</p>
      <p>loading:{String(loading)}</p>
      <p>error:{error?.message ?? 'none'}</p>
      <button type="button" onClick={() => void refresh()}>
        refresh
      </button>
      <button
        type="button"
        onClick={() =>
          void start({
            operationId: 'quality.freeze',
            environment: 'local',
            ref: 'main',
            inputs: { ref: 'main' },
            collectEvidence: true,
            confirmation: null,
          })
        }
      >
        start
      </button>
      <button type="button" onClick={() => void cancel('op-1')}>
        cancel
      </button>
      <button type="button" onClick={() => void decide('op-1', 'approve', 'reviewed')}>
        approve
      </button>
      <button type="button" onClick={() => void compare('left', 'right')}>
        compare
      </button>
    </div>
  );
}

function renderProvider() {
  render(
    <OperationsProvider>
      <OperationsProbe />
    </OperationsProvider>,
  );
}

function definition(overrides: Partial<OperationDefinitionResponse> = {}): OperationDefinitionResponse {
  return {
    operationId: 'quality.freeze',
    category: 'quality',
    displayName: 'Freeze verify',
    description: 'Runs freeze verification',
    requiredCapability: 'quality.execute.full',
    riskLevel: 'medium',
    requiresConfirmation: false,
    requiresApproval: false,
    environments: ['local'],
    inputs: [],
    workflow: 'freeze',
    confirmationTemplate: '',
    authorized: true,
    availability: 'implemented',
    evidenceLevel: 'PROVED_LOCAL',
    limitation: null,
    ...overrides,
  };
}

function operation(overrides: Partial<EngineeringOperationResponse> = {}): EngineeringOperationResponse {
  return {
    id: 'op-1',
    operationId: 'quality.freeze',
    category: 'quality',
    displayName: 'Freeze verify',
    status: 'Queued',
    environment: 'local',
    ref: 'main',
    requestedBy: 'miguel',
    requestedByRoles: ['QA'],
    requestedByCapabilities: ['quality.execute.full'],
    requestedAt: '2026-07-21T10:00:00.000Z',
    updatedAt: '2026-07-21T10:00:00.000Z',
    collectEvidence: true,
    riskLevel: 'medium',
    requiresApproval: false,
    provider: null,
    providerReference: null,
    workflow: 'freeze',
    planHash: null,
    evidenceLevel: 'CONTROL_PLANE_ONLY',
    inputs: {},
    steps: [],
    artifacts: [],
    approvals: [],
    limitations: [],
    ...overrides,
  };
}

function environment(overrides: Partial<CloudEnvironmentResponse> = {}): CloudEnvironmentResponse {
  return {
    environment: 'local',
    projectId: 'np-local',
    region: 'local',
    deployable: false,
    configurationSource: 'test',
    observedState: 'configured',
    evidenceLevel: 'STATIC_CONFIRMED',
    resources: [],
    limitations: [],
    ...overrides,
  };
}

describe('OperationsContext', () => {
  beforeEach(() => {
    capabilityState.value = {
      user: { id: 'user-1', username: 'miguel', fullName: 'Miguel Alves', email: 'miguel@example.test', roles: [] },
      capabilities: new Set(['cloud.read', 'approval.review']),
    };
    vi.mocked(api.listOperationCatalog).mockReset();
    vi.mocked(api.listOperations).mockReset();
    vi.mocked(api.listCloudEnvironments).mockReset();
    vi.mocked(api.startOperation).mockReset();
    vi.mocked(api.cancelOperation).mockReset();
    vi.mocked(api.decideOperation).mockReset();
    vi.mocked(api.compareEvidenceOperations).mockReset();
  });

  it('loads catalog, operations, cloud environments and reviewable approvals for an authorized user', async () => {
    vi.mocked(api.listOperationCatalog).mockResolvedValue([definition()]);
    vi.mocked(api.listOperations).mockResolvedValue([
      operation({ id: 'op-queued' }),
      operation({ id: 'op-approval', status: 'AwaitingApproval', requiresApproval: true }),
    ]);
    vi.mocked(api.listCloudEnvironments).mockResolvedValue([environment()]);

    renderProvider();

    expect(await screen.findByText('catalog:quality.freeze')).toBeInTheDocument();
    expect(screen.getByText('operations:op-queued,op-approval')).toBeInTheDocument();
    expect(screen.getByText('environments:local')).toBeInTheDocument();
    expect(screen.getByText('pending:op-approval')).toBeInTheDocument();
    expect(api.listOperations).toHaveBeenCalledWith(undefined, 100);
  });

  it('clears operational data without calling protected endpoints when there is no authenticated user', async () => {
    capabilityState.value = { user: null, capabilities: new Set() };

    renderProvider();

    expect(await screen.findByText('catalog:none')).toBeInTheDocument();
    expect(screen.getByText('operations:none')).toBeInTheDocument();
    expect(screen.getByText('environments:none')).toBeInTheDocument();
    expect(screen.getByText('error:none')).toBeInTheDocument();
    expect(api.listOperationCatalog).not.toHaveBeenCalled();
    expect(api.listOperations).not.toHaveBeenCalled();
    expect(api.listCloudEnvironments).not.toHaveBeenCalled();
  });

  it('fails closed for cloud inventory when the user lacks cloud.read capability', async () => {
    capabilityState.value = {
      user: { id: 'user-1', username: 'miguel', fullName: 'Miguel Alves', email: 'miguel@example.test', roles: [] },
      capabilities: new Set(),
    };
    vi.mocked(api.listOperationCatalog).mockResolvedValue([definition()]);
    vi.mocked(api.listOperations).mockResolvedValue([operation({ status: 'AwaitingApproval' })]);

    renderProvider();

    expect(await screen.findByText('catalog:quality.freeze')).toBeInTheDocument();
    expect(screen.getByText('environments:none')).toBeInTheDocument();
    expect(screen.getByText('pending:none')).toBeInTheDocument();
    expect(api.listCloudEnvironments).not.toHaveBeenCalled();
  });

  it('surfaces refresh failures and clears the loading flag', async () => {
    vi.mocked(api.listOperationCatalog).mockRejectedValue(new Error('catalog unavailable'));
    vi.mocked(api.listOperations).mockResolvedValue([]);
    vi.mocked(api.listCloudEnvironments).mockResolvedValue([]);

    renderProvider();

    expect(await screen.findByText('error:catalog unavailable')).toBeInTheDocument();
    expect(screen.getByText('loading:false')).toBeInTheDocument();
  });

  it('refreshes observable state after start, cancel and approval decisions', async () => {
    vi.mocked(api.listOperationCatalog).mockResolvedValue([definition()]);
    vi.mocked(api.listCloudEnvironments).mockResolvedValue([]);
    vi.mocked(api.listOperations)
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([operation({ id: 'op-started' })])
      .mockResolvedValueOnce([operation({ id: 'op-cancelled', status: 'Cancelled' })])
      .mockResolvedValueOnce([operation({ id: 'op-approved', status: 'Approved' })]);
    vi.mocked(api.startOperation).mockResolvedValue(operation({ id: 'op-started' }));
    vi.mocked(api.cancelOperation).mockResolvedValue(operation({ id: 'op-cancelled', status: 'Cancelled' }));
    vi.mocked(api.decideOperation).mockResolvedValue(operation({ id: 'op-approved', status: 'Approved' }));

    renderProvider();
    expect(await screen.findByText('operations:none')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'start' }));
    expect(await screen.findByText('operations:op-started')).toBeInTheDocument();
    expect(api.startOperation).toHaveBeenCalledWith(
      expect.objectContaining({ operationId: 'quality.freeze', environment: 'local' }),
    );

    fireEvent.click(screen.getByRole('button', { name: 'cancel' }));
    expect(await screen.findByText('operations:op-cancelled')).toBeInTheDocument();
    expect(api.cancelOperation).toHaveBeenCalledWith('op-1');

    fireEvent.click(screen.getByRole('button', { name: 'approve' }));
    expect(await screen.findByText('operations:op-approved')).toBeInTheDocument();
    expect(api.decideOperation).toHaveBeenCalledWith('op-1', 'approve', 'reviewed');
  });

  it('delegates evidence comparison without mutating loaded operations', async () => {
    const comparison: OperationComparisonResponse = {
      leftOperationId: 'left',
      rightOperationId: 'right',
      leftStatus: 'Succeeded',
      rightStatus: 'Failed',
      onlyOnLeft: ['left.csv'],
      onlyOnRight: ['right.csv'],
      sharedArtifacts: ['manifest.csv'],
      evidenceLevel: 'REPORT_READY',
    };
    vi.mocked(api.listOperationCatalog).mockResolvedValue([definition()]);
    vi.mocked(api.listOperations).mockResolvedValue([operation({ id: 'stable' })]);
    vi.mocked(api.listCloudEnvironments).mockResolvedValue([]);
    vi.mocked(api.compareEvidenceOperations).mockResolvedValue(comparison);

    renderProvider();
    expect(await screen.findByText('operations:stable')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'compare' }));

    await waitFor(() => expect(api.compareEvidenceOperations).toHaveBeenCalledWith('left', 'right'));
    expect(screen.getByText('operations:stable')).toBeInTheDocument();
  });
});
