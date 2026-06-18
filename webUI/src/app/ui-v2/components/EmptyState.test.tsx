import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmptyState } from './EmptyState';

describe('EmptyState', () => {
  it('renders the empty title and optional detail without adding actions', () => {
    render(<EmptyState title="Sem evidence disponivel" detail="A resposta atual nao contem artefactos descarregaveis." />);

    expect(screen.getByRole('heading', { name: 'Sem evidence disponivel', level: 3 })).toBeInTheDocument();
    expect(screen.getByText('A resposta atual nao contem artefactos descarregaveis.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('omits the detail paragraph when no detail is provided', () => {
    const { container } = render(<EmptyState title="Sem resultados" />);

    expect(screen.getByRole('heading', { name: 'Sem resultados', level: 3 })).toBeInTheDocument();
    expect(container.querySelector('p')).toBeNull();
  });
});
