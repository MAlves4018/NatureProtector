import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type ReactNode,
  type SetStateAction,
} from 'react';
import { api } from '../../services/api';
import { useToken } from '../../context/TokenContext';
import type {
  AreaResponse,
  ControlledValidationP3AvailabilityResponse,
  RabbitMqMetricsResponse,
  RuntimeEvidenceCatalogResponse,
  RuntimeOperationalHealthResponse,
  RuntimeRunAuditResponse,
  RuntimeRunStartRequest,
  RuntimeRunStartResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
  RuntimeSummaryResponse,
  ScenarioResponse,
  SimulationRunResponse,
} from '../../types';
import { getUiV2Capabilities, hasUiV2Capability, type UiV2Capability, type UiV2NavTarget } from '../capabilities';
import {
  buildUiV2RunContext,
  buildUiV2ScenarioContext,
  buildUiV2SimulationReview,
  resolveUiV2Area,
  type UiV2AreaResolutionModel,
  type UiV2RunContextModel,
  type UiV2ScenarioContextModel,
  type UiV2SimulationReviewModel,
} from '../coreContext';
import { buildUiV2RiskReadModel, type UiV2RiskReadModel } from '../outputContext';
import {
  buildUiV2AdminActions,
  buildUiV2EvidenceItems,
  buildUiV2P3Surface,
  buildUiV2PipelineSurface,
  buildUiV2QaSuites,
  buildUiV2ReadinessItems,
  type UiV2AdminAction,
  type UiV2EvidenceItem,
  type UiV2QaSuite,
  type UiV2ReadinessItem,
  type UiV2TechnicalField,
} from '../technicalSurfaces';
import { isUiV2Locale, translate, type UiV2Locale, type UiV2MessageKey } from '../i18n';
import { DEGRADATION_PROFILE_OPTIONS } from '../content/technicalLabels';
import { defaultPageFor, getUiV2Pages, type UiV2PageDefinition } from '../navigation/pageRegistry';

const AREA_STORAGE_KEY = 'np.uiV2.areaCode';
const SCENARIO_STORAGE_KEY = 'np.uiV2.scenarioCode';
const RUN_STORAGE_KEY = 'np.uiV2.runId';

export interface SimulationFormState {
  sensorCount: number;
  numberOfCycles: number;
  intervalSeconds: number;
  seed: string;
  degradationProfile: string;
  runLabel: string;
  waitForCompletion: boolean;
  collectEvidence: boolean;
  allowParallelRun: boolean;
  timeoutSeconds: number;
}

export const initialSimulationForm: SimulationFormState = {
  sensorCount: 2,
  numberOfCycles: 3,
  intervalSeconds: 60,
  seed: '42',
  degradationProfile: '',
  runLabel: 'ui-v2-structural',
  waitForCompletion: false,
  collectEvidence: false,
  allowParallelRun: false,
  timeoutSeconds: 60,
};

interface UiV2ContextValue {
  user: ReturnType<typeof useToken>['user'];
  locale: UiV2Locale;
  setLocale: (locale: UiV2Locale) => void;
  copy: (key: UiV2MessageKey) => string;
  capabilities: Set<UiV2Capability>;
  capabilityAuthority: string;
  capabilitiesLoading: boolean;
  pages: readonly UiV2PageDefinition[];
  activePage: UiV2NavTarget;
  setActivePage: (page: UiV2NavTarget) => void;
  isPublic: boolean;
  areas: AreaResponse[];
  areasLoading: boolean;
  areaError: Error | null;
  selectedAreaCode: string;
  setSelectedAreaCode: (areaCode: string) => void;
  areaResolution: UiV2AreaResolutionModel;
  resolvedAreaCode: string | null;
  summary: RuntimeSummaryResponse | null;
  summaryLoading: boolean;
  summaryError: Error | null;
  riskModel: UiV2RiskReadModel;
  scenarios: ScenarioResponse[];
  scenariosLoading: boolean;
  scenarioError: Error | null;
  selectedScenarioCode: string;
  setSelectedScenarioCode: (scenarioCode: string) => void;
  scenarioContext: UiV2ScenarioContextModel;
  runs: SimulationRunResponse[];
  runsLoading: boolean;
  runsError: Error | null;
  selectedRunId: string;
  setSelectedRunId: (runId: string) => void;
  selectedRun: RuntimeRunSummaryResponse | SimulationRunResponse | null;
  runAudit: RuntimeRunAuditResponse | null;
  runTimings: RuntimeRunTimingSummaryResponse | null;
  runDetailsLoading: boolean;
  runDetailsError: Error | null;
  operationalHealth: RuntimeOperationalHealthResponse | null;
  rabbitMqMetrics: RabbitMqMetricsResponse | null;
  evidenceCatalog: RuntimeEvidenceCatalogResponse | null;
  observabilityError: Error | null;
  runContext: UiV2RunContextModel;
  simulationForm: SimulationFormState;
  setSimulationForm: Dispatch<SetStateAction<SimulationFormState>>;
  simulationRequest: RuntimeRunStartRequest;
  simulationReview: UiV2SimulationReviewModel;
  simulationResult: RuntimeRunStartResponse | null;
  simulationSubmitting: boolean;
  simulationError: Error | null;
  canExecuteSimulation: boolean;
  submitSimulation: () => Promise<void>;
  reloadAreaContext: () => void;
  pipelineFields: UiV2TechnicalField[];
  pipelineLimitations: string[];
  qaSuites: UiV2QaSuite[];
  evidenceItems: UiV2EvidenceItem[];
  readinessItems: UiV2ReadinessItem[];
  adminActions: UiV2AdminAction[];
  p3Surface: ReturnType<typeof buildUiV2P3Surface>;
  p3Loading: boolean;
  degradationProfiles: readonly string[];
}

