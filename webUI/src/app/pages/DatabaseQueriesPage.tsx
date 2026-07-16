import { Database, Filter, Play, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { ExportActions } from '../components/ExportActions';
import { PageHeader } from '../components/PageHeader';
import { api } from '../services/api';
import { useUiActivity } from '../state/ActivityContext';
import { useUiArea } from '../state/AreaContext';
import type { RuntimeDiagnosticDefinitionResponse, RuntimeDiagnosticResultResponse } from '../types';
import { diagnosticResultToCsv } from '../utils/operationalMetrics';

const QUERY_METADATA: Record<string, { group: string; objective: string; source: string }> = {
  'runtime-table-counts': {
    group: 'Sistema',
    objective: 'Contagens atuais das tabelas runtime sem expor SQL livre.',
    source: 'PostgreSQL / control, pipeline e projection',
  },
  'active-runs': {
    group: 'Execução',
    objective: 'Identificar runs ainda ativas na área selecionada.',
    source: 'PostgreSQL / control.simulation_runs',
  },
  'latest-runs': {
    group: 'Execução',
    objective: 'Consultar as execuções persistidas mais recentes.',
    source: 'PostgreSQL / control.simulation_runs',
  },
  'inbox-by-status': {
    group: 'Pipeline',
    objective: 'Distribuição dos eventos por estado de inbox.',
    source: 'PostgreSQL / pipeline.event_inbox',
  },
  'attempts-by-outcome': {
    group: 'Pipeline',
    objective: 'Resultados das tentativas de processamento.',
    source: 'PostgreSQL / pipeline.processing_attempts',
  },
  'failed-attempts-by-error': {
    group: 'Falhas',
    objective: 'Erros recentes agrupados por código.',
    source: 'PostgreSQL / pipeline.processing_attempts',
  },
  'latest-rejected-events': {
    group: 'Falhas',
    objective: 'Eventos rejeitados mais recentes e respetivo motivo.',
    source: 'PostgreSQL / pipeline.rejected_events',
  },
  'latest-quarantined-events': {
    group: 'Falhas',
    objective: 'Eventos colocados em quarentena e respetivo motivo.',
    source: 'PostgreSQL / pipeline.quarantined_events',
  },
  'latest-run-expected-vs-observed': {
    group: 'Accounting',
    objective: 'Reconciliar eventos esperados, aceites, avaliados e ausentes.',
    source: 'PostgreSQL / run-scoped accounting',
  },
  'latest-run-events-by-cycle': {
    group: 'Timeline',
    objective: 'Observar eventos por ciclo da última run.',
    source: 'PostgreSQL / run-scoped pipeline',
  },
  'latest-run-quality-by-profile': {
    group: 'Qualidade',
    objective: 'Comparar qualidade e elegibilidade por perfil.',
    source: 'PostgreSQL / risk assessment log',
  },
  'latest-run-coverage-freshness': {
    group: 'Qualidade',
    objective: 'Consultar cobertura e atualidade dos estados operacionais.',
    source: 'PostgreSQL / operational projections',
  },
  'active-alerts': {
    group: 'Alertas',
    objective: 'Listar alertas ativos para a área.',
    source: 'PostgreSQL / projection.alert_state',
  },
  'compare-latest-b-vs-c': {
    group: 'Comparação',
    objective: 'Comparar as últimas execuções B e C com métricas persistidas.',
    source: 'PostgreSQL / diagnostics allowlisted',
  },
};

export function DatabaseQueriesPage() {
  const { resolvedAreaCode } = useUiArea();
  const { selectedScenarioCode } = useUiActivity();
  const [catalog, setCatalog] = useState<RuntimeDiagnosticDefinitionResponse[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [recentMinutes, setRecentMinutes] = useState(30);
  const [search, setSearch] = useState('');
  const [result, setResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .getRuntimeDiagnostics()
      .then((response) => {
        if (cancelled) return;
        setCatalog(response.diagnostics);
        setSelectedId((current) => current || response.diagnostics[0]?.id || '');
      })
      .catch((value) => {
        if (!cancelled) setError(value instanceof Error ? value.message : 'Não foi possível carregar o catálogo.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const visibleQueries = useMemo(() => {
    const needle = search.trim().toLowerCase();
    return catalog.filter((definition) => {
      const metadata = metadataFor(definition);
      return (
        !needle || `${definition.title} ${definition.description} ${metadata.group}`.toLowerCase().includes(needle)
      );
    });
  }, [catalog, search]);

  const selected = catalog.find((definition) => definition.id === selectedId) ?? null;
  const selectedMetadata = selected ? metadataFor(selected) : null;

  const execute = async () => {
    if (!selected) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      setResult(
        await api.executeRuntimeDiagnostic(selected.id, {
          areaCode: resolvedAreaCode,
          recentMinutes,
          scenarioCode: selectedScenarioCode || null,
        }),
      );
    } catch (value) {
      setError(value instanceof Error ? value.message : 'A consulta preparada falhou.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Consultas preparadas"
        subtitle="Diagnósticos read-only e allowlisted. A interface nunca aceita SQL ou InfluxQL livre."
        helpTopic="pipeline"
      />
      <section className="ui-query-layout">
        <aside className="ui-card ui-query-library">
          <div className="ui-section-heading">
            <h3>Biblioteca</h3>
            <span className="ui-badge">{catalog.length} disponíveis</span>
          </div>
          <label className="ui-field">
            <span>Filtrar consultas</span>
            <span className="ui-input-with-icon">
              <Search size={15} />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nome ou grupo" />
            </span>
          </label>
          <div className="ui-query-list">
            {visibleQueries.map((definition) => {
              const metadata = metadataFor(definition);
              return (
                <button
                  key={definition.id}
                  type="button"
                  className={selectedId === definition.id ? 'ui-query-item ui-query-item-active' : 'ui-query-item'}
                  onClick={() => setSelectedId(definition.id)}
                >
                  <span>{metadata.group}</span>
                  <strong>{definition.title}</strong>
                  <small>{metadata.objective}</small>
                </button>
              );
            })}
          </div>
        </aside>
        <div className="ui-query-workspace">
          <section className="ui-card">
            <div className="ui-section-heading">
              <div>
                <span className="ui-eyebrow">{selectedMetadata?.group ?? 'Consulta'}</span>
                <h3>{selected?.title ?? 'Selecione uma consulta'}</h3>
              </div>
              <Database size={22} />
            </div>
            <p>{selectedMetadata?.objective ?? 'O catálogo ainda não devolveu consultas.'}</p>
            {selected && (
              <dl className="ui-definition-list">
                <dt>Fonte</dt>
                <dd>{selectedMetadata?.source}</dd>
                <dt>Parâmetros</dt>
                <dd>Área, janela temporal e cenário selecionados na interface</dd>
                <dt>Limite</dt>
                <dd>{selected.description}</dd>
              </dl>
            )}
            <div className="ui-query-parameters">
              <label className="ui-field">
                <span>Área</span>
                <input value={resolvedAreaCode ?? ''} readOnly />
              </label>
              <label className="ui-field">
                <span>Janela recente</span>
                <select value={recentMinutes} onChange={(event) => setRecentMinutes(Number(event.target.value))}>
                  <option value={15}>15 minutos</option>
                  <option value={30}>30 minutos</option>
                  <option value={60}>60 minutos</option>
                  <option value={1440}>24 horas</option>
                </select>
              </label>
              <label className="ui-field">
                <span>Cenário</span>
                <input value={selectedScenarioCode || 'Não filtrado'} readOnly />
              </label>
            </div>
            <button type="button" className="ui-button" disabled={!selected || loading} onClick={() => void execute()}>
              {loading ? <Filter size={16} /> : <Play size={16} />}
              {loading ? 'A consultar…' : 'Executar consulta preparada'}
            </button>
            {error && <p className="ui-notice ui-error">{error}</p>}
          </section>
          <section className="ui-card">
            <div className="ui-section-heading">
              <h3>Resultado</h3>
              {result && <ExportActions filename={`${result.id}.csv`} content={diagnosticResultToCsv(result)} />}
            </div>
            {!result && !error && <p className="ui-notice">Execute uma consulta para obter resultados persistidos.</p>}
            {result && (
              <>
                <div className="ui-table-wrap">
                  <table className="ui-table">
                    <thead>
                      <tr>
                        {result.columns.map((column) => (
                          <th key={column}>{column}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {result.rows.length === 0 ? (
                        <tr>
                          <td colSpan={Math.max(1, result.columns.length)}>A consulta não encontrou rows.</td>
                        </tr>
                      ) : (
                        keyedRows(result.rows, result.columns).map(({ key, row }) => (
                          <tr key={key}>
                            {result.columns.map((column) => (
                              <td key={column}>{row[column] ?? 'Não medido'}</td>
                            ))}
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
                {result.limitations.length > 0 && (
                  <div className="ui-notice ui-warning">
                    <strong>Limitações</strong>
                    <ul>
                      {result.limitations.map((limitation) => (
                        <li key={limitation}>{limitation}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )}
          </section>
        </div>
      </section>
    </section>
  );
}

function keyedRows(rows: Array<Record<string, string | null>>, columns: string[]) {
  const occurrences = new Map<string, number>();

  return rows.map((row) => {
    const contentKey = columns.map((column) => JSON.stringify(row[column] ?? null)).join('|');
    const occurrence = (occurrences.get(contentKey) ?? 0) + 1;
    occurrences.set(contentKey, occurrence);
    return { key: `${contentKey}:${occurrence}`, row };
  });
}

function metadataFor(definition: RuntimeDiagnosticDefinitionResponse) {
  return (
    QUERY_METADATA[definition.id] ?? {
      group: 'Análise',
      objective: definition.description,
      source: 'PostgreSQL / diagnostic allowlisted pelo backend',
    }
  );
}
