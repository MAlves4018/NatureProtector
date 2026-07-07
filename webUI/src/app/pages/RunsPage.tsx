import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { useUiActivity } from '../state/ActivityContext';

export function RunsPage() {
  const { copy } = useUiLocale();
  const { runs, runsLoading, selectedRunId, setSelectedRunId, runContext, runAudit, runTimings } = useUiActivity();

  return (
    <section className="ui-page">
      <PageHeader title={copy('run.title')} subtitle={copy('run.subtitle')} helpTopic="runState" />
      <label className="ui-field">
        <span>{copy('run.selectLabel')}</span>
        <select
          className="ui-select"
          value={selectedRunId}
          onChange={(event) => setSelectedRunId(event.target.value)}
          disabled={runsLoading}
        >
          <option value="">{runsLoading ? copy('state.loading') : copy('run.none')}</option>
          {runs.map((run) => (
            <option key={run.id} value={run.id}>
              {run.status} / {run.scenarioCode} / {run.id}
            </option>
          ))}
        </select>
      </label>
      <section className="ui-card">
        <div className="ui-section-heading">
          <h3>{copy('run.latest')}</h3>
          <StatusBadge label={runContext.state} state={runContext.state === 'completed' ? 'ready' : 'partial'} />
        </div>
        <div className="ui-detail-grid">
          {runContext.fields.map((field) => (
            <div key={field.label} className="ui-detail-row">
              <span className="ui-label">{field.label}</span>
              <span className="ui-value">{field.value}</span>
            </div>
          ))}
        </div>
      </section>
      <div className="ui-grid">
        <EvidenceBox
          title={copy('run.audit')}
          value={
            runAudit
              ? `${runAudit.acceptedReadings} accepted / ${runAudit.rejected} rejected / ${runAudit.quarantined} quarantined`
              : copy('value.noEvidence')
          }
        />
        <EvidenceBox
          title={copy('run.timings')}
          value={
            runTimings?.runDurationMs == null ? copy('value.noEvidence') : `${Math.round(runTimings.runDurationMs)}ms`
          }
        />
      </div>
    </section>
  );
}

function EvidenceBox({ title, value }: { title: string; value: string }) {
  return (
    <article className="ui-card">
      <h3>{title}</h3>
      <p>{value}</p>
    </article>
  );
}
