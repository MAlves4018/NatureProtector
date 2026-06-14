import { AreaSelector } from '../components/AreaSelector';
import { BetaParityLinks } from '../components/BetaParityLinks';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

export function OverviewPage() {
  const { copy, riskModel, runContext, readinessItems } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title="Visao geral" subtitle="Resumo autenticado focado em leitura: area, estado dos dados, ultimo contexto de run e readiness local." helpTopic="overview" />
      <AreaSelector />
      <DataStatusSummary />
      <div className="ui-v2-grid">
        <article className="ui-v2-card">
          <h3>{copy('risk.title')}</h3>
          <p>{riskModel.summary}</p>
          <StatusBadge label={riskModel.canShowScore ? `${riskModel.scoreDisplay} / ${riskModel.classDisplay ?? '-'}` : copy('risk.noScore')} state={riskModel.state} />
        </article>
        <article className="ui-v2-card">
          <h3>{copy('run.latest')}</h3>
          <p>{runContext.resolvedRunId ?? copy('run.none')}</p>
          <StatusBadge label={runContext.state} state={runContext.state === 'completed' ? 'ready' : 'partial'} />
        </article>
      </div>
      <section className="ui-v2-panel">
        <h3>{copy('readiness.title')}</h3>
        <div className="ui-v2-grid">
          {readinessItems.map(item => (
            <article className="ui-v2-status-item" key={item.item}>
              <span className="ui-v2-label">{item.item}</span>
              <StatusBadge label={item.status} state={item.status} />
              <p>{item.evidence}</p>
              <small>{item.limitation}</small>
            </article>
          ))}
        </div>
      </section>
      <BetaParityLinks ids={['monitoring', 'map']} />
    </section>
  );
}
