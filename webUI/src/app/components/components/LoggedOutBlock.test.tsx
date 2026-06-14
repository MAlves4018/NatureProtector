import { render, screen } from '@testing-library/react';
import axe from 'axe-core';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { TokenProvider } from '../../context/TokenContext';
import { LoggedOutBlock } from './LoggedOutBlock';

function renderLoggedOutBlock() {
  return render(
    <MemoryRouter>
      <TokenProvider>
        <LoggedOutBlock isDark={false} message="Authentication is required for runtime controls." />
      </TokenProvider>
    </MemoryRouter>,
  );
}

describe('LoggedOutBlock', () => {
  it('renders the signed-out state and message', async () => {
    renderLoggedOutBlock();

    expect(await screen.findByText('Sign In Required')).toBeInTheDocument();
    expect(screen.getByText('Authentication is required for runtime controls.')).toBeInTheDocument();
  });

  it('passes a basic axe accessibility scan', async () => {
    const { container } = renderLoggedOutBlock();

    await screen.findByText('Sign In Required');
    const result = await axe.run(container, {
      rules: {
        'color-contrast': { enabled: false },
      },
    });

    expect(result.violations).toEqual([]);
  });
});
