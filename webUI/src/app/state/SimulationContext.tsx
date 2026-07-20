import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useUiActivity } from './ActivityContext';
import { useUiCapabilities } from './CapabilityContext';
import { useUiArea } from './AreaContext';
import { useUiLocale } from './LocaleContext';
import type { RuntimeOperationResponse, RuntimeRunStartRequest, RuntimeRunStartResponse } from '../types';
import type { UiMessageKey } from '../i18n';
import { buildUiSimulationReview, type UiSimulationReviewModel } from '../coreContext';
import { DEGRADATION_PROFILE_OPTIONS } from '../content/technicalLabels';

export interface SimulationFormState {
  sensorCount: number;
  numberOfCycles: number;
  intervalSeconds: number;
  seed: string;
  degradationProfiles: string[];
  runLabel: string;
  waitForCompletion: boolean;
  collectEvidence: boolean;
  allowParallelRun: boolean;
  waitTimeoutSeconds: number;
}

export const initialSimulationForm: SimulationFormState = {
  sensorCount: 2,
  numberOfCycles: 3,
  intervalSeconds: 60,
  seed: '42',
  degradationProfiles: [],
  runLabel: 'ui-structural',
  waitForCompletion: false,
  collectEvidence: false,
  allowParallelRun: false,
  waitTimeoutSeconds: 300,
};

export const SIMULATION_FORM_STORAGE_KEY = 'natureprotector.ui.simulationForm.v2';
const LEGACY_SIMULATION_FORM_STORAGE_KEY = 'natureprotector.ui.simulationForm.v1';
const SIMULATION_FORM_SCHEMA_VERSION = 2;
const SYNCHRONOUS_WAIT_MARGIN_SECONDS = 30;

export function isRuntimeLaunchAvailable(mode: string = import.meta.env.MODE) {
  return mode !== 'production';
}

interface UiSimulationContextValue {
  simulationForm: SimulationFormState;
  setSimulationForm: React.Dispatch<React.SetStateAction<SimulationFormState>>;
  simulationRequest: RuntimeRunStartRequest;
  simulationReview: UiSimulationReviewModel;
  simulationResult: RuntimeRunStartResponse | null;
  runtimeOperation: RuntimeOperationResponse | null;
  simulationSubmitting: boolean;
  simulationError: Error | null;
  canExecuteSimulation: boolean;
  runtimeLaunchAvailable: boolean;
  submitSimulation: () => Promise<void>;
  degradationProfiles: readonly string[];
}

const UiSimulationContext = createContext<UiSimulationContextValue | null>(null);

export function UiSimulationProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode, reloadAreaContext, selectedAreaCode } = useUiArea();
  const { canExecuteSimulation: hasSimulationCapability } = useUiCapabilities();
  const runtimeLaunchAvailable = isRuntimeLaunchAvailable();
  const canExecuteSimulation = hasSimulationCapability && runtimeLaunchAvailable;
  const { selectedScenarioCode, setSelectedRunId } = useUiActivity();
  const { copy } = useUiLocale();

  const [simulationForm, setSimulationForm] = useState<SimulationFormState>(() => hydrateSimulationForm());
  const [simulationResult, setSimulationResult] = useState<RuntimeRunStartResponse | null>(null);
  const [runtimeOperation, setRuntimeOperation] = useState<RuntimeOperationResponse | null>(null);
  const [simulationSubmitting, setSimulationSubmitting] = useState(false);
  const [simulationError, setSimulationError] = useState<Error | null>(null);

  const selectedAreaCodeTrimmed = selectedAreaCode.trim();

  const simulationRequest = useMemo(
    () => buildSimulationRequest(resolvedAreaCode ?? selectedAreaCodeTrimmed, selectedScenarioCode, simulationForm),
    [resolvedAreaCode, selectedAreaCodeTrimmed, selectedScenarioCode, simulationForm],
  );

  const simulationReview = useMemo(
    () => buildUiSimulationReview(simulationRequest, simulationResult, 'pt-PT'),
    [simulationRequest, simulationResult],
  );

  useEffect(() => {
    persistSimulationForm(simulationForm);
  }, [simulationForm]);

  useEffect(() => {
    const operationId = simulationResult?.operationId;
    if (!operationId || runtimeOperation?.terminalOutcome) return;

    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const poll = async () => {
      try {
        const operation = await api.getRuntimeOperation(operationId);
        if (cancelled) return;
        setRuntimeOperation(operation);
        setSimulationError(null);
        if (operation.simulationRunId) setSelectedRunId(operation.simulationRunId);
        if (operation.terminalOutcome) {
          reloadAreaContext();
          return;
        }
      } catch (err) {
        if (!cancelled) setSimulationError(asError(err, 'Failed to read persisted runtime operation'));
      }
      if (!cancelled) timer = setTimeout(poll, 1500);
    };
    void poll();
    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [reloadAreaContext, runtimeOperation?.terminalOutcome, setSelectedRunId, simulationResult?.operationId]);

  const submitSimulation = useCallback(async () => {
    const blocker = simulationBlocker(copy, canExecuteSimulation, resolvedAreaCode, selectedScenarioCode);
    if (blocker) {
      setSimulationError(new Error(blocker));
      return;
    }

    setSimulationSubmitting(true);
    setSimulationError(null);
    setRuntimeOperation(null);
    try {
      const result = await api.startRuntimeRun(simulationRequest);
      setSimulationResult(result);
      if (result.run?.id) {
        setSelectedRunId(result.run.id);
      }
      reloadAreaContext();
    } catch (err) {
      setSimulationError(asError(err, 'Failed to start simulation'));
    } finally {
      setSimulationSubmitting(false);
    }
  }, [
    copy,
    canExecuteSimulation,
    resolvedAreaCode,
    selectedScenarioCode,
    simulationRequest,
    setSelectedRunId,
    reloadAreaContext,
  ]);

  const value = useMemo(
    () => ({
      simulationForm,
      setSimulationForm,
      simulationRequest,
      simulationReview,
      simulationResult,
      runtimeOperation,
      simulationSubmitting,
      simulationError,
      canExecuteSimulation,
      runtimeLaunchAvailable,
      submitSimulation,
      degradationProfiles: DEGRADATION_PROFILE_OPTIONS,
    }),
    [
      simulationForm,
      simulationRequest,
      simulationReview,
      simulationResult,
      runtimeOperation,
      simulationSubmitting,
      simulationError,
      canExecuteSimulation,
      runtimeLaunchAvailable,
      submitSimulation,
    ],
  );

  return <UiSimulationContext.Provider value={value}>{children}</UiSimulationContext.Provider>;
}

