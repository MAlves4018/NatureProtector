import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { RiskTimelineChart } from '../components/RiskTimelineChart';
import { EligibilityPieChart } from '../components/EligibilityPieChart';
import { FreshnessPieChart } from '../components/FreshnessPieChart';
import { useUiLocale } from '../state/LocaleContext';
import { useUiRisk } from '../state/RiskContext';
import { useUiActivity } from '../state/ActivityContext';

export function RiskPage() {
  const { copy } = useUiLocale();
  const { riskModel, summary } = useUiRisk();
  const { runAudit } = useUiActivity();

  return (
    <section className="ui-page">
      <PageHeader title={copy('risk.title')} subtitle={copy('risk.subtitle')} helpTopic="risk" />
      <AreaSelector compact />
      <DataStatusSummary />
      <div className="ui-grid">
        <MetricCard
          label={copy('risk.scoreLabel')}
          value={riskModel.scoreDisplay ?? copy('risk.noScore')}
          state={riskModel.state}
        />
        <MetricCard
          label={copy('risk.classLabel')}
          value={riskModel.classDisplay ?? copy('value.notAvailable')}
          state={riskModel.state}
        />
        <MetricCard label={copy('risk.timestampLabel')} value={riskModel.timestampDisplay} state={riskModel.state} />
        <MetricCard label={copy('risk.runLabel')} value={riskModel.run} state={riskModel.state} />
      </div>
      <div className="ui-grid">
        <RiskTimelineChart data={summary?.risk.recentScores ?? []} />
        <EligibilityPieChart data={runAudit?.eligibilitySummary} />
        <FreshnessPieChart data={summary?.freshness} />
      </div>
      <section className="ui-notice">
        <strong>{copy('risk.notAlert')}</strong>
        <p>{copy('risk.officialBoundary')}</p>
      </section>
      {riskModel.warnings.length > 0 && (
        <section className="ui-panel">
          <h3>Avisos</h3>
          <ul>
            {riskModel.warnings.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>
      )}
    </section>
  );
}

function MetricCard({
  label,
  value,
  state,
}: {
  label: string;
  value: string;
  state: Parameters<typeof StatusBadge>[0]['state'];
}) {
  return (
    <article className="ui-card">
      <span className="ui-label">{label}</span>
      <strong className="ui-metric">{value}</strong>
      <StatusBadge label={state ?? 'ready'} state={state} />
    </article>
  );
}