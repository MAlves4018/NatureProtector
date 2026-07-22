import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useCallback,
  type ReactNode,
  type MouseEvent as ReactMouseEvent,
} from 'react';
import { PageHeader } from '../components/PageHeader';
import { api } from '../services/api';
import { ROQueryResponse, ROQueryRequest } from '../types';
import { Database, Play, Search, ChevronDown, ChevronRight } from 'lucide-react';
import { ExportActions } from '../components/ExportActions';
import { useUiActivity } from '../state/ActivityContext';
import { useUiArea } from '../state/AreaContext';
import type { RuntimeDiagnosticResultResponse } from '../types';
import {
  diagnosticResultToCsv,
  elapsedMs,
  evidenceIdentityMatchesRun,
  throughputPerSecond,
} from '../utils/operationalMetrics';

interface PreparedQuery {
  id: string;
  group: string;
  title: string;
  objective: string;
  source: string;
}

const PREPARED_QUERIES: PreparedQuery[] = [
  query(
    'run-summary',
    'Execução',
    'Resumo da run',
    'Identidade, configuração e estado persistido.',
    'GET /runtime/runs/{SimulationRunId}',
  ),
  query(
    'accounting',
    'Accounting',
    'Convergência do accounting',
    'Expected, accepted, processed, pending e settled.',
    'GET /runtime/runs/{id}/audit + /operation',
  ),
  query(
    'np-score',
    'Índices',
    'NP Score',
    'Snapshot persistido do NP Score e componentes.',
    'GET /runtime/runs/{id}/audit',
  ),
  query('fwi', 'Índices', 'FWI', 'FWI persistido, origem e classe.', 'GET /runtime/runs/{id}/audit'),
  query('kbdi', 'Índices', 'KBDI', 'KBDI persistido, origem e qualidade do histórico.', 'GET /runtime/runs/{id}/audit'),
  query(
    'portuguese-proxy',
    'Índices',
    'Portuguese Proxy',
    'Proxy candidato e respetiva proveniência.',
    'GET /runtime/runs/{id}/audit',
  ),
  query(
    'sensor-quality',
    'Qualidade',
    'Qualidade por sensor',
    'Flags de qualidade agregadas na run.',
    'GET /runtime/runs/{id}/audit',
  ),
  query(
    'integrity',
    'Qualidade',
    'Integridade, confidence e coverage',
    'Fatores persistidos e cobertura observada.',
    'GET /runtime/runs/{id}/audit',
  ),
  query(
    'latency',
    'Performance',
    'Latências do pipeline',
    'Timestamps e distribuição das tentativas.',
    'GET /runtime/runs/{id}/timings',
  ),
  query(
    'throughput',
    'Performance',
    'Throughput',
    'Observações e avaliações por segundo.',
    'GET /runtime/runs/{id}/audit + /timings',
  ),
  query(
    'rabbitmq',
    'Pipeline',
    'Backlog RabbitMQ',
    'Ready, unacknowledged, total e consumers observados.',
    'GET /runtime/observability/rabbitmq',
  ),
  query(
    'retries',
    'Pipeline',
    'Retries e quarantine',
    'Retries e quarantine isolados pela run.',
    'GET /runtime/runs/{id}/audit + /operation',
  ),
  query(
    'alerts',
    'Alertas',
    'Alertas da run',
    'Primeiro alerta associado e limitação do contrato run-scoped.',
    'GET /runtime/runs/{id}/timings',
  ),
  query(
    'evidence',
    'Evidence',
    'Evidence disponível',
    'Artefactos cujo scope ou identidade corresponde à run.',
    'GET /runtime/observability/evidence',
  ),
  query(
    'phases',
    'Lifecycle',
    'Estado e duração de cada fase',
    'Timeline persistida e lifecycle operacional.',
    'GET /runtime/runs/{id}/timings + /operation',
  ),
];

