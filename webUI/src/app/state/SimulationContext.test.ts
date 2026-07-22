import { describe, expect, it } from 'vitest';
import { DEGRADATION_PROFILE_OPTIONS } from '../content/technicalLabels';
import {
  SIMULATION_FORM_STORAGE_KEY,
  buildSimulationRequest,
  hydrateSimulationForm,
  initialSimulationForm,
  minimumSynchronousWaitSeconds,
  normalizeDegradationProfiles,
  persistSimulationForm,
  toggleDegradationProfile,
} from './SimulationContext';
import { executionStatusState } from '../truthfulPresentation';

const LEGACY_SIMULATION_FORM_STORAGE_KEY = 'natureprotector.ui.simulationForm.v1';
const CANONICAL_DEGRADATION_PROFILES = [
  'none',
  'missing-readings',
  'noise',
  'bias',
  'drift',
  'stuck-value',
  'outlier',
  'clipping/range',
  'lag/delay',
  'duplicate',
  'out-of-order',
] as const;

describe('simulation context degradation profiles', () => {
  it.each([
    ['LaunchAccepted', 'partial'],
    ['RunObserved', 'partial'],
    ['PipelineSettling', 'partial'],
    ['SystemCompleted', 'ready'],
    ['Failed', 'blocked'],
    ['TimedOut', 'blocked'],
    ['Orphaned', 'blocked'],
  ])('presents persisted operation state %s truthfully', (state, expected) => {
    expect(executionStatusState(state)).toBe(expected);
  });

  it('exposes the backend canonical degradation profiles to the UI', () => {
    expect(DEGRADATION_PROFILE_OPTIONS).toEqual(CANONICAL_DEGRADATION_PROFILES);
    expect(DEGRADATION_PROFILE_OPTIONS).not.toEqual(
      expect.arrayContaining([
        'sensor-failure-random',
        'sensor-failure-clustered',
        'communication-loss',
        'power-degradation',
      ]),
    );
  });

  it('sends clean runs without degradation profile overrides', () => {
    const request = buildSimulationRequest('proenca-a-nova', 'scenario_b', {
      ...initialSimulationForm,
      degradationProfiles: [],
    });

    expect(request.degradationProfile).toBe('none');
    expect(request.degradationProfiles).toEqual(['none']);
  });

  it('keeps legacy and plural degradation payload fields aligned for degraded runs', () => {
    const request = buildSimulationRequest('proenca-a-nova', 'scenario_c', {
      ...initialSimulationForm,
      degradationProfiles: ['missing-readings'],
    });

    expect(request.degradationProfile).toBe('missing-readings');
    expect(request.degradationProfiles).toEqual(['missing-readings']);
  });

  it('uses the first real profile as legacy fallback while preserving the plural payload', () => {
    const request = buildSimulationRequest('proenca-a-nova', 'scenario_c', {
      ...initialSimulationForm,
      degradationProfiles: ['missing-readings', 'noise'],
    });

    expect(request.degradationProfile).toBe('missing-readings');
    expect(request.degradationProfiles).toEqual(['missing-readings', 'noise']);
  });

  it('keeps none mutually exclusive with real degradation profiles', () => {
    expect(toggleDegradationProfile(['missing-readings', 'noise'], 'none', true)).toEqual([]);
    expect(toggleDegradationProfile([], 'missing-readings', true)).toEqual(['missing-readings']);
    expect(normalizeDegradationProfiles(['none', 'missing-readings', 'noise'])).toEqual(['missing-readings', 'noise']);
  });

  it('hydrates valid v2 persisted simulation form values', () => {
    const storage = memoryStorage({
      [SIMULATION_FORM_STORAGE_KEY]: JSON.stringify({
        schemaVersion: 2,
        sensorCount: 6,
        numberOfCycles: 5,
        intervalSeconds: 5,
        seed: '12345',
        degradationProfiles: ['missing-readings', 'noise'],
        runLabel: 'persisted-run',
        waitForCompletion: true,
        collectEvidence: true,
        allowParallelRun: true,
        waitTimeoutSeconds: 180,
      }),
    });

    expect(hydrateSimulationForm(storage)).toEqual({
      sensorCount: 6,
      numberOfCycles: 5,
      intervalSeconds: 5,
      seed: '12345',
      degradationProfiles: ['missing-readings', 'noise'],
      runLabel: 'persisted-run',
      waitForCompletion: true,
      collectEvidence: true,
      allowParallelRun: true,
      waitTimeoutSeconds: 180,
    });
  });

  it('migrates v1 without restoring synchronous waiting or a 60 second timeout', () => {
    const storage = memoryStorage({
      [LEGACY_SIMULATION_FORM_STORAGE_KEY]: JSON.stringify({
        sensorCount: 6,
        numberOfCycles: 5,
        intervalSeconds: 5,
        seed: '12345',
        degradationProfiles: ['missing-readings'],
        runLabel: 'legacy-run',
        waitForCompletion: true,
        collectEvidence: true,
        allowParallelRun: false,
        timeoutSeconds: 60,
      }),
    });

    expect(hydrateSimulationForm(storage)).toMatchObject({
      waitForCompletion: false,
      waitTimeoutSeconds: 300,
    });
  });

  it('ignores invalid persisted form values and drops non-canonical profiles', () => {
    const storage = memoryStorage({
      [SIMULATION_FORM_STORAGE_KEY]: JSON.stringify({
        schemaVersion: 2,
        sensorCount: -1,
        numberOfCycles: 'bad',
        intervalSeconds: Number.NaN,
        seed: 12345,
        degradationProfiles: ['none', 'sensor-failure-random', 'missing-readings', 'power-degradation'],
        runLabel: null,
        waitForCompletion: 'true',
        collectEvidence: false,
        allowParallelRun: true,
        waitTimeoutSeconds: 0,
      }),
    });

    expect(hydrateSimulationForm(storage)).toEqual({
      ...initialSimulationForm,
      degradationProfiles: ['missing-readings'],
      collectEvidence: false,
      allowParallelRun: true,
    });
  });

  it('falls back to defaults for invalid persisted JSON', () => {
    const storage = memoryStorage({
      [SIMULATION_FORM_STORAGE_KEY]: '{not json',
    });

    expect(hydrateSimulationForm(storage)).toEqual(initialSimulationForm);
  });

  it('persists only the safe v2 simulation form fields', () => {
    const storage = memoryStorage();

    persistSimulationForm(
      {
        ...initialSimulationForm,
        sensorCount: 4,
        degradationProfiles: ['missing-readings', 'noise'],
      },
      storage,
    );

    expect(JSON.parse(storage.getItem(SIMULATION_FORM_STORAGE_KEY)!)).toEqual({
      schemaVersion: 2,
      sensorCount: 4,
      numberOfCycles: 3,
      intervalSeconds: 60,
      seed: '42',
      degradationProfiles: ['missing-readings', 'noise'],
      runLabel: 'ui-structural',
      waitForCompletion: false,
      collectEvidence: false,
      allowParallelRun: false,
      waitTimeoutSeconds: 300,
    });
  });

  it('sends the visible synchronous wait limit as timeoutSeconds', () => {
    const request = buildSimulationRequest('proenca-a-nova', 'scenario_b', {
      ...initialSimulationForm,
      waitForCompletion: true,
      waitTimeoutSeconds: 420,
    });

    expect(request.timeoutSeconds).toBe(1890);
    expect(minimumSynchronousWaitSeconds(initialSimulationForm)).toBe(210);
  });
});

function memoryStorage(initialValues: Record<string, string> = {}) {
  const values = new Map(Object.entries(initialValues));

  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key),
  };
}
