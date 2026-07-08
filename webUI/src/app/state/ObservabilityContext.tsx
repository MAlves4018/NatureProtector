import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useUiArea } from './AreaContext';
import { useUiCapabilities } from './CapabilityContext';
import type {
  RabbitMqMetricsResponse,
  RuntimeEvidenceCatalogResponse,
  RuntimeOperationalHealthResponse,
} from '../types';

interface UiObservabilityContextValue {
  operationalHealth: RuntimeOperationalHealthResponse | null;
  rabbitMqMetrics: RabbitMqMetricsResponse | null;
  evidenceCatalog: RuntimeEvidenceCatalogResponse | null;
  observabilityError: Error | null;
}

const UiObservabilityContext = createContext<UiObservabilityContextValue | null>(null);

export function UiObservabilityProvider({ children }: { children: ReactNode }) {
  const { resolvedAreaCode, areasLoading } = useUiArea();
  const { canReadPipeline, canReadEvidence } = useUiCapabilities();

  const [operationalHealth, setOperationalHealth] = useState<RuntimeOperationalHealthResponse | null>(null);
  const [rabbitMqMetrics, setRabbitMqMetrics] = useState<RabbitMqMetricsResponse | null>(null);
  const [evidenceCatalog, setEvidenceCatalog] = useState<RuntimeEvidenceCatalogResponse | null>(null);
  const [observabilityError, setObservabilityError] = useState<Error | null>(null);

  useEffect(() => {
    if (!canReadPipeline && !canReadEvidence) {
      setOperationalHealth(null);
      setRabbitMqMetrics(null);
      setEvidenceCatalog(null);
      setObservabilityError(null);
      return;
    }
    if (!resolvedAreaCode || areasLoading) {
      return;
    }

    let cancelled = false;
    setObservabilityError(null);
    Promise.allSettled([
      canReadPipeline ? api.getRuntimeOperationalHealth() : Promise.resolve(null),
      canReadPipeline ? api.getRuntimeRabbitMqMetrics() : Promise.resolve(null),
      canReadEvidence ? api.listRuntimeEvidence() : Promise.resolve(null),
    ]).then(([healthResult, rabbitMqResult, evidenceResult]) => {
      if (cancelled) return;
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
  }, [canReadPipeline, canReadEvidence, resolvedAreaCode, areasLoading]);

  const value = useMemo(
    () => ({ operationalHealth, rabbitMqMetrics, evidenceCatalog, observabilityError }),
    [operationalHealth, rabbitMqMetrics, evidenceCatalog, observabilityError],
  );

  return <UiObservabilityContext.Provider value={value}>{children}</UiObservabilityContext.Provider>;
}

export function useUiObservability() {
  const context = useContext(UiObservabilityContext);
  if (!context) {
    throw new Error('useUiObservability must be used within UiObservabilityProvider');
  }
  return context;
}

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}
