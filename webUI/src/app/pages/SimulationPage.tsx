import { Check as CheckIcon, ChevronLeft, ChevronRight, Clock3, PlayCircle } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AreaSelector } from '../components/AreaSelector';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { RunProgressCockpit } from '../components/RunProgressCockpit';
import { StatusBadge } from '../components/StatusBadge';
import { useUiLocale } from '../state/LocaleContext';
import { toggleDegradationProfile, useUiSimulation } from '../state/SimulationContext';
import { useUiActivity } from '../state/ActivityContext';
import { executionStatusState } from '../truthfulPresentation';
import { HttpError } from '../services/httpError';

export function SimulationPage() {
  const [step, setStep] = useState(0);
  const [retrySeconds, setRetrySeconds] = useState(0);
  const navigate = useNavigate();
  const { copy } = useUiLocale();
  const {
    scenarios,
    selectedScenarioCode,
    setSelectedScenarioCode,
    selectedRunId,
    runAudit,
    runTimings,
    runOperation,
    refreshSelectedRun,
  } = useUiActivity();
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
  const effectiveOperation = runtimeOperation ?? runOperation;
  const runSearch = selectedRunId ? `?runId=${encodeURIComponent(selectedRunId)}` : '';
  const steps = ['Cenário', 'Sensores', 'Duração', 'Degradações', 'Execução', 'Revisão'];
  const rateLimited = simulationError instanceof HttpError && simulationError.status === 429;

  useEffect(() => {
    if (!rateLimited) {
      setRetrySeconds(0);
      return;
    }
    setRetrySeconds(simulationError.retryAfterSeconds ?? 30);
  }, [rateLimited, simulationError]);

  useEffect(() => {
    if (retrySeconds <= 0) return;
    const timer = setInterval(() => setRetrySeconds((value) => Math.max(0, value - 1)), 1000);
    return () => clearInterval(timer);
  }, [retrySeconds]);

  return (
    <section className="ui-page">
      <PageHeader
        title={copy('simulation.title')}
        subtitle={copy('simulation.subtitle')}
        helpTopic="degradationProfile"
      />
      <AreaSelector compact />
      <section className="ui-preset-bar" aria-label="Configurações predefinidas">
        <div>
          <span className="ui-eyebrow">Presets</span>
          <strong>Comece com uma configuração defensável</strong>
        </div>
        <button
          type="button"
          className="ui-secondary"
          onClick={() =>
            setSimulationForm((form) => ({
              ...form,
              sensorCount: 2,
              numberOfCycles: 3,
              intervalSeconds: 5,
              degradationProfiles: [],
              runLabel: 'ui-nominal-quick',
            }))
          }
        >
          Nominal rápido
        </button>
        <button
          type="button"
          className="ui-secondary"
          onClick={() =>
            setSimulationForm((form) => ({
              ...form,
              sensorCount: 6,
              numberOfCycles: 5,
              intervalSeconds: 5,
              degradationProfiles: ['missing-readings'],
              collectEvidence: true,
              runLabel: 'ui-degraded-evidence',
            }))
          }
        >
          Degradado com evidence
        </button>
      </section>
      <ol className="ui-stepper" aria-label="Etapas da simulação">
        {steps.map((label, index) => (
          <li key={label} className={index === step ? 'ui-step-active' : index < step ? 'ui-step-done' : ''}>
            <button type="button" onClick={() => setStep(index)} aria-current={index === step ? 'step' : undefined}>
              <span>{index < step ? <CheckIcon size={14} /> : index + 1}</span>
              {label}
            </button>
          </li>
        ))}
      </ol>
      <div className="ui-two-column">
        <form
          className="ui-card ui-form ui-launcher"
          onSubmit={(event) => {
            event.preventDefault();
            void submitSimulation();
          }}
        >
          <section className="ui-form-section" hidden={step !== 0}>
            <span className="ui-eyebrow">1 · Cenário</span>
            <h3>Que comportamento quer observar?</h3>
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
          </section>
          <section className="ui-form-section" hidden={step !== 1}>
            <span className="ui-eyebrow">2 · Sensores</span>
            <h3>Defina a dimensão da amostra</h3>
            <NumberField
              label={copy('simulation.sensorCount')}
              value={simulationForm.sensorCount}
              onChange={(value) => setSimulationForm((form) => ({ ...form, sensorCount: value }))}
            />
          </section>
          <section className="ui-form-section" hidden={step !== 2}>
            <span className="ui-eyebrow">3 · Duração</span>
            <h3>Cadência e repetição</h3>
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
          </section>
          <section className="ui-form-section" hidden={step !== 3}>
            <span className="ui-eyebrow">4 · Degradações</span>
            <h3>Introduza falhas de forma explícita</h3>
            <fieldset className="ui-field ui-degradation-grid">
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
          </section>
          <section className="ui-form-section" hidden={step !== 4}>
            <span className="ui-eyebrow">5 · Execução e evidence</span>
            <h3>Política de execução</h3>
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
          </section>
          <section className="ui-form-section" hidden={step !== 5}>
            <span className="ui-eyebrow">6 · Revisão</span>
            <h3>Confirme antes de iniciar</h3>
            <div className="ui-review-summary">
              <ReviewFact label="Área" value={simulationReview.requested.areaCode || 'Por resolver'} />
              <ReviewFact label="Cenário" value={simulationReview.requested.scenarioCode || 'Por selecionar'} />
              <ReviewFact label="Sensores" value={String(simulationReview.requested.sensorCount ?? 'default')} />
              <ReviewFact label="Ciclos" value={String(simulationReview.requested.numberOfCycles ?? 'default')} />
            </div>
            <p className="ui-notice">
              O progresso seguinte usa apenas estado persistido da operação e da run. Etapas não instrumentadas não são
              inferidas pelo browser.
            </p>
          </section>
          <div className="ui-launcher-footer">
            <button
              type="button"
              className="ui-secondary"
              disabled={step === 0}
              onClick={() => setStep((value) => value - 1)}
            >
              <ChevronLeft size={15} /> Anterior
            </button>
            {step < steps.length - 1 && (
              <button
                type="button"
                className="ui-secondary"
                onClick={() => setStep((value) => Math.min(steps.length - 1, value + 1))}
              >
                Seguinte <ChevronRight size={15} />
              </button>
            )}
            {step === steps.length - 1 && (
              <button type="submit" className="ui-button" disabled={!canExecuteSimulation || simulationSubmitting}>
                <PlayCircle size={16} />
                {simulationSubmitting ? copy('simulation.executing') : copy('simulation.execute')}
              </button>
            )}
          </div>
          {!runtimeLaunchAvailable ? (
            <p className="ui-notice">
              A execução de simulações não está disponível neste build. O endpoint atual está limitado ao ambiente de
              desenvolvimento.
            </p>
          ) : (
            !canExecuteSimulation && <p className="ui-notice">{copy('simulation.readOnly')}</p>
          )}
          {simulationError &&
            (rateLimited ? (
              <p className="ui-notice ui-warning" role="status">
                <Clock3 size={16} /> Limite de pedidos atingido. Tente novamente em {retrySeconds}s.
              </p>
            ) : (
              <p className="ui-notice ui-error">{simulationError.message}</p>
            ))}
        </form>
        <section className="ui-card ui-review-panel">
          <div className="ui-section-heading">
            <h3>{copy('simulation.review')}</h3>
            <StatusBadge
              label={effectiveOperation?.state ?? simulationReview.resultStatus}
              state={executionStatusState(
                effectiveOperation?.state ?? simulationReview.resultStatus,
                copy('simulation.idle'),
              )}
            />
          </div>
          <p>{effectiveOperation?.failureDetail ?? simulationReview.resultMessage}</p>
          <div className="ui-review-summary">
            <ReviewFact label="Área" value={simulationReview.requested.areaCode || 'Por resolver'} />
            <ReviewFact label="Cenário" value={simulationReview.requested.scenarioCode || 'Por selecionar'} />
            <ReviewFact label="Sensores" value={String(simulationReview.requested.sensorCount ?? 'default')} />
            <ReviewFact label="Ciclos" value={String(simulationReview.requested.numberOfCycles ?? 'default')} />
          </div>
          {effectiveOperation && (
            <dl className="ui-definition-list">
              <dt>OperationId</dt>
              <dd>{effectiveOperation.operationId}</dd>
              <dt>SimulationRunId</dt>
              <dd>{effectiveOperation.simulationRunId ?? 'Pending'}</dd>
              <dt>Processing</dt>
              <dd>{effectiveOperation.processingState}</dd>
              <dt>Accounting</dt>
              <dd>
                {effectiveOperation.accounting.processedInbox + effectiveOperation.accounting.quarantinedInbox}/
                {effectiveOperation.accounting.expectedObservations}
              </dd>
              <dt>Evidence</dt>
              <dd>{effectiveOperation.evidenceId ?? effectiveOperation.evidenceLocation ?? 'Not recorded'}</dd>
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
          {(effectiveOperation?.simulationRunId || runAudit) && (
            <div className="ui-button-row">
              <button type="button" className="ui-secondary" onClick={() => navigate(`/runs${runSearch}`)}>
                Abrir resultados
              </button>
              <button type="button" className="ui-secondary" onClick={() => navigate(`/scenario-compare${runSearch}`)}>
                Comparar cenários
              </button>
              <button type="button" className="ui-secondary" onClick={() => navigate(`/evidence${runSearch}`)}>
                Abrir evidence
              </button>
            </div>
          )}
        </section>
      </div>
      {(effectiveOperation || runAudit) && (
        <RunProgressCockpit
          operation={effectiveOperation}
          audit={runAudit}
          timings={runTimings}
          selectedRunId={selectedRunId}
          onRefresh={refreshSelectedRun}
        />
    )
  }
    </section >
  );
}

function ReviewFact({ label, value }: { label: string; value: string }) {
  return (
    <span>
      <small>{label}</small>
      <strong>{value}</strong>
    </span>
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
