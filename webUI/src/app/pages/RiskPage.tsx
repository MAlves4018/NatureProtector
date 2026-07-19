import { useState } from 'react';
import { BarChart3, ChartPie, AlertTriangle } from 'lucide-react';
import { AreaSelector } from '../components/AreaSelector';
import { DataStatusSummary } from '../components/DataStatusSummary';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { RiskTimelineChart } from '../components/RiskTimelineChart';
import { EligibilityPieChart } from '../components/EligibilityPieChart';
import { FreshnessPieChart } from '../components/FreshnessPieChart';
import { useUiLocale } from '../state/LocaleContext';
import { useUiArea } from '../state/AreaContext';
import { useUiRisk } from '../state/RiskContext';
import { useUiActivity } from '../state/ActivityContext';

export function RiskPage() {
  const { copy } = useUiLocale();
  const { resolvedAreaCode } = useUiArea();
  const { riskModel, summary } = useUiRisk();
  const { runAudit } = useUiActivity();
  const [tab, setTab] = useState<'metrics' | 'charts' | 'warnings'>('metrics');

  return (
    <section className="ui-page">
      <PageHeader title={copy('risk.title')} subtitle={copy('risk.subtitle')} helpTopic="risk" />
      <AreaSelector compact />
      {resolvedAreaCode ? (
        <>
          <DataStatusSummary />
          <div className="ui-segment-group" role="tablist" style={{ marginBottom: 16 }}>
            <button
              type="button"
              className={tab === 'metrics' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'metrics'}
              onClick={() => setTab('metrics')}
            >
              <BarChart3 size={16} />
              Score
            </button>
            <button
              type="button"
              className={tab === 'charts' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'charts'}
              onClick={() => setTab('charts')}
            >
              <ChartPie size={16} />
              Gráficos
            </button>
            <button
              type="button"
              className={tab === 'warnings' ? 'ui-segment-active' : 'ui-segment'}
              role="tab"
              aria-selected={tab === 'warnings'}
              onClick={() => setTab('warnings')}
            >
              <AlertTriangle size={16} />
              Avisos
            </button>
          </div>
          {tab === 'metrics' && (
            <>
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
              <section className="ui-notice">
                <strong>{copy('risk.notAlert')}</strong>
                <p>{copy('risk.officialBoundary')}</p>
              </section>
            </>
          )}
          {tab === 'charts' && (
            <div className="ui-grid">
              <RiskTimelineChart data={summary?.risk.recentScores ?? []} />
              <EligibilityPieChart data={runAudit?.eligibilitySummary} />
              <FreshnessPieChart data={summary?.freshness} />
            </div>
          )}
          {tab === 'warnings' && (
            <>
              {riskModel.warnings.length > 0 ? (
                <section className="ui-panel">
                  <h3>Avisos</h3>
                  <ul>
                    {riskModel.warnings.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </section>
              ) : (
                <p className="ui-notice">Sem avisos para a área selecionada.</p>
              )}
            </>
          )}
        </>
      ) : (
        <EmptyState title={copy('area.selectPrompt')} />
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
