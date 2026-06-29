import { useMemo, useState } from 'react';
import { Play, ShieldAlert } from 'lucide-react';
import type { OperationDefinitionResponse, StartOperationRequest } from '../../types';
import { useOperations } from './OperationsContext';

export function OperationLauncher({ definition }: { definition: OperationDefinitionResponse }) {
  const { start } = useOperations();
  const initialInputs = useMemo(
    () => Object.fromEntries(definition.inputs.map((input) => [input.name, input.defaultValue ?? ''])),
    [definition.inputs],
  );
  const [environment, setEnvironment] = useState(definition.environments[0] ?? 'ci');
  const [inputs, setInputs] = useState<Record<string, string>>(initialInputs);
  const [confirmation, setConfirmation] = useState('');
  const [collectEvidence, setCollectEvidence] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const expectedConfirmation = confirmationPhrase(definition, environment, inputs);
  const enabled = definition.availability === 'implemented' && definition.authorized;

  const submit = async () => {
    setSubmitting(true);
    setMessage(null);
    const request: StartOperationRequest = {
      operationId: definition.operationId,
      environment,
      ref: inputs.ref || 'master',
      inputs,
      collectEvidence,
      confirmation: definition.requiresConfirmation ? confirmation : null,
    };
    try {
      const operation = await start(request);
      setMessage(`Operação ${operation.id} registada com estado ${operation.status}.`);
    } catch (value) {
      setMessage(value instanceof Error ? value.message : 'Não foi possível iniciar a operação.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <article className="ui-v2-card ui-v2-operation-launcher">
      <div className="ui-v2-section-heading">
        <div>
          <p className="ui-v2-kicker">{definition.operationId}</p>
          <h3>{definition.displayName}</h3>
        </div>
        <span className="ui-v2-risk" data-risk={definition.riskLevel}>
          {definition.riskLevel}
        </span>
      </div>
      <p>{definition.description}</p>
      <div className="ui-v2-fact-list">
        <span>
          <strong>Capability</strong>
          {definition.requiredCapability}
        </span>
        <span>
          <strong>Workflow</strong>
          {definition.workflow}
        </span>
        <span>
          <strong>Evidence</strong>
          {definition.evidenceLevel}
        </span>
        <span>
          <strong>Approval</strong>
          {definition.requiresApproval ? 'obrigatória' : 'não'}
        </span>
      </div>
      <label className="ui-v2-field">
        <span>Ambiente</span>
        <select value={environment} onChange={(event) => setEnvironment(event.target.value)}>
          {definition.environments.map((item) => (
            <option key={item} value={item}>
              {item}
            </option>
          ))}
        </select>
      </label>
      {definition.inputs.map((input) => (
        <label className="ui-v2-field" key={input.name}>
          <span>
            {input.name}
            {input.required ? ' *' : ''}
          </span>
          <small>{input.description}</small>
          <input
            value={inputs[input.name] ?? ''}
            onChange={(event) => setInputs((current) => ({ ...current, [input.name]: event.target.value }))}
          />
        </label>
      ))}
      <label className="ui-v2-check-row">
        <input
          type="checkbox"
          checked={collectEvidence}
          onChange={(event) => setCollectEvidence(event.target.checked)}
        />
        Recolher evidence
      </label>
      {definition.requiresConfirmation && (
        <label className="ui-v2-field">
          <span>Confirmação exata</span>
          <code>{expectedConfirmation}</code>
          <input value={confirmation} onChange={(event) => setConfirmation(event.target.value)} />
        </label>
      )}
      {!enabled && (
        <div className="ui-v2-notice ui-v2-warning">
          <ShieldAlert size={16} />
          <span>{definition.limitation ?? definition.availability}</span>
        </div>
      )}
      <button
        type="button"
        className="ui-v2-button"
        disabled={!enabled || submitting || (definition.requiresConfirmation && confirmation !== expectedConfirmation)}
        onClick={submit}
      >
        <Play size={16} />{' '}
        {submitting ? 'A registar…' : definition.requiresApproval ? 'Pedir aprovação' : 'Executar operação'}
      </button>
      {message && <p className="ui-v2-notice">{message}</p>}
    </article>
  );
}

function confirmationPhrase(
  definition: OperationDefinitionResponse,
  environment: string,
  inputs: Record<string, string>,
) {
  return definition.confirmationTemplate
    .replace('{environment}', environment)
    .replace('{planHash}', inputs.planHash || '<missing-plan-hash>');
}
