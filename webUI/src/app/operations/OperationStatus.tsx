import { ExternalLink } from 'lucide-react';
import type { EngineeringOperationResponse } from '../types/operations';
import { operationStatusMeaning } from '../truthfulPresentation';
import { formatDurationMs, operationDurationMs } from '../utils/operationalMetrics';

export function OperationStatus({
  operation,
  compact = false,
}: {
  operation: EngineeringOperationResponse;
  compact?: boolean;
}) {
  const meaning = operationStatusMeaning(operation);

  return (
    <article className="ui-card ui-operation-card">
      <div className="ui-section-heading">
        <div>
          <p className="ui-kicker">
            {operation.category} / {operation.environment}
          </p>
          <h3>{operation.displayName}</h3>
        </div>
        <span className="ui-operation-status" data-status={operation.status}>
          {operation.status}
        </span>
      </div>
      <p className="ui-notice">{meaning}</p>
      <div className="ui-fact-list">
        <span>
          <strong>Evidence</strong>
          {operation.evidenceLevel}
        </span>
        <span>
          <strong>Ref</strong>
          {operation.ref}
        </span>
        <span>
          <strong>Pedido</strong>
          {new Date(operation.requestedAt).toLocaleString()}
        </span>
        <span>
          <strong>Duração observada</strong>
          {formatDurationMs(operationDurationMs(operation))}
        </span>
        <span>
          <strong>Pedido por</strong>
          {operation.requestedBy}
        </span>
        <span>
          <strong>Provider</strong>
          {operation.provider ?? 'não despachado'}
        </span>
        <span>
          <strong>Artefactos</strong>
          {operation.artifacts.length}
        </span>
      </div>
      {operation.limitations.length > 0 && (
        <div className="ui-notice ui-warning">
          <strong>Limitações</strong>
          <ul>
            {operation.limitations.map((limitation) => (
              <li key={limitation}>{limitation}</li>
            ))}
          </ul>
        </div>
      )}
      {operation.providerReference?.startsWith('http') && (
        <a className="ui-secondary" href={operation.providerReference} target="_blank" rel="noreferrer">
          <ExternalLink size={15} /> Abrir provider
        </a>
      )}
      {!compact && (
        <>
          <ol className="ui-timeline">
            {operation.steps.map((step) => (
              <li key={`${operation.id}-${step.sequence}`}>
                <strong>{step.name}</strong>
                <span>{step.status}</span>
                {step.detail && <small>{step.detail}</small>}
              </li>
            ))}
          </ol>
        </>
      )}
    </article>
  );
}
