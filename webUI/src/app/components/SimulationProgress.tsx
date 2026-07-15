import { CheckCircle2, Circle, Loader2, XCircle } from 'lucide-react';

export type SimulationProgressState = 'pending' | 'running' | 'completed' | 'failed';

export interface SimulationProgressStep {
  id: string;
  label: string;
  state: SimulationProgressState;
  detail?: string | null;
}

// Progress is supplied by an observed backend/run source and never advanced locally.
export function SimulationProgress({ steps }: { steps: readonly SimulationProgressStep[] }) {
  return (
    <ol className="ui-timeline" aria-label="Observed simulation progress">
      {steps.map((step) => (
        <li key={step.id}>
          <span aria-hidden="true">{progressIcon(step.state)}</span>
          <strong>{step.label}</strong>
          <span>{step.state}</span>
          {step.detail && <small>{step.detail}</small>}
        </li>
      ))}
    </ol>
  );
}

function progressIcon(state: SimulationProgressState) {
  switch (state) {
    case 'running':
      return <Loader2 size={16} />;
    case 'completed':
      return <CheckCircle2 size={16} />;
    case 'failed':
      return <XCircle size={16} />;
    default:
      return <Circle size={16} />;
  }
}
