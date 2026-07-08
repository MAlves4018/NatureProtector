import { useState } from 'react';
import { GitCompareArrows } from 'lucide-react';
import type { OperationComparisonResponse } from '../types/operations';
import { PageHeader } from '../components/PageHeader';
import { ComparisonBarChart } from '../components/ComparisonBarChart';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';

export function EvidenceExplorerPage() {
  const { catalog, operations, compare } = useOperations();
  const campaigns = catalog.filter((definition) => definition.category === 'evidence');
  const evidenceRuns = operations.filter(
    (operation) => operation.category === 'evidence' || operation.artifacts.length > 0,
  );
  const [left, setLeft] = useState('');
  const [right, setRight] = useState('');
  const [comparison, setComparison] = useState<OperationComparisonResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const runComparison = async () => {
    setError(null);
    try {
      setComparison(await compare(left, right));
    } catch (value) {
      setError(value instanceof Error ? value.message : 'A comparação falhou.');
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Evidence Explorer"
        subtitle="Campanhas, proveniência, artefactos, hashes e comparação sem promover evidência incompleta."
        helpTopic="evidence"
      />
      <section className="ui-panel">
        <h2>Campanhas autorizadas</h2>
        <div className="ui-grid ui-grid-wide">
          {campaigns.map((definition) => (
            <OperationLauncher key={definition.operationId} definition={definition} />
          ))}
        </div>
      </section>
      <section className="ui-panel">
        <h2>Comparar execuções</h2>
        <div className="ui-compare-row">
          <select value={left} onChange={(event) => setLeft(event.target.value)}>
            <option value="">Esquerda</option>
            {evidenceRuns.map((operation) => (
              <option key={`l-${operation.id}`} value={operation.id}>
                {operation.displayName} · {operation.status}
              </option>
            ))}
          </select>
          <select value={right} onChange={(event) => setRight(event.target.value)}>
            <option value="">Direita</option>
            {evidenceRuns.map((operation) => (
              <option key={`r-${operation.id}`} value={operation.id}>
                {operation.displayName} · {operation.status}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="ui-button"
            disabled={!left || !right || left === right}
            onClick={runComparison}
          >
            <GitCompareArrows size={16} /> Comparar
          </button>
        </div>
        {error && <p className="ui-notice ui-error">{error}</p>}
        <ComparisonBarChart comparison={comparison} />
      </section>
      <section className="ui-panel">
        <h2>Execuções e artefactos</h2>
        <div className="ui-grid">
          {evidenceRuns.map((operation) => (
            <OperationStatus key={operation.id} operation={operation} />
          ))}
        </div>
      </section>
    </section>
  );
}
