import { Activity, Clock3, Database, FileWarning } from 'lucide-react';
import type { RuntimeOperationResponse, RuntimeRunAuditResponse, RuntimeRunTimingSummaryResponse } from '../types';
import { executionStatusState } from '../truthfulPresentation';
import { buildRunProgress, formatDurationMs, timingFacts } from '../utils/operationalMetrics';
import { StatusBadge } from './StatusBadge';

export function RunProgressCockpit({
  operation,
  audit,
  timings,
}: {
  operation: RuntimeOperationResponse | null;
  audit: RuntimeRunAuditResponse | null;
  timings: RuntimeRunTimingSummaryResponse | null;
}) {
  const progress = buildRunProgress(audit, operation);
  const status = operation?.state ?? audit?.run.status ?? 'Sem execução';
  const currentWork = operation
    ? operation.accounting.pendingInbox + operation.accounting.processingInbox + operation.accounting.retryPendingInbox
    : null;

  return (
    <section className="ui-card ui-progress-cockpit">
      <div className="ui-section-heading">
        <div>
          <span className="ui-eyebrow">Cockpit da execução</span>
          <h3>{operation?.simulationRunId ?? audit?.run.id ?? 'A aguardar SimulationRunId'}</h3>
        </div>
        <StatusBadge label={status} state={executionStatusState(status)} />
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
      </div>
      <details className="ui-details">
        <summary>Tempos e latência observados</summary>
        <div className="ui-detail-grid">
          {timingFacts(timings).map((fact) => (
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
  return value == null ? 'Não medido' : String(value);
}
