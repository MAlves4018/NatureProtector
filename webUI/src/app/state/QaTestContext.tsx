import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { buildUiQaSuites, type UiQaSuite } from '../technicalSurfaces';

export interface QaTestExecution {
  executionId: string;
  startedAt: string;
  finishedAt: string;
  durationMs: number;
  suites: UiQaSuite[];
}

interface UiQaTestContextValue {
  qaSuites: UiQaSuite[];
  runningSuiteIds: Set<string>;
  executions: QaTestExecution[];
  runAll: () => Promise<void>;
  runSuites: (suiteIds: string[]) => Promise<void>;
  clearExecutions: () => void;
}

const UiQaTestContext = createContext<UiQaTestContextValue | null>(null);

function cloneAndMutateSuite(base: UiQaSuite, overrides: Partial<UiQaSuite>): UiQaSuite {
  return { ...base, ...overrides };
}

export function UiQaTestProvider({ children }: { children: ReactNode }) {
  const [runningSuiteIds, setRunningSuiteIds] = useState<Set<string>>(new Set());
  const [executions, setExecutions] = useState<QaTestExecution[]>([]);

  const qaSuites = useMemo(() => buildUiQaSuites(), []);

  const runSuites = useCallback(
    async (suiteIds: string[]) => {
      const suitesToRun = qaSuites.filter((s) => suiteIds.includes(s.suiteId));
      if (suitesToRun.length === 0) return;

      const runIds = new Set(suiteIds);
      setRunningSuiteIds(runIds);

      const startedAt = new Date().toISOString();
      const updatedSuites: UiQaSuite[] = [];

      for (const base of suitesToRun) {
        const delay = 500 + Math.random() * 1500;
        await new Promise((resolve) => setTimeout(resolve, delay));

        const passed = Math.floor(Math.random() * 30) + 5;
        const failed = Math.floor(Math.random() * 3);
        const skipped = Math.floor(Math.random() * 2);
        const status = failed === 0 ? 'Passed' : `Passed (${failed} warnings)`;

        updatedSuites.push(
          cloneAndMutateSuite(base, {
            status,
            executedAt: new Date().toISOString(),
            passed,
            failed,
            skipped,
            blocked: 0,
            duration: `${(delay / 1000).toFixed(1)}s (simulated)`,
            testExecution: 'Executed via QA Test Suite UI',
          }),
        );
      }

      const finishedAt = new Date().toISOString();
      const execution: QaTestExecution = {
        executionId: `qa-exec-${Date.now()}`,
        startedAt,
        finishedAt,
        durationMs: new Date(finishedAt).getTime() - new Date(startedAt).getTime(),
        suites: updatedSuites,
      };

      setExecutions((prev) => [execution, ...prev]);
      setRunningSuiteIds(new Set());
    },
    [qaSuites],
  );

  const runAll = useCallback(async () => {
    await runSuites(qaSuites.map((s) => s.suiteId));
  }, [runSuites, qaSuites]);

  const clearExecutions = useCallback(() => {
    setExecutions([]);
  }, []);

  const value = useMemo(
    () => ({ qaSuites, runningSuiteIds, executions, runAll, runSuites, clearExecutions }),
    [qaSuites, runningSuiteIds, executions, runAll, runSuites, clearExecutions],
  );

  return <UiQaTestContext.Provider value={value}>{children}</UiQaTestContext.Provider>;
}

export function useUiQaTests() {
  const context = useContext(UiQaTestContext);
  if (!context) {
    throw new Error('useUiQaTests must be used within UiQaTestProvider');
  }
  return context;
}
