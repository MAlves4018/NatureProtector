import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Pagination } from './Pagination';

describe('Pagination', () => {
  it('does not render navigation for a single page', () => {
    render(<Pagination currentPage={1} totalPages={1} onPageChange={vi.fn()} />);

    expect(screen.queryByRole('navigation', { name: 'Paginacao' })).not.toBeInTheDocument();
  });

  it('disables previous on the first page and emits selected page changes', () => {
    const onPageChange = vi.fn();
    render(<Pagination currentPage={1} totalPages={5} onPageChange={onPageChange} />);

    const buttons = screen.getAllByRole('button');
    expect(buttons[0]).toBeDisabled();
    expect(buttons.at(-1)).not.toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: '2' }));
    fireEvent.click(buttons.at(-1)!);

    expect(onPageChange).toHaveBeenNthCalledWith(1, 2);
    expect(onPageChange).toHaveBeenNthCalledWith(2, 2);
  });

  it('renders ellipses around the active window and disables next on the last page', () => {
    const onPageChange = vi.fn();
    render(<Pagination currentPage={9} totalPages={10} onPageChange={onPageChange} />);

    expect(screen.getByText('...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '9' })).toHaveClass('ui-pagination-active');
    expect(screen.getAllByRole('button').at(-1)).not.toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: '10' }));
    expect(onPageChange).toHaveBeenCalledWith(10);
  });

  it('disables next on the last page', () => {
    render(<Pagination currentPage={10} totalPages={10} onPageChange={vi.fn()} />);

    expect(screen.getAllByRole('button').at(-1)).toBeDisabled();
  });
});
