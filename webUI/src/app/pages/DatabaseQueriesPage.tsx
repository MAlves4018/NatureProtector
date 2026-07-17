import { Database, Play, Search } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { ExportActions } from '../components/ExportActions';
import { PageHeader } from '../components/PageHeader';
import { api } from '../services/api';
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
  const [result, setResult] = useState<RuntimeDiagnosticResultResponse | null>(null);
  const [resultRunId, setResultRunId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
    setResult(null);
    setResultRunId(null);
    setError(null);
  }, [selectedRunId]);

  const execute = async () => {
    if (!selectedRunId) return;
    const requestedRunId = selectedRunId;
    const generation = ++requestGeneration.current;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const response = await executePreparedQuery(selected, requestedRunId);
      if (generation !== requestGeneration.current) return;
      setResult(response);
      setResultRunId(requestedRunId);
    } catch (value) {
      if (generation !== requestGeneration.current) return;
      setError(value instanceof Error ? value.message : 'A consulta preparada falhou.');
    } finally {
      if (generation === requestGeneration.current) setLoading(false);
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Consultas preparadas"
        subtitle="Presets read-only ligados à SimulationRunId selecionada; SQL arbitrário não é aceite."
        helpTopic="pipeline"
      />
      <section className="ui-query-layout">
        <aside className="ui-card ui-query-library">
          <div className="ui-section-heading">
            <h3>Biblioteca</h3>
            <span className="ui-badge">{PREPARED_QUERIES.length} presets</span>
          </div>
          <label className="ui-field">
            <span>Filtrar consultas</span>
            <span className="ui-input-with-icon">
              <Search size={15} />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nome ou grupo" />
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
                  setResult(null);
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
          <section className="ui-card">
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
              onClick={() => void execute()}
            >
              <Play size={16} />
              {loading ? 'A consultar…' : 'Executar preset'}
            </button>
            {error && <p className="ui-notice ui-error">{error}</p>}
          </section>
          <section className="ui-card">
            <div className="ui-section-heading">
              <h3>Resultado</h3>
              {result && (
                <div className="ui-button-row">
                  <ExportActions filename={`${result.id}-${resultRunId}.csv`} content={diagnosticResultToCsv(result)} />
                  <ExportActions
                    filename={`${result.id}-${resultRunId}.json`}
                    content={JSON.stringify({ simulationRunId: resultRunId, ...result }, null, 2)}
                    contentType="application/json;charset=utf-8"
                  />
                </div>
              )}
            </div>
            {!result && !error && <p className="ui-notice">Execute o preset para ler os endpoints live.</p>}
            {result && <QueryResult result={result} runId={resultRunId} />}
          </section>
        </div>
      </section>
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

function QueryResult({ result, runId }: { result: RuntimeDiagnosticResultResponse; runId: string | null }) {
  return (
    <>
      <p className="ui-section-note">Resultado associado a SimulationRunId: {runId}</p>
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
                <td colSpan={Math.max(1, result.columns.length)}>Sem resultados para a run selecionada.</td>
              </tr>
            ) : (
              result.rows.map((row) => (
                <tr key={`${result.id}-${JSON.stringify(row)}`}>
                  {result.columns.map((column) => (
                    <td key={column}>{row[column] ?? 'Indisponível'}</td>
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

function serialize(value: unknown): string | null {
  if (value == null) return null;
  if (typeof value === 'object') return JSON.stringify(value);
  if (typeof value === 'number') return Number.isInteger(value) ? String(value) : value.toFixed(3);
  return String(value);
}

function query(id: string, group: string, title: string, objective: string, source: string): PreparedQuery {
  return { id, group, title, objective, source };
}
