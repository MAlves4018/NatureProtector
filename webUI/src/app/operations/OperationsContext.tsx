import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import type {
  CloudEnvironmentResponse,
  EngineeringOperationResponse,
  OperationComparisonResponse,
  OperationDefinitionResponse,
  StartOperationRequest,
} from '../types/operations';
import { useUiCapabilities } from '../state/CapabilityContext';

interface OperationsContextValue {
  catalog: OperationDefinitionResponse[];
  operations: EngineeringOperationResponse[];
  environments: CloudEnvironmentResponse[];
  pendingApprovals: EngineeringOperationResponse[];
  loading: boolean;
  error: Error | null;
  refresh: () => Promise<void>;
  start: (request: StartOperationRequest) => Promise<EngineeringOperationResponse>;
  cancel: (operationId: string) => Promise<EngineeringOperationResponse>;
  decide: (
    operationId: string,
    decision: 'approve' | 'reject',
    comment?: string,
  ) => Promise<EngineeringOperationResponse>;
  compare: (left: string, right: string) => Promise<OperationComparisonResponse>;
}

const OperationsContext = createContext<OperationsContextValue | null>(null);

export function OperationsProvider({ children }: { children: ReactNode }) {
  const { user, capabilities } = useUiCapabilities();
  const [catalog, setCatalog] = useState<OperationDefinitionResponse[]>([]);
  const [operations, setOperations] = useState<EngineeringOperationResponse[]>([]);
  const [environments, setEnvironments] = useState<CloudEnvironmentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const canReadCloud = capabilities.has('cloud.read');
  const canReviewApprovals = capabilities.has('approval.review');

  const refresh = useCallback(async () => {
    if (!user) {
      setCatalog([]);
      setOperations([]);
      setEnvironments([]);
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const [catalogResult, operationsResult, environmentResult] = await Promise.all([
        api.listOperationCatalog(),
        api.listOperations(undefined, 100),
        canReadCloud ? api.listCloudEnvironments() : Promise.resolve([]),
      ]);
      setCatalog(catalogResult);
      setOperations(operationsResult);
      setEnvironments(environmentResult);
    } catch (value) {
      setError(value instanceof Error ? value : new Error('Failed to load engineering operations.'));
    } finally {
      setLoading(false);
    }
  }, [user, canReadCloud]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const start = useCallback(
    async (request: StartOperationRequest) => {
      const operation = await api.startOperation(request);
      await refresh();
      return operation;
    },
    [refresh],
  );

  const cancel = useCallback(
    async (operationId: string) => {
      const operation = await api.cancelOperation(operationId);
      await refresh();
      return operation;
    },
    [refresh],
  );

  const decide = useCallback(
    async (operationId: string, decision: 'approve' | 'reject', comment?: string) => {
      const operation = await api.decideOperation(operationId, decision, comment);
      await refresh();
      return operation;
    },
    [refresh],
  );

  const compare = useCallback((left: string, right: string) => api.compareEvidenceOperations(left, right), []);

  const pendingApprovals = useMemo(
    () => (canReviewApprovals ? operations.filter((operation) => operation.status === 'AwaitingApproval') : []),
    [canReviewApprovals, operations],
  );

  const value = useMemo<OperationsContextValue>(
    () => ({
      catalog,
      operations,
      environments,
      pendingApprovals,
      loading,
      error,
      refresh,
      start,
      cancel,
      decide,
      compare,
    }),
    [catalog, operations, environments, pendingApprovals, loading, error, refresh, start, cancel, decide, compare],
  );

  return <OperationsContext.Provider value={value}>{children}</OperationsContext.Provider>;
}

export function useOperations() {
  const context = useContext(OperationsContext);
  if (!context) {
    throw new Error('useOperations must be used inside OperationsProvider.');
  }
  return context;
}