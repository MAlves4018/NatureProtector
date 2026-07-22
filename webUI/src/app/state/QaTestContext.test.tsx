import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { UiQaTestProvider, useUiQaTests } from './QaTestContext';

vi.mock('../services/api', () => ({
  api: {
    listQualitySuites: vi.fn().mockResolvedValue([
      {
        operationId: 'm05-final-gates',
        displayName: 'M05 Final Gates',
        category: 'quality',
        description: 'Final validation gates',
        environments: ['local'],
        authorized: true,
        availability: 'implemented',
        riskLevel: 'Low',
        requiredCapability: 'QaOperator',
        evidenceLevel: 'test-results',
        title: 'M05 Final Gates',
        limitation: null,
      },
    ]),
    listQualityRuns: vi.fn().mockResolvedValue([]),
  },
}));

function QaProbe() {
  const { qaSuites } = useUiQaTests();
  return <p>suites:{qaSuites.map((suite) => suite.suiteId).join(',')}</p>;
}

function BrokenProbe() {
  useUiQaTests();
  return null;
}

describe('QaTestContext', () => {
  it('exposes documented QA suites from the provider authority', async () => {
    render(
      <UiQaTestProvider>
        <QaProbe />
      </UiQaTestProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/suites:/)).toHaveTextContent('m05-final-gates');
    });
  });

  it('fails closed when the QA suite hook is used outside its provider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() => render(<BrokenProbe />)).toThrow('useUiQaTests must be used within UiQaTestProvider');

    consoleError.mockRestore();
  });
});
