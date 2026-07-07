import { useState, useCallback } from 'react';
import { RefreshCw, CheckCircle2, AlertTriangle, XCircle, Activity } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';

interface ModuleStatus {
  module: string;
  environment: string;
  status: 'healthy' | 'degraded' | 'outage' | 'unknown';
  lastCheck: string;
  version: string;
  uptime: string;
  detail: string;
}

function buildMockModules(): ModuleStatus[] {
  const now = new Date();
  return [
    {
      module: 'Docker Engine',
      environment: 'dev',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '27.2.0',
      uptime: '12d 7h',
      detail: 'All containers running',
    },
    {
      module: 'Runtime API',
      environment: 'dev',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '1.4.2',
      uptime: '12d 7h',
      detail: 'HTTP 200',
    },
    {
      module: 'PostgreSQL',
      environment: 'dev',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '16.4',
      uptime: '12d 7h',
      detail: 'Connections: 12/100',
    },
    {
      module: 'RabbitMQ',
      environment: 'dev',
      status: 'degraded',
      lastCheck: now.toISOString(),
      version: '3.13.6',
      uptime: '12d 7h',
      detail: 'Queue depth: 1423 (elevated)',
    },
    {
      module: 'InfluxDB',
      environment: 'dev',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '2.7.10',
      uptime: '12d 7h',
      detail: 'Write throughput nominal',
    },
    {
      module: 'Grafana',
      environment: 'dev',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '11.2.0',
      uptime: '12d 7h',
      detail: 'Dashboards responsive',
    },
    {
      module: 'Simulator',
      environment: 'dev',
      status: 'outage',
      lastCheck: now.toISOString(),
      version: '0.9.1',
      uptime: '0d 0h',
      detail: 'Service not responding',
    },
    {
      module: 'P3 Service',
      environment: 'dev',
      status: 'unknown',
      lastCheck: now.toISOString(),
      version: '0.5.0',
      uptime: 'N/A',
      detail: 'Not instrumented',
    },
    {
      module: 'Docker Engine',
      environment: 'staging',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '27.2.0',
      uptime: '5d 3h',
      detail: 'All containers running',
    },
    {
      module: 'Runtime API',
      environment: 'staging',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '1.4.2',
      uptime: '5d 3h',
      detail: 'HTTP 200',
    },
    {
      module: 'PostgreSQL',
      environment: 'staging',
      status: 'degraded',
      lastCheck: now.toISOString(),
      version: '16.4',
      uptime: '5d 3h',
      detail: 'Replication lag: 2.3s',
    },
    {
      module: 'RabbitMQ',
      environment: 'staging',
      status: 'healthy',
      lastCheck: now.toISOString(),
      version: '3.13.6',
      uptime: '5d 3h',
      detail: 'Nominal',
    },
  ];
}

const STATUS_ICONS = {
  healthy: CheckCircle2,
  degraded: AlertTriangle,
  outage: XCircle,
  unknown: Activity,
};

const STATUS_COLORS = {
  healthy: '#166534',
  degraded: '#a16207',
  outage: '#b91c1c',
  unknown: 'var(--ui-muted)',
};

const STATUS_LABELS = {
  healthy: 'Operational',
  degraded: 'Degraded',
  outage: 'Outage',
  unknown: 'Unknown',
};

export function DeploymentHealthPage() {
  const [modules, setModules] = useState<ModuleStatus[]>(buildMockModules);
  const [refreshing, setRefreshing] = useState(false);
  const [lastRefresh, setLastRefresh] = useState(new Date().toISOString());

  const handleRefresh = useCallback(async () => {
    setRefreshing(true);
    await new Promise((r) => setTimeout(r, 1200));
    setModules(buildMockModules());
    setLastRefresh(new Date().toISOString());
    setRefreshing(false);
  }, []);

  const globalStatus: 'healthy' | 'degraded' | 'outage' = (() => {
    if (modules.some((m) => m.status === 'outage')) return 'outage';
    if (modules.some((m) => m.status === 'degraded')) return 'degraded';
    return 'healthy';
  })();

  const GlobalIcon = STATUS_ICONS[globalStatus];

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
            <GlobalIcon size={20} style={{ color: STATUS_COLORS[globalStatus] }} />
            {globalStatus === 'healthy'
              ? 'All systems operational'
              : globalStatus === 'degraded'
                ? 'Degraded'
                : 'Outage'}
          </h3>
          <span className="ui-badge">
            {modules.filter((m) => m.status === 'healthy').length}/{modules.length} healthy
          </span>
        </div>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginTop: 8 }}>
          {(Object.keys(STATUS_LABELS) as Array<keyof typeof STATUS_LABELS>).map((key) => {
            const count = modules.filter((m) => m.status === key).length;
            if (count === 0) return null;
            return (
              <span key={key} style={{ fontSize: '0.85rem', color: STATUS_COLORS[key], fontWeight: 600 }}>
                {STATUS_LABELS[key]}: {count}
              </span>
            );
          })}
        </div>
      </div>

      <div className="ui-card">
        <div className="ui-section-heading">
          <h3>Modulos</h3>
          <div className="ui-button-row">
            <span className="ui-label">Ultimo check: {new Date(lastRefresh).toLocaleString()}</span>
            <button type="button" className="ui-button" disabled={refreshing} onClick={() => void handleRefresh()}>
              <RefreshCw size={16} className={refreshing ? 'ui-spin' : ''} />
              {refreshing ? 'A atualizar...' : 'Refresh All'}
            </button>
          </div>
        </div>
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr>
                <th>Modulo</th>
                <th>Ambiente</th>
                <th>Estado</th>
                <th>Ultimo check</th>
                <th>Versao</th>
                <th>Uptime</th>
                <th>Detalhe</th>
              </tr>
            </thead>
            <tbody>
              {modules.map((mod) => {
                const Icon = STATUS_ICONS[mod.status];
                return (
                  <tr key={`${mod.module}-${mod.environment}`}>
                    <td style={{ fontWeight: 700 }}>{mod.module}</td>
                    <td>
                      <span className="ui-badge">{mod.environment}</span>
                    </td>
                    <td>
                      <span style={{ display: 'flex', alignItems: 'center', gap: 6, color: STATUS_COLORS[mod.status] }}>
                        <Icon size={14} />
                        {STATUS_LABELS[mod.status]}
                      </span>
                    </td>
                    <td>{new Date(mod.lastCheck).toLocaleString()}</td>
                    <td>{mod.version}</td>
                    <td>{mod.uptime}</td>
                    <td style={{ fontSize: '0.85rem', color: 'var(--ui-muted)' }}>{mod.detail}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
