import { createContext, useCallback, useEffect, useRef, useContext, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { buildUiQaSuites, type UiQaSuite } from '../technicalSurfaces';
import type { EngineeringOperationResponse, OperationDefinitionResponse } from '../types';

export interface QaTestExecution {
  executionId: string;
  startedAt: string;
  finishedAt: string;
  durationMs: number;
  suites: UiQaSuite[];
}

interface UiQaTestContextValue {
  qaSuites: UiQaSuite[];
  suitesLoading: boolean;
  runningSuiteIds: Set<string>;
  executions: QaTestExecution[];
  pushResults: EngineeringOperationResponse[];
  pushResultsLoading: boolean;
  runAll: () => Promise<void>;
  runSuites: (suiteIds: string[]) => Promise<void>;
  clearExecutions: () => void;
}

const UiQaTestContext = createContext<UiQaTestContextValue | null>(null);

function defToUiSuite(def: OperationDefinitionResponse): UiQaSuite {
  return {
    suiteId: def.operationId,
    suiteName: def.displayName,
    category: def.category,
    testDefinition: def.description,
    testExecution: def.authorized ? 'Authorized — click Run to execute' : `Not authorized — ${def.availability}`,
    status: def.authorized ? 'Pending' : 'Blocked',
    executedAt: null,
    environment: def.environments.join(', '),
    passed: null,
    failed: null,
    skipped: null,
    blocked: null,
    duration: '-',
    coverage: 'Not applicable',
    reportReference: '',
    evidenceReference: '',
    limitations: def.limitation ? [def.limitation] : [],
  };
}

function opStatusToSuiteStatus(opStatus: string): string {
  switch (opStatus) {
    case 'Succeeded':
      return 'Passed';
    case 'Failed':
      return 'Failed';
    case 'Cancelled':
      return 'Cancelled';
    case 'Skipped':
      return 'Skipped';
    case 'Running':
      return 'Running';
    default:
      return opStatus;
  }
}

function opToUiSuite(op: EngineeringOperationResponse): UiQaSuite {
  const passed = op.status === 'Succeeded' ? 1 : 0;
  const failed = op.status === 'Failed' ? 1 : 0;
  return {
    suiteId: op.operationId,
    suiteName: op.displayName,
    category: op.category,
    testDefinition: '',
    testExecution: `Executed via quality API — ${op.status}`,
    status: opStatusToSuiteStatus(op.status),
    executedAt: op.updatedAt,
    environment: op.environment,
    passed,
    failed,
    skipped: 0,
    blocked: 0,
    duration: op.steps.length > 0
      ? `${op.steps.filter((s) => s.status === 'completed' || s.status === 'Succeeded').length}/${op.steps.length} steps`
      : '-',
    coverage: 'Not applicable',
    reportReference: op.providerReference ?? '',
    evidenceReference: op.artifacts.map((a) => a.reference).join('; '),
    limitations: op.limitations,
  };
}

function isOperationComplete(op: EngineeringOperationResponse): boolean {
  if (op.status === 'Succeeded' || op.status === 'Failed' || op.status === 'Cancelled' || op.status === 'Skipped') return true;
  if (op.provider === 'simulation') return true;
  return false;
}

export function UiQaTestProvider({ children }: { children: ReactNode }) {
  const [qaSuites, setQaSuites] = useState<UiQaSuite[]>(() => buildUiQaSuites());
  const [suitesLoading, setSuitesLoading] = useState(true);
  const [runningSuiteIds, setRunningSuiteIds] = useState<Set<string>>(new Set());
  const [executions, setExecutions] = useState<QaTestExecution[]>([]);
  const [pushResults, setPushResults] = useState<EngineeringOperationResponse[]>([]);
  const [pushResultsLoading, setPushResultsLoading] = useState(true);

  const pendingRef = useRef<{ suiteIds: string[]; startedAt: string } | null>(null);
  const batchDefsRef = useRef<UiQaSuite[]>([]);

  useEffect(() => {
    let cancelled = false;
    api
      .listQualitySuites()
      .then((defs) => {
        if (cancelled) return;
        const quality = defs.filter((d) => d.category === 'quality');
        if (quality.length > 0) {
          setQaSuites(quality.map(defToUiSuite));
        }
      })
      .catch(() => {})
      .finally(() => {
        if (!cancelled) setSuitesLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setPushResultsLoading(true);
    api
      .listQualityRuns(100)
      .then((runs) => {
        console.log('Fetched push results', runs);
        if (cancelled) return;
        setPushResults(runs.filter((r) => r.operationId === 'push-ci'));
      })
      .catch(() => {console.log('Failed to fetch push results')})
      .finally(() => {
        if (!cancelled) setPushResultsLoading(false);
        console.log('pushResults', pushResults);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (runningSuiteIds.size === 0) return;

    const interval = setInterval(async () => {
      const pending = pendingRef.current;
      if (!pending) return;

      try {
        const runs = await api.listQualityRuns(100);
        const relevant = runs.filter((r) => pending.suiteIds.includes(r.operationId));

        if (relevant.length > 0 && relevant.every(isOperationComplete)) {
          const finishedAt = new Date().toISOString();
          const execution: QaTestExecution = {
            executionId: `qa-exec-${Date.now()}`,
            startedAt: pending.startedAt,
            finishedAt,
            durationMs: new Date(finishedAt).getTime() - new Date(pending.startedAt).getTime(),
            suites: pending.suiteIds.map((id) => {
              const op = runs.find((r) => r.operationId === id);
              if (op) return opToUiSuite(op);
              const def = batchDefsRef.current.find((s) => s.suiteId === id);
              if (def) return { ...def, status: 'Not started', testExecution: 'Operation failed to start' };
              return {
                suiteId: id,
                suiteName: id,
                category: 'quality',
                testDefinition: '',
                testExecution: 'Unknown',
                status: 'Unknown',
                executedAt: null,
                environment: '',
                passed: null,
                failed: null,
                skipped: null,
                blocked: null,
                duration: '-',
                coverage: 'Not applicable',
                reportReference: '',
                evidenceReference: '',
                limitations: [],
              };
            }),
          };
          setExecutions((prev) => [execution, ...prev]);
          setRunningSuiteIds(new Set());
          pendingRef.current = null;
        }
      } catch {
        /* polling error */
      }
    }, 3000);

    return () => clearInterval(interval);
  }, [runningSuiteIds]);

  const runSuites = useCallback(
    async (suiteIds: string[]) => {
      const suitesToRun = qaSuites.filter((s) => suiteIds.includes(s.suiteId));
      if (suitesToRun.length === 0) return;

      const startedAt = new Date().toISOString();
      batchDefsRef.current = suitesToRun;
      pendingRef.current = { suiteIds, startedAt };
      setRunningSuiteIds(new Set(suiteIds));

      for (const suite of suitesToRun) {
        try {
          await api.startQualityRun({
            operationId: suite.suiteId,
            environment: 'ci',
            ref: 'master',
            inputs: {},
            collectEvidence: true,
            confirmation: null,
          });
        } catch {
          /* start failed — polling will mark it as "Not started" */
        }
      }
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
    () => ({ qaSuites, suitesLoading, runningSuiteIds, executions, pushResults, pushResultsLoading, runAll, runSuites, clearExecutions }),
    [qaSuites, suitesLoading, runningSuiteIds, executions, pushResults, pushResultsLoading, runAll, runSuites, clearExecutions],
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
