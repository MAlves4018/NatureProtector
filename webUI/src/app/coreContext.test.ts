import { describe, expect, it } from 'vitest';
import type {
  AreaResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  ScenarioResponse,
} from './types';
import { buildUiRunContext, buildUiScenarioContext, buildUiSimulationReview, resolveUiArea } from './coreContext';

const area: AreaResponse = {
  id: 'area-1',
  code: 'proenca-a-nova',
  name: 'Proenca-a-Nova',
  countryCode: 'PT',
  configurationVersionNumber: 1,
  gridCellCount: 2,
  sensorNodeCount: 2,
  scenarioCount: 2,
};

const scenario: ScenarioResponse = {
  id: 'scenario-1',
  code: 'scenario_b',
  name: 'Scenario B',
  scenarioKind: 'HighRisk',
  configurationVersionNumber: 1,
  description: 'High-risk context',
  baseScenarioCode: 'scenario_a',
  datasetBindingCount: 2,
};

describe('core context adapters', () => {
  it('distinguishes not-selected, resolved and not-found area states', () => {
    expect(resolveUiArea(null, [area], 'en').selectionStatus).toBe('not-selected');

    const resolved = resolveUiArea('proenca-a-nova', [area], 'en');
    expect(resolved.selectionStatus).toBe('resolved');
    expect(resolved.requestedArea).toBe('proenca-a-nova');
    expect(resolved.resolvedArea?.code).toBe('proenca-a-nova');

    const missing = resolveUiArea('missing-area', [area], 'en');
    expect(missing.selectionStatus).toBe('not-found');
    expect(missing.resolvedArea).toBeNull();
  });

  it('reports area loading, invalid, unavailable and error states without guessing a match', () => {
    expect(resolveUiArea('proenca-a-nova', [area], 'en', true).selectionStatus).toBe('resolving');
    expect(resolveUiArea('bad area!', [area], 'en').selectionStatus).toBe('invalid');
    expect(resolveUiArea('proenca-a-nova', [], 'en').selectionStatus).toBe('unavailable');

    const failed = resolveUiArea('proenca-a-nova', [area], 'en', false, new Error('areas unavailable'));
    expect(failed.selectionStatus).toBe('error');
    expect(failed.resolutionReason).toBe('areas unavailable');
  });

  it('maps scenario availability without inventing missing scenarios', () => {
    const available = buildUiScenarioContext('scenario_b', [scenario], 'en');
    expect(available.availability).toBe('available');
    expect(available.resolvedScenarioId).toBe('scenario_b');

    const missing = buildUiScenarioContext('scenario_c', [scenario], 'en');
    expect(missing.availability).toBe('not-found');
    expect(missing.scenario).toBeNull();
  });

  it('maps scenario empty and error states explicitly', () => {
    expect(buildUiScenarioContext('', [scenario], 'en').availability).toBe('not-selected');
    expect(buildUiScenarioContext('scenario_b', [], 'en').availability).toBe('unavailable');

    const failed = buildUiScenarioContext('scenario_b', [scenario], 'en', new Error('scenario endpoint failed'));
    expect(failed.availability).toBe('error');
    expect(failed.limitations).toEqual(['scenario endpoint failed']);
  });

  it('builds run context fields from run, audit, timings and override provenance', () => {
    const run = runFixture('run-1', 'Completed');
    const model = buildUiRunContext(
      {
        requestedRunId: 'run-1',
        selectedRun: run,
        audit: auditFixture(run),
        timings: timingFixture('run-1'),
      },
      'en',
    );

    expect(model.state).toBe('completed');
    expect(model.resolvedRunId).toBe('run-1');
    expect(model.fields.find((field) => field.label === 'Accepted/missing')?.value).toBe('27/3');
    expect(model.fields.find((field) => field.label === 'Timing')?.value).toBe('1235ms');
    expect(model.fields.find((field) => field.label === 'Requested config')?.value).toContain('sensorCount=4');
    expect(model.limitations).toEqual(['limited run sample']);
  });

  it.each([
    ['Started', 'running'],
    ['Running', 'running'],
    ['Failed', 'failed'],
    ['Rejected', 'blocked'],
    ['Cancelled', 'cancelled'],
    ['Mystery', 'unknown'],
  ] as const)('maps run status %s to %s', (status, expected) => {
    expect(buildUiRunContext({ selectedRun: runFixture('run-state', status) }, 'en').state).toBe(expected);
  });

  it('rejects run-scoped DataScope mismatches before rendering mixed evidence', () => {
    const run = runFixture('run-1', 'Completed');
    const model = buildUiRunContext(
      {
        requestedRunId: 'run-1',
        selectedRun: run,
        audit: {
          ...auditFixture(run),
          dataScope: {
            requestedRunId: 'run-1',
            resolvedRunId: 'other-run',
            dataRunId: 'run-1',
            observedAt: '2026-06-13T21:00:00Z',
            source: 'test',
            scope: 'run',
            limitations: [],
          },
        },
      },
      'en',
    );

    expect(model.state).toBe('error');
    expect(model.limitations[0]).toContain('audit.dataScope.resolvedRunId=other-run');
  });

  it('keeps missing and loading run selections distinct', () => {
    expect(buildUiRunContext({ requestedRunId: 'missing-run' }, 'en').state).toBe('not-found');
    expect(buildUiRunContext({ requestedRunId: 'pending-run', loading: true }, 'en').state).toBe('pending');
    expect(buildUiRunContext({ error: new Error('run endpoint failed') }, 'en').limitations).toEqual([
      'run endpoint failed',
    ]);
  });

  it('keeps requested and resolved simulation configuration separate', () => {
    const request: RuntimeRunStartRequest = {
      areaCode: 'proenca-a-nova',
      scenarioCode: 'scenario_b',
      sensorCount: 1,
      numberOfCycles: 5,
      intervalSeconds: 30,
      seed: 42,
      degradationProfile: 'none',
      collectEvidence: false,
      waitForCompletion: false,
      timeoutSeconds: 180,
      allowParallelRun: false,
      runLabel: 'm04-test',
      degradationProfiles: ['none'],
    };
    const response: RuntimeRunStartResponse = {
      requestId: 'request-1',
      orchestratorCorrelationId: 'corr-1',
      status: 'Validated',
      message: 'Validated only',
      requestedAtUtc: '2026-06-14T00:00:00Z',
      requested: {
        sensorCount: 1,
        numberOfCycles: 5,
        intervalSeconds: 30,
        seed: 42,
        degradationProfile: 'none',
        degradationProfiles: ['none'],
        orchestratorCorrelationId: 'corr-1',
      },
      run: null,
      warnings: ['launch disabled'],
      logDirectory: null,
      evidenceDirectory: null,
    };

    const review = buildUiSimulationReview(request, response, 'en');

    expect(review.resultStatus).toBe('Validated');
    expect(review.fields.find((field) => field.label === 'scenarioCode')?.requested).toBe('scenario_b');
    expect(review.fields.find((field) => field.label === 'scenarioCode')?.resolved).toBe('Not available');
    expect(review.warnings).toEqual(['launch disabled']);
  });

  it('marks defaulted simulation values when the runtime resolves a different configuration', () => {
    const request: RuntimeRunStartRequest = {
      areaCode: 'proenca-a-nova',
      scenarioCode: 'scenario_b',
      sensorCount: 1,
      numberOfCycles: 5,
      intervalSeconds: 30,
      seed: null,
      degradationProfile: 'none',
      collectEvidence: true,
      waitForCompletion: true,
      timeoutSeconds: 180,
      allowParallelRun: false,
      runLabel: null,
      degradationProfiles: ['none'],
    };
    const response: RuntimeRunStartResponse = {
      requestId: 'request-1',
      orchestratorCorrelationId: 'corr-1',
      status: 'Completed',
      message: 'Run completed',
      requestedAtUtc: '2026-06-14T00:00:00Z',
      requested: {
        sensorCount: 1,
        numberOfCycles: 5,
        intervalSeconds: 30,
        seed: null,
        degradationProfile: 'none',
        degradationProfiles: ['none'],
        orchestratorCorrelationId: 'corr-1',
      },
      run: {
        ...runFixture('run-1', 'Completed'),
        runOverrides: {
          requested: {
            sensorCount: 1,
            numberOfCycles: 5,
            intervalSeconds: 30,
            seed: null,
            degradationProfile: 'none',
            degradationProfiles: ['none'],
            orchestratorCorrelationId: 'corr-1',
          },
          resolved: {
            sensorCount: 4,
            numberOfCycles: 5,
            intervalSeconds: 60,
            seed: 42,
            degradationProfile: 'missing-readings',
            degradationProfiles: ['missing-readings'],
            orchestratorCorrelationId: 'corr-1',
          },
          selectedSensorNames: ['sensor-a'],
        },
      },
      warnings: [],
      logDirectory: 'logs',
      evidenceDirectory: 'evidence',
    };

    const review = buildUiSimulationReview(request, response, 'en');

    expect(review.fields.find((field) => field.label === 'sensorCount')?.state).toBe('defaulted');
    expect(review.fields.find((field) => field.label === 'intervalSeconds')?.resolved).toBe('60');
    expect(review.fields.find((field) => field.label === 'orchestratorCorrelationId')?.state).toBe('resolved');
  });
});

