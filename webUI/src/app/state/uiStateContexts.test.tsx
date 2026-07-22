import { act, fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { UiAlertProvider, useUiAlerts } from './AlertContext';
import { UiToastProvider, useUiToast } from './ToastContext';

let resolvedAreaCode: string | null = 'PT-11';

vi.mock('./AreaContext', () => ({
  useUiArea: () => ({ resolvedAreaCode }),
}));

vi.mock('../services/api', () => ({
  api: {
    getAlerts: vi.fn(),
  },
}));

function AlertProbe() {
  const { activeAlerts, dismissAlert, error } = useUiAlerts();
  return (
    <div>
      {error && <p>{error}</p>}
      {activeAlerts.map((alert) => (
        <button key={alert.id} type="button" onClick={() => dismissAlert(alert.id)}>
          {alert.id}:{alert.severity}:{alert.triggeredAt}
        </button>
      ))}
    </div>
  );
}

function ToastProbe() {
  const { addToast } = useUiToast();
  return (
    <div>
      <button type="button" onClick={() => addToast({ severity: 'success', title: 'Saved', message: 'persisted' })}>
        add-success
      </button>
      <button type="button" onClick={() => addToast({ severity: 'error', title: 'Failed' })}>
        add-error
      </button>
    </div>
  );
}

describe('UI state contexts', () => {
  beforeEach(() => {
    vi.useRealTimers();
    resolvedAreaCode = 'PT-11';
    vi.mocked(api.getAlerts).mockReset();
  });

  it('loads run alerts, exposes the newest active alerts first and dismisses by id', async () => {
    vi.mocked(api.getAlerts).mockResolvedValue([
      {
        id: 'old',
        severity: 'Warning',
        message: 'old warning',
        triggeredAt: '2026-07-21T12:00:00Z',
      },
      {
        id: 'new',
        severity: 'Alarm',
        message: 'new alarm',
        triggeredAt: '2026-07-21T12:01:00Z',
      },
    ]);

    render(
      <UiAlertProvider>
        <AlertProbe />
      </UiAlertProvider>,
    );

    const newAlert = await screen.findByRole('button', { name: /new:Alarm/i });
    const oldAlert = screen.getByRole('button', { name: /old:Warning/i });
    expect(newAlert.compareDocumentPosition(oldAlert) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    fireEvent.click(newAlert);
    expect(screen.queryByRole('button', { name: /new:Alarm/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /old:Warning/i })).toBeInTheDocument();
  });

  it('clears alerts when no area is resolved and surfaces fetch failures', async () => {
    vi.mocked(api.getAlerts).mockRejectedValue(new Error('offline'));

    render(
      <UiAlertProvider>
        <AlertProbe />
      </UiAlertProvider>,
    );

    expect(await screen.findByText('Failed to fetch alerts')).toBeInTheDocument();
    resolvedAreaCode = null;
  });

  it('adds, removes and auto-dismisses toast notifications', async () => {
    vi.useFakeTimers();

    render(
      <UiToastProvider>
        <ToastProbe />
      </UiToastProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'add-success' }));
    fireEvent.click(screen.getByRole('button', { name: 'add-error' }));
    expect(screen.getByText('Saved')).toBeInTheDocument();
    expect(screen.getByText('persisted')).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button').find((button) => button.className === 'ui-alert-dismiss')!);
    expect(screen.queryByText('Saved')).not.toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();

    await act(async () => {
      vi.advanceTimersByTime(5000);
    });
    expect(screen.queryByText('Failed')).not.toBeInTheDocument();
    vi.useRealTimers();
  });
});