export function DatabaseQueriesPage() {
  const { resolvedAreaCode } = useUiArea();
  const { selectedRunId, selectedRun } = useUiActivity();
  const [selectedId, setSelectedId] = useState(PREPARED_QUERIES[0].id);
  const [search, setSearch] = useState('');
  const [tables, setTables] = useState<string[]>([]);
  const [selectedTable, setSelectedTable] = useState('');
  const [tableColumns, setTableColumns] = useState<string[]>([]);
  const [selectedColumns, setSelectedColumns] = useState<Set<string>>(new Set());
  const [queryType, setQueryType] = useState<'select' | 'count'>('select');
  const [queryLimit, setQueryLimit] = useState(100);
  const [queryOffset, setQueryOffset] = useState(0);
  const [resultWrittenQuery, setWrittenQueryResult] = useState<ROQueryResponse | null>(null);
  const [resultPremadeQuery, setPremadeQueryResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [resultRunId, setResultRunId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [executing, setExecuting] = useState(false);
  const [errorPremade, setErrorPremade] = useState<string | null>(null);
  const [errorWritten, setErrorWritten] = useState<string | null>(null);
  const [tab, setTab] = useState<'prepared' | 'written'>('prepared');
  const writtenColumns = resultWrittenQuery?.columns ?? [];
  const {
    widths: widthsWritten,
    setThRef: setThRefWritten,
    startResize: startResizeWritten,
  } = useColumnResize(writtenColumns);
  const requestGeneration = useRef(0);
  const previousRunId = useRef(selectedRunId);
  const selected = PREPARED_QUERIES.find((item) => item.id === selectedId) ?? PREPARED_QUERIES[0];
  const visibleQueries = useMemo(() => {
    const needle = search.trim().toLowerCase();
    return PREPARED_QUERIES.filter(
      (item) => !needle || `${item.title} ${item.group} ${item.objective}`.toLowerCase().includes(needle),
    );
  }, [search]);

  useEffect(() => {
    if (previousRunId.current === selectedRunId) return;
    previousRunId.current = selectedRunId;
    requestGeneration.current += 1;
    setPremadeQueryResult(null);
    setWrittenQueryResult(null);
    setResultRunId(null);
    setErrorPremade(null);
    setErrorWritten(null);
  }, [selectedRunId]);

  useEffect(() => {
    api
      .getTablesPostgres()
      .then((result) => {
        setTables(result);
        if (result.length > 0 && !selectedTable) {
          setSelectedTable(result[0]);
        }
      })
      .catch(() => {});
  }, [selectedTable]);

  useEffect(() => {
    if (!selectedTable) return;
    api
      .getTableColumnsPostgres(selectedTable)
      .then((cols) => {
        setTableColumns(cols);
        setSelectedColumns(new Set(cols));
      })
      .catch(() => {
        setTableColumns([]);
        setSelectedColumns(new Set());
      });
  }, [selectedTable]);

  const handleExecutePreparedQuery = async () => {
    if (!selectedRunId) return;
    const requestedRunId = selectedRunId;
    const generation = ++requestGeneration.current;
    setLoading(true);
    setErrorPremade(null);
    try {
      const response = await executePreparedQuery(selected, requestedRunId);
      if (generation !== requestGeneration.current) return;
      setPremadeQueryResult(response);
      setResultRunId(requestedRunId);
    } catch (value) {
      if (generation !== requestGeneration.current) return;
      setErrorPremade(value instanceof Error ? value.message : 'A consulta preparada falhou.');
    } finally {
      if (generation === requestGeneration.current) setLoading(false);
    }
  };

  const executeBuiltQuery = useCallback(async () => {
    if (!selectedTable) return;
    setExecuting(true);
    setErrorWritten(null);
    setWrittenQueryResult(null);
    try {
      const chosenColumns =
        selectedColumns.size > 0 && selectedColumns.size < tableColumns.length ? [...selectedColumns] : undefined;
      const queryRequest: ROQueryRequest = {
        type: queryType,
        table: selectedTable,
        columns: chosenColumns,
        limit: queryType === 'count' ? undefined : queryLimit,
        offset: queryType === 'count' ? undefined : queryOffset,
      };
      const res = await api.postgresQuery(queryRequest);
      setWrittenQueryResult(res);
    } catch (e) {
      setErrorWritten(e instanceof Error ? e.message : 'Erro desconhecido');
    } finally {
      setExecuting(false);
    }
  }, [selectedTable, queryType, queryLimit, queryOffset, selectedColumns, tableColumns]);

  return (
    <section className="ui-page">
      <PageHeader
        title="Consultas à base de dados"
        subtitle="Consultas preparadas e bloco de escrita livre sobre os dados persistidos."
        helpTopic="pipeline"
      />

      <div className="ui-notice ui-warning" style={{ marginBottom: 16 }}>
        <strong>⚠ Aviso:</strong> Apenas queries de leitura permitidas. Operaçoes de escrita (INSERT, UPDATE, DELETE,
        DROP) sao bloqueadas.
      </div>

      <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
        <button
          type="button"
          className={tab === 'prepared' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'prepared'}
          onClick={() => setTab('prepared')}
        >
          <Database size={16} />
          Consultas Preparadas
        </button>
        <button
          type="button"
          className={tab === 'written' ? 'ui-segment-active' : 'ui-segment'}
          role="tab"
          aria-selected={tab === 'written'}
          onClick={() => setTab('written')}
        >
          <Play size={16} />
          Bloco de Escrita
        </button>
      </div>

      {tab === 'prepared' && (
        <section className="ui-card">
          <div className="ui-section-heading">
            <h2>Consultas Preparadas</h2>
            <span className="ui-badge">{PREPARED_QUERIES.length} presets</span>
          </div>
          <div className="ui-query-layout">
            <aside className="ui-query-library">
              <label className="ui-field">
                <span>Filtrar consultas</span>
                <span className="ui-input-with-icon">
                  <Search size={15} />
                  <input
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    placeholder="Nome ou grupo"
                  />
                </span>
              </label>
              <div className="ui-query-list">
                {visibleQueries.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    className={selectedId === item.id ? 'ui-query-item ui-query-item-active' : 'ui-query-item'}
                    onClick={() => {
                      requestGeneration.current += 1;
                      setSelectedId(item.id);
                      setPremadeQueryResult(null);
                      setResultRunId(null);
                      setLoading(false);
                    }}
                  >
                    <span>{item.group}</span>
                    <strong>{item.title}</strong>
                    <small>{item.objective}</small>
                  </button>
                ))}
              </div>
            </aside>
            <div className="ui-query-workspace">
              <div className="ui-card">
                <div className="ui-section-heading">
                  <div>
                    <span className="ui-eyebrow">{selected.group}</span>
                    <h3>{selected.title}</h3>
                  </div>
                  <Database size={22} />
                </div>
                <p>{selected.objective}</p>
                <dl className="ui-definition-list">
                  <dt>SimulationRunId</dt>
                  <dd>{selectedRunId || 'Selecione uma run em Execuções'}</dd>
                  <dt>Área / cenário</dt>
                  <dd>
                    {resolvedAreaCode ?? 'Indisponível'} / {selectedRun?.scenarioCode ?? 'Indisponível'}
                  </dd>
                  <dt>Fonte</dt>
                  <dd>{selected.source}</dd>
                  <dt>Filtro</dt>
                  <dd>Dados persistidos associados à run selecionada; sem SQL fornecido pelo utilizador.</dd>
                </dl>
                <button
                  type="button"
                  className="ui-button"
                  disabled={!selectedRunId || loading}
                  onClick={() => void handleExecutePreparedQuery()}
                >
                  <Play size={16} />
                  {loading ? 'A consultar…' : 'Executar preset'}
                </button>
                {errorPremade && <p className="ui-notice ui-error">{errorPremade}</p>}
              </div>
              <div className="ui-card">
                <div className="ui-section-heading">
                  <h3>Resultado</h3>
                  {resultPremadeQuery && (
                    <div className="ui-button-row">
                      <ExportActions
                        filename={`${resultPremadeQuery.id}-${resultRunId}.csv`}
                        content={diagnosticResultToCsv(resultPremadeQuery)}
                      />
                      <ExportActions
                        filename={`${resultPremadeQuery.id}-${resultRunId}.json`}
                        content={JSON.stringify({ simulationRunId: resultRunId, ...resultPremadeQuery }, null, 2)}
                        contentType="application/json;charset=utf-8"
                      />
                    </div>
                  )}
                </div>
                {!resultPremadeQuery && !errorPremade && (
                  <p className="ui-notice">Execute o preset para ler os endpoints live.</p>
                )}
                {resultPremadeQuery && <QueryResult result={resultPremadeQuery} runId={resultRunId} />}
              </div>
            </div>
          </div>
        </section>
      )}

      {tab === 'written' && (
        <section className="ui-card">
          <div className="ui-section-heading">
            <h2>Consulta Estruturada</h2>
          </div>
          <div className="ui-field">
            <span>Tabela</span>
            <select
              value={selectedTable}
              onChange={(e) => setSelectedTable(e.target.value)}
              disabled={executing || tables.length === 0}
            >
              {tables.length === 0 && <option value="">A carregar tabelas...</option>}
              {tables.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </div>
          <div className="ui-field">
            <span>Tipo</span>
            <select
              value={queryType}
              onChange={(e) => setQueryType(e.target.value as 'select' | 'count')}
              disabled={executing}
            >
              <option value="select">SELECT (com paginação)</option>
              <option value="count">COUNT (total de linhas)</option>
            </select>
          </div>
          {queryType === 'select' && (
            <div style={{ display: 'flex', gap: 16 }}>
              <label className="ui-field">
                <span>Limite (máx. 1000)</span>
                <input
                  type="number"
                  min={1}
                  max={1000}
                  value={queryLimit}
                  onChange={(e) => setQueryLimit(Number(e.target.value))}
                  disabled={executing}
                />
              </label>
              <label className="ui-field">
                <span>Offset (máx. 10000)</span>
                <input
                  type="number"
                  min={0}
                  max={10000}
                  value={queryOffset}
                  onChange={(e) => setQueryOffset(Number(e.target.value))}
                  disabled={executing}
                />
              </label>
            </div>
          )}
          {tableColumns.length > 0 && queryType === 'select' && (
            <div className="ui-field" style={{ marginTop: 8 }}>
              <span>
                Colunas ({selectedColumns.size} de {tableColumns.length} selecionadas)
              </span>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 4 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer', fontSize: 13 }}>
                  <input
                    type="checkbox"
                    checked={selectedColumns.size === tableColumns.length}
                    onChange={() => {
                      if (selectedColumns.size === tableColumns.length) {
                        setSelectedColumns(new Set());
                      } else {
                        setSelectedColumns(new Set(tableColumns));
                      }
                    }}
                    disabled={executing}
                  />
                  <strong>Todas</strong>
                </label>
                {tableColumns.map((col) => (
                  <label
                    key={col}
                    style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer', fontSize: 13 }}
                  >
                    <input
                      type="checkbox"
                      checked={selectedColumns.has(col)}
                      onChange={() => {
                        const next = new Set(selectedColumns);
                        if (next.has(col)) next.delete(col);
                        else next.add(col);
                        setSelectedColumns(next);
                      }}
                      disabled={executing}
                    />
                    {col}
                  </label>
                ))}
              </div>
            </div>
          )}
          <div className="ui-button-row" style={{ marginTop: 10 }}>
            <button
              type="button"
              className="ui-button"
              disabled={executing || !selectedTable}
              onClick={() => void executeBuiltQuery()}
            >
              <Play size={16} />
              {executing ? 'A consultar...' : 'Executar'}
            </button>
          </div>
          {errorWritten && (
            <div className="ui-notice ui-error" style={{ marginTop: 12 }}>
              <p style={{ fontWeight: 700 }}>{errorWritten}</p>
            </div>
          )}
          {resultWrittenQuery && (
            <div className="ui-card" style={{ marginTop: 12 }}>
              <div className="ui-section-heading">
                <h3>Resultado</h3>
                {resultWrittenQuery.columns.length > 0 && (
                  <ExportActions
                    filename={`${selectedTable}.json`}
                    content={JSON.stringify({ table: selectedTable, ...resultWrittenQuery }, null, 2)}
                    contentType="application/json;charset=utf-8"
                  />
                )}
              </div>
              <p className="ui-section-note">
                Tabela: {selectedTable}
                {queryType === 'count' ? ' (contagem)' : ` (limite ${queryLimit}, offset ${queryOffset})`}
                {selectedColumns.size > 0 &&
                  selectedColumns.size < tableColumns.length &&
                  ` (${selectedColumns.size} colunas selecionadas)`}
              </p>
              <div className="ui-table-wrap" style={{ overflowX: 'auto' }}>
                <table className="ui-table" style={{ tableLayout: 'fixed', whiteSpace: 'nowrap' }}>
                  <colgroup>
                    {resultWrittenQuery.columns.map((col) => (
                      <col key={col} style={{ width: widthsWritten?.[col] ? `${widthsWritten[col]}px` : undefined }} />
                    ))}
                  </colgroup>
                  <thead>
                    <tr>
                      {resultWrittenQuery.columns.map((col, i) => (
                        <th key={col} ref={(el) => setThRefWritten(col, el)} style={{ position: 'relative' }}>
                          {col}
                          <button
                            type="button"
                            aria-label="Redimensionar coluna"
                            onMouseDown={(e: ReactMouseEvent) => {
                              e.preventDefault();
                              startResizeWritten(col, e.clientX);
                            }}
                            style={{
                              display: 'inline-block',
                              width: 6,
                              height: '100%',
                              cursor: 'col-resize',
                              userSelect: 'none',
                              position: 'absolute',
                              top: 0,
                              right: 0,
                              bottom: 0,
                              padding: 0,
                              border: 'none',
                              background: 'none',
                            }}
                          />
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {resultWrittenQuery.rows.length === 0 ? (
                      <tr>
                        <td colSpan={Math.max(1, resultWrittenQuery.columns.length)}>Sem resultados.</td>
                      </tr>
                    ) : (
                      resultWrittenQuery.rows.map((row) => (
                        <tr key={JSON.stringify(row)}>
                          {resultWrittenQuery.columns.map((col) => (
                            <td key={col}>
                              <CollapsibleValue value={row[col]} />
                            </td>
                          ))}
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              {resultWrittenQuery.limitations.length > 0 && (
                <div className="ui-notice ui-warning">
                  <strong>Limitações</strong>
                  <ul>
                    {resultWrittenQuery.limitations.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </section>
      )}
    </section>
  );
}

async function executePreparedQuery(
  definition: PreparedQuery,
  runId: string,
): Promise<RuntimeDiagnosticResultResponse> {
  const [run, audit, timings, operation, rabbitMq, evidence] = await Promise.all([
    api.getRuntimeRun(runId),
    api.getRuntimeRunAudit(runId),
    api.getRuntimeRunTimings(runId),
    api.getRuntimeOperationByRun(runId).catch(() => null),
    api.getRuntimeRabbitMqMetrics(),
    api.listRuntimeEvidence(),
  ]);
  const score = audit.scoreComponents;
  const indices = audit.indexComparison;
  const scopedEvidence = evidence.items.filter((item) =>
    evidenceIdentityMatchesRun(item.evidenceId, item.scope, runId, operation?.evidenceId),
  );
  const coverage = audit.expectedEvents ? (audit.acceptedReadings / audit.expectedEvents) * 100 : null;
  const limitations: string[] = [];
  let rows: object[] = [];

  switch (definition.id) {
    case 'run-summary':
      rows = [run];
      break;
    case 'accounting':
      rows = [
        {
          expected: operation?.accounting.expectedObservations ?? audit.expectedEvents,
          accepted: operation?.accounting.acceptedObservations ?? audit.acceptedReadings,
          processed: operation?.accounting.processedInbox ?? audit.riskAssessments,
          pending: operation?.accounting.pendingInbox,
          processing: operation?.accounting.processingInbox,
          retryPending: operation?.accounting.retryPendingInbox,
          quarantined: operation?.accounting.quarantinedInbox ?? audit.quarantined,
          settled: operation?.accounting.settled,
        },
      ];
      break;
    case 'np-score':
      rows = [score ?? {}];
      break;
    case 'fwi':
      rows = [
        {
          value: indices?.fireWeatherIndex,
          normalized: indices?.normalizedFireWeatherIndex,
          status: indices?.fireWeatherCalculationStatus,
          source: indices?.fireWeatherIndexValueSource,
          ipmaClass: indices?.fireWeatherIpmaClassLabel,
          timestamp: indices?.logicalDate,
        },
      ];
      break;
    case 'kbdi':
      rows = [
        {
          value: indices?.keetchByramDroughtIndex,
          normalized: indices?.normalizedKeetchByramDroughtIndex,
          status: indices?.kbdiCalculationStatus,
          source: indices?.kbdiValueSource,
          drynessClass: indices?.kbdiDrynessClassLabel,
          antecedentDays: indices?.kbdiAntecedentDays,
        },
      ];
      break;
    case 'portuguese-proxy':
      rows = [
        {
          class: indices?.portugueseContextRiskProxyClass,
          label: indices?.portugueseContextRiskProxyLabel,
          territorialHazard: indices?.territorialHazardProxyClass,
          provenance: indices?.provenance,
        },
      ];
      break;
    case 'sensor-quality':
      rows = [...audit.qualityFlagsSummary, ...audit.eligibilitySummary];
      break;
    case 'integrity':
      rows = [
        {
          confidence: score?.confidenceFactor,
          integrity: score?.integrityFactor,
          coveragePercent: coverage,
          eligible: audit.eligibilitySummary
            .filter((item) => item.status.includes('Eligible'))
            .reduce((sum, item) => sum + item.count, 0),
          blocked: audit.eligibilitySummary
            .filter((item) => item.status.includes('Blocked'))
            .reduce((sum, item) => sum + item.count, 0),
        },
      ];
      break;
    case 'latency':
      rows = [{ ...timings.attempts }, ...timings.stages];
      break;
    case 'throughput':
      rows = [
        {
          accepted: audit.acceptedReadings,
          assessments: audit.riskAssessments,
          durationMs: timings.runDurationMs,
          observationsPerSecond: throughputPerSecond(audit.acceptedReadings, timings.runDurationMs),
          evaluationsPerSecond: throughputPerSecond(audit.riskAssessments, timings.runDurationMs),
        },
      ];
      break;
    case 'rabbitmq':
      rows = rabbitMq.queues;
      limitations.push(
        'RabbitMQ é observado no momento da consulta; o broker não mantém uma série histórica run-scoped neste contrato.',
      );
      break;
    case 'retries':
      rows = [
        {
          retries: audit.retryAttempts,
          quarantined: audit.quarantined,
          retryPending: operation?.accounting.retryPendingInbox,
          quarantinePending: operation?.accounting.quarantinedInbox,
        },
      ];
      break;
    case 'alerts':
      rows = [{ firstAlertAt: timings.firstAlertTriggeredAt, runId }];
      limitations.push('O contrato run-scoped expõe o primeiro alerta, mas não uma série de alertas por ciclo.');
      break;
    case 'evidence':
      rows = scopedEvidence;
      if (rows.length === 0)
        limitations.push(
          'Nenhum artefacto do catálogo contém a identidade desta run; audit e timings continuam disponíveis pelos endpoints run-scoped.',
        );
      break;
    case 'phases':
      rows = [
        ...(timings.timeline ?? []),
        ...(operation
          ? [
              {
                stage: 'SystemCompleted',
                timestamp: operation.systemCompletedAt,
                status: operation.state,
                settled: operation.accounting.settled,
                durationMs: elapsedMs(operation.acceptedAt, operation.systemCompletedAt),
              },
            ]
          : []),
      ];
      break;
  }
  if (definition.id === 'np-score' && !score) limitations.push('A run não possui score components persistidos.');
  return makeResult(definition, rows, limitations);
}

function useColumnResize(columns: string[]) {
  const [widths, setWidths] = useState<Record<string, number> | null>(null);
  const thRefs = useRef<Map<string, HTMLTableCellElement>>(new Map());
  const dragRef = useRef<{ col: string; startX: number; startWidth: number } | null>(null);

  const getOrInitWidths = useCallback(() => {
    if (widths) return widths;
    const init: Record<string, number> = {};
    for (const col of columns) {
      const el = thRefs.current.get(col);
      init[col] = el?.offsetWidth ?? 120;
    }
    if (Object.keys(init).length > 0) setWidths(init);
    return init;
  }, [widths, columns]);

  const setThRef = useCallback((col: string, el: HTMLTableCellElement | null) => {
    if (el) thRefs.current.set(col, el);
    else thRefs.current.delete(col);
  }, []);

  const startResize = useCallback(
    (col: string, startX: number) => {
      const currentWidths = getOrInitWidths();
      const startWidth = currentWidths[col] ?? 120;
      dragRef.current = { col, startX, startWidth };

      const onMouseMove = (e: MouseEvent) => {
        if (!dragRef.current) return;
        const { col, startX, startWidth } = dragRef.current;
        const newWidth = Math.max(50, startWidth + e.clientX - startX);
        setWidths((prev) => ({ ...prev, [col]: newWidth }));
      };

      const onMouseUp = () => {
        dragRef.current = null;
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
      };

      document.body.style.cursor = 'col-resize';
      document.body.style.userSelect = 'none';
      document.addEventListener('mousemove', onMouseMove);
      document.addEventListener('mouseup', onMouseUp);
    },
    [getOrInitWidths],
  );

  return { widths, setThRef, startResize };
}

function QueryResult({ result, runId }: { result: RuntimeDiagnosticResultResponse; runId: string | null }) {
  const { widths, setThRef, startResize } = useColumnResize(result.columns);
  return (
    <>
      <p className="ui-section-note">Resultado associado a SimulationRunId: {runId}</p>
      <div className="ui-table-wrap" style={{ overflowX: 'auto' }}>
        <table className="ui-table" style={{ tableLayout: 'fixed', whiteSpace: 'nowrap' }}>
          <colgroup>
            {result.columns.map((column) => (
              <col key={column} style={{ width: widths?.[column] ? `${widths[column]}px` : undefined }} />
            ))}
          </colgroup>
          <thead>
            <tr>
              {result.columns.map((column) => (
                <th key={column} ref={(el) => setThRef(column, el)} style={{ position: 'relative' }}>
                  {column}
                  <button
                    type="button"
                    aria-label="Redimensionar coluna"
                    onMouseDown={(e: ReactMouseEvent) => {
                      e.preventDefault();
                      startResize(column, e.clientX);
                    }}
                    style={{
                      display: 'inline-block',
                      width: 6,
                      height: '100%',
                      cursor: 'col-resize',
                      userSelect: 'none',
                      position: 'absolute',
                      top: 0,
                      right: 0,
                      bottom: 0,
                      padding: 0,
                      border: 'none',
                      background: 'none',
                    }}
                  />
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {result.rows.length === 0 ? (
              <tr>
                <td colSpan={Math.max(1, result.columns.length)}>Sem resultados para a run selecionada.</td>
              </tr>
            ) : (
              result.rows.map((row) => (
                <tr key={`${result.id}-${JSON.stringify(row)}`}>
                  {result.columns.map((column) => (
                    <td key={column}>
                      <CollapsibleValue value={row[column]} />
                    </td>
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
            {result.limitations.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      )}
    </>
  );
}

function makeResult(
  definition: PreparedQuery,
  values: object[],
  limitations: string[],
): RuntimeDiagnosticResultResponse {
  const rows = values.map((value) =>
    Object.fromEntries(Object.entries(value).map(([key, item]) => [key, serialize(item)])),
  );
  return {
    id: definition.id,
    title: definition.title,
    description: definition.objective,
    columns: [...new Set(rows.flatMap((row) => Object.keys(row)))],
    rows,
    limitations,
  };
}

const LONG_VALUE_THRESHOLD = 120;

function CollapsibleValue({ value }: { value: string | null | undefined }): ReactNode {
  const [expanded, setExpanded] = useState(false);
  if (value == null) return 'Indisponível';
  const shouldCollapse = value.length > LONG_VALUE_THRESHOLD;
  if (!shouldCollapse) return <span style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>{value}</span>;
  return (
    <span style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
      {expanded ? value : `${value.slice(0, LONG_VALUE_THRESHOLD)}...`}
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        style={{
          background: 'none',
          border: 'none',
          cursor: 'pointer',
          padding: 0,
          marginLeft: 6,
          verticalAlign: 'middle',
          fontSize: 'inherit',
          color: '#005fa3',
        }}
      >
        {expanded ? (
          <>
            <ChevronDown size={14} /> menos
          </>
        ) : (
          <>
            <ChevronRight size={14} /> {value.length - LONG_VALUE_THRESHOLD} mais
          </>
        )}
      </button>
    </span>
  );
}

function serialize(value: unknown): string | null {
  if (value == null) return null;
  if (typeof value === 'object') return JSON.stringify(value);
  if (typeof value === 'number') return Number.isInteger(value) ? String(value) : value.toFixed(3);
  return String(value);
}

function query(id: string, group: string, title: string, objective: string, source: string): PreparedQuery {
  return { id, group, title, objective, source };
}
