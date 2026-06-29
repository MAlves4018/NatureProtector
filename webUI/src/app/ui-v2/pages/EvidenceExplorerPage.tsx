import { useState } from 'react';
import { GitCompareArrows } from 'lucide-react';
import type { OperationComparisonResponse } from '../../types';
import { PageHeader } from '../components/PageHeader';
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
      setError(value instanceof Error ? value.message : 'Comparison failed.');
    }
  };

  return (
    <section className="ui-v2-page">
      <PageHeader
        title="Evidence Explorer"
        subtitle="Campanhas, proveniência, artifacts, hashes e comparação sem promover evidence incompleta."
        helpTopic="evidence"
      />
      <section className="ui-v2-panel">
        <h2>Campanhas autorizadas</h2>
        <div className="ui-v2-grid ui-v2-grid-wide">
          {campaigns.map((definition) => (
            <OperationLauncher key={definition.operationId} definition={definition} />
          ))}
        </div>
      </section>
      <section className="ui-v2-panel">
        <h2>Comparar execuções</h2>
        <div className="ui-v2-compare-row">
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
            className="ui-v2-button"
            disabled={!left || !right || left === right}
            onClick={runComparison}
          >
            <GitCompareArrows size={16} /> Comparar
          </button>
        </div>
        {error && <p className="ui-v2-notice ui-v2-error">{error}</p>}
        {comparison && (
          <div className="ui-v2-card">
            <p>
              <strong>Estado:</strong> {comparison.leftStatus} → {comparison.rightStatus}
            </p>
            <p>
              <strong>Evidence:</strong> {comparison.evidenceLevel}
            </p>
            <p>
              <strong>Partilhados:</strong> {comparison.sharedArtifacts.join(', ') || 'nenhum artifact indexado'}
            </p>
            <p>
              <strong>Só esquerda:</strong> {comparison.onlyOnLeft.join(', ') || 'nenhum'}
            </p>
            <p>
              <strong>Só direita:</strong> {comparison.onlyOnRight.join(', ') || 'nenhum'}
            </p>
          </div>
        )}
      </section>
      <section className="ui-v2-panel">
        <h2>Execuções e artifacts</h2>
        <div className="ui-v2-grid">
          {evidenceRuns.map((operation) => (
            <OperationStatus key={operation.id} operation={operation} />
          ))}
        </div>
      </section>
    </section>
  );
}
