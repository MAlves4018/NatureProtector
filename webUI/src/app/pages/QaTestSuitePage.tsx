import { useState } from 'react';
import { ExternalLink, PlayCircle, RotateCw } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiCapabilities } from '../state/CapabilityContext';
import { useUiQaTests } from '../state/QaTestContext';

export function QaTestSuitePage() {
  const { canExecuteFullQa } = useUiCapabilities();
  const { qaSuites, runningSuiteIds, executions, pushResults, pushResultsLoading, runAll, runSuites, clearExecutions } = useUiQaTests();

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set(qaSuites.map((s) => s.suiteId)));

  const toggleSuite = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const isRunning = runningSuiteIds.size > 0;
  const allSelected = qaSuites.every((s) => selectedIds.has(s.suiteId));

  return (
    <section className="ui-page">
      <PageHeader
        title="QA Test Suite"
        subtitle="Executa as suites de teste QA, ve resultados e historico de execucoes."
        helpTopic="qa"
      />

      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Executar todas as suites QA</h3>
          {isRunning && <StatusBadge label="A executar..." state="partial" />}
        </div>
        <p>
          Corre as {qaSuites.length} suites de teste QA sequencialmente: {qaSuites.map((s) => s.suiteName).join(', ')}.
        </p>
        <button
          type="button"
          className="ui-button"
          disabled={isRunning || !canExecuteFullQa}
          onClick={() => void runAll()}
        >
          {isRunning ? <RotateCw size={16} className="ui-spin" /> : <PlayCircle size={16} />}
          {isRunning ? 'A executar...' : `Executar todas (${qaSuites.length} suites)`}
        </button>
        {!canExecuteFullQa && (
          <p className="ui-notice" style={{ marginTop: 8 }}>
            O perfil atual nao tem permissao para executar suites de teste completas.
          </p>
        )}
      </section>

      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Executar por suite</h3>
        </div>
        <div style={{ display: 'grid', gap: 8, marginBottom: 12 }}>
          {qaSuites.map((suite) => {
            const suiteRunning = runningSuiteIds.has(suite.suiteId);
            return (
              <label key={suite.suiteId} className="ui-check-row" style={{ alignItems: 'center' }}>
                <input
                  type="checkbox"
                  checked={selectedIds.has(suite.suiteId)}
                  disabled={isRunning}
                  onChange={() => toggleSuite(suite.suiteId)}
                />
                <div style={{ display: 'grid', gap: 2 }}>
                  <span style={{ fontWeight: 700 }}>{suite.suiteName}</span>
                  <span className="ui-label" style={{ margin: 0 }}>
                    {suite.category}
                  </span>
                </div>
                {suiteRunning && <RotateCw size={14} className="ui-spin" />}
                <StatusBadge label={suite.status} state={suite.status === 'Passed' ? 'ready' : 'partial'} />
              </label>
            );
          })}
        </div>
        <div className="ui-button-row">
          <button
            type="button"
            className="ui-button"
            disabled={isRunning || selectedIds.size === 0}
            onClick={() => void runSuites(Array.from(selectedIds))}
          >
            <PlayCircle size={16} />
            Executar selecionadas ({selectedIds.size})
          </button>
          <button
            type="button"
            className="ui-secondary"
            disabled={isRunning}
            onClick={() => {
              if (allSelected) setSelectedIds(new Set());
              else setSelectedIds(new Set(qaSuites.map((s) => s.suiteId)));
            }}
          >
            {allSelected ? 'Desmarcar todas' : 'Selecionar todas'}
          </button>
        </div>
      </section>

      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Execucoes anteriores ({executions.length})</h3>
          {executions.length > 0 && (
            <button type="button" className="ui-secondary" onClick={clearExecutions}>
              Limpar historico
            </button>
          )}
        </div>
        {executions.length === 0 ? (
          <p className="ui-notice">Nenhuma execucao registada. Executa suites para gerar resultados.</p>
        ) : (
          <div style={{ display: 'grid', gap: 14 }}>
            {executions.map((exec) => (
              <article key={exec.executionId} className="ui-operation-card" style={{ display: 'grid', gap: 10 }}>
                <div className="ui-section-heading">
                  <h4>Execucao {new Date(exec.startedAt).toLocaleString()}</h4>
                  <StatusBadge
                    label={`${exec.suites.filter((s) => s.status === 'Passed').length}/${exec.suites.length} passed`}
                    state="ready"
                  />
                </div>
                <div
                  className="ui-fact-list"
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))',
                    gap: 8,
                    margin: 0,
                  }}
                >
                  <span>
                    <strong>Duracao</strong> {(exec.durationMs / 1000).toFixed(1)}s
                  </span>
                  <span>
                    <strong>Total passed</strong>
                    <span style={{ color: 'var(--ui-success)', fontWeight: 900 }}>
                      {exec.suites.reduce((s, su) => s + (su.passed ?? 0), 0)}
                    </span>
                  </span>
                  <span>
                    <strong>Total failed</strong>
                    <span style={{ color: 'var(--ui-error)', fontWeight: 900 }}>
                      {exec.suites.reduce((s, su) => s + (su.failed ?? 0), 0)}
                    </span>
                  </span>
                </div>
                <details>
                  <summary style={{ cursor: 'pointer', fontWeight: 700 }}>
                    Detalhes por suite ({exec.suites.length})
                  </summary>
                  <div style={{ display: 'grid', gap: 8, marginTop: 8 }}>
                    {exec.suites.map((suite) => (
                      <div
                        key={suite.suiteId}
                        className="ui-detail-row"
                        style={{ display: 'grid', gridTemplateColumns: '1fr auto auto', gap: 8, alignItems: 'center' }}
                      >
                        <span style={{ fontWeight: 700 }}>{suite.suiteName}</span>
                        <span style={{ display: 'flex', gap: 8 }}>
                          <span style={{ color: 'var(--ui-success)', fontWeight: 900 }}>P {suite.passed ?? '-'}</span>
                          {(suite.failed ?? 0) > 0 && (
                            <span style={{ color: 'var(--ui-error)', fontWeight: 900 }}>F {suite.failed}</span>
                          )}
                          {(suite.skipped ?? 0) > 0 && (
                            <span style={{ color: 'var(--ui-warning)', fontWeight: 900 }}>S {suite.skipped}</span>
                          )}
                        </span>
                        <span style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                          <StatusBadge label={suite.status} state={suite.status === 'Passed' ? 'ready' : 'partial'} />
                          {suite.coverage !== 'Not applicable' && suite.coverage !== 'N/A' && (
                            <span className="ui-badge">{suite.coverage}</span>
                          )}
                        </span>
                      </div>
                    ))}
                  </div>
                </details>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Resultados do Push CI</h3>
          {pushResultsLoading && <StatusBadge label="A carregar..." state="partial" />}
        </div>
        <p className="ui-label" style={{ marginBottom: 8 }}>
          Resultados da última execução do workflow <em>engineering-foundations.yml</em> despoletada por push no branch master.
        </p>
        {pushResults.length === 0 ? (
          <p className="ui-notice">
            {pushResultsLoading ? 'A carregar resultados dos pushes...' : 'Nenhum resultado de push CI disponível. Os resultados aparecem após um push que complete o workflow.'}
          </p>
        ) : (
          <div style={{ display: 'grid', gap: 10 }}>
            {pushResults.slice(0, 5).map((run) => (
              <article key={run.id} className="ui-operation-card" style={{ display: 'grid', gap: 8 }}>
                <div className="ui-section-heading">
                  <h4>Push CI — {new Date(run.updatedAt).toLocaleString()}</h4>
                  <StatusBadge
                    label={run.status}
                    state={run.status === 'Succeeded' ? 'ready' : run.status === 'Failed' ? 'partial' : 'partial'}
                  />
                </div>
                <div className="ui-fact-list" style={{ margin: 0 }}>
                  <span>
                    <strong>Branch</strong> {run.ref}
                  </span>
                  <span>
                    <strong>Workflow</strong> {run.workflow ?? 'engineering-foundations.yml'}
                  </span>
                  <span>
                    <strong>Estado</strong> {run.status}
                  </span>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  {run.providerReference?.startsWith('http') && (
                    <a
                      href={run.providerReference}
                      target="_blank"
                      rel="noreferrer"
                      className="ui-secondary"
                      style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '4px 10px', fontSize: 13 }}
                    >
                      <ExternalLink size={14} />
                      Abrir no GitHub
                    </a>
                  )}
                  {run.evidenceLevel && (
                    <span className="ui-badge">{run.evidenceLevel}</span>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </section>
  );
}
