import { useEffect, useState } from 'react';
import { GitCompareArrows } from 'lucide-react';
import type { EngineeringOperationResponse, OperationComparisonResponse } from '../types/operations';
import { PageHeader } from '../components/PageHeader';
import { ComparisonBarChart } from '../components/ComparisonBarChart';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';

export function EvidenceExplorerPage() {
  const [latestScenarioOp, setLatestScenarioOp] = useState<{
    b: EngineeringOperationResponse | null;
    c: EngineeringOperationResponse | null;
  }>({ b: null, c: null });



  const { catalog, operations, compare } = useOperations();
  const campaigns = catalog.filter((definition) => definition.category === 'evidence');
  const evidenceRuns = operations.filter(
    (operation) => operation.category === 'evidence' || operation.artifacts.length > 0,
  );
  const [left, setLeft] = useState('');
  const [right, setRight] = useState('');
  const [comparison, setComparison] = useState<OperationComparisonResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (evidenceRuns.length === 0) return;

    const filterByScenario = (code: string) => {
      // Tenta encontrar pelo nome ou inputs que contenham o código
      const candidates = evidenceRuns.filter(
        (op) =>
          op.displayName.toLowerCase().includes(code) ||
          Object.values(op.inputs).some((v) => v?.toLowerCase().includes(code)) ||
          op.ref?.toLowerCase().includes(code),
      );
      // Devolve a mais recente (por requestedAt)
      return candidates.sort(
        (a, b) => new Date(b.requestedAt).getTime() - new Date(a.requestedAt).getTime(),
      )[0] ?? null;
    };

    setLatestScenarioOp({
      b: filterByScenario('scenario_b'),
      c: filterByScenario('scenario_c'),
    });
  }, [evidenceRuns]);

  const runComparison = async () => {
    setError(null);
    try {
      setComparison(await compare(left, right));
    } catch (value) {
      setError(value instanceof Error ? value.message : 'A comparação falhou.');
    }
  };

  const handleCompareBvsC = async () => {
    if (!latestScenarioOp.b || !latestScenarioOp.c) return;
    setLeft(latestScenarioOp.b.id);
    setRight(latestScenarioOp.c.id);
    // Dispara a comparação automaticamente
    setComparison(await compare(latestScenarioOp.b.id, latestScenarioOp.c.id));
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
      <section className="ui-panel">
        <h2>Comparação B vs C</h2>
        {latestScenarioOp.b && latestScenarioOp.c ? (
          <>
            <p>
              <strong>B:</strong> {latestScenarioOp.b.displayName} · {latestScenarioOp.b.status}
              <br />
              <strong>C:</strong> {latestScenarioOp.c.displayName} · {latestScenarioOp.c.status}
            </p>
            <button type="button" className="ui-button" onClick={handleCompareBvsC}>
              <GitCompareArrows size={16} /> Comparar B vs C
            </button>
            {comparison && <ComparisonBarChart comparison={comparison} />}
          </>
        ) : (
          <p className="ui-notice">
            {latestScenarioOp.b === null && latestScenarioOp.c === null
              ? 'Nenhuma operação de evidência encontrada para scenario_b ou scenario_c.'
              : latestScenarioOp.b === null
                ? 'Operação de scenario_b não encontrada.'
                : 'Operação de scenario_c não encontrada.'}
          </p>
        )}
      </section>
    </section>
  );
}


