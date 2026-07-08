import { ExternalLink } from 'lucide-react';
import type { EngineeringOperationResponse } from '../types/operations';

export function OperationStatus({
  operation,
  compact = false,
}: {
  operation: EngineeringOperationResponse;
  compact?: boolean;
}) {
  return (
    <article className="ui-card ui-operation-card">
      <div className="ui-section-heading">
        <div>
          <p className="ui-kicker">
            {operation.category} / {operation.environment}
          </p>
          <h3>{operation.displayName}</h3>
        </div>
        <span className={`ui-operation-status status-${operation.status.toLowerCase()}`}>{operation.status}</span>
      </div>
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
          <strong>Provider</strong>
          {operation.provider ?? 'não despachado'}
        </span>
      </div>
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
