import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ExportActions } from './ExportActions';

describe('ExportActions', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } });
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:export');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('copies content and resets the copied label after the timer', async () => {
    render(<ExportActions filename="evidence.csv" content="a,b" />);

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /Copiar/i }));
      await Promise.resolve();
    });

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('a,b');
    expect(screen.getByRole('button', { name: /Copiado/i })).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(1500);
    });
    expect(screen.getByRole('button', { name: /Copiar/i })).toBeInTheDocument();
  });

  it('downloads content through a temporary object URL', () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    render(<ExportActions filename="evidence.csv" content="a,b" contentType="text/plain" />);
    fireEvent.click(screen.getByRole('button', { name: /Exportar/i }));

    expect(URL.createObjectURL).toHaveBeenCalled();
    expect(click).toHaveBeenCalled();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:export');
  });
});
