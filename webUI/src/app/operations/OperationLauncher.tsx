import { useMemo, useState } from 'react';
import { ClipboardCheck, ShieldAlert } from 'lucide-react';
import type { OperationDefinitionResponse, StartOperationRequest } from '../types/operations';
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
      setMessage(
        `Pedido ${operation.id} registado com estado ${operation.status}. Este estado não prova conclusão sem resultado terminal e evidence verificável.`,
      );
    } catch (value) {
      setMessage(value instanceof Error ? value.message : 'Não foi possível registar o pedido de operação.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <article className="ui-card ui-operation-launcher">
      <div className="ui-section-heading">
        <div>
          <p className="ui-kicker">{definition.operationId}</p>
          <h3>{definition.displayName}</h3>
        </div>
        <span className="ui-risk" data-risk={definition.riskLevel}>
          {definition.riskLevel}
        </span>
      </div>
      <p>{definition.description}</p>
      <div className="ui-notice ui-warning">
        <ShieldAlert size={16} />
        <span>
          This control registers a closed operation request. Catalog availability does not prove dispatcher readiness,
          provider execution or terminal success. A Queued result means dispatch/request tracking only.
        </span>
      </div>
      <div className="ui-fact-list">
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
      <label className="ui-field">
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
        <label className="ui-field" key={input.name}>
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
      <label className="ui-check-row">
        <input
          type="checkbox"
          checked={collectEvidence}
          onChange={(event) => setCollectEvidence(event.target.checked)}
        />
        Recolher evidence
      </label>
      {definition.requiresConfirmation && (
        <label className="ui-field">
          <span>Confirmação exata</span>
          <code>{expectedConfirmation}</code>
          <input value={confirmation} onChange={(event) => setConfirmation(event.target.value)} />
        </label>
      )}
      {!enabled && (
        <div className="ui-notice ui-warning">
          <ShieldAlert size={16} />
          <span>{definition.availability}</span>
        </div>
      )}
      <button
        type="button"
        className="ui-button"
        disabled={!enabled || submitting || (definition.requiresConfirmation && confirmation !== expectedConfirmation)}
        onClick={submit}
      >
        <ClipboardCheck size={16} />{' '}
        {submitting
          ? 'A registar pedido…'
          : definition.requiresApproval
            ? 'Registar pedido de aprovação'
            : 'Registar pedido de operação'}
      </button>
      {message && <p className="ui-notice">{message}</p>}
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
