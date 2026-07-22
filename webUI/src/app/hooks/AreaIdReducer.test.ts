import { describe, expect, it } from 'vitest';
import { areaReducer, initialAreaState } from './AreaIdReducer';

describe('areaReducer', () => {
  it('tracks the selected area code without mutating the previous state', () => {
    const next = areaReducer(initialAreaState, { type: 'SET_CODE', payload: 'PT-11' });

    expect(next).toEqual({ ...initialAreaState, code: 'PT-11' });
    expect(initialAreaState.code).toBeNull();
  });

  it('keeps independent flags and messages when each action updates one field', () => {
    const withCode = areaReducer(initialAreaState, { type: 'SET_CODE', payload: 'PT-11' });
    const awaitingResolved = areaReducer(withCode, { type: 'AWAITING_INPUT', payload: false });
    const removing = areaReducer(awaitingResolved, { type: 'REMOVE_ID', payload: true });
    const withMessage = areaReducer(removing, {
      type: 'SET_MESSAGE',
      payload: { text: 'Area removida', type: 'success' },
    });

    expect(withMessage).toEqual({
      code: 'PT-11',
      awating: false,
      removingId: true,
      message: { text: 'Area removida', type: 'success' },
    });
  });

  it('returns the same state object for an unknown action to preserve reducer compatibility', () => {
    const state = { ...initialAreaState, code: 'PT-11' };
    const next = areaReducer(state, { type: 'UNKNOWN' } as never);

    expect(next).toBe(state);
  });
});
