import { Activity, Clock3, Copy, Database, FileWarning, RotateCw } from 'lucide-react';
import type { RuntimeOperationResponse, RuntimeRunAuditResponse, RuntimeRunTimingSummaryResponse } from '../types';
import { executionStatusState } from '../truthfulPresentation';
import { buildRunProgress, formatDurationMs, timingFacts } from '../utils/operationalMetrics';
import { StatusBadge } from './StatusBadge';

export function RunProgressCockpit({
  operation,
  audit,
  timings,
  selectedRunId,
  onRefresh,
}: {
  operation: RuntimeOperationResponse | null;
  audit: RuntimeRunAuditResponse | null;
  timings: RuntimeRunTimingSummaryResponse | null;
  selectedRunId: string | null;
  onRefresh?: () => void;
}) {
  const progress = buildRunProgress(audit, operation);
  const runId = operation?.simulationRunId ?? audit?.run.id ?? selectedRunId;
  const status =
    operation?.systemCompletedAt && operation.accounting.settled === false
      ? 'A consolidar'
      : (operation?.state ?? audit?.run.status ?? (runId ? 'A carregar' : 'Sem execução'));
  const currentWork = operation
    ? operation.accounting.pendingInbox + operation.accounting.processingInbox + operation.accounting.retryPendingInbox
    : null;

  return (
    <section className="ui-card ui-progress-cockpit">
      <div className="ui-section-heading">
        <div>
          <span className="ui-eyebrow">Cockpit da execução</span>
          <h3>{runId ?? 'Selecione uma execução'}</h3>
          {runId && (
            <button
              type="button"
              className="ui-icon-button"
              title="Copiar SimulationRunId"
              aria-label="Copiar SimulationRunId"
              onClick={() => void navigator.clipboard.writeText(runId)}
            >
              <Copy size={14} />
            </button>
          )}
        </div>
        <div className="ui-button-row">
          {onRefresh && (
            <button type="button" className="ui-secondary" onClick={onRefresh}>
              <RotateCw size={14} /> Atualizar
            </button>
          )}
          <StatusBadge label={status} state={executionStatusState(status)} />
        </div>
      </div>
      <div
        className="ui-progress-track"
        role="progressbar"
        aria-label="Progresso observado"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={progress.completedPercent ?? 0}
      >
        <span style={{ width: `${progress.completedPercent ?? 0}%` }} />
      </div>
      <div className="ui-metric-grid ui-metric-grid-compact">
        <Metric icon={<Database />} label="Esperados" value={metric(progress.expected)} />
        <Metric
          icon={<Activity />}
          label="Aceites / avaliados"
          value={`${metric(progress.accepted)} / ${metric(progress.assessed)}`}
        />
        <Metric icon={<Clock3 />} label="Duração" value={formatDurationMs(timings?.runDurationMs)} />
        <Metric icon={<FileWarning />} label="Trabalho pendente" value={metric(currentWork ?? progress.pending)} />
        <Metric icon={<Activity />} label="Processado" value={percent(progress.completedPercent)} />
        <Metric icon={<Activity />} label="Aceite" value={percent(progress.acceptedPercent)} />
        <Metric icon={<FileWarning />} label="Perda observada" value={percent(progress.lostPercent)} />
        <Metric
          icon={<Database />}
          label="Settled"
          value={progress.settled == null ? 'Indisponível' : progress.settled ? 'Sim' : 'Não'}
        />
      </div>
      {operation && (
        <div className="ui-detail-grid">
          <Detail label="Pending" value={operation.accounting.pendingInbox} />
          <Detail label="Processing" value={operation.accounting.processingInbox} />
          <Detail label="A aguardar retry" value={operation.accounting.retryPendingInbox} />
          <Detail label="Quarantine" value={operation.accounting.quarantinedInbox} />
        </div>
      )}
      <details className="ui-details">
        <summary>Tempos e latência observados</summary>
        <div className="ui-detail-grid">
          {timingFacts(timings, operation).map((fact) => (
            <div key={fact.label} className="ui-detail-row">
              <span>{fact.label}</span>
              <strong>{fact.value}</strong>
            </div>
          ))}
        </div>
      </details>
      {operation?.failureDetail && <p className="ui-notice ui-error">{operation.failureDetail}</p>}
    </section>
  );
}

function Metric({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <article className="ui-metric-card">
      <span className="ui-metric-icon">{icon}</span>
      <strong>{value}</strong>
      <small>{label}</small>
    </article>
  );
}

function metric(value: number | null) {
  return value == null ? 'Indisponível' : String(value);
}

function percent(value: number | null) {
  return value == null ? 'Indisponível' : `${value.toFixed(1)}%`;
}

function Detail({ label, value }: { label: string; value: number }) {
  return (
    <div className="ui-detail-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
