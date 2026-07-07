import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useUiRisk } from '../state/RiskContext';
import { useUiActivity } from '../state/ActivityContext';
import { useReadinessItems } from '../state/useUiSurfaces';

export function OverviewPage() {
  const { copy } = useUiLocale();
  const { riskModel } = useUiRisk();
  const { runContext } = useUiActivity();
  const readinessItems = useReadinessItems();

  return (
    <section className="ui-page">
      <PageHeader
        title="Visao geral"
        subtitle="Resumo autenticado focado em leitura: área, estado dos dados, última execução e readiness local."
        helpTopic="overview"
      />
      <AreaSelector />
      <DataStatusSummary />
      <div className="ui-grid">
        <article className="ui-card">
          <h3>{copy('risk.title')}</h3>
          <p>{riskModel.summary}</p>
          <StatusBadge
            label={
              riskModel.canShowScore
                ? `${riskModel.scoreDisplay} / ${riskModel.classDisplay ?? '-'}`
                : copy('risk.noScore')
            }
            state={riskModel.state}
          />
        </article>
        <article className="ui-card">
          <h3>{copy('run.latest')}</h3>
          <p>{runContext.resolvedRunId ?? copy('run.none')}</p>
          <StatusBadge label={runContext.state} state={runContext.state === 'completed' ? 'ready' : 'partial'} />
        </article>
      </div>
      <section className="ui-panel">
        <h3>{copy('readiness.title')}</h3>
        <div className="ui-grid">
          {readinessItems.map((item) => (
            <article className="ui-status-item" key={item.item}>
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