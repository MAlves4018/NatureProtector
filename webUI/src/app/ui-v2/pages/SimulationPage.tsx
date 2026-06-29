import { PlayCircle } from 'lucide-react';
import { AreaSelector } from '../components/AreaSelector';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiV2 } from '../state/UiV2Context';

export function SimulationPage() {
  const {
    copy,
    scenarios,
    selectedScenarioCode,
    setSelectedScenarioCode,
    simulationForm,
    setSimulationForm,
    simulationReview,
    simulationSubmitting,
    simulationError,
    canExecuteSimulation,
    submitSimulation,
    degradationProfiles,
  } = useUiV2();

  return (
    <section className="ui-v2-page">
      <PageHeader
        title={copy('simulation.title')}
        subtitle={copy('simulation.subtitle')}
        helpTopic="degradationProfile"
      />
      <AreaSelector compact />
      <div className="ui-v2-two-column">
        <form
          className="ui-v2-card ui-v2-form"
          onSubmit={(event) => {
            event.preventDefault();
            void submitSimulation();
          }}
        >
          <label className="ui-v2-field">
            <span>{copy('scenario.selectLabel')}</span>
            <select
              className="ui-v2-select"
              value={selectedScenarioCode}
              onChange={(event) => setSelectedScenarioCode(event.target.value)}
            >
              <option value="">{copy('scenario.none')}</option>
              {scenarios.map((scenario) => (
                <option key={scenario.code} value={scenario.code}>
                  {scenario.name} ({scenario.code})
                </option>
              ))}
            </select>
          </label>
          <NumberField
            label={copy('simulation.sensorCount')}
            value={simulationForm.sensorCount}
            onChange={(value) => setSimulationForm((form) => ({ ...form, sensorCount: value }))}
          />
          <NumberField
            label={copy('simulation.cycles')}
            value={simulationForm.numberOfCycles}
            onChange={(value) => setSimulationForm((form) => ({ ...form, numberOfCycles: value }))}
          />
          <NumberField
            label={copy('simulation.interval')}
            value={simulationForm.intervalSeconds}
            onChange={(value) => setSimulationForm((form) => ({ ...form, intervalSeconds: value }))}
          />
          <label className="ui-v2-field">
            <span>{copy('simulation.seed')}</span>
            <input
              className="ui-v2-input"
              value={simulationForm.seed}
              onChange={(event) => setSimulationForm((form) => ({ ...form, seed: event.target.value }))}
            />
          </label>
          <label className="ui-v2-field">
            <span>{copy('simulation.degradation')}</span>
            <select
              className="ui-v2-select"
              value={simulationForm.degradationProfile || 'none'}
              onChange={(event) => setSimulationForm((form) => ({ ...form, degradationProfile: event.target.value }))}
            >
              {degradationProfiles.map((profile) => (
                <option key={profile} value={profile}>
                  {profile}
                </option>
              ))}
            </select>
          </label>
          <label className="ui-v2-field">
            <span>{copy('simulation.runLabel')}</span>
            <input
              className="ui-v2-input"
              value={simulationForm.runLabel}
              onChange={(event) => setSimulationForm((form) => ({ ...form, runLabel: event.target.value }))}
            />
          </label>
          <Check
            label={copy('simulation.wait')}
            checked={simulationForm.waitForCompletion}
            onChange={(value) => setSimulationForm((form) => ({ ...form, waitForCompletion: value }))}
          />
          <Check
            label={copy('simulation.evidence')}
            checked={simulationForm.collectEvidence}
            onChange={(value) => setSimulationForm((form) => ({ ...form, collectEvidence: value }))}
          />
          <Check
            label={copy('simulation.parallel')}
            checked={simulationForm.allowParallelRun}
            onChange={(value) => setSimulationForm((form) => ({ ...form, allowParallelRun: value }))}
          />
          <button type="submit" className="ui-v2-button" disabled={!canExecuteSimulation || simulationSubmitting}>
            <PlayCircle size={16} />
            {simulationSubmitting ? copy('simulation.executing') : copy('simulation.execute')}
          </button>
          {!canExecuteSimulation && <p className="ui-v2-notice">{copy('simulation.readOnly')}</p>}
          {simulationError && <p className="ui-v2-notice ui-v2-error">{simulationError.message}</p>}
        </form>
        <section className="ui-v2-card">
          <div className="ui-v2-section-heading">
            <h3>{copy('simulation.review')}</h3>
            <StatusBadge
              label={simulationReview.resultStatus}
              state={simulationReview.resultStatus === copy('simulation.idle') ? 'partial' : 'ready'}
            />
          </div>
          <p>{simulationReview.resultMessage}</p>
          <div className="ui-v2-table-wrap">
            <table className="ui-v2-table">
              <thead>
                <tr>
                  <th>Campo</th>
                  <th>{copy('config.requested')}</th>
                  <th>{copy('config.resolved')}</th>
                  <th>{copy('config.state')}</th>
                </tr>
              </thead>
              <tbody>
                {simulationReview.fields.map((field) => (
                  <tr key={field.label}>
                    <td>{field.label}</td>
                    <td>{field.requested}</td>
                    <td>{field.resolved}</td>
                    <td>{field.state}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </section>
  );
}

function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return (
    <label className="ui-v2-field">
      <span>{label}</span>
      <input
        className="ui-v2-input"
        type="number"
        min="1"
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
      />
    </label>
  );
}

function Check({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="ui-v2-checkbox">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}
