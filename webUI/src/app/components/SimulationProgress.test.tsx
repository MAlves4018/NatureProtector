import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SimulationProgress, type SimulationProgressStep } from './SimulationProgress';

describe('SimulationProgress', () => {
  it('renders observed steps in supplied order without inventing local progress', () => {
    const steps: SimulationProgressStep[] = [
      { id: 'publish', label: 'Published', state: 'completed', detail: '6 readings' },
      { id: 'assess', label: 'Assessment', state: 'running', detail: null },
      { id: 'alert', label: 'Alert', state: 'pending' },
      { id: 'settle', label: 'Settlement', state: 'failed', detail: 'timeout' },
    ];

    render(<SimulationProgress steps={steps} />);

    const list = screen.getByRole('list', { name: 'Observed simulation progress' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(4);
    expect(items.map((item) => item.textContent)).toEqual([
      'Publishedcompleted6 readings',
      'Assessmentrunning',
      'Alertpending',
      'Settlementfailedtimeout',
    ]);
  });

  it('renders an empty observed timeline when the backend has no steps yet', () => {
    render(<SimulationProgress steps={[]} />);

    expect(screen.getByRole('list', { name: 'Observed simulation progress' })).toBeEmptyDOMElement();
  });
});
