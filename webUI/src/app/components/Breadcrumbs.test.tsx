import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Breadcrumbs } from './Breadcrumbs';

describe('Breadcrumbs', () => {
  it('does not render an empty trail', () => {
    const { container } = render(<Breadcrumbs items={[]} onNavigate={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('navigates home and intermediate targets while keeping the last item current', () => {
    const onNavigate = vi.fn();
    render(
      <Breadcrumbs
        items={[
          { label: 'Operações', target: 'operations' },
          { label: 'Runtime', target: 'runs' },
          { label: 'Run 001', target: 'runs' },
        ]}
        onNavigate={onNavigate}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Início' }));
    fireEvent.click(screen.getByRole('button', { name: 'Operações' }));
    fireEvent.click(screen.getByRole('button', { name: 'Runtime' }));

    expect(onNavigate).toHaveBeenNthCalledWith(1, 'demo');
    expect(onNavigate).toHaveBeenNthCalledWith(2, 'operations');
    expect(onNavigate).toHaveBeenNthCalledWith(3, 'runs');
    expect(screen.getByText('Run 001')).toHaveClass('ui-breadcrumb-current');
  });
});
