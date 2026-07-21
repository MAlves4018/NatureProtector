import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AlertBanner } from './AlertBanner';

const dismissAlert = vi.fn();
let activeAlerts: { id: string; alertCode: string; message: string; severity: string }[] = [];

vi.mock('../state/AlertContext', () => ({
  useUiAlerts: () => ({ activeAlerts, dismissAlert }),
}));

describe('AlertBanner', () => {
  it('does not render when no active alerts exist', () => {
    activeAlerts = [];

    render(<AlertBanner />);

    expect(screen.queryByRole('region', { name: 'Alertas ativos' })).not.toBeInTheDocument();
  });

  it('renders pluralized alerts and dismisses by alert id', () => {
    dismissAlert.mockReset();
    activeAlerts = [
      { id: 'a1', alertCode: 'WARN-1', message: 'Warning observed', severity: 'warning' },
      { id: 'a2', alertCode: 'CRIT-1', message: 'Critical observed', severity: 'critical' },
      { id: 'a3', alertCode: 'INFO-1', message: 'Unknown severity defaults to info', severity: 'unknown' },
    ];

    render(<AlertBanner />);

    expect(screen.getByText('3 alertas ativos')).toBeInTheDocument();
    expect(screen.getByText('WARN-1')).toBeInTheDocument();
    expect(screen.getByText('Critical observed')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Dismissir alerta: CRIT-1' }));
    expect(dismissAlert).toHaveBeenCalledWith('a2');
  });
});
