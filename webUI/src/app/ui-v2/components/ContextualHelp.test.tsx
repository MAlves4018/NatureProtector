import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ContextualHelp } from './ContextualHelp';

vi.mock('../state/UiV2Context', () => ({
  useUiV2: () => ({ locale: 'pt-PT' }),
}));

describe('ContextualHelp', () => {
  it('opens a popover from keyboard focus and closes it without navigation', async () => {
    render(<ContextualHelp topicId="evidence" />);

    const trigger = screen.getByRole('button', { name: /Evidencia: ajuda/i });
    await act(async () => {
      fireEvent.focus(trigger);
    });

    expect(screen.getByRole('note')).toBeInTheDocument();
    expect(screen.getByText('Evidencia')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Fechar' }));
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('traps dialog focus, closes from the focused control and restores focus to the trigger', async () => {
    render(<ContextualHelp topicId="qa" mode="dialog" />);

    const trigger = screen.getByRole('button', { name: /Qualidade: ajuda/i });
    await act(async () => {
      trigger.focus();
      fireEvent.focus(trigger);
    });

    const dialog = screen.getByRole('dialog', { name: 'Qualidade' });
    const close = screen.getByRole('button', { name: 'Fechar ajuda' });
    await waitFor(() => expect(close).toHaveFocus());

    fireEvent.keyDown(dialog, { key: 'Tab' });
    expect(close).toHaveFocus();

    fireEvent.click(close);
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Qualidade' })).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });
});
