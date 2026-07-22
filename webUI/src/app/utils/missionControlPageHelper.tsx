import { useOperations } from '../operations/OperationsContext';

export interface ReadinessItem {
  label: string;
  status: string;
  detail: string;
  risk: 'low' | 'medium' | 'high';
}

export function buildReadiness(
  catalog: ReturnType<typeof useOperations>['catalog'],
  operations: ReturnType<typeof useOperations>['operations'],
  environments: ReturnType<typeof useOperations>['environments'],
): ReadinessItem[] {
  const latestByCategory = (category: string) => operations.find((operation) => operation.category === category);
  const summarize = (category: string, label: string): ReadinessItem => {
    const operation = latestByCategory(category);
    if (!operation) {
      return {
        label,
        status: 'NOT_RUN',
        detail: 'Não existe uma execução registada neste operation store.',
        risk: 'medium',
      };
    }
    const proved = operation.evidenceLevel.startsWith('PROVED');
    return {
      label,
      status: proved ? 'PROVED' : operation.status.toUpperCase(),
      detail: `${operation.operationId} · ${operation.evidenceLevel}`,
      risk: proved ? 'low' : operation.status === 'Failed' ? 'high' : 'medium',
    };
  };
  const production = environments.find((environment) => environment.environment === 'production');
  const staging = environments.find((environment) => environment.environment === 'staging');
  const productionDeploy = catalog.find((definition) => definition.operationId === 'production-deploy');
  const pendingApprovals = operations.filter((operation) => operation.status === 'AwaitingApproval').length;

  return [
    summarize('quality', 'Quality'),
    summarize('evidence', 'Evidence'),
    {
      label: 'Staging',
      status: staging?.deployable ? 'DECLARED' : 'UNKNOWN',
      detail: staging
        ? `${staging.projectId} · ${staging.observedState}; não equivale a deployment observado.`
        : 'O catálogo de ambientes não está disponível para esta role.',
      risk: 'medium',
    },
    {
      label: 'Production',
      status: production?.deployable && productionDeploy?.authorized ? 'GATED' : 'LOCKED',
      detail:
        production?.limitations[0] ?? productionDeploy?.limitation ?? 'Produção requer release, staging e aprovação.',
      risk: 'high',
    },
    {
      label: 'Approvals',
      status: pendingApprovals > 0 ? `${pendingApprovals} PENDING` : 'CLEAR',
      detail:
        pendingApprovals > 0
          ? 'Existem operações à espera de decisão separada.'
          : 'Não existem approvals pendentes visíveis.',
      risk: pendingApprovals > 0 ? 'medium' : 'low',
    },
  ];
}
