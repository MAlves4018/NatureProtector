import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useUiActivity } from './ActivityContext';
import { useUiCapabilities } from './CapabilityContext';
import { useUiArea } from './AreaContext';
import { useUiLocale } from './LocaleContext';
import type { RuntimeRunStartRequest, RuntimeRunStartResponse } from '../types';
import type { UiMessageKey } from '../i18n';
import { buildUiSimulationReview, type UiSimulationReviewModel } from '../coreContext';

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
  runLabel: 'ui-structural',
  waitForCompletion: false,
  collectEvidence: false,
  allowParallelRun: false,
  timeoutSeconds: 60,
};

const DEGRADATION_PROFILE_OPTIONS = [
  'none',
  'sensor-failure-random',
  'sensor-failure-clustered',
  'communication-loss',
  'power-degradation',
] as const;

interface UiSimulationContextValue {
  simulationForm: SimulationFormState;
  setSimulationForm: React.Dispatch<React.SetStateAction<SimulationFormState>>;
  simulationRequest: RuntimeRunStartRequest;
  simulationReview: UiSimulationReviewModel;
  simulationResult: RuntimeRunStartResponse | null;
  setSimulationResult: React.Dispatch<React.SetStateAction<RuntimeRunStartResponse | null>>;
  simulationSubmitting: boolean;
  simulationError: Error | null;
  canExecuteSimulation: boolean;
  submitSimulation: () => Promise<void>;
  degradationProfiles: readonly string[];
}

const UiSimulationContext = createContext<UiSimulationContextValue | null>(null);

export function UiSimulationProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode, reloadAreaContext, selectedAreaCode } = useUiArea();
  const { canExecuteSimulation } = useUiCapabilities();
  const { selectedScenarioCode, setSelectedRunId } = useUiActivity();
  const { copy } = useUiLocale();

  const [simulationForm, setSimulationForm] = useState<SimulationFormState>(initialSimulationForm);
  const [simulationResult, setSimulationResult] = useState<RuntimeRunStartResponse | null>(null);
  const [simulationSubmitting, setSimulationSubmitting] = useState(false);
  const [simulationError, setSimulationError] = useState<Error | null>(null);

  const selectedAreaCodeTrimmed = selectedAreaCode.trim();

  const simulationRequest = useMemo(
    () => buildSimulationRequest(resolvedAreaCode ?? selectedAreaCodeTrimmed, selectedScenarioCode, simulationForm),
    [resolvedAreaCode, selectedScenarioCode, simulationForm],
  );

  const simulationReview = useMemo(
    () => buildUiSimulationReview(simulationRequest, simulationResult, 'pt-PT'),
    [simulationRequest, simulationResult],
  );

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
      }
      reloadAreaContext();
    } catch (err) {
      setSimulationError(asError(err, 'Failed to start simulation'));
    } finally {
      setSimulationSubmitting(false);
    }
  }, [copy, canExecuteSimulation, resolvedAreaCode, selectedScenarioCode, simulationRequest, setSelectedRunId, reloadAreaContext]);

  const value = useMemo(
    () => ({
      simulationForm,
      setSimulationForm,
      simulationRequest,
      simulationReview,
      simulationResult,
      setSimulationResult,
      simulationSubmitting,
      simulationError,
      canExecuteSimulation,
      submitSimulation,
      degradationProfiles: DEGRADATION_PROFILE_OPTIONS,
    }),
    [
      simulationForm,
      simulationRequest,
      simulationReview,
      simulationResult,
      simulationSubmitting,
      simulationError,
      canExecuteSimulation,
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