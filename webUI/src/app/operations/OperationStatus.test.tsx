import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { EngineeringOperationResponse } from '../types/operations';
import { OperationStatus } from './OperationStatus';

function operation(overrides: Partial<EngineeringOperationResponse> = {}): EngineeringOperationResponse {
  return {
    id: 'op-1',
    operationId: 'quality.freeze',
    category: 'quality',
    displayName: 'Freeze verify',
    status: 'succeeded',
    environment: 'local',
    ref: 'main',
    requestedBy: 'miguel',
    requestedByRoles: ['Admin'],
    requestedByCapabilities: ['quality:run'],
    requestedAt: '2026-07-21T10:00:00.000Z',
    updatedAt: '2026-07-21T10:00:01.250Z',
    collectEvidence: true,
    riskLevel: 'medium',
    requiresApproval: false,
    provider: 'github-actions',
    providerReference: 'https://example.test/run/1',
    workflow: 'freeze',
    planHash: 'abc',
    evidenceLevel: 'PROVED_LOCAL',
    inputs: {},
    steps: [
      { sequence: 1, name: 'Plan', status: 'succeeded', at: '2026-07-21T10:00:00.100Z', detail: 'clean' },
      { sequence: 2, name: 'Verify', status: 'succeeded', at: '2026-07-21T10:00:01.250Z', detail: null },
    ],
    artifacts: [
      {
        artifactId: 'a1',
        name: 'summary',
        kind: 'text',
        reference: 'summary.md',
        sha256: null,
        sizeBytes: 42,
        evidenceLevel: 'PROVED_LOCAL',
      },
    ],
    approvals: [],
    limitations: [],
    detail: null,
    ...overrides,
  };
}

describe('OperationStatus', () => {
  it('renders provider evidence, observed duration, provider link and detailed steps', () => {
    render(<OperationStatus operation={operation()} />);

    expect(screen.getByRole('heading', { name: 'Freeze verify', level: 3 })).toBeInTheDocument();
    expect(screen.getByText('Terminal provider result with recorded proved evidence.')).toBeInTheDocument();
    expect(screen.getByText('1.3 s')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Abrir provider/i })).toHaveAttribute('href', 'https://example.test/run/1');
    expect(screen.getByText('Plan')).toBeInTheDocument();
    expect(screen.getByText('clean')).toBeInTheDocument();
  });

  it('keeps compact mode from rendering the timeline and reports undispatched providers truthfully', () => {
    render(
      <OperationStatus
        compact
        operation={operation({
          status: 'queued',
          provider: null,
          providerReference: null,
          evidenceLevel: 'CONTROL_PLANE_ONLY',
          steps: [{ sequence: 1, name: 'Queued', status: 'queued', at: '2026-07-21T10:00:00.000Z', detail: null }],
          limitations: ['provider result not observed'],
        })}
      />,
    );

    expect(
      screen.getByText('Request recorded or accepted for dispatch; provider completion is not proved.'),
    ).toBeInTheDocument();
    expect(screen.getByText('não despachado')).toBeInTheDocument();
    expect(screen.getByText('provider result not observed')).toBeInTheDocument();
    expect(screen.queryByText('Queued')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Abrir provider/i })).not.toBeInTheDocument();
  });
});
