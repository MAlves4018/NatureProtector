import { useMemo } from 'react';
import { useToken } from '../context/TokenContext';
import { useUiRisk } from './RiskContext';
import { useUiActivity } from './ActivityContext';
import { useUiObservability } from './ObservabilityContext';
import { useUiLocale } from './LocaleContext';
import {
  buildUiPipelineSurface,
  buildUiQaSuites,
  buildUiEvidenceItems,
  buildUiReadinessItems,
  buildUiAdminActions,
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
  const { operationalHealth, rabbitMqMetrics, evidenceCatalog } = useUiObservability();
  const { user } = useToken();

  return useMemo(
    () =>
      buildUiReadinessItems({
        summary,
        run: selectedRun,
        user,
        health: operationalHealth,
        rabbitMq: rabbitMqMetrics,
        evidence: evidenceCatalog,
      }),
    [summary, selectedRun, user, operationalHealth, rabbitMqMetrics, evidenceCatalog],
  );
}

export function useAdminActions(): readonly UiAdminAction[] {
  const { user } = useToken();

  return useMemo(() => buildUiAdminActions(user), [user]);
}
