// State
export interface AreaState {
  code: string | null;
  awating: boolean;
  removingId: boolean;
  message: { text: string; type: 'error' | 'success' } | null;
}

export const initialAreaState: AreaState = {
  code: null,
  awating: true,
  removingId: false,
  message: null,
};

// Actions
export type AreaAction =
  | { type: 'AWAITING_INPUT'; payload: boolean }
  | { type: 'SET_CODE'; payload: string | null }
  | { type: 'REMOVE_ID'; payload: boolean }
  | { type: 'SET_MESSAGE'; payload: { text: string; type: 'error' | 'success' } | null };

// Reducer
export function areaReducer(state: AreaState, action: AreaAction): AreaState {
  switch (action.type) {
    case 'AWAITING_INPUT':
      return { ...state, awating: action.payload };

    case 'SET_CODE':
      return { ...state, code: action.payload };

    case 'REMOVE_ID':
      return { ...state, removingId: action.payload };

    case 'SET_MESSAGE':
      return { ...state, message: action.payload };

    default:
      return state;
  }
}
