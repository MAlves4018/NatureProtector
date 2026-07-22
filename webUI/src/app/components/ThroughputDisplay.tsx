import type { RuntimeRunAuditResponse, RuntimeRunTimingSummaryResponse } from '../types';

interface Props {
  audit: RuntimeRunAuditResponse | null;
  timings: RuntimeRunTimingSummaryResponse | null;
}

interface MetricCard {
  label: string;
  value: string;
  color: string;
}

export function ThroughputDisplay({ audit, timings }: Props) {
  const metrics: MetricCard[] = [];

  if (audit) {
    const acceptedRate = audit.expectedEvents
      ? `${((audit.acceptedReadings / audit.expectedEvents) * 100).toFixed(1)}%`
      : `${audit.acceptedReadings}`;
    metrics.push({ label: 'Taxa de aceitacao', value: acceptedRate, color: '#22c55e' });

    metrics.push({
      label: 'Rejeitados',
      value: String(audit.rejected),
      color: audit.rejected > 0 ? '#ef4444' : '#22c55e',
    });
    metrics.push({
      label: 'Quarentena',
      value: String(audit.quarantined),
      color: audit.quarantined > 0 ? '#eab308' : '#22c55e',
    });
    metrics.push({
      label: 'Retries',
      value: String(audit.retryAttempts),
      color: audit.retryAttempts > 0 ? '#eab308' : '#22c55e',
    });
    metrics.push({ label: 'Risk assessments', value: String(audit.riskAssessments), color: '#22c55e' });
  }

  if (timings?.timeline) {
    const completed = timings.timeline.filter((p) => p.status === 'completed').length;
    const total = timings.timeline.length;
    metrics.push({
      label: 'Pipeline stages',
      value: `${completed}/${total}`,
      color: completed === total ? '#22c55e' : '#eab308',
    });
  }

  if (timings?.attempts) {
    const successRate =
      timings.attempts.attemptCount > 0
        ? `${((timings.attempts.successfulAttempts / timings.attempts.attemptCount) * 100).toFixed(1)}%`
        : 'N/A';
    metrics.push({ label: 'Sucesso attempts', value: successRate, color: '#22c55e' });

    if (timings.attempts.avgDurationMs !== null) {
      metrics.push({
        label: 'Duracao media',
        value: `${(timings.attempts.avgDurationMs / 1000).toFixed(2)}s`,
        color: '#3b82f6',
      });
    }
  }

  if (audit?.expectedEvents !== null && audit?.expectedEvents !== undefined) {
    metrics.push({
      label: 'Eventos esperados',
      value: String(audit.expectedEvents),
      color: '#3b82f6',
    });
  }

  if (metrics.length === 0) {
    return (
      <div className="ui-chart-empty">
        <p>Sem metricas de throughput.</p>
      </div>
    );
  }

  return (
    <div className="ui-chart-card">
      <h3>Throughput / Latencia</h3>
      <div className="ui-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))' }}>
        {metrics.map((m) => (
          <article key={m.label} className="ui-card" style={{ padding: 12, textAlign: 'center' }}>
            <p style={{ fontSize: '0.78rem', marginBottom: 4 }}>{m.label}</p>
            <p style={{ fontSize: '1.3rem', fontWeight: 900, color: m.color }}>{m.value}</p>
          </article>
        ))}
      </div>
    </div>
  );
}
