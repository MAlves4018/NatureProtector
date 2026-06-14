import { AreaSelector } from '../components/AreaSelector';
import { BetaParityLinks } from '../components/BetaParityLinks';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

export function RiskPage() {
  const { copy, riskModel } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('risk.title')} subtitle={copy('risk.subtitle')} helpTopic="risk" />
      <AreaSelector compact />
      <DataStatusSummary />
      <div className="ui-v2-grid">
        <MetricCard label={copy('risk.scoreLabel')} value={riskModel.scoreDisplay ?? copy('risk.noScore')} state={riskModel.state} />
        <MetricCard label={copy('risk.classLabel')} value={riskModel.classDisplay ?? copy('value.notAvailable')} state={riskModel.state} />
        <MetricCard label={copy('risk.timestampLabel')} value={riskModel.timestampDisplay} state={riskModel.state} />
        <MetricCard label={copy('risk.runLabel')} value={riskModel.run} state={riskModel.state} />
      </div>
      <section className="ui-v2-notice">
        <strong>{copy('risk.notAlert')}</strong>
        <p>{copy('risk.officialBoundary')}</p>
      </section>
      {riskModel.warnings.length > 0 && (
        <section className="ui-v2-panel">
          <h3>Avisos</h3>
          <ul>{riskModel.warnings.map(item => <li key={item}>{item}</li>)}</ul>
        </section>
      )}
      <BetaParityLinks ids={['monitoring', 'map']} />
    </section>
  );
}

function MetricCard({ label, value, state }: { label: string; value: string; state: Parameters<typeof StatusBadge>[0]['state'] }) {
  return (
    <article className="ui-v2-card">
      <span className="ui-v2-label">{label}</span>
      <strong className="ui-v2-metric">{value}</strong>
      <StatusBadge label={state ?? 'ready'} state={state} />
    </article>
  );
}
