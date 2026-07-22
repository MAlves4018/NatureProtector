import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { createUiRuntimeSummaryFixture } from '../fixtures';
import type { RuntimeRunAuditResponse } from '../types';
import { RunScientificMetrics } from './RunScientificMetrics';

describe('RunScientificMetrics', () => {
  it('renders unavailable placeholders when no run is selected', () => {
    render(<RunScientificMetrics audit={null} />);

    expect(screen.getByText('SimulationRunId: não selecionada')).toBeInTheDocument();
    expect(screen.getAllByText('Indisponível para esta run').length).toBeGreaterThan(5);
  });

  it('renders persisted scientific metrics, coverage and limitations from the run audit', () => {
    const audit = auditFixture({
      expectedEvents: 40,
      acceptedReadings: 30,
      eligibilitySummary: [
        { status: 'Eligible', count: 24 },
        { status: 'BlockedMissingData', count: 6 },
      ],
    });

    render(<RunScientificMetrics audit={audit} />);

    expect(screen.getByText('SimulationRunId: audit-run')).toBeInTheDocument();
    expect(metricCard('NP Score')).toHaveTextContent('0.780');
    expect(metricCard('FWI')).toHaveTextContent('17.070');
    expect(metricCard('KBDI')).toHaveTextContent('16.560');
    expect(metricCard('Portuguese Proxy')).toHaveTextContent('Elevado');

    const details = screen.getByText('Ver proveniência, timestamps e todas as métricas').closest('details')!;
    expect(within(details).getByText('75.0%')).toBeInTheDocument();
    expect(within(details).getByText('24')).toBeInTheDocument();
    expect(within(details).getByText('6')).toBeInTheDocument();
    expect(screen.getByText(/Limited antecedent history/)).toBeInTheDocument();
  });

  it('falls back to raw classes and unavailable notes when optional persisted fields are absent', () => {
    const audit = auditFixture({
      expectedEvents: 0,
      scoreComponents: {
        ...createUiRuntimeSummaryFixture().scoreComponents!,
        npRiskClassLabel: null,
        npRiskClass: 'Moderate',
        calculationStatus: null,
        latestAssessmentTimestamp: null,
      },
      indexComparison: {
        ...createUiRuntimeSummaryFixture().indexComparison!,
        portugueseContextRiskProxyLabel: null,
        portugueseContextRiskProxyClass: 'High',
        provenance: null,
        limitations: null,
        logicalDate: null,
      },
      eligibilitySummary: [],
    });

    render(<RunScientificMetrics audit={audit} />);

    expect(metricCard('Portuguese Proxy')).toHaveTextContent('High');

    const details = screen.getByText('Ver proveniência, timestamps e todas as métricas').closest('details')!;
    expect(within(details).getByText('Moderate')).toBeInTheDocument();
    expect(within(details).getAllByText('Origem não registada').length).toBeGreaterThan(0);
    expect(within(details).getAllByText('Indisponível para esta run').length).toBeGreaterThan(0);
  });
});

function metricCard(label: string) {
  return screen.getAllByText(label)[0].closest('article')!;
}

function auditFixture(overrides: Partial<RuntimeRunAuditResponse> = {}): RuntimeRunAuditResponse {
  const summary = createUiRuntimeSummaryFixture();
  return {
    run: { ...summary.latestRun!, id: 'audit-run' },
    expectedEvents: 30,
    acceptedReadings: 30,
    missingEvents: 0,
    rejected: 0,
    quarantined: 0,
    retryAttempts: 0,
    riskAssessments: 30,
    qualityFlagsSummary: [],
    eligibilitySummary: [],
    areaSnapshot: null,
    limitations: [],
    scoreComponents: summary.scoreComponents,
    indexComparison: summary.indexComparison,
    ...overrides,
  };
}
