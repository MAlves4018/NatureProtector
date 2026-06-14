import { describe, expect, it } from 'vitest';
import type { AreaResponse, RuntimeRunStartRequest, RuntimeRunStartResponse, ScenarioResponse } from '../types';
import {
  buildUiV2ScenarioContext,
  buildUiV2SimulationReview,
  resolveUiV2Area,
} from './coreContext';

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

describe('UI v2 core context adapters', () => {
  it('distinguishes not-selected, resolved and not-found area states', () => {
    expect(resolveUiV2Area(null, [area], 'en').selectionStatus).toBe('not-selected');

    const resolved = resolveUiV2Area('proenca-a-nova', [area], 'en');
    expect(resolved.selectionStatus).toBe('resolved');
    expect(resolved.requestedArea).toBe('proenca-a-nova');
    expect(resolved.resolvedArea?.code).toBe('proenca-a-nova');

    const missing = resolveUiV2Area('missing-area', [area], 'en');
    expect(missing.selectionStatus).toBe('not-found');
    expect(missing.resolvedArea).toBeNull();
  });

  it('maps scenario availability without inventing missing scenarios', () => {
    const available = buildUiV2ScenarioContext('scenario_b', [scenario], 'en');
    expect(available.availability).toBe('available');
    expect(available.resolvedScenarioId).toBe('scenario_b');

    const missing = buildUiV2ScenarioContext('scenario_c', [scenario], 'en');
    expect(missing.availability).toBe('not-found');
    expect(missing.scenario).toBeNull();
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

    const review = buildUiV2SimulationReview(request, response, 'en');

    expect(review.resultStatus).toBe('Validated');
    expect(review.fields.find(field => field.label === 'scenarioCode')?.requested).toBe('scenario_b');
    expect(review.fields.find(field => field.label === 'scenarioCode')?.resolved).toBe('Not available');
    expect(review.warnings).toEqual(['launch disabled']);
  });
});
