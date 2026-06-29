import { ExternalLink } from 'lucide-react';
import type { EngineeringOperationResponse } from '../../types';

export function OperationStatus({
  operation,
  compact = false,
}: {
  operation: EngineeringOperationResponse;
  compact?: boolean;
}) {
  return (
    <article className="ui-v2-card ui-v2-operation-card">
      <div className="ui-v2-section-heading">
        <div>
          <p className="ui-v2-kicker">
            {operation.category} / {operation.environment}
          </p>
          <h3>{operation.displayName}</h3>
        </div>
        <span className={`ui-v2-operation-status status-${operation.status.toLowerCase()}`}>{operation.status}</span>
      </div>
      <div className="ui-v2-fact-list">
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
          <strong>Provider</strong>
          {operation.provider ?? 'não despachado'}
        </span>
      </div>
      {operation.providerReference?.startsWith('http') && (
        <a className="ui-v2-secondary" href={operation.providerReference} target="_blank" rel="noreferrer">
          <ExternalLink size={15} /> Abrir provider
        </a>
      )}
      {!compact && (
        <>
          <ol className="ui-v2-timeline">
            {operation.steps.map((step) => (
              <li key={`${operation.id}-${step.sequence}`}>
                <strong>{step.name}</strong>
                <span>{step.status}</span>
                {step.detail && <small>{step.detail}</small>}
              </li>
            ))}
          </ol>
          {operation.limitations.length > 0 && (
            <div className="ui-v2-notice">
              <strong>Limitações</strong>
              <ul>
                {operation.limitations.map((limitation) => (
                  <li key={limitation}>{limitation}</li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </article>
  );
}