function runFixture(id: string, status: string): RuntimeRunSummaryResponse {
  return {
    id,
    areaCode: 'proenca-a-nova',
    scenarioCode: 'scenario_b',
    scenarioName: 'Scenario B',
    status,
    configurationVersionNumber: 1,
    createdAt: '2026-06-13T21:00:00Z',
    startedAt: '2026-06-13T21:01:00Z',
    endedAt: '2026-06-13T21:02:00Z',
    durationSeconds: 60,
    logicalStartTimestamp: '2020-09-13T11:00:00Z',
    intervalSeconds: 60,
    numberOfCycles: 5,
    executionSeed: 42,
    metadataJson: null,
    metadataJsonStatus: 'valid',
    orchestratorCorrelationId: 'corr-1',
    runOverrides: {
      requested: {
        sensorCount: 4,
        numberOfCycles: 5,
        intervalSeconds: 60,
        seed: 42,
        degradationProfile: 'none',
        degradationProfiles: ['none'],
        orchestratorCorrelationId: 'corr-1',
      },
      resolved: {
        sensorCount: 4,
        numberOfCycles: 5,
        intervalSeconds: 60,
        seed: 42,
        degradationProfile: 'none',
        degradationProfiles: ['none'],
        orchestratorCorrelationId: 'corr-1',
      },
      selectedSensorNames: ['sensor-a'],
    },
  };
}

