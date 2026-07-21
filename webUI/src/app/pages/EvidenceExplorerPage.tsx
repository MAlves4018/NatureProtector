import { Download, FileCheck2, GitCompareArrows, ShieldCheck, FileText, TableProperties, Ratio, History } from 'lucide-react';
import { useMemo, useState } from 'react';
import type { OperationComparisonResponse } from '../types/operations';
import { PageHeader } from '../components/PageHeader';
import { ComparisonBarChart } from '../components/ComparisonBarChart';
import { OperationLauncher } from '../operations/OperationLauncher';
import { OperationStatus } from '../operations/OperationStatus';
import { useOperations } from '../operations/OperationsContext';
import { api } from '../services/api';
import { useUiCapabilities } from '../state/CapabilityContext';
import { useUiObservability } from '../state/ObservabilityContext';
import { useUiActivity } from '../state/ActivityContext';
import { evidenceIdentityMatchesRun, normalizeEvidenceCatalog } from '../utils/operationalMetrics';
import { Claims, EvidenceMetric } from '../components/Claims';

export function EvidenceExplorerPage() {
  const { catalog, operations, compare } = useOperations();
  const { evidenceCatalog, observabilityError } = useUiObservability();
  const { selectedRunId, runAudit, runTimings, runOperation } = useUiActivity();
  const { capabilities } = useUiCapabilities();
  const campaigns = catalog.filter((definition) => definition.category === 'evidence');
  const evidenceRuns = operations.filter(
    (operation) => operation.category === 'evidence' || operation.artifacts.length > 0,
  );
  const runtimeEvidence = useMemo(
    () => normalizeEvidenceCatalog(evidenceCatalog?.items ?? []),
    [evidenceCatalog?.items],
  );
  const scopedEvidence = useMemo(
    () =>
      runtimeEvidence.filter((item) => {
        if (!selectedRunId) return false;
        return evidenceIdentityMatchesRun(item.id, item.scope, selectedRunId, runOperation?.evidenceId);
      }),
    [runtimeEvidence, selectedRunId, runOperation?.evidenceId],
  );
  const [selectedCampaignId, setSelectedCampaignId] = useState('');
  const [left, setLeft] = useState('');
  const [right, setRight] = useState('');
  const [comparison, setComparison] = useState<OperationComparisonResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [downloadMessage, setDownloadMessage] = useState<string | null>(null);
  const selectedCampaign = campaigns.find((campaign) => campaign.operationId === selectedCampaignId) ?? null;
  const [tab, setTab] = useState<'claims' | 'artefacts' | 'campaigns' | 'comparison' | 'history'>('claims');

  const runComparison = async () => {
    setError(null);
    try {
      setComparison(await compare(left, right));
    } catch (value) {
      setError(value instanceof Error ? value.message : 'A comparação falhou.');
    }
  };

  const download = async (evidenceId: string) => {
    setDownloadMessage(null);
    try {
      const result = await api.downloadRuntimeEvidence(evidenceId);
      const url = URL.createObjectURL(result.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = result.filename ?? `${evidenceId}.bin`;
      anchor.click();
      URL.revokeObjectURL(url);
      setDownloadMessage(`Artefacto ${evidenceId} transferido.`);
    } catch (value) {
      setDownloadMessage(value instanceof Error ? value.message : 'Não foi possível transferir o artefacto.');
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Cockpit de evidência"
        subtitle="Versão, ambiente, execução, artefactos e limites mantêm-se distintos de claims não provadas."
        helpTopic="evidence"
      />
      <section className="ui-metric-grid">
        <EvidenceMetric label="Campanhas no catálogo" value={campaigns.length} />
        <EvidenceMetric label="Execuções registadas" value={evidenceRuns.length} />
        <EvidenceMetric label="Artefactos da run" value={scopedEvidence.length} />
        <EvidenceMetric label="Transferíveis" value={scopedEvidence.filter((item) => item.downloadable).length} />
      </section>
      <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
        <button
          type="button"
          className={tab === 'claims' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'claims'}
          onClick={() => setTab('claims')}
        >
          <FileText size={16} />
          Claims
        </button>
        <button
          type="button"
          className={tab === 'artefacts' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'artefacts'}
          onClick={() => setTab('artefacts')}
        >
          <TableProperties size={16} />
          Artefactos
        </button>
        <button
          type="button"
          className={tab === 'campaigns' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'campaigns'}
          onClick={() => setTab('campaigns')}
        >
          <ShieldCheck size={16} />
          Campanhas
        </button>
        <button
          type="button"
          className={tab === 'comparison' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'comparison'}
          onClick={() => setTab('comparison')}
        >
          <Ratio size={16} />
          Comparar
        </button>
        <button
          type="button"
          className={tab === 'history' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'history'}
          onClick={() => setTab('history')}
        >
          <History size={16} />
          Histórico
        </button>
      </div>
      {tab === 'claims' && (
        <Claims 
          selectedRunId={selectedRunId}
          runAudit={runAudit}
          runOperation={runOperation}
          runTimings={runTimings}
        />
      )}
      {tab === 'artefacts' && (
        <section className="ui-card">
          <div className="ui-section-heading">
            <div>
              <span className="ui-eyebrow">Índice normalizado</span>
              <h3>Artefactos runtime disponíveis</h3>
            </div>
            <ShieldCheck size={22} />
          </div>
          <p className="ui-notice">
            O índice é filtrado pela run selecionada. Artefactos sem SimulationRunId ou EvidenceId correspondente não são
            apresentados como prova desta execução.
          </p>
          {observabilityError && <p className="ui-notice ui-error">{observabilityError.message}</p>}
          <div className="ui-table-wrap">
            <table className="ui-table">
              <thead>
                <tr>
                  <th>Artefacto</th>
                  <th>Classe</th>
                  <th>Ambiente / scope</th>
                  <th>Versão</th>
                  <th>Estado</th>
                  <th>Ação</th>
                </tr>
              </thead>
              <tbody>
                {scopedEvidence.length === 0 ? (
                  <tr>
                    <td colSpan={6}>O runtime não publicou artefactos consultáveis.</td>
                  </tr>
                ) : (
                  scopedEvidence.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <strong>{item.title}</strong>
                        <small className="ui-table-note">{item.generatedAt ?? 'Data não registada'}</small>
                      </td>
                      <td>{item.evidenceClass}</td>
                      <td>
                        {item.environment}
                        <small className="ui-table-note">{item.scope}</small>
                      </td>
                      <td>{item.version ?? 'Não registada'}</td>
                      <td>
                        {item.status}
                        {item.limitation && <small className="ui-table-note">{item.limitation}</small>}
                      </td>
                      <td>
                        <button
                          type="button"
                          className="ui-secondary"
                          disabled={!item.downloadable || !capabilities.has('evidence.download')}
                          onClick={() => void download(item.id)}
                        >
                          <Download size={14} /> Transferir
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          {downloadMessage && (
            <p className="ui-notice" role="status">
              {downloadMessage}
            </p>
          )}
        </section>
      )}
      {tab === 'campaigns' && (
        <section className="ui-card">
          <div className="ui-section-heading">
            <div>
              <span className="ui-eyebrow">Campanhas governadas</span>
              <h3>Catálogo e execução</h3>
            </div>
            <FileCheck2 size={22} />
          </div>
          <div className="ui-compact-list">
            {campaigns.length === 0 ? (
              <p className="ui-notice">Sem campanhas autorizadas para este perfil.</p>
            ) : (
              campaigns.map((campaign) => (
                <button
                  type="button"
                  key={campaign.operationId}
                  className={
                    selectedCampaignId === campaign.operationId
                      ? 'ui-compact-row ui-compact-row-active'
                      : 'ui-compact-row'
                  }
                  onClick={() => setSelectedCampaignId(campaign.operationId)}
                >
                  <span>
                    <strong>{campaign.displayName}</strong>
                    <small>{campaign.description}</small>
                  </span>
                  <span>{campaign.authorized ? campaign.availability : `Requer ${campaign.requiredCapability}`}</span>
                </button>
              ))
            )}
          </div>
          {selectedCampaign && (
            <details className="ui-details" open>
              <summary>Preparar {selectedCampaign.displayName}</summary>
              <OperationLauncher definition={selectedCampaign} showTruthWarning={false} />
            </details>
          )}
        </section>
      )}
      {tab === 'comparison' && (
        <section className="ui-card">
          <h3>Comparar execuções de evidence</h3>
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
              onClick={() => void runComparison()}
            >
              <GitCompareArrows size={16} /> Comparar
            </button>
          </div>
          {error && <p className="ui-notice ui-error">{error}</p>}
          <ComparisonBarChart comparison={comparison} />
        </section>
      )}
      {tab === 'history' && (
        <section className="ui-card">
          <h3>Histórico auditável</h3>
          {evidenceRuns.length === 0 ? (
            <p className="ui-notice">Sem execuções de evidence registadas.</p>
          ) : (
            <div className="ui-grid">
              {evidenceRuns.map((operation) => (
                <OperationStatus key={operation.id} operation={operation} compact />
              ))}
            </div>
          )}
        </section>
      )}
    </section>
  );
}