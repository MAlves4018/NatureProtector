import { Activity, AlertTriangle, ArrowRight, Database, Gauge, RotateCw, ShieldCheck } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { useUiRisk } from '../state/RiskContext';
import { useUiActivity } from '../state/ActivityContext';
import { useReadinessItems } from '../state/useUiSurfaces';
import { useUiObservability } from '../state/ObservabilityContext';
import { useUiAlerts } from '../state/AlertContext';
import { globalOperationalStatus } from '../truthfulPresentation';

export function OverviewPage() {
  const navigate = useNavigate();
  const { copy } = useUiLocale();
  const { riskModel, summary } = useUiRisk();
  const { runContext, setSelectedRunId } = useUiActivity();
  const { operationalHealth, rabbitMqMetrics, refreshObservability } = useUiObservability();
  const { activeAlerts } = useUiAlerts();
  const readinessItems = useReadinessItems();
  const health = globalOperationalStatus(operationalHealth?.components ?? []);
  const primaryQueue = rabbitMqMetrics?.queues.find((queue) => queue.queueRole === 'PrimaryWorkQueue');
  const activeRun = runContext.run ?? summary?.currentRun ?? summary?.latestRun;

  return (
    <section className="ui-page">
      <PageHeader
        title="Visão geral operacional"
        subtitle="Estado do sistema, trabalho em curso e sinais que exigem atenção, sem esconder ausência ou degradação."
        helpTopic="overview"
        actions={
          <button type="button" className="ui-button" onClick={() => navigate('/simulation')}>
            Nova simulação <ArrowRight size={15} />
          </button>
        }
      />
      <div className="ui-overview-bar">
        <AreaSelector compact />
        <span className="ui-observed-at">
          Última observação:{' '}
          {operationalHealth?.observedAt ? new Date(operationalHealth.observedAt).toLocaleTimeString() : 'indisponível'}
        </span>
        <button type="button" className="ui-secondary" onClick={refreshObservability}>
          <RotateCw size={14} /> Atualizar readiness
        </button>
      </div>
      <div className="ui-metric-grid">
        <MetricCard
          icon={<ShieldCheck />}
          label="Saúde global"
          value={health}
          detail="Componentes observados"
          tone={health}
        />
        <MetricCard
          icon={<Activity />}
          label="Run ativa"
          value={activeRun?.status ?? 'Sem run'}
          detail={activeRun?.scenarioName ?? 'Nenhuma execução selecionada'}
          tone={activeRun?.status ?? 'Unknown'}
        />
        <MetricCard
          icon={<Database />}
          label="Backlog primário"
          value={primaryQueue?.messagesTotal == null ? 'Sem dados' : String(primaryQueue.messagesTotal)}
          detail={primaryQueue ? `${primaryQueue.consumers ?? '—'} consumidores` : 'RabbitMQ indisponível'}
          tone={primaryQueue?.messagesTotal ? 'Degraded' : 'Healthy'}
        />
        <MetricCard
          icon={<Gauge />}
          label="Risco territorial"
          value={riskModel.canShowScore ? (riskModel.scoreDisplay ?? 'Sem score') : 'Sem score'}
          detail={riskModel.classDisplay ?? riskModel.summary ?? 'Sem contexto disponível'}
          tone={riskModel.state}
        />
      </div>
      {activeRun && (
        <article className="ui-run-strip">
          <div>
            <span className="ui-eyebrow">SimulationRunId · {activeRun.id}</span>
            <h3>{activeRun.scenarioName}</h3>
            <p>
              {activeRun.areaCode} · {activeRun.numberOfCycles} ciclos
            </p>
          </div>
          <StatusBadge label={runContext.state} state={runContext.state === 'completed' ? 'ready' : 'partial'} />
          <button
            type="button"
            className="ui-secondary"
            onClick={() => {
              setSelectedRunId(activeRun.id);
              navigate('/runs');
            }}
          >
            Abrir run <ArrowRight size={15} />
          </button>
        </article>
      )}
      <div className="ui-two-column ui-overview-columns">
        <DataStatusSummary showDetails={false} />
        <section className="ui-panel">
          <div className="ui-section-heading">
            <h3>Atenção operacional</h3>
            <StatusBadge
              label={`${activeAlerts.length} alertas`}
              state={activeAlerts.length > 0 ? 'partial' : 'ready'}
            />
          </div>
          {activeAlerts.length === 0 ? (
            <div className="ui-empty-inline">
              <ShieldCheck size={20} />
              <span>Não existem alertas ativos observados para a área.</span>
            </div>
          ) : (
            <div className="ui-attention-list">
              {activeAlerts.map((alert) => (
                <article key={alert.id}>
                  <AlertTriangle size={17} />
                  <span>
                    <strong>{alert.severity}</strong>
                    {alert.message}
                  </span>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
      <section className="ui-panel">
        <div className="ui-section-heading">
          <h3>{copy('readiness.title')}</h3>
          <span className="ui-section-note">Capacidade declarada e respetiva evidência</span>
        </div>
        <div className="ui-readiness-grid">
          {readinessItems.map((item) => (
            <article className="ui-readiness-card" key={item.item}>
              <span className="ui-label">{item.item}</span>
              <StatusBadge label={item.status} state={item.status} />
              <p>{item.evidence}</p>
              <small>{item.limitation}</small>
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}

function MetricCard({
  icon,
  label,
  value,
  detail,
  tone,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  detail: string;
  tone: string;
}) {
  return (
    <article className="ui-metric-card" data-tone={tone.toLowerCase()}>
      <span className="ui-metric-icon">{icon}</span>
      <span className="ui-label">{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}
