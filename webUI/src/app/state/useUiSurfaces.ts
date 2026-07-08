import { useEffect, useMemo, useState } from 'react';
import { api } from '../services/api';
import { useToken } from '../context/TokenContext';
import { useUiRisk } from './RiskContext';
import { useUiActivity } from './ActivityContext';
import { useUiObservability } from './ObservabilityContext';
import { useUiLocale } from './LocaleContext';
import { useUiCapabilities } from './CapabilityContext';
import type { ControlledValidationP3AvailabilityResponse } from '../types';
import {
  buildUiPipelineSurface,
  buildUiQaSuites,
  buildUiEvidenceItems,
  buildUiReadinessItems,
  buildUiAdminActions,
  buildUiP3Surface,
  type UiReadinessItem,
  type UiAdminAction,
} from '../technicalSurfaces';

export function usePipelineSurface() {
  const { summary } = useUiRisk();
  const { selectedRun, runAudit, runTimings } = useUiActivity();
  const { operationalHealth, rabbitMqMetrics, observabilityError } = useUiObservability();
  const { locale } = useUiLocale();

  return useMemo(
    () =>
      buildUiPipelineSurface(
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
}

export function useQaSuites() {
  return useMemo(() => buildUiQaSuites(), []);
}

export function useEvidenceItems() {
  const { summary } = useUiRisk();
  const { selectedRun, runAudit, runTimings } = useUiActivity();
  const { evidenceCatalog } = useUiObservability();
  const { locale } = useUiLocale();

  return useMemo(
    () =>
      buildUiEvidenceItems(
        { summary, run: selectedRun, audit: runAudit, timings: runTimings, catalog: evidenceCatalog },
        locale,
      ),
    [summary, selectedRun, runAudit, runTimings, evidenceCatalog, locale],
  );
}

export function useReadinessItems(): readonly UiReadinessItem[] {
  const { summary } = useUiRisk();
  const { selectedRun } = useUiActivity();
  const { user } = useToken();

  return useMemo(() => buildUiReadinessItems({ summary, run: selectedRun, user }), [summary, selectedRun, user]);
}

export function useAdminActions(): readonly UiAdminAction[] {
  const { user } = useToken();

  return useMemo(() => buildUiAdminActions(user), [user]);
}

export function useP3Surface() {
  const { p3Availability, p3Error } = useP3Data();
  const { locale } = useUiLocale();

  return useMemo(() => buildUiP3Surface(p3Availability, p3Error, locale), [p3Availability, p3Error, locale]);
}

// Separate hook for P3 data fetching (only needed by ExperimentalPage)
export function useP3Data() {
  const { canReadProtectedP3 } = useUiCapabilities();
  const [p3Availability, setP3Availability] = useState<ControlledValidationP3AvailabilityResponse | null>(null);
  const [p3Error, setP3Error] = useState<Error | null>(null);
  const [p3Loading, setP3Loading] = useState(false);

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
        if (!cancelled) setP3Availability(result);
      })
      .catch((err) => {
        if (!cancelled) setP3Error(asError(err, 'Failed to load P3 availability'));
      })
      .finally(() => {
        if (!cancelled) setP3Loading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [canReadProtectedP3]);

  return { p3Availability, p3Error, p3Loading };
}

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}