const UiV2Context = createContext<UiV2ContextValue | null>(null);

export function UiV2Provider({ children }: { children: ReactNode }) {
  const { user } = useToken();
  const [locale, setLocale] = useState<UiV2Locale>(() => {
    const stored = sessionStorage.getItem('np.uiV2.locale');
    return isUiV2Locale(stored) ? stored : 'pt-PT';
  });
  const [activePage, setActivePage] = useState<UiV2NavTarget>('demo');
  const [areas, setAreas] = useState<AreaResponse[]>([]);
  const [areasLoading, setAreasLoading] = useState(false);
  const [areaError, setAreaError] = useState<Error | null>(null);
  const [selectedAreaCode, setSelectedAreaCode] = useState(
    () => initialValueFromQuery('area') ?? sessionStorage.getItem(AREA_STORAGE_KEY) ?? '',
  );
  const [summary, setSummary] = useState<RuntimeSummaryResponse | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryError, setSummaryError] = useState<Error | null>(null);
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
  const [runDetailsLoading, setRunDetailsLoading] = useState(false);
  const [runDetailsError, setRunDetailsError] = useState<Error | null>(null);
  const [operationalHealth, setOperationalHealth] = useState<RuntimeOperationalHealthResponse | null>(null);
  const [rabbitMqMetrics, setRabbitMqMetrics] = useState<RabbitMqMetricsResponse | null>(null);
  const [evidenceCatalog, setEvidenceCatalog] = useState<RuntimeEvidenceCatalogResponse | null>(null);
  const [observabilityError, setObservabilityError] = useState<Error | null>(null);
  const [simulationForm, setSimulationForm] = useState<SimulationFormState>(initialSimulationForm);
  const [simulationResult, setSimulationResult] = useState<RuntimeRunStartResponse | null>(null);
  const [simulationSubmitting, setSimulationSubmitting] = useState(false);
  const [simulationError, setSimulationError] = useState<Error | null>(null);
  const [p3Availability, setP3Availability] = useState<ControlledValidationP3AvailabilityResponse | null>(null);
  const [p3Loading, setP3Loading] = useState(false);
  const [p3Error, setP3Error] = useState<Error | null>(null);
  const [_refreshNonce, setRefreshNonce] = useState(0);
  const fallbackCapabilities = useMemo(() => getUiV2Capabilities(user), [user]);
  const [serverCapabilities, setServerCapabilities] = useState<Set<UiV2Capability> | null>(null);
  const [capabilityAuthority, setCapabilityAuthority] = useState('frontend-fallback');
  const [capabilitiesLoading, setCapabilitiesLoading] = useState(false);

  const capabilities = serverCapabilities ?? fallbackCapabilities;
  const pages = useMemo(() => getUiV2Pages(capabilities), [capabilities]);
  const isPublic = !user;
  const copy = useCallback((key: UiV2MessageKey) => translate(locale, key), [locale]);
  const canReadArea = hasUiV2Capability(capabilities, 'area.read');
  const canReadRisk = hasUiV2Capability(capabilities, 'risk.read');
  const canReadRun = hasUiV2Capability(capabilities, 'run.read');
  const canReadScenario = hasUiV2Capability(capabilities, 'scenario.read');
  const canExecuteSimulation = hasUiV2Capability(capabilities, 'simulation.execute');
  const canReadPipeline = hasUiV2Capability(capabilities, 'pipeline.read');
  const canReadEvidence = hasUiV2Capability(capabilities, 'evidence.read');
  const canReadProtectedP3 = hasUiV2Capability(capabilities, 'p3.read');

  const areaResolution = useMemo(
    () => resolveUiV2Area(selectedAreaCode, areas, locale, areasLoading, areaError),
    [selectedAreaCode, areas, locale, areasLoading, areaError],
  );
  const resolvedAreaCode = areaResolution.resolvedArea?.code ?? null;

  const riskModel = useMemo(
    () =>
      buildUiV2RiskReadModel(
        { summary, loading: summaryLoading, error: summaryError, accessDenied: !canReadRisk },
        locale,
      ),
    [summary, summaryLoading, summaryError, canReadRisk, locale],
  );
  const scenarioContext = useMemo(
    () => buildUiV2ScenarioContext(selectedScenarioCode, scenarios, locale, scenarioError),
    [selectedScenarioCode, scenarios, locale, scenarioError],
  );
  const selectedRunFromSummary = useMemo(() => findRunInSummary(summary, selectedRunId), [summary, selectedRunId]);
  const selectedRunFromList = useMemo(
    () => runs.find((run) => run.id === selectedRunId) ?? null,
    [runs, selectedRunId],
  );
  const selectedRun = runtimeRun ?? selectedRunFromSummary ?? selectedRunFromList;
  const runContext = useMemo(
    () =>
      buildUiV2RunContext(
        {
          requestedRunId: selectedRunId || null,
          selectedRun,
          summary,
          audit: runAudit,
          timings: runTimings,
          error: selectedRun ? null : runDetailsError,
        },
        locale,
      ),
    [selectedRunId, selectedRun, summary, runAudit, runTimings, runDetailsError, locale],
  );
  const simulationRequest = useMemo(
    () => buildSimulationRequest(resolvedAreaCode ?? selectedAreaCode.trim(), selectedScenarioCode, simulationForm),
    [resolvedAreaCode, selectedAreaCode, selectedScenarioCode, simulationForm],
  );
  const simulationReview = useMemo(
    () => buildUiV2SimulationReview(simulationRequest, simulationResult, locale),
    [simulationRequest, simulationResult, locale],
  );
  const pipelineSurface = useMemo(
    () =>
      buildUiV2PipelineSurface(
        {
          summary,
          run: selectedRun,
          audit: runAudit,
          timings: runTimings,
          health: operationalHealth,
          rabbitMq: rabbitMqMetrics,
          observabilityError,
        },
        locale,
      ),
    [summary, selectedRun, runAudit, runTimings, operationalHealth, rabbitMqMetrics, observabilityError, locale],
  );
  const qaSuites = useMemo(() => buildUiV2QaSuites(), []);
  const evidenceItems = useMemo(
    () =>
      buildUiV2EvidenceItems(
        { summary, run: selectedRun, audit: runAudit, timings: runTimings, catalog: evidenceCatalog },
        locale,
      ),
    [summary, selectedRun, runAudit, runTimings, evidenceCatalog, locale],
  );
  const adminActions = useMemo(() => buildUiV2AdminActions(user), [user]);
  const p3Surface = useMemo(
    () => buildUiV2P3Surface(p3Availability, p3Error, locale),
    [p3Availability, p3Error, locale],
  );
  const readinessItems = useMemo(
    () => buildUiV2ReadinessItems({ summary, run: selectedRun, user }),
    [summary, selectedRun, user],
  );

  useEffect(() => {
    if (!user) {
      setServerCapabilities(null);
      setCapabilityAuthority('public-fallback');
      setCapabilitiesLoading(false);
      return;
    }

    let cancelled = false;
    setCapabilitiesLoading(true);
    api
      .getCurrentCapabilities()
      .then((profile) => {
        if (cancelled) {
          return;
        }
        const allowed = new Set(profile.capabilities.filter(isUiV2Capability));
        setServerCapabilities(allowed);
        setCapabilityAuthority(profile.authority || 'backend-role-capability-policy');
      })
      .catch(() => {
        if (!cancelled) {
          setServerCapabilities(null);
          setCapabilityAuthority('frontend-fallback-after-backend-error');
        }
      })
      .finally(() => {
        if (!cancelled) {
          setCapabilitiesLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  useEffect(() => {
    sessionStorage.setItem('np.uiV2.locale', locale);
  }, [locale]);

  useEffect(() => {
    if (selectedAreaCode) {
      sessionStorage.setItem(AREA_STORAGE_KEY, selectedAreaCode);
      setQueryParam('area', selectedAreaCode);
    } else {
      sessionStorage.removeItem(AREA_STORAGE_KEY);
      setQueryParam('area', null);
    }
  }, [selectedAreaCode]);

  useEffect(() => {
    selectedScenarioCode
      ? sessionStorage.setItem(SCENARIO_STORAGE_KEY, selectedScenarioCode)
      : sessionStorage.removeItem(SCENARIO_STORAGE_KEY);
  }, [selectedScenarioCode]);

  useEffect(() => {
    selectedRunId ? sessionStorage.setItem(RUN_STORAGE_KEY, selectedRunId) : sessionStorage.removeItem(RUN_STORAGE_KEY);
  }, [selectedRunId]);

  useEffect(() => {
    const fallback = defaultPageFor(capabilities);
    if (!pages.some((page) => page.id === activePage)) {
      setActivePage(fallback);
    }
  }, [activePage, capabilities, pages]);

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key === 'F1') {
        event.preventDefault();
        window.dispatchEvent(new CustomEvent('np-ui-v2-help', { detail: 'overview' }));
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  useEffect(() => {
    if (!canReadPipeline && !canReadEvidence) {
      setOperationalHealth(null);
      setRabbitMqMetrics(null);
      setEvidenceCatalog(null);
      setObservabilityError(null);
      return;
    }

    let cancelled = false;
    setObservabilityError(null);
    Promise.allSettled([
      canReadPipeline ? api.getRuntimeOperationalHealth() : Promise.resolve(null),
      canReadPipeline ? api.getRuntimeRabbitMqMetrics() : Promise.resolve(null),
      canReadEvidence ? api.listRuntimeEvidence() : Promise.resolve(null),
    ]).then(([healthResult, rabbitMqResult, evidenceResult]) => {
      if (cancelled) {
        return;
      }
      setOperationalHealth(healthResult.status === 'fulfilled' ? healthResult.value : null);
      setRabbitMqMetrics(rabbitMqResult.status === 'fulfilled' ? rabbitMqResult.value : null);
      setEvidenceCatalog(evidenceResult.status === 'fulfilled' ? evidenceResult.value : null);
      const rejected = [healthResult, rabbitMqResult, evidenceResult].find((result) => result.status === 'rejected');
      setObservabilityError(
        rejected && rejected.status === 'rejected'
          ? asError(rejected.reason, 'Failed to load runtime observability')
          : null,
      );
    });

    return () => {
      cancelled = true;
    };
  }, [canReadPipeline, canReadEvidence]);

  useEffect(() => {
    if (!canReadArea) {
      setAreas([]);
      return;
    }

    let cancelled = false;
    setAreasLoading(true);
    setAreaError(null);
    api
      .getAreas()
      .then((result) => {
        if (!cancelled) {
          setAreas(result);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setAreaError(asError(err, 'Failed to load areas'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setAreasLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [canReadArea]);

  useEffect(() => {
    if (!resolvedAreaCode || !canReadRisk) {
      setSummary(null);
      setSummaryError(null);
      setScenarios([]);
      setScenarioError(null);
      setRuns([]);
      setRunsError(null);
      return;
    }

    let cancelled = false;
    setSummaryLoading(true);
    setScenariosLoading(canReadScenario);
    setRunsLoading(canReadRun);
    setSummaryError(null);
    setScenarioError(null);
    setRunsError(null);

    Promise.allSettled([
      api.getRuntimeSummary(resolvedAreaCode),
      canReadScenario ? api.getAreaScenarios(resolvedAreaCode) : Promise.resolve([]),
      canReadRun ? api.listSimulationRuns(resolvedAreaCode, null, 20) : Promise.resolve([]),
    ])
      .then(([summaryResult, scenariosResult, runsResult]) => {
        if (cancelled) {
          return;
        }
        if (summaryResult.status === 'fulfilled') {
          setSummary(summaryResult.value);
          if (!selectedRunId && summaryResult.value.latestRun?.id) {
            setSelectedRunId(summaryResult.value.latestRun.id);
          }
        } else {
          setSummary(null);
          setSummaryError(asError(summaryResult.reason, 'Failed to load runtime summary'));
        }
        if (scenariosResult.status === 'fulfilled') {
          setScenarios(scenariosResult.value);
          if (!selectedScenarioCode && scenariosResult.value[0]?.code) {
            setSelectedScenarioCode(scenariosResult.value[0].code);
          }
        } else {
          setScenarios([]);
          setScenarioError(asError(scenariosResult.reason, 'Failed to load scenarios'));
        }
        if (runsResult.status === 'fulfilled') {
          setRuns(runsResult.value);
        } else {
          setRuns([]);
          setRunsError(asError(runsResult.reason, 'Failed to load runs'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setSummaryLoading(false);
          setScenariosLoading(false);
          setRunsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [resolvedAreaCode, canReadRisk, canReadRun, canReadScenario, selectedScenarioCode, selectedRunId]);

  useEffect(() => {
    if (!selectedRunId || !canReadRun) {
      setRuntimeRun(null);
      setRunAudit(null);
      setRunTimings(null);
      return;
    }

    let cancelled = false;
    setRunDetailsLoading(true);
    setRunDetailsError(null);
    Promise.allSettled([
      api.getRuntimeRun(selectedRunId),
      api.getRuntimeRunAudit(selectedRunId),
      api.getRuntimeRunTimings(selectedRunId),
    ])
      .then(([runResult, auditResult, timingsResult]) => {
        if (cancelled) {
          return;
        }
        setRuntimeRun(runResult.status === 'fulfilled' ? runResult.value : null);
        setRunAudit(auditResult.status === 'fulfilled' ? auditResult.value : null);
        setRunTimings(timingsResult.status === 'fulfilled' ? timingsResult.value : null);
        const rejected = [runResult, auditResult, timingsResult].find((result) => result.status === 'rejected');
        setRunDetailsError(
          rejected && rejected.status === 'rejected' ? asError(rejected.reason, 'Failed to load run details') : null,
        );
      })
      .finally(() => {
        if (!cancelled) {
          setRunDetailsLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [selectedRunId, canReadRun]);

  useEffect(() => {
    if (!canReadProtectedP3) {
      setP3Availability(null);
      setP3Error(null);
      return;
    }

    let cancelled = false;
    setP3Loading(true);
    api
      .getControlledValidationP3Availability()
      .then((result) => {
        if (!cancelled) {
          setP3Availability(result);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setP3Error(asError(err, 'Failed to load P3 availability'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setP3Loading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [canReadProtectedP3]);

  const reloadAreaContext = useCallback(() => setRefreshNonce((value) => value + 1), []);

  const submitSimulation = useCallback(async () => {
    const blocker = simulationBlocker(copy, canExecuteSimulation, resolvedAreaCode, selectedScenarioCode);
    if (blocker) {
      setSimulationError(new Error(blocker));
      return;
    }

    setSimulationSubmitting(true);
    setSimulationError(null);
    try {
      const result = await api.startRuntimeRun(simulationRequest);
      setSimulationResult(result);
      if (result.run?.id) {
        setSelectedRunId(result.run.id);
        setActivePage('runs');
      }
      reloadAreaContext();
    } catch (err) {
      setSimulationError(asError(err, 'Failed to start simulation'));
    } finally {
      setSimulationSubmitting(false);
    }
  }, [copy, canExecuteSimulation, resolvedAreaCode, selectedScenarioCode, simulationRequest, reloadAreaContext]);

  const value = useMemo<UiV2ContextValue>(
    () => ({
      user,
      locale,
      setLocale,
      copy,
      capabilities,
      capabilityAuthority,
      capabilitiesLoading,
      pages,
      activePage,
      setActivePage,
      isPublic,
      areas,
      areasLoading,
      areaError,
      selectedAreaCode,
      setSelectedAreaCode,
      areaResolution,
      resolvedAreaCode,
      summary,
      summaryLoading,
      summaryError,
      riskModel,
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
      runAudit,
      runTimings,
      runDetailsLoading,
      runDetailsError,
      operationalHealth,
      rabbitMqMetrics,
      evidenceCatalog,
      observabilityError,
      runContext,
      simulationForm,
      setSimulationForm,
      simulationRequest,
      simulationReview,
      simulationResult,
      simulationSubmitting,
      simulationError,
      canExecuteSimulation,
      submitSimulation,
      reloadAreaContext,
      pipelineFields: pipelineSurface.fields,
      pipelineLimitations: pipelineSurface.limitations,
      qaSuites,
      evidenceItems,
      readinessItems,
      adminActions,
      p3Surface,
      p3Loading,
      degradationProfiles: DEGRADATION_PROFILE_OPTIONS,
    }),
    [
      user,
      locale,
      copy,
      capabilities,
      capabilityAuthority,
      capabilitiesLoading,
      pages,
      activePage,
      isPublic,
      areas,
      areasLoading,
      areaError,
      selectedAreaCode,
      areaResolution,
      resolvedAreaCode,
      summary,
      summaryLoading,
      summaryError,
      riskModel,
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
      runAudit,
      runTimings,
      runDetailsLoading,
      runDetailsError,
      operationalHealth,
      rabbitMqMetrics,
      evidenceCatalog,
      observabilityError,
      runContext,
      simulationForm,
      simulationRequest,
      simulationReview,
      simulationResult,
      simulationSubmitting,
      simulationError,
      canExecuteSimulation,
      submitSimulation,
      reloadAreaContext,
      pipelineSurface.fields,
      pipelineSurface.limitations,
      qaSuites,
      evidenceItems,
      readinessItems,
      adminActions,
      p3Surface,
      p3Loading,
    ],
  );

  return <UiV2Context.Provider value={value}>{children}</UiV2Context.Provider>;
}

export function useUiV2() {
  const context = useContext(UiV2Context);
  if (!context) {
    throw new Error('useUiV2 must be used within UiV2Provider');
  }
  return context;
}

function buildSimulationRequest(
  areaCode: string,
  scenarioCode: string,
  form: SimulationFormState,
): RuntimeRunStartRequest {
  const seed = form.seed.trim();
  const degradationProfile = form.degradationProfile.trim();
  const runLabel = form.runLabel.trim();

  return {
    areaCode,
    scenarioCode,
    sensorCount: form.sensorCount,
    numberOfCycles: form.numberOfCycles,
    intervalSeconds: form.intervalSeconds,
    seed: seed ? Number(seed) : null,
    degradationProfile: degradationProfile && degradationProfile !== 'none' ? degradationProfile : null,
    collectEvidence: form.collectEvidence,
    waitForCompletion: form.waitForCompletion,
    timeoutSeconds: form.timeoutSeconds,
    allowParallelRun: form.allowParallelRun,
    runLabel: runLabel || null,
    degradationProfiles: degradationProfile && degradationProfile !== 'none' ? [degradationProfile] : null,
  };
}

function simulationBlocker(
  copy: (key: UiV2MessageKey) => string,
  canExecute: boolean,
  resolvedAreaCode: string | null,
  selectedScenarioCode: string,
) {
  if (!canExecute) {
    return copy('simulation.forbidden');
  }
  if (!resolvedAreaCode) {
    return copy('simulation.blockedNoArea');
  }
  if (!selectedScenarioCode) {
    return copy('simulation.blockedNoScenario');
  }
  return null;
}

function findRunInSummary(summary: RuntimeSummaryResponse | null, selectedRunId: string) {
  if (!summary || !selectedRunId) {
    return null;
  }
  if (summary.currentRun?.id === selectedRunId) {
    return summary.currentRun;
  }
  if (summary.latestRun?.id === selectedRunId) {
    return summary.latestRun;
  }
  return null;
}

function initialValueFromQuery(name: string) {
  return new URLSearchParams(window.location.search).get(name);
}

function setQueryParam(name: string, value: string | null) {
  const url = new URL(window.location.href);
  if (value) {
    url.searchParams.set(name, value);
  } else {
    url.searchParams.delete(name);
  }
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
}

function isUiV2Capability(value: string): value is UiV2Capability {
  return getUiV2Capabilities({ roles: ['Pipeline', 'Sim', 'QA', 'Operations', 'ReleaseApprover', 'Admin'] }).has(
    value as UiV2Capability,
  );
}

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}
