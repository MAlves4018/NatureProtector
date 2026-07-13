import { PlayCircle } from 'lucide-react';
import { AreaSelector } from '../components/AreaSelector';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { toggleDegradationProfile, useUiSimulation } from '../state/SimulationContext';
import { useUiActivity } from '../state/ActivityContext';
import { executionStatusState } from '../truthfulPresentation';

export function SimulationPage() {
  const { copy } = useUiLocale();
  const { scenarios, selectedScenarioCode, setSelectedScenarioCode } = useUiActivity();
  const {
    simulationForm,
    setSimulationForm,
    simulationReview,
    simulationSubmitting,
    simulationError,
    runtimeOperation,
    canExecuteSimulation,
    runtimeLaunchAvailable,
    submitSimulation,
    degradationProfiles,
  } = useUiSimulation();

  return (
    <section className="ui-page">
      <PageHeader
        title={copy('simulation.title')}
        subtitle={copy('simulation.subtitle')}
        helpTopic="degradationProfile"
      />
      <AreaSelector compact />
      <div className="ui-two-column">
        <form
          className="ui-card ui-form"
          onSubmit={(event) => {
            event.preventDefault();
            void submitSimulation();
          }}
        >
          <label className="ui-field">
            <span>{copy('scenario.selectLabel')}</span>
            <select
              className="ui-select"
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
          <label className="ui-field">
            <span>{copy('simulation.seed')}</span>
            <input
              className="ui-input"
              value={simulationForm.seed}
              onChange={(event) => setSimulationForm((form) => ({ ...form, seed: event.target.value }))}
            />
          </label>
          <fieldset className="ui-field">
            <legend>{copy('simulation.degradation')}</legend>
            {degradationProfiles.map((profile) => {
              const checked =
                profile === 'none'
                  ? simulationForm.degradationProfiles.length === 0
                  : simulationForm.degradationProfiles.includes(profile);

              return (
                <label key={profile} className="ui-checkbox">
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(event) =>
                      setSimulationForm((form) => ({
                        ...form,
                        degradationProfiles: toggleDegradationProfile(
                          form.degradationProfiles,
                          profile,
                          event.target.checked,
                        ),
                      }))
                    }
                  />
                  <span>{profile}</span>
                </label>
              );
            })}
          </fieldset>
          <label className="ui-field">
            <span>{copy('simulation.runLabel')}</span>
            <input
              className="ui-input"
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
          <button type="submit" className="ui-button" disabled={!canExecuteSimulation || simulationSubmitting}>
            <PlayCircle size={16} />
            {simulationSubmitting ? copy('simulation.executing') : copy('simulation.execute')}
          </button>
          {!runtimeLaunchAvailable ? (
            <p className="ui-notice">
              A execução de simulações não está disponível neste build. O endpoint atual está limitado ao ambiente de
              desenvolvimento.
            </p>
          ) : (
            !canExecuteSimulation && <p className="ui-notice">{copy('simulation.readOnly')}</p>
          )}
          {simulationError && <p className="ui-notice ui-error">{simulationError.message}</p>}
        </form>
        <section className="ui-card">
          <div className="ui-section-heading">
            <h3>{copy('simulation.review')}</h3>
            <StatusBadge
              label={runtimeOperation?.state ?? simulationReview.resultStatus}
              state={executionStatusState(
                runtimeOperation?.state ?? simulationReview.resultStatus,
                copy('simulation.idle'),
              )}
            />
          </div>
          <p>{runtimeOperation?.failureDetail ?? simulationReview.resultMessage}</p>
          {runtimeOperation && (
            <dl className="ui-definition-list">
              <dt>OperationId</dt>
              <dd>{runtimeOperation.operationId}</dd>
              <dt>SimulationRunId</dt>
              <dd>{runtimeOperation.simulationRunId ?? 'Pending'}</dd>
              <dt>Processing</dt>
              <dd>{runtimeOperation.processingState}</dd>
              <dt>Accounting</dt>
              <dd>
                {runtimeOperation.accounting.processedInbox + runtimeOperation.accounting.quarantinedInbox}/
                {runtimeOperation.accounting.expectedObservations}
              </dd>
              <dt>Evidence</dt>
              <dd>{runtimeOperation.evidenceId ?? runtimeOperation.evidenceLocation ?? 'Not recorded'}</dd>
            </dl>
          )}
          <div className="ui-table-wrap">
            <table className="ui-table">
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
    <label className="ui-field">
      <span>{label}</span>
      <input
        className="ui-input"
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
    <label className="ui-checkbox">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}
