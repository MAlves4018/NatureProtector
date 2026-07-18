import { describe, expect, it } from 'vitest';
import {
  executionStatusState,
  globalOperationalStatus,
  operationStatusMeaning,
  runPresentationState,
} from './truthfulPresentation';

describe('truthful presentation', () => {
  it('never presents negative, in-progress or unknown execution states as ready', () => {
    expect(executionStatusState('Blocked')).toBe('blocked');
    expect(executionStatusState('Failed')).toBe('blocked');
    expect(executionStatusState('TimedOut')).toBe('blocked');
    expect(executionStatusState('Queued')).toBe('partial');
    expect(executionStatusState('Started')).toBe('partial');
    expect(executionStatusState('UnexpectedFutureState')).toBe('unknown');
    expect(executionStatusState('Completed')).toBe('ready');
  });

  it('requires a non-empty all-healthy component set for aggregate healthy', () => {
    expect(globalOperationalStatus([])).toBe('Unknown');
    expect(globalOperationalStatus([{ status: 'Healthy' }, { status: 'Unknown' }])).toBe('Unknown');
    expect(globalOperationalStatus([{ status: 'Healthy' }, { status: 'Healthy' }])).toBe('Healthy');
    expect(globalOperationalStatus([{ status: 'Healthy' }, { status: 'Degraded' }])).toBe('Degraded');
    expect(globalOperationalStatus([{ status: 'Unhealthy' }])).toBe('Unhealthy');
  });

  it('describes queued and simulated operations without claiming completion', () => {
    expect(
      operationStatusMeaning({ status: 'Queued', provider: 'github-actions', evidenceLevel: 'NOT_PROVED' }),
    ).toContain('not proved');
    expect(
      operationStatusMeaning({ status: 'Queued', provider: 'simulation', evidenceLevel: 'DEMONSTRATION_ONLY' }),
    ).toContain('Simulated');
  });

  it('keeps SystemCompleted with unsettled accounting in consolidation', () => {
    expect(
      runPresentationState({
        status: 'SystemCompleted',
        expected: 12,
        accepted: 12,
        missing: 0,
        settled: false,
      }),
    ).toEqual({ label: 'A consolidar', state: 'partial' });
  });

  it('presents expected missing observations as completed with loss', () => {
    expect(
      runPresentationState({
        status: 'Completed',
        expected: 12,
        accepted: 9,
        missing: 3,
        settled: true,
      }),
    ).toEqual({ label: 'Concluída com perda prevista', state: 'partial' });
  });
});
