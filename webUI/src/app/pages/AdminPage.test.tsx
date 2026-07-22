import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminPage } from './AdminPage';

const mocks = vi.hoisted(() => ({
  resetRuntimeState: vi.fn(),
  adminActions: [
    {
      capability: 'simulation.execute',
      action: 'Start runtime simulation run',
      riskLevel: 'Medium',
      authorizationState: 'Backend allows Sim/Admin in Development',
      confirmationRequired: 'Review requested/resolved',
      auditAvailable: 'Run audit endpoint',
      availability: 'partial',
      limitations: [],
    },
    {
      capability: 'admin.execute',
      action: 'Runtime reset',
      riskLevel: 'High',
      authorizationState: 'Backend allows Sim/Admin in Development',
      confirmationRequired: 'Exact token',
      auditAvailable: 'Before/after counts',
      availability: 'partial',
      limitations: [],
    },
  ],
}));

vi.mock('../services/api', () => ({
  api: {
    resetRuntimeState: mocks.resetRuntimeState,
  },
}));

vi.mock('../state/useUiSurfaces', () => ({
  useAdminActions: () => mocks.adminActions,
}));

vi.mock('../state/LocaleContext', () => ({
  useUiLocale: () => ({
    copy: (key: string) =>
      ({
        'admin.title': 'Administration',
        'admin.subtitle': 'Governed runtime operations',
        'technical.adminAction': 'Action',
        'technical.authorization': 'Authorization',
        'technical.confirmation': 'Confirmation',
        'technical.audit': 'Audit',
        'technical.status': 'Status',
      })[key] ?? key,
  }),
}));

describe('AdminPage', () => {
  beforeEach(() => {
    mocks.resetRuntimeState.mockReset();
    mocks.adminActions[1].availability = 'partial';
  });

  it('renders governed admin actions and keeps reset disabled until the exact confirmation token is entered', () => {
    render(<AdminPage />);

    expect(screen.getByRole('heading', { name: 'Administration' })).toBeInTheDocument();
    expect(screen.getByText('Start runtime simulation run')).toBeInTheDocument();
    expect(screen.getAllByText('Runtime reset')).toHaveLength(2);
    expect(screen.getByRole('button', { name: /Executar dry-run/ })).toBeDisabled();

    fireEvent.change(screen.getByPlaceholderText('RESET_RUNTIME_STATE'), { target: { value: 'RESET_RUNTIME_STATE' } });

    expect(screen.getByRole('button', { name: /Executar dry-run/ })).toBeEnabled();
  });

  it('submits dry-run reset with exact scope and renders before/after accounting', async () => {
    mocks.resetRuntimeState.mockResolvedValue({
      generatedAtUtc: '2026-07-21T10:00:00Z',
      dryRun: true,
      status: 'Validated',
      message: 'Reset can proceed safely.',
      before: [
        { schema: 'pipeline', table: 'event_inbox', count: 3 },
        { schema: 'projection', table: 'risk_assessment_log', count: 2 },
      ],
      after: [
        { schema: 'pipeline', table: 'event_inbox', count: 0 },
        { schema: 'projection', table: 'risk_assessment_log', count: 0 },
      ],
    });

    render(<AdminPage />);

    fireEvent.change(screen.getByPlaceholderText('RESET_RUNTIME_STATE'), { target: { value: 'RESET_RUNTIME_STATE' } });
    fireEvent.click(screen.getByRole('button', { name: /Executar dry-run/ }));

    await waitFor(() =>
      expect(mocks.resetRuntimeState).toHaveBeenCalledWith({
        scope: 'runtime-only',
        confirm: 'RESET_RUNTIME_STATE',
        dryRun: true,
      }),
    );
    expect(await screen.findByText('Pré-visualização do reset')).toBeInTheDocument();
    expect(screen.getByText('Reset can proceed safely.')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('submits destructive reset only when dry-run is unchecked and reports backend rejection', async () => {
    mocks.resetRuntimeState.mockRejectedValue(new Error('active runtime operation blocks reset'));

    render(<AdminPage />);

    fireEvent.click(screen.getByLabelText(/Dry-run/));
    fireEvent.change(screen.getByPlaceholderText('RESET_RUNTIME_STATE'), { target: { value: 'RESET_RUNTIME_STATE' } });
    fireEvent.click(screen.getByRole('button', { name: /Executar reset/ }));

    await waitFor(() =>
      expect(mocks.resetRuntimeState).toHaveBeenCalledWith(
        expect.objectContaining({ dryRun: false, confirm: 'RESET_RUNTIME_STATE' }),
      ),
    );
    expect(await screen.findByText('active runtime operation blocks reset')).toBeInTheDocument();
  });

  it('hides reset form when runtime reset action is blocked', () => {
    mocks.adminActions[1].availability = 'blocked';

    render(<AdminPage />);

    expect(screen.getByText('Runtime reset')).toBeInTheDocument();
    expect(screen.queryByPlaceholderText('RESET_RUNTIME_STATE')).not.toBeInTheDocument();
  });
});