export function useUiSimulation() {
  const context = useContext(UiSimulationContext);
  if (!context) {
    throw new Error('useUiSimulation must be used within UiSimulationProvider');
  }
  return context;
}

export function buildSimulationRequest(
  areaCode: string,
  scenarioCode: string,
  form: SimulationFormState,
): RuntimeRunStartRequest {
  const seed = form.seed.trim();
  const degradationProfiles = normalizeDegradationProfiles(form.degradationProfiles);
  // An empty selection is an explicit nominal override, not permission to inherit
  // a scenario's default degradation profile.
  const requestedDegradationProfiles = degradationProfiles.length > 0 ? degradationProfiles : ['none'];
  const legacyDegradationProfile = requestedDegradationProfiles[0];
  const runLabel = form.runLabel.trim();

  const timeoutSeconds = form.waitTimeoutSeconds;

  return {
    areaCode,
    scenarioCode,
    sensorCount: form.sensorCount,
    numberOfCycles: form.numberOfCycles,
    intervalSeconds: form.intervalSeconds,
    seed: seed ? Number(seed) : null,
    degradationProfile: legacyDegradationProfile,
    collectEvidence: form.collectEvidence,
    waitForCompletion: form.waitForCompletion,
    timeoutSeconds: timeoutSeconds,
    allowParallelRun: form.allowParallelRun,
    runLabel: runLabel || null,
    degradationProfiles: requestedDegradationProfiles,
  };
}

export function minimumSynchronousWaitSeconds(form: SimulationFormState) {
  return form.numberOfCycles * form.intervalSeconds + SYNCHRONOUS_WAIT_MARGIN_SECONDS;
}

export function toggleDegradationProfile(
  currentProfiles: readonly string[],
  profile: string,
  checked: boolean,
): string[] {
  if (!DEGRADATION_PROFILE_OPTIONS.includes(profile as (typeof DEGRADATION_PROFILE_OPTIONS)[number])) {
    return normalizeDegradationProfiles(currentProfiles);
  }

  if (profile === 'none') {
    return [];
  }

  const nextProfiles = checked
    ? [...currentProfiles, profile]
    : currentProfiles.filter((currentProfile) => currentProfile !== profile);

  return normalizeDegradationProfiles(nextProfiles);
}

export function normalizeDegradationProfiles(value: unknown): string[] {
  const values = Array.isArray(value) ? value : typeof value === 'string' ? [value] : [];
  const canonicalProfiles = new Set(DEGRADATION_PROFILE_OPTIONS);
  const normalized = values
    .filter((profile): profile is string => typeof profile === 'string')
    .map((profile) => profile.trim())
    .filter((profile) => profile && canonicalProfiles.has(profile as (typeof DEGRADATION_PROFILE_OPTIONS)[number]));

  return [...new Set(normalized)].filter((profile) => profile !== 'none');
}

