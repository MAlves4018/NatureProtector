import {
  Activity,
  ArrowUpDown,
  BarChart3,
  Clock3,
  Database,
  FileCheck2,
  GitCompareArrows,
  Search,
  ShieldAlert,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ExportActions } from '../components/ExportActions';
import { PageHeader } from '../components/PageHeader';
import { RunProgressCockpit } from '../components/RunProgressCockpit';
import { RunScientificMetrics } from '../components/RunScientificMetrics';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { useUiActivity } from '../state/ActivityContext';
import { runPresentationState } from '../truthfulPresentation';
import { formatDurationMs, rowsToCsv, timingFacts } from '../utils/operationalMetrics';

export function RunsPage() {
  const { copy } = useUiLocale();
  const navigate = useNavigate();
  const {
    runs,
    runsLoading,
    selectedRun,
    selectedRunId,
    setSelectedRunId,
    runContext,
    runAudit,
    runTimings,
    runOperation,
    refreshSelectedRun,
  } = useUiActivity();
  const [tab, setTab] = useState<'summary' | 'lifecycle' | 'accounting' | 'quality' | 'evidence'>('summary');
  const [runSearch, setRunSearch] = useState('');
  const [scenarioFilter, setScenarioFilter] = useState('');
  const [profileFilter, setProfileFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [oldestFirst, setOldestFirst] = useState(false);
  const selectableRuns = useMemo(
    () => (selectedRun && !runs.some((run) => run.id === selectedRun.id) ? [selectedRun, ...runs] : runs),
    [runs, selectedRun],
  );
  const visibleRuns = useMemo(() => {
    const needle = runSearch.trim().toLowerCase();
    return selectableRuns
      .filter(
        (run) =>
          (!needle || `${run.id} ${run.scenarioCode} ${run.scenarioName}`.toLowerCase().includes(needle)) &&
          (!scenarioFilter || run.scenarioCode === scenarioFilter) &&
          (!profileFilter || runProfiles(run) === profileFilter) &&
          (!statusFilter || run.status === statusFilter),
      )
      .sort((a, b) => {
        const difference = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
        return oldestFirst ? difference : -difference;
      });
  }, [oldestFirst, runSearch, scenarioFilter, profileFilter, selectableRuns, statusFilter]);
  const scenarioOptions = [...new Set(selectableRuns.map((run) => run.scenarioCode))].sort();
  const profileOptions = [...new Set(selectableRuns.map(runProfiles))].sort();
  const statusOptions = [...new Set(selectableRuns.map((run) => run.status))].sort();
  const presentation = runPresentationState({
    status: runContext.run?.status ?? runContext.state,
    expected: runAudit?.expectedEvents ?? null,
    accepted: runAudit?.acceptedReadings ?? null,
    missing: runAudit?.missingEvents ?? null,
  });
  const runQuerySuffix = selectedRunId ? `?runId=${encodeURIComponent(selectedRunId)}` : '';

  return (
    <section className="ui-page">
      <PageHeader
        title="Run workspace"
        subtitle="Lifecycle, accounting, pipeline, qualidade e evidence no contexto da mesma SimulationRunId."
        helpTopic="runState"
      />
      <section className="ui-run-selector">
        <label className="ui-field">
          <span>{copy('run.selectLabel')}</span>
          <select
            className="ui-select"
            value={selectedRunId}
            onChange={(event) => setSelectedRunId(event.target.value)}
            disabled={runsLoading}
          >
            <option value="">{runsLoading ? copy('state.loading') : copy('run.none')}</option>
            {selectableRuns.map((run) => (
              <option key={run.id} value={run.id}>
                {run.status} · {run.scenarioCode} · {run.id}
              </option>
            ))}
          </select>
        </label>
        <StatusBadge label={presentation.label} state={presentation.state} />
      </section>
      <section className="ui-card ui-run-history">
        <div className="ui-section-heading">
          <div>
            <span className="ui-eyebrow">Histórico carregado</span>
            <h3>Localizar e abrir uma run</h3>
          </div>
          <div className="ui-button-row">
            <span className="ui-section-note">
              {visibleRuns.length} de {selectableRuns.length}
            </span>
            <ExportActions
              filename="runtime-runs.csv"
              content={rowsToCsv(
                visibleRuns.map((run) => ({
                  simulationRunId: run.id,
                  scenario: run.scenarioCode,
                  status: run.status,
                  seed: run.executionSeed,
                  cycles: run.numberOfCycles,
                  intervalSeconds: run.intervalSeconds,
                  createdAt: run.createdAt,
                  startedAt: run.startedAt,
                  endedAt: run.endedAt,
                })),
              )}
            />
          </div>
        </div>
        <div className="ui-run-history-toolbar">
          <label className="ui-field">
            <span>Pesquisar ID ou cenário</span>
            <span className="ui-input-with-icon">
              <Search size={15} />
              <input value={runSearch} onChange={(event) => setRunSearch(event.target.value)} />
            </span>
          </label>
          <label className="ui-field">
            <span>Cenário</span>
            <select value={scenarioFilter} onChange={(event) => setScenarioFilter(event.target.value)}>
              <option value="">Todos</option>
              {scenarioOptions.map((scenario) => (
                <option key={scenario}>{scenario}</option>
              ))}
            </select>
          </label>
          <label className="ui-field">
            <span>Perfil</span>
            <select value={profileFilter} onChange={(event) => setProfileFilter(event.target.value)}>
              <option value="">Todos</option>
              {profileOptions.map((profile) => (
                <option key={profile}>{profile}</option>
              ))}
            </select>
          </label>
          <label className="ui-field">
            <span>Estado</span>
            <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
              <option value="">Todos</option>
              {statusOptions.map((status) => (
                <option key={status}>{status}</option>
              ))}
            </select>
          </label>
          <button type="button" className="ui-secondary" onClick={() => setOldestFirst((value) => !value)}>
            <ArrowUpDown size={15} /> {oldestFirst ? 'Mais antigas' : 'Mais recentes'}
          </button>
        </div>
        <div className="ui-table-wrap ui-run-history-table">
          <table className="ui-table">
            <thead>
              <tr>
                <th>SimulationRunId</th>
                <th>Cenário</th>
                <th>Estado</th>
                <th>Configuração</th>
                <th>Início / fim</th>
                <th>Accounting</th>
                <th>Ação</th>
              </tr>
            </thead>
            <tbody>
              {visibleRuns.map((run) => {
                const active = run.id === selectedRunId;
                return (
                  <tr key={run.id} className={active ? 'ui-run-history-row-active' : undefined}>
                    <td>
                      <strong>{run.id}</strong>
                    </td>
                    <td>
                      {run.scenarioCode}
                      <small className="ui-table-note">{run.scenarioName}</small>
                    </td>
                    <td>{run.status}</td>
                    <td>
                      {run.numberOfCycles} ciclos · {run.intervalSeconds}s
                      <small className="ui-table-note">
                        seed {run.executionSeed ?? 'não registada'} · {runProfiles(run)} · {runSensorCount(run) ?? '—'}{' '}
                        sensores
                      </small>
                    </td>
                    <td>
                      {formatDate(run.startedAt ?? run.createdAt)}
                      <small className="ui-table-note">{formatDate(run.endedAt)}</small>
                    </td>
                    <td>
                      {active && runAudit
                        ? `${runAudit.expectedEvents ?? '—'} / ${runAudit.acceptedReadings} / ${runAudit.riskAssessments}`
                        : 'Abrir para carregar'}
                      <small className="ui-table-note">expected / accepted / processed</small>
                    </td>
                    <td>
                      <button type="button" className="ui-secondary" onClick={() => setSelectedRunId(run.id)}>
                        {active ? 'Aberta' : 'Abrir'}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
      {runContext.resolvedRunId ? (
        <>
          <section className="ui-run-identity">
            <div>
              <span className="ui-eyebrow">SimulationRunId</span>
              <strong>{runContext.resolvedRunId}</strong>
            </div>
            <div>
              <span className="ui-eyebrow">Cenário</span>
              <strong>{runContext.run?.scenarioName ?? 'Indisponível'}</strong>
            </div>
            <div>
              <span className="ui-eyebrow">Área</span>
              <strong>{runContext.run?.areaCode ?? 'Indisponível'}</strong>
            </div>
          </section>
          <RunProgressCockpit
            operation={runOperation}
            audit={runAudit}
            timings={runTimings}
            selectedRunId={selectedRunId}
            onRefresh={refreshSelectedRun}
          />
          <RunScientificMetrics audit={runAudit} />
          <div className="ui-button-row ui-run-actions">
            <button
              type="button"
              className="ui-secondary"
              onClick={() => navigate(`/scenario-compare${runQuerySuffix}`)}
            >
              <GitCompareArrows size={15} /> Comparar cenários
            </button>
            <button type="button" className="ui-secondary" onClick={() => navigate(`/queries${runQuerySuffix}`)}>
              <Search size={15} /> Consultas preparadas
            </button>
            <button type="button" className="ui-secondary" onClick={() => navigate(`/evidence${runQuerySuffix}`)}>
              <FileCheck2 size={15} /> Abrir evidence
            </button>
          </div>
          <nav className="ui-tabs" aria-label="Detalhes da run">
            <RunTab icon={<Activity />} label="Resumo" active={tab === 'summary'} onClick={() => setTab('summary')} />
            <RunTab
              icon={<Clock3 />}
              label="Lifecycle"
              active={tab === 'lifecycle'}
              onClick={() => setTab('lifecycle')}
            />
            <RunTab
              icon={<Database />}
              label="Accounting"
              active={tab === 'accounting'}
              onClick={() => setTab('accounting')}
            />
            <RunTab
              icon={<ShieldAlert />}
              label="Qualidade"
              active={tab === 'quality'}
              onClick={() => setTab('quality')}
            />
            <RunTab
              icon={<FileCheck2 />}
              label="Evidence"
              active={tab === 'evidence'}
              onClick={() => setTab('evidence')}
            />
          </nav>
          {tab === 'summary' && (
            <section className="ui-card ui-run-summary-card">
              <div className="ui-section-heading">
                <h3>Resumo da execução</h3>
                <StatusBadge label={presentation.label} state={presentation.state} />
              </div>
              <div className="ui-detail-grid">
                {runContext.fields.map((field) => (
                  <div key={field.label} className="ui-detail-row">
                    <span className="ui-label">{field.label}</span>
                    <span className="ui-value">{field.value}</span>
                  </div>
                ))}
              </div>
            </section>
          )}
          {tab === 'lifecycle' && <Lifecycle timings={runTimings} operation={runOperation} />}
          {tab === 'accounting' && <Accounting audit={runAudit} operation={runOperation} />}
          {tab === 'quality' && <Quality audit={runAudit} />}
          {tab === 'evidence' && (
            <section className="ui-card">
              <div className="ui-section-heading">
                <h3>Pacote exportável da run</h3>
                <ExportActions
                  filename={`run-${runContext.resolvedRunId}.json`}
                  content={JSON.stringify(
                    { run: runContext.run, operation: runOperation, audit: runAudit, timings: runTimings },
                    null,
                    2,
                  )}
                  contentType="application/json;charset=utf-8"
                />
              </div>
              <div className="ui-grid">
                <EvidenceBox
                  title="Audit runtime"
                  value={runAudit ? 'Recolhida para esta run' : copy('value.noEvidence')}
                />
                <EvidenceBox
                  title="Timing"
                  value={
                    runTimings?.runDurationMs == null
                      ? copy('value.noEvidence')
                      : formatDurationMs(runTimings.runDurationMs)
                  }
                />
                <EvidenceBox
                  title="DataScope"
                  value={runAudit?.dataScope?.dataRunId ?? runTimings?.dataScope?.dataRunId ?? 'Não instrumentado'}
                />
              </div>
            </section>
          )}
        </>
      ) : (
        <section className="ui-empty-state">
          <BarChart3 size={28} />
          <h3>Selecione uma run</h3>
          <p>O workspace mantém todos os indicadores associados à mesma SimulationRunId.</p>
        </section>
      )}
    </section>
  );
}

function RunTab({
  icon,
  label,
  active,
  onClick,
}: {
  icon: React.ReactNode;
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      className={active ? 'ui-tab ui-tab-active' : 'ui-tab'}
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
    >
      {icon}
      {label}
    </button>
  );
}

function Lifecycle({
  timings,
  operation,
}: {
  timings: ReturnType<typeof useUiActivity>['runTimings'];
  operation: ReturnType<typeof useUiActivity>['runOperation'];
}) {
  const events = [
    ['Pedido aceite', operation?.acceptedAt],
    ['Simulator iniciado', operation?.startedAt],
    ['Run iniciada', timings?.startedAt],
    ['Primeiro evento recebido', timings?.firstInboxReceivedAt],
    ['Primeiro processamento', timings?.firstProcessingAttemptStartedAt],
    ['Primeiro risco', timings?.firstRiskAssessmentCreatedAt],
    ['Último processamento', timings?.lastProcessingAttemptFinishedAt],
    ['Run terminada', timings?.endedAt],
    ['SystemCompleted', operation?.systemCompletedAt],
    ['Settled', operation?.accounting.settled ? (operation.finishedAt ?? operation.systemCompletedAt) : null],
  ];
  return (
    <div className="ui-two-column">
      <section className="ui-card">
        <h3>Lifecycle observado</h3>
        <ol className="ui-lifecycle">
          {events.map(([label, timestamp]) => (
            <li key={label} data-observed={Boolean(timestamp)}>
              <span />
              <div>
                <strong>{label}</strong>
                <small>{timestamp ? new Date(timestamp).toLocaleString() : 'Não observado'}</small>
              </div>
            </li>
          ))}
        </ol>
      </section>
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>Durações defensáveis</h3>
          {timings && (
            <ExportActions
              filename={`timings-${timings.simulationRunId}.csv`}
              content={rowsToCsv(timingFacts(timings))}
            />
          )}
        </div>
        <div className="ui-detail-grid">
          {timingFacts(timings, operation).map((fact) => (
            <div key={fact.label} className="ui-detail-row">
              <span>{fact.label}</span>
              <strong>{fact.value}</strong>
            </div>
          ))}
        </div>
        {timings?.stages && timings.stages.length > 0 && (
          <details className="ui-details">
            <summary>Duração por fase</summary>
            <div className="ui-table-wrap">
              <table className="ui-table">
                <thead>
                  <tr>
                    <th>Fase</th>
                    <th>Resultado</th>
                    <th>Contagem</th>
                    <th>Média</th>
                    <th>Máximo</th>
                  </tr>
                </thead>
                <tbody>
                  {timings.stages.map((stage) => (
                    <tr key={`${stage.stage}-${stage.outcome}-${stage.errorCode ?? 'none'}`}>
                      <td>{stage.stage}</td>
                      <td>{stage.outcome}</td>
                      <td>{stage.count}</td>
                      <td>{formatDurationMs(stage.avgDurationMs)}</td>
                      <td>{formatDurationMs(stage.maxDurationMs)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </details>
        )}
      </section>
    </div>
  );
}

function Accounting({
  audit,
  operation,
}: {
  audit: ReturnType<typeof useUiActivity>['runAudit'];
  operation: ReturnType<typeof useUiActivity>['runOperation'];
}) {
  const expected = audit?.expectedEvents;
  const accepted = audit?.acceptedReadings;
  return (
    <section className="ui-card">
      <h3>Accounting isolado por run</h3>
      <div className="ui-metric-grid">
        <EvidenceBox title="Esperados" value={expected == null ? 'Sem dados' : String(expected)} />
        <EvidenceBox title="Aceites" value={accepted == null ? 'Sem dados' : String(accepted)} />
        <EvidenceBox title="Missing" value={audit?.missingEvents == null ? 'Sem dados' : String(audit.missingEvents)} />
        <EvidenceBox title="Processed" value={audit ? String(audit.riskAssessments) : 'Sem dados'} />
        <EvidenceBox title="Pending" value={operation ? String(operation.accounting.pendingInbox) : 'Indisponível'} />
        <EvidenceBox
          title="Processing"
          value={operation ? String(operation.accounting.processingInbox) : 'Indisponível'}
        />
        <EvidenceBox
          title="A aguardar retry"
          value={operation ? String(operation.accounting.retryPendingInbox) : 'Indisponível'}
        />
        <EvidenceBox
          title="Em quarentena"
          value={
            operation
              ? String(operation.accounting.quarantinedInbox)
              : audit
                ? String(audit.quarantined)
                : 'Indisponível'
          }
        />
        <EvidenceBox
          title="Settled"
          value={operation ? (operation.accounting.settled ? 'Sim' : 'Não') : 'Indisponível'}
        />
      </div>
      {expected != null && accepted != null && accepted < expected && (
        <p className="ui-notice ui-warning">
          Accepted é inferior a expected. Isto pode ser válido num perfil missing; processed deve ser comparado com
          accepted.
        </p>
      )}
    </section>
  );
}

function Quality({ audit }: { audit: ReturnType<typeof useUiActivity>['runAudit'] }) {
  return (
    <section className="ui-card">
      <h3>Qualidade e perdas</h3>
      <div className="ui-metric-grid">
        <EvidenceBox title="Rejected" value={audit ? String(audit.rejected) : 'Sem dados'} />
        <EvidenceBox title="Em quarentena" value={audit ? String(audit.quarantined) : 'Sem dados'} />
        <EvidenceBox title="Retries" value={audit ? String(audit.retryAttempts) : 'Sem dados'} />
        <EvidenceBox title="Risk assessments" value={audit ? String(audit.riskAssessments) : 'Sem dados'} />
      </div>
    </section>
  );
}

function EvidenceBox({ title, value }: { title: string; value: string }) {
  return (
    <article className="ui-card">
      <h3>{title}</h3>
      <p>{value}</p>
    </article>
  );
}

function runProfiles(run: ReturnType<typeof useUiActivity>['runs'][number]) {
  const resolved = runMetadata(run)?.run_overrides?.resolved;
  return resolved?.degradation_profiles?.join(', ') || resolved?.degradation_profile || 'none';
}

function runSensorCount(run: ReturnType<typeof useUiActivity>['runs'][number]) {
  return runMetadata(run)?.sensor_count ?? null;
}

function runMetadata(run: ReturnType<typeof useUiActivity>['runs'][number]) {
  if (!run.metadataJson) return null;
  try {
    return JSON.parse(run.metadataJson) as {
      sensor_count?: number;
      run_overrides?: {
        resolved?: { degradation_profile?: string; degradation_profiles?: string[] };
      };
    };
  } catch {
    return null;
  }
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString('pt-PT') : 'Não registado';
}
