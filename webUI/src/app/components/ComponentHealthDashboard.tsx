import type { RuntimeOperationalHealthResponse } from '../types';

interface Props {
  health: RuntimeOperationalHealthResponse | null;
}

const STATUS_ORDER: Record<string, number> = {
  Healthy: 0,
  Degraded: 1,
  Unhealthy: 2,
  Unknown: 3,
  NotInstrumented: 4,
  NotApplicable: 5,
};

function statusColor(status: string): string {
  switch (status) {
    case 'Healthy':
      return '#166534';
    case 'Degraded':
      return '#a16207';
    case 'Unhealthy':
      return '#b91c1c';
    default:
      return 'var(--ui-muted)';
  }
}

export function ComponentHealthDashboard({ health }: Props) {
  if (!health) {
    return (
      <div className="ui-chart-empty">
        <p>Sem dados de saude dos componentes.</p>
      </div>
    );
  }

  const sorted = [...health.components].sort((a, b) => (STATUS_ORDER[a.status] ?? 99) - (STATUS_ORDER[b.status] ?? 99));

  return (
    <div className="ui-chart-card">
      <div className="ui-section-heading">
        <h3>Saude dos Componentes</h3>
        <span className="ui-badge">Observado em {new Date(health.observedAt).toLocaleTimeString()}</span>
      </div>
      <div className="ui-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))' }}>
        {sorted.map((comp) => (
          <article key={comp.component} className="ui-card" style={{ padding: 12 }}>
            <div className="ui-section-heading" style={{ marginBottom: 8 }}>
              <strong>{comp.component}</strong>
              <span className="ui-badge" style={{ background: statusColor(comp.status), color: '#fff' }}>
                {comp.status}
              </span>
            </div>
            <div className="ui-fact-list" style={{ gap: 4, margin: 0 }}>
              <span style={{ fontSize: '0.82rem' }}>
                <strong>Fonte</strong> {comp.source}
              </span>
              {comp.ageSeconds !== null && (
                <span style={{ fontSize: '0.82rem' }}>
                  <strong>Idade</strong> {Math.round(comp.ageSeconds / 60)}min
                </span>
              )}
              {comp.lastSuccessAt && (
                <span style={{ fontSize: '0.82rem' }}>
                  <strong>Ultimo sucesso</strong> {new Date(comp.lastSuccessAt).toLocaleString()}
                </span>
              )}
              {comp.lastFailureAt && (
                <span style={{ fontSize: '0.82rem' }}>
                  <strong>Ultima falha</strong> {new Date(comp.lastFailureAt).toLocaleString()}
                </span>
              )}
            </div>
            {comp.reason && <p style={{ fontSize: '0.8rem', marginTop: 6, color: 'var(--ui-muted)' }}>{comp.reason}</p>}
          </article>
        ))}
      </div>
      {health.limitations.length > 0 && (
        <ul style={{ marginTop: 8 }}>
          {health.limitations.map((lim) => (
            <li key={lim.message} className="ui-muted">
              {lim.message}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