export function hydrateSimulationForm(storage: Pick<Storage, 'getItem'> | null = getSimulationFormStorage()) {
  if (!storage) {
    return initialSimulationForm;
  }

  try {
    const currentRawValue = storage.getItem(SIMULATION_FORM_STORAGE_KEY);
    if (currentRawValue) {
      const value = JSON.parse(currentRawValue) as Partial<SimulationFormState> & {
        schemaVersion?: unknown;
        degradationProfile?: unknown;
      };
      const persistedProfiles =
        'degradationProfiles' in value ? value.degradationProfiles : (value.degradationProfile ?? []);

      return {
        sensorCount: positiveNumberOrDefault(value.sensorCount, initialSimulationForm.sensorCount),
        numberOfCycles: positiveNumberOrDefault(value.numberOfCycles, initialSimulationForm.numberOfCycles),
        intervalSeconds: positiveNumberOrDefault(value.intervalSeconds, initialSimulationForm.intervalSeconds),
        seed: stringOrDefault(value.seed, initialSimulationForm.seed),
        degradationProfiles: normalizeDegradationProfiles(persistedProfiles),
        runLabel: stringOrDefault(value.runLabel, initialSimulationForm.runLabel),
        waitForCompletion: booleanOrDefault(value.waitForCompletion, initialSimulationForm.waitForCompletion),
        collectEvidence: booleanOrDefault(value.collectEvidence, initialSimulationForm.collectEvidence),
        allowParallelRun: booleanOrDefault(value.allowParallelRun, initialSimulationForm.allowParallelRun),
        waitTimeoutSeconds: positiveNumberOrDefault(value.waitTimeoutSeconds, initialSimulationForm.waitTimeoutSeconds),
      };
    }

    const legacyRawValue = storage.getItem(LEGACY_SIMULATION_FORM_STORAGE_KEY);
    if (!legacyRawValue) {
      return initialSimulationForm;
    }

    const legacy = JSON.parse(legacyRawValue) as Partial<SimulationFormState> & {
      timeoutSeconds?: unknown;
      degradationProfile?: unknown;
    };
    const persistedProfiles =
      'degradationProfiles' in legacy ? legacy.degradationProfiles : (legacy.degradationProfile ?? []);

    return {
      sensorCount: positiveNumberOrDefault(legacy.sensorCount, initialSimulationForm.sensorCount),
      numberOfCycles: positiveNumberOrDefault(legacy.numberOfCycles, initialSimulationForm.numberOfCycles),
      intervalSeconds: positiveNumberOrDefault(legacy.intervalSeconds, initialSimulationForm.intervalSeconds),
      seed: stringOrDefault(legacy.seed, initialSimulationForm.seed),
      degradationProfiles: normalizeDegradationProfiles(persistedProfiles),
      runLabel: stringOrDefault(legacy.runLabel, initialSimulationForm.runLabel),
      waitForCompletion: false,
      collectEvidence: booleanOrDefault(legacy.collectEvidence, initialSimulationForm.collectEvidence),
      allowParallelRun: booleanOrDefault(legacy.allowParallelRun, initialSimulationForm.allowParallelRun),
      waitTimeoutSeconds: Math.max(
        initialSimulationForm.waitTimeoutSeconds,
        positiveNumberOrDefault(legacy.timeoutSeconds, initialSimulationForm.waitTimeoutSeconds),
      ),
    };
  } catch {
    return initialSimulationForm;
  }
}

export function persistSimulationForm(
  form: SimulationFormState,
  storage: Pick<Storage, 'setItem' | 'removeItem'> | null = getSimulationFormStorage(),
) {
  if (!storage) {
    return;
  }

  try {
    storage.setItem(
      SIMULATION_FORM_STORAGE_KEY,
      JSON.stringify({
        schemaVersion: SIMULATION_FORM_SCHEMA_VERSION,
        sensorCount: form.sensorCount,
        numberOfCycles: form.numberOfCycles,
        intervalSeconds: form.intervalSeconds,
        seed: form.seed,
        degradationProfiles: normalizeDegradationProfiles(form.degradationProfiles),
        runLabel: form.runLabel,
        waitForCompletion: form.waitForCompletion,
        collectEvidence: form.collectEvidence,
        allowParallelRun: form.allowParallelRun,
        waitTimeoutSeconds: form.waitTimeoutSeconds,
      }),
    );
  } catch {
    storage.removeItem(SIMULATION_FORM_STORAGE_KEY);
    storage.removeItem(LEGACY_SIMULATION_FORM_STORAGE_KEY);
  }
}

function getSimulationFormStorage() {
  return typeof sessionStorage === 'undefined' ? null : sessionStorage;
}

function positiveNumberOrDefault(value: unknown, fallback: number) {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : fallback;
}

function stringOrDefault(value: unknown, fallback: string) {
  return typeof value === 'string' ? value : fallback;
}

function booleanOrDefault(value: unknown, fallback: boolean) {
  return typeof value === 'boolean' ? value : fallback;
}

function simulationBlocker(
  copy: (key: UiMessageKey) => string,
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

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}
