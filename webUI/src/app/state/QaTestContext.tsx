import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { buildUiQaSuites, type UiQaSuite } from '../technicalSurfaces';

interface UiQaTestContextValue {
  qaSuites: UiQaSuite[];
}

const UiQaTestContext = createContext<UiQaTestContextValue | null>(null);

export function UiQaTestProvider({ children }: { children: ReactNode }) {
  const qaSuites = useMemo(() => buildUiQaSuites(), []);
  const value = useMemo(() => ({ qaSuites }), [qaSuites]);

  return <UiQaTestContext.Provider value={value}>{children}</UiQaTestContext.Provider>;
}

export function useUiQaTests() {
  const context = useContext(UiQaTestContext);
  if (!context) throw new Error('useUiQaTests must be used within UiQaTestProvider');
  return context;
}
