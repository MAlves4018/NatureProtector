import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

export function RunsPage() {
  const { copy, runs, runsLoading, selectedRunId, setSelectedRunId, runContext, runAudit, runTimings } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader title={copy('run.title')} subtitle={copy('run.subtitle')} helpTopic="runState" />
      <label className="ui-v2-field">
        <span>{copy('run.selectLabel')}</span>
        <select
          className="ui-v2-select"
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
      <section className="ui-v2-card">
        <div className="ui-v2-section-heading">
          <h3>{copy('run.latest')}</h3>
          <StatusBadge label={runContext.state} state={runContext.state === 'completed' ? 'ready' : 'partial'} />
        </div>
        <div className="ui-v2-detail-grid">
          {runContext.fields.map((field) => (
            <div key={field.label} className="ui-v2-detail-row">
              <span className="ui-v2-label">{field.label}</span>
              <span className="ui-v2-value">{field.value}</span>
            </div>
          ))}
        </div>
      </section>
      <div className="ui-v2-grid">
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
    <article className="ui-v2-card">
      <h3>{title}</h3>
      <p>{value}</p>
    </article>
  );
}
