import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useUiArea } from './AreaContext';
import { useUiCapabilities } from './CapabilityContext';
import { useUiLocale } from './LocaleContext';
import { useUiRisk } from './RiskContext';
import type {
  ScenarioResponse,
  SimulationRunResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunAuditResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeOperationResponse,
} from '../types';
import {
  buildUiScenarioContext,
  buildUiRunContext,
  type UiScenarioContextModel,
  type UiRunContextModel,
} from '../coreContext';

const SCENARIO_STORAGE_KEY = 'np.Ui.scenarioCode';
const RUN_STORAGE_KEY = 'np.Ui.runId';

interface UiActivityContextValue {
  scenarios: ScenarioResponse[];
  scenariosLoading: boolean;
  scenarioError: Error | null;
  selectedScenarioCode: string;
  setSelectedScenarioCode: (scenarioCode: string) => void;
  scenarioContext: UiScenarioContextModel;
  runs: SimulationRunResponse[];
  runsLoading: boolean;
  runsError: Error | null;
  selectedRunId: string;
  setSelectedRunId: (runId: string) => void;
  selectedRun: RuntimeRunSummaryResponse | SimulationRunResponse | null;
  runAudit: RuntimeRunAuditResponse | null;
  runTimings: RuntimeRunTimingSummaryResponse | null;
  runOperation: RuntimeOperationResponse | null;
  runDetailsLoading: boolean;
  runDetailsError: Error | null;
  runContext: UiRunContextModel;
  refreshSelectedRun: () => void;
}

const UiActivityContext = createContext<UiActivityContextValue | null>(null);

