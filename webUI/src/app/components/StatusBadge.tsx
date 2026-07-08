import type { UiOutputState } from '../outputContext';
import type { UiTechnicalState } from '../technicalSurfaces';

export function StatusBadge({
  label,
  state = 'ready',
}: {
  label: string;
  state?: UiOutputState | UiTechnicalState | 'ready';
}) {
  const tone =
    state === 'blocked' || state === 'error' || state === 'access-denied' || state === 'no-evidence'
      ? 'ui-badge-error'
      : state === 'partial' || state === 'stale' || state === 'not-instrumented' || state === 'not-confirmed'
        ? 'ui-badge-warning'
        : 'ui-badge-ready';

  return <span className={`ui-badge ${tone}`}>{label}</span>;
}
