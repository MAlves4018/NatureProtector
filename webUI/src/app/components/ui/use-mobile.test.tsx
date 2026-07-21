import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useIsMobile } from './use-mobile';

type Listener = () => void;

describe('useIsMobile', () => {
  let listeners: Listener[];

  beforeEach(() => {
    listeners = [];
    Object.defineProperty(window, 'innerWidth', { value: 1024, configurable: true });
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      media: '(max-width: 767px)',
      addEventListener: (_event: string, listener: Listener) => listeners.push(listener),
      removeEventListener: (_event: string, listener: Listener) => {
        listeners = listeners.filter((candidate) => candidate !== listener);
      },
    }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('resolves initial mobile state from viewport width and reacts to media changes', () => {
    const { result, unmount } = renderHook(() => useIsMobile());

    expect(result.current).toBe(false);

    act(() => {
      Object.defineProperty(window, 'innerWidth', { value: 640, configurable: true });
      listeners.forEach((listener) => listener());
    });
    expect(result.current).toBe(true);

    unmount();
    expect(listeners).toHaveLength(0);
  });
});