export function UiActivityProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode, areasLoading } = useUiArea();
  const { canReadRisk, canReadRun, canReadScenario } = useUiCapabilities();
  const { locale } = useUiLocale();
  const { summary } = useUiRisk();

  const [scenarios, setScenarios] = useState<ScenarioResponse[]>([]);
  const [scenariosLoading, setScenariosLoading] = useState(false);
  const [scenarioError, setScenarioError] = useState<Error | null>(null);
  const [selectedScenarioCode, setSelectedScenarioCode] = useState(
    () => sessionStorage.getItem(SCENARIO_STORAGE_KEY) ?? '',
  );

  const [runs, setRuns] = useState<SimulationRunResponse[]>([]);
  const [runsLoading, setRunsLoading] = useState(false);
  const [runsError, setRunsError] = useState<Error | null>(null);
  const [selectedRunId, setSelectedRunId] = useState(() => sessionStorage.getItem(RUN_STORAGE_KEY) ?? '');

  const [runtimeRun, setRuntimeRun] = useState<RuntimeRunSummaryResponse | null>(null);
  const [runAudit, setRunAudit] = useState<RuntimeRunAuditResponse | null>(null);
  const [runTimings, setRunTimings] = useState<RuntimeRunTimingSummaryResponse | null>(null);
  const [runOperation, setRunOperation] = useState<RuntimeOperationResponse | null>(null);
  const [runDetailsLoading, setRunDetailsLoading] = useState(false);
  const [runDetailsError, setRunDetailsError] = useState<Error | null>(null);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const previousAreaCodeRef = useRef<string | null | undefined>(resolvedAreaCode);

  const scenarioContext = useMemo(
    () => buildUiScenarioContext(selectedScenarioCode, scenarios, locale, scenarioError),
    [selectedScenarioCode, scenarios, locale, scenarioError],
  );

  const selectedRunFromSummary = useMemo(() => findRunInSummary(summary, selectedRunId), [summary, selectedRunId]);
  const selectedRunFromList = useMemo(
    () => runs.find((run) => run.id === selectedRunId) ?? null,
    [runs, selectedRunId],
  );
  const selectedRun =
    (runtimeRun?.id === selectedRunId ? runtimeRun : null) ?? selectedRunFromSummary ?? selectedRunFromList;
  const scopedAudit = runAudit?.run.id === selectedRunId ? runAudit : null;
  const scopedTimings = runTimings?.simulationRunId === selectedRunId ? runTimings : null;
  const scopedOperation = runOperation?.simulationRunId === selectedRunId ? runOperation : null;

  const runContext = useMemo(
    () =>
      buildUiRunContext(
        {
          requestedRunId: selectedRunId || null,
          selectedRun,
          summary,
          audit: scopedAudit,
          timings: scopedTimings,
          loading: runDetailsLoading,
          error: selectedRun ? null : runDetailsError,
        },
        locale,
      ),
    [selectedRunId, selectedRun, summary, scopedAudit, scopedTimings, runDetailsLoading, runDetailsError, locale],
  );

  useEffect(() => {
    const previousAreaCode = previousAreaCodeRef.current;
    previousAreaCodeRef.current = resolvedAreaCode;
    if (previousAreaCode === undefined || previousAreaCode === resolvedAreaCode) return;

    setSelectedScenarioCode('');
    setSelectedRunId('');
    setRuntimeRun(null);
    setRunAudit(null);
    setRunTimings(null);
    setRunOperation(null);
    setRunDetailsError(null);
    setRunDetailsLoading(false);
  }, [resolvedAreaCode]);

  useEffect(() => {
    if (selectedScenarioCode) {
      sessionStorage.setItem(SCENARIO_STORAGE_KEY, selectedScenarioCode);
    } else {
      sessionStorage.removeItem(SCENARIO_STORAGE_KEY);
    }
  }, [selectedScenarioCode]);

  useEffect(() => {
    selectedRunId ? sessionStorage.setItem(RUN_STORAGE_KEY, selectedRunId) : sessionStorage.removeItem(RUN_STORAGE_KEY);
  }, [selectedRunId]);

  useEffect(() => {
    if (!resolvedAreaCode || !canReadRisk || areasLoading) {
      setScenarios([]);
      setScenarioError(null);
      setRuns([]);
      setRunsError(null);
      return;
    }

    let cancelled = false;
    setScenariosLoading(canReadScenario);
    setRunsLoading(canReadRun);
    setScenarioError(null);
    setRunsError(null);

    Promise.allSettled([
      canReadScenario ? api.getAreaScenarios(resolvedAreaCode) : Promise.resolve([]),
      canReadRun ? api.listSimulationRuns(resolvedAreaCode, null, 20) : Promise.resolve([]),
    ])
      .then(([scenariosResult, runsResult]) => {
        if (cancelled) return;
        if (scenariosResult.status === 'fulfilled') {
          setScenarios(scenariosResult.value);
          setSelectedScenarioCode((current) =>
            scenariosResult.value.some((scenario) => scenario.code === current)
              ? current
              : (scenariosResult.value[0]?.code ?? ''),
          );
        } else {
          setScenarios([]);
          setSelectedScenarioCode('');
          setScenarioError(asError(scenariosResult.reason, 'Failed to load scenarios'));
        }
        if (runsResult.status === 'fulfilled') {
          setRuns(runsResult.value);
          setSelectedRunId((current) => {
            if (runsResult.value.some((run) => run.id === current)) return current;
            const preferredId = summary?.currentRun?.id ?? summary?.latestRun?.id;
            return runsResult.value.some((run) => run.id === preferredId)
              ? (preferredId ?? '')
              : (runsResult.value[0]?.id ?? '');
          });
        } else {
          setRuns([]);
          setSelectedRunId('');
          setRunsError(asError(runsResult.reason, 'Failed to load runs'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setScenariosLoading(false);
          setRunsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [
    resolvedAreaCode,
    canReadRisk,
    canReadRun,
    canReadScenario,
    areasLoading,
    summary?.currentRun?.id,
    summary?.latestRun?.id,
  ]);

  useEffect(() => {
    void refreshVersion;
    if (!selectedRunId || !canReadRun) {
      setRuntimeRun(null);
      setRunAudit(null);
      setRunTimings(null);
      setRunOperation(null);
      return;
    }

    let cancelled = false;
    setRuntimeRun(null);
    setRunAudit(null);
    setRunTimings(null);
    setRunOperation(null);
    setRunDetailsLoading(true);
    setRunDetailsError(null);
    Promise.allSettled([
      api.getRuntimeRun(selectedRunId),
      api.getRuntimeRunAudit(selectedRunId),
      api.getRuntimeRunTimings(selectedRunId),
      api.getRuntimeOperationByRun(selectedRunId).catch(() => null),
    ])
      .then(([runResult, auditResult, timingsResult, operationResult]) => {
        if (cancelled) return;
        const resolvedRun = runResult.status === 'fulfilled' ? runResult.value : null;
        const resolvedAudit = auditResult.status === 'fulfilled' ? auditResult.value : null;
        const resolvedTimings = timingsResult.status === 'fulfilled' ? timingsResult.value : null;
        const resolvedOperation = operationResult.status === 'fulfilled' ? operationResult.value : null;
        const areaMismatch = resolvedRun && resolvedAreaCode && resolvedRun.areaCode !== resolvedAreaCode;

        if (areaMismatch) {
          setRuntimeRun(null);
          setRunAudit(null);
          setRunTimings(null);
          setRunOperation(null);
          setRunDetailsError(new Error(`Run ${selectedRunId} does not belong to area ${resolvedAreaCode}.`));
          return;
        }

        setRuntimeRun(resolvedRun);
        setRunAudit(resolvedAudit);
        setRunTimings(resolvedTimings);
        setRunOperation(resolvedOperation);
        const rejected = [runResult, auditResult, timingsResult].find((result) => result.status === 'rejected');
        setRunDetailsError(
          rejected && rejected.status === 'rejected' ? asError(rejected.reason, 'Failed to load run details') : null,
        );
      })
      .finally(() => {
        if (!cancelled) setRunDetailsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedRunId, canReadRun, resolvedAreaCode, refreshVersion]);

  useEffect(() => {
    if (!selectedRunId || scopedOperation?.accounting.settled !== false) return;
    const timer = window.setInterval(() => setRefreshVersion((current) => current + 1), 3000);
    return () => window.clearInterval(timer);
  }, [selectedRunId, scopedOperation?.accounting.settled]);

  const value = useMemo(
    () => ({
      scenarios,
      scenariosLoading,
      scenarioError,
      selectedScenarioCode,
      setSelectedScenarioCode,
      scenarioContext,
      runs,
      runsLoading,
      runsError,
      selectedRunId,
      setSelectedRunId,
      selectedRun,
      runAudit: scopedAudit,
      runTimings: scopedTimings,
      runOperation: scopedOperation,
      runDetailsLoading,
      runDetailsError,
      runContext,
      refreshSelectedRun: () => setRefreshVersion((current) => current + 1),
    }),
    [
      scenarios,
      scenariosLoading,
      scenarioError,
      selectedScenarioCode,
      scenarioContext,
      runs,
      runsLoading,
      runsError,
      selectedRunId,
      selectedRun,
      scopedAudit,
      scopedTimings,
      scopedOperation,
      runDetailsLoading,
      runDetailsError,
      runContext,
    ],
  );

  return <UiActivityContext.Provider value={value}>{children}</UiActivityContext.Provider>;
}

export function useUiActivity() {
  const context = useContext(UiActivityContext);
  if (!context) {
    throw new Error('useUiActivity must be used within UiActivityProvider');
  }
  return context;
}

function findRunInSummary(
  summary: { currentRun?: RuntimeRunSummaryResponse | null; latestRun?: RuntimeRunSummaryResponse | null } | null,
  selectedRunId: string,
) {
  if (!summary || !selectedRunId) return null;
  if (summary.currentRun?.id === selectedRunId) return summary.currentRun;
  if (summary.latestRun?.id === selectedRunId) return summary.latestRun;
  return null;
}

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}
