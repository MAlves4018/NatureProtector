import { describe, expect, it } from 'vitest';
import { buildUiRunContext } from './coreContext';
import { createUiRuntimeSummaryFixture } from './fixtures';
import { buildUiRiskReadModel } from './outputContext';
import type { RuntimeRunAuditResponse } from './types';

describe('R1M-002 DataScope contract', () => {
  it('rejects audit data from a run different from the requested run', () => {
    const fixture = createUiRuntimeSummaryFixture();
    const runA = { ...fixture.latestRun!, id: 'run-A' };
    const audit = {
      run: runA,
      expectedEvents: 1,
      acceptedReadings: 1,
      missingEvents: 0,
      rejected: 0,
      quarantined: 0,
      retryAttempts: 0,
      riskAssessments: 1,
      qualityFlagsSummary: [],
      eligibilitySummary: [],
      areaSnapshot: null,
      limitations: [],
      scoreComponents: null,
      indexComparison: null,
      dataScope: {
        requestedRunId: 'run-B',
        resolvedRunId: 'run-A',
        dataRunId: 'run-A',
        observedAt: '2026-07-13T00:00:00Z',
        source: 'contract',
        scope: 'RunScoped',
        limitations: [],
      },
    } satisfies RuntimeRunAuditResponse;

    const model = buildUiRunContext({ requestedRunId: 'run-B', selectedRun: { ...runA, id: 'run-B' }, audit }, 'en');

    expect(model.state).toBe('error');
    expect(model.resolvedRunId).toBeNull();
    expect(model.run).toBeNull();
  });

  it('does not label an area-current fallback score as belonging to the latest run', () => {
    const fixture = createUiRuntimeSummaryFixture({
      scoreComponents: null,
      areaOperationalState: {
        ...createUiRuntimeSummaryFixture().areaOperationalState!,
        aggregateRiskScore: 0.91,
      },
    });

    const model = buildUiRiskReadModel({ summary: fixture }, 'en');

    expect(model.canShowScore).toBe(false);
    expect(model.run).not.toBe(fixture.latestRun?.scenarioCode);
    expect(model.limitations.join(' ')).toMatch(/scope|run|provenance/i);
  });
});
