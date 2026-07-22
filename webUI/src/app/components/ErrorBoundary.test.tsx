import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorBoundary } from './ErrorBoundary';

function Broken({ shouldThrow }: { shouldThrow: boolean }) {
  if (shouldThrow) throw new Error('render failed');
  return <p>Recovered child</p>;
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  it('renders a custom fallback when supplied', () => {
    render(
      <ErrorBoundary fallback={<p>Fallback controlado</p>}>
        <Broken shouldThrow />
      </ErrorBoundary>,
    );

    expect(screen.getByText('Fallback controlado')).toBeInTheDocument();
  });

  it('shows the default failure message and retries rendering children', () => {
    const view = render(
      <ErrorBoundary>
        <Broken shouldThrow />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('heading', { name: 'Algo correu mal', level: 3 })).toBeInTheDocument();
    expect(screen.getByText('render failed')).toBeInTheDocument();

    view.rerender(
      <ErrorBoundary>
        <Broken shouldThrow={false} />
      </ErrorBoundary>,
    );
    fireEvent.click(screen.getByRole('button', { name: /Tentar novamente/i }));

    expect(screen.getByText('Recovered child')).toBeInTheDocument();
  });
});
