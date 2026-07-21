import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmDialog } from './ConfirmDialog';

describe('ConfirmDialog', () => {
  it('does not render dialog content while closed', () => {
    render(
      <ConfirmDialog open={false} title="Apagar run" message="Irreversivel" onConfirm={vi.fn()} onCancel={vi.fn()} />,
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('focuses the confirm action and dispatches confirm and cancel callbacks', async () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    render(
      <ConfirmDialog
        open
        title="Apagar run"
        message="Esta acao remove apenas dados locais desta run."
        confirmLabel="Apagar"
        cancelLabel="Voltar"
        variant="danger"
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    );

    const dialog = screen.getByRole('dialog', { name: 'Apagar run' });
    expect(dialog).toHaveTextContent('Esta acao remove apenas dados locais desta run.');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Apagar' })).toHaveFocus());

    fireEvent.click(screen.getByRole('button', { name: 'Apagar' }));
    fireEvent.click(screen.getByRole('button', { name: 'Voltar' }));
    fireEvent.click(screen.getByRole('button', { name: 'Fechar' }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).toHaveBeenCalledTimes(2);
  });

  it('cancels from Escape only while open', () => {
    const onCancel = vi.fn();
    const { rerender } = render(
      <ConfirmDialog open title="Confirmar" message="Mensagem" onConfirm={vi.fn()} onCancel={onCancel} />,
    );

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onCancel).toHaveBeenCalledTimes(1);

    rerender(<ConfirmDialog open={false} title="Confirmar" message="Mensagem" onConfirm={vi.fn()} onCancel={onCancel} />);
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
