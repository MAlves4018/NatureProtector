import { useCallback, useEffect, useState } from 'react';
import { ExternalLink, PlayCircle, RotateCw, RefreshCw, FileText } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { PageHeader } from '../components/PageHeader';
import { useUiQaTests } from '../state/QaTestContext';

interface LocalTestResult {
  Slug: string;
  Name: string;
  Status: string;
  ExitCode: number;
}

interface LocalTestSummary {
  Passed: number;
  Failed: number;
  Overall: string;
  Timestamp: string;
  Results: LocalTestResult[];
}

function suiteState(status: string) {
  if (status === 'Passed') return 'ready' as const;
  if (status.toLowerCase().includes('finding')) return 'partial' as const;
  return 'unknown' as const;
}

export function QaTestSuitePage() {
  const { canExecuteFullQa } = useUiCapabilities();
  const { qaSuites, runningSuiteIds, executions, pushResults, pushResultsLoading, runAll, runSuites, clearExecutions } = useUiQaTests();

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set(qaSuites.map((s) => s.suiteId)));

  const [localSummary, setLocalSummary] = useState<LocalTestSummary | null>(null);
  const [localLogs, setLocalLogs] = useState<Record<string, string>>({});
  const [localLoading, setLocalLoading] = useState(true);

  const loadLocalResults = useCallback(async () => {
    setLocalLoading(true);
    try {
      const res = await fetch('/testSuiteResults/_summary.json');
      if (!res.ok) { setLocalSummary(null); return; }
      const json: LocalTestSummary = await res.json();
      setLocalSummary(json);
      const logMap: Record<string, string> = {};
      for (const r of json.Results) {
        if (r.Status !== 'failed') continue;
        try {
          const logRes = await fetch(`/testSuiteResults/${r.Slug}.log`);
          if (logRes.ok) logMap[r.Slug] = await logRes.text();
        } catch { /* skip */ }
      }
      setLocalLogs(logMap);
    } catch {
      setLocalSummary(null);
    } finally {
      setLocalLoading(false);
    }
  }, []);

  const [runningLocally, setRunningLocally] = useState(false);

  const [localError, setLocalError] = useState<string | null>(null);

  const runLocalTests = useCallback(async () => {
    setRunningLocally(true);
    setLocalError(null);
    try {
      const res = await fetch('/__local-run-tests', { method: 'POST' });
      if (res.ok) {
        await loadLocalResults();
      } else {
        const err = await res.json();
        setLocalError(err.details || err.error || 'Erro desconhecido');
      }
    } catch {
      setLocalError('Falhou ao contactar o servidor');
    } finally {
      setRunningLocally(false);
    }
  }, [loadLocalResults]);

  useEffect(() => { void loadLocalResults(); }, [loadLocalResults]);

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
  const isLocal = import.meta.env.DEV;

  const hasLocalResults = localSummary !== null && !localLoading;

  const subtitle = hasLocalResults
    ? 'Resultados locais da run-all-tests.ps1.'
    : 'Executa as suites de teste QA, ve resultados e historico de execucoes.';

  return (
    <section className="ui-page">
      <PageHeader
        title="QA Test Suite"
        subtitle={subtitle}
        helpTopic="qa"
      />

      {isLocal ? (
        <>
          <p className="ui-notice" style={{ marginBottom: 16 }}>
            <RefreshCw size={14} style={{ verticalAlign: 'middle', marginRight: 6 }} />
            Modo local — as opções de execução via API foram ocultadas.
            <button type="button" className="ui-secondary" style={{ marginLeft: 8 }} disabled={localLoading} onClick={() => void loadLocalResults()}>
              Recarregar resultados locais
            </button>
            <button
              type="button"
              className="ui-button"
              style={{ marginLeft: 8 }}
              disabled={runningLocally}
              onClick={() => void runLocalTests()}
            >
              {runningLocally ? <RotateCw size={14} className="ui-spin" /> : <PlayCircle size={14} />}
              {runningLocally ? 'A executar...' : 'Executar todas'}
            </button>
            {localError && (
              <p className="ui-notice" style={{ color: 'var(--ui-error)', marginTop: 8 }}>
                {localError}
              </p>
            )}
          </p>

          <section className="ui-card">
            <div className="ui-section-heading">
              <h3>Resultados Locais (run-all-tests.ps1)</h3>
            </div>
            <p className="ui-label" style={{ marginBottom: 8 }}>
              Resultados da última execução local da script <em>run-all-tests.ps1</em> guardados em <em>webUI/testSuiteResults/</em>.
            </p>
            {localLoading ? (
              <p className="ui-notice">A carregar resultados locais...</p>
            ) : !localSummary ? (
              <p className="ui-notice">
                Nenhum resultado local disponível. Executa <code>scripts/tests/run-all-tests.ps1</code> para gerar resultados.
              </p>
            ) : (
              <div style={{ display: 'grid', gap: 10 }}>
                <div className="ui-fact-list" style={{ margin: 0 }}>
                  <span><strong>Overall</strong> <StatusBadge label={localSummary.Overall} state={localSummary.Overall === 'passed' ? 'ready' : 'partial'} /></span>
                  <span><strong>Passed</strong> <span style={{ color: 'var(--ui-success)', fontWeight: 900 }}>{localSummary.Passed}</span></span>
                  <span><strong>Failed</strong> <span style={{ color: 'var(--ui-error)', fontWeight: 900 }}>{localSummary.Failed}</span></span>
                  <span><strong>Timestamp</strong> {new Date(localSummary.Timestamp).toLocaleString()}</span>
                </div>

                <div style={{ display: 'grid', gap: 6 }}>
                  {localSummary.Results.map((r) => {
                    const logContent = localLogs[r.Slug];
                    return (
                      <div
                        key={r.Slug}
                        className="ui-detail-row"
                        style={{
                          display: 'grid',
                          gap: 6,
                          padding: '8px 10px',
                        }}
                      >
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'space-between' }}>
                          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                            <StatusBadge
                              label={r.Status === 'passed' ? 'PASS' : 'FAIL'}
                              state={r.Status === 'passed' ? 'ready' : 'partial'}
                            />
                            <span style={{ fontWeight: 700 }}>{r.Name}</span>
                            {r.ExitCode !== 0 && (
                              <span className="ui-badge" style={{ fontSize: 11 }}>exit {r.ExitCode}</span>
                            )}
                          </div>
                          {logContent && (
                            <button
                              type="button"
                              className="ui-secondary"
                              style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 8px', fontSize: 12 }}
                              onClick={() => {
                                const el = document.getElementById(`log-${r.Slug}`);
                                if (el) el.hidden = !el.hidden;
                              }}
                            >
                              <FileText size={12} />
                              Log
                            </button>
                          )}
                        </div>
                        {logContent && (
                          <pre
                            id={`log-${r.Slug}`}
                            hidden
                            style={{
                              margin: 0,
                              padding: 8,
                              fontSize: 11,
                              lineHeight: 1.4,
                              maxHeight: 300,
                              overflow: 'auto',
                              background: 'var(--ui-surface-alt)',
                              border: '1px solid var(--ui-border)',
                              borderRadius: 4,
                              whiteSpace: 'pre-wrap',
                              wordBreak: 'break-word',
                            }}
                          >
                            {logContent.length > 5000 ? logContent.slice(0, 5000) + '\n... (truncado)' : logContent}
                          </pre>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </section>
        </>
      ) : (
        <>
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
                {pushResults.slice(0, 5).map((run) => {
                  let jobs: Record<string, { status: string; passed: number; failed: number; skipped: number; tests: number }> | null = null;
                  try {
                    const parsed = JSON.parse(run.detail ?? '{}');
                    if (parsed.jobs) jobs = parsed.jobs;
                  } catch { /* detail is plain text */ }

                  return (
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

                      {jobs && (
                        <details>
                          <summary style={{ cursor: 'pointer', fontWeight: 700 }}>
                            Resultados por job ({Object.keys(jobs).length})
                          </summary>
                          <div style={{ display: 'grid', gap: 6, marginTop: 8 }}>
                            {Object.entries(jobs).map(([name, j]) => (
                              <div
                                key={name}
                                className="ui-detail-row"
                                style={{
                                  display: 'grid',
                                  gridTemplateColumns: '1fr auto auto auto auto',
                                  gap: 8,
                                  alignItems: 'center',
                                  padding: '6px 8px',
                                }}
                              >
                                <span style={{ fontWeight: 700, fontSize: 13 }}>{name.replace(/-artifacts$/, '').replace(/-/g, ' ')}</span>
                                <StatusBadge
                                  label={j.status}
                                  state={j.status === 'Succeeded' ? 'ready' : 'partial'}
                                />
                                <span style={{ color: 'var(--ui-success)', fontWeight: 900, fontSize: 13 }}>
                                  P {j.passed}
                                </span>
                                {j.failed > 0 && (
                                  <span style={{ color: 'var(--ui-error)', fontWeight: 900, fontSize: 13 }}>
                                    F {j.failed}
                                  </span>
                                )}
                                {j.skipped > 0 && (
                                  <span style={{ color: 'var(--ui-warning)', fontWeight: 900, fontSize: 13 }}>
                                    S {j.skipped}
                                  </span>
                                )}
                                <span className="ui-label" style={{ fontSize: 12 }}>
                                  {j.tests} testes
                                </span>
                              </div>
                            ))}
                          </div>
                        </details>
                      )}

                      {run.artifacts.length > 0 && (
                        <details>
                          <summary style={{ cursor: 'pointer', fontWeight: 700 }}>
                            Evidências ({run.artifacts.length})
                          </summary>
                          <div style={{ display: 'grid', gap: 6, marginTop: 8 }}>
                            {run.artifacts.map((a) => (
                              <div key={a.artifactId} className="ui-detail-row" style={{ display: 'grid', gap: 4, padding: '6px 8px' }}>
                                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                                  <span style={{ fontWeight: 700, fontSize: 13 }}>{a.name}</span>
                                  <span className="ui-badge" style={{ fontSize: 11 }}>{a.kind}</span>
                                  {a.evidenceLevel && <span className="ui-badge" style={{ fontSize: 11 }}>{a.evidenceLevel}</span>}
                                </div>
                                <div className="ui-fact-list" style={{ margin: 0, fontSize: 12 }}>
                                  {a.sizeBytes != null && (
                                    <span><strong>Tamanho</strong> {(a.sizeBytes / 1024).toFixed(1)} KB</span>
                                  )}
                                  {a.reference?.startsWith('http') && (
                                    <a href={a.reference} target="_blank" rel="noreferrer">
                                      <ExternalLink size={12} /> Abrir
                                    </a>
                                  )}
                                </div>
                              </div>
                            ))}
                          </div>
                        </details>
                      )}

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
                  );
                })}
              </div>
            )}
          </section>
        </>
      )}
    </section>
  );
}
