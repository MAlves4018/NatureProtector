import { CheckCircle2, AlertTriangle, XCircle, Activity } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { useUiObservability } from '../state/ObservabilityContext';
import { globalOperationalStatus } from '../truthfulPresentation';

const STATUS_ICONS: Record<string, typeof CheckCircle2> = {
  Healthy: CheckCircle2,
  Degraded: AlertTriangle,
  Unhealthy: XCircle,
  Unknown: Activity,
  AuthRequired: Activity,
  NotApplicable: Activity,
  NotInstrumented: Activity,
};

const STATUS_COLORS: Record<string, string> = {
  Healthy: '#166534',
  Degraded: '#a16207',
  Unhealthy: '#b91c1c',
  Unknown: 'var(--ui-muted)',
  AuthRequired: 'var(--ui-muted)',
  NotApplicable: 'var(--ui-muted)',
  NotInstrumented: 'var(--ui-muted)',
};

const STATUS_LABELS: Record<string, string> = {
  Healthy: 'Operational',
  Degraded: 'Degraded',
  Unhealthy: 'Unhealthy',
  Unknown: 'Unknown',
  AuthRequired: 'Authentication required',
  NotApplicable: 'Not applicable',
  NotInstrumented: 'Not instrumented',
};

const GLOBAL_LABELS = {
  Healthy: 'All observed systems healthy',
  Degraded: 'Observed deployment degraded',
  Unhealthy: 'Observed deployment unhealthy',
  Unknown: 'Overall health not established',
} as const;

function formatAge(seconds: number | null): string {
  if (seconds === null || seconds === undefined) return '\u2014';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}min`;
  return `${Math.round(seconds / 3600)}h`;
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) return '\u2014';
  return new Date(value).toLocaleString();
}

export function DeploymentHealthPage() {
  const { operationalHealth, observabilityError } = useUiObservability();

  const components = operationalHealth?.components ?? [];
  const status = globalOperationalStatus(components);
  const GlobalIcon = STATUS_ICONS[status] ?? Activity;

  return (
    <section className="ui-page">
      <PageHeader
        title="Deployment Health"
        subtitle="Estado operacional de cada modulo do projeto."
        helpTopic="pipeline"
      />

      <div className="ui-card" style={{ marginBottom: 16 }}>
        <div className="ui-section-heading">
          <h3 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <GlobalIcon size={20} style={{ color: STATUS_COLORS[status] }} />
            {GLOBAL_LABELS[status]}
          </h3>
          <span className="ui-badge">
            {components.filter((c) => c.status === 'Healthy').length}/{components.length} healthy
          </span>
        </div>
        {status === 'Unknown' && (
          <p className="ui-notice ui-warning">
            Overall health requires a non-empty set in which every observed component is explicitly Healthy.
          </p>
        )}
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginTop: 8 }}>
          {Object.keys(STATUS_LABELS).map((key) => {
            const count = components.filter((c) => c.status === key).length;
            if (count === 0) return null;
            return (
              <span key={key} style={{ fontSize: '0.85rem', color: STATUS_COLORS[key], fontWeight: 600 }}>
                {STATUS_LABELS[key]}: {count}
              </span>
            );
          })}
        </div>
      </div>

      {observabilityError && (
        <div className="ui-card" style={{ marginBottom: 16 }}>
          <p className="ui-state-error">{observabilityError.message}</p>
        </div>
      )}

      {!operationalHealth && !observabilityError && (
        <div className="ui-card">
          <p>A carregar dados de saude...</p>
        </div>
      )}

      {operationalHealth && (
        <>
          <div className="ui-card">
            <div className="ui-section-heading">
              <h3>Modulos</h3>
              <span className="ui-badge">Observado em {new Date(operationalHealth.observedAt).toLocaleString()}</span>
            </div>
            <div className="ui-table-wrap">
              <table className="ui-table">
                <thead>
                  <tr>
                    <th>Modulo</th>
                    <th>Estado</th>
                    <th>Fonte</th>
                    <th>Idade</th>
                    <th>Ultimo sucesso</th>
                    <th>Ultima falha</th>
                    <th>Detalhe</th>
                  </tr>
                </thead>
                <tbody>
                  {components.length === 0 ? (
                    <tr>
                      <td colSpan={7}>Sem dados de saude dos componentes.</td>
                    </tr>
                  ) : (
                    components.map((comp) => {
                      const Icon = STATUS_ICONS[comp.status] ?? Activity;
                      return (
                        <tr key={comp.component}>
                          <td style={{ fontWeight: 700 }}>{comp.component}</td>
                          <td>
                            <span
                              style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: 6,
                                color: STATUS_COLORS[comp.status] ?? 'var(--ui-muted)',
                              }}
                            >
                              <Icon size={14} />
                              {STATUS_LABELS[comp.status] ?? comp.status}
                            </span>
                          </td>
                          <td style={{ fontSize: '0.85rem' }}>{comp.source}</td>
                          <td style={{ fontSize: '0.85rem' }}>{formatAge(comp.ageSeconds)}</td>
                          <td style={{ fontSize: '0.85rem' }}>{formatTimestamp(comp.lastSuccessAt)}</td>
                          <td style={{ fontSize: '0.85rem' }}>{formatTimestamp(comp.lastFailureAt)}</td>
                          <td style={{ fontSize: '0.85rem', color: 'var(--ui-muted)' }}>{comp.reason}</td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </section>
  );
}
