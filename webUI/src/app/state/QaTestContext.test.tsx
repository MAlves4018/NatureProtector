import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { UiQaTestProvider, useUiQaTests } from './QaTestContext';

function QaProbe() {
  const { qaSuites } = useUiQaTests();
  return <p>suites:{qaSuites.map((suite) => suite.suiteId).join(',')}</p>;
}

function BrokenProbe() {
  useUiQaTests();
  return null;
}

describe('QaTestContext', () => {
  it('exposes documented QA suites from the provider authority', () => {
    render(
      <UiQaTestProvider>
        <QaProbe />
      </UiQaTestProvider>,
    );

    expect(screen.getByText(/suites:/)).toHaveTextContent('m05-final-gates');
  });

  it('fails closed when the QA suite hook is used outside its provider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() => render(<BrokenProbe />)).toThrow('useUiQaTests must be used within UiQaTestProvider');

    consoleError.mockRestore();
  });
});