function auditFixture(run: RuntimeRunSummaryResponse): RuntimeRunAuditResponse {
  return {
    run,
    expectedEvents: 30,
    acceptedReadings: 27,
    missingEvents: 3,
    rejected: 0,
    quarantined: 0,
    retryAttempts: 1,
    riskAssessments: 27,
    qualityFlagsSummary: [],
    eligibilitySummary: [],
    areaSnapshot: null,
    limitations: [{ code: 'limited', message: 'limited run sample' }],
    scoreComponents: null,
    indexComparison: null,
  };
}

function timingFixture(runId: string): RuntimeRunTimingSummaryResponse {
  return {
    simulationRunId: runId,
    runDurationMs: 1234.56,
    startedAt: '2026-06-13T21:00:00Z',
    endedAt: '2026-06-13T21:02:00Z',
    firstInboxReceivedAt: null,
    firstProcessingAttemptStartedAt: null,
    lastProcessingAttemptFinishedAt: null,
    firstRiskAssessmentCreatedAt: null,
    firstAlertTriggeredAt: null,
    timeToFirstInboxMs: null,
    timeToFirstProcessingAttemptMs: null,
    timeToFirstRiskAssessmentMs: null,
    timeToFirstAlertMs: null,
    attempts: {
      attemptCount: 0,
      successfulAttempts: 0,
      failedAttempts: 0,
      quarantinedAttempts: 0,
      minDurationMs: null,
      avgDurationMs: null,
      maxDurationMs: null,
      p50DurationMs: null,
      p95DurationMs: null,
      p99DurationMs: null,
    },
    stages: [],
    limitations: [],
  };
}
