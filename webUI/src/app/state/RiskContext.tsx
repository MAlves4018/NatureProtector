import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useUiArea } from './AreaContext';
import { useUiCapabilities } from './CapabilityContext';
import type { RuntimeSummaryResponse } from '../types';
import { buildUiRiskReadModel, type UiRiskReadModel } from '../outputContext';
import { useUiLocale } from './LocaleContext';

interface UiRiskContextValue {
  summary: RuntimeSummaryResponse | null;
  summaryLoading: boolean;
  summaryError: Error | null;
  riskModel: UiRiskReadModel;
}

const UiRiskContext = createContext<UiRiskContextValue | null>(null);

export function UiRiskProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode, areasLoading } = useUiArea();
  const { canReadRisk, canReadRun, canReadScenario } = useUiCapabilities();
  const { locale } = useUiLocale();
  const [summary, setSummary] = useState<RuntimeSummaryResponse | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryError, setSummaryError] = useState<Error | null>(null);

  const riskModel = useMemo(
    () =>
      buildUiRiskReadModel(
        { summary, loading: summaryLoading, error: summaryError, accessDenied: !canReadRisk },
        locale,
      ),
    [summary, summaryLoading, summaryError, canReadRisk, locale],
  );

  useEffect(() => {
    if (!resolvedAreaCode || !canReadRisk || areasLoading) {
      setSummary(null);
      setSummaryError(null);
      return;
    }

    let cancelled = false;
    setSummaryLoading(true);
    setSummaryError(null);
    api
      .getRuntimeSummary(resolvedAreaCode)
      .then((result) => {
        if (!cancelled) setSummary(result);
      })
      .catch((err) => {
        if (!cancelled) setSummaryError(asError(err, 'Failed to load runtime summary'));
      })
      .finally(() => {
        if (!cancelled) setSummaryLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [resolvedAreaCode, canReadRisk, areasLoading]);

  const value = useMemo(
    () => ({ summary, summaryLoading, summaryError, riskModel }),
    [summary, summaryLoading, summaryError, riskModel],
  );

  return <UiRiskContext.Provider value={value}>{children}</UiRiskContext.Provider>;
}

export function useUiRisk() {
  const context = useContext(UiRiskContext);
  if (!context) {
    throw new Error('useUiRisk must be used within UiRiskProvider');
  }
  return context;
}

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}