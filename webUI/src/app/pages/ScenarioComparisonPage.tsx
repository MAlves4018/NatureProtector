import { GitCompareArrows } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ExportActions } from '../components/ExportActions';
import { PageHeader } from '../components/PageHeader';
import { api } from '../services/api';
import { useUiActivity } from '../state/ActivityContext';
import type {
  RuntimeOperationResponse,
  RuntimeRunAuditResponse,
  RuntimeRunSummaryResponse,
  RuntimeRunTimingSummaryResponse,
} from '../types';
import { elapsedMs, rowsToCsv, throughputPerSecond } from '../utils/operationalMetrics';

interface RunBundle {
  run: RuntimeRunSummaryResponse;
  audit: RuntimeRunAuditResponse;
  timings: RuntimeRunTimingSummaryResponse;
  operation: RuntimeOperationResponse | null;
}

interface ComparisonRow {
  metric: string;
  a: string | number | null;
  b: string | number | null;
  absoluteDifference: number | null;
  percentageDifference: number | null;
}

export function ScenarioComparisonPage() {
  const { runs, selectedRunId } = useUiActivity();
  const [searchParams, setSearchParams] = useSearchParams();
  const [runAId, setRunAId] = useState(() => searchParams.get('runA') ?? selectedRunId);
  const [runBId, setRunBId] = useState(() => searchParams.get('runB') ?? '');
  const [rows, setRows] = useState<ComparisonRow[]>([]);
  const [comparedIds, setComparedIds] = useState<[string, string] | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const requestGeneration = useRef(0);

  const updateRunId = useCallback(
    (parameter: 'runA' | 'runB', value: string) => {
      parameter === 'runA' ? setRunAId(value) : setRunBId(value);
      requestGeneration.current += 1;
      setRows([]);
      setComparedIds(null);
      setSearchParams(
        (current) => {
          const next = new URLSearchParams(current);
          value ? next.set(parameter, value) : next.delete(parameter);
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  useEffect(() => {
    if (runs.length < 2) return;
    const defaultA = selectedRunId || runs.find((run) => run.scenarioCode === 'scenario_b')?.id || runs[0].id;
    if (!runAId) updateRunId('runA', defaultA);
    if (!runBId) {
      updateRunId(
        'runB',
        runs.find((run) => run.scenarioCode === 'scenario_c' && run.id !== defaultA)?.id ||
          runs.find((run) => run.id !== defaultA)?.id ||
          '',
      );
    }
  }, [runs, runAId, runBId, selectedRunId, updateRunId]);

  const compare = async () => {
    if (!runAId || !runBId || runAId === runBId) return;
    const requestedIds: [string, string] = [runAId, runBId];
    const generation = ++requestGeneration.current;
    setLoading(true);
    setError(null);
    try {
      const [a, b] = await Promise.all([loadRun(runAId), loadRun(runBId)]);
      if (generation !== requestGeneration.current) return;
      setRows(buildComparison(a, b));
      setWarnings(comparabilityWarnings(a, b));
      setComparedIds(requestedIds);
    } catch (value) {
      if (generation !== requestGeneration.current) return;
      setError(value instanceof Error ? value.message : 'Não foi possível comparar as runs.');
    } finally {
      if (generation === requestGeneration.current) setLoading(false);
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Comparar execuções"
        subtitle="Comparação run-to-run usando duas SimulationRunId explícitas e dados persistidos."
        helpTopic="runState"
      />
      <section className="ui-card">
        <div className="ui-compare-row">
          <RunSelect label="Run A" value={runAId} onChange={(value) => updateRunId('runA', value)} runs={runs} />
          <RunSelect label="Run B" value={runBId} onChange={(value) => updateRunId('runB', value)} runs={runs} />
          <button
            type="button"
            className="ui-button"
            disabled={!runAId || !runBId || runAId === runBId || loading}
            onClick={() => void compare()}
          >
            <GitCompareArrows size={16} />
            {loading ? 'A comparar…' : 'Comparar'}
          </button>
        </div>
        {error && <p className="ui-notice ui-error">{error}</p>}
      </section>
      {warnings.length > 0 && (
        <section className="ui-notice ui-warning">
          <strong>Comparabilidade limitada</strong>
          <ul>
            {warnings.map((warning) => (
              <li key={warning}>{warning}</li>
            ))}
          </ul>
        </section>
      )}
      {rows.length > 0 && (
        <section className="ui-card">
          <div className="ui-section-heading">
            <div>
              <span className="ui-eyebrow">Persistência run-scoped</span>
              <h3>Valores A/B e diferenças</h3>
              <p className="ui-section-note">
                A: {comparedIds?.[0]} · B: {comparedIds?.[1]}
              </p>
            </div>
            <div className="ui-button-row">
              <ExportActions
                filename={`comparacao-${comparedIds?.[0]}-${comparedIds?.[1]}.csv`}
                content={rowsToCsv(rows.map((row) => ({ ...row })))}
              />
              <ExportActions
                filename={`comparacao-${comparedIds?.[0]}-${comparedIds?.[1]}.json`}
                content={JSON.stringify(
                  { runAId: comparedIds?.[0], runBId: comparedIds?.[1], warnings, rows },
                  null,
                  2,
                )}
                contentType="application/json;charset=utf-8"
              />
            </div>
          </div>
          <div className="ui-table-wrap">
            <table className="ui-table">
              <thead>
                <tr>
                  <th>Métrica</th>
                  <th>Run A</th>
                  <th>Run B</th>
                  <th>Diferença absoluta</th>
                  <th>Diferença %</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.metric}>
                    <td>{row.metric}</td>
                    <td>{display(row.a)}</td>
                    <td>{display(row.b)}</td>
                    <td>{display(row.absoluteDifference)}</td>
                    <td>
                      {row.percentageDifference == null ? 'Não aplicável' : `${row.percentageDifference.toFixed(1)}%`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="ui-notice">
            A interface não classifica automaticamente “melhor” ou “pior”; essa interpretação depende do significado
            científico de cada métrica.
          </p>
        </section>
      )}
    </section>
  );
}

function RunSelect({
  label,
  value,
  onChange,
  runs,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  runs: ReturnType<typeof useUiActivity>['runs'];
}) {
  return (
    <label className="ui-field">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Selecione</option>
        {value && !runs.some((run) => run.id === value) && <option value={value}>{value} · histórica</option>}
        {runs.map((run) => (
          <option key={run.id} value={run.id}>
            {run.scenarioCode} · seed {run.executionSeed ?? '—'} · {run.id}
          </option>
        ))}
      </select>
    </label>
  );
}

async function loadRun(runId: string): Promise<RunBundle> {
  const [run, audit, timings, operation] = await Promise.all([
    api.getRuntimeRun(runId),
    api.getRuntimeRunAudit(runId),
    api.getRuntimeRunTimings(runId),
    api.getRuntimeOperationByRun(runId).catch(() => null),
  ]);
  return { run, audit, timings, operation };
}

function buildComparison(a: RunBundle, b: RunBundle): ComparisonRow[] {
  const valueRows: Array<[string, string | number | null | undefined, string | number | null | undefined]> = [
    ['SimulationRunId', a.run.id, b.run.id],
    ['Cenário', a.run.scenarioCode, b.run.scenarioCode],
    ['Área', a.run.areaCode, b.run.areaCode],
    ['Estado', a.run.status, b.run.status],
    ['Versão da configuração', a.run.configurationVersionNumber, b.run.configurationVersionNumber],
    ['Seed', a.run.executionSeed, b.run.executionSeed],
    ['Sensores', a.run.runOverrides?.resolved?.sensorCount, b.run.runOverrides?.resolved?.sensorCount],
    ['Ciclos', a.run.numberOfCycles, b.run.numberOfCycles],
    ['Intervalo (s)', a.run.intervalSeconds, b.run.intervalSeconds],
    ['Perfis de degradação', profiles(a), profiles(b)],
    ['Criada', a.run.createdAt, b.run.createdAt],
    ['Iniciada', a.run.startedAt, b.run.startedAt],
    ['Concluída', a.run.endedAt, b.run.endedAt],
    ['Expected', a.audit.expectedEvents, b.audit.expectedEvents],
    ['Accepted', a.audit.acceptedReadings, b.audit.acceptedReadings],
    ['Processed/evaluated', a.audit.riskAssessments, b.audit.riskAssessments],
    ['Missing', a.audit.missingEvents, b.audit.missingEvents],
    ['Rejected', a.audit.rejected, b.audit.rejected],
    ['Pending', a.operation?.accounting.pendingInbox, b.operation?.accounting.pendingInbox],
    ['Processing', a.operation?.accounting.processingInbox, b.operation?.accounting.processingInbox],
    ['Retry pending', a.operation?.accounting.retryPendingInbox, b.operation?.accounting.retryPendingInbox],
    ['Settled', a.operation?.accounting.settled ? 'Sim' : 'Não', b.operation?.accounting.settled ? 'Sim' : 'Não'],
    ['NP Score', a.audit.scoreComponents?.npScore, b.audit.scoreComponents?.npScore],
    ['Base Risk', a.audit.scoreComponents?.baseRisk, b.audit.scoreComponents?.baseRisk],
    ['Adjusted Risk', a.audit.scoreComponents?.adjustedScore, b.audit.scoreComponents?.adjustedScore],
    ['Score100', a.audit.scoreComponents?.score100, b.audit.scoreComponents?.score100],
    ['FWI', a.audit.indexComparison?.fireWeatherIndex, b.audit.indexComparison?.fireWeatherIndex],
    ['KBDI', a.audit.indexComparison?.keetchByramDroughtIndex, b.audit.indexComparison?.keetchByramDroughtIndex],
    [
      'Portuguese Proxy',
      a.audit.indexComparison?.portugueseContextRiskProxyLabel,
      b.audit.indexComparison?.portugueseContextRiskProxyLabel,
    ],
    ['Confidence', a.audit.scoreComponents?.confidenceFactor, b.audit.scoreComponents?.confidenceFactor],
    ['Integrity', a.audit.scoreComponents?.integrityFactor, b.audit.scoreComponents?.integrityFactor],
    ['Coverage (%)', coverage(a), coverage(b)],
    ['Eligible', eligibility(a, 'eligible'), eligibility(b, 'eligible')],
    ['Blocked', eligibility(a, 'blocked'), eligibility(b, 'blocked')],
    ['Risco', a.audit.scoreComponents?.npRiskClassLabel, b.audit.scoreComponents?.npRiskClassLabel],
    ['Duração total (ms)', a.timings.runDurationMs, b.timings.runDurationMs],
    ['Até primeira observação (ms)', a.timings.timeToFirstInboxMs, b.timings.timeToFirstInboxMs],
    ['Até settled (ms)', timeToSettled(a), timeToSettled(b)],
    ['Gap SystemCompleted → settled (ms)', settlementGap(a), settlementGap(b)],
    [
      'Throughput (obs/s)',
      throughputPerSecond(a.audit.acceptedReadings, a.timings.runDurationMs),
      throughputPerSecond(b.audit.acceptedReadings, b.timings.runDurationMs),
    ],
    ['Retries', a.audit.retryAttempts, b.audit.retryAttempts],
    ['Quarantine', a.audit.quarantined, b.audit.quarantined],
    ['Evidence associada', a.operation?.evidenceId ? 'Sim' : 'Não', b.operation?.evidenceId ? 'Sim' : 'Não'],
  ];
  return valueRows.map(([metric, valueA, valueB]) => difference(metric, valueA ?? null, valueB ?? null));
}

function comparabilityWarnings(a: RunBundle, b: RunBundle) {
  const warnings: string[] = [];
  if (a.run.executionSeed !== b.run.executionSeed) warnings.push('Seeds diferentes.');
  if (a.run.numberOfCycles !== b.run.numberOfCycles || a.run.intervalSeconds !== b.run.intervalSeconds)
    warnings.push('Duração ou cadência configurada diferente.');
  if (a.run.runOverrides?.resolved?.sensorCount !== b.run.runOverrides?.resolved?.sensorCount)
    warnings.push('Número de sensores diferente.');
  if (profiles(a) !== profiles(b)) warnings.push('Perfis de degradação diferentes.');
  if (a.run.configurationVersionNumber !== b.run.configurationVersionNumber)
    warnings.push('Versões de configuração diferentes.');
  return warnings;
}

function difference(metric: string, a: string | number | null, b: string | number | null): ComparisonRow {
  const numeric = typeof a === 'number' && typeof b === 'number';
  const absoluteDifference = numeric ? b - a : null;
  return {
    metric,
    a,
    b,
    absoluteDifference,
    percentageDifference: numeric && a !== 0 ? ((b - a) / Math.abs(a)) * 100 : null,
  };
}

function profiles(bundle: RunBundle) {
  return (
    bundle.run.runOverrides?.resolved?.degradationProfiles?.join(', ') ||
    bundle.run.runOverrides?.resolved?.degradationProfile ||
    'Nenhum'
  );
}
function coverage(bundle: RunBundle) {
  return bundle.audit.expectedEvents ? (bundle.audit.acceptedReadings / bundle.audit.expectedEvents) * 100 : null;
}
function timeToSettled(bundle: RunBundle) {
  return bundle.operation?.accounting.settled
    ? elapsedMs(bundle.operation.acceptedAt, bundle.operation.finishedAt ?? bundle.operation.systemCompletedAt)
    : null;
}
function settlementGap(bundle: RunBundle) {
  return bundle.operation?.accounting.settled
    ? elapsedMs(bundle.operation.systemCompletedAt, bundle.operation.finishedAt ?? bundle.operation.systemCompletedAt)
    : null;
}
function eligibility(bundle: RunBundle, status: string) {
  return bundle.audit.eligibilitySummary
    .filter((item) => item.status.toLowerCase().includes(status))
    .reduce((sum, item) => sum + item.count, 0);
}
function display(value: string | number | null) {
  return value == null || value === ''
    ? 'Indisponível'
    : typeof value === 'number'
      ? value.toFixed(Number.isInteger(value) ? 0 : 3)
      : value;
}
