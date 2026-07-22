import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
  EngineeringOperationResponse,
  OperationDefinitionResponse,
  StartOperationRequest,
} from '../types/operations';
import { OperationLauncher } from './OperationLauncher';

const start = vi.fn<(request: StartOperationRequest) => Promise<EngineeringOperationResponse>>();

vi.mock('./OperationsContext', () => ({
  useOperations: () => ({ start }),
}));

function definition(overrides: Partial<OperationDefinitionResponse> = {}): OperationDefinitionResponse {
  return {
    operationId: 'quality.freeze',
    category: 'quality',
    displayName: 'Freeze verify',
    description: 'Executa verificacao de freeze',
    requiredCapability: 'quality.execute.full',
    riskLevel: 'medium',
    requiresConfirmation: true,
    requiresApproval: false,
    environments: ['local', 'ci'],
    inputs: [
      { name: 'ref', description: 'Git ref', required: true, defaultValue: 'main' },
      { name: 'planHash', description: 'Hash do plano', required: true, defaultValue: 'abc123' },
    ],
    workflow: 'freeze',
    confirmationTemplate: 'RUN {environment} {planHash}',
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
    requestedByRoles: [],
    requestedByCapabilities: [],
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

describe('OperationLauncher', () => {
  beforeEach(() => {
    start.mockReset();
  });

  it('requires exact confirmation and submits the resolved operation request', async () => {
    start.mockResolvedValue(operation());
    render(<OperationLauncher definition={definition()} />);

    const submit = screen.getByRole('button', { name: /Registar pedido de operação/i });
    expect(submit).toBeDisabled();
    fireEvent.change(screen.getByLabelText(/Confirmação exata/i), { target: { value: 'RUN local abc123' } });
    expect(submit).not.toBeDisabled();

    fireEvent.click(submit);

    await waitFor(() => expect(start).toHaveBeenCalledTimes(1));
    expect(start).toHaveBeenCalledWith({
      operationId: 'quality.freeze',
      environment: 'local',
      ref: 'main',
      inputs: { ref: 'main', planHash: 'abc123' },
      collectEvidence: true,
      confirmation: 'RUN local abc123',
    });
    expect(await screen.findByText(/Pedido op-1 registado com estado Queued/i)).toBeInTheDocument();
  });

  it('fails closed when the operation is unauthorized or unavailable', () => {
    render(
      <OperationLauncher
        definition={definition({
          authorized: false,
          availability: 'implemented',
          requiresConfirmation: false,
        })}
      />,
    );

    expect(screen.getByText(/Bloqueada: falta a capability quality.execute.full/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Registar pedido de operação/i })).toBeDisabled();
  });

  it('uses the explicit unavailable limitation and reports start failures', async () => {
    render(
      <OperationLauncher
        definition={definition({
          availability: 'temporarily-disabled',
          limitation: 'Freeze provider disabled for local rehearsal.',
          requiresConfirmation: false,
        })}
      />,
    );

    expect(screen.getByText('Freeze provider disabled for local rehearsal.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Registar pedido de operação/i })).toBeDisabled();
  });

  it('submits approval requests with selected environment, fallback ref and evidence opt-out', async () => {
    start.mockResolvedValue(operation({ id: 'op-approval', status: 'AwaitingApproval', requiresApproval: true }));
    render(
      <OperationLauncher
        showTruthWarning={false}
        definition={definition({
          requiresApproval: true,
          inputs: [{ name: 'planHash', description: 'Hash do plano', required: true, defaultValue: null }],
        })}
      />,
    );

    expect(screen.queryByText(/Queued não significa sucesso/i)).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Ambiente'), { target: { value: 'ci' } });
    expect(screen.getByText('RUN ci <missing-plan-hash>')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/planHash/i), { target: { value: 'def456' } });
    fireEvent.click(screen.getByLabelText(/Recolher evidence/i));
    fireEvent.change(screen.getByLabelText(/Confirmação exata/i), { target: { value: 'RUN ci def456' } });

    fireEvent.click(screen.getByRole('button', { name: /Registar pedido de aprovação/i }));

    await waitFor(() => expect(start).toHaveBeenCalledTimes(1));
    expect(start).toHaveBeenCalledWith({
      operationId: 'quality.freeze',
      environment: 'ci',
      ref: 'master',
      inputs: { planHash: 'def456' },
      collectEvidence: false,
      confirmation: 'RUN ci def456',
    });
    expect(await screen.findByText(/Pedido op-approval registado com estado AwaitingApproval/i)).toBeInTheDocument();
  });

  it('reports non-error provider failures with the generic closed message', async () => {
    start.mockRejectedValue('provider refused the request');
    render(
      <OperationLauncher
        definition={definition({
          requiresConfirmation: false,
        })}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: /Registar pedido de operação/i }));

    expect(await screen.findByText('Não foi possível registar o pedido de operação.')).toBeInTheDocument();
  });
});
